using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SPS.App.DSP;

public static class MathDsp
{
    private const double Epsilon = 1e-12;

    public static double[] Hann(int length)
    {
        if (length <= 0)
            return Array.Empty<double>();

        var window = new double[length];
        for (int n = 0; n < length; n++)
            window[n] = 0.5 * (1 - Math.Cos((2 * Math.PI * n) / (length - 1)));
        return window;
    }

    public static int NextPowerOfTwo(int value)
    {
        if (value < 1)
            return 1;

        int power = 1;
        while (power < value)
            power <<= 1;
        return power;
    }

    public static double ToDecibels(double magnitude, double floorDb = -120)
    {
        double value = 20 * Math.Log10(Math.Max(magnitude, 1e-12));
        return double.IsFinite(value) ? Math.Max(value, floorDb) : floorDb;
    }

    public static (double[] numerator, double[] denominator) BilinearTransform(double[] num, double[] den, double sampleRate)
    {
        double t = 1.0 / sampleRate;
        double k = 2.0 / t;

        int order = Math.Max(num.Length, den.Length) - 1;
        if (order <= 0)
            return (new[] { 1.0 }, new[] { 1.0 });

        return order switch
        {
            1 => BilinearFirst(num, den, k),
            2 => BilinearSecond(num, den, k),
            _ => (num, den)
        };
    }

    private static (double[] numerator, double[] denominator) BilinearFirst(IReadOnlyList<double> num, IReadOnlyList<double> den, double k)
    {
        double b0 = num.Count > 1 ? num[0] : num[0];
        double b1 = num.Count > 1 ? num[1] : 0;
        double a0 = den[0];
        double a1 = den[1];

        double b0z = b0 * k + b1;
        double b1z = -b0 * k + b1;
        double a0z = a0 * k + a1;
        double a1z = -a0 * k + a1;

        if (Math.Abs(a0z) < Epsilon)
            a0z = 1;

        double inv = 1.0 / a0z;
        return (new[] { b0z * inv, b1z * inv }, new[] { 1.0, a1z * inv });
    }

    private static (double[] numerator, double[] denominator) BilinearSecond(IReadOnlyList<double> num, IReadOnlyList<double> den, double k)
    {
        double b0 = num.Count > 2 ? num[0] : 0;
        double b1 = num.Count > 1 ? num[num.Count - 2] : 0;
        double b2 = num[^1];

        double a0 = den[0];
        double a1 = den[1];
        double a2 = den[2];

        double k2 = k * k;

        double b0z = b0 * k2 + b1 * k + b2;
        double b1z = 2 * b2 - 2 * b0 * k2;
        double b2z = b0 * k2 - b1 * k + b2;

        double a0z = a0 * k2 + a1 * k + a2;
        double a1z = 2 * a2 - 2 * a0 * k2;
        double a2z = a0 * k2 - a1 * k + a2;

        if (Math.Abs(a0z) < Epsilon)
            a0z = 1;

        double inv = 1.0 / a0z;
        return (new[] { b0z * inv, b1z * inv, b2z * inv }, new[] { 1.0, a1z * inv, a2z * inv });
    }

    public static Complex[] PolynomialRoots(double[] coefficients)
    {
        if (coefficients.Length <= 1)
            return Array.Empty<Complex>();

        int order = coefficients.Length - 1;
        int leadingIndex = 0;
        while (leadingIndex < coefficients.Length && Math.Abs(coefficients[leadingIndex]) < Epsilon)
            leadingIndex++;

        if (leadingIndex >= coefficients.Length)
            return Array.Empty<Complex>();

        int effectiveLength = coefficients.Length - leadingIndex;
        var normalized = new double[effectiveLength];
        Array.Copy(coefficients, leadingIndex, normalized, 0, effectiveLength);

        double leading = normalized[0];
        if (Math.Abs(leading) < Epsilon)
            return Array.Empty<Complex>();

        for (int i = 0; i < normalized.Length; i++)
            normalized[i] /= leading;

        int degree = normalized.Length - 1;
        if (degree == 1)
            return new[] { new Complex(-normalized[1], 0) };

        var roots = new Complex[degree];
        double radius = 1.0;
        for (int i = 0; i < degree; i++)
            roots[i] = Complex.FromPolarCoordinates(radius, 2 * Math.PI * (i + 1) / degree);

        for (int iter = 0; iter < 100; iter++)
        {
            bool converged = true;
            for (int i = 0; i < degree; i++)
            {
                Complex numerator = EvaluatePolynomial(normalized, roots[i]);
                Complex denominator = Complex.One;
                for (int j = 0; j < degree; j++)
                {
                    if (i == j)
                        continue;
                    denominator *= roots[i] - roots[j];
                }

                if (denominator == Complex.Zero)
                {
                    roots[i] += Complex.FromPolarCoordinates(1e-3, 2 * Math.PI * i / degree);
                    converged = false;
                    continue;
                }

                Complex delta = numerator / denominator;
                roots[i] -= delta;
                if (delta.Magnitude > 1e-12)
                    converged = false;
            }

            if (converged)
                break;
        }

        return roots;
    }

