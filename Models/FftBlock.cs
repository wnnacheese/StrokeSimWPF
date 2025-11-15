using System;

namespace SPS.App.Models;

public sealed class FftBlock
{
    public FftBlock(double[] frequency, double[] magnitude, double[] magnitudeDb, double sampleRate)
    {
        Frequency = frequency ?? Array.Empty<double>();
        Magnitude = magnitude ?? Array.Empty<double>();
        MagnitudeDb = magnitudeDb ?? Array.Empty<double>();
        SampleRate = sampleRate;

        if (MagnitudeDb.Length == 0)
        {
            PeakFrequency = 0;
            PeakMagnitude = double.NegativeInfinity;
            PeakIndex = -1;
            return;
        }

        int index = 0;
        double value = MagnitudeDb[0];
        for (int i = 1; i < MagnitudeDb.Length; i++)
        {
            if (MagnitudeDb[i] > value)
            {
                value = MagnitudeDb[i];
                index = i;
            }
        }

        PeakIndex = index;
        PeakFrequency = index < Frequency.Length ? Frequency[index] : 0;
        PeakMagnitude = value;
    }

    public static FftBlock Empty(double sampleRate) =>
        new(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), sampleRate);

    public double[] Frequency { get; }

    public double[] Magnitude { get; }

    public double[] MagnitudeDb { get; }

    public double SampleRate { get; }

    public double PeakFrequency { get; }

    public double PeakMagnitude { get; }

    public int PeakIndex { get; }
}
