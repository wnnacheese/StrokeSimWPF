using System;

namespace SPS.App.Models;

public sealed class StateSpaceSystem
{
    public StateSpaceSystem(double[,] a, double[,] b, double[,] c, double[,] d)
    {
        A = (double[,])a.Clone();
        B = (double[,])b.Clone();
        C = (double[,])c.Clone();
        D = (double[,])d.Clone();
        Validate();
    }

    public double[,] A { get; }

    public double[,] B { get; }

    public double[,] C { get; }

    public double[,] D { get; }

    public int StateCount => A.GetLength(0);

    public int InputCount => B.GetLength(1);

    public int OutputCount => C.GetLength(0);

    public bool IsSiso => InputCount == 1 && OutputCount == 1;

    private void Validate()
    {
        if (A.GetLength(0) != A.GetLength(1))
            throw new ArgumentException("State matrix A must be square.");

        if (A.GetLength(0) != B.GetLength(0))
            throw new ArgumentException("Matrix dimensions for A and B do not align.");

        if (A.GetLength(0) != C.GetLength(1))
            throw new ArgumentException("Matrix dimensions for A and C do not align.");

        if (C.GetLength(0) != D.GetLength(0) || B.GetLength(1) != D.GetLength(1))
            throw new ArgumentException("Matrix dimensions for B, C, and D do not align.");
    }
}
