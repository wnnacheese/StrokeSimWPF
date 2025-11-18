using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SPS.App.DSP;
using SPS.App.Models;

namespace SPS.App.Services;

public sealed class TransformsService
{
    private static readonly IReadOnlyDictionary<SensorType, double> DefaultCombinedWeights = new Dictionary<SensorType, double>
    {
        { SensorType.Imu, 1.0 },
        { SensorType.Fsr, 1.0 },
        { SensorType.Strain, 1.0 },
        { SensorType.Emg, 1.0 }
    };
    public TransferFunction BuildImuTransfer(ImuModel imu, double sampleRate)
    {
        var numerator = new[] { imu.OmegaN * imu.OmegaN };
        var denominator = new[] { 1.0, 2.0 * imu.Zeta * imu.OmegaN, imu.OmegaN * imu.OmegaN };
        var tf = new TransferFunction("IMU Orientation Filter", SensorType.Imu, numerator, denominator);
        PopulateDerivedData(tf, sampleRate);
        return tf;
    }

    public TransferFunction BuildFsrTransfer(FsrModel fsr, double effectiveResistance, double sampleRate)
    {
        const double sensingCapFarads = 1.0e-6;
        double omegaC = 1.0 / Math.Max(effectiveResistance * sensingCapFarads, 1e-6);
        var numerator = new[] { omegaC };
        var denominator = new[] { 1.0, omegaC };
        var tf = new TransferFunction("FSR Front-End", SensorType.Fsr, numerator, denominator);
        PopulateDerivedData(tf, sampleRate);
        return tf;
    }

    public TransferFunction BuildStrainTransfer(StrainModel strain, double sampleRate)
    {
        double cutoffHz = Math.Max(strain.MechanicalFrequency * 3, 40);
        double omegaC = 2 * Math.PI * cutoffHz;
        var numerator = new[] { omegaC };
        var denominator = new[] { 1.0, omegaC };
        var tf = new TransferFunction("Strain Anti-Alias", SensorType.Strain, numerator, denominator);
        PopulateDerivedData(tf, sampleRate);
        return tf;
    }

    public TransferFunction BuildEmgTransfer(EmgModel emg, double sampleRate)
    {
        double omegaZ = 2 * Math.PI * 20;
        double omegaP = 2 * Math.PI * 450;

        if (!emg.BandpassEnabled)
        {
            var unity = new TransferFunction("EMG Unity Gain", SensorType.Emg, new[] { 1.0 }, new[] { 1.0 });
            PopulateDerivedData(unity, sampleRate);
            return unity;
        }

        var numerator = new[] { 0.0, omegaP / omegaZ };
        var denominator = new[] { 1.0, omegaP };
        var tf = new TransferFunction("EMG Band-Pass", SensorType.Emg, numerator, denominator);
        PopulateDerivedData(tf, sampleRate);
        return tf;
    }

    public double[] ComputeBodeMagnitude(TransferFunction tf, IReadOnlyList<double> frequenciesHz)
    {
        var result = new double[frequenciesHz.Count];
        for (int i = 0; i < frequenciesHz.Count; i++)
        {
            double omega = 2 * Math.PI * Math.Max(frequenciesHz[i], 1e-6);
            Complex s = Complex.ImaginaryOne * omega;
            Complex numerator = EvaluatePolynomial(tf.AnalogNumerator, s);
            Complex denominator = EvaluatePolynomial(tf.AnalogDenominator, s);
            double gain = denominator == Complex.Zero ? 0 : (numerator / denominator).Magnitude;
            result[i] = MathDsp.ToDecibels(gain);
        }

        return result;
    }

    public double[] ComputeBodeMagnitude(StateSpaceSystem discreteSystem, IReadOnlyList<double> frequenciesHz, double sampleRate)
    {
        return MathDsp.DiscreteStateSpaceMagnitude(discreteSystem.A, discreteSystem.B, discreteSystem.C, discreteSystem.D, sampleRate, frequenciesHz);
    }

    public double[] GetDefaultNormalizedWeights(IReadOnlyList<TransferFunction> transfers)
    {
        var weights = new double[transfers.Count];
        for (int i = 0; i < transfers.Count; i++)
        {
            var sensor = transfers[i].Sensor;
            if (!DefaultCombinedWeights.TryGetValue(sensor, out double weight))
            {
                weight = 0.0;
            }

            weight = Math.Max(0.0, weight);
            weights[i] = weight;
        }

        return weights;
    }

    public Complex[] GetDiscretePoles(StateSpaceSystem discreteSystem)
    {
        return MathDsp.Eigenvalues(discreteSystem.A);
    }

    public Complex[] GetAnalogPoles(StateSpaceSystem continuousSystem)
    {
        return MathDsp.Eigenvalues(continuousSystem.A);
    }

