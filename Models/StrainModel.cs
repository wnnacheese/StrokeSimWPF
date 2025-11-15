using SPS.App.ViewModels;

namespace SPS.App.Models;

public enum StrainExcitationMode
{
    LoadStep,
    Ramp,
    Vibration
}

public sealed class StrainModel : ObservableObject
{
    private double _epsilonMicro = 650;
    private double _excitationVoltage = 5.0;
    private double _gaugeFactor = 2.06;
    private double _mechanicalFrequency = 4.5;
    private double _mechanicalTau = 0.4;
    private double _noiseSigma = 0.001;
    private StrainExcitationMode _mode = StrainExcitationMode.LoadStep;
    private double _lastPatternChange;

    public double EpsilonMicro
    {
        get => _epsilonMicro;
        set => SetProperty(ref _epsilonMicro, Math.Clamp(value, 50, 2000));
    }

    public double ExcitationVoltage
    {
        get => _excitationVoltage;
        set => SetProperty(ref _excitationVoltage, Math.Clamp(value, 1, 12));
    }

    public double GaugeFactor
    {
        get => _gaugeFactor;
        set => SetProperty(ref _gaugeFactor, Math.Clamp(value, 1.5, 4));
    }

    public double MechanicalFrequency
    {
        get => _mechanicalFrequency;
        set => SetProperty(ref _mechanicalFrequency, Math.Clamp(value, 0.1, 20));
    }

    public double MechanicalTau
    {
        get => _mechanicalTau;
        set => SetProperty(ref _mechanicalTau, Math.Clamp(value, 0.05, 2));
    }

    public double NoiseSigma
    {
        get => _noiseSigma;
        set => SetProperty(ref _noiseSigma, Math.Clamp(value, 0, 0.02));
    }

    public StrainExcitationMode Mode
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
        EpsilonMicro = 650;
        ExcitationVoltage = 5.0;
        GaugeFactor = 2.06;
        MechanicalFrequency = 4.5;
        MechanicalTau = 0.4;
        NoiseSigma = 0.001;
        Mode = StrainExcitationMode.LoadStep;
        LastPatternChange = 0;
    }
}
