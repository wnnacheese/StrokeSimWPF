using System.Numerics;

namespace SPS.App.Models;

public sealed class TransferFunction
{
    public TransferFunction(string name, SensorType sensor, double[] analogNumerator, double[] analogDenominator)
    {
        Name = name;
        Sensor = sensor;
        AnalogNumerator = analogNumerator;
        AnalogDenominator = analogDenominator;
    }

    public string Name { get; }

    public SensorType Sensor { get; }

    public double[] AnalogNumerator { get; }

    public double[] AnalogDenominator { get; }

    public double[] DigitalNumerator { get; set; } = Array.Empty<double>();

    public double[] DigitalDenominator { get; set; } = Array.Empty<double>();

    public Complex[] AnalogPoles { get; set; } = Array.Empty<Complex>();

    public Complex[] AnalogZeros { get; set; } = Array.Empty<Complex>();

    public Complex[] DigitalPoles { get; set; } = Array.Empty<Complex>();

    public Complex[] DigitalZeros { get; set; } = Array.Empty<Complex>();
}
