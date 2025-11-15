using System.Windows.Input;

namespace SPS.App.ViewModels;

public sealed class SensorActionViewModel
{
    public SensorActionViewModel(string name, ICommand command)
    {
        Name = name;
        Command = command;
    }

    public string Name { get; }

    public ICommand Command { get; }
}
