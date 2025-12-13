
using SPS.App.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SPS.App.Services;

public sealed class ParametersStore
{
    private readonly Dictionary<SensorType, object> _store = new();
    private readonly Dictionary<SensorType, (SensorParamsBase Instance, PropertyChangedEventHandler Handler)> _subscriptions = new();
    private bool _suppressPresetTracking;
    private RunPreset _currentPreset = RunPreset.BaselineStable;

    public event EventHandler? PresetChanged;

    public RunPreset CurrentPreset => _currentPreset;

    public ImuParams Imu => GetOrCreate(SensorType.Imu, DefaultsFactory.CreateImuParams);

    public FsrParams Fsr => GetOrCreate(SensorType.Fsr, DefaultsFactory.CreateFsrParams);

    public StrainParams Strain => GetOrCreate(SensorType.Strain, DefaultsFactory.CreateStrainParams);

    public EmgParams Emg => GetOrCreate(SensorType.Emg, DefaultsFactory.CreateEmgParams);

    public T GetOrCreate<T>(SensorType sensorType, Func<T> factory)
        where T : class
    {
        if (_store.TryGetValue(sensorType, out var existing))
        {
            return (T)existing;
        }

        var created = factory();
        if (created == null)
        {
            throw new InvalidOperationException($"Factory for {sensorType} returned null.");
        }

        _store[sensorType] = created;
        AttachIfSensorParams(sensorType, created);
        return created;
    }

    public object GetOrCreate(SensorType sensorType, Func<object> factory)
    {
        if (_store.TryGetValue(sensorType, out var existing))
        {
            return existing;
        }

        var created = factory();
        _store[sensorType] = created;
        AttachIfSensorParams(sensorType, created);
        return created;
    }

    public ParametersSnapshot Snapshot()
    {
        return new ParametersSnapshot
        {
            Imu = CloneImu(Imu),
            Fsr = CloneFsr(Fsr),
            Strain = CloneStrain(Strain),
            Emg = CloneEmg(Emg)
        };
    }

    public void Override(ParametersSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        bool previousSuppress = _suppressPresetTracking;
        _suppressPresetTracking = true;
        try
        {
            if (snapshot.Imu != null)
            {
                ApplyImu(Imu, snapshot.Imu.AmplitudeDeg, snapshot.Imu.OffsetDeg, snapshot.Imu.FrequencyHz, snapshot.Imu.Zeta, snapshot.Imu.OmegaN);
            }
            if (snapshot.Fsr != null)
            {
                double supply = snapshot.Fsr.SupplyVoltage > 0 ? snapshot.Fsr.SupplyVoltage : Fsr.SupplyVoltage;
                double fixedResistor = snapshot.Fsr.FixedResistor > 0 ? snapshot.Fsr.FixedResistor : Fsr.FixedResistor;
                ApplyFsr(Fsr, snapshot.Fsr.ForceAmplitude, snapshot.Fsr.ForceOffset, snapshot.Fsr.FsrA, snapshot.Fsr.FsrB, snapshot.Fsr.FsrRmin, supply, fixedResistor);
            }
            if (snapshot.Strain != null)
            {
                ApplyStrain(Strain, snapshot.Strain.EpsilonOffsetMicro, snapshot.Strain.EpsilonAmplitudeMicro, snapshot.Strain.GaugeFactor, snapshot.Strain.ExcitationVoltage);
            }
            if (snapshot.Emg != null)
            {
                ApplyEmg(Emg, snapshot.Emg.Amplitude, snapshot.Emg.ActivationLevel);
            }
        }
        finally
        {
            _suppressPresetTracking = previousSuppress;
        }
    }

    public void ApplyPreset(RunPreset preset)
    {
        if (preset == RunPreset.Custom)
        {
            SetCurrentPreset(RunPreset.Custom, forceNotify: true);
            return;
        }

        var imu = Imu;
        var fsr = Fsr;
        var strain = Strain;
        var emg = Emg;

        _suppressPresetTracking = true;
        try
        {
            switch (preset)
            {
                case RunPreset.BaselineStable:
                    ApplyImu(imu, 15.0, 0.0, 2.5, imu.Zeta, imu.OmegaN);
                    ApplyFsr(fsr, 10.0, 5.0, 0.4, 0.8, 400.0, 3.3, 10_000.0);
                    ApplyStrain(strain, 150.0, 100.0, 2.0, 5.0);
                    ApplyEmg(emg, 0.5, 0.25);
                    break;

                case RunPreset.FastExercise:
                    ApplyImu(imu, 60.0, 5.0, 6.0, imu.Zeta, imu.OmegaN);
                    ApplyFsr(fsr, 60.0, 20.0, 0.25, 0.7, 200.0, 3.3, 10_000.0);
                    ApplyStrain(strain, 300.0, 500.0, 2.5, 7.0);
                    ApplyEmg(emg, 2.5, 0.8);
                    break;

                case RunPreset.DriftBias:
                    ApplyImu(imu, 20.0, 30.0, 0.5, imu.Zeta, imu.OmegaN);
                    ApplyFsr(fsr, 15.0, 40.0, 0.15, 1.1, 1_000.0, 3.3, 10_000.0);
                    ApplyStrain(strain, 500.0, 200.0, 3.0, 4.0);
                    ApplyEmg(emg, 1.0, 0.4);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown preset.");
            }
        }
        finally
        {
            _suppressPresetTracking = false;
        }

        SetCurrentPreset(preset, forceNotify: true);
    }

