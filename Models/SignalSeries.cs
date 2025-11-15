namespace SPS.App.Models;

public sealed class SignalSeries
{
    public SignalSeries(double[] time, double[] samples)
    {
        Time = time;
        Samples = samples;
    }

    public double[] Time { get; }

    public double[] Samples { get; }

    public double Mean => Samples.Length == 0 ? 0 : Samples.Average();
}
