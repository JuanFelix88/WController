using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Plugin.ProjectsSearch.Util;
internal class OThreadPool
{
    public int Count { get; }
    private int RunningCount = 0;
    public OThreadPool(int count = 4)
    {
        Count = count;
    }

    public void Run(Action action)
    {
        if (RunningCount >= Count)
        {
            while (RunningCount >= Count)
            {
                Thread.Sleep(10);
            }
        }

        Thread thread = new(() =>
        {
            try
            {
                Interlocked.Increment(ref RunningCount);
                action();
            }
            finally
            {
                Interlocked.Decrement(ref RunningCount);
            }
        });
    }

    public static implicit operator OThreadPool(int otpSize) => new OThreadPool(otpSize);
}
