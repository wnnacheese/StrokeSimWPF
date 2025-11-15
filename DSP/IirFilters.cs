using System;

namespace SPS.App.DSP;

public static class IirFilters
{
    public static double[] ApplyFirstOrder(ReadOnlySpan<double> input, ReadOnlySpan<double> b, ReadOnlySpan<double> a)
    {
        var output = new double[input.Length];
        double x1 = 0, y1 = 0;

        double b0 = b.Length > 0 ? b[0] : 0;
        double b1 = b.Length > 1 ? b[1] : 0;
        double a1 = a.Length > 1 ? a[1] : 0; // a[0] assumed 1

        for (int i = 0; i < input.Length; i++)
        {
            double x0 = input[i];
            double y0 = b0 * x0 + b1 * x1 - a1 * y1;
            output[i] = y0;
            x1 = x0;
            y1 = y0;
        }

        return output;
    }

    public static double[] ApplySecondOrder(ReadOnlySpan<double> input, ReadOnlySpan<double> b, ReadOnlySpan<double> a)
    {
        var output = new double[input.Length];
        double x1 = 0, x2 = 0;
        double y1 = 0, y2 = 0;

        double b0 = b.Length > 0 ? b[0] : 0;
        double b1 = b.Length > 1 ? b[1] : 0;
        double b2 = b.Length > 2 ? b[2] : 0;
        double a1 = a.Length > 1 ? a[1] : 0;
        double a2 = a.Length > 2 ? a[2] : 0; // a[0] assumed 1

        for (int i = 0; i < input.Length; i++)
        {
            double x0 = input[i];
            double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            output[i] = y0;
            x2 = x1;
            x1 = x0;
            y2 = y1;
            y1 = y0;
        }

        return output;
    }

    public static double[] FullWaveRectifyAndSmooth(ReadOnlySpan<double> input, double alpha)
    {
        var output = new double[input.Length];
        double state = 0;
        alpha = Math.Clamp(alpha, 0.01, 0.99);

        for (int i = 0; i < input.Length; i++)
        {
            double rectified = Math.Abs(input[i]);
            state = alpha * rectified + (1 - alpha) * state;
            output[i] = state;
        }

        return output;
    }
}
