using SPS.App.ViewModels;

namespace SPS.App.Models;

public abstract class SensorParamsBase : ObservableObject
{
    public abstract SensorType SensorType { get; }
}

public sealed class ImuParams : SensorParamsBase
{
    private double _amplitudeDeg = 35.0;
    private double _offsetDeg = 0.0;
    private double _frequencyHz = 2.5;

    public override SensorType SensorType => SensorType.Imu;

    public double AmplitudeDeg
    {
        get => _amplitudeDeg;
        set => SetProperty(ref _amplitudeDeg, Math.Clamp(value, 0.0, 180.0));
    }

    public double OffsetDeg
    {
        get => _offsetDeg;
        set => SetProperty(ref _offsetDeg, Math.Clamp(value, -180.0, 180.0));
    }

    public double FrequencyHz
    {
        get => _frequencyHz;
        set => SetProperty(ref _frequencyHz, Math.Clamp(value, 0.0, 20.0));
    }
}

public sealed class FsrParams : SensorParamsBase
{
    private double _forceAmplitude = 20.0;
    private double _forceOffset = 2.0;
    private double _fsrA = 0.2;
    private double _fsrB = 0.8;
    private double _fsrRmin = 150.0;

    public override SensorType SensorType => SensorType.Fsr;

    public double ForceAmplitude
    {
        get => _forceAmplitude;
        set => SetProperty(ref _forceAmplitude, Math.Clamp(value, 0.0, 200.0));
    }

    public double ForceOffset
    {
        get => _forceOffset;
        set => SetProperty(ref _forceOffset, Math.Clamp(value, 0.0, 200.0));
    }

    public double FsrA
    {
        get => _fsrA;
        set => SetProperty(ref _fsrA, Math.Clamp(value, 0.0, 2.0));
    }

    public double FsrB
    {
        get => _fsrB;
        set => SetProperty(ref _fsrB, Math.Clamp(value, 0.0, 2.5));
    }

    public double FsrRmin
    {
        get => _fsrRmin;
        set => SetProperty(ref _fsrRmin, Math.Clamp(value, 0.0, 5_000.0));
    }
}

public sealed class StrainParams : SensorParamsBase
{
    private double _epsilonOffsetMicro = 100.0;
    private double _epsilonAmplitudeMicro = 200.0;
    private double _gaugeFactor = 2.0;
    private double _excitationVoltage = 5.0;

    public override SensorType SensorType => SensorType.Strain;

    public double EpsilonOffsetMicro
    {
        get => _epsilonOffsetMicro;
        set => SetProperty(ref _epsilonOffsetMicro, Math.Clamp(value, 0.0, 1000.0));
    }

    public double EpsilonAmplitudeMicro
    {
        get => _epsilonAmplitudeMicro;
        set => SetProperty(ref _epsilonAmplitudeMicro, Math.Clamp(value, 0.0, 1000.0));
    }

    public double GaugeFactor
    {
        get => _gaugeFactor;
        set => SetProperty(ref _gaugeFactor, Math.Clamp(value, 0.0, 4.0));
    }

    public double ExcitationVoltage
    {
        get => _excitationVoltage;
        set => SetProperty(ref _excitationVoltage, Math.Clamp(value, 0.0, 10.0));
    }
}

public sealed class EmgParams : SensorParamsBase
{
    private double _amplitude = 1.0;
    private double _activationLevel = 0.5;

    public override SensorType SensorType => SensorType.Emg;

    public double Amplitude
    {
        get => _amplitude;
        set => SetProperty(ref _amplitude, Math.Clamp(value, 0.0, 5.0));
    }

    public double ActivationLevel
    {
        get => _activationLevel;
        set => SetProperty(ref _activationLevel, Math.Clamp(value, 0.0, 1.0));
    }
}
