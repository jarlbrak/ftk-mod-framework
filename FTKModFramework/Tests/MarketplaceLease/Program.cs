using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FTKModFramework.Core.Marketplace;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "hold")
        {
            MarketplaceRuntimeLease.Acquire(args[1]);
            // Unity's legacy Mono may return a new owning SafeFileHandle wrapper per
            // getter. Finalization must not release the process lease while held is rooted.
            for (int i = 0; i < 5; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
            Console.WriteLine("locked");
            Console.Out.Flush();
            Console.ReadLine();
            return 0;
        }
        string root = Path.Combine(Path.GetTempPath(), "ftkmf-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("hold"); start.ArgumentList.Add(root);
        using (Process child = Process.Start(start))
        {
            if (child.StandardOutput.ReadLine() != "locked") throw new Exception("Child did not acquire native lock.");
            bool rejected = false;
            try { MarketplaceRuntimeLease.Acquire(root); } catch (IOException) { rejected = true; }
            if (!rejected) throw new Exception("A second process acquired the lifetime lock.");
            child.Kill();
            if (!child.WaitForExit(5000)) throw new Exception("Child did not exit.");
        }
        MarketplaceRuntimeLease.Acquire(root);
        MarketplaceRuntimeLease.Acquire(root);
        bool otherRootRejected = false;
        try { MarketplaceRuntimeLease.Acquire(root + "-other"); } catch (IOException) { otherRootRejected = true; }
        if (!otherRootRejected) throw new Exception("Lease changed roots after acquisition.");
        for (int i = 0; i < 5; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        if (args.Length == 1)
        {
            string request = Path.Combine(root,"request.json"), result = Path.Combine(root,"result.json");
            File.WriteAllText(request, System.Text.Json.JsonSerializer.Serialize(new { schemaVersion=1,operationId=Guid.NewGuid().ToString("N"),stateRoot=root }));
            var helper = new ProcessStartInfo(args[0]) { UseShellExecute=false, RedirectStandardError=true };
            foreach (string arg in new[] { "marketplace","collect","--request",request,"--result",result }) helper.ArgumentList.Add(arg);
            using (Process process = Process.Start(helper))
            {
                process.WaitForExit();
                if (process.ExitCode == 0 || !File.ReadAllText(result).Contains("close the game"))
                    throw new Exception("Go helper did not observe the managed runtime's OS lock: " + process.StandardError.ReadToEnd());
            }
        }
        Console.WriteLine("PASS: GC-pressure cross-process exclusion, abrupt death recovery" + (args.Length == 1 ? ", Go/C# lock interoperability" : ""));
        return 0;
    }
}