    private static Complex EvaluatePolynomial(IReadOnlyList<double> coefficients, Complex value)
    {
        Complex result = Complex.Zero;
        for (int i = 0; i < coefficients.Count; i++)
            result = result * value + coefficients[i];
        return result;
    }

    public static double[,] Identity(int dimension)
    {
        var matrix = new double[dimension, dimension];
        for (int i = 0; i < dimension; i++)
            matrix[i, i] = 1.0;
        return matrix;
    }

    public static double[,] MatrixClone(double[,] source) => (double[,])source.Clone();

    public static double[,] MatrixAdd(double[,] left, double[,] right)
    {
        int rows = left.GetLength(0);
        int cols = left.GetLength(1);
        var result = new double[rows, cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[i, j] = left[i, j] + right[i, j];
        return result;
    }

    public static double[,] MatrixSubtract(double[,] left, double[,] right)
    {
        int rows = left.GetLength(0);
        int cols = left.GetLength(1);
        var result = new double[rows, cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[i, j] = left[i, j] - right[i, j];
        return result;
    }

    public static double[,] MatrixScale(double[,] matrix, double scalar)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var result = new double[rows, cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[i, j] = matrix[i, j] * scalar;
        return result;
    }

    public static double[,] MatrixMultiply(double[,] left, double[,] right)
    {
        if (left.GetLength(1) != right.GetLength(0))
            throw new ArgumentException("Matrix dimensions are incompatible.");

        int rows = left.GetLength(0);
        int cols = right.GetLength(1);
        int shared = left.GetLength(1);
        var result = new double[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                double sum = 0;
                for (int k = 0; k < shared; k++)
                    sum += left[i, k] * right[k, j];
                result[i, j] = sum;
            }
        }

        return result;
    }

    public static double[] MatrixVectorMultiply(double[,] matrix, double[] vector)
    {
        if (matrix.GetLength(1) != vector.Length)
            throw new ArgumentException("Matrix and vector dimensions are incompatible.");

        int rows = matrix.GetLength(0);
        var result = new double[rows];
        for (int i = 0; i < rows; i++)
        {
            double sum = 0;
            for (int j = 0; j < vector.Length; j++)
                sum += matrix[i, j] * vector[j];
            result[i] = sum;
        }

        return result;
    }

    public static double Dot(double[] left, double[] right)
    {
        if (left.Length != right.Length)
            throw new ArgumentException("Vector dimensions are incompatible.");

        double sum = 0;
        for (int i = 0; i < left.Length; i++)
            sum += left[i] * right[i];
        return sum;
    }

    public static double MatrixTrace(double[,] matrix)
    {
        int size = matrix.GetLength(0);
        double trace = 0;
        for (int i = 0; i < size; i++)
            trace += matrix[i, i];
        return trace;
    }

    public static double MatrixInfinityNorm(double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        double max = 0;
        for (int i = 0; i < rows; i++)
        {
            double sum = 0;
            for (int j = 0; j < cols; j++)
                sum += Math.Abs(matrix[i, j]);
            if (sum > max)
                max = sum;
        }

        return max;
    }

    public static double[,] MatrixExponential(double[,] matrix, int maxTerms = 32, double tolerance = 1e-12)
    {
        int size = matrix.GetLength(0);
        var result = Identity(size);
        var term = Identity(size);

        for (int k = 1; k <= maxTerms; k++)
        {
            term = MatrixMultiply(term, matrix);
            term = MatrixScale(term, 1.0 / k);
            result = MatrixAdd(result, term);
            if (MatrixInfinityNorm(term) < tolerance)
                break;
        }

        return result;
    }

    public static double[,] MatrixInverse(double[,] matrix)
    {
        int size = matrix.GetLength(0);
        if (matrix.GetLength(1) != size)
            throw new ArgumentException("Matrix must be square.");

        var augmented = new double[size, 2 * size];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
                augmented[i, j] = matrix[i, j];
            for (int j = 0; j < size; j++)
                augmented[i, j + size] = i == j ? 1.0 : 0.0;
        }

        for (int col = 0; col < size; col++)
        {
            int pivot = col;
            double pivotValue = Math.Abs(augmented[col, col]);
            for (int row = col + 1; row < size; row++)
            {
                double value = Math.Abs(augmented[row, col]);
                if (value > pivotValue)
                {
                    pivot = row;
                    pivotValue = value;
                }
            }

            if (pivotValue < Epsilon)
                throw new InvalidOperationException("Matrix is singular.");

            if (pivot != col)
            {
                for (int j = 0; j < 2 * size; j++)
                {
                    (augmented[col, j], augmented[pivot, j]) = (augmented[pivot, j], augmented[col, j]);
                }
            }

            double diag = augmented[col, col];
            for (int j = 0; j < 2 * size; j++)
                augmented[col, j] /= diag;

            for (int row = 0; row < size; row++)
            {
                if (row == col)
                    continue;

                double factor = augmented[row, col];
                if (Math.Abs(factor) < Epsilon)
                    continue;

                for (int j = 0; j < 2 * size; j++)
                    augmented[row, j] -= factor * augmented[col, j];
            }
        }

        var inverse = new double[size, size];
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                inverse[i, j] = augmented[i, j + size];
        return inverse;
    }

