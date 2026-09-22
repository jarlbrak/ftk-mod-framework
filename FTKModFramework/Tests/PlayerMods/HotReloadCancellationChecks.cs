using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using FTKModFramework.Core.Marketplace;

internal static class HotReloadCancellationChecks
{
    internal static void Run()
    {
        if (MarketplaceRuntime.Busy) throw new Exception("Cancellation fixture requires idle marketplace.");
        FieldInfo operationField = typeof(MarketplaceRuntime).GetField("_operation", BindingFlags.NonPublic | BindingFlags.Static);
        var oldBlocker = MarketplaceRuntime.MutationsBlocked;
        string id = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(MarketplaceRuntime.StateRoot, "operations");
        Directory.CreateDirectory(directory);
        string resultPath = Path.Combine(directory, id + ".result.json");
        File.WriteAllText(resultPath, JsonConvert.SerializeObject(new MarketplaceResult {
            SchemaVersion = 1, OperationId = id, Ok = true, Status = "ready", Pending = MarketplaceRuntime.Pending }));
        int completions = 0;
        MarketplaceResult completedResult = null;
        Process process = Process.Start(new ProcessStartInfo("/bin/sh", "-c \"exit 0\"") { UseShellExecute = false });
        if (!process.WaitForExit(5000)) throw new Exception("Cancellation process fixture did not exit.");
            MarketplaceOperation operation = new MarketplaceOperation {
                Id = id, Process = process, ResultPath = resultPath, Clock = Stopwatch.StartNew(), TimeoutMs = 5000,
                Complete = delegate(MarketplaceResult result) { completions++; completedResult = result; }, DeferCompletion = true
        };
        try
        {
            operationField.SetValue(null, operation);
            MarketplaceRuntime.MutationsBlocked = delegate { return true; };
            MarketplaceRuntime.CancelRunning();
            if (!object.ReferenceEquals(operationField.GetValue(null), operation) || !MarketplaceRuntime.Busy ||
                operation.Complete == null || completions != 0 || File.Exists(Path.Combine(directory, id + ".cancel")))
                throw new Exception("Blocked cancellation changed the hot operation or its completion.");
            // A completed helper still requires Poll to deliver durable reconciliation. The
            // cancellation guard must preserve that callback even after the process exits.
            MarketplaceRuntime.Poll();
            MarketplaceRuntime.Poll();
            if (MarketplaceRuntime.Busy || completions != 0)
                throw new Exception("Hot completion ran while marketplace polling was still unwinding.");
            MarketplaceRuntime.DispatchHotReloadCompletion();
            MarketplaceRuntime.DispatchHotReloadCompletion();
            if (completions != 1 || completedResult == null || !completedResult.Ok)
                throw new Exception("Blocked cancellation prevented exactly-once deferred completion delivery.");
            Console.WriteLine("PASS: blocked hot cancellation dispatches exactly once after polling unwinds");
        }
        finally
        {
            MarketplaceRuntime.MutationsBlocked = oldBlocker;
            operationField.SetValue(null, null);
            process.Dispose();
        }
    }
}
