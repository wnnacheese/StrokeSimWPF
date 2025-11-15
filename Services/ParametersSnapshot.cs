using SPS.App.Models;

namespace SPS.App.Services;

public sealed class ParametersSnapshot
{
    public ImuParams? Imu { get; set; }
    public FsrParams? Fsr { get; set; }
    public StrainParams? Strain { get; set; }
    public EmgParams? Emg { get; set; }
}
