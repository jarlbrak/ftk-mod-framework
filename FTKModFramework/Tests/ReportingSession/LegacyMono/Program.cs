using System;
using FTKModFramework.Core.Reporting;

internal static class Program
{
    private static int Main(string[] args)
    {
        string[] parts = args[0].Split('|');
        ReportingSessionStore store;
        bool opened = ReportingSessionStore.TryOpen(parts[1], DateTime.UtcNow, out store);
        if (parts[0] == "blocked")
        {
            if (opened) { store.Dispose(); return 10; }
            Console.WriteLine("BLOCKED"); return 0;
        }
        if (!opened) return 11;
        using (store)
        {
            if (parts[0] == "hold")
            {
                if (store.Pending != null || !store.TryCheckpoint("previous-mods", "title", DateTime.UtcNow)) return 12;
                for (int i = 0; i < 10; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
                Console.WriteLine("READY_AFTER_GC"); Console.Out.Flush(); Console.ReadLine();
                return 0;
            }
            if (parts[0] == "recover")
            {
                if (store.Pending == null || store.Pending.Metadata != "previous-mods" || store.Pending.CheckpointId == null) return 13;
                string session = store.Pending.SessionId;
                if (!store.TryCheckpoint("current-mods", "title", DateTime.UtcNow) ||
                    store.Pending.SessionId != session || store.Pending.Metadata != "previous-mods") return 14;
                if (!store.TryDismissPending(DateTime.UtcNow)) return 15;
            }
            else if (parts[0] != "normal" || store.Pending != null) return 16;
            if (!store.TryRecordShutdown(DateTime.UtcNow)) return 17;
            Console.WriteLine("OK"); return 0;
        }
    }
}
