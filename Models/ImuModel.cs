using SPS.App.ViewModels;

namespace SPS.App.Models;

public enum ImuExcitationMode
{
    Chirp,
    Pulse,
    Step,
    DriftSpike
}

public sealed class ImuModel : ObservableObject
{
    private double _amplitudeDeg = 35;
    private double _frequencyHz = 2.5;
    private double _driftRate = 0.05;
    private double _noiseSigma = 0.6;
    private double _fusionAlpha = 0.6;
    private double _omegaN = 12.0;
    private double _zeta = 0.45;
    private ImuExcitationMode _mode = ImuExcitationMode.Chirp;
    private double _lastPatternChange;

    public double AmplitudeDeg
    {
        get => _amplitudeDeg;
        set => SetProperty(ref _amplitudeDeg, Math.Clamp(value, 1, 90));
    }

    public double FrequencyHz
    {
        get => _frequencyHz;
        set => SetProperty(ref _frequencyHz, Math.Clamp(value, 0.1, 15));
    }

    public double DriftRate
    {
        get => _driftRate;
        set => SetProperty(ref _driftRate, Math.Clamp(value, -5, 5));
    }

    public double NoiseSigma
    {
        get => _noiseSigma;
        set => SetProperty(ref _noiseSigma, Math.Clamp(value, 0, 5));
    }

    public double FusionAlpha
    {
        get => _fusionAlpha;
        set => SetProperty(ref _fusionAlpha, Math.Clamp(value, 0, 1));
    }

    public double OmegaN
    {
        get => _omegaN;
        set => SetProperty(ref _omegaN, Math.Clamp(value, 1, 40));
    }

    public double Zeta
    {
        get => _zeta;
        set => SetProperty(ref _zeta, Math.Clamp(value, 0.05, 1.5));
    }

    public ImuExcitationMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                LastPatternChange = 0;
            }
        }
    }

    public double LastPatternChange
    {
        get => _lastPatternChange;
        set => SetProperty(ref _lastPatternChange, value);
    }

    public void Reset()
    {
        AmplitudeDeg = 35;
        FrequencyHz = 2.5;
        DriftRate = 0.05;
        NoiseSigma = 0.6;
        FusionAlpha = 0.6;
        OmegaN = 12.0;
        Zeta = 0.45;
        Mode = ImuExcitationMode.Chirp;
        LastPatternChange = 0;
    }
}
