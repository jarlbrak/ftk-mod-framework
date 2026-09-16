using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static int ExactCount(ItemContainer container,FTK_itembase.ID item)
    {
        int count;
        return container.m_ItemCounts!=null && container.m_ItemCounts.TryGetValue(item,out count)?count:0;
    }
    static JObject EquipmentView(CharacterOverworld cow)
    {
        JArray slots=new JArray();
        if(cow.m_PlayerInventory!=null)
            foreach(PlayerInventory.ContainerID slot in Enum.GetValues(typeof(PlayerInventory.ContainerID)))
            {
                ItemContainer container=cow.m_PlayerInventory.Get(slot);
                if(container==null)continue;
                JArray items=new JArray();
                if(container.m_ItemCounts!=null)foreach(KeyValuePair<FTK_itembase.ID,int> entry in container.m_ItemCounts)
                    items.Add(new JObject{{"item",(int)entry.Key},{"name",entry.Key.ToString()},{"count",entry.Value}});
                slots.Add(new JObject{{"slot",slot.ToString()},{"slotId",(int)slot},{"items",items}});
            }
        CharacterEventListener avatar=cow.m_Avatar;
        CharacterEventListener dummy=cow.m_CurrentDummy==null?null:cow.m_CurrentDummy.m_EventListener;
        return new JObject{{"heroInstanceId",cow.GetInstanceID()},{"alive",cow.m_CharacterStats!=null && cow.m_CharacterStats.m_HealthCurrent>0},
            {"dummyCelInstanceId",dummy==null?0:dummy.GetInstanceID()},{"dummyLease",dummy==null?null:ReadLease(dummy)},
            {"celInstanceId",avatar==null?0:avatar.GetInstanceID()},{"lease",avatar==null?null:ReadLease(avatar)},{"slots",slots}};
    }
    JObject EquipmentInventory()
    {
        JArray heroes=new JArray();
        IEnumerable party=Field(Instance(typeof(FTKHub)),"m_CharacterOverworlds")as IEnumerable;
        if(party!=null)foreach(object value in party){CharacterOverworld cow=value as CharacterOverworld;if(cow!=null)heroes.Add(EquipmentView(cow));}
        return new JObject{{"ok",true},{"provenance","read-only native owned equipment; no item grants"},{"heroes",heroes}};
    }
    JObject ChangeBodyEquipment(JObject command,bool equip)
    {
        RequireReadyPreparation();
        int hero=Int(command,"heroInstanceId",0),itemValue=Int(command,"item",-1);
        int expectedBody=Int(command,"expectedBodyCount",-1),expectedBackpack=Int(command,"expectedBackpackCount",-1);
        if(hero==0 || itemValue<0 || expectedBody<0 || expectedBackpack<0)
            throw new ArgumentException("Explicit heroInstanceId, item enum integer, expectedBodyCount and expectedBackpackCount required.");
        CharacterOverworld cow=null;
        foreach(CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if(candidate!=null && candidate.GetInstanceID()==hero){if(cow!=null)throw new InvalidOperationException("Ambiguous hero.");cow=candidate;}
        if(cow==null || cow.m_PlayerInventory==null)throw new InvalidOperationException("Hero must belong to current living party with native inventory.");
        FTK_itembase.ID item=(FTK_itembase.ID)itemValue;
        FTK_itembase row=FTK_itembase.GetItemBase(item);
        if(row==null || !row.m_Equippable || row.m_ObjectType!=FTK_itembase.ObjectType.armor)
            throw new InvalidOperationException("Only actual equippable Body armor is supported.");
        ItemContainer body=cow.m_PlayerInventory.Get(PlayerInventory.ContainerID.Body),backpack=cow.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack);
        if(body==null || backpack==null)throw new InvalidOperationException("Native Body and Backpack required.");
        if(ExactCount(body,item)!=expectedBody || ExactCount(backpack,item)!=expectedBackpack)
            throw new InvalidOperationException("Owned item counts changed; reobserve equipment-inventory.");
        int bodyTotal=0;
        if(body.m_ItemCounts!=null)foreach(int count in body.m_ItemCounts.Values){if(count<0)throw new InvalidOperationException("Invalid Body counts.");bodyTotal+=count;}
        if(equip)
        {
            if(expectedBody!=0 || bodyTotal!=0 || expectedBackpack<1 || !body.CanAdd(item))
                throw new InvalidOperationException("Equip requires empty Body, owned backpack armor and native Body.CanAdd.");
        }
        else if(expectedBody!=1 || bodyTotal!=1 || !backpack.CanAdd(item))
            throw new InvalidOperationException("Unequip requires exactly one owned Body armor and native Backpack.CanAdd.");
        JObject before=EquipmentView(cow);
        if(equip)cow.EquipItem(item,false);else cow.UnequipItem(item,true);
        return new JObject{{"ok",true},{"status","native-method-submitted"},{"method",equip?"CharacterOverworld.EquipItem(item,false)":"CharacterOverworld.UnequipItem(item,true)"},
            {"item",itemValue},{"before",before},{"after",EquipmentView(cow)},
            {"note","One native owned-item transfer requested. Reobserve counts and avatar identity before any inverse command; no automatic retry or restoration."}};
    }
}
