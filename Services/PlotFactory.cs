using System.Collections.Generic;
using ScottPlot;
using SPS.App.Models;
using MediaColor = System.Windows.Media.Color;

namespace SPS.App.Services;

public static class PlotFactory
{
    private static readonly Dictionary<SensorType, (Color PlotColor, MediaColor MediaColor, string Hex)> Palette = new()
    {
        { SensorType.Imu, (Color.FromHex("#4FC3F7"), (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#4FC3F7")!, "#4FC3F7") },
        { SensorType.Fsr, (Color.FromHex("#66BB6A"), (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#66BB6A")!, "#66BB6A") },
        { SensorType.Strain, (Color.FromHex("#FBC02D"), (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#FBC02D")!, "#FBC02D") },
        { SensorType.Emg, (Color.FromHex("#EF5350"), (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#EF5350")!, "#EF5350") }
    };

    public static Color GetColor(SensorType sensor) => Palette[sensor].PlotColor;

    public static MediaColor GetMediaColor(SensorType sensor) => Palette[sensor].MediaColor;

    public static string GetHex(SensorType sensor) => Palette[sensor].Hex;

    public static Color FromHex(string hex) => Color.FromHex(hex);

    public static MediaColor MediaColorFromHex(string hex) =>
        (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
}
