using System;
using System.IO;
using System.Text;
using System.Threading;
using FTKModFramework.Core.Reporting;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Main()
    {
        Redaction(); BufferBounds(); Offers(); SessionPersistence(); LargeTranscriptPersistence(); HookCoverage();
        Console.WriteLine("Reporting diagnostics: " + checks + " checks passed.");
    }
    private sealed class Source : BepInEx.Logging.ILogSource
    {
        public string SourceName { get; private set; }
        internal Source(string name) { SourceName = name; }
    }
    private static void HookCoverage()
    {
        ReportingDiagnostics.Start(); ReportingDiagnostics.Start();
        Check(BepInEx.Logging.Logger.Listeners.Count == 1, "Duplicate listener registration");
        BepInEx.Logging.Logger.Emit("combat completed", BepInEx.Logging.LogLevel.Info, new Source("mod"));
        BepInEx.Logging.Logger.Emit("transition delayed", BepInEx.Logging.LogLevel.Warning, new Source("mod"));
        BepInEx.Logging.Logger.Emit("turn ownership changed", BepInEx.Logging.LogLevel.Debug, new Source("mod"));
        UnityEngine.Application.Emit("awaiting next player", "", UnityEngine.LogType.Log);
        UnityEngine.Application.Emit("animation still playing", "", UnityEngine.LogType.Warning);
        string activity = ReportingDiagnostics.CaptureCurrent();
        Check(activity.Contains("combat completed") && activity.Contains("transition delayed") && activity.Contains("turn ownership changed"), "Ordinary BepInEx activity missing");
        Check(activity.Contains("awaiting next player") && activity.Contains("animation still playing"), "Ordinary Unity activity missing");
        Check(activity.Contains("mod Info") && activity.Contains("Unity Warning"), "Log source or level missing");
        Check(ReportingDiagnostics.PendingError == null, "Ordinary activity opened an error offer");
        BepInEx.Logging.Logger.Emit("forwarded excluded", BepInEx.Logging.LogLevel.Error, new Source("Unity Log"));
        Check(ReportingDiagnostics.CaptureCurrent() == activity, "Unity forwarded duplicate captured");
        BepInEx.Logging.Logger.Emit("loader fatal", BepInEx.Logging.LogLevel.Fatal, new Source("mod"));
        BepInEx.Logging.Logger.Emit("loader error", BepInEx.Logging.LogLevel.Error, new Source("mod"));
        Thread emitter = new Thread(delegate() { UnityEngine.Application.Emit("Unity exception", " at Game.Method()", UnityEngine.LogType.Exception); });
        emitter.Start(); emitter.Join();
        string captured = ReportingDiagnostics.CaptureCurrent();
        Check(captured.Contains("loader fatal") && captured.Contains("loader error") && captured.Contains(" at Game.Method()"), "Hook did not capture fatal/error/stack");
        Check(ReportingDiagnostics.PendingError != null, "Error hook did not create an offer");
        Check(ReportingDiagnostics.CapturePrevious("unrelated") == "", "Wrong previous session returned");
        ReportingDiagnostics.Stop();
        Check(BepInEx.Logging.Logger.Listeners.Count == 0, "Listener not removed");
        UnityEngine.Application.Emit("after stop", "", UnityEngine.LogType.Error);
        Check(ReportingDiagnostics.CaptureCurrent() == captured, "Unity hook not removed");
    }
    private static void Redaction()
    {
        string[] sensitive = {
            "Authorization: Bearer private-secret", "{\"token\":\"private-secret\"}",
            "PASSWORD=private-secret", "username=private-secret", "player_name: private-secret",
            "credential=private-secret", "CREDENTIALS: private-secret",
            "{\"credentials\":\"private-secret with spaces\"}", "credential:\nprivate-secret",
            "-----BEGIN RSA PRIVATE KEY-----\nprivate-secret\n-----END RSA PRIVATE KEY-----",
            "https://user:private-secret@example.net/path?q=secret", "private-secret@example.net",
            "/Users/private-secret/Library/Application Support/FTK/save.json", "C:\\Users\\private-secret\\FTK\\save.json",
            "\\\\private-secret\\share\\save.json", "/home/private-secret/.steam/log", "/var/private-secret.log",
            "github_pat_private-secret", "ghp_verylongsecretcredential", "sk-thisissecretcredentialcontent"
        };
        foreach (string value in sensitive)
        {
            string clean = ReportingDiagnosticsBuffer.Sanitize(value);
            Check(!clean.Contains("private-secret") && !clean.Contains("verylongsecretcredential") && !clean.Contains("thisissecretcredentialcontent"), "Redaction leaked " + value);
        }
        string identifiers = ReportingDiagnosticsBuffer.Sanitize("192.168.22.30:9999 2001:db8::42 76561198000000000 STEAM_0:1:123456 [U:1:123456]");
        Check(!identifiers.Contains("192.168") && !identifiers.Contains("db8") && !identifiers.Contains("765611") && !identifiers.Contains("123456"), "Account or IP leaked");
        Check(ReportingDiagnosticsBuffer.Sanitize("NullReferenceException\n at MyMod.DoThing()\n at Game.Update()") == "NullReferenceException\n at MyMod.DoThing()\n at Game.Update()", "Stack types damaged");
    }
    private static void BufferBounds()
    {
        ReportingDiagnosticsBuffer buffer = new ReportingDiagnosticsBuffer();
        DateTime now = DateTime.UtcNow;
        for (int i = 0; i < 1000; i++) buffer.Add("mod", "Exception " + i + new string('x', 5000), " at Game.Update()", now.AddSeconds(i));
        string captured = buffer.Capture();
        Check(Encoding.UTF8.GetByteCount(captured) <= ReportingDiagnosticsBuffer.ByteLimit, "Buffer exceeded byte bound");
        Check(captured.Contains("Exception 999") && !captured.Contains("Exception 0"), "Buffer did not retain recent entries");
        Check(captured.Contains("older or rate-limited entries omitted"), "Truncated transcript missing disclosure");
        ReportingDiagnosticsBuffer flood = new ReportingDiagnosticsBuffer();
        for (int i = 0; i < 10000; i++) flood.Add("mod", "Error " + i, "", now);
        Check(flood.Version == 32, "Callback flood was not bounded");
        ReportingDiagnosticsBuffer repeated = new ReportingDiagnosticsBuffer();
        for (int i = 0; i < 100; i++) repeated.Add("mod", "Same error", " at Method()", now.AddSeconds(i));
        Check(repeated.Version == 100, "Repeated log activity lost from transcript");
        repeated.Acknowledge(repeated.Pending.Id);
        repeated.Add("mod", "Same error", " at Method()", now.AddMinutes(6));
        Check(repeated.Pending == null, "Repeated error offered again after acknowledgement");
        ReportingDiagnosticsBuffer ordinaryFlood = new ReportingDiagnosticsBuffer();
        for (int i = 0; i < 10000; i++) ordinaryFlood.Add("mod Info", "activity " + i, "", now, false);
        Check(ordinaryFlood.Version == 128 && ordinaryFlood.Pending == null, "Normal activity flood was not bounded separately");
        ordinaryFlood.Add("mod Error", "critical error after activity", "", now, true);
        Check(ordinaryFlood.Pending != null && ordinaryFlood.Capture().Contains("critical error after activity"), "Normal flood suppressed an error");
        Check(ordinaryFlood.Capture().Contains("9872 older or rate-limited entries omitted"), "Rate limit silently lost activity");
        ReportingDiagnosticsBuffer concurrent = new ReportingDiagnosticsBuffer();
        Thread a = new Thread(delegate() { for (int i = 0; i < 1000; i++) concurrent.Add("a", "a" + i, "", now.AddSeconds(i)); });
        Thread b = new Thread(delegate() { for (int i = 0; i < 1000; i++) { concurrent.Add("b", "b" + i, "", now.AddSeconds(i)); concurrent.Capture(); } });
        a.Start(); b.Start(); a.Join(); b.Join();
        Check(Encoding.UTF8.GetByteCount(concurrent.Capture()) <= ReportingDiagnosticsBuffer.ByteLimit, "Concurrent buffer exceeded bound");
        string unicode = ReportingDiagnosticsBuffer.LimitUtf8(new string('\u2603', 9000), 4096);
        Check(Encoding.UTF8.GetByteCount(unicode) <= 4096, "UTF8 limit used character count");
        ReportingDiagnosticsBuffer unicodeBuffer = new ReportingDiagnosticsBuffer();
        for (int i = 0; i < 100; i++) unicodeBuffer.Add("mod Info", i + new string('\u2603', 5000), "", now.AddSeconds(i), false);
        Check(Encoding.UTF8.GetByteCount(unicodeBuffer.Capture()) <= 128 * 1024 && unicodeBuffer.Capture().Contains("99"), "Large UTF8 transcript exceeded bound or lost latest entry");
        ReportingDiagnosticsBuffer smallEntries = new ReportingDiagnosticsBuffer();
        for (int i = 0; i < 3000; i++) smallEntries.Add("mod Info", "turn " + i, "", now.AddSeconds(i), false);
        Check(smallEntries.Capture().Contains("turn 2999") && !smallEntries.Capture().Contains("turn 0\n"), "Small log entries bypassed count bound");
        Check(smallEntries.Pending == null, "Normal transcript produced error offer");
    }
    private static void Offers()
    {
        DateTime now = DateTime.UtcNow;
        ReportingDiagnosticsBuffer buffer = new ReportingDiagnosticsBuffer();
        buffer.Add("mod", "first", "", now);
        ReportingDiagnosticsError first = buffer.Pending;
        Check(first != null && first.Summary == "first", "Missing initial offer");
        buffer.Acknowledge("not-the-id"); Check(buffer.Pending == first, "Stale dismissal cleared current offer");
        buffer.Acknowledge(first.Id); Check(buffer.Pending == null, "Dismissal failed");
        buffer.Add("mod", "second", "", now.AddSeconds(1)); Check(buffer.Pending == null, "Cooldown failed");
        buffer.Add("mod", "third", "", now.AddMinutes(6)); Check(buffer.Pending != null, "New error after cooldown missing");
        buffer.Acknowledge(buffer.Pending.Id);
        buffer.Add("mod", "fourth", "", now.AddMinutes(12)); buffer.Acknowledge(buffer.Pending.Id);
        buffer.Add("mod", "fifth", "", now.AddMinutes(18)); Check(buffer.Pending == null, "Session offer cap failed");
    }
    private static void SessionPersistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "ftk-diagnostics-test-" + Guid.NewGuid().ToString("N"));
        if (root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        try
        {
            ReportingSessionStore first;
            Check(ReportingSessionStore.TryOpen(root, DateTime.UtcNow, out first), "First session failed");
            string firstId = first.SessionId;
            ReportingDiagnosticsStore diagnostics = new ReportingDiagnosticsStore(first, root);
            diagnostics.Write("old complete snapshot"); diagnostics.Write("new snapshot");
            string unrelated = Path.Combine(root, "diagnostics-" + new string('c', 32) + "-0.record");
            File.WriteAllText(unrelated, "unrelated user file");
            ReportingSessionStore collision;
            Check(!ReportingSessionStore.TryOpen(root, DateTime.UtcNow, out collision), "Second session stole lease");
            Check(first.TryCheckpoint("metadata", "title", DateTime.UtcNow), "Diagnostics broke root invariants");
            first.Dispose();
            bool refused = false; try { diagnostics.Write("after release"); } catch (IOException) { refused = true; }
            Check(refused, "Writer accepted released lease");
            // Simulate a torn most recent write. The previous complete slot must survive.
            File.WriteAllText(Path.Combine(root, "diagnostics-" + firstId + "-0.record"), "torn");
            ReportingSessionStore next;
            Check(ReportingSessionStore.TryOpen(root, DateTime.UtcNow, out next), "Restart session failed");
            string nextId = next.SessionId;
            ReportingDiagnosticsStore nextDiagnostics = new ReportingDiagnosticsStore(next, root);
            Check(nextDiagnostics.PreviousSessionId == firstId && nextDiagnostics.Previous == "old complete snapshot", "Prior session recovery mismatch");
            Check(File.ReadAllText(unrelated) == "unrelated user file", "Matching filename used as ownership proof");
            nextDiagnostics.Write("new session evidence");
            Check(next.TryCheckpoint("metadata2", "title", DateTime.UtcNow), "Diagnostics broke later checkpoint");
            Check(next.TryRecordShutdown(DateTime.UtcNow), "Shutdown checkpoint failed"); next.Dispose();
            ReportingSessionStore third;
            Check(ReportingSessionStore.TryOpen(root, DateTime.UtcNow, out third), "Third session failed");
            ReportingDiagnosticsStore thirdDiagnostics = new ReportingDiagnosticsStore(third, root);
            Check(thirdDiagnostics.PreviousSessionId == firstId && thirdDiagnostics.Previous == "old complete snapshot", "Pending evidence replaced by unrelated intervening session");
            Check(Directory.GetFiles(root, "diagnostics-" + nextId + "-*").Length == 0, "Completed intervening session not pruned");
            Check(third.TryDismissPending(DateTime.UtcNow), "Pending dismissal failed");
            Check(third.TryRecordShutdown(DateTime.UtcNow), "Final shutdown failed"); third.Dispose();
            ReportingSessionStore fourth;
            Check(ReportingSessionStore.TryOpen(root, DateTime.UtcNow, out fourth), "Fourth session failed");
            ReportingDiagnosticsStore clean = new ReportingDiagnosticsStore(fourth, root);
            Check(clean.Previous == "" && clean.PreviousSessionId == null, "Unrelated logs attributed to clean launch");
            Check(!File.Exists(Path.Combine(root, "diagnostics-" + firstId + "-1.record")), "Dismissed verified diagnostics retained");
            Check(File.ReadAllText(unrelated) == "unrelated user file" && File.ReadAllText(Path.Combine(root, "diagnostics-" + firstId + "-0.record")) == "torn", "Unverifiable files deleted");
            for (int i = 0; i < 10; i++) clean.Write(new string('x', ReportingDiagnosticsBuffer.ByteLimit));
            string[] rolling = Directory.GetFiles(root, "diagnostics-" + fourth.SessionId + "-*");
            long storedBytes = 0; foreach (string path in rolling) storedBytes += new FileInfo(path).Length;
            Check(rolling.Length == 2 && storedBytes <= 2 * (ReportingDiagnosticsBuffer.ByteLimit + 128), "Rolling persistence exceeded slot/byte bounds");
            bool oversize = false;
            try { clean.Write(new string('x', ReportingDiagnosticsBuffer.ByteLimit + 1)); } catch (InvalidDataException) { oversize = true; }
            Check(oversize, "Oversize persistence accepted");
            string pressure = Path.Combine(root, "test-quota-pressure.bin");
            File.WriteAllBytes(pressure, new byte[ReportingSessionStore.StoreLimit]);
            bool full = false; try { clean.Write("at quota"); } catch (IOException) { full = true; }
            Check(full, "Diagnostics exceeded shared root quota");
            File.Delete(pressure);
            fourth.Dispose();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static void LargeTranscriptPersistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "ftk-diagnostics-transcript-" + Guid.NewGuid().ToString("N"));
        if (root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        try
        {
            ReportingSessionStore first;
            Check(ReportingSessionStore.TryOpen(root, DateTime.UtcNow, out first), "Transcript session failed");
            string firstId = first.SessionId;
            string unrelatedLog = Path.Combine(root, "LogOutput.log");
            File.WriteAllText(unrelatedLog, "another game instance private data");
            ReportingDiagnosticsBuffer buffer = new ReportingDiagnosticsBuffer();
            for (int i = 0; i < 40; i++) buffer.Add("mod Info", "turn " + i + new string('x', 5000), "", DateTime.UtcNow.AddSeconds(i), false);
            string transcript = buffer.Capture();
            Check(Encoding.UTF8.GetByteCount(transcript) > 32 * 1024, "Fixture did not exercise expanded transcript");
            ReportingDiagnosticsStore store = new ReportingDiagnosticsStore(first, root);
            store.Write(transcript);
            Check(first.TryCheckpoint("metadata", "session_or_transition", DateTime.UtcNow), "Transcript checkpoint failed");
            first.Dispose();
            ReportingSessionStore second;
            Check(ReportingSessionStore.TryOpen(root, DateTime.UtcNow, out second), "Transcript restart failed");
            ReportingDiagnosticsStore restored = new ReportingDiagnosticsStore(second, root);
            Check(restored.PreviousSessionId == firstId && restored.Previous == transcript, "Full transcript did not survive exact-session restart");
            Check(!restored.Previous.Contains("another game instance") && File.ReadAllText(unrelatedLog) == "another game instance private data", "Unrelated installation log read or changed");
            second.Dispose();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
