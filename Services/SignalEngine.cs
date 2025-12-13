using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using ScottPlot;
using SPS.App.Models;

namespace SPS.App.Services;

/// <summary>
/// Deterministic physics-based signal synthesizer running at fs = 100 Hz with 5 s windows per sensor.
/// </summary>
public sealed class SignalEngine : IDisposable
{
    private const double SampleRateHz = 100.0;
    private const double SamplePeriodSeconds = 1.0 / SampleRateHz;
    private const double BufferDurationSeconds = 5.0;
    private const int SamplesPerBuffer = (int)(SampleRateHz * BufferDurationSeconds);
    private const double HardClipLimit = 1_000_000.0;
    private const double ImuDegreesLimit = 180.0;
    private const double FsrVoltageLimit = 5.0;
    private const double StrainVoltageLimit = 0.1;
    private const double EmgVoltageLimit = 5.0;

    private static readonly Lazy<bool> DeterminismGuardToken = new(RunDeterminismGuard);

    private readonly ImuModel _imuModel;
    private readonly FsrModel _fsrModel;
    private readonly StrainModel _strainModel;
    private readonly EmgModel _emgModel;
    private readonly TransformsService _transformsService;
    private readonly ParamsBus _paramsBus;

    private readonly ImuParams _imuParameters;
    private readonly FsrParams _fsrParameters;
    private readonly StrainParams _strainParameters;
    private readonly EmgParams _emgParameters;

    private readonly RingBuffer<double> _imuBuffer = new(SamplesPerBuffer);
    private readonly RingBuffer<double> _fsrBuffer = new(SamplesPerBuffer);
    private readonly RingBuffer<double> _strainBuffer = new(SamplesPerBuffer);
    private readonly RingBuffer<double> _emgBuffer = new(SamplesPerBuffer);

    private readonly double[] _timeAxis = Generate.Consecutive(
        SamplesPerBuffer,
        BufferDurationSeconds / Math.Max(SamplesPerBuffer - 1, 1),
        0.0);
    private readonly double[] _imuWorkspace = new double[SamplesPerBuffer];
    private readonly Dictionary<SensorType, PropertyChangedEventHandler> _parameterSubscriptions = new();
    private readonly object _stateLock = new();

    private bool _isRunning;
    private double _elapsedSeconds = BufferDurationSeconds;
    private long _producedSamples = SamplesPerBuffer;
    private double _fsrResistance;

    private double _latestImu;
    private double _latestFsr;
    private double _latestStrain;
    private double _latestEmg;

    private double _lastStepTime;
    private double _previousPosition;

    public SignalEngine(
        ImuModel imu,
        FsrModel fsr,
        StrainModel strain,
        EmgModel emg,
        ParametersStore parametersStore,
        TransformsService transformsService,
        ParamsBus paramsBus)
    {
        _ = DeterminismGuardToken.Value;

        _imuModel = imu ?? throw new ArgumentNullException(nameof(imu));
        _fsrModel = fsr ?? throw new ArgumentNullException(nameof(fsr));
        _strainModel = strain ?? throw new ArgumentNullException(nameof(strain));
        _emgModel = emg ?? throw new ArgumentNullException(nameof(emg));
        _transformsService = transformsService ?? throw new ArgumentNullException(nameof(transformsService));
        _paramsBus = paramsBus ?? throw new ArgumentNullException(nameof(paramsBus));
        if (parametersStore == null)
        {
            throw new ArgumentNullException(nameof(parametersStore));
        }

        _imuParameters = parametersStore.GetOrCreate(SensorType.Imu, DefaultsFactory.CreateImuParams);
        _fsrParameters = parametersStore.GetOrCreate(SensorType.Fsr, DefaultsFactory.CreateFsrParams);
        _strainParameters = parametersStore.GetOrCreate(SensorType.Strain, DefaultsFactory.CreateStrainParams);
        _emgParameters = parametersStore.GetOrCreate(SensorType.Emg, DefaultsFactory.CreateEmgParams);

        _fsrResistance = 10_000.0;

        AttachParameterSets();
        _paramsBus.Publish(ParamsChangedEventArgs.Broadcast);
    }

