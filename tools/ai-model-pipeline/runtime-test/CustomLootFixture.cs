using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static RuntimeModelTest customLootObserver;
    Harmony customLootHarmony;
    MethodInfo customLootMethod;
    EncounterSessionMC customLootMaster;
    EncounterSession customLootClient;
    UnityEngine.Object customLootDiorama;
    ArrayList customLootList;
    FTK_items customLootItem;
    int customLootItemId;
    float customLootDeadline;
    bool customLootClaimed;
    JObject customLootReceipt;
    static MethodInfo CustomLootPostfixMethod(){return typeof(RuntimeModelTest).GetMethod("CustomLootPostfix",Statics);}
    void CustomLootRemoveHook()
    {
        if(customLootHarmony!=null && customLootMethod!=null)customLootHarmony.Unpatch(customLootMethod,CustomLootPostfixMethod());
        customLootHarmony=null;if(customLootObserver==this)customLootObserver=null;
    }
    void CustomLootAbort(string reason)
    {
        if(customLootReceipt!=null){customLootReceipt["status"]="aborted";customLootReceipt["reason"]=reason;}
        CustomLootRemoveHook();
    }
    void CustomLootTick()
    {
        if(customLootHarmony==null)return;
        if(Time.realtimeSinceStartup>=customLootDeadline){CustomLootAbort("expired");return;}
        if(customLootMaster==null || customLootClient==null || EncounterSessionMC.Instance!=customLootMaster || EncounterSession.Instance!=customLootClient)
        {CustomLootAbort("encounter owner changed");return;}
        if(customLootDiorama==null || customLootClient.m_ActiveDiorama!=customLootDiorama)
            CustomLootAbort("pinned diorama changed");
    }
    static void CustomLootPostfix(ArrayList _arrayList,int _perPlayerGold,int _perPlayerXP)
    {
        RuntimeModelTest observer=customLootObserver;if(observer==null || observer.customLootHarmony==null)return;
        if(!object.ReferenceEquals(_arrayList,observer.customLootList))return;
        // Consume the fixture before any append. A failed/uncertain attempt cannot be rearmed.
        observer.CustomLootRemoveHook();
        try
        {
            RequireSinglePlayer();
            EncounterSessionMC master=observer.customLootMaster;EncounterSession client=observer.customLootClient;
            var currentList=master==null?null:FTKUtil.GetPlaymakerListByName(master.gameObject,"lootDropItems");
            if(Time.realtimeSinceStartup>=observer.customLootDeadline || master==null || client==null ||
                EncounterSessionMC.Instance!=master || EncounterSession.Instance!=client || !master.m_EncounterStarted || !master.m_IsInCombat ||
                (MiniHexDungeon.EncounterType)typeof(EncounterSessionMC).GetField("m_EncounterType",Members).GetValue(master)!=MiniHexDungeon.EncounterType.Enemy ||
                observer.customLootDiorama==null || client.m_ActiveDiorama!=observer.customLootDiorama ||
                currentList==null || !object.ReferenceEquals(currentList.arrayList,_arrayList))
                throw new InvalidOperationException("Pinned enemy victory context changed");
            FTK_items row=FTK_itemsDB.GetDB().GetEntry((FTK_itembase.ID)observer.customLootItemId);
            if(!object.ReferenceEquals(row,observer.customLootItem) || row.m_ID!="paladin_helmet_novice" || !string.IsNullOrEmpty(row.m_CollectLoreItemUnlock))
                throw new InvalidOperationException("Exact corrected custom item unavailable");
            if(_arrayList.Count>256)throw new InvalidOperationException("Native loot list exceeds fixture limit");
            JArray before=new JArray();foreach(object value in _arrayList)
            {if(!(value is string))throw new InvalidOperationException("Unexpected native loot token type");before.Add((string)value);}
            observer.customLootReceipt["before"]=before;observer.customLootReceipt["beforeCount"]=_arrayList.Count;
            observer.customLootReceipt["gold"]=_perPlayerGold;observer.customLootReceipt["xp"]=_perPlayerXP;
            observer.customLootReceipt["status"]="consumed-before-append";
            _arrayList.Add("paladin_helmet_novice");
            observer.customLootReceipt["afterCount"]=_arrayList.Count;
            observer.customLootReceipt["appendedToken"]="paladin_helmet_novice";
            observer.customLootReceipt["status"]="appended";
        }
        catch(Exception e)
        {
            observer.customLootReceipt["status"]="aborted-or-unknown";
            observer.customLootReceipt["error"]=e.GetType().Name+": "+e.Message;
        }
    }
    JObject CustomLootFixture(JObject command)
    {
        CatalogKeys(command,"id","session","op","action");RequireSinglePlayer();CatalogNoLinks(root);
        string action=Str(command,"action");CustomLootTick();
        if(action=="arm")
        {
            if(customLootClaimed)throw new InvalidOperationException("This helper process already claimed its one-shot custom loot fixture.");
            customLootMaster=EncounterSessionMC.Instance;customLootClient=EncounterSession.Instance;
            if(customLootMaster==null || customLootClient==null || !customLootMaster.m_EncounterStarted || !customLootMaster.m_IsInCombat ||
                !customLootClient.m_IsInCombat || customLootClient.m_EncounterType!=MiniHexDungeon.EncounterType.Enemy || customLootClient.m_ActiveDiorama==null ||
                (MiniHexDungeon.EncounterType)typeof(EncounterSessionMC).GetField("m_EncounterType",Members).GetValue(customLootMaster)!=MiniHexDungeon.EncounterType.Enemy)
                throw new InvalidOperationException("Active native enemy combat required before arming");
            customLootDiorama=customLootClient.m_ActiveDiorama;
            var list=FTKUtil.GetPlaymakerListByName(customLootMaster.gameObject,"lootDropItems");
            if(list==null || list.arrayList==null)throw new InvalidOperationException("Native lootDropItems list unavailable");
            customLootList=list.arrayList;customLootItem=FTK_itemsDB.GetDB().GetEntryByStringID("paladin_helmet_novice");
            object[] lookup={"paladin_helmet_novice",-1,new Type[]{typeof(FTK_itemsDB)}};
            bool registered=(bool)CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.ContentRegistry",true).GetMethod("TryGetSyntheticId",Statics).Invoke(null,lookup);
            if(!registered || customLootItem==null || customLootItem.m_ID!="paladin_helmet_novice" || !string.IsNullOrEmpty(customLootItem.m_CollectLoreItemUnlock))
                throw new InvalidOperationException("Registered corrected Novice Helm required");
            customLootItemId=(int)lookup[1];
            customLootMethod=typeof(GameLogic).GetMethod("FillLootDropList",Statics,null,new[]{typeof(ArrayList),typeof(int),typeof(FTK_enemyCombat.ItemDrops[]),typeof(int[]),typeof(RewardData),typeof(int).MakeByRefType(),typeof(int).MakeByRefType(),typeof(bool)},null);
            if(customLootMethod==null || customLootMethod.ReturnType!=typeof(void))throw new InvalidOperationException("Exact native loot method unavailable");
            customLootClaimed=true;customLootDeadline=Time.realtimeSinceStartup+600f;
            customLootReceipt=new JObject{{"token",Guid.NewGuid().ToString("N")},{"status","armed"},{"itemId",customLootItemId},
                {"dioramaInstanceId",customLootDiorama.GetInstanceID()},{"coreIdentity",PreviewCoreIdentity()},{"helperIdentity",ScaleIdentity(typeof(RuntimeModelTest).Assembly)},
                {"masterInstanceId",customLootMaster.GetInstanceID()},{"encounterInstanceId",customLootClient.GetInstanceID()},
                {"scope","One synthetic custom item appended to native enemy-victory loot. Native display/vote/Collect unchanged; collection success requires separate inventory and log evidence."}};
            customLootHarmony=new Harmony("com.ftkmf.runtime-model-test.custom-loot-fixture");customLootObserver=this;
            try{customLootHarmony.Patch(customLootMethod,null,new HarmonyMethod(CustomLootPostfixMethod()),null,null,null);CustomLootTick();}
            catch{CustomLootRemoveHook();throw;}
        }
        else if(action=="disarm")CustomLootAbort("disarmed");
        else if(action!="inspect")throw new ArgumentException("action must be arm, inspect or disarm");
        return new JObject{{"ok",true},{"armed",customLootHarmony!=null},{"receipt",customLootReceipt==null?null:customLootReceipt.DeepClone()}};
    }
}
