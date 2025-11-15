using System;
using System.Collections.Generic;
using ScottPlot;
using ScottPlot.DataSources;
using SPS.App.Models;
using SPS.App.Services;

namespace SPS.App.ViewModels.Plots;

public sealed class MultiSeriesPlotViewModel : PlotViewModelBase
{
    private const int MaxPoints = 2000;

    private IReadOnlyList<PlotSeries> _series = Array.Empty<PlotSeries>();
    private string? _annotationText;

    public MultiSeriesPlotViewModel(string title, string xLabel, string yLabel)
        : base(title, xLabel, yLabel)
    {
    }

    public IReadOnlyList<PlotSeries> Series
    {
        get => _series;
        set
        {
            _series = value ?? Array.Empty<PlotSeries>();
            NotifyDataChanged();
        }
    }

    public string? AnnotationText
    {
        get => _annotationText;
        set
        {
            if (_annotationText == value)
            {
                return;
            }

            _annotationText = value;
            NotifyDataChanged();
        }
    }

    protected override void Render(Plot plot)
    {
        foreach (var series in _series)
        {
            var color = PlotFactory.FromHex(series.HexColor);

            switch (series.Kind)
            {
                case PlotSeriesKind.Signal:
                    var signal = plot.Add.Signal(series.Y, series.SamplePeriod > 0 ? series.SamplePeriod : 1);
                    if (signal.Data is SignalSourceBase source)
                    {
                        source.XOffset = series.Offset;
                    }
                    signal.Color = color;
                    signal.LineWidth = 2;
                    signal.MaximumMarkerSize = 0;
                    signal.Label = series.Label;
                    break;

                case PlotSeriesKind.Line:
                    var (lineX, lineY) = Downsample(series.X ?? Array.Empty<double>(), series.Y, MaxPoints);
                    var line = plot.Add.Scatter(lineX, lineY);
                    line.Color = color;
                    line.LineWidth = 2;
                    line.MarkerSize = 6;
                    line.MarkerStyle.Fill.Color = color;
                    line.MarkerStyle.Outline.Color = color;
                    line.MarkerStyle.Shape = MarkerShape.FilledCircle;
                    line.Label = series.Label;
                    break;

                case PlotSeriesKind.Marker:
                    var marker = plot.Add.Scatter(series.X ?? Array.Empty<double>(), series.Y);
                    marker.LineWidth = 0;
                    marker.MarkerSize = 10;
                    marker.MarkerStyle.Shape = MarkerShape.OpenCircle;
                    marker.MarkerStyle.Fill.Color = color.WithAlpha(0.25);
                    marker.MarkerStyle.Outline.Color = color;
                    marker.Label = string.Empty;
                    break;
            }
        }

        plot.Axes.AutoScale();

        if (!string.IsNullOrWhiteSpace(_annotationText))
        {
            var limits = plot.Axes.GetLimits();
            double spanX = limits.Right - limits.Left;
            double spanY = limits.Top - limits.Bottom;
            double x = double.IsFinite(limits.Left) ? limits.Left + spanX * 0.01 : 0.0;
            double y = double.IsFinite(limits.Top) ? limits.Top - spanY * 0.05 : 0.0;
            plot.Add.Text(_annotationText!, x, y);
        }
    }

    private static (double[] xs, double[] ys) Downsample(double[]? xs, double[] ys, int maxPoints)
    {
        xs ??= Array.Empty<double>();
        int length = Math.Min(xs.Length, ys.Length);
        if (length <= maxPoints)
        {
            return (xs, ys);
        }

        var resultX = new double[maxPoints];
        var resultY = new double[maxPoints];
        double step = (length - 1d) / (maxPoints - 1);

        for (int i = 0; i < maxPoints; i++)
        {
            int index = (int)Math.Round(i * step);
            if (index >= length)
            {
                index = length - 1;
            }

            resultX[i] = xs[index];
            resultY[i] = ys[index];
        }

        return (resultX, resultY);
    }
}



