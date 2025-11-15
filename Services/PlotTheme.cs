using ScottPlot;

namespace SPS.App.Services;

public static class PlotTheme
{
    private static readonly Color FigureColor = Color.FromHex("#0F141B");
    private static readonly Color DataColor = Color.FromHex("#1B202B");
    private static readonly Color GridColor = Color.FromARGB(0x3CE6EAF2);
    private static readonly Color LegendForeground = Color.FromHex("#E6EAF2");
    private static readonly Color LegendBackground = Color.FromARGB(0xA0151A23);
    private static readonly Color LegendOutline = Color.FromARGB(0x40E6EAF2);

    public static void Apply(Plot plot)
    {
        plot.FigureBackground = FigureColor;
        plot.DataBackground = DataColor;
        plot.Style.ColorGrids(GridColor);
        plot.Axes.Title.Label.ForeColor = LegendForeground;
        plot.Axes.Bottom.Label.ForeColor = LegendForeground;
        plot.Axes.Left.Label.ForeColor = LegendForeground;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = LegendForeground;
        plot.Axes.Left.TickLabelStyle.ForeColor = LegendForeground;

        var legend = plot.Legend;
        legend.IsVisible = true;
        legend.Location = Alignment.UpperLeft;
        legend.BackgroundFill.Color = LegendBackground;
        legend.OutlineStyle.Color = LegendOutline;
        legend.OutlineStyle.Width = 1;
        legend.Font.Name = "Segoe UI";
        legend.Font.Size = 12;
        legend.Font.Color = LegendForeground;

        plot.Axes.AutoScaler = new ScottPlot.AutoScalers.FractionalAutoScaler(0.05, 0.10);
    }
}
