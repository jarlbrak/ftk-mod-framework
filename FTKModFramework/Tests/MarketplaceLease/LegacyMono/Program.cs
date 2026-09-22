using System;
using FTKModFramework.Core.Marketplace;

internal static class Program
{
    private static void Main(string[] args)
    {
        MarketplaceRuntimeLease.Acquire(args[0]);
        MarketplaceRuntimeLease.Acquire(args[0]);
        for (int i = 0; i < 10; i++)
        {
            byte[] pressure = new byte[1024 * 1024];
            GC.KeepAlive(pressure);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Console.WriteLine("LOCKED_AFTER_GC");
        Console.Out.Flush();
        Console.ReadLine();
    }
}
