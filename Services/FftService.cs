using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using SPS.App.DSP;
using SPS.App.Models;

namespace SPS.App.Services;

public sealed class FftService
{
    private sealed class Cache
    {
        public int N;
        public double[] Window = Array.Empty<double>();
        public double[] Frequency = Array.Empty<double>();
        public double[] Magnitude = Array.Empty<double>();
        public double[] Db = Array.Empty<double>();
        public FftBlock Block = FftBlock.Empty(0);
        public DateTime LastRun = DateTime.MinValue;
    }

    private readonly ArrayPool<double> _doublePool = ArrayPool<double>.Shared;
    private readonly ArrayPool<Complex> _complexPool = ArrayPool<Complex>.Shared;
    private readonly Dictionary<object, Cache> _cacheByKey = new();
    private readonly object _syncRoot = new();

    public FftBlock Compute(ReadOnlySpan<double> samples, double sampleRate, object key, int maxHz = 10)
    {
        if (samples.Length == 0 || sampleRate <= 0 || double.IsNaN(sampleRate))
        {
            return FftBlock.Empty(sampleRate);
        }

        Cache cache;
        lock (_syncRoot)
        {
            if (!_cacheByKey.TryGetValue(key, out Cache? existing) || existing is null)
            {
                existing = new Cache();
                _cacheByKey[key] = existing;
            }

            cache = existing;
        }

        TimeSpan minInterval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, maxHz));
        if ((DateTime.UtcNow - cache.LastRun) < minInterval && cache.Block.MagnitudeDb.Length > 0)
        {
            return cache.Block;
        }

        int n = GetPreferredFftLength(samples.Length);
        if (cache.N != n)
        {
            cache.N = n;
            cache.Window = EnsureLength(cache.Window, n);
            for (int i = 0; i < n; i++)
            {
                cache.Window[i] = 0.5 * (1 - Math.Cos((2 * Math.PI * i) / (n - 1)));
            }

            int bins = n / 2 + 1;
            cache.Frequency = EnsureLength(cache.Frequency, bins);
            cache.Magnitude = EnsureLength(cache.Magnitude, bins);
            cache.Db = EnsureLength(cache.Db, bins);
        }

        double[] work = _doublePool.Rent(cache.N)!;
        Complex[] specArray = _complexPool.Rent(cache.N)!;

        try
        {
            Span<double> buffer = work.AsSpan(0, cache.N);
            buffer.Clear();

            int copy = Math.Min(samples.Length, cache.N);
            if (copy == 0)
            {
                return FftBlock.Empty(sampleRate);
            }

            samples.Slice(samples.Length - copy, copy).CopyTo(buffer.Slice(cache.N - copy, copy));

            double mean = 0;
            for (int i = cache.N - copy; i < cache.N; i++)
            {
                mean += buffer[i];
            }

            mean /= copy;

            for (int i = 0; i < cache.N; i++)
            {
                double detrended = buffer[i] - mean;
                double windowed = detrended * cache.Window[i];
                buffer[i] = windowed;
                specArray[i] = new Complex(windowed, 0);
            }

            Span<Complex> spectrum = specArray.AsSpan(0, cache.N);
            PerformFft(spectrum);

            int bins = cache.N / 2 + 1;
            double binWidth = sampleRate / cache.N;
            for (int i = 0; i < bins; i++)
            {
                Complex c = spectrum[i];
                double amplitude = Math.Sqrt(c.Real * c.Real + c.Imaginary * c.Imaginary) / cache.N;
                cache.Magnitude[i] = amplitude;
                cache.Db[i] = 20 * Math.Log10(Math.Max(amplitude, 1e-12));
                cache.Frequency[i] = i * binWidth;
            }

            cache.Block = new FftBlock(cache.Frequency, cache.Magnitude, cache.Db, sampleRate);
            cache.LastRun = DateTime.UtcNow;
            return cache.Block;
        }
        finally
        {
            _complexPool.Return(specArray, clearArray: false);
            _doublePool.Return(work, clearArray: true);
        }
    }

    private static double[] EnsureLength(double[] array, int length)
    {
        return array.Length == length ? array : new double[length];
    }

    private static int GetPreferredFftLength(int sampleCount)
    {
        int capped = Math.Clamp(sampleCount, 32, 4096);
        int next = MathDsp.NextPowerOfTwo(capped);
        int previous = next >> 1;
        if (previous >= 32 && Math.Abs(capped - previous) < Math.Abs(next - capped))
        {
            return previous;
        }

        return next;
    }

    private static void PerformFft(Span<Complex> buffer)
    {
        int n = buffer.Length;
        if (n <= 1)
        {
            return;
        }

        int bits = (int)Math.Log2(n);
        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
            {
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2 * Math.PI / len;
            var wLen = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                int half = len / 2;
                for (int j = 0; j < half; j++)
                {
                    var u = buffer[i + j];
                    var v = buffer[i + j + half] * w;
                    buffer[i + j] = u + v;
                    buffer[i + j + half] = u - v;
                    w *= wLen;
                }
            }
        }
    }

    private static int BitReverse(int value, int bits)
    {
        int reversed = 0;
        for (int i = 0; i < bits; i++)
        {
            reversed <<= 1;
            reversed |= value & 1;
            value >>= 1;
        }

        return reversed;
    }
}
