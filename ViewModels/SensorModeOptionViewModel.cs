using System;

namespace SPS.App.ViewModels;

public sealed class SensorModeOptionViewModel : ObservableObject
{
    private readonly Func<Enum> _getMode;
    private readonly Action<Enum> _setMode;
    private readonly Enum _modeValue;

    public SensorModeOptionViewModel(string name, Enum modeValue, Func<Enum> modeGetter, Action<Enum> modeSetter, string? toolTip = null)
    {
        Name = name;
        _modeValue = modeValue;
        _getMode = modeGetter ?? throw new ArgumentNullException(nameof(modeGetter));
        _setMode = modeSetter ?? throw new ArgumentNullException(nameof(modeSetter));
        ToolTip = toolTip;
    }

    public string Name { get; }

    public string? ToolTip { get; }

    public bool IsSelected
    {
        get => Equals(_getMode(), _modeValue);
        set
        {
            if (value && !Equals(_getMode(), _modeValue))
            {
                _setMode(_modeValue);
                OnPropertyChanged();
            }
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsSelected));
}
