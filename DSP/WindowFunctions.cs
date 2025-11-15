using System;

namespace SPS.App.DSP;

public static class WindowFunctions
{
    public static double[] Hann(int length)
    {
        if (length <= 0)
        {
            return Array.Empty<double>();
        }

        var window = new double[length];
        for (int n = 0; n < length; n++)
        {
            window[n] = 0.5 * (1 - Math.Cos((2 * Math.PI * n) / (length - 1)));
        }

        return window;
    }
}
