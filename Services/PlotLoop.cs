using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using SPS.App.Models;

namespace SPS.App.Services;

public sealed class PlotLoop : IDisposable
{
    private static readonly SensorType[] SensorOrder =
    {
        SensorType.Imu,
        SensorType.Fsr,
        SensorType.Strain,
        SensorType.Emg
    };

    private readonly SignalEngine _engine;
    private readonly FftService _fftService;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<SensorType, double[]> _signalBuffers;
    private readonly Dictionary<SensorType, FftBlock> _spectra;
    private readonly Dictionary<SensorType, PlotLoopFrame.AxisRange> _axisRanges;
    private readonly Dictionary<SensorType, double[]> _scratchBuffers;
    private readonly PlotLoopFrame _frame;
    private readonly TimeSpan _fftInterval = TimeSpan.FromMilliseconds(100);
    private DateTime _lastFft = DateTime.MinValue;
    private const double InvalidRatioThreshold = 0.01;
    private const double ClipLimit = 1_000_000.0;

    public PlotLoop(SignalEngine engine, FftService fftService, TimeSpan? window = null, TimeSpan? refreshInterval = null)
    {
        _engine = engine;
        _fftService = fftService;

        var windowSpan = window ?? TimeSpan.FromSeconds(5);
        WindowSeconds = windowSpan.TotalSeconds;
        WindowSampleCount = Math.Max(1, (int)Math.Ceiling(_engine.SampleRate * WindowSeconds));

        _signalBuffers = SensorOrder.ToDictionary(sensor => sensor, _ => new double[WindowSampleCount]);
        _spectra = SensorOrder.ToDictionary(sensor => sensor, _ => FftBlock.Empty(_engine.SampleRate));
        _axisRanges = SensorOrder.ToDictionary(sensor => sensor, _ => PlotLoopFrame.AxisRange.Create(0, 0));
        _scratchBuffers = SensorOrder.ToDictionary(sensor => sensor, _ => new double[WindowSampleCount]);
        _frame = new PlotLoopFrame(_signalBuffers, _spectra, _axisRanges, WindowSeconds);
        SetDefaultRanges();

        _timer = new DispatcherTimer
        {
            Interval = refreshInterval ?? TimeSpan.FromMilliseconds(33)
        };
        _timer.Tick += OnTick;
    }

    public event EventHandler<PlotLoopFrame>? FrameReady;

    public double WindowSeconds { get; }

    public int WindowSampleCount { get; }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Reset()
    {
        foreach (var buffer in _signalBuffers.Values)
        {
            Array.Clear(buffer, 0, buffer.Length);
        }

        foreach (var sensor in SensorOrder)
        {
            _spectra[sensor] = FftBlock.Empty(_engine.SampleRate);
        }

        _lastFft = DateTime.MinValue;
        _frame.Elapsed = 0;
        _frame.SamplesInWindow = 0;
        _frame.SamplePeriod = _engine.SamplePeriod;
        _frame.BufferFill = 0;
        _frame.WindowStart = 0;
        _frame.WindowEnd = WindowSeconds;
        SetDefaultRanges();
        FrameReady?.Invoke(this, _frame);
    }

    public void RequestImmediateFrame() => OnTick(this, EventArgs.Empty);

    public void Dispose()
    {
        _timer.Tick -= OnTick;
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _engine.PumpToWallClock();

        foreach (var sensor in SensorOrder)
        {
            _engine.CopyLatest(sensor, _signalBuffers[sensor].AsSpan());
        }

        double elapsed = _engine.Elapsed;
        double samplePeriod = _engine.SamplePeriod;
        int producedSamples = (int)Math.Min(_engine.ProducedSamples, int.MaxValue);
        int samplesInWindow = Math.Min(WindowSampleCount, Math.Max(producedSamples, 0));

        _frame.Elapsed = elapsed;
        _frame.SamplePeriod = samplePeriod;
        _frame.SamplesInWindow = samplesInWindow;
        _frame.BufferFill = _engine.BufferFill;
        _frame.WindowStart = 0;
        _frame.WindowEnd = WindowSeconds;

        foreach (var sensor in SensorOrder)
        {
            var range = ProcessSensorWindow(sensor, _signalBuffers[sensor], samplesInWindow);
            _frame.SetAxisRange(sensor, range);
        }

        var now = DateTime.UtcNow;
        if ((now - _lastFft) >= _fftInterval)
        {
            foreach (var sensor in SensorOrder)
            {
                _spectra[sensor] = _fftService.Compute(_signalBuffers[sensor], _engine.SampleRate, sensor, maxHz: 10);
            }

        _lastFft = now;
        }

        FrameReady?.Invoke(this, _frame);
    }

