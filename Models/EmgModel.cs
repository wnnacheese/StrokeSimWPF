using SPS.App.ViewModels;

namespace SPS.App.Models;

public enum EmgExcitationMode
{
    ShortBurst,
    FatigueTrain
}

public sealed class EmgModel : ObservableObject
{
    private double _activationDuty = 45;
    private double _burstRate = 1.8;
    private double _attackTau = 0.08;
    private double _decayTau = 0.18;
    private double _noiseSigma = 0.35;
    private bool _bandpassEnabled = true;
    private bool _rectifierEnabled = true;
    private double _envelopeAlpha = 0.2;
    private EmgExcitationMode _mode = EmgExcitationMode.ShortBurst;
    private double _lastPatternChange;

    public double ActivationDuty
    {
        get => _activationDuty;
        set => SetProperty(ref _activationDuty, Math.Clamp(value, 5, 95));
    }

    public double BurstRate
    {
        get => _burstRate;
        set => SetProperty(ref _burstRate, Math.Clamp(value, 0.5, 8));
    }

    public double AttackTau
    {
        get => _attackTau;
        set => SetProperty(ref _attackTau, Math.Clamp(value, 0.01, 0.3));
    }

    public double DecayTau
    {
        get => _decayTau;
        set => SetProperty(ref _decayTau, Math.Clamp(value, 0.05, 0.8));
    }

    public double NoiseSigma
    {
        get => _noiseSigma;
        set => SetProperty(ref _noiseSigma, Math.Clamp(value, 0, 1.5));
    }

    public bool BandpassEnabled
    {
        get => _bandpassEnabled;
        set => SetProperty(ref _bandpassEnabled, value);
    }

    public bool RectifierEnabled
    {
        get => _rectifierEnabled;
        set => SetProperty(ref _rectifierEnabled, value);
    }

    /// <summary>
    /// Exponential smoothing factor (0-1) used for drawing the EMG envelope when rectified.
    /// </summary>
    public double EnvelopeAlpha
    {
        get => _envelopeAlpha;
        set => SetProperty(ref _envelopeAlpha, Math.Clamp(value, 0.01, 0.8));
    }

    public EmgExcitationMode Mode
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
        ActivationDuty = 45;
        BurstRate = 1.8;
        AttackTau = 0.08;
        DecayTau = 0.18;
        NoiseSigma = 0.35;
        BandpassEnabled = true;
        RectifierEnabled = true;
        EnvelopeAlpha = 0.2;
        Mode = EmgExcitationMode.ShortBurst;
        LastPatternChange = 0;
    }
}
