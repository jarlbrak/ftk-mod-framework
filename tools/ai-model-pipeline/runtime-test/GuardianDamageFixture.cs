using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    const string GuardianFixtureOwner="com.ftkmf.runtime-model-test.guardian-damage-fixture";
    static RuntimeModelTest guardianFixtureObserver;
    sealed class GuardianDamageReceipt
    {
        internal string Token;
        internal GuardianDamageFixturePolicy Policy;
        internal EncounterSession Encounter;
        internal UnityEngine.Object Diorama;
        internal EnemyDummy Enemy;
        internal CharacterDummy Victim;
        internal CharacterDummy[] Guardians;
        internal CharacterEventListener EnemyAvatar,VictimAvatar;
        internal CharacterEventListener[] GuardianAvatars;
        internal int[] GuardianHealth;
        internal float Deadline;
        internal JArray Events=new JArray();
        internal JObject Transformed;
    }
    GuardianDamageReceipt guardianDamageReceipt;
    readonly List<MethodInfo> guardianFixtureMethods=new List<MethodInfo>();
    bool guardianFixtureCleanup;

    static Type GuardianFixtureRuntime(){return CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.GuardianRuntime",true);}
    static object GuardianFixtureState(){return GuardianFixtureRuntime().GetField("State",Statics).GetValue(null);}
    static string GuardianFixtureIdentity(CharacterDummy value){return (string)GuardianQuery(GuardianFixtureRuntime(),null,"Identity",value);}
    static bool GuardianFixtureRescue(CharacterDummy value)
    {object state=GuardianFixtureState();return (bool)GuardianQuery(state.GetType(),state,"RescueAvailable",GuardianFixtureIdentity(value));}
    static bool[] GuardianFixtureRescues(GuardianDamageReceipt r)
    {
        bool[] result=new bool[r.Guardians.Length];
        for(int i=0;i<result.Length;i++)result[i]=GuardianFixtureRescue(r.Guardians[i]);
        return result;
    }
    static JArray GuardianFixtureGuardianReport(GuardianDamageReceipt r)
    {
        JArray result=new JArray();
        for(int i=0;i<r.Guardians.Length;i++)result.Add(new JObject{{"identity",r.Policy.GuardianIds[i]},
            {"instanceId",r.Guardians[i].GetInstanceID()},{"rescueAvailable",GuardianFixtureRescue(r.Guardians[i])}});
        return result;
    }
    static void GuardianFixtureGuard(GuardianDamageReceipt receipt)
    {
        object state=GuardianFixtureState();string victimId=GuardianFixtureIdentity(receipt.Victim);
        foreach(CharacterDummy guardian in receipt.Guardians)
        {
            string id=GuardianFixtureIdentity(guardian);
            if(!(bool)GuardianQuery(GuardianFixtureRuntime(),null,"IsGuardian",guardian) ||
                !(bool)GuardianQuery(GuardianFixtureRuntime(),null,"CanAct",guardian) ||
                !(bool)GuardianQuery(state.GetType(),state,"IsActive",id) ||
                (string)GuardianQuery(state.GetType(),state,"DesignatedAlly",id)!=victimId)
                throw new InvalidOperationException("Pinned guardians must actively protect the victim");
        }
        string[] protectors=(string[])GuardianQuery(state.GetType(),state,"ActiveGuardians",victimId);
        bool[] charges=new bool[protectors.Length];
        for(int i=0;i<charges.Length;i++)charges[i]=(bool)GuardianQuery(state.GetType(),state,"RescueAvailable",protectors[i]);
        if(!receipt.Policy.MatchesPins(protectors,charges))throw new InvalidOperationException("Exact guardian list or rescue-state snapshot changed");
    }
    static void GuardianFixturePins(GuardianDamageReceipt r,bool beforeCommit)
    {
        RequireSinglePlayer();
        if(EncounterSession.Instance!=r.Encounter || r.Encounter==null || !r.Encounter.m_IsInCombat || r.Encounter.m_ActiveDiorama!=r.Diorama ||
            r.Enemy==null || r.Victim==null || !r.Enemy.m_IsAlive ||
            (beforeCommit || r.Policy.ExpectAlive) && !r.Victim.m_IsAlive ||
            r.Encounter.GetDummyByFID(r.Enemy.FID)!=r.Enemy ||
            (beforeCommit || r.Victim.m_IsAlive) && r.Encounter.GetDummyByFID(r.Victim.FID)!=r.Victim ||
            r.Enemy.m_EventListener!=r.EnemyAvatar || r.Victim.m_EventListener!=r.VictimAvatar)
            throw new InvalidOperationException("Pinned encounter/actors/avatars changed");
        for(int i=0;i<r.Guardians.Length;i++)
        {
            CharacterDummy guardian=r.Guardians[i];
            if(guardian==null || !guardian.m_IsAlive || r.Encounter.GetDummyByFID(guardian.FID)!=guardian ||
                guardian.m_EventListener!=r.GuardianAvatars[i] || GuardianFixtureIdentity(guardian)!=r.Policy.GuardianIds[i] ||
                beforeCommit && guardian.GetCurrentHealth()!=r.GuardianHealth[i])
                throw new InvalidOperationException("Pinned guardian identity/avatar/health changed");
        }
        if(beforeCommit)
        {
            if(r.Victim.GetCurrentHealth()!=r.Policy.Health)throw new InvalidOperationException("Pinned victim health changed before fixture commit");
            if(r.Victim.IsResistDeath || r.Victim.m_CharacterOverworld.m_CharacterStats.m_MySanctumID!=FTK_sanctumStats.ID.None)
                throw new InvalidOperationException("Native death protection excluded from fixture");
            GuardianFixtureGuard(r);
        }
    }
    static JObject GuardianFixtureDdi(DummyDamageInfo d)
    {
        if(d==null)return null;
        return new JObject{{"attacker",d.m_AttackerID.m_TurnIndex+":"+d.m_AttackerID.m_PhotonID},
            {"victim",d.m_VictimID.m_TurnIndex+":"+d.m_VictimID.m_PhotonID},{"damage",d.m_Damage},{"health",d.m_NewHealth},
            {"response",d.m_AttackResponse.ToString()},{"proficiency",d.m_Prof.ToString()},{"crit",d.m_CritDamage},
            {"attackerHealthMod",d.m_AttackerHealthMod},{"aoe",d.m_IsAOE}};
    }
    void GuardianFixtureRecord(string phase,JObject data)
    {
        GuardianDamageReceipt r=guardianDamageReceipt;if(r==null)return;
        if(r.Events.Count>=24){r.Policy.Abort();guardianFixtureCleanup=true;return;}
        r.Events.Add(new JObject{{"phase",phase},{"frame",Time.frameCount},{"data",data}});
    }
    void GuardianFixtureAbort(Exception error)
    {
        if(guardianDamageReceipt==null)return;
        GuardianFixtureRecord("aborted",new JObject{{"reason",error.Message},{"previousStage",guardianDamageReceipt.Policy.Stage}});
        guardianDamageReceipt.Policy.Abort();guardianFixtureCleanup=true;
    }
    JObject GuardianDamageFixture(JObject command)
    {
        CatalogKeys(command,"id","session","op","action","case","encounterInstanceId","enemyInstanceId","victimInstanceId","guardianInstanceId","guardians","expectedVictimHp","receipt");
        RequireSinglePlayer();string action=Str(command,"action");
        if(action=="inspect")return GuardianFixtureReport();
        if(action=="disarm")
        {
            if(guardianDamageReceipt==null || Str(command,"receipt")!=guardianDamageReceipt.Token)throw new ArgumentException("Exact receipt required");
            if(guardianDamageReceipt.Policy.Stage=="committed")throw new InvalidOperationException("Committed native hit cannot be cancelled");
            GuardianFixtureAbort(new InvalidOperationException("Explicit disarm"));GuardianFixtureRemoveHooks();return GuardianFixtureReport();
        }
        if(action!="arm")throw new ArgumentException("Action must be arm, inspect or disarm");
        if(guardianDamageReceipt!=null && guardianDamageReceipt.Policy.Active)throw new InvalidOperationException("Fixture already active");
        GuardianFixtureRemoveHooks();
        EncounterSession encounter=EncounterSession.Instance;uiBattleStanceButtons buttons=FTKUI.Instance.m_BattleStanceButtons;
        if(encounter==null || !encounter.m_IsInCombat || encounter.GetInstanceID()!=Int(command,"encounterInstanceId",0) ||
            buttons==null || !buttons.m_Initialized || buttons.CombatCow==null ||
            buttons.CombatCow.GetCombatDummy().m_CharacterDummyFSM.ActiveStateName!="Wait For Stance")
            throw new InvalidOperationException("Exact encounter at stable native hero stance required");
        CharacterDummy victim=null;EnemyDummy enemy=null;
        foreach(CharacterDummy d in encounter.m_PlayerDummies.Values)
            if(d.GetInstanceID()==Int(command,"victimInstanceId",0))victim=d;
        foreach(EnemyDummy d in encounter.m_EnemyDummies.Values)if(d.GetInstanceID()==Int(command,"enemyInstanceId",0))enemy=d;
        List<CharacterDummy> guardians=new List<CharacterDummy>();List<bool> rescues=new List<bool>();
        JArray requested=command["guardians"] as JArray;
        if(command["guardians"]!=null && requested==null)throw new ArgumentException("guardians must be an array");
        if(requested==null)
        {
            if(Str(command,"case")!="reduction" && Str(command,"case")!="rescue")throw new ArgumentException("New cases require explicit guardians and rescue states");
            requested=new JArray(new JObject{{"instanceId",Int(command,"guardianInstanceId",0)},{"rescueAvailable",true}});
        }
        else if(command["guardianInstanceId"]!=null)throw new ArgumentException("Use guardians or guardianInstanceId, not both");
        if(requested.Count<1 || requested.Count>2)throw new ArgumentException("One or two exact guardians required");
        foreach(JToken token in requested)
        {
            JObject pin=token as JObject;if(pin==null)throw new ArgumentException("Guardian pin must be an object");
            CatalogKeys(pin,"instanceId","rescueAvailable");
            if(pin["rescueAvailable"]==null || pin["rescueAvailable"].Type!=JTokenType.Boolean)throw new ArgumentException("Explicit boolean rescue state required");
            CharacterDummy guardian=null;
            foreach(CharacterDummy d in encounter.m_PlayerDummies.Values)if(d.GetInstanceID()==Int(pin,"instanceId",0))guardian=d;
            if(guardian==null || guardian==victim || guardian.m_EventListener==null || guardians.Contains(guardian))
                throw new ArgumentException("Distinct exact guardian and victim required");
            guardians.Add(guardian);rescues.Add((bool)pin["rescueAvailable"]);
        }
        if(enemy==null || victim==null || enemy.m_EventListener==null || victim.m_EventListener==null)
            throw new ArgumentException("Exact live enemy and victim required");
        string[] ids=new string[guardians.Count];for(int i=0;i<ids.Length;i++)ids[i]=GuardianFixtureIdentity(guardians[i]);
        GuardianDamageFixturePolicy policy=new GuardianDamageFixturePolicy(Str(command,"case"),Int(command,"expectedVictimHp",0),ids,rescues.ToArray());
        guardians.Sort(delegate(CharacterDummy a,CharacterDummy b){return StringComparer.Ordinal.Compare(GuardianFixtureIdentity(a),GuardianFixtureIdentity(b));});
        GuardianDamageReceipt r=new GuardianDamageReceipt{Token=Guid.NewGuid().ToString("N"),Encounter=encounter,Diorama=encounter.m_ActiveDiorama,
            Enemy=enemy,Victim=victim,Guardians=guardians.ToArray(),EnemyAvatar=enemy.m_EventListener,VictimAvatar=victim.m_EventListener,
            GuardianAvatars=new CharacterEventListener[guardians.Count],GuardianHealth=new int[guardians.Count],Deadline=Time.realtimeSinceStartup+120,Policy=policy};
        for(int i=0;i<r.Guardians.Length;i++){r.GuardianAvatars[i]=r.Guardians[i].m_EventListener;r.GuardianHealth[i]=r.Guardians[i].GetCurrentHealth();}
        GuardianFixturePins(r,true);guardianDamageReceipt=r;guardianFixtureObserver=this;
        try{GuardianFixtureInstallHooks();}catch{r.Policy.Abort();GuardianFixtureRemoveHooks();throw;}
        GuardianFixtureRecord("armed",new JObject{{"enemyId",enemy.GetInstanceID()},{"victimId",victim.GetInstanceID()},{"guardians",GuardianFixtureGuardianReport(r)},
            {"guardianId",r.Guardians.Length==1?new JValue(r.Guardians[0].GetInstanceID()):new JValue((object)null)},
            {"rescueAvailable",r.Guardians.Length==1?new JValue(GuardianFixtureRescue(r.Guardians[0])):new JValue((object)null)},
            {"hp",r.Policy.Health},{"damage",r.Policy.Damage},{"expectedHp",r.Policy.ExpectedHealth},{"expectedAlive",r.Policy.ExpectAlive},{"case",r.Policy.Scenario}});
        return GuardianFixtureReport();
    }
    JObject GuardianFixtureReport()
    {
        GuardianDamageReceipt r=guardianDamageReceipt;
        return new JObject{{"ok",true},{"session",sessionId},{"status",r==null?"idle":r.Policy.Stage},{"receipt",r==null?null:r.Token},
            {"events",r==null?new JArray():r.Events.DeepClone()},{"scope","One-shot fixture outcome through native enemy turn/playback. Not ordinary RNG, damage calculation or co-op evidence."}};
    }
    void GuardianFixtureInstallHooks()
    {
        MethodInfo playback=typeof(CharacterDummy).GetMethod("PlayAttackSequence",Members);
        Type production=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.GuardianDamagePatch",true);
        MethodInfo expected=production.GetMethod("Prefix",Statics);Patches patches=Harmony.GetPatchInfo(playback);int matches=0;
        if(patches!=null)foreach(Patch patch in patches.Prefixes)
            if(patch.owner=="com.ftkmf.framework" && patch.PatchMethod==expected)matches++;
        if(matches!=1)throw new InvalidOperationException("Exact production Guardian damage prefix must be installed");
        Harmony h=new Harmony(GuardianFixtureOwner);
        GuardianFixtureHook(h,typeof(CharacterDummy).GetMethod("EngageBattle",Members),"GuardianFixtureEngage",null);
        GuardianFixtureHook(h,typeof(DamageCalculator).GetMethod("_playAttackSequence",Statics),"GuardianFixtureCommit",null);
        GuardianFixtureHook(h,typeof(CharacterDummy).GetMethod("PlayAttackSequence",Members),null,"GuardianFixturePlayback");
        GuardianFixtureHook(h,typeof(CharacterDummy).GetMethod("RespondToHit",Members),null,"GuardianFixtureHit");
    }
    void GuardianFixtureHook(Harmony h,MethodInfo method,string prefix,string postfix)
    {
        if(method==null)throw new InvalidOperationException("Native fixture hook missing");
        guardianFixtureMethods.Add(method);
        h.Patch(method,prefix==null?null:new HarmonyMethod(typeof(RuntimeModelTest).GetMethod(prefix,Statics)),
            postfix==null?null:new HarmonyMethod(typeof(RuntimeModelTest).GetMethod(postfix,Statics)));
    }
    void GuardianFixtureRemoveHooks()
    {
        Harmony h=new Harmony(GuardianFixtureOwner);
        foreach(MethodInfo method in guardianFixtureMethods)h.Unpatch(method,HarmonyPatchType.All,GuardianFixtureOwner);
        guardianFixtureMethods.Clear();guardianFixtureCleanup=false;if(guardianFixtureObserver==this)guardianFixtureObserver=null;
    }
    void GuardianFixtureTick()
    {
        if(guardianFixtureCleanup){GuardianFixtureRemoveHooks();return;}
        GuardianDamageReceipt r=guardianDamageReceipt;if(r==null || !r.Policy.Active)return;
        try
        {
            if(Time.realtimeSinceStartup>r.Deadline)throw new InvalidOperationException("Fixture timed out; no automatic retry");
            GuardianFixturePins(r,r.Policy.Stage!="committed");
        }
        catch(Exception e){GuardianFixtureAbort(e);}
    }
    static void GuardianFixtureEngage(CharacterDummy __instance,ref FTKPlayerID _playerVictim)
    {
        RuntimeModelTest o=guardianFixtureObserver;if(o==null)return;GuardianDamageReceipt r=o.guardianDamageReceipt;
        if(r.Policy.Stage!="armed" || __instance!=r.Enemy)return;
        try
        {
            GuardianFixturePins(r,true);r.Policy.Retarget();
            o.GuardianFixtureRecord("retarget",new JObject{{"originalVictim",_playerVictim.m_TurnIndex+":"+_playerVictim.m_PhotonID}});
            _playerVictim=r.Victim.FID;
        }
        catch(Exception e){o.GuardianFixtureAbort(e);}
    }
    static void GuardianFixtureCommit(ref AttackAttempt _atk,ref DummyDamageInfo _ddi0,ref DummyDamageInfo _ddi1,ref DummyDamageInfo _ddi2)
    {
        RuntimeModelTest o=guardianFixtureObserver;if(o==null)return;GuardianDamageReceipt r=o.guardianDamageReceipt;
        if(r.Policy.Stage!="targeted" || _atk.m_AttackingDummy!=r.Enemy)return;
        try
        {
            GuardianFixturePins(r,true);
            // Native m_RepeatCount is ProficiencyRecord duration, not an attack-count field.
            // _finishEngageAttack represents additional victims in the actual AoE list and DDIs.
            int aoeCount=_atk.m_AoeTargets==null?0:_atk.m_AoeTargets.Count;
            o.GuardianFixtureRecord("commit-shape",new JObject{{"pinnedVictimMatches",_atk.m_DamagedDummy==r.Victim},
                {"main",GuardianFixtureDdi(_ddi0)},{"secondary1",GuardianFixtureDdi(_ddi1)},
                {"secondary2",GuardianFixtureDdi(_ddi2)},{"aoeTargets",aoeCount}});
            GuardianDamageFixturePolicy.RequireSingleTarget(_atk.m_DamagedDummy==r.Victim,_ddi0!=null,
                _ddi1!=null || _ddi2!=null,aoeCount,_ddi0!=null && _ddi0.m_IsAOE);
            DummyDamageInfo known=new DummyDamageInfo{m_AttackerID=r.Enemy.FID,m_VictimID=r.Victim.FID,
                m_WeaponType=r.EnemyAvatar.m_Weapon.m_WeaponType,m_WeaponSubType=r.EnemyAvatar.m_Weapon.m_WeaponSubType,
                m_DamageType=FTK_weaponStats2.DamageType.physical,m_Damage=r.Policy.Damage,m_NewHealth=Math.Max(0,r.Policy.Health-r.Policy.Damage),
                m_AttackResponse=r.Policy.Damage>=r.Policy.Health?CharacterDummy.AttackResponse.Death:CharacterDummy.AttackResponse.Damaged};
            r.Policy.Commit(); // Consume before changing the native commit; never replay an uncertain result.
            o.GuardianFixtureRecord("commit",new JObject{{"original",GuardianFixtureDdi(_ddi0)},{"replacement",GuardianFixtureDdi(known)}});
            _ddi0=known;_ddi1=null;_ddi2=null;_atk.m_AttackAnim=CharacterDummy.AttackAnim.DirectAttack;
            _atk.m_AttackAnimOverride=CharacterEventListener.CombatAnimTrigger.None;
        }
        catch(Exception e){o.GuardianFixtureAbort(e);}
    }
    static void GuardianFixturePlayback(CharacterDummy __instance)
    {
        RuntimeModelTest o=guardianFixtureObserver;if(o==null)return;GuardianDamageReceipt r=o.guardianDamageReceipt;
        if(r.Policy.Stage!="committed" || __instance!=r.Enemy)return;
        try
        {r.Transformed=GuardianFixtureDdi(__instance.m_AttackInfo);o.GuardianFixtureRecord("production-transformed",r.Transformed);}
        catch(Exception e){o.GuardianFixtureAbort(e);}
    }
    static void GuardianFixtureHit(CharacterDummy __instance)
    {
        RuntimeModelTest o=guardianFixtureObserver;if(o==null)return;GuardianDamageReceipt r=o.guardianDamageReceipt;
        if(r.Policy.Stage!="committed" || __instance!=r.Victim)return;
        try
        {
            GuardianFixturePins(r,false);
            if(r.Transformed==null || r.Victim.m_DamageInfo==null ||
                !JToken.DeepEquals(GuardianFixtureDdi(r.Victim.m_DamageInfo),r.Transformed))
                throw new InvalidOperationException("Native hit does not match observed production-transformed outcome");
            bool pass=r.Policy.Complete(r.Victim.GetCurrentHealth(),r.Victim.m_IsAlive,GuardianFixtureRescues(r),
                (int)r.Transformed["damage"],(int)r.Transformed["health"]);
            o.GuardianFixtureRecord("native-hit",new JObject{{"passed",pass},{"hp",r.Victim.GetCurrentHealth()},{"alive",r.Victim.m_IsAlive},
                {"guardians",GuardianFixtureGuardianReport(r)},
                {"rescueAvailable",r.Guardians.Length==1?new JValue(GuardianFixtureRescue(r.Guardians[0])):new JValue((object)null)},
                {"outcome",GuardianFixtureDdi(r.Victim.m_DamageInfo)}});
            o.guardianFixtureCleanup=true;
        }
        catch(Exception e){o.GuardianFixtureAbort(e);}
    }
}