    private PlotLoopFrame.AxisRange ProcessSensorWindow(SensorType sensor, double[] buffer, int samplesInWindow)
    {
        if (samplesInWindow <= 0)
        {
            return GetDefaultRange(sensor);
        }

        int tailStart = Math.Max(buffer.Length - samplesInWindow, 0);
        var span = buffer.AsSpan(tailStart, samplesInWindow);
        SanitizeWindow(sensor, span, samplesInWindow);
        return ComputeRobustRange(sensor, span, samplesInWindow);
    }

    private void SanitizeWindow(SensorType sensor, Span<double> data, int sampleCount)
    {
        if (sampleCount == 0 || data.IsEmpty)
        {
            return;
        }

        int invalid = 0;
        bool clipped = false;

        for (int i = 0; i < data.Length; i++)
        {
            double value = data[i];
            if (!double.IsFinite(value))
            {
                data[i] = 0.0;
                invalid++;
                continue;
            }

            if (Math.Abs(value) > ClipLimit)
            {
                data[i] = Math.Sign(value) * ClipLimit;
                invalid++;
                clipped = true;
            }
        }

        if (clipped)
        {
            Debug.WriteLine($"[PlotLoop] {sensor} values clipped to ±{ClipLimit:E1}.");
        }

        if (invalid > sampleCount * InvalidRatioThreshold)
        {
            double ratio = invalid / (double)sampleCount;
            Debug.WriteLine($"[PlotLoop] {sensor} sanitized {ratio:P1} samples in current window.");
        }
    }

    private PlotLoopFrame.AxisRange ComputeRobustRange(SensorType sensor, ReadOnlySpan<double> data, int sampleCount)
    {
        double minSpan = GetMinimumSpan(sensor);
        if (sampleCount == 0 || data.IsEmpty)
        {
            double half = minSpan / 2.0;
            return PlotLoopFrame.AxisRange.Create(-half, half);
        }

        Span<double> scratch = _scratchBuffers[sensor].AsSpan(0, sampleCount);
        data.CopyTo(scratch);
        scratch.Sort();

        double lower = GetPercentile(scratch, 0.05);
        double upper = GetPercentile(scratch, 0.95);
        if (double.IsNaN(lower) || double.IsNaN(upper))
        {
            double halfFallback = minSpan / 2.0;
            return PlotLoopFrame.AxisRange.Create(-halfFallback, halfFallback);
        }

        if (upper < lower)
        {
            (lower, upper) = (upper, lower);
        }

        double span = upper - lower;
        if (span < 1e-6)
        {
            double median = GetPercentile(scratch, 0.5);
            double half = minSpan / 2.0;
            return PlotLoopFrame.AxisRange.Create(median - half, median + half);
        }

        double margin = span * 0.1;
        double min = lower - margin;
        double max = upper + margin;
        double adjustedSpan = max - min;
        if (adjustedSpan < minSpan)
        {
            double center = (min + max) / 2.0;
            double half = minSpan / 2.0;
            min = center - half;
            max = center + half;
        }

        return PlotLoopFrame.AxisRange.Create(min, max);
    }

    private static double GetPercentile(ReadOnlySpan<double> sorted, double percentile)
    {
        if (sorted.Length == 0)
        {
            return double.NaN;
        }

        if (sorted.Length == 1)
        {
            return sorted[0];
        }

        double position = percentile * (sorted.Length - 1);
        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);
        double fraction = position - lowerIndex;
        double lower = sorted[Math.Clamp(lowerIndex, 0, sorted.Length - 1)];
        double upper = sorted[Math.Clamp(upperIndex, 0, sorted.Length - 1)];
        return lower + (upper - lower) * fraction;
    }

    private static double GetMinimumSpan(SensorType sensor) => sensor switch
    {
        SensorType.Imu => 1.0,
        SensorType.Fsr => 0.5,
        SensorType.Strain => 0.02,
        SensorType.Emg => 1.0,
        _ => 1.0
    };

    private static PlotLoopFrame.AxisRange GetDefaultRange(SensorType sensor)
    {
        double half = GetMinimumSpan(sensor) / 2.0;
        return PlotLoopFrame.AxisRange.Create(-half, half);
    }

    private void SetDefaultRanges()
    {
        foreach (var sensor in SensorOrder)
        {
            _axisRanges[sensor] = GetDefaultRange(sensor);
        }
    }
}