    public static (double[,] Ad, double[,] Bd) DiscretizeZoh(double[,] A, double[,] B, double samplePeriod)
    {
        int states = A.GetLength(0);
        int inputs = B.GetLength(1);

        var augmented = new double[states + inputs, states + inputs];
        for (int i = 0; i < states; i++)
        {
            for (int j = 0; j < states; j++)
                augmented[i, j] = A[i, j];
            for (int j = 0; j < inputs; j++)
                augmented[i, states + j] = B[i, j];
        }

        var scaled = MatrixScale(augmented, samplePeriod);
        var exponential = MatrixExponential(scaled);
        var Ad = SubMatrix(exponential, 0, 0, states, states);
        var Bd = SubMatrix(exponential, 0, states, states, inputs);
        return (Ad, Bd);
    }

    public static (double[,] Ad, double[,] Bd) DiscretizeTustin(double[,] A, double[,] B, double samplePeriod)
    {
        int states = A.GetLength(0);
        var I = Identity(states);
        double halfTs = samplePeriod / 2.0;
        var lhs = MatrixSubtract(I, MatrixScale(A, halfTs));
        var rhs = MatrixAdd(I, MatrixScale(A, halfTs));
        var lhsInv = MatrixInverse(lhs);
        var Ad = MatrixMultiply(lhsInv, rhs);
        var Bd = MatrixMultiply(lhsInv, MatrixScale(B, samplePeriod));
        return (Ad, Bd);
    }

    public static Complex[] DiscreteStateSpaceFrequencyResponse(double[,] Ad, double[,] Bd, double[,] Cd, double[,] Dd, double sampleRate, IReadOnlyList<double> frequenciesHz)
    {
        int states = Ad.GetLength(0);
        if (Bd.GetLength(1) != 1 || Cd.GetLength(0) != 1 || Dd.GetLength(0) != 1 || Dd.GetLength(1) != 1)
            throw new ArgumentException("Frequency response helper expects a SISO system.");

        var response = new Complex[frequenciesHz.Count];
        var rhs = new Complex[states];
        for (int i = 0; i < states; i++)
            rhs[i] = Bd[i, 0];

        for (int idx = 0; idx < frequenciesHz.Count; idx++)
        {
            double omega = 2 * Math.PI * frequenciesHz[idx] / sampleRate;
            Complex z = Complex.Exp(Complex.ImaginaryOne * omega);
            var matrix = new Complex[states, states];
            for (int i = 0; i < states; i++)
                for (int j = 0; j < states; j++)
                    matrix[i, j] = -Ad[i, j];
            for (int i = 0; i < states; i++)
                matrix[i, i] += z;

            Complex[] solution = SolveLinearSystem(matrix, rhs);
            Complex output = new Complex(Dd[0, 0], 0);
            for (int j = 0; j < states; j++)
                output += Cd[0, j] * solution[j];
            response[idx] = output;
        }

        return response;
    }

    public static double[] DiscreteStateSpaceMagnitude(double[,] Ad, double[,] Bd, double[,] Cd, double[,] Dd, double sampleRate, IReadOnlyList<double> frequenciesHz)
    {
        var response = DiscreteStateSpaceFrequencyResponse(Ad, Bd, Cd, Dd, sampleRate, frequenciesHz);
        var magnitudes = new double[response.Length];
        for (int i = 0; i < response.Length; i++)
            magnitudes[i] = ToDecibels(response[i].Magnitude);
        return magnitudes;
    }

