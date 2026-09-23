using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Read metadata without loading Unity or executing anything in the supplied installed assembly.
internal static class NativeSignatures
{
    internal static void Verify(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (PEReader pe = new PEReader(stream))
        {
            MetadataReader reader = pe.GetMetadataReader();
            Require(reader, "uiBattleStanceButtons", "CreateWeaponProficiencyButtons", "_weapon", "_needsReload");
            Require(reader, "uiBattleStanceButtons", "DisplayBattleActionInfo", "_button", "_on");
            Require(reader, "uiBattleStanceButtons", "FocusSlot", "_button");
            Require(reader, "uiBattleStanceButtons", "AttackProficiency", "_button");
            Require(reader, "DamageCalculator", "_waitForUserPickTarget", "_party", "_av", "_dmgMod", "_aoeDmgMod", "_targetType", "_targetFriendly", "_consumable", "_cheatType");
            Require(reader, "DamageCalculator", "_calcDamage", "_atk", "_dmgMultiplier", "_mainTarget", "_itemAttack", "_cheatType");
            Require(reader, "DamageCalculator", "_playAttackSequence", "_atk", "_ddi0", "_ddi1", "_ddi2");
            Require(reader, "DamageCalculator", "_finishEngageAttack", "_aa", "_dmgMod", "_aoeDmgMod", "_targetType", "_targetFriendly", "_consumable", "_cheatType");
            Require(reader, "CharacterOverworld", "CheckUpdateAvatarAndPortrait", "_item", "_itembase", "_contID", "_isEquip", "_isWeaponSwap");
            Require(reader, "EncounterSession", "UpdateAttackTimeline", "_attacked", "_foe", "_activeTimePortraitID", "_cfsm");
            Require(reader, "CharacterDummy", "FleeRPC", "_success", "_fireSelected");
            Require(reader, "CharacterStats", "UpdateFocusPoints", "_focus", "_broadcast");
            Require(reader, "CharacterStats", "BroadcastAllCursesRPC", "_activecurses", "_permacurse");
            Require(reader, "CharacterStats", "SetPoison", "_lvl", "_broadcast", "_hud");
            Require(reader, "CharacterDummy", "RemoveSpecificProficiency", "_c");
            Require(reader, "CharacterDummy", "RespondToHit", "_mainVictim");
            Require(reader, "CharacterDummy", "PlayAttackSequence", "_attackAnim", "_override", "_ddi", "_ddi1", "_ddi2");
            Require(reader, "CharacterDummy", "EngageBattle", "_randomCheck", "_isFirstAttack", "_playerVictim", "_cheerIfDie");
            Require(reader, "CharacterDummy", "AddProfToDummy", "_prof", "_fx", "_hud");
            Require(reader, "CharacterDummy", "ResetForCombat");
            Require(reader, "CharacterDummy", "InitDummyForCombat", "_canFlee", "_useLightprobe");
            Require(reader, "CharacterDummy", "CombatFinished");
            Require(reader, "CharacterDummy", "EngageDirectAttack", "_prof");
            Require(reader, "CharacterStats", "GainSpecificHealth", "_hpGain", "_showHudText", "_broadcast");
            Require(reader, "CharacterDummy", "PostAttackHealthModSequence", "_mod");
            Require(reader, "EnemyDummy", "TakeSecondaryDamage", "_type", "_dmg", "_profID", "_spawnHud", "_attackerID");
            Require(reader, "uiItemDetail", "Show", "_itemID", "_mode", "_cow", "_showingEquip", "_forceFrontSide", "_loreCard");
            Require(reader, "uiWeaponDetail", "ShowWeapon", "_itemInfo");
            Require(reader, "uiSelectCharacterInfo", "ShowCharacterInfo", "_characterType");
            Console.WriteLine("PASS GuardianCombat: 29 native method/parameter metadata checks");
        }
    }

    private static void Require(MetadataReader reader, string type, string method, params string[] parameters)
    {
        TypeDefinition definition = reader.TypeDefinitions.Select(reader.GetTypeDefinition)
            .Single(t => reader.GetString(t.Name) == type);
        MethodDefinition[] methods = definition.GetMethods().Select(reader.GetMethodDefinition)
            .Where(m => reader.GetString(m.Name) == method).ToArray();
        if (methods.Length != 1) throw new Exception(type + "." + method + " missing or ambiguous");
        string[] names = methods[0].GetParameters().Select(reader.GetParameter)
            .Where(p => p.SequenceNumber > 0).Select(p => reader.GetString(p.Name)).ToArray();
        if (!names.SequenceEqual(parameters)) throw new Exception(type + "." + method + " parameter contract changed");
    }
}
