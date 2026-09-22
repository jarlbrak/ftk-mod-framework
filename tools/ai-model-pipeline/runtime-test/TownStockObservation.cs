using System;
using System.Collections.Generic;
using GridEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static JArray PaladinTownStock(IDictionary<FTK_itembase.ID,int> stock)
    {
        JArray result=new JArray();
        if(stock==null)return result;
        List<FTK_itembase.ID> ids=new List<FTK_itembase.ID>(stock.Keys);
        ids.Sort(delegate(FTK_itembase.ID a,FTK_itembase.ID b){return ((int)a).CompareTo((int)b);});
        foreach(FTK_itembase.ID id in ids)
        {
            FTK_itembase row=FTK_itembase.GetItemBase(id);
            if(row!=null && row.m_ID!=null && row.m_ID.StartsWith("paladin_",StringComparison.Ordinal))
                result.Add(new JObject{{"itemId",(int)id},{"key",row.m_ID},{"count",stock[id]}});
        }
        return result;
    }

    JObject TownStockObservation(JObject command)
    {
        CatalogKeys(command,"id","session","op");
        if(FTKHex.Instance==null || FTKHub.Instance==null)
            throw new InvalidOperationException("An existing world and party are required.");
        JArray towns=new JArray(),heroes=new JArray();
        foreach(MiniHexInfo poi in FTKHex.Instance.GetPOIList(MiniHexInfo.MiniHexType.Town))
        {
            MiniHexTown town=poi as MiniHexTown;
            if(town==null)continue;
            towns.Add(new JObject{{"name",town.GetPOIDisplayValue()},
                {"parentIndex",town.m_HexLand==null?-1:town.m_HexLand.m_ParentIndex},
                {"index",town.m_HexLand==null?-1:town.m_HexLand.m_Index},
                {"generatedStockAvailable",town.m_ShopItemStock!=null},
                {"currentStockAvailable",town.m_ShopItemStockCurrent!=null},
                {"generatedPaladinItems",PaladinTownStock(town.m_ShopItemStock)},
                {"currentPaladinItems",PaladinTownStock(town.m_ShopItemStockCurrent==null?null:town.m_ShopItemStockCurrent.m_CountDictionary)}});
        }
        foreach(CharacterOverworld hero in FTKHub.Instance.m_CharacterOverworlds)
            if(hero!=null && hero.m_CharacterStats!=null)
                heroes.Add(new JObject{{"heroInstanceId",hero.GetInstanceID()},
                    {"turnIndex",hero.m_FTKPlayerID.m_TurnIndex},{"photonId",hero.m_FTKPlayerID.m_PhotonID},
                    {"gold",hero.m_CharacterStats.m_Gold}});
        return new JObject{{"ok",true},{"frame",Time.frameCount},{"towns",towns},{"heroes",heroes},
            {"scope","Existing native town stock only. No generation, refresh, movement, purchase or database mutation."}};
    }
}
