using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    internal static class PlayerRaceRegistry
    {
        private sealed class Race
        {
            internal string Guid, Key, Name;
            internal readonly Dictionary<string, Binding> Classes = new Dictionary<string, Binding>(StringComparer.Ordinal);
        }

        private sealed class Binding
        {
            internal FTK_skinset.ID Template;
            internal FTK_skinset Skinset;
            internal PlayerMeshPlan Plan;
        }

        private static readonly Dictionary<int, Race> Races = new Dictionary<int, Race>();
        private static readonly HashSet<int> Collisions = new HashSet<int>();

        internal static int Register(string modGuid, string key, string displayName)
        {
            if (string.IsNullOrEmpty(modGuid) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(displayName) ||
                modGuid.IndexOf(':') >= 0 || key.IndexOf(':') >= 0)
                throw new ArgumentException("Race GUID, key, and display name are required.");
            string allocationKey = "PlayerRace/" + key;
            int expected = PlayerRaceSelection.UnprobedId(modGuid, allocationKey);
            if (Collisions.Contains(expected)) throw new InvalidOperationException("Race identity collision is disabled for this session.");
            Race collision;
            if (Races.TryGetValue(expected, out collision) && (collision.Guid != modGuid || collision.Key != key))
            {
                Races.Remove(expected);
                Collisions.Add(expected);
                throw new InvalidOperationException("Race identity collision: both race registrations disabled.");
            }
            int id = IdAllocator.Allocate(modGuid, allocationKey);
            // Collision probing is order-dependent. A persisted race must never accept a probed identity.
            if (id != expected)
                throw new InvalidOperationException("Race identity hash collision: choose another race key.");
            Race existing;
            if (Races.TryGetValue(id, out existing))
            {
                if (existing.Guid != modGuid || existing.Key != key)
                    throw new InvalidOperationException("Race identity is already registered.");
                return id;
            }
            Races.Add(id, new Race { Guid = modGuid, Key = key, Name = displayName });
            return id;
        }

        internal static bool Bind(int raceId, FTK_playerGameStart classRow, FTK_skinset.ID template,
            PlayerRendererMesh[] body, PlayerApparelMesh[] apparel)
        {
            Race race;
            if (!Races.TryGetValue(raceId, out race) || classRow == null || string.IsNullOrEmpty(classRow.m_ID))
                return Reject("registered race and class are required");
            FTK_playerGameStartDB classes = Content.Db<FTK_playerGameStartDB>();
            if (!object.ReferenceEquals(classes.GetEntryByStringID(classRow.m_ID), classRow))
                return Reject("class must be the exact current database row");
            FTK_skinsetDB skins = Content.Db<FTK_skinsetDB>();
            FTK_skinset donor = skins.GetEntry(template);
            if (donor == null || classRow.m_Skinsets == null || Array.IndexOf(classRow.m_Skinsets, template) < 0)
                return Reject("template must be a valid member of the class's skinsets");
            PlayerMeshPlan plan;
            string error;
            if (!PlayerMeshPlan.TryCreate(body, apparel, out plan, out error)) return Reject(error);
            Binding prior;
            if (race.Classes.TryGetValue(classRow.m_ID, out prior))
            {
                if (prior.Template != template) return Reject("an existing race binding cannot change its donor skinset");
                prior.Plan = plan;
                return true;
            }
            string skinKey = race.Guid + "/race/" + race.Key + "/" + classRow.m_ID;
            int existing;
            if (ContentRegistry.TryGetSyntheticId(skinKey, out existing, typeof(FTK_skinsetDB)))
                return Reject("race skinset identity is already registered");
            FTK_skinset clone = (FTK_skinset)ContentRegistry.Register(skins, race.Guid, skinKey, donor);
            race.Classes.Add(classRow.m_ID, new Binding { Template = template, Skinset = clone, Plan = plan });
            return true;
        }

        private static bool Reject(string reason)
        {
            Plugin.Log.LogWarning("SetRaceClassBodyMeshesFromGlb: " + reason + "; binding unchanged.");
            return false;
        }

        internal static bool TryName(int id, out string name)
        {
            Race race;
            bool found = Races.TryGetValue(id, out race);
            name = found ? race.Name : null;
            return found;
        }

        internal static void Disable(int id) { Races.Remove(id); }

        internal static PlayerMeshPlan GetPlan(FTK_playerGameStart row, FTK_skinset skinset)
        {
            if (row == null || skinset == null) return null;
            foreach (Race race in Races.Values)
            {
                Binding binding;
                if (race.Classes.TryGetValue(row.m_ID, out binding) && object.ReferenceEquals(binding.Skinset, skinset))
                    return binding.Plan;
            }
            return null;
        }

        internal static bool NeedsResolution(FTK_playerGameStart row, int selected)
        {
            return selected != -1 && (selected < 0 || row.m_Skinsets == null || selected >= row.m_Skinsets.Length);
        }

        internal static FTK_playerGameStart.SkinType NormalizePreview(FTK_playerGameStart row, FTK_playerGameStart.SkinType selected)
        {
            if (row == null || !NeedsResolution(row, (int)selected)) return selected;
            Race race;
            if (Races.TryGetValue((int)selected, out race) && race.Classes.ContainsKey(row.m_ID)) return selected;
            FTK_skinset.ID fallback = ResolveId(row, selected);
            int index = (int)row.m_DefaultSkinType;
            if (row.m_Skinsets != null && index >= 0 && index < row.m_Skinsets.Length && row.m_Skinsets[index] == fallback)
                return (FTK_playerGameStart.SkinType)index;
            if (row.m_Skinsets != null && fallback != FTK_skinset.ID.None)
                for (int i = 0; i < row.m_Skinsets.Length; i++)
                    if (row.m_Skinsets[i] == fallback) return (FTK_playerGameStart.SkinType)i;
            return FTK_playerGameStart.SkinType.None;
        }

        internal static FTK_skinset.ID ResolvePreviewId(FTK_playerGameStart row, uiQuickPlayerCreate preview)
        {
            // Normalize UI state before native avatar creation, text and preference serialization.
            // World characters keep their saved identity so reinstalling a missing race can recover it.
            preview.m_SkinType = NormalizePreview(row, preview.m_SkinType);
            return ResolveId(row, preview.m_SkinType);
        }

        internal static FTK_skinset.ID ResolveId(FTK_playerGameStart row, FTK_playerGameStart.SkinType selected)
        {
            Race race;
            Binding binding;
            if (Races.TryGetValue((int)selected, out race) && race.Classes.TryGetValue(row.m_ID, out binding))
                return (FTK_skinset.ID)Content.Db<FTK_skinsetDB>().GetIntFromID(binding.Skinset.m_ID);
            int index = (int)selected;
            if (row.m_Skinsets != null && index >= 0 && index < row.m_Skinsets.Length)
                return row.m_Skinsets[index];
            index = (int)row.m_DefaultSkinType;
            if (row.m_Skinsets != null && index >= 0 && index < row.m_Skinsets.Length &&
                row.m_Skinsets[index] != FTK_skinset.ID.None && Content.Db<FTK_skinsetDB>().GetEntry(row.m_Skinsets[index]) != null)
                return row.m_Skinsets[index];
            if (row.m_Skinsets != null)
                foreach (FTK_skinset.ID candidate in row.m_Skinsets)
                    if (candidate != FTK_skinset.ID.None && Content.Db<FTK_skinsetDB>().GetEntry(candidate) != null) return candidate;
            return FTK_skinset.ID.None;
        }

        internal static bool TryCycle(FTK_playerGameStart row, int current, bool backwards, out int selected)
        {
            List<int> custom = new List<int>();
            foreach (KeyValuePair<int, Race> pair in Races)
                if (pair.Value.Classes.ContainsKey(row.m_ID)) custom.Add(pair.Key);
            selected = current;
            if (custom.Count == 0 && !NeedsResolution(row, current)) return false;
            List<int> native = new List<int>();
            if (row.m_Skinsets != null)
                for (int i = 0; i < row.m_Skinsets.Length; i++)
                    if (row.m_Skinsets[i] != FTK_skinset.ID.None &&
                        FTK_loreExtraUnlockDB.GetDB().IsSkinUnlocked((FTK_playerGameStart.SkinType)i)) native.Add(i);
            selected = PlayerRaceSelection.Next(current, backwards, native, custom);
            return true;
        }
    }
}
