using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Slae.Application.Models;

namespace Slae.Application.Services
{
    public static class JacobiSolver
    {
        public static JacobiResult Solve(
            double[][] a, double[] b, int n,
            double epsilon, int maxIterations,
            int maxThreads,
            IProgress<int>? progress,
            CancellationToken ct)
        {
            Stopwatch sw = Stopwatch.StartNew();
            double[] xOld = new double[n];
            double[] xNew = new double[n];

            int chunks = Math.Min(n, Math.Max(1, maxThreads) * 4);
            double[] chunkMax = new double[chunks];
            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxThreads,
                CancellationToken = ct
            };

            int iterations = 0;
            bool isSolved = false;
            double firstDiff = -1;
            int lastProgress = 0;

            while (iterations < maxIterations)
            {
                ct.ThrowIfCancellationRequested();

                double[] cur = xOld;
                double[] next = xNew;

                Parallel.For(0, chunks, options, c =>
                {
                    int from = (int)((long)c * n / chunks);
                    int to = (int)((long)(c + 1) * n / chunks);
                    double localMax = 0;

                    for (int i = from; i < to; i++)
                    {
                        double sum = 0;
                        for (int j = 0; j < n; j++)
                        {
                            if (j != i)
                            {
                                sum += a[i][j] * cur[j];
                            }
                        }

                        double v = (b[i] - sum) / a[i][i];
                        next[i] = v;

                        double d = Math.Abs(v - cur[i]);
                        if (d > localMax)
                        {
                            localMax = d;
                        }
                    }
                    chunkMax[c] = localMax;
                });

                iterations++;

                double[] temp = xOld;
                xOld = xNew;
                xNew = temp;

                double diff = chunkMax.Max();

                if (!double.IsFinite(diff))
                {
                    throw new InvalidOperationException("Jacobi method failed: obtained infinity!");
                }

                if (firstDiff < 0)
                {
                    firstDiff = diff;
                }

                if (diff < epsilon)
                {
                    isSolved = true;
                    break;
                }

                double logRatio = 0;
                if (firstDiff > epsilon && diff > 0)
                {
                    logRatio = Math.Log(firstDiff / diff) / Math.Log(firstDiff / epsilon);
                }

                int p = (int)Math.Max(logRatio * 100, iterations * 100.0 / maxIterations);
                p = Math.Min(p, 99);

                if (p > lastProgress)
                {
                    lastProgress = p;
                    progress?.Report(p);
                }
            }

            return new JacobiResult(xOld, iterations, isSolved, sw.Elapsed.TotalSeconds);
        }
    }
}