using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Domain.Enums
{
    public enum SlaeTaskStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Canceled = 3,
        Failed = 4
    }
}
