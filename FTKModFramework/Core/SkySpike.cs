using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    // SKY SPIKE: an environment-gated live-test proof of concept, NOT a supported API or content.
    // It answers one visual question: can the archipelago's water read as a cloud sea and its boats
    // as airships. Everything here is inert unless FTK_SKY_SPIKE=1 is set when the plugin loads: the
    // adventure, airship item and airship boat row are not registered and every patch class below fails
    // Prepare(), so PatchAll installs nothing. With the variable set, the cloud sea, sky and boat shop
    // entry act only while the active adventure is the spike adventure; the airship art and combat hull
    // follow the airship boat type itself. Everything visual uses owned materials, meshes and GameObjects
    // on per-map instances (never a prefab, shared mesh or shared material).
    internal static partial class SkySpike
    {
        internal const string EnvVar = "FTK_SKY_SPIKE";
        internal const string SaveName = "skyspike";
        const string DisplayName = "Skyreach (Spike)";
        const string TemplateName = "Pirates";

        // Read once when the plugin first touches this type (Prepare() during Plugin.Awake PatchAll).
        internal static readonly bool Enabled = Environment.GetEnvironmentVariable(EnvVar) == "1";

        static readonly Color SkyBlue = new Color(0.53f, 0.77f, 0.96f, 1f);

        static Texture2D _noise;
        static Material _cloudSurface, _cloudSeabed;
        static int _surfaceCount, _seabedCount, _hexCount;

        static Camera _skyCamera;
        static Color _savedBackground;

        // ---- registration (TableManager.Initialize postfix, first run) ---------------------------------

        // Runs at the single content entry point, before the title screen, so the item and boat rows exist
        // before any save load re-instantiates MMboat (MiniHexBoat.Awake2 -> GetDBEntry). Each part fails alone.
        internal static void Register()
        {
            try { RegisterAdventure(); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] adventure registration threw: " + e); }
            RegisterShipTypes();
        }

        internal static bool TryResolveBoatRow(string id, out FTK_boat.ID result)
        {
            int value;
            if (id != null && ContentRegistry.TryGetSyntheticId(id, out value, typeof(FTK_boatDB)))
            {
                result = (FTK_boat.ID)value;
                return true;
            }
            result = FTK_boat.ID.None;
            return false;
        }

        // ---- adventure ------------------------------------------------------------------------------

        static void RegisterAdventure()
        {
            GameDefinitionPreview preview = Adventures.AddFromTemplate(
                Plugin.Guid, SaveName, TemplateName, DisplayName,
                "Live-test spike: the archipelago above a cloud sea, sailed by airships. Not a supported adventure.",
                jo => { jo["m_HasKraken"] = false; });
            if (preview != null)
                Plugin.Log.LogInfo("[SkySpike] adventure '" + SaveName + "' registered from template '" + TemplateName + "'.");
            else
                Plugin.Log.LogError("[SkySpike] adventure registration failed; the spike visuals will never activate.");
        }

        static bool SpikeActive()
        {
            GameLogic logic = GameLogic.Instance;
            if (logic == null) return false;
            GameDefinition def = logic.GetGameDef();
            return def != null && string.Equals(def.m_SaveFileName, SaveName, StringComparison.Ordinal);
        }

        // ---- cloud sea ------------------------------------------------------------------------------

        internal static void OnHexInfoPostAwake(HexInfo hex)
        {
            if (!SpikeActive())
            {
                // A vanilla adventure after a spike adventure in the same session: undo only our own camera edit.
                RestoreSky();
                return;
            }
            if (hex == null) return;
            EnsureCloudMaterials();

            int surfaces = 0, seabeds = 0;
            foreach (Renderer renderer in hex.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                // WaterDistort keeps animating its per-instance mesh; only the renderer's material slots change.
                if (renderer.GetComponent<WaterDistort>() != null || renderer.GetComponent<IslandWaterDistort>() != null)
                {
                    Material[] current = renderer.sharedMaterials;
                    int count = Math.Max(1, current.Length);
                    bool already = current.Length == count;
                    for (int i = 0; already && i < current.Length; i++) already = current[i] == _cloudSurface;
                    if (already) continue;
                    Material[] replacement = new Material[count];
                    for (int i = 0; i < count; i++) replacement[i] = _cloudSurface;
                    renderer.sharedMaterials = replacement;
                    surfaces++;
                    continue;
                }

                // sharedMaterials returns a copy, so editing it and assigning back changes only this renderer.
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].name.StartsWith("matOceanSwatch", StringComparison.Ordinal))
                    {
                        slots[i] = _cloudSeabed;
                        changed = true;
                    }
                }
                if (changed)
                {
                    renderer.sharedMaterials = slots;
                    seabeds++;
                }
            }

            _surfaceCount += surfaces;
            _seabedCount += seabeds;
            _hexCount++;
            if (surfaces > 0 || seabeds > 0)
                Plugin.Log.LogDebug("[SkySpike] hex '" + hex.name + "': " + surfaces + " surfaces, " + seabeds + " seabeds.");
            ApplySky();
        }

        internal static void OnMapDataInitialized()
        {
            if (!SpikeActive()) return;
            ApplySky();
            Plugin.Log.LogInfo("[SkySpike] cloud sea: " + _surfaceCount + " surfaces, " + _seabedCount +
                " seabeds across " + _hexCount + " hexes.");
            _surfaceCount = 0;
            _seabedCount = 0;
            _hexCount = 0;
            _testBoatSpawned = false;
            _partySurveyDone = false;
            _nearestPort = null;
            // The party may not be placed yet, so this pass has no distances; Tick repeats it once it is.
            try { LogBoatSellers(null, "map ready"); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] boat seller survey failed: " + e); }
        }

        // ---- live-test aids ---------------------------------------------------------------------------

        // FTK_SKY_SPIKE_TEST_BOAT=1 spawns one airship boat (type FTK_SKY_SPIKE_TEST_SHIP=<key>, default
        // clipper) through the native master-only SpawnBoatHex path on the nearest vacant water hex.
        // FTK_SKY_SPIKE_TEST_BOAT=2 also spawns a vanilla boat on a different, non-adjacent water hex so both
        // appear in one screenshot. FTK_SKY_SPIKE_TEST_BOAT=all spawns every registered airship type, each on its
        // own vacant water hex with no two adjacent.
        // FTK_SKY_SPIKE_TEST_BOAT_NEAR=port places the first test ship on a vacant water hex beside the nearest
        // Port (by hex steps from the party) instead of beside the party.
        // FTK_SKY_SPIKE_TEST_ENEMY=1 then spawns one native sea enemy on a vacant water hex beside the first ship.
        static readonly string TestBoatMode = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_TEST_BOAT");
        static readonly string TestShipKey = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_TEST_SHIP");
        static readonly bool TestBoatNearPort = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_TEST_BOAT_NEAR") == "port";
        static readonly bool TestEnemy = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_TEST_ENEMY") == "1";
        static bool _testBoatSpawned, _partySurveyDone;
        static float _nextTestBoatCheck;
        static MiniHexInfo _nearestPort;

        static bool TestBoatRequested { get { return TestBoatMode == "1" || TestBoatMode == "2" || TestBoatMode == "all"; } }

        // FTK_SKY_SPIKE_LOG_SCREEN=1 (test aid): every two seconds on the overworld, log each on-screen enemy boat
        // camp's screen position (bottom-left origin, the native-input frame) so automation can click it.
        static readonly bool ScreenLogRequested = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_LOG_SCREEN") == "1";
        static float _nextScreenLog;

        // FTK_SKY_SPIKE_TEST_CAMP=<enemy id> (test aid, master client): once per session, spawn that enemy on a vacant
        // water hex beside the first character, as a boat camp when its row has an enemy boat. Loads of a save with
        // the party aboard an airship then give a boarding fight one hex away.
        static readonly string TestCampId = NonEmpty(Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_TEST_CAMP"));
        static bool _testCampSpawned;
        static float _nextTestCampCheck;

        static string NonEmpty(string s) { return string.IsNullOrEmpty(s) ? null : s; }

        static void SpawnTestCampNearParty()
        {
            if (Time.unscaledTime < _nextTestCampCheck) return;
            _nextTestCampCheck = Time.unscaledTime + 1f;
            try
            {
                if (!SpikeActive() || GameFlowMC.Instance == null || !GameFlowMC.Instance.m_IsMapReady || !PhotonNetwork.isMasterClient) return;
                FTKHub hub = FTKHub.Instance;
                HexLand start = hub != null && hub.m_CharacterOverworlds.Count > 0 && hub.m_CharacterOverworlds[0] != null
                    ? hub.m_CharacterOverworlds[0].m_HexLand : null;
                if (start == null) return;
                _testCampSpawned = true;
                FTK_enemyCombat.ID id = FTK_enemyCombat.GetEnum(TestCampId);
                FTK_enemyCombat row = id != FTK_enemyCombat.ID.None ? FTK_enemyCombatDB.Get(id) : null;
                if (row == null) { Plugin.Log.LogWarning("[SkySpike] test camp: unknown enemy '" + TestCampId + "'."); return; }
                HexLand spot = VacantWaterNeighbor(start, null);
                if (spot == null) { Plugin.Log.LogWarning("[SkySpike] test camp: no vacant water hex beside " + HexKey(start) + "."); return; }
                bool camp = spot.m_Type == HexLand.Type.Sea && row.m_Boat != FTKDict_EnemyBoats.EnemyBoats.None;
                if (camp) GameFlowMC.Instance.SpawnEnemyCampHex(spot.m_ParentIndex, spot.m_Index, id.ToString());
                else GameFlowMC.Instance.SpawnEnemyHex(spot.m_ParentIndex, spot.m_Index, id.ToString());
                Plugin.Log.LogInfo("[SkySpike] test camp " + id + " spawned at hex " + HexKey(spot) + " beside " + HexKey(start) + " (" +
                    (camp ? "enemy boat camp, " + row.m_Boat : "enemy, no boat") + ").");
            }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] test camp failed: " + e); }
        }

        static void LogBoatCampScreens()
        {
            if (Time.unscaledTime < _nextScreenLog) return;
            _nextScreenLog = Time.unscaledTime + 2f;
            try
            {
                Camera camera = Camera.main;
                if (camera == null || !SpikeActive() || GameFlowMC.Instance == null || !GameFlowMC.Instance.m_IsMapReady) return;
                foreach (MiniHexEnemyCamp camp in UnityEngine.Object.FindObjectsOfType<MiniHexEnemyCamp>())
                {
                    if (camp == null || !camp.IsBoat() || camp.m_HexLand == null) continue;
                    Vector3 sp = camera.WorldToScreenPoint(camp.transform.position);
                    if (sp.z <= 0f || sp.x < 0f || sp.y < 0f || sp.x > Screen.width || sp.y > Screen.height) continue;
                    Plugin.Log.LogInfo("[SkySpike] test screen: boat camp at hex " + HexKey(camp.m_HexLand) + " screen " +
                        ((int)sp.x).ToString(CultureInfo.InvariantCulture) + " " + ((int)sp.y).ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] test screen log failed: " + e.Message); }
        }

        internal static void Tick()
        {
            if (ScreenLogRequested) LogBoatCampScreens();
            if (TestCampId != null && !_testCampSpawned) SpawnTestCampNearParty();
            if (Time.unscaledTime < _nextTestBoatCheck) return;
            if (_partySurveyDone && (_testBoatSpawned || !TestBoatRequested)) return;
            _nextTestBoatCheck = Time.unscaledTime + 1f;
            try
            {
                if (!SpikeActive()) return;
                // Wait for map ready so OnMapDataInitialized's per-map reset cannot re-arm a spawn already made.
                if (GameFlowMC.Instance == null || !GameFlowMC.Instance.m_IsMapReady) return;
                FTKHub hub = FTKHub.Instance;
                if (hub == null || hub.m_CharacterOverworlds.Count == 0) return;
                HexLand start = hub.m_CharacterOverworlds[0] != null ? hub.m_CharacterOverworlds[0].m_HexLand : null;
                if (start == null) return;

                if (!_partySurveyDone)
                {
                    _partySurveyDone = true;
                    _nearestPort = LogBoatSellers(start, "party placed");
                }
                if (!TestBoatRequested || _testBoatSpawned) return;
                if (!PhotonNetwork.isMasterClient) return;
                _testBoatSpawned = true;

                HexLand target = null;
                string near = "the party";
                if (TestBoatNearPort)
                {
                    if (_nearestPort != null && _nearestPort.m_HexLand != null)
                    {
                        target = VacantWaterNeighbor(_nearestPort.m_HexLand, null);
                        if (target != null) near = "port '" + PoiName(_nearestPort) + "'";
                        else Plugin.Log.LogWarning("[SkySpike] test boat: nearest port has no vacant water neighbor; using the party.");
                    }
                    else Plugin.Log.LogWarning("[SkySpike] test boat: no port reachable from the party; using the party.");
                }
                if (target == null) target = NearestVacantWater(start, 400, null);
                if (target == null)
                {
                    Plugin.Log.LogWarning("[SkySpike] test boat: no vacant water hex found near the party.");
                    return;
                }
                List<ShipType> ships = new List<ShipType>();
                if (TestBoatMode == "all") { foreach (ShipType t in Ships) if (t.Item >= 0) ships.Add(t); }
                else
                {
                    ShipType chosen = ShipByKey(string.IsNullOrEmpty(TestShipKey) ? "clipper" : TestShipKey);
                    if (chosen == null || chosen.Item < 0)
                        Plugin.Log.LogWarning("[SkySpike] test ship '" + TestShipKey + "' is not a registered airship type; using clipper.");
                    if (chosen == null || chosen.Item < 0) chosen = ShipByKey("clipper");
                    if (chosen != null && chosen.Item >= 0) ships.Add(chosen);
                }
                if (ships.Count == 0)
                {
                    Plugin.Log.LogWarning("[SkySpike] test boat: no airship type is registered; nothing spawned.");
                    return;
                }

                // The first spawn may not have claimed its hex's POI yet, so exclude each used hex and its ring.
                HashSet<HexLand> exclude = new HashSet<HexLand>();
                HexLand first = target;
                for (int i = 0; i < ships.Count; i++)
                {
                    HexLand hex = i == 0 ? target : NearestVacantWater(first, 600, exclude);
                    if (hex == null)
                    {
                        Plugin.Log.LogWarning("[SkySpike] test ship " + ships[i].Key + ": no further vacant water hex found.");
                        break;
                    }
                    GameFlowMC.Instance.SpawnBoatHex(hex.m_ParentIndex, hex.m_Index, (FTK_itembase.ID)ships[i].Item, Color.white);
                    Plugin.Log.LogInfo("[SkySpike] test ship " + ships[i].Key + " spawned at hex " + HexKey(hex) +
                        (i == 0 ? " near " + near : "") + " (item " + ships[i].Item + ").");
                    exclude.Add(hex);
                    if (hex.m_Neighbors != null)
                        foreach (HexLand n in hex.m_Neighbors) if (n != null) exclude.Add(n);
                }

                if (TestEnemy)
                {
                    try { SpawnTestSeaEnemy(target); }
                    catch (Exception e) { Plugin.Log.LogError("[SkySpike] test sea enemy failed: " + e); }
                }

                if (TestBoatMode != "2") return;
                HexLand second = NearestVacantWater(first, 600, exclude);
                if (second == null)
                {
                    Plugin.Log.LogWarning("[SkySpike] test vanilla boat: no second vacant water hex found.");
                    return;
                }
                GameFlowMC.Instance.SpawnBoatHex(second.m_ParentIndex, second.m_Index, FTK_itembase.ID.boat, Color.white);
                Plugin.Log.LogInfo("[SkySpike] test vanilla boat spawned at hex " + HexKey(second) + ".");
            }
            catch (Exception e)
            {
                _testBoatSpawned = true;
                _partySurveyDone = true;
                Plugin.Log.LogError("[SkySpike] test aid failed: " + e);
            }
        }

        // Mirrors EnemyManager's trickle spawn for one hex: the realm's own spawn pool filtered to the hex type
        // (m_SpawnWater for Sea), then SpawnEnemyCampHex for sea enemies with an enemy boat, else SpawnEnemyHex.
        static void SpawnTestSeaEnemy(HexLand boatHex)
        {
            HexLand spot = VacantWaterNeighbor(boatHex, null);
            if (spot == null || spot.m_HexInfo == null)
            {
                Plugin.Log.LogWarning("[SkySpike] test sea enemy: no vacant water hex beside the first test ship.");
                return;
            }
            GameLogic logic = GameLogic.Instance;
            FTK_realm.ID realm = spot.m_HexInfo.m_Realm;
            // The ocean realm has no GameStage (stage -1), and GetOverworldDifficultyLevel dereferences one;
            // the native trickle only spawns in realm-stage buckets. Level the enemy from the airship's land
            // neighborhood instead: the first staged realm around it, else the party's hex.
            HexLand levelHex = StagedNeighbor(boatHex) ?? FirstPartyHex();
            int level = 1;
            try
            {
                if (levelHex != null)
                    level = logic.GetOverworldDifficultyLevel(levelHex.m_HexInfo.m_Realm, levelHex.m_HexInfo.m_StageIndex, false);
            }
            catch (Exception e)
            {
                // Test aid only: an early-turn difficulty lookup can dereference unset state; level 1 suffices.
                Plugin.Log.LogWarning("[SkySpike] test sea enemy: difficulty lookup failed (" + e.GetType().Name + "); using level 1.");
            }
            FTKHub hub = FTKHub.Instance;
            bool night = hub != null && hub.m_TimeOfDayProperties != null && hub.m_TimeOfDayProperties.IsItNight();
            OverworldRealmProperties props = spot.GetOverworldRealmProperties();
            float multiplier = props != null ? props.m_GenericEnemyMultiplier : 0.5f;

            string source = "realm spawn pool";
            FTK_enemyCombat.ID id = logic.m_EnemyManager != null
                ? logic.m_EnemyManager._getRealmSpawnEnemy(level, realm, spot.m_Type, night, multiplier)
                : FTK_enemyCombat.ID.None;
            if (id == FTK_enemyCombat.ID.None)
            {
                id = AnyWaterEnemy(realm);
                source = "fallback: first m_SpawnWater row valid in realm";
            }
            if (id == FTK_enemyCombat.ID.None)
            {
                Plugin.Log.LogWarning("[SkySpike] test sea enemy: no water enemy valid in realm " + realm + " (level " + level + ").");
                return;
            }
            FTK_enemyCombat row = FTK_enemyCombatDB.Get(id);
            bool camp = spot.m_Type == HexLand.Type.Sea && row != null && row.m_Boat != FTKDict_EnemyBoats.EnemyBoats.None;
            if (camp) GameFlowMC.Instance.SpawnEnemyCampHex(spot.m_ParentIndex, spot.m_Index, id.ToString());
            else GameFlowMC.Instance.SpawnEnemyHex(spot.m_ParentIndex, spot.m_Index, id.ToString());
            Plugin.Log.LogInfo("[SkySpike] test sea enemy " + id + " spawned at hex " + HexKey(spot) + " (" +
                (camp ? "enemy boat camp" : "enemy") + ", realm " + realm + ", level " + level + ", " +
                (night ? "night" : "day") + ", " + source + ").");
        }

        static FTK_enemyCombat.ID AnyWaterEnemy(FTK_realm.ID realm)
        {
            FTK_enemyCombatDB db = TableManager.Instance.Get<FTK_enemyCombatDB>();
            if (db == null || db.m_Array == null) return FTK_enemyCombat.ID.None;
            foreach (FTK_enemyCombat row in db.m_Array)
            {
                if (row == null || !row.m_SpawnWater) continue;
                if (!GameLogic.IsValidInRealm(realm, row.m_RealmInclude, row.m_RealmExclude)) continue;
                FTK_enemyCombat.ID id = FTK_enemyCombat.GetEnum(row.m_ID);
                if (id != FTK_enemyCombat.ID.None) return id;
            }
            return FTK_enemyCombat.ID.None;
        }

        // Lists every Port utility and every shore-land town (the two GetBoatItemsForSale callers), with the
        // stage's vanilla boat-tier window, current boat stock and, when the party is placed, hex steps.
        static MiniHexInfo LogBoatSellers(HexLand party, string phase)
        {
            FTKHex map = FTKHex.Instance;
            if (map == null) return null;
            Dictionary<HexLand, int> steps = party != null ? StepDistances(party, 20000) : null;
            List<MiniHexInfo> sellers = new List<MiniHexInfo>();
            int ports = 0, towns = 0, inlandTowns = 0;
            foreach (MiniHexInfo poi in map.GetPOIList(MiniHexInfo.MiniHexType.Utility))
                if (IsPort(poi)) { sellers.Add(poi); ports++; }
            foreach (MiniHexInfo poi in map.GetPOIList(MiniHexInfo.MiniHexType.Town))
            {
                if (poi == null || poi.m_HexLand == null) continue;
                if (poi.m_HexLand.IsShoreLand()) { sellers.Add(poi); towns++; }
                else inlandTowns++;
            }
            Plugin.Log.LogInfo("[SkySpike] boat sellers (" + phase + "): " + ports + " ports, " + towns +
                " shore towns (" + inlandTowns + " inland towns sell no boats).");

            MiniHexInfo nearest = null;
            int best = int.MaxValue;
            foreach (MiniHexInfo poi in sellers)
            {
                int distance = -1;
                if (steps != null) { int d; if (steps.TryGetValue(poi.m_HexLand, out d)) distance = d; }
                Plugin.Log.LogInfo("[SkySpike]   " + (IsPort(poi) ? "port" : "shore town") + " '" + PoiName(poi) +
                    "' at hex " + HexKey(poi.m_HexLand) + ": " + StageBoatWindow(poi.m_HexLand) +
                    "; stocked boats " + StockedBoats(poi) + (distance >= 0 ? "; " + distance + " hex steps from party" : "") + ".");
                if (IsPort(poi) && distance >= 0 && distance < best) { best = distance; nearest = poi; }
            }
            if (nearest != null)
                Plugin.Log.LogInfo("[SkySpike] nearest port: '" + PoiName(nearest) + "' at hex " + HexKey(nearest.m_HexLand) +
                    ", " + best + " hex steps from the party at " + HexKey(party) + ".");
            else if (party != null)
                Plugin.Log.LogInfo("[SkySpike] nearest port: none reachable from the party at " + HexKey(party) + ".");
            return nearest;
        }

        static bool IsPort(MiniHexInfo poi)
        {
            MiniHexUtility utility = poi as MiniHexUtility;
            return utility != null && utility.m_ID == FTK_utility.ID.Port;
        }

        // Same window GetBoatItemsForSale reads. Vanilla loops i <= min(to, m_BoatTiers.Length), so a window
        // that reaches Length indexes past the array; that is flagged rather than reproduced.
        static string StageBoatWindow(HexLand land)
        {
            if (land == null || land.m_HexInfo == null) return "no hex info";
            GameStage stage = GameLogic.Instance.GetGameDef().GetRealmStage(land.m_HexInfo.m_Realm, land.m_HexInfo.m_StageIndex);
            if (stage == null) return "no realm stage for " + land.m_HexInfo.m_Realm + "/" + land.m_HexInfo.m_StageIndex;
            FTK_itembase.ID[] tiers = GameLogic.Instance.m_TownManager != null ? GameLogic.Instance.m_TownManager.m_BoatTiers : null;
            int length = tiers != null ? tiers.Length : 0;
            int from = Mathf.Max(stage.m_PortBoatLvlFrom, 0);
            int to = Mathf.Min(stage.m_PortBoatLvlTo, length);
            string window = "stage " + land.m_HexInfo.m_Realm + "/" + land.m_HexInfo.m_StageIndex + " boat tiers " +
                stage.m_PortBoatLvlFrom + ".." + stage.m_PortBoatLvlTo + " of " + length;
            if (to >= length && from <= to) window += " (vanilla would index past m_BoatTiers)";
            else if (to < from) window += " (no vanilla boats)";
            return window + (RegisteredShipCount() > 0 ? ", " + RegisteredShipCount() + " airships appended" : "");
        }

        static string StockedBoats(MiniHexInfo poi)
        {
            if (poi.m_ShopItemStock == null) return "[not stocked yet]";
            List<string> boats = new List<string>();
            foreach (KeyValuePair<FTK_itembase.ID, int> entry in poi.m_ShopItemStock)
                if (FTK_itembase.IsBoatItem(entry.Key)) boats.Add(ItemLabel(entry.Key) + " x" + entry.Value);
            return "[" + string.Join(", ", boats.ToArray()) + "]";
        }

        static Dictionary<HexLand, int> StepDistances(HexLand start, int limit)
        {
            Dictionary<HexLand, int> steps = new Dictionary<HexLand, int>();
            Queue<HexLand> queue = new Queue<HexLand>();
            steps[start] = 0;
            queue.Enqueue(start);
            while (queue.Count > 0 && steps.Count < limit)
            {
                HexLand h = queue.Dequeue();
                if (h.m_Neighbors == null) continue;
                int next = steps[h] + 1;
                foreach (HexLand n in h.m_Neighbors)
                    if (n != null && !steps.ContainsKey(n)) { steps[n] = next; queue.Enqueue(n); }
            }
            return steps;
        }

        static HexLand VacantWaterNeighbor(HexLand land, HashSet<HexLand> exclude)
        {
            if (land == null || land.m_Neighbors == null) return null;
            foreach (HexLand n in land.m_Neighbors)
                if (n != null && n.IsWater() && !n.HasPOI() && (exclude == null || !exclude.Contains(n))) return n;
            return null;
        }

        // Nearest hex (breadth-first from land) whose realm/stage resolves to a GameStage.
        static HexLand StagedNeighbor(HexLand land)
        {
            if (land == null) return null;
            GameDefinition def = GameLogic.Instance.GetGameDef();
            var seen = new HashSet<HexLand> { land };
            var queue = new Queue<HexLand>();
            queue.Enqueue(land);
            while (queue.Count > 0 && seen.Count < 2000)
            {
                HexLand h = queue.Dequeue();
                if (h.m_HexInfo != null && def.GetRealmStage(h.m_HexInfo.m_Realm, h.m_HexInfo.m_StageIndex) != null) return h;
                if (h.m_Neighbors == null) continue;
                foreach (HexLand n in h.m_Neighbors)
                    if (n != null && seen.Add(n)) queue.Enqueue(n);
            }
            return null;
        }

        static HexLand FirstPartyHex()
        {
            FTKHub hub = FTKHub.Instance;
            if (hub == null) return null;
            foreach (CharacterOverworld cow in hub.m_CharacterOverworlds)
                if (cow != null && cow.m_HexLand != null && cow.m_HexLand.m_HexInfo != null) return cow.m_HexLand;
            return null;
        }

        static string HexKey(HexLand land)
        {
            return land == null ? "?" : land.m_ParentIndex + "/" + land.m_Index;
        }

        static string PoiName(MiniHexInfo poi)
        {
            try { return poi.GetPOIDisplayValue(); }
            catch (Exception) { return poi.GetType().Name; }
        }

        static string ItemLabel(FTK_itembase.ID id)
        {
            ShipType ship = ShipForItem(id);
            return ship != null ? (int)id + " (airship " + ship.Key + ")" : id.ToString();
        }

        static HexLand NearestVacantWater(HexLand start, int limit, HashSet<HexLand> exclude)
        {
            var seen = new HashSet<HexLand> { start };
            var queue = new Queue<HexLand>();
            queue.Enqueue(start);
            while (queue.Count > 0 && seen.Count < limit)
            {
                HexLand h = queue.Dequeue();
                if (h != start && h.IsWater() && !h.HasPOI() && (exclude == null || !exclude.Contains(h))) return h;
                if (h.m_Neighbors == null) continue;
                foreach (HexLand n in h.m_Neighbors)
                    if (n != null && seen.Add(n)) queue.Enqueue(n);
            }
            return null;
        }

        static void EnsureCloudMaterials()
        {
            if (_cloudSurface != null && _cloudSeabed != null) return;
            Shader standard = Shader.Find("Standard");
            if (standard == null) throw new InvalidOperationException("Standard shader unavailable.");
            if (_noise == null) _noise = BuildNoise();

            _cloudSurface = new Material(standard);
            _cloudSurface.name = "SkySpike cloud surface";
            _cloudSurface.color = new Color(0.97f, 0.98f, 1f, 1f);
            _cloudSurface.mainTexture = _noise;
            _cloudSurface.mainTextureScale = new Vector2(2f, 2f);
            _cloudSurface.SetFloat("_Glossiness", 0.05f);
            _cloudSurface.SetFloat("_Metallic", 0f);
            _cloudSurface.EnableKeyword("_EMISSION");
            _cloudSurface.SetColor("_EmissionColor", new Color(0.22f, 0.24f, 0.28f, 1f));

            _cloudSeabed = new Material(standard);
            _cloudSeabed.name = "SkySpike cloud seabed";
            _cloudSeabed.color = new Color(0.70f, 0.76f, 0.86f, 1f);
            _cloudSeabed.mainTexture = _noise;
            _cloudSeabed.mainTextureScale = new Vector2(2f, 2f);
            _cloudSeabed.SetFloat("_Glossiness", 0f);
            _cloudSeabed.SetFloat("_Metallic", 0f);
            Plugin.Log.LogInfo("[SkySpike] cloud materials created.");
        }

        // Tileable fractal Perlin noise: each octave blends four offset samples so the texture repeats seamlessly.
        static Texture2D BuildNoise()
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.name = "SkySpike cloud noise";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            Color shadow = new Color(0.80f, 0.86f, 0.95f, 1f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = 0f, amplitude = 0.5f, total = 0f;
                    for (int octave = 0; octave < 4; octave++)
                    {
                        float cells = 4 << octave;
                        value += amplitude * TileablePerlin(x, y, size, cells, 17.3f + octave * 31.7f);
                        total += amplitude;
                        amplitude *= 0.5f;
                    }
                    float n = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, value / total));
                    pixels[y * size + x] = Color.Lerp(shadow, Color.white, n);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        static float TileablePerlin(float x, float y, float size, float cells, float offset)
        {
            float k = cells / size;
            float a = Mathf.PerlinNoise(offset + x * k, offset + y * k);
            float b = Mathf.PerlinNoise(offset + (x - size) * k, offset + y * k);
            float c = Mathf.PerlinNoise(offset + x * k, offset + (y - size) * k);
            float d = Mathf.PerlinNoise(offset + (x - size) * k, offset + (y - size) * k);
            float u = x / size, v = y / size;
            return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
        }

        // ---- sky background -------------------------------------------------------------------------

        static Camera FindOverworldCamera()
        {
            OverworldCamera overworld = OverworldCamera.Instance;
            if (overworld != null)
            {
                Camera camera = overworld.m_Camera != null ? overworld.m_Camera : overworld.GetComponent<Camera>();
                if (camera != null) return camera;
            }
            RtsCamera rts = RtsCamera.Instance;
            if (rts != null)
            {
                Camera camera = rts.m_ThisCamera != null ? rts.m_ThisCamera : rts.GetComponent<Camera>();
                if (camera != null) return camera;
            }
            return null;
        }

        static void ApplySky()
        {
            if (_skyCamera != null && _skyCamera.backgroundColor == SkyBlue) return;
            Camera camera = FindOverworldCamera();
            if (camera == null) return;
            if (camera.clearFlags != CameraClearFlags.SolidColor)
            {
                if (_skyCamera != camera)
                    Plugin.Log.LogWarning("[SkySpike] overworld camera '" + camera.name + "' clears with " +
                        camera.clearFlags + ", not SolidColor; sky color left unchanged.");
                _skyCamera = camera;
                return;
            }
            if (_skyCamera != camera) _savedBackground = camera.backgroundColor;
            _skyCamera = camera;
            camera.backgroundColor = SkyBlue;
            Plugin.Log.LogInfo("[SkySpike] overworld sky set on camera '" + camera.name + "' (was " + _savedBackground + ").");
        }

        static void RestoreSky()
        {
            if (ReferenceEquals(_skyCamera, null)) return;
            Camera camera = _skyCamera;
            _skyCamera = null;
            if (camera != null && camera.clearFlags == CameraClearFlags.SolidColor && camera.backgroundColor == SkyBlue)
            {
                camera.backgroundColor = _savedBackground;
                Plugin.Log.LogInfo("[SkySpike] overworld sky restored for a non-spike adventure.");
            }
        }

        // ---- airship boat type: shop and sea combat ---------------------------------------------------

        static readonly HashSet<int> _loggedBoatShops = new HashSet<int>();

        internal static void OnBoatItemsForSale(MiniHexInfo poi, List<FTK_itembase.ID> items)
        {
            if (items == null || !SpikeActive()) return;
            foreach (ShipType ship in Ships)
            {
                if (ship.Item < 0) continue;
                FTK_itembase.ID item = (FTK_itembase.ID)ship.Item;
                if (!items.Contains(item)) items.Add(item);
            }
            // Test aid: one line per POI instance showing who asked and what the final list is.
            if (poi == null || !_loggedBoatShops.Add(poi.GetInstanceID())) return;
            string caller = IsPort(poi) ? "Port utility"
                : poi is MiniHexTown ? (poi.m_HexLand != null && poi.m_HexLand.IsShoreLand() ? "shore town" : "inland town")
                : poi.GetType().Name;
            List<string> labels = new List<string>();
            foreach (FTK_itembase.ID id in items) labels.Add(ItemLabel(id));
            Plugin.Log.LogInfo("[SkySpike] boat shop list: " + poi.GetType().Name + " '" + PoiName(poi) + "' at hex " +
                HexKey(poi.m_HexLand) + " (" + caller + "): [" + string.Join(", ", labels.ToArray()) + "].");
        }

        static float EnvFloat(string name, float fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);
            float value;
            if (!string.IsNullOrEmpty(raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value))
                return value;
            return fallback;
        }
    }

    // Every spike patch is Prepare-gated on the environment variable, so without it PatchAll never
    // attaches them. Each body also catches everything so a spike fault cannot reach the game.

    [HarmonyPatch(typeof(HexInfo), "PostAwake")]
    internal static class SkySpikeHexInfoPostAwakePatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(HexInfo __instance)
        {
            try { SkySpike.OnHexInfoPostAwake(__instance); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] cloud sea swap failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(FTKHex), "InitializePOIMapData")]
    internal static class SkySpikeMapReadyPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix()
        {
            try { SkySpike.OnMapDataInitialized(); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] map summary failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(MiniHexBoat), "_instantiateArtAsset")]
    internal static class SkySpikeBoatArtPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(MiniHexBoat __instance)
        {
            try { SkySpike.OnBoatArtInstantiated(__instance); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] airship reskin failed: " + e); }
        }
    }

    // FTK_boat.GetEnum Enum.Parses the row's string id, so the airship row would resolve to None and
    // MiniHexBoat.Awake2 would read m_BoatHealth from a null row. Same shape as the EnumPatches prefixes;
    // kept here so it only installs with the spike.
    [HarmonyPatch(typeof(FTK_boat), "GetEnum")]
    internal static class SkySpikeBoatGetEnumPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static bool Prefix(string _id, ref FTK_boat.ID __result)
        {
            try
            {
                FTK_boat.ID resolved;
                if (!SkySpike.TryResolveBoatRow(_id, out resolved)) return true;
                __result = resolved;
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[SkySpike] boat GetEnum prefix failed: " + e);
                return true;
            }
        }
    }

    // IsBoatItem is a hard-coded list of the five vanilla boat items; purchase, launch and inventory checks use it.
    [HarmonyPatch(typeof(FTK_itembase), "IsBoatItem")]
    internal static class SkySpikeIsBoatItemPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(FTK_itembase.ID _item, ref bool __result)
        {
            if (!__result && SkySpike.IsAirshipItem(_item)) __result = true;
        }
    }

    // Ports and shore towns build their boat stock from this list; append the airship instead of growing m_BoatTiers.
    [HarmonyPatch(typeof(MiniHexInfo), "GetBoatItemsForSale")]
    internal static class SkySpikeBoatItemsForSalePatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(MiniHexInfo __instance, List<FTK_itembase.ID> __result)
        {
            try { SkySpike.OnBoatItemsForSale(__instance, __result); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] boat shop append failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(DioramaBoat), "_initForEncounter")]
    internal static class SkySpikeBoatDioramaPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(DioramaBoat __instance)
        {
            try { SkySpike.OnBoatDioramaInit(__instance); }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] airship combat hull failed: " + e); }
        }
    }

    // Isolated live tests block Steam initialization (tools/reporting-proof SteamGuard) so a disposable
    // profile never touches the player's real statistics. The installed stats adapter's two reads bypass
    // its Initialized guard, so report them unavailable, as the reporting proof does. Armed only inside
    // that guarded disposable process: spike flag, proof flag, and a root directly beneath scratch/.
    static class SkySpikeOfflineProfile
    {
        internal static readonly bool Armed = SkySpike.Enabled && IsDisposableRoot();

        static bool IsDisposableRoot()
        {
            if (Environment.GetEnvironmentVariable("FTK_REPORTING_PROOF") != "1") return false;
            string root = Environment.GetEnvironmentVariable("FTK_REPORTING_ROOT");
            if (string.IsNullOrEmpty(root)) return false;
            DirectoryInfo dir = new DirectoryInfo(root);
            return dir.Exists && dir.Parent != null && dir.Parent.Name == "scratch";
        }
    }

    [HarmonyPatch(typeof(StatsAchievements.SteamStatsAndAchievements), "GetStatValue",
        new[] { typeof(string), typeof(int) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    internal static class SkySpikeOfflineStatPatch
    {
        static bool Prepare() { return SkySpikeOfflineProfile.Armed; }
        static bool Prefix(ref int pValue, ref bool __result) { pValue = 0; __result = false; return false; }
    }

    [HarmonyPatch(typeof(StatsAchievements.SteamStatsAndAchievements), "GetAchievementValue",
        new[] { typeof(string), typeof(bool) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    internal static class SkySpikeOfflineAchievementPatch
    {
        static bool Prepare() { return SkySpikeOfflineProfile.Armed; }
        static bool Prefix(ref bool pIsAchieved, ref bool __result) { pIsAchieved = false; __result = false; return false; }
    }

    // Vanilla AkInitializer.OnApplicationFocus dereferences uiOptionsMenu.Instance, which is null until the
    // options menu exists. Each window focus change in the disposable test process then raises the
    // framework's error-report prompt and stalls automated input. Swallow only that NRE, only there.
    [HarmonyPatch(typeof(AkInitializer), "OnApplicationFocus")]
    internal static class SkySpikeOfflineAudioFocusPatch
    {
        static bool Prepare() { return SkySpikeOfflineProfile.Armed; }
        static Exception Finalizer(Exception __exception)
        {
            return __exception is NullReferenceException && uiOptionsMenu.Instance == null ? null : __exception;
        }
    }
}
