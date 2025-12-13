using System;

namespace SPS.App.Models;

public static class ImuWaveform
{
    public static double[] Generate(
        double[] time,
        double targetValue,
        double initialValue,
        double stepTime,
        double zeta,
        double omegaN)
    {
        int length = time?.Length ?? 0;
        var output = new double[length];
        if (time != null)
        {
            Generate(time, targetValue, initialValue, stepTime, zeta, omegaN, output.AsSpan());
        }
        return output;
    }

    public static void Generate(
        ReadOnlySpan<double> time,
        double targetValue,
        double initialValue,
        double stepTime,
        double zeta,
        double omegaN,
        Span<double> output)
    {
        int length = Math.Min(time.Length, output.Length);
        if (length == 0)
        {
            return;
        }

        // Clamp physical parameters to stable domains
        zeta = Math.Max(0.0, zeta);
        omegaN = Math.Max(1e-9, omegaN);

        double initialDisplacement = initialValue - targetValue;

        for (int i = 0; i < length; i++)
        {
            double tDelta = time[i] - stepTime;
            if (tDelta < 0.0)
            {
                output[i] = initialValue;
                continue;
            }

            if (zeta < 1.0)
            {
                double omegaD = omegaN * Math.Sqrt(1.0 - zeta * zeta);
                double expTerm = Math.Exp(-zeta * omegaN * tDelta);
                double cosTerm = Math.Cos(omegaD * tDelta);
                double sinTerm = Math.Sin(omegaD * tDelta);
                double zetaFactor = zeta / Math.Sqrt(1.0 - zeta * zeta);

                output[i] = targetValue + initialDisplacement * expTerm * (cosTerm + zetaFactor * sinTerm);
            }
            else if (Math.Abs(zeta - 1.0) < 1e-6)
            {
                double expTerm = Math.Exp(-omegaN * tDelta);
                output[i] = targetValue + initialDisplacement * expTerm * (1.0 + omegaN * tDelta);
            }
            else
            {
                double zetaRoot = Math.Sqrt(zeta * zeta - 1.0);
                double expTerm = Math.Exp(-zeta * omegaN * tDelta);
                double coshTerm = Math.Cosh(omegaN * zetaRoot * tDelta);
                double sinhTerm = Math.Sinh(omegaN * zetaRoot * tDelta);
                double zetaFactor = zeta / zetaRoot;

                output[i] = targetValue + initialDisplacement * expTerm * (coshTerm + zetaFactor * sinhTerm);
            }
        }
    }
}

public static class FsrWaveform
{
    private const double MinForce = 1e-6;

    public static double[] Generate(double[] time, FsrParams parameters)
    {
        int length = time?.Length ?? 0;
        var output = new double[length];
        if (length == 0 || time == null)
        {
            return output;
        }

        double forceOffset = Math.Max(parameters.ForceOffset, 0.0);
        double forceAmplitude = Math.Max(parameters.ForceAmplitude, 0.0);
        double a = Math.Max(parameters.FsrA, 1e-6);
        double b = Math.Max(parameters.FsrB, 1e-6);
        double rMin = Math.Max(parameters.FsrRmin, 0.0);
        double vcc = Math.Clamp(parameters.SupplyVoltage, 0.0, 12.0);
        double fixedResistor = Math.Max(parameters.FixedResistor, 1e-3);
        double omega = 2 * Math.PI * 1.5; // 1.5 Hz taps

        for (int i = 0; i < length; i++)
        {
            double force = forceOffset + forceAmplitude * Math.Abs(Math.Sin(omega * time[i]));
            force = Math.Max(force, MinForce);

            double resistance = 1.0 / (a * Math.Pow(force, b)) + rMin;
            double value = vcc * fixedResistor / (fixedResistor + resistance);
            output[i] = value;
        }

        return output;
    }

    public static double EstimateResistance(double _, in FsrParams parameters)
    {
        double force = Math.Max(parameters.ForceOffset + parameters.ForceAmplitude, MinForce);
        double a = Math.Max(parameters.FsrA, 1e-6);
        double b = Math.Max(parameters.FsrB, 1e-6);
        double rMin = Math.Max(parameters.FsrRmin, 0.0);
        return 1.0 / (a * Math.Pow(force, b)) + rMin;
    }
}

public static class StrainWaveform
{
    public static double[] Generate(double[] time, StrainParams parameters)
    {
        var output = new double[time?.Length ?? 0];
        if (output.Length == 0)
        {
            return output;
        }

        double offsetMicro = Math.Max(parameters.EpsilonOffsetMicro, 0.0);
        double amplitudeMicro = Math.Max(parameters.EpsilonAmplitudeMicro, 0.0);
        double gaugeFactor = Math.Max(parameters.GaugeFactor, 0.0);
        double excitation = Math.Clamp(parameters.ExcitationVoltage, 0.0, 10.0);
        double strain = (offsetMicro + amplitudeMicro) * 1e-6;
        double value = 0.25 * excitation * gaugeFactor * strain;
        Array.Fill(output, value);
        return output;
    }
}

public static class EmgWaveform
{
    public static double[] Generate(double[] time, EmgParams parameters)
    {
        var output = new double[time?.Length ?? 0];
        if (output.Length == 0)
        {
            return output;
        }

        double amplitude = Math.Max(parameters.Amplitude, 0.0);
        double activation = Math.Clamp(parameters.ActivationLevel, 0.0, 1.0);
        double value = amplitude * activation;
        Array.Fill(output, value);
        return output;
    }
}
