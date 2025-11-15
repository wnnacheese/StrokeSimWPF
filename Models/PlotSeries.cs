using System;

namespace SPS.App.Models;

public sealed class PlotSeries
{
    public PlotSeries(string label, double[] y, string hexColor, double samplePeriod, double offset)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Y = y ?? Array.Empty<double>();
        HexColor = hexColor;
        SamplePeriod = samplePeriod;
        Offset = offset;
        Kind = PlotSeriesKind.Signal;
    }

    public PlotSeries(string label, double[] x, double[] y, string hexColor, bool marker = false)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        X = x ?? Array.Empty<double>();
        Y = y ?? Array.Empty<double>();
        HexColor = hexColor;
        Kind = marker ? PlotSeriesKind.Marker : PlotSeriesKind.Line;
    }

    public string Label { get; }

    public double[]? X { get; private set; }

    public double[] Y { get; private set; }

    public string HexColor { get; }

    public PlotSeriesKind Kind { get; }

    public double SamplePeriod { get; private set; }

    public double Offset { get; private set; }

    public void UpdateSignal(double[] y, double samplePeriod, double offset)
    {
        if (Kind != PlotSeriesKind.Signal)
        {
            throw new InvalidOperationException("Only signal series support UpdateSignal.");
        }

        Y = y ?? Array.Empty<double>();
        SamplePeriod = samplePeriod;
        Offset = offset;
    }

    public void UpdateLine(double[] x, double[] y)
    {
        if (Kind == PlotSeriesKind.Signal)
        {
            throw new InvalidOperationException("Signal series do not support UpdateLine.");
        }

        X = x ?? Array.Empty<double>();
        Y = y ?? Array.Empty<double>();
    }
}
