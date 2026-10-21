using Slae.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Application.Services
{
    public static class TaskParamsValidator
    {
        public static List<string> Validate(JacobiParams p)
        {
            var errors = new List<string>();
            bool manual = (p.Matrix is not null) || (p.Vector is not null);
            int maxDim = manual ? TaskLimits.MaxManualDimension : TaskLimits.MaxDimension;

            if (p.Dimension < 1 || p.Dimension > maxDim)
            {
                errors.Add($"Dimension must be between 1 and {maxDim}!");
            }
            if (!double.IsFinite(p.Epsilon) || p.Epsilon < TaskLimits.MinEpsilon || p.Epsilon > TaskLimits.MaxEpsilon)
            {
                errors.Add($"Epsilon must be between {TaskLimits.MinEpsilon} and {TaskLimits.MaxEpsilon}!");
            }
            if (p.MaxIterations < 1 || p.MaxIterations > TaskLimits.MaxIterations)
            {
                errors.Add($"MaxIterations must be between 1 and {TaskLimits.MaxIterations}!");
            }
            if (!double.IsFinite(p.Dominance) || p.Dominance < TaskLimits.MinDominance || p.Dominance > TaskLimits.MaxDominance)
            {
                errors.Add($"Dominance must be between {TaskLimits.MinDominance} and {TaskLimits.MaxDominance}!");
            }

            double operations = (double)p.Dimension * p.Dimension * p.MaxIterations;

            if (operations > TaskLimits.MaxOperations)
            {
                errors.Add("Task is too large: reduce Dimension or MaxIterations!");
            }
            if ((p.Matrix is null) != (p.Vector is null))
            {
                errors.Add("Matrix and Vector must be provided together!");
            }

            if (p.Matrix is not null && p.Vector is not null && errors.Count == 0)
            {
                int n = p.Dimension;
                if (p.Matrix.Length != n || p.Vector.Length != n || p.Matrix.Any(r => r is null || r.Length != n))
                {
                    errors.Add("Matrix must be n x n, and Vector must be of length n!");
                }
                else
                {
                    for (int i = 0; i < n; i++)
                    {
                        double diag = Math.Abs(p.Matrix[i][i]);
                        double rest = 0;
                        for (int j = 0; j < n; j++)
                        {
                            if (!double.IsFinite(p.Matrix[i][j]))
                            {
                                errors.Add("Matrix contains invalid numbers!");
                            }
                            if (j != i)
                            {
                                rest += Math.Abs(p.Matrix[i][j]);
                            }
                        }
                        if (diag <= rest)
                        {
                            errors.Add($"Row {i + 1} lacks diagonal dominance: Jacobi method may not converge!");
                            break;
                        }
                    }
                    if (p.Vector.Any(v => !double.IsFinite(v)))
                    {
                        errors.Add("Vector contains invalid numbers!");
                    }
                }
            }
            return errors;
        }
    }
}