    public static Complex[] SolveLinearSystem(Complex[,] matrix, Complex[] vector)
    {
        int size = matrix.GetLength(0);
        if (matrix.GetLength(1) != size)
            throw new ArgumentException("Matrix must be square.");
        if (vector.Length != size)
            throw new ArgumentException("Vector dimension mismatch.");

        var a = (Complex[,])matrix.Clone();
        var b = (Complex[])vector.Clone();

        for (int col = 0; col < size; col++)
        {
            int pivot = col;
            double pivotMagnitude = a[col, col].Magnitude;
            for (int row = col + 1; row < size; row++)
            {
                double magnitude = a[row, col].Magnitude;
                if (magnitude > pivotMagnitude)
                {
                    pivot = row;
                    pivotMagnitude = magnitude;
                }
            }

            if (pivotMagnitude < Epsilon)
                throw new InvalidOperationException("Matrix is singular.");

            if (pivot != col)
            {
                for (int j = col; j < size; j++)
                    (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
                (b[col], b[pivot]) = (b[pivot], b[col]);
            }

            Complex diag = a[col, col];
            for (int j = col; j < size; j++)
                a[col, j] /= diag;
            b[col] /= diag;

            for (int row = 0; row < size; row++)
            {
                if (row == col)
                    continue;
                Complex factor = a[row, col];
                if (factor == Complex.Zero)
                    continue;
                for (int j = col; j < size; j++)
                    a[row, j] -= factor * a[col, j];
                b[row] -= factor * b[col];
            }
        }

        return b;
    }

    public static double[] CharacteristicPolynomial(double[,] matrix)
    {
        int size = matrix.GetLength(0);
        var coefficients = new double[size + 1];
        coefficients[0] = 1.0;

        var identity = Identity(size);
        var previous = identity;

        for (int k = 1; k <= size; k++)
        {
            var product = MatrixMultiply(matrix, previous);
            double ck = MatrixTrace(product) / k;
            coefficients[k] = -ck;
            var scaledIdentity = MatrixScale(identity, ck);
            previous = MatrixSubtract(product, scaledIdentity);
        }

        return coefficients;
    }

    public static Complex[] Eigenvalues(double[,] matrix) => PolynomialRoots(CharacteristicPolynomial(matrix));

    public static (double[] numerator, double[] denominator) StateSpaceToTransferFunctionDiscrete(double[,] Ad, double[,] Bd, double[,] Cd, double[,] Dd)
    {
        int states = Ad.GetLength(0);
        if (states == 0)
        {
            double gain = Dd.Length > 0 ? Dd[0, 0] : 0;
            return (new[] { gain }, new[] { 1.0 });
        }

        if (Bd.GetLength(1) != 1 || Cd.GetLength(0) != 1 || Dd.GetLength(0) != 1 || Dd.GetLength(1) != 1)
            throw new ArgumentException("Transfer function conversion expects SISO system.");

        var denominator = CharacteristicPolynomial(Ad);
        int order = states;
        var h = new double[order + 1];

        var state = new double[states];
        for (int i = 0; i < states; i++)
            state[i] = Bd[i, 0];

        var c = new double[states];
        for (int i = 0; i < states; i++)
            c[i] = Cd[0, i];
        double d = Dd[0, 0];

        h[0] = Dot(c, state) + d;
        var current = (double[])state.Clone();
        for (int k = 1; k <= order; k++)
        {
            current = MatrixVectorMultiply(Ad, current);
            h[k] = Dot(c, current);
        }

        var numerator = new double[order + 1];
        for (int k = 0; k <= order; k++)
        {
            double value = h[k];
            for (int i = 1; i <= Math.Min(k, order); i++)
                value += denominator[i] * h[k - i];
            numerator[k] = value;
        }

        double leading = numerator.FirstOrDefault(v => Math.Abs(v) > Epsilon);
        if (Math.Abs(leading) < Epsilon)
            leading = numerator[0];

        if (Math.Abs(denominator[0] - 1.0) > Epsilon)
        {
            double scale = denominator[0];
            for (int i = 0; i < denominator.Length; i++)
                denominator[i] /= scale;
            for (int i = 0; i < numerator.Length; i++)
                numerator[i] /= scale;
        }

        return (numerator, denominator);
    }

    public static Complex[] StateSpaceZeroes(double[,] Ad, double[,] Bd, double[,] Cd, double[,] Dd)
    {
        var (num, _) = StateSpaceToTransferFunctionDiscrete(Ad, Bd, Cd, Dd);
        int leading = 0;
        while (leading < num.Length && Math.Abs(num[leading]) < Epsilon)
            leading++;
        if (leading >= num.Length - 1)
            return Array.Empty<Complex>();

        var trimmed = new double[num.Length - leading];
        Array.Copy(num, leading, trimmed, 0, trimmed.Length);
        return PolynomialRoots(trimmed);
    }

    private static double[,] SubMatrix(double[,] matrix, int rowStart, int colStart, int rows, int cols)
    {
        var result = new double[rows, cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[i, j] = matrix[rowStart + i, colStart + j];
        return result;
    }
}
