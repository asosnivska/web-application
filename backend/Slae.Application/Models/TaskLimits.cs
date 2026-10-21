using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Application.Models
{
    public static class TaskLimits
    {
        public const int MaxDimension = 3000;
        public const int MaxManualDimension = 500;
        public const int MaxIterations = 200000;
        public const double MinEpsilon = 1e-12;
        public const double MaxEpsilon = 0.1;
        public const double MaxOperations = 5e11;
        public const int MaxRunSeconds = 900;
        public const double MinDominance = 1.0001;
        public const double MaxDominance = 10.0;
        public const int MaxActiveTasksPerUser = 3;
    }
}
