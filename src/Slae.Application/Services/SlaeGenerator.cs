using Slae.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Application.Services
{
    public static class SlaeGenerator
    {
        public static (double[][] A, double[] B) Build(JacobiParams p)
        {
            int n = p.Dimension;
            var a = new double[n][];
            for (int i = 0; i < n; i++)
            {
                a[i] = new double[n];
            }
            var b = new double[n];

            if ((p.Matrix is not null) && (p.Vector is not null))
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        a[i][j] = p.Matrix[i][j];
                    }
                    b[i] = p.Vector[i];
                }
                return (a, b);
            }

            var rnd = new Random(p.Seed);
            var expectedX = new double[n];

            for (int i = 0; i < n; i++)
            {
                expectedX[i] = rnd.NextDouble() * 10 - 5;
                double rowSum = 0;

                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    double v = rnd.NextDouble();
                    a[i][j] = v;
                    rowSum += v;
                }

                a[i][i] = Math.Max(rowSum * p.Dominance, 1.0);
            }

            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int j = 0; j < n; j++)
                {
                    s += a[i][j] * expectedX[j];
                }
                b[i] = s;
            }

            return (a, b);
        }
    }
}
