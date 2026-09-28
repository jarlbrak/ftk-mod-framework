using System;
using System.Collections;
using System.Reflection;
using GridEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class BlacksmithAppearancePin
    {
        internal CharacterOverworld hero;
        internal CharacterStats stats;
        internal PlayerInventory inventory;
        internal CharacterEventListener avatar, retired;
        internal FTK_playerGameStart.SkinType original, desired;
        internal JToken preserved;
        internal int frame;
        internal bool restoring, valid, packageFit;
    }
    BlacksmithAppearancePin blacksmithAppearance;

    static bool BlacksmithPreservedEquals(JToken before, JToken after)
    {
        // The shipped Newtonsoft version throws when DeepEquals compares empty objects.
        // These snapshots use the same construction order; compare every serialized field
        // conservatively, so a changed value or order fails instead of weakening preservation.
        if (before == null || after == null) return ReferenceEquals(before, after);
        return String.Equals(before.ToString(Newtonsoft.Json.Formatting.None),
            after.ToString(Newtonsoft.Json.Formatting.None), StringComparison.Ordinal);
    }
    static JObject BlacksmithAppearanceScalars(object owner)
    {
        JObject result = new JObject();
        foreach (FieldInfo field in owner.GetType().GetFields(Members | BindingFlags.DeclaredOnly))
        {
            Type type = field.FieldType;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                result[field.Name] = field.GetValue(owner) == null ? new JValue((object)null) : JToken.FromObject(field.GetValue(owner));
            else if (type == typeof(Color)) result[field.Name] = PreviewRaceColor((Color)field.GetValue(owner));
        }
        return result;
    }
    static JObject BlacksmithAppearancePreserved(CharacterOverworld hero)
    {
        JObject inventory = new JObject();
        foreach (PlayerInventory.ContainerID slot in Enum.GetValues(typeof(PlayerInventory.ContainerID)))
        {
            JObject items = new JObject();
            foreach (System.Collections.Generic.KeyValuePair<FTK_itembase.ID, int> item in hero.m_PlayerInventory.Get(slot).m_CountDictionary)
                items[((int)item.Key).ToString(System.Globalization.CultureInfo.InvariantCulture)] = item.Value;
            inventory[slot.ToString()] = items;
        }
        return new JObject {{"class", (int)hero.m_CharacterStats.m_CharacterClass}, {"inventory", inventory},
            {"outfit", BlacksmithAppearanceScalars(hero.m_CustomOutfit)},
            {"colors", new JArray(PreviewRaceColor(hero.m_CharacterStats.m_ColorMain), PreviewRaceColor(hero.m_CharacterStats.m_ColorSkin), PreviewRaceColor(hero.m_CharacterStats.m_ColorHair))}};
    }
    static CharacterOverworld BlacksmithAppearanceHero(int id, bool inventoryClosed, bool packageFit = false)
    {
        RequireSinglePlayer(); RequireOutsideCombat();
        CharacterOverworld hero = packageFit ? ExactPackageFitHero(id) : ExactBlacksmithHero(id);
        if (!SceneOwner(hero) || !hero.gameObject.activeInHierarchy || !hero.IsOwner || hero.m_CharacterStats.m_IsInCombat || hero.m_WaitForRespawn || hero.m_Avatar == null || hero.m_Avatar.m_CharacterOverworld != hero)
            throw new InvalidOperationException("Owned living noncombat native Blacksmith avatar required.");
        foreach (CharacterOverworld member in FTKHub.Instance.m_CharacterOverworlds)
        {
            if (member == null) continue;
            object queue = BlacksmithRead(member, "m_MoveRPCQueue");
            if (member.m_IsMoving || member.m_CharacterStats.m_IsInCombat || (bool)BlacksmithRead(queue, "m_MoveCoroutineRunning") || ((ICollection)BlacksmithRead(queue, "m_Queue")).Count != 0)
                throw new InvalidOperationException("Party movement must be idle, with no queued native move or combat.");
        }
        if (inventoryClosed && uiPlayerInventory.Instance != null && uiPlayerInventory.Instance.m_IsShowing)
            throw new InvalidOperationException("Close native inventory before rebuilding appearance.");
        return hero;
    }
    bool BlacksmithAppearanceSettled()
    {
        BlacksmithAppearancePin pin = blacksmithAppearance;
        return pin != null && pin.hero != null && Time.frameCount > pin.frame && pin.retired == null &&
            pin.avatar != null && pin.hero.m_Avatar == pin.avatar && pin.avatar.m_CharacterOverworld == pin.hero;
    }
    void BlacksmithAppearanceCheck()
    {
        BlacksmithAppearancePin pin = blacksmithAppearance;
        if (pin == null) return;
        if (pin.hero == null || pin.hero.m_Avatar != pin.avatar || !ReferenceEquals(pin.hero.m_CharacterStats, pin.stats) ||
            !ReferenceEquals(pin.hero.m_PlayerInventory, pin.inventory) || pin.hero.m_SkinType != pin.original ||
            !BlacksmithPreservedEquals(pin.preserved, BlacksmithAppearancePreserved(pin.hero)))
        {
            var error = new InvalidOperationException("Appearance fixture owner, avatar, serialized skin, class, inventory, outfit or colors changed; no automatic correction attempted.");
            if (pin.packageFit)
            {
                packageFitUncertain = true; packageFitMutationHero = pin.hero; packageFitMutationOperation = "package-gear-appearance-validation";
                PackageFitFailureReceipt(error);
            }
            throw error;
        }
    }
    void BlacksmithAppearanceRebuild(FTK_playerGameStart.SkinType desired)
    {
        BlacksmithAppearancePin pin = blacksmithAppearance;
        JObject statsBefore = BlacksmithAppearanceScalars(pin.stats);
        if (pin.packageFit) PackageFitBeginMutation(pin.hero, "package-gear-appearance-rebuild");
        pin.valid = false;
        pin.retired = pin.hero.m_Avatar; pin.frame = Time.frameCount;
        try
        {
            pin.hero.m_SkinType = desired;
            pin.hero.AssignAvatar();
        }
        finally
        {
            // Never leave the serialized skin overridden across frames, including failure paths.
            pin.hero.m_SkinType = pin.original;
            pin.avatar = pin.hero.m_Avatar;
        }
        pin.desired = desired == FTK_playerGameStart.SkinType.None ? pin.hero.GetDBEntry().m_DefaultSkinType : desired;
        BlacksmithAppearanceCheck();
        if (!BlacksmithPreservedEquals(statsBefore, BlacksmithAppearanceScalars(pin.stats)))
            throw new InvalidOperationException("Native appearance rebuild changed scalar stats; no stat correction attempted.");
        pin.valid = true;
        if (pin.packageFit) packageFitMutationVerified = true;
    }
    JObject BlacksmithAppearanceStudio(int ownerId, int avatarId)
    {
        BlacksmithAppearancePin pin = blacksmithAppearance;
        if (pin == null || pin.hero == null || pin.hero.GetInstanceID() != ownerId) return null;
        BlacksmithAppearanceHero(ownerId, false, pin.packageFit); BlacksmithAppearanceCheck();
        if (!pin.valid || !BlacksmithAppearanceSettled() || pin.restoring || pin.avatar.GetInstanceID() != avatarId)
            throw new InvalidOperationException("Exact settled fixture avatar required for appearance capture.");
        return BlacksmithAppearanceReceipt();
    }
    JObject BlacksmithAppearanceReceipt()
    {
        BlacksmithAppearancePin pin = blacksmithAppearance;
        return pin == null ? null : new JObject {{"heroInstanceId", pin.hero.GetInstanceID()},
            {"celInstanceId", pin.avatar == null ? 0 : pin.avatar.GetInstanceID()},
            {"desiredVisualSkin", pin.desired.ToString()}, {"desiredVisualSkinType", (int)pin.desired},
            {"skinset", FTK_skinsetDB.Get(pin.hero.GetDBEntry().m_Skinsets[(int)pin.desired]).m_ID},
            {"restoredSerializedSkin", pin.hero.m_SkinType.ToString()}, {"originalSerializedSkin", pin.original.ToString()},
            {"preservationVerified", pin.valid}, {"settled", BlacksmithAppearanceSettled()}, {"restoring", pin.restoring},
            {"scope", "Temporary native visual avatar; serialized skin restored synchronously. Fit evidence only."}};
    }
    JObject BlacksmithAppearanceFixture(JObject command, bool packageFit = false)
    {
        if (packageFit)
        {
            CatalogKeys(command, "id", "session", "op", "action", "heroInstanceId", "skinType", "classKey", "classId");
            RequirePackageFitIsolation();
        }
        else CatalogKeys(command, "id", "session", "op", "action", "heroInstanceId", "skinType");
        int ownerId = LeaseObservationPin.ExactId(command, "heroInstanceId", false);
        string action = Str(command, "action");
        if (action != "apply" && action != "inspect" && action != "restore") throw new ArgumentException("action must be apply, inspect or restore");
        CharacterOverworld hero = BlacksmithAppearanceHero(ownerId, action != "inspect", packageFit);
        if (packageFit && !ReferenceEquals(hero.GetDBEntry(), ExactClassRow(Str(command, "classKey"), PreviewRaceIndex(command, "classId"))))
            throw new InvalidOperationException("Exact appearance owner class differs.");
        if (blacksmithAppearance != null && blacksmithAppearance.packageFit != packageFit)
            throw new InvalidOperationException("Restore through the operation that created this appearance fixture.");
        if (blacksmithAppearance != null && blacksmithAppearance.hero != hero)
            throw new InvalidOperationException("Restore the active exact hero fixture first.");
        if (blacksmithAppearance != null) BlacksmithAppearanceCheck();
        if (action == "apply")
        {
            int desired = PreviewRaceIndex(command, "skinType");
            FTK_playerGameStart row = hero.GetDBEntry();
            if (desired > 6 || row.m_Skinsets == null || desired >= row.m_Skinsets.Length || row.m_Skinsets[desired] == FTK_skinset.ID.None || FTK_skinsetDB.Get(row.m_Skinsets[desired]) == null)
                throw new ArgumentException("Requested native Blacksmith skin is unavailable.");
            int original = (int)(hero.m_SkinType == FTK_playerGameStart.SkinType.None ? row.m_DefaultSkinType : hero.m_SkinType);
            if (original < 0 || original > 6 || original >= row.m_Skinsets.Length || row.m_Skinsets[original] == FTK_skinset.ID.None || FTK_skinsetDB.Get(row.m_Skinsets[original]) == null)
                throw new InvalidOperationException("Original native skin is not restorable.");
            if (blacksmithAppearance != null && !BlacksmithAppearanceSettled()) throw new InvalidOperationException("Wait for the prior rebuild to settle.");
            if (blacksmithAppearance == null) blacksmithAppearance = new BlacksmithAppearancePin { hero = hero, stats = hero.m_CharacterStats,
                inventory = hero.m_PlayerInventory, packageFit = packageFit, original = hero.m_SkinType, avatar = hero.m_Avatar, preserved = BlacksmithAppearancePreserved(hero) };
            blacksmithAppearance.restoring = false;
            BlacksmithAppearanceRebuild((FTK_playerGameStart.SkinType)desired);
        }
        else if (action == "restore" && blacksmithAppearance != null)
        {
            if (!BlacksmithAppearanceSettled()) throw new InvalidOperationException("Wait for the prior rebuild to settle.");
            blacksmithAppearance.restoring = true;
            BlacksmithAppearanceRebuild(blacksmithAppearance.original);
        }
        JObject receipt = BlacksmithAppearanceReceipt();
        if (action == "inspect" && blacksmithAppearance != null && blacksmithAppearance.restoring && BlacksmithAppearanceSettled()) blacksmithAppearance = null;
        return new JObject {{"ok", true}, {"operation", packageFit ? "package-gear-appearance" : "blacksmith-appearance"}, {"session", sessionId}, {"heroInstanceId", ownerId},
            {"fixtureActive", blacksmithAppearance != null}, {"appearance", receipt}, {"serializedSkinType", (int)hero.m_SkinType},
            {"celInstanceId", hero.m_Avatar.GetInstanceID()}, {"preserved", BlacksmithAppearancePreserved(hero)}};
    }
}
