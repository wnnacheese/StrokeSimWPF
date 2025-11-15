using System;
using ScottPlot;
using SPS.App.Services;

namespace SPS.App.ViewModels.Plots;

public abstract class PlotViewModelBase : ObservableObject
{
    private double? _fixedXMin;
    private double? _fixedXMax;
    private double? _fixedYMin;
    private double? _fixedYMax;
    private Action<Plot>? _configurePlot;

    protected PlotViewModelBase(string title, string xLabel, string yLabel)
    {
        Title = title;
        XLabel = xLabel;
        YLabel = yLabel;
    }

    public string Title { get; }

    public string XLabel { get; }

    public string YLabel { get; }

    public event EventHandler? DataChanged;

    public void RequestRender(Plot plot)
    {
        ConfigureBase(plot);
        Render(plot);
        ApplyFixedLimits(plot);
        _configurePlot?.Invoke(plot);
    }

    public void Invalidate() => NotifyDataChanged();

    public void SetXAxisLimits(double? minimum, double? maximum)
    {
        if (_fixedXMin == minimum && _fixedXMax == maximum)
        {
            return;
        }

        _fixedXMin = minimum;
        _fixedXMax = maximum;
        NotifyDataChanged();
    }

    public void SetYAxisLimits(double? minimum, double? maximum)
    {
        if (_fixedYMin == minimum && _fixedYMax == maximum)
        {
            return;
        }

        _fixedYMin = minimum;
        _fixedYMax = maximum;
        NotifyDataChanged();
    }

    public void ConfigurePlot(Action<Plot>? configure)
    {
        _configurePlot = configure;
        NotifyDataChanged();
    }

    protected abstract void Render(Plot plot);

    protected void NotifyDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);

    private void ConfigureBase(Plot plot)
    {
        plot.Clear();
        plot.Title(Title);
        plot.XLabel(XLabel);
        plot.YLabel(YLabel);
        PlotTheme.Apply(plot);
    }

    private void ApplyFixedLimits(Plot plot)
    {
        bool lockX = _fixedXMin.HasValue || _fixedXMax.HasValue;
        bool lockY = _fixedYMin.HasValue || _fixedYMax.HasValue;
        if (!lockX && !lockY)
        {
            return;
        }

        var limits = plot.Axes.GetLimits();

        if (lockX)
        {
            double min = _fixedXMin ?? limits.Left;
            double max = _fixedXMax ?? limits.Right;
            plot.Axes.SetLimitsX(min, max);
        }

        if (lockY)
        {
            double min = _fixedYMin ?? limits.Bottom;
            double max = _fixedYMax ?? limits.Top;
            plot.Axes.SetLimitsY(min, max);
        }
    }
}
