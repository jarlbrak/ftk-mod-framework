using System;
using GridEditor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using FTKHelp;

public sealed partial class RuntimeModelTest
{
    sealed class HeroDamageReceipt
    {
        internal string token;
        internal CharacterOverworld hero;
        internal CharacterStats stats;
        internal Weapon nativeWeapon;
        internal FTK_weaponStats2 weaponRow;
        internal CharacterEventListener cel;
        internal Animator animator;
        internal RuntimeAnimatorController controller;
        internal JObject before,after,invariants;
        internal HeroDamageFixturePolicy policy;
    }
    HeroDamageReceipt heroDamageReceipt;

    static int DamageInteger(JObject command,string key)
    {
        JToken value=command[key];
        if(value==null || value.Type!=JTokenType.Integer)throw new ArgumentException("Explicit integer required: "+key);
        return checked((int)value);
    }

    static void HeroDamageSafeBoundary()
    {
        RequireSinglePlayer();
        try{RequireOutsideCombat();}catch(InvalidOperationException){RequireReadyPreparation();}
    }
    static CharacterOverworld DamageHero(int id)
    {
        CharacterOverworld found=null;
        foreach(CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if(candidate!=null && candidate.GetInstanceID()==id)
            {if(found!=null)throw new InvalidOperationException("Ambiguous hero identity.");found=candidate;}
        if(found==null || found.m_CharacterStats==null || found.m_CharacterStats.m_HealthCurrent<=0
            || !ReferenceEquals(found.m_CharacterStats.m_CharacterOverworld,found))
            throw new InvalidOperationException("Exact living current-party hero required.");
        return found;
    }
    static JObject DamageInvariants(CharacterOverworld hero)
    {
        FTK_itembase.ID weapon;FTK_weaponStats2.SkillType skill=EquippedAttackSkill(hero,out weapon);
        CharacterStats stats=hero.m_CharacterStats;
        FTK_weaponStats2 row=FTK_itembase.GetItemBase(weapon)as FTK_weaponStats2;
        if(row==null || !Enum.IsDefined(typeof(FTK_itembase.ID),weapon)
            || row._dmgtype!=FTK_weaponStats2.DamageType.physical || (int)hero.m_WeaponID!=(int)weapon)
            throw new InvalidOperationException("Exact equipped physical native weapon and stats weapon must agree.");
        if(stats.SpentFocus!=0)throw new InvalidOperationException("Damage fixture requires zero spent focus.");
        JObject health=HeroHealth(stats,hero);
        CharacterEventListener cel=hero.m_Avatar;
        Animator animator=cel==null?null:cel.m_Animator;
        if(cel==null || cel.m_Weapon==null || animator==null || animator.runtimeAnimatorController==null
            || !ReferenceEquals(row,FTK_weaponStats2DB.GetDB().GetEntry(hero.m_WeaponID)))
            throw new InvalidOperationException("Exact native weapon object, row, CEL, Animator and controller required.");
        int physical=(int)typeof(CharacterStats).GetField("m_ModAttackPhysical",Members).GetValue(stats);
        int all=(int)typeof(CharacterStats).GetField("m_ModAttackAll",Members).GetValue(stats);
        if(stats.m_IsInCombat && hero.m_CurrentDummy==null)throw new InvalidOperationException("Native combat damage owner unavailable.");
        return new JObject{{"heroInstanceId",hero.GetInstanceID()},{"statsInstanceId",stats.GetInstanceID()},
            {"photonId",health["photonId"]},{"turnIndex",health["turnIndex"]},{"weaponItemId",(int)weapon},{"weaponItem",weapon.ToString()},
            {"statsWeaponId",(int)hero.m_WeaponID},{"damageType",row._dmgtype.ToString()},
            {"nativeWeaponInstanceId",cel.m_Weapon.GetInstanceID()},
            {"weaponBaseDamage",row._maxdmg},{"weaponDamageGain",row._dmggain},
            {"playerLevel",stats.m_PlayerLevel},{"playerXp",stats.m_PlayerXP},
            {"levelXpThresholds",new JArray(GameFlow.Instance.m_LevelXpValues)},
            {"statsInCombat",stats.m_IsInCombat},
            {"combatDamageModifier",stats.m_IsInCombat?hero.m_CurrentDummy.AttackDmgMod:0f},
            {"chaosDamageModifier",GameFlow.Instance.GetChaosAttackDamageMultiplier()},
            {"rawPhysicalModifier",physical},{"rawAllDamageModifier",all},
            {"rawPhysicalDamageBeforeAugmentation",row._dmggain*stats.m_PlayerLevel+row._maxdmg+physical+all},
            {"magicAugmentation",stats.m_AugmentedDamageMagic},
            {"skill",skill.ToString()},{"rawSkill",stats.GetRawSkillValue(skill)},
            {"focusPoints",stats.m_FocusPoints},{"spentFocus",stats.SpentFocus},
            {"avatarId",cel==null?0:cel.GetInstanceID()},{"animatorId",animator==null?0:animator.GetInstanceID()},
            {"controllerId",animator==null || animator.runtimeAnimatorController==null?0:animator.runtimeAnimatorController.GetInstanceID()}};
    }
    static JObject DamageView(CharacterOverworld hero)
    {
        JObject result=DamageInvariants(hero);
        result["augmentedPhysicalDamage"]=hero.m_CharacterStats.m_AugmentedDamagePhysical;
        result["nativeCritDamagePercent"]=GameFlow.Instance.m_CritDmgPercent;
        result["nativeChaosDamageMultiplier"]=GameFlow.Instance.GetChaosAttackDamageMultiplier();
        JObject raceBonuses=new JObject();
        foreach(var bonus in hero.m_CharacterStats.m_DamageAgainstBonus)raceBonuses[bonus.Key.ToString()]=bonus.Value;
        result["nativeRaceDamageBonuses"]=raceBonuses;
        result["nativeWeaponMaxDamage"]=hero.m_CharacterStats.GetWeaponMaxDamage();return result;
    }
    void ExactDamageReceipt(HeroDamageReceipt receipt,bool allowLevelProgression=false)
    {
        HeroDamageSafeBoundary();
        CharacterOverworld hero=DamageHero(receipt.hero.GetInstanceID());
        if(!ReferenceEquals(hero,receipt.hero) || !ReferenceEquals(hero.m_CharacterStats,receipt.stats)
            || !ReferenceEquals(hero.m_Avatar,receipt.cel) || receipt.cel==null
            || !ReferenceEquals(receipt.cel.m_Weapon,receipt.nativeWeapon) || receipt.nativeWeapon==null
            || !ReferenceEquals(receipt.cel.m_Animator,receipt.animator) || receipt.animator==null
            || !ReferenceEquals(receipt.animator.runtimeAnimatorController,receipt.controller) || receipt.controller==null
            || !ReferenceEquals(FTK_weaponStats2DB.GetDB().GetEntry(hero.m_WeaponID),receipt.weaponRow))
            throw new InvalidOperationException("Hero/weapon/focus/stat authority drifted; refusing fixture mutation.");
        JObject current=DamageInvariants(hero),expected=(JObject)receipt.invariants.DeepClone();
        if(allowLevelProgression)
        {
            HeroDamageFixturePolicy.RequireLevelProgression((int)expected["playerLevel"],(int)expected["playerXp"],
                hero.m_CharacterStats.m_PlayerLevel,hero.m_CharacterStats.m_PlayerXP,GameFlow.Instance.m_LevelXpValues);
            // Only XP, level and the known weapon-gain term may differ. Skill, focus,
            // modifiers, native references and controller identity remain exact.
            expected["playerLevel"]=current["playerLevel"];
            expected["playerXp"]=current["playerXp"];
            expected["rawPhysicalDamageBeforeAugmentation"]=current["rawPhysicalDamageBeforeAugmentation"];
        }
        if(!JToken.DeepEquals(current,expected))throw new InvalidOperationException("Hero/weapon/focus/stat authority drifted; refusing fixture mutation.");
    }
    static int RestoredDamageAtCurrentLevel(HeroDamageReceipt receipt)
    {
        // Same arithmetic/order as CharacterStats.GetWeaponMaxDamage() without races,
        // using the original augmentation and current validated native level. No writes.
        CharacterStats stats=receipt.stats;
        float value=receipt.weaponRow._dmggain*stats.m_PlayerLevel+receipt.weaponRow._maxdmg;
        value+=(int)typeof(CharacterStats).GetField("m_ModAttackPhysical",Members).GetValue(stats)+receipt.policy.before;
        value+=(int)typeof(CharacterStats).GetField("m_ModAttackAll",Members).GetValue(stats);
        if(stats.m_IsInCombat)value*=1f+receipt.hero.m_CurrentDummy.AttackDmgMod;
        value*=1f+GameFlow.Instance.GetChaosAttackDamageMultiplier();
        if(float.IsNaN(value) || float.IsInfinity(value))throw new InvalidOperationException("Nonfinite current-level damage baseline.");
        return FTKUtil.RoundToInt(value);
    }
    JObject HeroDamageFixture(JObject command)
    {
        CatalogKeys(command,"id","session","op","action","heroInstanceId","expectedWeaponItemId","expectedAugmentedPhysicalDamage","expectedNativeWeaponMaxDamage","minimumNativeWeaponMaxDamage","receipt");
        HeroDamageSafeBoundary();
        string action=Str(command,"action");
        if(action=="restore")
        {
            HeroDamageReceipt receipt=heroDamageReceipt;
            if(receipt==null || Str(command,"receipt")!=receipt.token || DamageInteger(command,"heroInstanceId")!=receipt.hero.GetInstanceID())
                throw new InvalidOperationException("Exact active damage fixture receipt and hero required.");
            JObject before=DamageView(receipt.hero);
            ExactDamageReceipt(receipt,true);
            int expectedMaximum=RestoredDamageAtCurrentLevel(receipt);
            receipt.policy.Restore(()=>receipt.stats.m_AugmentedDamagePhysical,
                delta=>receipt.stats.AugmentCharacterOther(FTK_miniEncounter.TrainerType.PhysDmg,delta),
                ()=>receipt.stats.GetWeaponMaxDamage(),()=>ExactDamageReceipt(receipt,true),expectedMaximum);
            JObject after=DamageView(receipt.hero);heroDamageReceipt=null;
            return new JObject{{"ok",true},{"status","hero-damage-restored"},{"receipt",receipt.token},{"before",before},{"after",after},
                {"originalNativeWeaponMaxDamage",receipt.policy.maximumBefore},{"expectedRestoredNativeWeaponMaxDamage",expectedMaximum},
                {"levelProgression",new JObject{{"originalLevel",receipt.invariants["playerLevel"]},{"currentLevel",after["playerLevel"]},
                    {"originalXp",receipt.invariants["playerXp"]},{"currentXp",after["playerXp"]},
                    {"weaponDamageGainPerLevel",receipt.invariants["weaponDamageGain"]},
                    {"originalRawPhysicalDamage",receipt.invariants["rawPhysicalDamageBeforeAugmentation"]},
                    {"currentRawPhysicalDamage",after["rawPhysicalDamageBeforeAugmentation"]}}}};
        }
        CharacterOverworld hero=DamageHero(DamageInteger(command,"heroInstanceId"));
        JObject view=DamageView(hero);
        if(action=="inspect")return new JObject{{"ok",true},{"hero",view},{"receipt",heroDamageReceipt==null?null:heroDamageReceipt.token},
            {"restorationPending",heroDamageReceipt!=null && heroDamageReceipt.policy.restoreRequired},
            {"nativeRestoreUncertain",heroDamageReceipt!=null && heroDamageReceipt.policy.nativeRestoreUncertain},
            {"receiptBefore",heroDamageReceipt==null?null:heroDamageReceipt.before},
            {"receiptAfter",heroDamageReceipt==null?null:heroDamageReceipt.after}};
        if(action!="apply")throw new ArgumentException("action must be inspect, apply or restore.");
        if(heroDamageReceipt!=null)throw new InvalidOperationException("Restore the active damage fixture before another apply.");
        if(DamageInteger(command,"expectedWeaponItemId")!=(int)view["weaponItemId"]
            || DamageInteger(command,"expectedAugmentedPhysicalDamage")!=(int)view["augmentedPhysicalDamage"]
            || DamageInteger(command,"expectedNativeWeaponMaxDamage")!=(int)view["nativeWeaponMaxDamage"])
            throw new InvalidOperationException("Explicit before-values changed; inspect before applying.");
        HeroDamageReceipt pending=new HeroDamageReceipt{token=Guid.NewGuid().ToString("N"),hero=hero,stats=hero.m_CharacterStats,
            cel=hero.m_Avatar,nativeWeapon=hero.m_Avatar.m_Weapon,animator=hero.m_Avatar.m_Animator,
            controller=hero.m_Avatar.m_Animator.runtimeAnimatorController,weaponRow=FTK_weaponStats2DB.GetDB().GetEntry(hero.m_WeaponID),
            before=view,invariants=DamageInvariants(hero),
            policy=new HeroDamageFixturePolicy((int)view["augmentedPhysicalDamage"],(int)view["nativeWeaponMaxDamage"],DamageInteger(command,"minimumNativeWeaponMaxDamage"))};
        heroDamageReceipt=pending;
        try
        {
            pending.policy.Apply(()=>pending.stats.m_AugmentedDamagePhysical,
                delta=>pending.stats.AugmentCharacterOther(FTK_miniEncounter.TrainerType.PhysDmg,delta),
                ()=>pending.stats.GetWeaponMaxDamage(),()=>ExactDamageReceipt(pending));
            pending.after=DamageView(hero);
        }
        catch{if(!pending.policy.restoreRequired)heroDamageReceipt=null;throw;}
        return new JObject{{"ok",true},{"status","hero-damage-fixture-applied"},{"receipt",pending.token},
            {"minimumNativeWeaponMaxDamage",pending.policy.minimum},{"before",pending.before},{"after",pending.after},
            {"provenance","Explicit temporary hero physical-damage augmentation through native PhysDmg trainer method; native weapon, focus, RNG and attack callbacks unchanged. Balance is a fixture."}};
    }
}
