using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core;
using FTKModFramework.Core.Reporting;

internal sealed class MemoryStore : ITweakPreferenceStore
{
    internal readonly Dictionary<string, TweakPreference> Values = new Dictionary<string, TweakPreference>();
    public TweakPreference Read(string id) { TweakPreference value; return Values.TryGetValue(id, out value) ? value : TweakPreference.Default; }
    public void Write(string id, TweakPreference preference) { Values[id] = preference; }
}

internal static class Program
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Main()
    {
        var input = new ReportingMetadataInput { FrameworkVersion = "1.0.2", UnityVersion = "2017.2.2p2",
            GameVersion = "1.1.00", DataContent = true, BehaviorLoading = true, CampaignEngine = true, SelfTests = false, ScaleBudgetGate = false,
            Platform = "Unix", RuntimeVersion = "2.0.50727", ProcessBits = 64, Phase = "session_or_transition", SourcesReady = true };
        input.TweakState = new ReportingTweakState { Rows = new List<ReportingTweakRow>(), TotalCount = 0, SessionState = "none" };
        var rows = new List<ReportingInventoryRow> {
            new ReportingInventoryRow { Id = "test.mod", Version = "1.2.3", Enabled = true, PendingEnabled = false },
            new ReportingInventoryRow { Id = "/Users/private/name", Version = "https://private/token", Enabled = false },
            new ReportingInventoryRow { Id = "person@example.test", Version = "C:\\private\\secret" } };
        input.Mods = new ReportingInventory { Rows = rows, TotalCount = rows.Count };
        string json = ReportingMetadata.Capture(input, DateTime.UtcNow);
        Require(!json.Contains("private") && !json.Contains("example.test"), "Unsafe machine fields leaked");
        var snapshot = JObject.Parse(json);
        var mods = snapshot["sections"]["mods"]["payload"];
        Require((string)mods[2]["id"] == "test.mod" && (bool)mods[2]["enabled"] && !(bool)mods[2]["pendingEnabled"], "Selection lost");
        Require((string)mods[2]["outcome"] == "unknown", "Invented outcome");
        Require((string)snapshot["sections"]["logs"]["status"] == "omitted", "Logs enabled automatically");
        rows[0].Id = "changed.mod";
        Require(json.Contains("test.mod") && !json.Contains("changed.mod"), "Snapshot retained live references");
        input.Transitioning = true;
        Require((string)JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow))["sections"]["mods"]["reason"] == "transition_in_progress", "Transition leaked candidate rows");
        input.Transitioning = false; input.RuntimeFaulted = true;
        Require((string)JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow))["sections"]["mods"]["reason"] == "runtime_faulted", "Fault leaked rows");
        input.RuntimeFaulted = false; input.SourcesReady = false;
        Require((string)JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow))["sections"]["mods"]["reason"] == "not_initialized", "Early empty inventory claimed success");
        input.SourcesReady = true;
        var huge = new List<ReportingInventoryRow>();
        string maxField = new string('a', 128).Replace("a", "a-");
        for (int i = 0; i < 256; i++) huge.Add(new ReportingInventoryRow { Id = maxField, Version = maxField });
        input.Mods = input.Plugins = input.Active = input.Pending = new ReportingInventory { Rows = huge, TotalCount = 256 };
        json = ReportingMetadata.Capture(input, DateTime.UtcNow);
        Require(Encoding.UTF8.GetByteCount(json) <= ReportingMetadata.MaximumBytes, "Exceeded escaped byte cap");
        Require((string)JObject.Parse(json)["status"] == "partial", "Byte cap claimed complete");
        Require((string)JObject.Parse(json)["reason"] == "limit_reached", "Truncation not disclosed");
        Require(ReportingMetadata.Identifier(new string('a', 257)) == null && ReportingMetadata.Identifier("line\nbreak") == null, "Field cap/control accepted");
        foreach (string sensitive in new[] { "192.168.1.10", "mod.192.168.1.10", "192.168.1.10-mod", "999.192.168.1.10", "ghp_abc", "github_pat_abc", "sk-abc", "my_secret", new string('a', 32) })
            Require(ReportingMetadata.Identifier(sensitive) == null, "Sensitive identifier accepted: " + sensitive);
        var empty = new ReportingInventory { Rows = new List<ReportingInventoryRow>(), TotalCount = 0 };
        input.Mods = input.Plugins = input.Active = input.Pending = empty;
        var healthy = JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow));
        Require((string)healthy["status"] == "complete" && (string)healthy["reason"] == "none", "Invented source error");
        int calls = 0;
        var previous = new ReportingIncident { SessionId = "previous-session", CheckpointId = "previous-checkpoint",
            Metadata = "previous-mods", ObservedAtUtc = DateTime.UtcNow };
        var report = ReportingReport.Create(delegate { calls++; return json; }, previous, DateTime.UtcNow);
        previous.Metadata = "new-session-mods";
        Require(calls == 1 && report.IncludeMetadata && !report.IncludeLogs, "Creation did not collect automatically");
        Require(report.PreviousMetadata == "previous-mods" && report.CurrentMetadata == json, "Restart provenance overwritten");
        report.IncludeMetadata = false;
        Require(!report.IncludeMetadata && report.CurrentMetadata == json, "Exclusion destroyed local recovery evidence");
        var absentReport = ReportingReport.Create(delegate { return null; }, null, DateTime.UtcNow);
        Require((string)JObject.Parse(absentReport.CurrentMetadata)["status"] == "failed", "Absent capture claimed success");
        Require(report.ReportId != report.CaptureId, "Identities conflated");
        var failedReport = ReportingReport.Create(delegate { throw new Exception("private path or secret"); }, null, DateTime.UtcNow);
        Require(!failedReport.CurrentMetadata.Contains("private") && failedReport.CurrentMetadata.Contains("source_error"), "Collector failure leaked text");
        Tweaks();
        Console.WriteLine("PASS: bounded metadata, redaction, detached selection, missing/transition/fault authority, tweak preferences and effective set, and logs off");
    }

    private static JObject Snapshot(ReportingTweakState tweaks)
    {
        var input = new ReportingMetadataInput { FrameworkVersion = "1.0.2", SourcesReady = true, TweakState = tweaks };
        return JObject.Parse(ReportingMetadata.Capture(input, DateTime.UtcNow));
    }

    // FR-7 (#233): configured preferences and the effective set are separate sections, copied from a
    // real registry, allowlisted, sorted, row-capped and inside the byte cap.
    private static void Tweaks()
    {
        var store = new MemoryStore();
        var registry = new TweakRegistry(null);
        int fix = registry.Register(new TweakDescriptor("fix.sample", TweakCategory.Fix, TweakScope.Local, "Fix", "", ""));
        int info = registry.Register(new TweakDescriptor("information.sample", TweakCategory.Information, TweakScope.Local, "Info", "", ""));
        int rule = registry.Register(new TweakDescriptor("convenience.rule", TweakCategory.Convenience, TweakScope.Session, "Rule", "", ""));
        store.Values["information.sample"] = TweakPreference.On;
        store.Values["convenience.rule"] = TweakPreference.On;
        store.Values["fix.sample"] = TweakPreference.Off;
        Require(ReportingTweakSource.Copy(registry) == null, "An uninitialized registry reported tweak state");
        var cold = Snapshot(null);
        Require((string)cold["sections"]["tweakPreferences"]["status"] == "unavailable" && (string)cold["sections"]["tweakPreferences"]["reason"] == "not_initialized"
            && (string)cold["sections"]["tweakEffective"]["status"] == "unavailable" && (string)cold["status"] == "partial", "Absent tweak state claimed success");

        Require(registry.Initialize(store), "Registry did not initialize");
        registry.Capture(TweakSessionMode.SinglePlayer);
        registry.Lock();
        registry.Toggle(rule);
        registry.Fault(info, new InvalidOperationException("sample"));
        ReportingTweakState copy = ReportingTweakSource.Copy(registry);
        registry.Toggle(fix);
        var snapshot = Snapshot(copy);
        var preferences = snapshot["sections"]["tweakPreferences"];
        Require((string)preferences["status"] == "complete" && (int)preferences["retainedCount"] == 3 && (int)preferences["totalCount"] == 3
            && !(bool)preferences["fieldsExcluded"] && preferences["observedAt"].Type != JTokenType.Null, "Preference section bookkeeping");
        var prefRows = (JArray)preferences["payload"];
        Require((string)prefRows[0]["id"] == "convenience.rule" && (string)prefRows[0]["preference"] == "default"
            && (string)prefRows[1]["id"] == "fix.sample" && (string)prefRows[1]["preference"] == "off"
            && (string)prefRows[2]["id"] == "information.sample" && (string)prefRows[2]["preference"] == "on",
            "Preferences are not the stored choices in ordinal ID order: " + prefRows);
        var effective = snapshot["sections"]["tweakEffective"]["payload"];
        var session = effective["session"];
        Require((string)session["state"] == "locked" && (string)session["mode"] == "single_player" && (string)session["source"] == "preferences",
            "Session state, mode or source lost: " + session);
        var rows = (JArray)effective["tweaks"];
        Require((string)rows[0]["id"] == "convenience.rule" && (bool)rows[0]["on"], "A mid-run Session change leaked into the effective set");
        Require((string)rows[1]["id"] == "fix.sample" && !(bool)rows[1]["on"], "An explicit Off did not reach the effective set");
        Require((string)rows[2]["id"] == "information.sample" && !(bool)rows[2]["on"], "A faulted Local tweak reported on");
        var faulted = (JArray)effective["faulted"];
        Require(faulted.Count == 1 && (string)faulted[0] == "information.sample", "Faulted IDs lost");
        Require(copy.Rows[0].Id == "fix.sample" && copy.Rows[0].Preference == "off" && !copy.Rows[0].On, "The copy followed a later registry change");
        Require((string)snapshot["status"] == "partial" && (string)snapshot["sections"]["environment"]["status"] == "complete", "Unrelated sections changed");

        registry.Clear();
        var cleared = Snapshot(ReportingTweakSource.Copy(registry))["sections"]["tweakEffective"]["payload"];
        Require((string)cleared["session"]["state"] == "none" && cleared["session"]["mode"].Type == JTokenType.Null
            && cleared["session"]["source"].Type == JTokenType.Null && !(bool)cleared["tweaks"][0]["on"], "A cleared run kept Session state");
        registry.Capture(TweakSessionMode.Multiplayer);
        var coop = Snapshot(ReportingTweakSource.Copy(registry))["sections"]["tweakEffective"]["payload"]["session"];
        Require((string)coop["state"] == "captured" && (string)coop["mode"] == "multiplayer" && (string)coop["source"] == "pending", "Co-op capture misreported");
        Require(ReportingTweakSource.Mode(TweakSessionMode.LocalMultiplayer) == "local_multiplayer", "Local multiplayer wire name");

        // Values outside the allowlist or identifier syntax are excluded and disclosed, never passed through.
        var hostile = new ReportingTweakState { TotalCount = 3, SessionState = "running", SessionMode = "LocalMultiplayer", SessionSource = "https://private/token",
            Rows = new List<ReportingTweakRow> { new ReportingTweakRow { Id = "fix.my_secret", Preference = "7", Faulted = true },
                new ReportingTweakRow { Id = "/Users/private/tweak", Preference = "On, Off" }, null } };
        var bad = Snapshot(hostile);
        string badJson = bad.ToString();
        Require(!badJson.Contains("private") && !badJson.Contains("secret") && !badJson.Contains("running") && !badJson.Contains("On, Off"), "Unsafe tweak fields leaked");
        Require((bool)bad["sections"]["tweakPreferences"]["fieldsExcluded"] && (bool)bad["sections"]["tweakEffective"]["fieldsExcluded"], "Exclusion not disclosed");
        var badSession = bad["sections"]["tweakEffective"]["payload"]["session"];
        Require(badSession["state"].Type == JTokenType.Null && badSession["mode"].Type == JTokenType.Null && badSession["source"].Type == JTokenType.Null, "Session fields passed through");
        Require(((JArray)bad["sections"]["tweakPreferences"]["payload"]).Count == 3 && ((JArray)bad["sections"]["tweakEffective"]["payload"]["faulted"]).Count == 1,
            "Excluded rows were dropped instead of nulled");

        // Row cap: the copier and the snapshot both stop at MaximumRows and disclose the total.
        var many = new TweakRegistry(null);
        for (int i = 0; i < 300; i++) many.Register(new TweakDescriptor("fix.t" + i.ToString("D3"), TweakCategory.Fix, TweakScope.Local, "T", "", ""));
        many.Initialize(new MemoryStore());
        ReportingTweakState capped = ReportingTweakSource.Copy(many);
        Require(capped.Rows.Count == ReportingMetadata.MaximumRows && capped.TotalCount == 300, "Copier ignored the row cap");
        var limited = Snapshot(capped);
        Require((string)limited["sections"]["tweakPreferences"]["status"] == "partial" && (string)limited["sections"]["tweakPreferences"]["reason"] == "limit_reached"
            && (int)limited["sections"]["tweakEffective"]["retainedCount"] == 256 && (int)limited["sections"]["tweakEffective"]["totalCount"] == 300
            && (string)limited["reason"] == "limit_reached", "Tweak truncation not disclosed");

        // Byte cap: maximum tweak rows survive beside maximum inventories, and the total still fits.
        string maxField = new string('a', 128).Replace("a", "a-");
        var maxRows = new List<ReportingTweakRow>();
        for (int i = 0; i < 256; i++) maxRows.Add(new ReportingTweakRow { Id = maxField, Preference = "default", On = true, Faulted = true });
        var maxTweaks = new ReportingTweakState { Rows = maxRows, TotalCount = 256, SessionState = "locked", SessionMode = "local_multiplayer", SessionSource = maxField };
        var inventoryRows = new List<ReportingInventoryRow>();
        for (int i = 0; i < 256; i++) inventoryRows.Add(new ReportingInventoryRow { Id = maxField, Version = maxField });
        var huge = new ReportingInventory { Rows = inventoryRows, TotalCount = 256 };
        var full = new ReportingMetadataInput { FrameworkVersion = "1.0.2", SourcesReady = true, TweakState = maxTweaks,
            Mods = huge, Plugins = huge, Active = huge, Pending = huge };
        string fullJson = ReportingMetadata.Capture(full, DateTime.UtcNow);
        Require(Encoding.UTF8.GetByteCount(fullJson) <= ReportingMetadata.MaximumBytes, "Tweak sections broke the byte cap");
        var fullSnapshot = JObject.Parse(fullJson);
        Require(((JArray)fullSnapshot["sections"]["tweakPreferences"]["payload"]).Count == 256
            && ((JArray)fullSnapshot["sections"]["tweakEffective"]["payload"]["faulted"]).Count == 256, "Tweak sections were dropped at the byte cap");
        Console.WriteLine("Tweak sections at maximum input: " + Encoding.UTF8.GetByteCount(fullJson) + " of " + ReportingMetadata.MaximumBytes + " bytes");
    }
}
