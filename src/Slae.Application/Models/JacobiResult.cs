using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Application.Models
{
    public record JacobiResult(
        double[] X,
        int Iterations,
        bool IsSolved,
        double ElapsedSeconds);
}
