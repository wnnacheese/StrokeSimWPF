namespace SPS.App.Models;

public sealed class ParameterState
{
    public ImuState Imu { get; set; } = new();
    public FsrState Fsr { get; set; } = new();
    public StrainState Strain { get; set; } = new();
    public EmgState Emg { get; set; } = new();
    public double ImuWeight { get; set; } = 1.0;
    public double FsrWeight { get; set; } = 1.0;
    public double StrainWeight { get; set; } = 1.0;
    public double EmgWeight { get; set; } = 1.0;
    public bool NormalizeWeights { get; set; } = true;
}

public sealed class ImuState
{
    public double AmplitudeDeg { get; set; }
    public double OffsetDeg { get; set; }
}

public sealed class FsrState
{
    public double ForceAmplitude { get; set; }
    public double ForceOffset { get; set; }
    public double FsrA { get; set; }
    public double FsrB { get; set; }
    public double FsrRmin { get; set; }
    public double SupplyVoltage { get; set; }
    public double FixedResistor { get; set; }
}

public sealed class StrainState
{
    public double EpsilonOffsetMicro { get; set; }
    public double EpsilonAmplitudeMicro { get; set; }
    public double ExcitationVoltage { get; set; }
    public double GaugeFactor { get; set; }
}

public sealed class EmgState
{
    public double Amplitude { get; set; }
    public double ActivationLevel { get; set; }
}
