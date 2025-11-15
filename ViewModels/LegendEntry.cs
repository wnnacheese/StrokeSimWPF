using System.Windows.Media;

namespace SPS.App.ViewModels;

public sealed class LegendEntry
{
    public LegendEntry(string label, Color color)
    {
        Label = label;
        Color = color;
    }

    public string Label { get; }

    public Color Color { get; }
}
