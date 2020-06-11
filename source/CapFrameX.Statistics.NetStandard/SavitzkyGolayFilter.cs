using MathNet.Numerics.LinearAlgebra;
using System;

namespace CapFrameX.Statistics.NetStandard
{
    /// <summary>
    /// <para>Implements a Savitzky-Golay smoothing filter, as found in [1].</para>
    /// <para>[1] Sophocles J.Orfanidis. 1995. Introduction to Signal Processing. Prentice-Hall, Inc., Upper Saddle River, NJ, USA.</para>
    /// </summary>
    public sealed class SavitzkyGolayFilter
    {
        private readonly int sidePoints;

        private Matrix<double> coefficients;

        public SavitzkyGolayFilter(int sidePoints, int polynomialOrder)
        {
            this.sidePoints = sidePoints;
            Design(polynomialOrder);
        }

        /// <summary>
        /// Smoothes the input samples.
        /// </summary>
        /// <param name="samples"></param>
        /// <returns></returns>
        public double[] Process(double[] samples)
        {
            int length = samples.Length;
            double[] output = new double[length];
            int frameSize = (sidePoints << 1) + 1;
            double[] frame = new double[frameSize];

            Array.Copy(samples, frame, frameSize);

            for (int i = 0; i < sidePoints; ++i)
            {
                output[i] = coefficients.Column(i).DotProduct(Vector<double>.Build.DenseOfArray(frame));
            }

            for (int n = sidePoints; n < length - sidePoints; ++n)
            {
                Array.ConstrainedCopy(samples, n - sidePoints, frame, 0, frameSize);
                output[n] = coefficients.Column(sidePoints).DotProduct(Vector<double>.Build.DenseOfArray(frame));
            }

            Array.ConstrainedCopy(samples, length - frameSize, frame, 0, frameSize);

            for (int i = 0; i < sidePoints; ++i)
            {
                output[length - sidePoints + i] = coefficients.Column(sidePoints + 1 + i).DotProduct(Vector<double>.Build.Dense(frame));
            }

            return output;
        }

        private void Design(int polynomialOrder)
        {
            double[,] a = new double[(sidePoints << 1) + 1, polynomialOrder + 1];

            for (int m = -sidePoints; m <= sidePoints; ++m)
            {
                for (int i = 0; i <= polynomialOrder; ++i)
                {
                    a[m + sidePoints, i] = Math.Pow(m, i);
                }
            }

            Matrix<double> s = Matrix<double>.Build.DenseOfArray(a);
            coefficients = s.Multiply(s.TransposeThisAndMultiply(s).Inverse()).Multiply(s.Transpose());
        }
    }
}
