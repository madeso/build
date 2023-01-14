using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workbench;

internal static class Functional
{
    // start is returned first
    public static IEnumerable<int> Integers(int start = 0, int step=1)
    {
        while (true)
        {
            yield return start;
            start += step;
        }
    }
}
