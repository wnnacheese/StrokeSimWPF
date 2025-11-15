using System.Collections.Generic;
using SPS.App.Models;

namespace SPS.App.Services;

public sealed class PlotLoopFrame
{
    private readonly Dictionary<SensorType, double[]> _signals;
    private readonly Dictionary<SensorType, FftBlock> _spectra;
    private readonly Dictionary<SensorType, AxisRange> _axisRanges;

    internal PlotLoopFrame(Dictionary<SensorType, double[]> signals, Dictionary<SensorType, FftBlock> spectra, Dictionary<SensorType, AxisRange> axisRanges, double windowSeconds)
    {
        _signals = signals;
        _spectra = spectra;
        _axisRanges = axisRanges;
        WindowSeconds = windowSeconds;
    }

    public IReadOnlyDictionary<SensorType, double[]> Signals => _signals;

    public IReadOnlyDictionary<SensorType, FftBlock> Spectra => _spectra;

    public IReadOnlyDictionary<SensorType, AxisRange> AxisRanges => _axisRanges;

    public double Elapsed { get; internal set; }

    public double SamplePeriod { get; internal set; }

    public int SamplesInWindow { get; internal set; }

    public double BufferFill { get; internal set; }

    public double WindowSeconds { get; }

    public double WindowStart { get; internal set; }

    public double WindowEnd { get; internal set; }

    internal void SetAxisRange(SensorType sensor, AxisRange range) => _axisRanges[sensor] = range;

    public readonly struct AxisRange
    {
        public AxisRange(double min, double max)
        {
            Min = min;
            Max = max;
        }

        public double Min { get; }

        public double Max { get; }

        public static AxisRange Create(double min, double max) => new(min, max);
    }
}
