using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using FTKModFramework.Core.HotReload;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.UI
{
    internal sealed partial class ModsPanel
    {
        internal sealed class SavedSet
        {
            internal string Fingerprint, Summary, Unavailable;
            internal int SaveCount;
        }
        private SavedSet _savedSet;
        private string _savedCurrent, _savedPending;
        private static string GenerationId(ManagedSnapshot snapshot) { return snapshot == null ? "" : snapshot.GenerationId; }
        private static string BoundedSavedRecord(string path, int maximum)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > maximum)
                throw new IOException("Unsafe or oversized saved-set record.");
            return File.ReadAllText(path);
        }
        internal static SavedSet ReadSavedSet(string path)
        {
            SavedSet item = new SavedSet { Fingerprint = Path.GetFileNameWithoutExtension(path), Summary = "Unavailable saved mod set" };
            try
            {
                SaveSetIdentity.RequireHash(item.Fingerprint);
                if (item.Fingerprint != item.Fingerprint.ToLowerInvariant()) throw new IOException("Invalid saved-set filename.");
                JObject pin = JObject.Parse(BoundedSavedRecord(path, 4096));
                if ((int?)pin["schemaVersion"] != 1) throw new IOException("Unsupported save pin schema.");
                string generation = (string)pin["generation"];
                if (generation == null || generation.Length != 32) throw new IOException("Invalid saved generation.");
                foreach (char c in generation) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) throw new IOException("Invalid saved generation.");
                string folder = Path.Combine(Path.Combine(MarketplaceRuntime.StateRoot, "generations"), generation);
                SaveSetIdentity.ValidateOwnedDirectory(folder);
                JObject record = JObject.Parse(BoundedSavedRecord(Path.Combine(folder, "lock.json"), 2 * 1024 * 1024));
                ManagedSnapshot snapshot = MarketplaceProtocol.ReadGeneration(MarketplaceRuntime.StateRoot, generation);
                StringBuilder summary = new StringBuilder();
                foreach (PackageDescriptor package in snapshot.Packages)
                    if (package.Enabled) summary.Append(package.Name ?? package.ModGuid).Append(" ").Append(package.Version).Append("\n");
                item.Summary = summary.Length == 0 ? "No enabled community mods" : summary.ToString().TrimEnd();
                string library = SaveSetIdentity.DirectoryFor(Application.persistentDataPath, item.Fingerprint);
                SaveSetIdentity.ValidateOwnedDirectory(library);
                if (Directory.Exists(library))
                    foreach (string file in Directory.GetFiles(library))
                    {
                        if (!string.Equals(Path.GetExtension(file), ".run", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!SaveSetIdentity.IsSavePath(library, file, true)) throw new IOException("Unsafe save entry in library.");
                        item.SaveCount++;
                    }
                if ((string)record["frameworkVersion"] != Plugin.Version) throw new IOException("Requires a different framework version.");
                if (SaveNamespace.FingerprintFor(snapshot) != item.Fingerprint) throw new IOException("Game, package or registration settings differ from this library.");
            }
            catch (Exception error) { item.Unavailable = error.Message; }
            return item;
        }
        private void SavedSets()
        {
            TextLine("Saved mod sets", 36, 56);
            TextLine("Choose the original mod set for a save library. This prepares mods; it does not load a save.", 23, 64);
            string reason = HotReloadCoordinator.UnavailableReason();
            if (reason != null) TextLine(reason, 22, 64);
            List<SavedSet> items = new List<SavedSet>();
            try
            {
                string pins = Path.Combine(MarketplaceRuntime.StateRoot, "save-pins");
                SaveSetIdentity.ValidateOwnedDirectory(pins);
                if (Directory.Exists(pins))
                {
                    string[] paths = Directory.GetFiles(pins, "*.json");
                    Array.Sort(paths, StringComparer.Ordinal);
                    foreach (string path in paths)
                    {
                        SavedSet item = ReadSavedSet(path);
                        if (item.SaveCount > 0 || item.Unavailable != null) items.Add(item);
                    }
                }
            }
            catch (Exception error) { TextLine("Saved libraries are unavailable: " + error.Message, 22, 80); return; }
            if (items.Count == 0) { TextLine("No saved adventure libraries yet.", 24, 70); return; }
            const int pageSize = 3;
            int pages = (items.Count + pageSize - 1) / pageSize;
            _page = Math.Min(_page, pages - 1);
            for (int index = _page * pageSize; index < Math.Min(items.Count, (_page + 1) * pageSize); index++)
            {
                SavedSet item = items[index];
                TextLine(item.Summary + "\n" + item.SaveCount + " save(s) / " + item.Fingerprint.Substring(0, Math.Min(12, item.Fingerprint.Length)), 22, 88);
                if (item.Unavailable != null) TextLine(item.Unavailable, 20, 52);
                ActionButton("Review this saved mod set", delegate {
                    _message = "";
                    _savedSet = item;
                    _savedCurrent = GenerationId(MarketplaceRuntime.Active);
                    _savedPending = GenerationId(MarketplaceRuntime.Pending);
                    Navigate("saved-set-review");
                }, reason == null && item.Unavailable == null && !PanelBusy);
            }
            PageButtons(pages);
        }
        private void SavedSetReview()
        {
            TextLine("Review saved mod set", 36, 56);
            if (_savedSet == null) { TextLine("Select a saved mod set first.", 24, 64); return; }
            if (_message.Length > 0) TextLine(_message, 22, 64);
            TextLine(_savedSet.Summary + "\n" + _savedSet.SaveCount + " save(s)", 24, 120);
            TextLine("Preparing this mod set replaces any currently prepared community mod changes. Your running content and save files stay unchanged. Apply the prepared set before opening Resume.", 24, 130);
            string reason = HotReloadCoordinator.UnavailableReason();
            if (reason != null) TextLine(reason, 22, 72);
            PrimaryButton("Prepare this saved mod set", delegate {
                bool started = MarketplaceRuntime.RestoreSavedSet(_savedSet.Fingerprint, _savedCurrent, _savedPending, delegate(MarketplaceResult result) {
                    _message = result.Message ?? result.Status;
                    if (result.Ok) Navigate("maintenance");
                    else Refresh();
                });
                if (!started) _message = MarketplaceRuntime.Notice ?? "Could not prepare this saved mod set.";
                Refresh();
            }, reason == null && !PanelBusy);
        }
    }
}
