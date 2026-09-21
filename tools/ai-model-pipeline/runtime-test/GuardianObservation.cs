using System;
using System.Reflection;
using System.Collections.Generic;
using GridEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    // Only these existing query methods may be invoked. No state initialization or resolution.
    static object GuardianQuery(Type type, object target, string name, object argument)
    {
        MethodInfo method=type.GetMethod(name,target==null?Statics:Members,null,
            new[]{argument is CharacterDummy?typeof(CharacterDummy):typeof(string)},null);
        if(method==null)throw new InvalidOperationException("Guardian query unavailable: "+name);
        return method.Invoke(target,new[]{argument});
    }

    static JObject GuardianFocusBinding()
    {
        FTKInput input=FTKInput.Instance;
        if(input==null)return new JObject{{"available",false}};
        FieldInfo field=typeof(FTKInput).GetField("m_ActionKeys",Members);
        if(field==null)throw new InvalidOperationException("Native action bindings unavailable.");
        Dictionary<string,FTKInput.RemapKeyInfo> bindings=field.GetValue(input) as Dictionary<string,FTKInput.RemapKeyInfo>;
        FTKInput.RemapKeyInfo focus;
        if(bindings==null || !bindings.TryGetValue("Focus",out focus) || focus==null)
            return new JObject{{"available",false}};
        JArray keys=new JArray();
        for(int index=0;index<focus.m_PosKeys.Length;index++)
            keys.Add(new JObject{{"index",index},{"key",focus.m_PosKeys[index].ToString()},
                {"keyCode",(int)focus.m_PosKeys[index]},
                {"modifiers",index<focus.m_PosMods.Length?focus.m_PosMods[index].ToString():null},
                {"modifierFlags",index<focus.m_PosMods.Length?new JValue((int)focus.m_PosMods[index]):new JValue((object)null)}});
        return new JObject{{"available",true},{"action","Focus"},{"positiveBindings",keys}};
    }

    static string GuardianObservedFid(FTKPlayerID fid)
    {return fid.m_TurnIndex+":"+fid.m_PhotonID;}

    static JObject GuardianStoredDamage(DummyDamageInfo damage)
    {
        return damage==null?null:new JObject{
            {"attacker",GuardianObservedFid(damage.m_AttackerID)},
            {"victim",GuardianObservedFid(damage.m_VictimID)},
            {"damage",damage.m_Damage},{"newHealth",damage.m_NewHealth},
            {"attackerHealthMod",damage.m_AttackerHealthMod},{"aoe",damage.m_IsAOE},
            {"response",damage.m_AttackResponse.ToString()},{"proficiencyId",(int)damage.m_Prof},
            {"profSuccess",damage.m_ProfSuccess},{"profAffect",damage.m_ProfAffect},
            {"profImmune",damage.m_ProfImmune}};
    }

    static JObject GuardianArmorSnapshot(CharacterDummy dummy, string side)
    {
        if(dummy.m_SufferingProficiencies==null)
            throw new InvalidOperationException("Native suffering proficiency map is unavailable.");
        JArray categories=new JArray();
        foreach(KeyValuePair<ProficiencyBase.Category,CharacterDummy.ProficiencyRecord> entry in dummy.m_SufferingProficiencies)
        {
            if(entry.Value==null || entry.Value.m_Proficiency==null)
                throw new InvalidOperationException("Native suffering proficiency record is incomplete.");
            categories.Add(new JObject{{"category",entry.Key.ToString()},
                {"proficiencyId",(int)entry.Value.m_Proficiency.m_ProficiencyID},
                {"count",entry.Value.m_Count}});
        }
        EnemyDummy enemy=dummy as EnemyDummy;
        CharacterStats stats=dummy.m_CharacterOverworld==null?null:dummy.m_CharacterOverworld.m_CharacterStats;
        JToken maxHp=enemy!=null && enemy.m_EnemyCombat!=null?new JValue(enemy.m_EnemyCombat.GetHealthTotal()):
            stats!=null?new JValue(stats.MaxHealth):new JValue((object)null);
        CharacterDummy.ProficiencyRecord record;
        bool present=dummy.m_SufferingProficiencies.TryGetValue(ProficiencyBase.Category.Armor,out record);
        if(present && (record==null || record.m_Proficiency==null))
            throw new InvalidOperationException("Native Armor record is incomplete.");
        JObject effect=null;
        if(present)
        {
            ProficiencyBase proficiency=record.m_Proficiency;
            effect=new JObject{{"count",record.m_Count},{"time",record.m_Time},{"fullTime",record.m_FullTime},
                {"proficiencyInstanceId",proficiency.GetInstanceID()},
                {"proficiencyId",(int)proficiency.m_ProficiencyID},
                {"customValue",proficiency.m_CustomValue},{"endOnTurn",proficiency.m_IsEndOnTurn}};
        }
        return new JObject{{"dummyInstanceId",dummy.GetInstanceID()},{"fid",GuardianObservedFid(dummy.FID)},
            {"side",side},{"alive",dummy.m_IsAlive},{"fled",dummy.m_DidFlee},
            {"hp",dummy.GetCurrentHealth()},{"maxHp",maxHp},{"armorMod",dummy.ArmorMod},
            {"sufferingCategories",categories},
            {"storedCombat",new JObject{
                {"scope","Last stored native fields; may belong to an earlier attack. Match FIDs and combat log before attribution."},
                {"attackInfo",GuardianStoredDamage(dummy.m_AttackInfo)},
                {"damageInfo",GuardianStoredDamage(dummy.m_DamageInfo)},
                {"postAttackHealthMod",dummy.m_PostAttackHealthMod}}},
            {"tauntArmor",dummy.m_TauntArmor},{"armorEffectPresent",present},{"armorEffect",effect}};
    }

    JObject GuardianObservation(JObject command)
    {
        CatalogKeys(command,"id","session","op");
        EncounterSession encounter=EncounterSession.Instance;
        if(encounter==null || !(bool)typeof(EncounterSession).GetField("m_IsInCombat",Members).GetValue(encounter))
            throw new InvalidOperationException("A current native combat encounter is required.");
        Type runtime=null;
        foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            if(assembly.GetName().Name=="FTKModFramework")runtime=assembly.GetType("FTKModFramework.Core.GuardianRuntime",false);
        if(runtime==null)throw new InvalidOperationException("Guardian runtime is unavailable.");
        object state=runtime.GetField("State",Statics).GetValue(null);
        if(state==null)throw new InvalidOperationException("Guardian state is unavailable.");
        JArray armorDummies=new JArray();
        foreach(CharacterDummy dummy in encounter.m_PlayerDummies.Values)
            if(dummy!=null)armorDummies.Add(GuardianArmorSnapshot(dummy,"player"));
        foreach(EnemyDummy dummy in encounter.m_EnemyDummies.Values)
            if(dummy!=null)armorDummies.Add(GuardianArmorSnapshot(dummy,"enemy"));
        JArray heroes=new JArray();
        foreach(CharacterDummy dummy in encounter.m_PlayerDummies.Values)
        {
            if(dummy==null || dummy.m_CharacterOverworld==null)continue;
            CharacterOverworld cow=dummy.m_CharacterOverworld;
            CharacterStats stats=cow.m_CharacterStats;
            string identity=(string)GuardianQuery(runtime,null,"Identity",dummy);
            bool guardian=(bool)GuardianQuery(runtime,null,"IsGuardian",dummy);
            bool canAct=(bool)GuardianQuery(runtime,null,"CanAct",dummy);
            bool active=guardian && (bool)GuardianQuery(state.GetType(),state,"IsActive",identity);
            JArray hands=new JArray();
            if(cow.m_PlayerInventory!=null)
                foreach(PlayerInventory.ContainerID slot in new[]{PlayerInventory.ContainerID.LeftHand,PlayerInventory.ContainerID.RightHand})
                {
                    JArray items=new JArray();ItemContainer container=cow.m_PlayerInventory.Get(slot);
                    if(container!=null && container.m_ItemCounts!=null)
                        foreach(KeyValuePair<FTK_itembase.ID,int> item in container.m_ItemCounts)
                            items.Add(new JObject{{"itemId",(int)item.Key},{"count",item.Value}});
                    hands.Add(new JObject{{"slot",slot.ToString()},{"items",items}});
                }
            heroes.Add(new JObject{{"identity",identity},{"dummyInstanceId",dummy.GetInstanceID()},
                {"heroInstanceId",cow.GetInstanceID()},{"classId",stats==null?-1:(int)stats.m_CharacterClass},
                {"classKey",cow.GetDBEntry()==null?null:cow.GetDBEntry().m_ID},
                {"hp",dummy.GetCurrentHealth()},{"focusPoints",stats==null?0:stats.m_FocusPoints},
                {"spentFocus",stats==null?0:stats.SpentFocus},{"weaponItemId",(int)cow.m_WeaponID},{"hands",hands},
                {"guardian",guardian},{"canAct",canAct},{"alive",dummy.m_IsAlive},
                {"stunned",dummy.Stunned},{"petrified",dummy.Petrified},{"fled",dummy.m_DidFlee},
                {"designatedAlly",guardian?(string)GuardianQuery(state.GetType(),state,"DesignatedAlly",identity):null},
                {"storedActive",active},{"effectiveActive",active && canAct},
                {"rescueAvailable",guardian?new JValue((bool)GuardianQuery(state.GetType(),state,"RescueAvailable",identity)):new JValue((object)null)},
                {"status",(string)GuardianQuery(runtime,null,"StatusDescription",dummy)},
                {"activeGuardians",new JArray((string[])GuardianQuery(state.GetType(),state,"ActiveGuardians",identity))}});
        }
        return new JObject{{"ok",true},{"frame",Time.frameCount},{"encounterInstanceId",encounter.GetInstanceID()},
            {"scope","Read-only current encounter snapshot; no actions, hits, grants or outcomes forced."},{"heroes",heroes},
            {"armorDummies",armorDummies},{"focusBinding",GuardianFocusBinding()},
            {"combatLog",encounter.EncounterSessionGameLog==null?null:encounter.EncounterSessionGameLog.SerializeLogInfo()}};
    }
}
