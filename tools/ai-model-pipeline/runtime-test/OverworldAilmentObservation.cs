using System;
using GridEditor;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    bool stagedPoisonHex;
    bool stagedCurseHex;

    JObject StageNativeAilmentHex(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "type", "heroInstanceId", "big", "small");
        CatalogNoLinks(root);
        RequireSinglePlayer();
        if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST_PACKAGE_ONLY") != "1")
            throw new InvalidOperationException("Package-only native hazard staging is required.");
        string type = Str(command, "type");
        if (type != "Poison" && type != "Curse") throw new ArgumentException("Use Poison or Curse.");
        if (type == "Poison" ? stagedPoisonHex : stagedCurseHex)
            throw new InvalidOperationException("This hazard type was already staged in this process.");
        int heroId = LeaseObservationPin.ExactId(command, "heroInstanceId", true);
        if (command["big"] == null || command["big"].Type != JTokenType.Integer ||
            command["small"] == null || command["small"].Type != JTokenType.Integer)
            throw new ArgumentException("Exact integer hex indices required.");
        int big = (int)command["big"];
        int small = (int)command["small"];
        if (big < 0 || small < 0) throw new ArgumentException("Negative hex index.");
        GameLogic logic = GameLogic.Instance;
        if (logic == null || !logic.IsMasterClient || uiStartGame.Instance == null ||
            !uiStartGame.Instance.m_GameStarted || EncounterSession.Instance == null ||
            EncounterSession.Instance.m_IsInCombat)
            throw new InvalidOperationException("An active native master-client overworld is required.");
        CharacterOverworld hero = null;
        foreach (CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if (candidate != null && candidate.GetInstanceID() == heroId) hero = candidate;
        if (hero == null || !hero.IsOwner || hero.m_CharacterStats == null ||
            hero.GetDBEntry() == null ||
            (hero.GetDBEntry().m_ID != "paladin" && hero.GetDBEntry().m_ID != "hunter") ||
            hero.m_HexLand == null)
            throw new InvalidOperationException("Exact owned Paladin or Hunter and current hex required.");
        HexLand target = FTKHex.Instance.GetHexLand(big, small);
        bool neighbor = false;
        for (int i = 0; i < hero.m_HexLand.m_NeighborCount && i < hero.m_HexLand.m_Neighbors.Length; i++)
            neighbor |= hero.m_HexLand.m_Neighbors[i] == target;
        if (target == null || !neighbor || !target.CanTravel() || target.m_POI != null ||
            target.m_PlayersInHex.Count != 0)
            throw new InvalidOperationException("Exact vacant travelable neighboring hex required.");
        if (type == "Poison") stagedPoisonHex = true;
        else stagedCurseHex = true;
        string prefab = type == "Poison" ? "MMpoison" : "MMcurse";
        GameObject spawned = PhotonNetwork.InstantiateSceneObject(prefab, target.transform.position,
            Quaternion.identity, 0, new object[] { false, big, small, 3 });
        MiniHexInfo poi = target.m_POI;
        bool attached = spawned != null && poi != null && poi.gameObject == spawned &&
            poi.m_MiniHexType.ToString() == type;
        return new JObject {
            {"ok", attached}, {"type", type}, {"big", big}, {"small", small},
            {"heroInstanceId", heroId}, {"poiInstanceId", poi == null ? 0 : poi.GetInstanceID()},
            {"status", attached ? "native_hazard_attached" : "native_hazard_attachment_unconfirmed"},
            {"scope", "One native PhotonNetwork.InstantiateSceneObject hazard prefab on an exact vacant neighboring hex in a disposable single-player package trial. Entry and ailment outcome require separate movement and observation."}
        };
    }

    JObject EnterNativeAilmentHex(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "type", "heroInstanceId", "big", "small");
        CatalogNoLinks(root);
        RequireSinglePlayer();
        if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST_PACKAGE_ONLY") != "1")
            throw new InvalidOperationException("Package-only native hazard entry is required.");
        string type = Str(command, "type");
        if (type != "Poison" && type != "Curse") throw new ArgumentException("Use Poison or Curse.");
        int heroId = LeaseObservationPin.ExactId(command, "heroInstanceId", true);
        if (command["big"] == null || command["big"].Type != JTokenType.Integer ||
            command["small"] == null || command["small"].Type != JTokenType.Integer)
            throw new ArgumentException("Exact integer hex indices required.");
        int big = (int)command["big"], small = (int)command["small"];
        if (big < 0 || small < 0 || FTKHub.Instance == null || FTKHex.Instance == null)
            throw new InvalidOperationException("Native world unavailable.");
        CharacterOverworld hero = null;
        foreach (CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if (candidate != null && candidate.GetInstanceID() == heroId) hero = candidate;
        HexLand target = FTKHex.Instance.GetHexLand(big, small);
        if (hero == null || !hero.IsOwner || hero.m_HexLand == null || target == null ||
            target == hero.m_HexLand || target.m_POI == null ||
            target.m_POI.m_MiniHexType.ToString() != type)
            throw new InvalidOperationException("Exact owned hero and live native ailment POI required.");
        bool neighbor = false;
        for (int i = 0; i < hero.m_HexLand.m_NeighborCount && i < hero.m_HexLand.m_Neighbors.Length; i++)
            neighbor |= hero.m_HexLand.m_Neighbors[i] == target;
        if (!neighbor) throw new InvalidOperationException("A neighboring hazard is required.");
        JObject before = OverworldAilmentObservation(command, false);
        hero.SnapTo(target, false, true);
        JObject after = OverworldAilmentObservation(command, false);
        return new JObject {
            {"ok", hero.m_HexLand == target}, {"heroInstanceId", heroId},
            {"type", type}, {"big", big}, {"small", small},
            {"before", before["heroes"]}, {"after", after["heroes"]},
            {"scope", "Exact owned hero entered an adjacent existing native hazard through CharacterOverworld.SnapTo(target, false, true). This invokes HexLand.OnPlayerEnter; it is a test placement, not ordinary walking."}
        };
    }

    JObject OverworldAilmentObservation(JObject command, bool validate = true)
    {
        if (validate) CatalogKeys(command, "id", "session", "op");
        CatalogNoLinks(root);
        RequireSinglePlayer();
        FTKHub hub = FTKHub.Instance;
        FTKHex world = FTKHex.Instance;
        if (hub == null || world == null || hub.m_CharacterOverworlds == null)
            throw new InvalidOperationException("An active native overworld is required.");
        JArray heroes = new JArray();
        foreach (CharacterOverworld hero in hub.m_CharacterOverworlds)
        {
            if (hero == null || hero.m_CharacterStats == null) continue;
            CharacterStats stats = hero.m_CharacterStats;
            HexLand hex = hero.m_HexLand;
            JArray activeCurses = new JArray();
            foreach (CharacterStats.CurseType curse in stats.m_ActiveCurses)
                activeCurses.Add(curse.ToString());
            heroes.Add(new JObject {
                {"instanceId", hero.GetInstanceID()},
                {"fid", hero.m_FTKPlayerID.m_TurnIndex + ":" + hero.m_FTKPlayerID.m_PhotonID},
                {"classId", (int)stats.m_CharacterClass},
                {"classKey", hero.GetDBEntry() == null ? null : hero.GetDBEntry().m_ID},
                {"inCombat", stats.m_IsInCombat},
                {"poisonImmunity", stats.HasImmunity(ProficiencyBase.Category.Poison)},
                {"curseImmunity", stats.HasImmunity(ProficiencyBase.Category.Curse)},
                {"poisonLevels", stats.m_PoisonLvl},
                {"activeCurses", activeCurses},
                {"permanentCurseCount", stats.m_PermaCurses.Count},
                {"health", stats.m_HealthCurrent},
                {"gold", stats.m_Gold},
                {"hex", hex == null ? null : new JObject {
                    {"big", hex.m_ParentIndex}, {"small", hex.m_Index},
                    {"poi", hex.m_POI == null ? null : hex.m_POI.m_MiniHexType.ToString()}
                }}
            });
        }
        JArray hazards = new JArray();
        if (world.m_MiniHexList != null)
            foreach (MiniHexInfo.MiniHexType type in new[] {
                MiniHexInfo.MiniHexType.Poison, MiniHexInfo.MiniHexType.Curse,
                MiniHexInfo.MiniHexType.Chaos, MiniHexInfo.MiniHexType.Fire })
            {
                if (!world.m_MiniHexList.ContainsKey(type)) continue;
                foreach (MiniHexInfo poi in world.m_MiniHexList[type])
                {
                    if (poi == null || poi.m_HexLand == null || hazards.Count >= 128) continue;
                    HexLand hex = poi.m_HexLand;
                    if (hex.m_POI != poi) continue;
                    JArray neighbors = new JArray();
                    for (int i = 0; i < hex.m_NeighborCount && i < hex.m_Neighbors.Length; i++)
                    {
                        HexLand neighbor = hex.m_Neighbors[i];
                        if (neighbor == null || !neighbor.CanTravel()) continue;
                        neighbors.Add(new JObject {
                            {"big", neighbor.m_ParentIndex}, {"small", neighbor.m_Index},
                            {"poi", neighbor.m_POI == null ? null : neighbor.m_POI.m_MiniHexType.ToString()}
                        });
                    }
                    hazards.Add(new JObject {
                        {"type", type.ToString()}, {"instanceId", poi.GetInstanceID()},
                        {"big", hex.m_ParentIndex}, {"small", hex.m_Index},
                        {"active", poi.gameObject.activeInHierarchy},
                        {"revealed", hex.m_IsRevealed}, {"discovered", hex.m_IsDiscovered},
                        {"canTravel", hex.CanTravel()}, {"neighbors", neighbors}
                    });
                }
            }
        return new JObject {
            {"ok", true}, {"heroes", heroes}, {"hazards", hazards},
            {"scope", "Read-only native character counters, immunity queries and existing hazard POIs. No movement or status application."}
        };
    }
}
