using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using FTKModFramework.Core.Reporting;

internal static class Program
{
    private static readonly DateTime Now = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
    private static int Main(string[] args)
    {
        if (args.Length != 0)
        {
            ReportingSessionStore child;
            if (!ReportingSessionStore.TryOpen(args[1], Now, out child)) return 19;
            using (child)
            {
                if (!child.TryCheckpoint("previous-mods", "title", Now)) return 20;
                Console.WriteLine("ready"); Console.Out.Flush();
                if (args[0] == "hold") Thread.Sleep(Timeout.Infinite);
                else if (!child.TryRecordShutdown(Now)) return 21;
            }
            return 0;
        }
        string root = Path.Combine(Path.GetTempPath(), "ftk-report-test-" + Guid.NewGuid().ToString("N"));
        if (root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        Directory.CreateDirectory(root);
        try
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                string linkRoot = Path.Combine(root, "link"); Directory.CreateDirectory(linkRoot);
                File.CreateSymbolicLink(Path.Combine(linkRoot, "reporting.lock"), Path.Combine(root, "absent"));
                ReportingSessionStore linked;
                Check(!ReportingSessionStore.TryOpen(linkRoot, Now, out linked), "dangling lock link rejected");
                Check(!File.Exists(Path.Combine(root, "absent")), "link target untouched");
                Directory.CreateSymbolicLink(Path.Combine(root, "linked-root"), Path.Combine(root, "missing-root"));
                Check(!ReportingSessionStore.TryOpen(Path.Combine(root, "linked-root"), Now, out linked), "dangling root link rejected");
            }
            string crashRoot = Path.Combine(root, "crash");
            using (Process child = Spawn("hold", crashRoot))
            {
                try
                {
                    var ready = child.StandardOutput.ReadLineAsync();
                    Check(ready.Wait(5000) && ready.Result == "ready", "child startup");
                    ReportingSessionStore competing;
                    Check(!ReportingSessionStore.TryOpen(crashRoot, Now, out competing), "live owner blocks inspection");
                }
                finally { Reap(child); }
            }
            string prior;
            using (ReportingSessionStore store = Open(crashRoot, Now))
            {
                Check(store.Pending != null && store.Pending.Metadata == "previous-mods", "forced exit offer");
                prior = store.Pending.SessionId;
                Check(store.Pending.CheckpointId != null, "checkpoint identity");
                Check(store.TryCheckpoint("new-mods", "title", Now), "new checkpoint");
                ReportingIncident copied = store.Pending; copied.Metadata = "mutated";
                Check(store.Pending.Metadata == "previous-mods", "detached provenance");
            }
            string secondIncident;
            using (ReportingSessionStore store = Open(crashRoot, Now))
            {
                secondIncident = store.SessionId;
                Check(store.Pending.SessionId == prior && store.Pending.Metadata == "previous-mods", "pending wins repeated restart");
                Check(store.TryLinkPendingReport(new string('a', 32), Now), "durable linkage");
            }
            using (ReportingSessionStore store = Open(crashRoot, Now))
            {
                Check(store.Pending.SessionId == secondIncident && store.Pending.ReportId == null, "linked incident retires before distinct crash promotion");
                Check(store.TryDismissPending(Now), "dismiss persists");
                Check(store.TryRecordShutdown(Now), "normal callback");
            }
            using (ReportingSessionStore store = Open(crashRoot, Now))
                Check(store.Pending == null, "dismiss and normal shutdown no offer");
            string linkedNormal = Path.Combine(root, "linked-normal");
            using (ReportingSessionStore store = Open(linkedNormal, Now)) { }
            using (ReportingSessionStore store = Open(linkedNormal, Now))
            {
                Check(store.TryLinkPendingReport(new string('b', 32), Now), "link before normal quit");
                Check(store.Pending.ReportId == new string('b', 32), "link available in current launch");
                Check(store.TryRecordShutdown(Now), "linked launch normal quit");
            }
            using (ReportingSessionStore store = Open(linkedNormal, Now)) Check(store.Pending == null, "linked incident and clean launch produce no offer");
            string foreign = Path.Combine(root, "foreign");
            using (ReportingSessionStore store = Open(foreign, Now))
            {
                Check(store.TryCheckpoint("one", "title", Now), "advance generation");
                string[] names = { "stage-not-ours.tmp", "state-not-ours.record", "stage-" + new string('c', 32) + ".tmp", "state-00000000000000000001.record" };
                foreach (string name in names) File.WriteAllText(Path.Combine(foreign, name), "foreign or invalid");
                Check(store.TryCheckpoint("two", "title", Now), "publication with foreign files");
                foreach (string name in names) Check(File.ReadAllText(Path.Combine(foreign, name)) == "foreign or invalid", "foreign content retained " + name);
            }
            using (ReportingSessionStore store = Open(foreign, Now)) Check(store.Pending.Metadata == "two", "latest valid revision ignores unrelated names");
            string normal = Path.Combine(root, "normal");
            using (Process child = Spawn("normal", normal))
            { try { Check(child.WaitForExit(5000) && child.ExitCode == 0, "normal process exits"); } finally { Reap(child); } }
            using (ReportingSessionStore store = Open(normal, Now)) Check(store.Pending == null, "normal process no offer");
            using (ReportingSessionStore store = Open(normal, Now.AddDays(8))) Check(store.Pending == null, "expired session no offer");
            string corrupt = Path.Combine(root, "corrupt");
            using (ReportingSessionStore store = Open(corrupt, Now)) Check(store.TryCheckpoint("data", "title", Now), "corrupt setup");
            string record = Directory.GetFiles(corrupt, "*.record")[0];
            File.WriteAllBytes(record, new byte[] {1, 2, 3});
            ReportingSessionStore invalid;
            Check(!ReportingSessionStore.TryOpen(corrupt, Now, out invalid), "truncation never inferred crash");
            string hashRoot = Path.Combine(root, "hash");
            using (ReportingSessionStore store = Open(hashRoot, Now)) { }
            string valid = Directory.GetFiles(hashRoot, "*.record")[0];
            byte[] damaged = File.ReadAllBytes(valid); damaged[20] ^= 1;
            File.WriteAllBytes(Path.Combine(hashRoot, "state-00000000000000000002.record"), damaged);
            Check(!ReportingSessionStore.TryOpen(hashRoot, Now, out invalid), "invalid latest never falls back to older state");
            string budget = Path.Combine(root, "budget");
            using (ReportingSessionStore store = Open(budget, Now))
                Check(!store.TryCheckpoint(new string('x', ReportingSessionStore.RecordLimit), "title", Now), "envelope counts toward cap");
            using (ReportingSessionStore store = Open(budget, Now))
            { Check(store.Pending.Metadata == "" && store.Pending.CheckpointId == null, "failed checkpoint not published"); store.TryRecordShutdown(Now); }
            using (ReportingSessionStore store = Open(budget, Now))
            {
                File.WriteAllBytes(Path.Combine(budget, "other.bin"), new byte[ReportingSessionStore.StoreLimit]);
                Check(!store.TryDismissPending(Now) && store.Pending == null, "failed disposition suppresses offer for launch");
            }
            File.WriteAllBytes(Path.Combine(budget, "other.bin"), new byte[ReportingSessionStore.StoreLimit]);
            Check(!ReportingSessionStore.TryOpen(budget, Now, out invalid), "quota includes other and staging");
            Console.WriteLine("PASS: process lease, forced/normal exit, provenance, restart priority, linkage, dismissal, expiry, truncation and budgets");
            return 0;
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Reap(Process child)
    {
        if (!child.HasExited) child.Kill();
        Check(child.WaitForExit(5000), "owned child reaped");
    }
    private static ReportingSessionStore Open(string root, DateTime now)
    { ReportingSessionStore store; Check(ReportingSessionStore.TryOpen(root, now, out store), "open store"); return store; }
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    private static Process Spawn(string mode, string root)
    {
        ProcessStartInfo start = new ProcessStartInfo(Environment.ProcessPath);
        start.ArgumentList.Add(mode); start.ArgumentList.Add(root); start.RedirectStandardOutput = true; start.UseShellExecute = false;
        return Process.Start(start);
    }
}
