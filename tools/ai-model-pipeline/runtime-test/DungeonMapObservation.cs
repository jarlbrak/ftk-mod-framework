using System;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    JObject DungeonMapObservation(JObject command)
    {
        CatalogKeys(command,"id","session","op");
        RequireSinglePlayer();RequireOutsideCombat();
        if(FTKHex.Instance==null)throw new InvalidOperationException("Existing world required.");
        JArray dungeons=new JArray();
        foreach(MiniHexInfo poi in FTKHex.Instance.GetPOIList(MiniHexInfo.MiniHexType.Dungeon))
        {
            MiniHexDungeon dungeon=poi as MiniHexDungeon;
            if(dungeon==null)continue;
            HexLand origin=dungeon.m_HexLand;
            JArray neighbors=new JArray();
            if(origin!=null && origin.m_Neighbors!=null)
            {
                if(origin.m_Neighbors.Length>8)throw new InvalidOperationException("Native neighbor array exceeds observation bound.");
                foreach(HexLand hex in origin.m_Neighbors)
                {
                    if(hex==null)continue;
                    MiniHexInfo neighborPoi=hex.GetPOI();
                    bool sameRealm=hex.m_HexInfo!=null && origin.m_HexInfo!=null && hex.m_HexInfo.m_Realm==origin.m_HexInfo.m_Realm;
                    bool emptyLand=hex.m_Type==HexLand.Type.Land && hex.m_POI==null && hex.m_AirPOI==null && hex.CanTravelLandOnly();
                    neighbors.Add(new JObject{{"parentIndex",hex.m_ParentIndex},{"index",hex.m_Index},
                        {"type",hex.m_Type.ToString()},{"realm",hex.m_HexInfo==null?null:hex.m_HexInfo.m_Realm.ToString()},
                        {"sameRealm",sameRealm},{"canTravelLandOnly",hex.CanTravelLandOnly()},
                        {"poiType",neighborPoi==null?null:neighborPoi.m_MiniHexType.ToString()},
                        {"poiInstanceId",neighborPoi==null?0:neighborPoi.GetInstanceID()},
                        {"emptyLandStagingCandidate",sameRealm && emptyLand}});
                }
            }
            dungeons.Add(new JObject{{"id",dungeon.m_ID.ToString()},
                {"instanceId",dungeon.GetInstanceID()},
                {"realm",origin==null || origin.m_HexInfo==null?null:origin.m_HexInfo.m_Realm.ToString()},
                {"neighbors",neighbors},
                {"locked",Convert.ToBoolean(Field(dungeon,"m_Locked"))},
                {"deactivated",Convert.ToBoolean(Field(dungeon,"m_Deactivated"))},
                {"parentIndex",dungeon.m_HexLand==null?-1:dungeon.m_HexLand.m_ParentIndex},
                {"index",dungeon.m_HexLand==null?-1:dungeon.m_HexLand.m_Index}});
        }
        return new JObject{{"ok",true},{"dungeons",dungeons},
            {"scope","Read-only existing dungeon POIs. No generation, movement, unlock, entry or room mutation."}};
    }
}
