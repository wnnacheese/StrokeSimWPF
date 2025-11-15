using SPS.App.ViewModels;

namespace SPS.App.Models;

public enum FsrExcitationMode
{
    Tap,
    PressHold,
    RepeatedTaps
}

public sealed class FsrModel : ObservableObject
{
    private double _forceAmplitude = 20; // Newtons
    private double _burstWidth = 0.35;
    private double _repetitionRate = 1.5;
    private double _fixedResistor = 4700;
    private double _noiseSigma = 0.02;
    private FsrExcitationMode _mode = FsrExcitationMode.Tap;
    private double _lastPatternChange;

    public double ForceAmplitude
    {
        get => _forceAmplitude;
        set => SetProperty(ref _forceAmplitude, Math.Clamp(value, 1, 100));
    }

    public double BurstWidth
    {
        get => _burstWidth;
        set => SetProperty(ref _burstWidth, Math.Clamp(value, 0.05, 1.5));
    }

    public double RepetitionRate
    {
        get => _repetitionRate;
        set => SetProperty(ref _repetitionRate, Math.Clamp(value, 0.1, 8));
    }

    public double FixedResistor
    {
        get => _fixedResistor;
        set => SetProperty(ref _fixedResistor, Math.Clamp(value, 1000, 20000));
    }

    public double NoiseSigma
    {
        get => _noiseSigma;
        set => SetProperty(ref _noiseSigma, Math.Clamp(value, 0, 0.2));
    }

    public FsrExcitationMode Mode
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
        ForceAmplitude = 20;
        BurstWidth = 0.35;
        RepetitionRate = 1.5;
        FixedResistor = 4700;
        NoiseSigma = 0.02;
        Mode = FsrExcitationMode.Tap;
        LastPatternChange = 0;
    }
}
