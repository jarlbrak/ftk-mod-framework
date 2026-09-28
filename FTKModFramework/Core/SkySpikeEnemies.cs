using System;
using System.Collections.Generic;
using System.Reflection;
using GridEditor;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FTKModFramework.Core
{
    // SKY SPIKE enemy airships (env-gated live-test spike, not a supported API). Inside the spike adventure, enemy
    // boat camps wear an airship keyed by their FTKDict_EnemyBoats faction through a mapping table, and sea combat
    // against them can use the authored-but-unused EnemyBoat diorama layout with an enemy airship under the enemies.
    internal static partial class SkySpike
    {
        // Several factions may share one model; airships.json "enemyMap" overrides entries.
        static readonly Dictionary<string, string> DefaultEnemyMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Pirate", "corsair" }, { "Gold", "corsair" },
            { "Goblin", "raider" }, { "Beastman", "raider" }, { "Tribal", "raider" },
            { "Undead", "ghost" }, { "Ghost", "ghost" },
            { "Cultist", "cult" },
        };

        // FTK_SKY_SPIKE_ENEMY_LAYOUT=0 keeps the vanilla Default layout; unset or 1 uses EnemyBoat when it validates.
        static readonly bool EnemyLayoutDisabled = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_ENEMY_LAYOUT") == "0";
        // FTK_SKY_SPIKE_HIDE_ENEMY_OTHERS=0 leaves layout scenery near the enemy targets visible under a custom enemy ship.
        static readonly bool HideEnemyOthers = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_HIDE_ENEMY_OTHERS") != "0";

        static readonly FieldInfo LayoutIdField = AccessTools.Field(typeof(Diorama), "m_LayoutID");
        static readonly MethodInfo ResetTargetQueueMethod = AccessTools.Method(typeof(Diorama), "_resetTargetQueue");
        static readonly HashSet<string> _loggedLayoutRefusals = new HashSet<string>();
        static readonly HashSet<int> _probedDioramas = new HashSet<int>();
        static readonly List<Renderer> _hiddenOthers = new List<Renderer>();
        static Diorama _layoutOverriddenOn;
        static bool _enemyLayoutFailed;

        static ShipModel _darkStandIn, _darkStandInFrom;
        static float _darkStandInTint = -1f;

        // ---- mapping and stand-in model ------------------------------------------------------------------

        static string EnemyModelFor(string faction)
        {
            JObject map = _tuning != null ? _tuning["enemyMap"] as JObject : null;
            JToken value = map != null ? map[faction] : null;
            if (value != null && value.Type == JTokenType.String && IsSafeKey((string)value)) return (string)value;
            string model;
            return DefaultEnemyMap.TryGetValue(faction, out model) ? model : "corsair";
        }

        static bool IsSafeKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 64) return false;
            foreach (char c in key)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_' || c == '-')) return false;
            return true;
        }

        static string TuningString(string name, string fallback)
        {
            JToken v = _tuning != null ? _tuning[name] : null;
            return v != null && v.Type == JTokenType.String && IsSafeKey((string)v) ? (string)v : fallback;
        }

        static float TuningNumber(string name, float fallback)
        {
            return _tuning != null ? Num(_tuning, name, fallback) : fallback;
        }

        // Missing enemy art: a darker copy of a player airship (airships.json "enemyFallback", default stormbreaker,
        // "enemyFallbackTint", default 0.45), with its own material copies so the player ship is not darkened.
        static ShipModel DarkStandIn()
        {
            string baseKey = TuningString("enemyFallback", "stormbreaker");
            float tint = Mathf.Clamp01(TuningNumber("enemyFallbackTint", 0.45f));
            ShipModel source = ModelFor(baseKey, SkyharborModel);
            if (source == null) return null;
            if (_darkStandIn != null && _darkStandInFrom == source && Mathf.Approximately(_darkStandInTint, tint)) return _darkStandIn;

            ShipModel dark = new ShipModel
            {
                Source = baseKey + " dark stand-in (" + source.Source + ", tint " + F2(tint) + ")",
                Generated = source.Generated, Bounds = source.Bounds, Vertices = source.Vertices,
                DeckFraction = source.DeckFraction, DeckMethod = source.DeckMethod
            };
            Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
            foreach (ShipPart part in source.Parts)
            {
                Material copy;
                if (!copies.TryGetValue(part.Material, out copy))
                {
                    copy = new Material(part.Material);
                    copy.name = part.Material.name + " (dark)";
                    Color c = copy.color;
                    copy.color = new Color(c.r * tint, c.g * tint, c.b * tint, c.a);
                    copies.Add(part.Material, copy);
                    dark.Materials.Add(copy);
                }
                dark.Parts.Add(new ShipPart { Name = part.Name, Mesh = part.Mesh, Material = copy, Pivot = part.Pivot });
            }
            _darkStandIn = dark;
            _darkStandInFrom = source;
            _darkStandInTint = tint;
            Plugin.Log.LogInfo("[SkySpike] enemy stand-in model built: " + dark.Source + ".");
            return dark;
        }

        static ShipModel EnemyModel(string faction, out string key)
        {
            key = "enemy_" + EnemyModelFor(faction);
            return ModelFor(key, DarkStandIn);
        }

        // ---- which encounter is against an enemy boat -----------------------------------------------------

        // The encounter hex's POI is the enemy boat camp (MiniHexEnemyCamp.IsBoat); otherwise a sea encounter
        // whose first enemy row has a boat faction.
        static FTKDict_EnemyBoats.EnemyBoats EncounterEnemyBoat(out string how)
        {
            how = "none";
            EncounterSession session = EncounterSession.Instance;
            if (session == null || session.m_EncounterHexes == null || session.m_EncounterHexes.Length == 0) return FTKDict_EnemyBoats.EnemyBoats.None;
            HexLand hex = session.m_EncounterHexes[0].GetHexLand();
            if (hex == null) return FTKDict_EnemyBoats.EnemyBoats.None;
            MiniHexEnemyCamp camp = hex.m_POI as MiniHexEnemyCamp;
            if (camp != null && camp.m_EnemyCombat != null && camp.IsBoat())
            {
                how = "boat camp '" + camp.m_EnemyCombat.m_ID + "' at hex " + HexKey(hex);
                return camp.m_EnemyCombat.m_Boat;
            }
            if (hex.m_Type == HexLand.Type.Sea && session.m_EncounterObjects != null && session.m_EncounterObjects.Length > 0)
            {
                FTK_enemyCombat row = FTK_enemyCombatDB.Get(session.m_EncounterObjects[0]);
                if (row != null && row.m_Boat != FTKDict_EnemyBoats.EnemyBoats.None)
                {
                    how = "sea enemy row '" + row.m_ID + "'";
                    return row.m_Boat;
                }
            }
            return FTKDict_EnemyBoats.EnemyBoats.None;
        }

        // ---- EnemyBoat layout --------------------------------------------------------------------------------

        // Prefix on Diorama._resetTargetQueue (its only caller passes EncounterSession.GetDioramaLayoutID()):
        // the diorama instance is available here to validate the layout before any vanilla code reads it.
        internal static void OnResetTargetQueue(Diorama diorama, ref Diorama.LayoutID layout)
        {
            _layoutOverriddenOn = null;
            if (EnemyLayoutDisabled || _enemyLayoutFailed || layout != Diorama.LayoutID.Default) return;
            if (!(diorama is DioramaBoat) || !SpikeActive()) return;
            string how;
            FTKDict_EnemyBoats.EnemyBoats faction = EncounterEnemyBoat(out how);
            if (faction == FTKDict_EnemyBoats.EnemyBoats.None) return;
            string reason;
            if (!EnemyLayoutUsable(diorama, out reason))
            {
                if (_loggedLayoutRefusals.Add(diorama.GetInstanceID() + reason))
                    Plugin.Log.LogInfo("[SkySpike] enemy-boat layout unusable (" + reason + "); keeping Default.");
                return;
            }
            layout = Diorama.LayoutID.EnemyBoat;
            _layoutOverriddenOn = diorama;
            Plugin.Log.LogInfo("[SkySpike] enemy-boat layout selected: faction " + faction + " (" + how + ").");
        }

        // If vanilla still throws with EnemyBoat, rerun once with Default and never try EnemyBoat again this session.
        internal static Exception OnResetTargetQueueFinished(Diorama diorama, Exception exception)
        {
            if (exception == null || diorama == null || _layoutOverriddenOn != diorama) return exception;
            _layoutOverriddenOn = null;
            _enemyLayoutFailed = true;
            Plugin.Log.LogError("[SkySpike] enemy-boat layout threw in _resetTargetQueue; retrying with Default and disabling it: " + exception);
            try
            {
                ResetTargetQueueMethod.Invoke(diorama, new object[] { Diorama.LayoutID.Default });
                return null;
            }
            catch (Exception retry)
            {
                Plugin.Log.LogError("[SkySpike] Default layout retry also failed: " + retry);
                return exception;
            }
        }

        // Mirrors what _resetTargetQueue, SetupTargets and the camera read: a target root with at least as many
        // Player and enemy targets as Default (and at least three each), each carrying DummyAttackSlide, an
        // Exit_target, a camera shot manager and an m_Others root (DioramaBoat toggles every layout's m_Others).
        static bool EnemyLayoutUsable(Diorama diorama, out string reason)
        {
            reason = null;
            Diorama.Layout layout, standard;
            if (diorama.m_LayoutTable == null || !diorama.m_LayoutTable.TryGetValue(Diorama.LayoutID.EnemyBoat, out layout) || layout == null)
                reason = "no EnemyBoat entry";
            else if (!diorama.m_LayoutTable.TryGetValue(Diorama.LayoutID.Default, out standard) || standard == null || standard.m_TargetRoot == null)
                reason = "no Default layout to compare";
            else if (layout.m_TargetRoot == null) reason = "EnemyBoat has no m_TargetRoot";
            else if (layout.m_CameraShotManager == null) reason = "EnemyBoat has no m_CameraShotManager";
            else if (layout.m_Others == null) reason = "EnemyBoat has no m_Others";
            else if (layout.m_TargetRoot.Find("Exit_target") == null) reason = "EnemyBoat has no Exit_target";
            else
            {
                int players = CountTargets(layout.m_TargetRoot, "Player"), enemies = CountTargets(layout.m_TargetRoot, "enemy");
                int defaultPlayers = CountTargets(standard.m_TargetRoot, "Player"), defaultEnemies = CountTargets(standard.m_TargetRoot, "enemy");
                if (players < 0 || enemies < 0) reason = "EnemyBoat target without DummyAttackSlide";
                else if (players < 3 || enemies < 3 || players < defaultPlayers || enemies < defaultEnemies)
                    reason = "EnemyBoat targets " + players + " Player/" + enemies + " enemy vs Default " + defaultPlayers + "/" + defaultEnemies;
            }
            return reason == null;
        }

        // Counts children whose name contains the tag (SetupTargets' own test); -1 if any lacks DummyAttackSlide.
        static int CountTargets(Transform root, string tag)
        {
            int count = 0;
            foreach (Transform child in root)
            {
                if (!child.name.Contains(tag)) continue;
                if (child.GetComponent<DummyAttackSlide>() == null) return -1;
                count++;
            }
            return count;
        }

        // ---- probe ------------------------------------------------------------------------------------------

        // Full layout dump once per diorama instance, then a short per-encounter line.
        static void ProbeDiorama(DioramaBoat diorama)
        {
            Diorama.LayoutID current = LayoutIdField != null ? (Diorama.LayoutID)LayoutIdField.GetValue(diorama) : Diorama.LayoutID.Default;
            string how;
            FTKDict_EnemyBoats.EnemyBoats faction = EncounterEnemyBoat(out how);
            if (_probedDioramas.Add(diorama.GetInstanceID()) && diorama.m_LayoutTable != null)
            {
                List<string> keys = new List<string>();
                foreach (Diorama.LayoutID key in diorama.m_LayoutTable.Keys) keys.Add(key.ToString());
                Plugin.Log.LogInfo("[SkySpike] probe: diorama '" + diorama.name + "' layouts [" + string.Join(", ", keys.ToArray()) + "].");
                int budget = 400;
                foreach (KeyValuePair<Diorama.LayoutID, Diorama.Layout> entry in diorama.m_LayoutTable)
                {
                    Diorama.Layout layout = entry.Value;
                    if (layout == null) { Plugin.Log.LogInfo("[SkySpike] probe:   layout " + entry.Key + ": null"); continue; }
                    string usable = "";
                    if (entry.Key == Diorama.LayoutID.EnemyBoat)
                    {
                        string reason;
                        usable = EnemyLayoutUsable(diorama, out reason) ? ", usable" : ", unusable (" + reason + ")";
                    }
                    Plugin.Log.LogInfo("[SkySpike] probe:   layout " + entry.Key + ": targetRoot " + Name(layout.m_TargetRoot) +
                        " (" + (layout.m_TargetRoot != null ? layout.m_TargetRoot.childCount : 0) + " children), camera shots " +
                        (layout.m_CameraShotManager != null ? "'" + layout.m_CameraShotManager.name + "'" : "none") + ", others " +
                        Name(layout.m_Others) + (layout.m_Others != null ? " active " + layout.m_Others.gameObject.activeSelf : "") + usable);
                    if (layout.m_TargetRoot != null)
                        foreach (Transform t in layout.m_TargetRoot)
                            Plugin.Log.LogInfo("[SkySpike] probe:     target '" + t.name + "' at " + Fmt(t.position) +
                                (t.GetComponent<DummyAttackSlide>() != null ? " (DummyAttackSlide)" : ""));
                    if (layout.m_Others != null) DumpHierarchy(layout.m_Others, 0, ref budget);
                }
            }
            List<string> players = new List<string>(), enemies = new List<string>();
            foreach (Vector3 p in TargetPositions(diorama.m_PlayerTargets)) players.Add(Fmt(p));
            foreach (Vector3 p in TargetPositions(diorama.m_EnemyTargets)) enemies.Add(Fmt(p));
            Plugin.Log.LogInfo("[SkySpike] probe: encounter layout " + current + ", enemy boat " + faction + " (" + how + "), alignment " +
                Fmt(diorama.m_AlignmentDir) + ", players [" + string.Join(", ", players.ToArray()) + "], enemies [" +
                string.Join(", ", enemies.ToArray()) + "].");
        }

        static void DumpHierarchy(Transform t, int depth, ref int budget)
        {
            if (budget <= 0) return;
            if (--budget == 0) { Plugin.Log.LogInfo("[SkySpike] probe:     ... (hierarchy truncated)"); return; }
            string info = "";
            Renderer r = t.GetComponent<Renderer>();
            if (r != null)
            {
                bool any = false;
                Bounds b = new Bounds();
                MeshFilter mf = t.GetComponent<MeshFilter>();
                SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                Mesh mesh = mf != null ? mf.sharedMesh : smr != null ? smr.sharedMesh : null;
                if (mesh != null) Encapsulate(ref b, ref any, mesh.bounds, t.localToWorldMatrix);
                else if (r.enabled && t.gameObject.activeInHierarchy) { b = r.bounds; any = true; }
                info = ", " + r.GetType().Name + (r.enabled ? "" : " disabled") + (any ? " bounds center " + Fmt(b.center) + " size " + Fmt(b.size) : "");
            }
            Plugin.Log.LogInfo("[SkySpike] probe:     " + new string(' ', depth * 2) + "'" + t.name + "' active " + t.gameObject.activeSelf + info);
            if (depth >= 8) return;
            foreach (Transform child in t) DumpHierarchy(child, depth + 1, ref budget);
        }

        static string Name(Transform t)
        {
            return t != null ? "'" + t.name + "'" : "none";
        }

        // ---- combat: enemy airship under the enemy targets -------------------------------------------------

        static void RestoreHiddenOthers()
        {
            foreach (Renderer r in _hiddenOthers) if (r != null) r.enabled = true;
            _hiddenOthers.Clear();
        }

        // Returns the placed ship's keel height, or NaN when no enemy ship is shown.
        static float PlaceEnemyShip(DioramaBoat diorama, Transform parent, int layer, CombatFrame frame)
        {
            string how;
            FTKDict_EnemyBoats.EnemyBoats faction = EncounterEnemyBoat(out how);
            if (faction == FTKDict_EnemyBoats.EnemyBoats.None) return float.NaN;
            List<Vector3> enemies = TargetPositions(diorama.m_EnemyTargets);
            if (enemies.Count == 0)
            {
                Plugin.Log.LogWarning("[SkySpike] enemy combat ship skipped: no enemy targets.");
                return float.NaN;
            }
            RefreshTuning();
            string key;
            ShipModel model = EnemyModel(faction.ToString(), out key);
            if (model == null) return float.NaN;
            ShipTuning tune = TuningFor(key, model);
            ApplySurface(model, tune);
            GameObject go = CombatObject(CombatEnemyPrefix + key, model, tune, layer, parent, diorama);
            Placement placed = PlaceCombatShip(go, model, tune, enemies, Vector3.zero, frame, true, parent, key);
            int hidden = HideEnemyScenery(diorama, placed);
            Diorama.LayoutID current = LayoutIdField != null ? (Diorama.LayoutID)LayoutIdField.GetValue(diorama) : Diorama.LayoutID.Default;
            Plugin.Log.LogInfo("[SkySpike] enemy combat ship active: faction " + faction + " (" + how + ") -> " + key + " (" + model.Source +
                ") in layout " + current + ", deck at " + Fmt(placed.Target) + " from " + enemies.Count + " enemy targets, enemy spread " +
                F2(placed.Spread) + ", " + PlacementText(placed, model, tune) + ", hid " + hidden + " layout renderers.");
            return placed.KeelY;
        }

        // Hides layout scenery that would clip the custom enemy ship: active renderers of the current layout's
        // m_Others whose bounds cover the enemy centroid and are not scene-sized (sea planes, backdrops).
        static int HideEnemyScenery(Diorama diorama, Placement placed)
        {
            if (!HideEnemyOthers || diorama.m_LayoutTable == null || LayoutIdField == null) return 0;
            Diorama.Layout layout;
            if (!diorama.m_LayoutTable.TryGetValue((Diorama.LayoutID)LayoutIdField.GetValue(diorama), out layout) || layout == null || layout.m_Others == null)
                return 0;
            float margin = Mathf.Max(placed.Spread * 0.25f, 0.25f);
            float maxSize = Mathf.Max(placed.TargetLength, placed.Spread) * 3f;
            int hidden = 0;
            foreach (Renderer r in layout.m_Others.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                Bounds b = r.bounds;
                if (Mathf.Max(b.size.x, b.size.z) > maxSize) continue;
                if (placed.Target.x < b.min.x - margin || placed.Target.x > b.max.x + margin ||
                    placed.Target.z < b.min.z - margin || placed.Target.z > b.max.z + margin) continue;
                r.enabled = false;
                _hiddenOthers.Add(r);
                hidden++;
                Plugin.Log.LogInfo("[SkySpike] enemy combat ship: hid layout renderer '" + r.name + "' bounds center " + Fmt(b.center) +
                    " size " + Fmt(b.size) + ".");
            }
            return hidden;
        }

        // ---- overworld: enemy boat camps ------------------------------------------------------------------

        internal static void OnEnemyCampAlternateAsset(MiniHexEnemyCamp camp)
        {
            if (camp == null || camp.m_EnemyCombat == null || !SpikeActive() || !camp.IsBoat()) return;
            RefreshTuning();
            string faction = camp.m_EnemyCombat.m_Boat.ToString();
            string key;
            ShipModel model = EnemyModel(faction, out key);
            if (model == null) return;
            if (AttachOverworld(camp.m_AttachedObject, key, model, TuningFor(key, model)))
                Plugin.Log.LogInfo("[SkySpike] enemy airship art applied: '" + camp.m_EnemyCombat.m_ID + "' faction " + faction + " -> " + key +
                    " (" + model.Source + ") at hex " + HexKey(camp.m_HexLand) + ".");
        }
    }

    [HarmonyPatch(typeof(Diorama), "_resetTargetQueue")]
    internal static class SkySpikeEnemyLayoutPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Prefix(Diorama __instance, ref Diorama.LayoutID _layoutID)
        {
            try { SkySpike.OnResetTargetQueue(__instance, ref _layoutID); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] enemy-boat layout choice failed; keeping vanilla: " + e); }
        }

        static Exception Finalizer(Diorama __instance, Exception __exception)
        {
            try { return SkySpike.OnResetTargetQueueFinished(__instance, __exception); }
            catch (Exception) { return __exception; }
        }
    }

    [HarmonyPatch(typeof(MiniHexEnemyCamp), "GenerateAsAlternateAsset")]
    internal static class SkySpikeEnemyCampArtPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(MiniHexEnemyCamp __instance)
        {
            try { SkySpike.OnEnemyCampAlternateAsset(__instance); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] enemy airship art failed: " + e); }
        }
    }
}
