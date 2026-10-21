using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Application.Models
{
    public class JacobiParams
    {
        public int Dimension { get; set; }

        public double Epsilon { get; set; } = 1e-6;

        public int MaxIterations { get; set; } = 10000;

        public double[][]? Matrix { get; set; }

        public double[]? Vector { get; set; }

        public int Seed { get; set; } = 1;

        public double Dominance { get; set; } = 1.01;
    }
}