    public double Fs => SampleRateHz;

    public double SampleRate => Fs;

    public double SamplePeriod => SamplePeriodSeconds;

    public bool IsRunning => _isRunning;

    public double Elapsed => _elapsedSeconds;

    public long ProducedSamples => Interlocked.Read(ref _producedSamples);

    public double BufferFill =>
        (_imuBuffer.FillRatio + _fsrBuffer.FillRatio + _strainBuffer.FillRatio + _emgBuffer.FillRatio) / 4.0;

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
    }

    public void Reset()
    {
        Stop();
        RegenerateAllSensors();
    }

    public void Dispose()
    {
        Stop();
        DetachParameterSets();
    }

    public void CopyLatest(SensorType sensor, Span<double> destination)
    {
        GetBuffer(sensor).Snapshot(destination);
    }

    public double GetLatest(SensorType sensor) => sensor switch
    {
        SensorType.Imu => Volatile.Read(ref _latestImu),
        SensorType.Fsr => Volatile.Read(ref _latestFsr),
        SensorType.Strain => Volatile.Read(ref _latestStrain),
        SensorType.Emg => Volatile.Read(ref _latestEmg),
        _ => 0.0
    };

    public IReadOnlyList<TransferFunction> GetTransferFunctionsSnapshot()
    {
        lock (_stateLock)
        {
            var imu = _transformsService.BuildImuTransfer(_imuModel, SampleRateHz);
            var fsr = _transformsService.BuildFsrTransfer(_fsrModel, Math.Max(_fsrResistance, 1.0), SampleRateHz);
            var strain = _transformsService.BuildStrainTransfer(_strainModel, SampleRateHz);
            var emg = _transformsService.BuildEmgTransfer(_emgModel, SampleRateHz);

            return new[]
            {
                CloneTransfer(imu),
                CloneTransfer(fsr),
                CloneTransfer(strain),
                CloneTransfer(emg)
            };
        }
    }

    public void PumpToWallClock()
    {
        // Buffers already reflect the latest parameters; no real-time pumping required.
    }

    private void AttachParameterSets()
    {
        AttachParams(_imuParameters, SensorType.Imu, UpdateImuWaveform);
        AttachParams(_fsrParameters, SensorType.Fsr, UpdateFsrWaveform);
        AttachParams(_strainParameters, SensorType.Strain, UpdateStrainWaveform);
        AttachParams(_emgParameters, SensorType.Emg, UpdateEmgWaveform);
    }

    private void DetachParameterSets()
    {
        foreach (var kvp in _parameterSubscriptions)
        {
            var handler = kvp.Value;
            switch (kvp.Key)
            {
                case SensorType.Imu:
                    _imuParameters.PropertyChanged -= handler;
                    break;
                case SensorType.Fsr:
                    _fsrParameters.PropertyChanged -= handler;
                    break;
                case SensorType.Strain:
                    _strainParameters.PropertyChanged -= handler;
                    break;
                case SensorType.Emg:
                    _emgParameters.PropertyChanged -= handler;
                    break;
            }
        }

        _parameterSubscriptions.Clear();
    }

    private void AttachParams(INotifyPropertyChanged parameters, SensorType sensor, Action syncAction)
    {
        void Handler(object? _, PropertyChangedEventArgs __)
        {
            syncAction();
            _paramsBus.Publish(ParamsChangedEventArgs.Broadcast);
        }

        parameters.PropertyChanged += Handler;
        _parameterSubscriptions[sensor] = Handler;
        syncAction();
    }

    private void UpdateImuWaveform()
    {
        lock (_stateLock)
        {
            // Reset state for full-buffer regeneration, creating a deterministic square wave response from t=0
            _previousPosition = _imuParameters.OffsetDeg; // Start simulation from the center
            _lastStepTime = 0.0;

            // Update the underlying model for any other services that might read it
            _imuModel.AmplitudeDeg = _imuParameters.AmplitudeDeg;
            _imuModel.OmegaN = _imuParameters.OmegaN;
            _imuModel.Zeta = _imuParameters.Zeta;
            _imuModel.FrequencyHz = 0;
            _imuModel.DriftRate = 0.0;
            _imuModel.NoiseSigma = 0.0;
            _imuModel.FusionAlpha = 0.0;

            var samples = new double[SamplesPerBuffer];
            const double stepInterval = 2.0;
            int sign = 1;
            double stepTime = _lastStepTime;
            int writeStart = 0;

            while (writeStart < samples.Length)
            {
                double targetValue = _imuParameters.OffsetDeg + sign * _imuParameters.AmplitudeDeg;
                ImuWaveform.Generate(
                    _timeAxis,
                    targetValue,
                    _previousPosition,
                    stepTime,
                    _imuParameters.Zeta,
                    _imuParameters.OmegaN,
                    _imuWorkspace.AsSpan());

                double nextStepTime = stepTime + stepInterval;
                int nextStepIndex = Array.BinarySearch(_timeAxis, nextStepTime);
                if (nextStepIndex < 0)
                {
                    nextStepIndex = ~nextStepIndex;
                }

                if (nextStepIndex > samples.Length)
                {
                    nextStepIndex = samples.Length;
                }

                int count = nextStepIndex - writeStart;
                if (count > 0)
                {
                    _imuWorkspace.AsSpan(writeStart, count).CopyTo(samples.AsSpan(writeStart, count));
                }

                if (nextStepIndex > writeStart)
                {
                    _previousPosition = samples[nextStepIndex - 1];
                }

                if (nextStepIndex >= samples.Length)
                {
                    _lastStepTime = stepTime;
                    break;
                }

                stepTime = nextStepTime;
                _lastStepTime = stepTime;
                sign *= -1;
                writeStart = nextStepIndex;
            }

            _latestImu = WriteSeries(_imuBuffer, samples, ImuDegreesLimit);
        }

        MarkBuffersUpdated();
    }

    private void UpdateFsrWaveform()
    {
        lock (_stateLock)
        {
            _fsrModel.ForceAmplitude = _fsrParameters.ForceAmplitude;
            _fsrModel.BurstWidth = 0.0;
            _fsrModel.RepetitionRate = 0.0;
            _fsrModel.FixedResistor = _fsrParameters.FixedResistor;
            _fsrModel.NoiseSigma = 0.0;

            var samples = FsrWaveform.Generate(_timeAxis, _fsrParameters);
            _latestFsr = WriteSeries(_fsrBuffer, samples, FsrVoltageLimit);
            _fsrResistance = Math.Max(FsrWaveform.EstimateResistance(0.0, _fsrParameters), 1.0);
        }

        MarkBuffersUpdated();
    }

    private void UpdateStrainWaveform()
    {
        lock (_stateLock)
        {
            _strainModel.EpsilonMicro = Math.Max(_strainParameters.EpsilonAmplitudeMicro, 0.0);
            _strainModel.GaugeFactor = _strainParameters.GaugeFactor;
            _strainModel.ExcitationVoltage = _strainParameters.ExcitationVoltage;
            _strainModel.NoiseSigma = 0.0;
            _strainModel.MechanicalFrequency = 0.0;
            _strainModel.MechanicalTau = 0.0;

            var samples = StrainWaveform.Generate(_timeAxis, _strainParameters);
            _latestStrain = WriteSeries(_strainBuffer, samples, StrainVoltageLimit);
        }

        MarkBuffersUpdated();
    }

    private void UpdateEmgWaveform()
    {
        lock (_stateLock)
        {
            _emgModel.ActivationDuty = dutyPercent(_emgParameters.ActivationLevel);
            _emgModel.BurstRate = 0.0;
            _emgModel.AttackTau = 0.0;
            _emgModel.DecayTau = 0.0;
            _emgModel.NoiseSigma = 0.0;
            _emgModel.BandpassEnabled = false;
            _emgModel.RectifierEnabled = false;
            _emgModel.EnvelopeAlpha = 0.0;
            _emgModel.Mode = EmgExcitationMode.ShortBurst;

            var samples = EmgWaveform.Generate(_timeAxis, _emgParameters);
            _latestEmg = WriteSeries(_emgBuffer, samples, EmgVoltageLimit);
        }

        MarkBuffersUpdated();

        static double dutyPercent(double activationLevel) => Math.Clamp(activationLevel * 100.0, 0.0, 100.0);
    }

    public void RegenerateAllSensors()
    {
        UpdateImuWaveform();
        UpdateFsrWaveform();
        UpdateStrainWaveform();
        UpdateEmgWaveform();
    }

    private void MarkBuffersUpdated()
    {
        _elapsedSeconds = BufferDurationSeconds;
        Interlocked.Exchange(ref _producedSamples, SamplesPerBuffer);
    }

    private static double WriteSeries(RingBuffer<double> buffer, ReadOnlySpan<double> samples, double limit)
    {
        buffer.Clear();
        double latest = 0.0;

        if (samples.IsEmpty)
        {
            for (int i = 0; i < buffer.Capacity; i++)
            {
                buffer.Write(0.0);
            }

            return latest;
        }

        foreach (double sample in samples)
        {
            double sanitized = SanitizeSample(sample, limit);
            buffer.Write(sanitized);
            latest = sanitized;
        }

        return latest;
    }

    private static double SanitizeSample(double value, double limit)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        if (Math.Abs(value) > limit)
        {
            value = Math.Sign(value) * limit;
        }

        if (Math.Abs(value) > HardClipLimit)
        {
            value = Math.Sign(value) * HardClipLimit;
        }

        return value;
    }

    private RingBuffer<double> GetBuffer(SensorType sensor) => sensor switch
    {
        SensorType.Imu => _imuBuffer,
        SensorType.Fsr => _fsrBuffer,
        SensorType.Strain => _strainBuffer,
        SensorType.Emg => _emgBuffer,
        _ => throw new ArgumentOutOfRangeException(nameof(sensor))
    };

    private static TransferFunction CloneTransfer(TransferFunction tf)
    {
        return new TransferFunction(tf.Name, tf.Sensor, tf.AnalogNumerator.ToArray(), tf.AnalogDenominator.ToArray())
        {
            DigitalNumerator = tf.DigitalNumerator.ToArray(),
            DigitalDenominator = tf.DigitalDenominator.ToArray(),
            AnalogPoles = tf.AnalogPoles?.ToArray() ?? Array.Empty<System.Numerics.Complex>(),
            AnalogZeros = tf.AnalogZeros?.ToArray() ?? Array.Empty<System.Numerics.Complex>(),
            DigitalPoles = tf.DigitalPoles?.ToArray() ?? Array.Empty<System.Numerics.Complex>(),
            DigitalZeros = tf.DigitalZeros?.ToArray() ?? Array.Empty<System.Numerics.Complex>()
        };
    }

    private static bool RunDeterminismGuard()
    {
        var randomTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => t.Namespace?.StartsWith("System", StringComparison.Ordinal) == true
                        && t.Name.Contains("Random", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var targetTypes = new[]
        {
            typeof(SignalEngine),
            typeof(ImuModel),
            typeof(FsrModel),
            typeof(StrainModel),
            typeof(EmgModel),
            typeof(ImuWaveform),
            typeof(FsrWaveform),
            typeof(StrainWaveform),
            typeof(EmgWaveform),
            typeof(ImuParams),
            typeof(FsrParams),
            typeof(StrainParams),
            typeof(EmgParams)
        };

        foreach (var target in targetTypes)
        {
            foreach (var field in target.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (IsRandomType(field.FieldType, randomTypes))
                {
                    throw new InvalidOperationException($"Determinism guard: {target.FullName}.{field.Name} references {field.FieldType.FullName}.");
                }
            }

            foreach (var property in target.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (IsRandomType(property.PropertyType, randomTypes))
                {
                    throw new InvalidOperationException($"Determinism guard: {target.FullName}.{property.Name} references {property.PropertyType.FullName}.");
                }
            }
        }

        Debug.WriteLine("Determinism OK");
        return true;
    }

    private static bool IsRandomType(Type candidate, IReadOnlyList<Type> randomTypes) =>
        randomTypes.Any(rt => rt.IsAssignableFrom(candidate));

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!.Cast<Type>();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }
}

