using System;

namespace SPS.App.Models;

public static class ImuWaveform
{
    public static double[] Generate(double[] time, ImuParams parameters)
    {
        var output = new double[time?.Length ?? 0];
        if (output.Length == 0)
        {
            return output;
        }

        double amplitude = Math.Clamp(parameters.AmplitudeDeg, 0.0, 180.0);
        double offset = Math.Clamp(parameters.OffsetDeg, -180.0, 180.0);
        double frequency = Math.Max(parameters?.FrequencyHz ?? 0.0, 0.0);

        if (time == null)
        {
            double dcValue = amplitude + offset;
            Array.Fill(output, dcValue);
            return output;
        }

        if (frequency <= 0.0)
        {
            double dcValue = amplitude + offset;
            Array.Fill(output, dcValue);
            return output;
        }

        double omega = 2.0 * Math.PI * frequency;
        for (int i = 0; i < output.Length; i++)
        {
            double t = time[i];
            output[i] = offset + amplitude * Math.Sin(omega * t);
        }

        return output;
    }
}

public static class FsrWaveform
{
    private const double MinForce = 1e-6;

    public static double[] Generate(double[] time, FsrParams parameters)
    {
        var output = new double[time?.Length ?? 0];
        if (output.Length == 0)
        {
            return output;
        }

        double force = Math.Max(parameters.ForceOffset + parameters.ForceAmplitude, MinForce);
        double a = Math.Max(parameters.FsrA, 1e-6);
        double b = Math.Max(parameters.FsrB, 1e-6);
        double rMin = Math.Max(parameters.FsrRmin, 0.0);
        double vcc = Math.Clamp(parameters.SupplyVoltage, 0.0, 12.0);
        double fixedResistor = Math.Max(parameters.FixedResistor, 1e-3);

        // Fully parameter-driven FSR front-end: voltage divider from F = f(params)
        double resistance = 1.0 / (a * Math.Pow(force, b)) + rMin;
        double value = vcc * fixedResistor / (fixedResistor + resistance);
        Array.Fill(output, value);
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