    private static void ApplyImu(ImuParams imu, double amplitudeDeg, double offsetDeg, double frequencyHz, double zeta, double omegaN)
    {
        imu.AmplitudeDeg = amplitudeDeg;
        imu.OffsetDeg = offsetDeg;
        imu.FrequencyHz = frequencyHz;
        imu.Zeta = zeta;
        imu.OmegaN = omegaN;
    }

    private static void ApplyFsr(FsrParams fsr, double forceAmplitude, double forceOffset, double a, double b, double rMin, double supplyVoltage, double fixedResistor)
    {
        fsr.ForceAmplitude = forceAmplitude;
        fsr.ForceOffset = forceOffset;
        fsr.FsrA = a;
        fsr.FsrB = b;
        fsr.FsrRmin = rMin;
        fsr.SupplyVoltage = supplyVoltage;
        fsr.FixedResistor = fixedResistor;
    }

    private static void ApplyStrain(StrainParams strain, double offsetMicro, double amplitudeMicro, double gaugeFactor, double excitationVoltage)
    {
        strain.EpsilonOffsetMicro = offsetMicro;
        strain.EpsilonAmplitudeMicro = amplitudeMicro;
        strain.GaugeFactor = gaugeFactor;
        strain.ExcitationVoltage = excitationVoltage;
    }

    private static void ApplyEmg(EmgParams emg, double amplitude, double activationLevel)
    {
        emg.Amplitude = amplitude;
        emg.ActivationLevel = activationLevel;
    }

    private void AttachIfSensorParams(SensorType sensorType, object instance)
    {
        if (instance is not SensorParamsBase sensorParams)
        {
            return;
        }

        if (_subscriptions.TryGetValue(sensorType, out var existing))
        {
            existing.Instance.PropertyChanged -= existing.Handler;
            _subscriptions.Remove(sensorType);
        }

        PropertyChangedEventHandler handler = (_, _) => OnTrackedParamsChanged();
        sensorParams.PropertyChanged += handler;
        _subscriptions[sensorType] = (sensorParams, handler);
    }

    private void OnTrackedParamsChanged()
    {
        if (_suppressPresetTracking || _currentPreset == RunPreset.Custom)
        {
            return;
        }

        SetCurrentPreset(RunPreset.Custom);
    }

    private void SetCurrentPreset(RunPreset preset, bool forceNotify = false)
    {
        if (!forceNotify && _currentPreset == preset)
        {
            return;
        }

        _currentPreset = preset;
        PresetChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ImuParams CloneImu(ImuParams source)
    {
        var clone = DefaultsFactory.CreateImuParams();
        ApplyImu(clone, source.AmplitudeDeg, source.OffsetDeg, source.FrequencyHz, source.Zeta, source.OmegaN);
        return clone;
    }

    private static FsrParams CloneFsr(FsrParams source)
    {
        var clone = DefaultsFactory.CreateFsrParams();
        ApplyFsr(clone, source.ForceAmplitude, source.ForceOffset, source.FsrA, source.FsrB, source.FsrRmin, source.SupplyVoltage, source.FixedResistor);
        return clone;
    }

    private static StrainParams CloneStrain(StrainParams source)
    {
        var clone = DefaultsFactory.CreateStrainParams();
        ApplyStrain(clone, source.EpsilonOffsetMicro, source.EpsilonAmplitudeMicro, source.GaugeFactor, source.ExcitationVoltage);
        return clone;
    }

    private static EmgParams CloneEmg(EmgParams source)
    {
        var clone = DefaultsFactory.CreateEmgParams();
        ApplyEmg(clone, source.Amplitude, source.ActivationLevel);
        return clone;
    }
}
