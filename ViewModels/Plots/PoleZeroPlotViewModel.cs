using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ScottPlot;
using MediaColor = System.Windows.Media.Color;
using SPS.App.Models;
using SPS.App.Services;

namespace SPS.App.ViewModels.Plots;

public enum PoleZeroDomain
{
    Digital,
    Analog
}

public sealed class PoleZeroPlotViewModel : PlotViewModelBase
{
    private IReadOnlyList<(Complex Point, bool IsPole, MediaColor Color)> _points = Array.Empty<(Complex, bool, MediaColor)>();
    private PoleZeroDomain _domain = PoleZeroDomain.Digital;

    public PoleZeroPlotViewModel()
        : this(PoleZeroDomain.Digital)
    {
    }

    public PoleZeroPlotViewModel(PoleZeroDomain domain)
        : base(domain == PoleZeroDomain.Digital ? "Z-Domain (Pole-Zero)" : "S-Domain (Pole-Zero)", "Real", "Imag")
    {
        _domain = domain;
    }

    public IReadOnlyList<(Complex Point, bool IsPole, MediaColor Color)> Points
    {
        get => _points;
        set
        {
            _points = value ?? Array.Empty<(Complex, bool, MediaColor)>();
            NotifyDataChanged();
        }
    }

    public PoleZeroDomain Domain
    {
        get => _domain;
        set
        {
            _domain = value;
            NotifyDataChanged();
        }
    }

    protected override void Render(Plot plot)
    {
        plot.Legend.IsVisible = false;
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
        plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();

        if (_domain == PoleZeroDomain.Digital)
        {
            plot.Axes.SetLimits(-1.25, 1.25, -1.25, 1.25);

            const int samples = 256;
            var xs = new double[samples + 1];
            var ys = new double[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                double angle = 2 * Math.PI * i / samples;
                xs[i] = Math.Cos(angle);
                ys[i] = Math.Sin(angle);
            }

            var circle = plot.Add.Scatter(xs, ys);
            circle.Color = PlotFactory.FromHex("#3E4B5C");
            circle.LineWidth = 1;
            circle.LineStyle.Pattern = LinePattern.Dashed;
            circle.MarkerSize = 0;
            circle.Label = string.Empty;
        }
        else
        {
            double maxSpan = GetAnalogSpan();
            plot.Axes.SetLimits(-maxSpan, maxSpan, -maxSpan, maxSpan);

            var xs = new[] { 0.0, 0.0 };
            var ys = new[] { -maxSpan, maxSpan };
            var axisLine = plot.Add.Scatter(xs, ys);
            axisLine.Color = PlotFactory.FromHex("#3E4B5C");
            axisLine.LineWidth = 1;
            axisLine.LineStyle.Pattern = LinePattern.Dashed;
            axisLine.MarkerSize = 0;
            axisLine.Label = string.Empty;
        }

        foreach (var group in _points.GroupBy(p => new { p.IsPole, p.Color }))
        {
            var gx = group.Select(p => p.Point.Real).ToArray();
            var gy = group.Select(p => p.Point.Imaginary).ToArray();
            if (gx.Length == 0)
            {
                continue;
            }

            var color = ToPlotColor(group.Key.Color);
            var scatter = plot.Add.Scatter(gx, gy);
            scatter.LineWidth = 0;
            scatter.MarkerSize = group.Key.IsPole ? 10 : 12;
            scatter.MarkerStyle.Shape = group.Key.IsPole ? MarkerShape.Cross : MarkerShape.OpenCircle;
            scatter.MarkerStyle.Fill.Color = group.Key.IsPole ? color : color.WithAlpha(0.25);
            scatter.MarkerStyle.Outline.Color = color;
            scatter.Label = string.Empty;
        }
    }

    private static Color ToPlotColor(MediaColor color) => Color.FromHex($"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");

    private double GetAnalogSpan()
    {
        double maxMag = 1.0;
        foreach (var (point, _, _) in _points)
        {
            double span = Math.Max(Math.Abs(point.Real), Math.Abs(point.Imaginary));
            if (span > maxMag)
            {
                maxMag = span;
            }
        }

        return Math.Max(1.0, maxMag * 1.1);
    }
}


