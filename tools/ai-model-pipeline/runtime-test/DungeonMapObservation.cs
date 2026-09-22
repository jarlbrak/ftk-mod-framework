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
            dungeons.Add(new JObject{{"id",dungeon.m_ID.ToString()},
                {"instanceId",dungeon.GetInstanceID()},
                {"locked",Convert.ToBoolean(Field(dungeon,"m_Locked"))},
                {"deactivated",Convert.ToBoolean(Field(dungeon,"m_Deactivated"))},
                {"parentIndex",dungeon.m_HexLand==null?-1:dungeon.m_HexLand.m_ParentIndex},
                {"index",dungeon.m_HexLand==null?-1:dungeon.m_HexLand.m_Index}});
        }
        return new JObject{{"ok",true},{"dungeons",dungeons},
            {"scope","Read-only existing dungeon POIs. No generation, movement, unlock, entry or room mutation."}};
    }
}
