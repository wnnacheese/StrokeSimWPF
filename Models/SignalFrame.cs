using System.Collections.Generic;

namespace SPS.App.Models;

public sealed class SignalFrame
{
    public required double[] Time { get; init; }

    public required Dictionary<SensorType, double[]> Signals { get; init; }

    public required Dictionary<SensorType, FftBlock> Spectra { get; init; }

    public required IReadOnlyList<TransferFunction> TransferFunctions { get; init; }

    public double BufferFill { get; init; }

    public double SampleRate { get; init; }
}
