using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Reporting
{
    // Detached allowlisted input. Adapters must copy on the Unity thread; no live objects enter reports.
    internal sealed class ReportingInventoryRow
    {
        internal string Id, Version;
        internal bool Enabled;
        internal bool? PendingEnabled;
    }

    internal sealed class ReportingInventory
    {
        internal IList<ReportingInventoryRow> Rows;
        internal int TotalCount;
    }

    // Detached copy of one registered tweak. Preference is the stored choice ("default", "on" or
    // "off"); On and Faulted are what Tweaks.IsOn and the fault flag report at copy time.
    internal sealed class ReportingTweakRow
    {
        internal string Id, Preference;
        internal bool On, Faulted;
    }

    // Detached tweak registry state. Null means the registry never initialized, so every tweak is off.
    internal sealed class ReportingTweakState
    {
        internal IList<ReportingTweakRow> Rows;
        internal int TotalCount;
        internal string SessionState, SessionMode, SessionSource;
    }

    internal sealed class ReportingMetadataInput
    {
        internal string FrameworkVersion, UnityVersion, GameVersion;
        internal string Platform, RuntimeVersion, Phase;
        internal int ProcessBits;
        internal bool SourcesReady, Transitioning, RuntimeFaulted;
        internal bool? DataContent, BehaviorLoading, CampaignEngine, SelfTests, ScaleBudgetGate;
        internal ReportingInventory Mods, Plugins, Active, Pending;
        internal ReportingTweakState TweakState;
    }

    internal static class ReportingMetadata
    {
        internal const int MaximumRows = 256;
        // Reserve record identity/timestamps and framing inside the 256 KiB checkpoint cap.
        internal const int MaximumBytes = 256 * 1024 - 1024;

        // Free-form names, descriptions, paths and exception strings are intentionally absent.
        // Restrict machine fields to short identifier syntax; this is not a general log sanitizer.
        internal static string Identifier(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 256) return null;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_' || c == '+')) return null;
            }
            string lower = value.ToLowerInvariant();
            if (lower.Contains("ghp_") || lower.Contains("github_pat_") || lower.Contains("sk-") ||
                lower.Contains("token") || lower.Contains("secret") || lower.Contains("password") ||
                lower.Contains("apikey") || lower.Contains("api_key")) return null;
            int run = 0;
            foreach (char c in value)
            {
                run = Char.IsLetterOrDigit(c) ? run + 1 : 0;
                if (run >= 32) return null;
            }
            if (ContainsAddress(value)) return null;
            if (value == "." || value == "..") return null;
            return value;
        }

        private static bool ContainsAddress(string value)
        {
            for (int start = 0; start < value.Length; start++)
            {
                if (value[start] < '0' || value[start] > '9' ||
                    (start > 0 && value[start - 1] >= '0' && value[start - 1] <= '9')) continue;
                int cursor = start;
                bool valid = true;
                for (int part = 0; part < 4; part++)
                {
                    int octet = 0, digits = 0;
                    while (cursor < value.Length && value[cursor] >= '0' && value[cursor] <= '9')
                    {
                        if (++digits > 3) { valid = false; break; }
                        octet = octet * 10 + value[cursor++] - '0';
                    }
                    if (!valid || digits == 0 || octet > 255) { valid = false; break; }
                    if (part < 3 && (cursor >= value.Length || value[cursor++] != '.')) { valid = false; break; }
                }
                if (valid) return true;
            }
            return false;
        }

        private static Dictionary<string, object> Section(string status, string reason, object payload)
        {
            return new Dictionary<string, object> { { "status", status }, { "reason", reason }, { "payload", payload } };
        }

        private static object Inventory(ReportingInventory inventory, string unavailable, bool selection)
        {
            if (unavailable != null || inventory == null || inventory.Rows == null)
                return Section("unavailable", unavailable ?? "source_absent", null);
            IList<ReportingInventoryRow> source = inventory.Rows;
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            bool redacted = false;
            int count = Math.Min(MaximumRows, source.Count);
            for (int i = 0; i < count; i++)
            {
                ReportingInventoryRow item = source[i];
                string id = item == null ? null : Identifier(item.Id);
                string version = item == null ? null : Identifier(item.Version);
                if (id == null || (item != null && item.Version != null && version == null)) redacted = true;
                rows.Add(new Dictionary<string, object> { { "id", id }, { "version", version },
                    { "enabled", selection && item != null ? (object)item.Enabled : null },
                    { "pendingEnabled", selection && item != null ? item.PendingEnabled : null },
                    { "outcome", "unknown" } });
            }
            rows.Sort(delegate(Dictionary<string, object> a, Dictionary<string, object> b)
            {
                int result = StringComparer.Ordinal.Compare((string)a["id"], (string)b["id"]);
                return result != 0 ? result : StringComparer.Ordinal.Compare((string)a["version"], (string)b["version"]);
            });
            Dictionary<string, object> section = Section(inventory.TotalCount > count ? "partial" : "complete",
                inventory.TotalCount > count ? "limit_reached" : "none", rows);
            section.Add("retainedCount", count);
            section.Add("totalCount", inventory.TotalCount);
            section.Add("fieldsExcluded", redacted);
            return section;
        }

        private static readonly string[] PreferenceValues = { "default", "on", "off" };
        private static readonly string[] StateValues = { "none", "captured", "locked" };
        private static readonly string[] ModeValues = { "single_player", "multiplayer", "local_multiplayer" };

        private static string Allowed(string value, string[] allowed)
        {
            foreach (string candidate in allowed) if (value == candidate) return candidate;
            return null;
        }

        private static int CompareIds(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            return StringComparer.Ordinal.Compare((string)a["id"], (string)b["id"]);
        }

        private static Dictionary<string, object> Counted(Dictionary<string, object> section, int retained, int total, bool redacted)
        {
            section.Add("retainedCount", retained);
            section.Add("totalCount", total);
            section.Add("fieldsExcluded", redacted);
            return section;
        }

        // Two sections: what the player configured, and what patches see now. They differ during a
        // run (Session choices wait for the next capture) and after a fault. Neither reads hot-reload
        // authority, because the registry is static and its patches are installed once.
        private static void AddTweaks(Dictionary<string, object> sections, ReportingTweakState tweaks)
        {
            if (tweaks == null || tweaks.Rows == null)
            {
                sections.Add("tweakPreferences", Section("unavailable", "not_initialized", null));
                sections.Add("tweakEffective", Section("unavailable", "not_initialized", null));
                return;
            }
            IList<ReportingTweakRow> source = tweaks.Rows;
            int count = Math.Min(MaximumRows, source.Count);
            List<Dictionary<string, object>> preferences = new List<Dictionary<string, object>>();
            List<Dictionary<string, object>> effective = new List<Dictionary<string, object>>();
            List<string> faulted = new List<string>();
            bool idsExcluded = false, preferenceExcluded = false;
            for (int i = 0; i < count; i++)
            {
                ReportingTweakRow row = source[i];
                string id = row == null ? null : Identifier(row.Id);
                string preference = row == null ? null : Allowed(row.Preference, PreferenceValues);
                if (id == null) idsExcluded = true;
                if (preference == null) preferenceExcluded = true;
                preferences.Add(new Dictionary<string, object> { { "id", id }, { "preference", preference } });
                effective.Add(new Dictionary<string, object> { { "id", id }, { "on", row == null ? null : (object)row.On } });
                if (row != null && row.Faulted) faulted.Add(id);
            }
            preferences.Sort(CompareIds);
            effective.Sort(CompareIds);
            faulted.Sort(StringComparer.Ordinal);
            bool limited = tweaks.TotalCount > count;
            string status = limited ? "partial" : "complete", reason = limited ? "limit_reached" : "none";
            sections.Add("tweakPreferences", Counted(Section(status, reason, preferences), count, tweaks.TotalCount,
                idsExcluded || preferenceExcluded));
            string state = Allowed(tweaks.SessionState, StateValues);
            string mode = Allowed(tweaks.SessionMode, ModeValues);
            string label = Identifier(tweaks.SessionSource);
            bool sessionExcluded = state == null || (tweaks.SessionMode != null && mode == null) ||
                (tweaks.SessionSource != null && label == null);
            Dictionary<string, object> payload = new Dictionary<string, object> {
                { "session", new Dictionary<string, object> { { "state", state }, { "mode", mode }, { "source", label } } },
                { "tweaks", effective }, { "faulted", faulted } };
            sections.Add("tweakEffective", Counted(Section(status, reason, payload), count, tweaks.TotalCount,
                idsExcluded || sessionExcluded));
        }

        internal static string Capture(ReportingMetadataInput input, DateTime observedAtUtc)
        {
            if (input == null) throw new ArgumentNullException("input");
            string unavailable = input.RuntimeFaulted ? "runtime_faulted" : input.Transitioning ? "transition_in_progress" :
                !input.SourcesReady ? "not_initialized" : null;
            Dictionary<string, object> sections = new Dictionary<string, object>();
            sections.Add("environment", Section("complete", "none", new Dictionary<string, object> {
                { "frameworkVersion", Identifier(input.FrameworkVersion) }, { "unityVersion", Identifier(input.UnityVersion) },
                { "platform", Identifier(input.Platform) }, { "runtimeVersion", Identifier(input.RuntimeVersion) },
                { "processBits", input.ProcessBits == 32 || input.ProcessBits == 64 ? (object)input.ProcessBits : null } }));
            string gameVersion = Identifier(input.GameVersion);
            sections.Add("gameVersion", Section(gameVersion != null ? "complete" : input.GameVersion == null ? "unavailable" : "omitted",
                gameVersion != null ? "none" : input.GameVersion == null ? "source_absent" : "policy_excluded", gameVersion));
            string phase = input.Phase == "title" || input.Phase == "session_or_transition" ? input.Phase : "startup_or_unknown";
            sections.Add("context", Section("complete", "none", new Dictionary<string, object> {
                { "phase", phase }, { "role", null }, { "adventure", null } }));
            bool settingsReady = input.DataContent.HasValue && input.BehaviorLoading.HasValue && input.CampaignEngine.HasValue &&
                input.SelfTests.HasValue && input.ScaleBudgetGate.HasValue;
            bool settingsPresent = input.DataContent.HasValue || input.BehaviorLoading.HasValue || input.CampaignEngine.HasValue ||
                input.SelfTests.HasValue || input.ScaleBudgetGate.HasValue;
            sections.Add("settings", Section(settingsReady ? "complete" : settingsPresent ? "partial" : "unavailable",
                settingsReady ? "none" : settingsPresent ? "source_error" : "not_initialized", !settingsPresent ? null : new Dictionary<string, object> {
                { "enableDataContent", input.DataContent }, { "enableBehaviorLoading", input.BehaviorLoading },
                { "enableCampaignEngine", input.CampaignEngine }, { "runSelfTests", input.SelfTests },
                { "scaleBudgetGate", input.ScaleBudgetGate }, { "applied", "unknown" } }));
            sections.Add("mods", Inventory(input.Mods, unavailable, true));
            sections.Add("plugins", Inventory(input.Plugins, unavailable, false));
            sections.Add("managedActive", Inventory(input.Active, unavailable, true));
            sections.Add("managedPending", Inventory(input.Pending, unavailable, true));
            AddTweaks(sections, input.TweakState);
            sections.Add("logs", Section("omitted", "user_declined", null));
            bool incomplete = false, limited = false;
            foreach (Dictionary<string, object> section in sections.Values)
            {
                if ((string)section["status"] != "complete" && (string)section["status"] != "omitted") incomplete = true;
                if ((string)section["reason"] == "limit_reached") limited = true;
            }
            Dictionary<string, object> envelope = new Dictionary<string, object> {
                { "schemaVersion", 1 }, { "observedAt", observedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) },
                { "status", incomplete ? "partial" : "complete" }, { "reason", limited ? "limit_reached" : incomplete ? "source_error" : "none" }, { "sections", sections } };
            foreach (Dictionary<string, object> section in sections.Values)
            {
                section["observedAt"] = section["payload"] == null ? null : envelope["observedAt"];
                if (!section.ContainsKey("retainedCount")) section["retainedCount"] = null;
                if (!section.ContainsKey("totalCount")) section["totalCount"] = null;
            }
            // Exact post-escaping bytes govern the cap. Keep basic context if maximum inventories do not fit.
            // Tweak sections stay: with 256 rows of 256-byte IDs the snapshot is about 213 KB once the
            // inventories are dropped, so it still fits. Tests/ReportingMetadata holds that bound.
            string json = JsonConvert.SerializeObject(envelope);
            if (Encoding.UTF8.GetByteCount(json) <= MaximumBytes) return json;
            foreach (string key in new[] { "mods", "plugins", "managedActive", "managedPending" })
            {
                if (((Dictionary<string, object>)sections[key])["payload"] == null) continue;
                Dictionary<string, object> dropped = Section("partial", "limit_reached", null);
                dropped["observedAt"] = envelope["observedAt"];
                dropped["retainedCount"] = 0;
                dropped["totalCount"] = ((Dictionary<string, object>)sections[key])["totalCount"];
                sections[key] = dropped;
            }
            envelope["status"] = "partial";
            envelope["reason"] = "limit_reached";
            return JsonConvert.SerializeObject(envelope);
        }
    }
}
