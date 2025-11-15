
using SPS.App.Models;

namespace SPS.App.Services;

public static class DefaultsFactory
{
    public static ImuParams CreateImuParams() => new()
    {
        AmplitudeDeg = 30.0,
        OffsetDeg = 0.0,
        FrequencyHz = 2.5
    };

    public static FsrParams CreateFsrParams() => new()
    {
        ForceAmplitude = 20.0,
        ForceOffset = 2.0,
        FsrA = 0.2,
        FsrB = 0.8,
        FsrRmin = 150.0
    };

    public static StrainParams CreateStrainParams() => new()
    {
        EpsilonOffsetMicro = 100.0,
        EpsilonAmplitudeMicro = 200.0,
        GaugeFactor = 2.0,
        ExcitationVoltage = 5.0
    };

    public static EmgParams CreateEmgParams() => new()
    {
        Amplitude = 1.0,
        ActivationLevel = 0.5
    };

    public static object For(SensorType sensorType) => sensorType switch
    {
        SensorType.Imu => CreateImuParams(),
        SensorType.Fsr => CreateFsrParams(),
        SensorType.Strain => CreateStrainParams(),
        SensorType.Emg => CreateEmgParams(),
        _ => throw new ArgumentOutOfRangeException(nameof(sensorType), sensorType, null)
    };

    public static ParametersSnapshot CreateDefaultsSnapshot() => new()
    {
        Imu = CreateImuParams(),
        Fsr = CreateFsrParams(),
        Strain = CreateStrainParams(),
        Emg = CreateEmgParams()
    };
}
