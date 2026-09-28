using System;
using System.Reflection;
using FTKModFramework.Core;
using GridEditor;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception(name);
    }
    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, name);
    }

    private static void Main()
    {
        Reject(delegate { new ItemClassAffinityBonuses(); }, "empty affinity rejected");
        Reject(delegate { new ItemClassAffinityBonuses(armor: 2); }, "armor bound enforced");
        Reject(delegate { new ItemClassAffinityBonuses(vitality: 0.02f); }, "per-item vitality bound enforced");
        Reject(delegate { new ItemClassAffinityBonuses(speed: float.NaN); }, "non-finite speed rejected");

        FTK_playerGameStart blacksmith = new FTK_playerGameStart { m_ID = "blacksmith", DisplayName = "Blacksmith" };
        FTK_playerGameStart hunter = new FTK_playerGameStart { m_ID = "hunter", DisplayName = "Hunter" };
        Check(ClassAffinityRuntime.Register(8101, blacksmith, 10, new ItemClassAffinityBonuses(vitality: 0.01f)),
            "one item affinity registers");
        Check(ClassAffinityRuntime.Register(8102, blacksmith, 10, new ItemClassAffinityBonuses(armor: 1)),
            "second item affinity registers");
        Check(ClassAffinityRuntime.Register(8103, blacksmith, 10,
            new ItemClassAffinityBonuses(resistance: 1, vitality: 0.01f, speed: 0.01f, reflect: 1)),
            "defense and secondary stat affinity registers");
        Check(ClassAffinityRuntime.Register(8101, blacksmith, 10, new ItemClassAffinityBonuses(vitality: 0.01f)),
            "identical registration is idempotent");
        Check(!ClassAffinityRuntime.Register(8101, blacksmith, 10, new ItemClassAffinityBonuses(speed: 0.01f)),
            "conflicting item registration is rejected");

        ItemClassAffinityTotal blacksmithTotals = ClassAffinityRuntime.Aggregate(10, new[] { 8101, 8102, 8102, 9090 });
        Check(blacksmithTotals != null && blacksmithTotals.Vitality == 0.01f && blacksmithTotals.Armor == 1,
            "matching equipped item rows add once and unrelated IDs are ignored");
        CharacterStats stats = new CharacterStats { m_CharacterClass = FTK_playerGameStart.ID.blacksmith };
        stats.m_CharacterMods.Add(FTK_characterModifier.ID.item_vitality);
        stats.m_CharacterMods.Add(FTK_characterModifier.ID.item_armor);
        stats.m_CharacterMods.Add(FTK_characterModifier.ID.item_extra);
        ClassAffinityCharacterModsPatch.Apply(stats, false);
        Check(stats.m_ModVitality == 0.02f && stats.Quickness == 0.01f && stats.m_ReflectDamage == 1,
            "native stat tally applies only supported matching affinity fields");
        ClassAffinityCharacterModsPatch.Apply(stats, true);
        Check(stats.m_ModDefensePhysical == 1 && stats.m_ModDefenseMagic == 1,
            "native defense tally applies affinity after rebuilding defense");
        CharacterStats hunterStats = new CharacterStats { m_CharacterClass = FTK_playerGameStart.ID.hunter };
        hunterStats.m_CharacterMods.Add(FTK_characterModifier.ID.item_vitality);
        ClassAffinityCharacterModsPatch.Apply(hunterStats, false);
        Check(hunterStats.m_ModVitality == 0f && hunterStats.Quickness == 0f,
            "native tally does not apply a different class's item affinity");
        Check(ClassAffinityRuntime.Aggregate(11, new[] { 8101, 8102 }) == null,
            "another class receives no class affinity");
        Check(ClassAffinityRuntime.Aggregate(10, new int[0]) == null,
            "unworn items contribute nothing");
        Check(ClassAffinityRuntime.Description(8101) == "Blacksmith bonus: +1 Vitality",
            "item description names the matching class and exact bonus");
        Check(GuardianEquipmentDescription.Append("Native item text", ClassAffinityRuntime.Description(8101)) ==
            "Native item text\nBlacksmith bonus: +1 Vitality", "item detail preserves and appends native text");
        Check(GuardianEquipmentDescription.Append(GuardianEquipmentDescription.Append("Native item text",
            ClassAffinityRuntime.Description(8101)), ClassAffinityRuntime.Description(8101)) ==
            "Native item text\nBlacksmith bonus: +1 Vitality", "description append is idempotent");
        uiItemDetail itemDetail = new uiItemDetail();
        itemDetail.m_EquippableProperties.text = "Native armor stats";
        InvokePatch(typeof(ClassAffinityItemUiPatch), itemDetail, FTK_itembase.ID.item_vitality);
        Check(itemDetail.m_EquippableProperties.text ==
            "Native armor stats\nBlacksmith bonus: +1 Vitality",
            "item detail patch preserves and appends affinity text");
        InvokePatch(typeof(ClassAffinityItemUiPatch), itemDetail, FTK_itembase.ID.item_vitality);
        Check(itemDetail.m_EquippableProperties.text.Split(new[] { "Blacksmith bonus" }, StringSplitOptions.None).Length == 2,
            "item detail refresh does not duplicate affinity text");
        uiWeaponDetail weaponDetail = new uiWeaponDetail();
        weaponDetail.m_WeaponStatDisplay.text = "Native weapon stats";
        InvokePatch(typeof(ClassAffinityWeaponUiPatch), weaponDetail,
            new FTK_itembase { m_ID = "affinity_item" });
        Check(weaponDetail.m_WeaponStatDisplay.text ==
            "Native weapon stats\nBlacksmith bonus: +1 Vitality",
            "weapon detail patch resolves synthetic identity and appends affinity text");
        Check(ClassAffinityRuntime.Description(9999) == string.Empty, "unregistered item has no description");
        blacksmith.DisplayName = "blacksmith";
        Check(ClassAffinityRuntime.Description(8101) == "Blacksmith bonus: +1 Vitality", "lowercase native label becomes a readable heading");
        blacksmith.DisplayName = "Forgeron";
        Check(ClassAffinityRuntime.Description(8101) == "Forgeron bonus: +1 Vitality", "localized class name resolves when tooltip is requested");
        blacksmith.DisplayName = "Blacksmith";

        Action restore = ClassAffinityRuntime.SuspendForReload();
        Check(ClassAffinityRuntime.ReloadEquipmentCount == 0 && ClassAffinityRuntime.Aggregate(10, new[] { 8101 }) == null,
            "hot reload suspension detaches prior registrations");
        Check(ClassAffinityRuntime.Register(8201, hunter, 11, new ItemClassAffinityBonuses(speed: 0.01f)),
            "candidate registrations remain isolated during reload");
        restore();
        Check(ClassAffinityRuntime.ReloadEquipmentCount == 3 && ClassAffinityRuntime.Aggregate(10, new[] { 8101 }) != null &&
            ClassAffinityRuntime.Aggregate(11, new[] { 8201 }) == null,
            "rollback restores prior affinity identity and discards candidate registrations");

        Console.WriteLine("PASS " + checks + " class-affinity contract assertions (game-free; live behavior remains separate)");
    }

    private static void InvokePatch(Type patchType, params object[] arguments)
    {
        MethodInfo method = patchType.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null) throw new Exception("Postfix not found: " + patchType.Name);
        method.Invoke(null, arguments);
    }
}