    public Complex[] GetAnalogZeros(StateSpaceSystem continuousSystem)
    {
        return MathDsp.StateSpaceZeroes(continuousSystem.A, continuousSystem.B, continuousSystem.C, continuousSystem.D);
    }

    public Complex[] GetDiscreteZeros(StateSpaceSystem discreteSystem)
    {
        return MathDsp.StateSpaceZeroes(discreteSystem.A, discreteSystem.B, discreteSystem.C, discreteSystem.D);
    }

    public (double[] numerator, double[] denominator) GetDiscreteTransferFunction(StateSpaceSystem discreteSystem)
    {
        return MathDsp.StateSpaceToTransferFunctionDiscrete(discreteSystem.A, discreteSystem.B, discreteSystem.C, discreteSystem.D);
    }

    public CombinedStability CheckDiscreteStability(double[,] ad)
    {
        const double tolerance = 1e-6;
        try
        {
            var poles = MathDsp.Eigenvalues(ad);
            if (poles.Length == 0)
            {
                return new CombinedStability(true, 0, Array.Empty<Complex>(), "No poles detected");
            }

            double maxMagnitude = double.NegativeInfinity;
            bool hasInvalid = false;
            foreach (var pole in poles)
            {
                double magnitude = double.IsNaN(pole.Magnitude) || double.IsInfinity(pole.Magnitude)
                    ? double.PositiveInfinity
                    : pole.Magnitude;
                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                }
                if (!double.IsFinite(magnitude))
                {
                    hasInvalid = true;
                }
            }

            if (hasInvalid)
            {
                return new CombinedStability(false, double.PositiveInfinity, poles, "Non-finite pole magnitude detected");
            }

            bool isStable = maxMagnitude < 1.0 - tolerance;
            string reason = isStable
                ? $"max |λ| = {maxMagnitude:F6}"
                : $"max |λ| = {maxMagnitude:F6} (≥ 1)";

            return new CombinedStability(isStable, maxMagnitude, poles, reason);
        }
        catch (Exception ex)
        {
            return new CombinedStability(false, double.PositiveInfinity, Array.Empty<Complex>(), $"Eigenvalue failure: {ex.Message}");
        }
    }

    public StateSpaceSystem? BuildCombinedContinuous(IReadOnlyList<TransferFunction> transfers, IReadOnlyList<double> weights)
    {
        if (transfers.Count == 0 || weights.Count == 0 || transfers.Count != weights.Count)
            return null;

        if (weights.All(w => Math.Abs(w) < 1e-9))
            return null;

        var continuousSystems = transfers.Select(BuildContinuousStateSpace).ToList();
        if (continuousSystems.Any(s => !s.IsSiso))
            throw new InvalidOperationException("Combined system expects all sensor models to be SISO.");

        int stateCount = continuousSystems.Sum(s => s.StateCount);
        int sensorCount = continuousSystems.Count;

        var A = new double[stateCount, stateCount];
        var B = new double[stateCount, 1];
        var offsets = new int[sensorCount];

        int offset = 0;
        for (int i = 0; i < sensorCount; i++)
        {
            var system = continuousSystems[i];
            offsets[i] = offset;
            CopyBlock(system.A, A, offset, offset);
            CopyColumn(system.B, B, offset);
            offset += system.StateCount;
        }

        var combinedC = new double[1, stateCount];
        var combinedD = new double[1, 1];
        for (int i = 0; i < sensorCount; i++)
        {
            double weight = weights[i];
            if (Math.Abs(weight) < 1e-9)
                continue;

            var system = continuousSystems[i];
            int start = offsets[i];
            for (int state = 0; state < system.StateCount; state++)
                combinedC[0, start + state] += weight * system.C[0, state];
            combinedD[0, 0] += weight * system.D[0, 0];
        }

        bool hasContribution = combinedC.Cast<double>().Any(v => Math.Abs(v) > 1e-9) || Math.Abs(combinedD[0, 0]) > 1e-9;
        if (!hasContribution)
            return null;

        return new StateSpaceSystem(A, B, combinedC, combinedD);
    }

    public StateSpaceSystem? BuildCombinedDiscrete(IReadOnlyList<TransferFunction> transfers, IReadOnlyList<double> weights, double sampleRate, bool useTustin = false)
    {
        if (transfers.Count == 0 || weights.Count == 0 || transfers.Count != weights.Count)
            return null;

        if (weights.All(w => Math.Abs(w) < 1e-9))
            return null;

        var continuous = BuildCombinedContinuous(transfers, weights);
        if (continuous == null)
            return null;

        double samplePeriod = 1.0 / sampleRate;
        var (Ad, Bd) = useTustin
            ? MathDsp.DiscretizeTustin(continuous.A, continuous.B, samplePeriod)
            : MathDsp.DiscretizeZoh(continuous.A, continuous.B, samplePeriod);

        return new StateSpaceSystem(Ad, Bd, continuous.C, continuous.D);
    }

    private StateSpaceSystem BuildContinuousStateSpace(TransferFunction transfer)
    {
        return transfer.Sensor switch
        {
            SensorType.Imu => BuildImuStateSpace(transfer),
            SensorType.Fsr => BuildFirstOrderStateSpace(transfer),
            SensorType.Strain => BuildFirstOrderStateSpace(transfer),
            SensorType.Emg => BuildEmgStateSpace(transfer),
            _ => throw new ArgumentOutOfRangeException(nameof(transfer.Sensor))
        };
    }

    private static StateSpaceSystem BuildImuStateSpace(TransferFunction transfer)
    {
        double omegaSquared = Math.Max(transfer.AnalogDenominator[^1], 1e-9);
        double omegaN = Math.Sqrt(omegaSquared);
        double dampingTerm = transfer.AnalogDenominator.Length > 2 ? transfer.AnalogDenominator[1] : 0;
        double zeta = omegaN <= 0 ? 0.7 : dampingTerm / (2.0 * omegaN);
        double gain = transfer.AnalogNumerator.Length > 0 ? transfer.AnalogNumerator[^1] / omegaSquared : 1.0;

        var A = new double[,]
        {
            { 0.0, 1.0 },
            { -omegaSquared, -2.0 * zeta * omegaN }
        };
        var B = new double[,]
        {
            { 0.0 },
            { omegaSquared }
        };
        var C = new double[,] { { gain, 0.0 } };
        var D = new double[,] { { 0.0 } };
        return new StateSpaceSystem(A, B, C, D);
    }

    private static StateSpaceSystem BuildFirstOrderStateSpace(TransferFunction transfer)
    {
        double pole = Math.Max(transfer.AnalogDenominator.Length > 1 ? transfer.AnalogDenominator[1] : 1.0, 1e-9);
        double numerator = transfer.AnalogNumerator.Length > 0 ? transfer.AnalogNumerator[^1] : pole;
        double gain = numerator / pole;

        var A = new double[,] { { -pole } };
        var B = new double[,] { { pole } };
        var C = new double[,] { { gain } };
        var D = new double[,] { { 0.0 } };
        return new StateSpaceSystem(A, B, C, D);
    }

    private static StateSpaceSystem BuildEmgStateSpace(TransferFunction transfer)
    {
        double pole = Math.Max(transfer.AnalogDenominator.Length > 1 ? transfer.AnalogDenominator[1] : 1.0, 1e-9);
        double gain = transfer.AnalogNumerator.Length > 0 ? transfer.AnalogNumerator[^1] : 1.0;

        var A = new double[,] { { -pole } };
        var B = new double[,] { { 1.0 } };
        var C = new double[,] { { gain } };
        var D = new double[,] { { 0.0 } };
        return new StateSpaceSystem(A, B, C, D);
    }

    private void PopulateDerivedData(TransferFunction tf, double sampleRate)
    {
        tf.AnalogPoles = MathDsp.PolynomialRoots(tf.AnalogDenominator);
        tf.AnalogZeros = MathDsp.PolynomialRoots(tf.AnalogNumerator);

        var (b, a) = MathDsp.BilinearTransform(tf.AnalogNumerator, tf.AnalogDenominator, sampleRate);
        tf.DigitalNumerator = b;
        tf.DigitalDenominator = a;

        tf.DigitalPoles = MathDsp.PolynomialRoots(tf.DigitalDenominator);
        tf.DigitalZeros = MathDsp.PolynomialRoots(tf.DigitalNumerator);
    }

    private static Complex EvaluatePolynomial(IReadOnlyList<double> coefficients, Complex variable)
    {
        if (coefficients.Count == 0)
            return Complex.One;

        Complex acc = Complex.Zero;
        foreach (double coefficient in coefficients)
            acc = acc * variable + coefficient;
        return acc;
    }

    private static void CopyBlock(double[,] source, double[,] destination, int row, int column)
    {
        int rows = source.GetLength(0);
        int cols = source.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                destination[row + r, column + c] = source[r, c];
    }

    private static void CopyColumn(double[,] source, double[,] destination, int rowOffset)
    {
        int rows = source.GetLength(0);
        for (int r = 0; r < rows; r++)
            destination[rowOffset + r, 0] = source[r, 0];
    }

    public readonly record struct CombinedStability(bool IsStable, double MaxMagnitude, Complex[] Poles, string Reason);
}
