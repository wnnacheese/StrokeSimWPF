using System;

namespace SPS.App.DSP;

public sealed class DigitalFilter
{
    private double[] _b = Array.Empty<double>();
    private double[] _a = Array.Empty<double>();
    private double[] _xHistory = Array.Empty<double>();
    private double[] _yHistory = Array.Empty<double>();

    public void Configure(ReadOnlySpan<double> numerator, ReadOnlySpan<double> denominator)
    {
        if (denominator.Length == 0)
        {
            throw new ArgumentException("Denominator must have at least one coefficient.", nameof(denominator));
        }

        _b = numerator.ToArray();
        _a = denominator.ToArray();

        int order = Math.Max(_b.Length, _a.Length);
        _xHistory = new double[order];
        _yHistory = new double[order];
    }

    public double Process(double input)
    {
        if (_b.Length == 0)
        {
            return input;
        }

        _xHistory[0] = input;
        double output = 0;

        for (int i = 0; i < _b.Length; i++)
        {
            output += _b[i] * _xHistory[i];
        }

        for (int i = 1; i < _a.Length; i++)
        {
            output -= _a[i] * _yHistory[i];
        }

        double a0 = _a[0];
        if (Math.Abs(a0) < 1e-12)
        {
            a0 = 1.0;
        }

        output /= a0;

        for (int i = _xHistory.Length - 1; i > 0; i--)
        {
            _xHistory[i] = _xHistory[i - 1];
        }

        for (int i = _yHistory.Length - 1; i > 0; i--)
        {
            _yHistory[i] = _yHistory[i - 1];
        }

        _yHistory[0] = output;

        return output;
    }

    public void Reset()
    {
        Array.Clear(_xHistory, 0, _xHistory.Length);
        Array.Clear(_yHistory, 0, _yHistory.Length);
    }
}
