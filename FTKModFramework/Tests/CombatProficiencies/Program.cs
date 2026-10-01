using System;
using FTKModFramework.Core;
using FTKModFramework.Core.Data;
using GridEditor;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
    private static FTK_proficiencyTable Add(int id, string name, ProficiencyBase behavior)
    {
        FTK_proficiencyTable row = new FTK_proficiencyTable { m_ID = name, m_ProficiencyPrefab = behavior, m_CustomValue = -4 };
        Content.Proficiencies.Values.Add(id, row); Content.Proficiencies.Ids.Add(name, id); ContentRegistry.Custom.Add(id);
        return row;
    }

    private static void AddWeapon(int id, string name)
    {
        Content.Weapons.Values.Add(id, new FTK_weaponStats2 {
            m_ID = name, m_Prefab = new GameObject { CurrentWeapon = new Weapon() } });
        Content.Weapons.Ids.Add(name, id);
    }

    private static void Main()
    {
        Check(ProficiencyAttachmentPolicy.Resolve("weapon", null, false) == ProficiencyAttachmentMode.None &&
            ProficiencyAttachmentPolicy.Resolve("weapon", new string[0], false) == ProficiencyAttachmentMode.None,
            "omitted or empty nonreplacement weapon list leaves inherited actions alone");
        Check(ProficiencyAttachmentPolicy.Resolve("weapon", new string[0], true) == ProficiencyAttachmentMode.Replace,
            "explicit empty replacement clears inherited weapon actions");
        Check(ProficiencyAttachmentPolicy.Resolve("weapon", null, true) == ProficiencyAttachmentMode.Invalid &&
            ProficiencyAttachmentPolicy.Resolve("class", new string[0], false) == ProficiencyAttachmentMode.Invalid &&
            ProficiencyAttachmentPolicy.Resolve("item", new string[0], false) == ProficiencyAttachmentMode.Invalid,
            "missing replacement array and empty class or item grants are rejected");
        FTK_proficiencyTable armor = Add(100, "armor", new ProficiencyArmor { m_Category = ProficiencyBase.Category.Armor });
        FTK_proficiencyTable resist = Add(101, "resist", new ProficiencyResist { m_Category = ProficiencyBase.Category.Resist });
        FTK_proficiencyTable smite = Add(102, "smite", null);
        Add(103, "censure", null);
        Add(105, "censure.two", null);
        smite.m_DmgTypeOverride = FTK_weaponStats2.DamageType.magic; smite.m_DmgMultiplier = .25f;
        string[] tooltip = { "Single target", "Standard attack" };
        ProficiencyDescriptionRestoration.Apply(smite, tooltip);
        Check(tooltip[1] == "Standard attack", "absent explicit description preserves native fallback");
        Localization.Descriptions["smite"] = "Magic damage; sixfold against Censure resistance reduction.";
        ProficiencyDescriptionRestoration.Apply(smite, tooltip);
        Check(tooltip[0] == "Single target" && tooltip[1] == Localization.Descriptions["smite"], "nonperfect custom action preserves authored body and native target");
        smite.m_FullSlots = true; tooltip[1] = "Perfect only";
        ProficiencyDescriptionRestoration.Apply(smite, tooltip);
        Check(tooltip[1] == "Perfect only", "perfect-only native formatting preserved"); smite.m_FullSlots = false;
        smite.m_IgnoresArmor = true; tooltip[1] = "Ignores resistance";
        ProficiencyDescriptionRestoration.Apply(smite, tooltip);
        Check(tooltip[1] == "Ignores resistance", "native piercing description preserved"); smite.m_IgnoresArmor = false;
        smite.m_ProficiencyPrefab = resist.m_ProficiencyPrefab; tooltip[1] = "Status effect";
        ProficiencyDescriptionRestoration.Apply(smite, tooltip);
        Check(tooltip[1] == "Status effect", "native prefab description preserved"); smite.m_ProficiencyPrefab = null;
        ContentRegistry.Custom.Remove(102); tooltip[1] = "Vanilla body";
        ProficiencyDescriptionRestoration.Apply(smite, tooltip);
        Check(tooltip[1] == "Vanilla body", "unregistered or vanilla row unchanged even with description key"); ContentRegistry.Custom.Add(102);
        ProficiencyDescriptionRestoration.Apply(new FTK_proficiencyTable { m_ID = "smite" }, tooltip);
        Check(tooltip[1] == "Vanilla body", "copied row cannot claim registered description");
        Localization.Descriptions["smite"] = "";
        ProficiencyDescriptionRestoration.Apply(smite, tooltip);
        Check(tooltip[1] == "Vanilla body", "empty description preserves fallback");
        ProficiencyDescriptionRestoration.Apply(null, tooltip);
        ProficiencyDescriptionRestoration.Apply(smite, null);
        ProficiencyDescriptionRestoration.Apply(smite, new string[1]);
        Check(true, "missing or truncated native results safely ignored");
        Check(Content.SetRandomDebuffOutcomes(armor, armor, resist), "compatible native defense branches");
        Check(!Content.SetRandomDebuffOutcomes(armor, resist, resist), "duplicate branch rejected");
        resist.m_SlotOverride++;
        Check(!Content.SetRandomDebuffOutcomes(armor, armor, resist), "incompatible slot contract rejected atomically");
        resist.m_SlotOverride--;
        int[] registered;
        Check(CombatProficiencyRegistry.TryRandom(100, out registered) && registered[0] == 100 && registered[1] == 101, "failed overwrite preserves grant");
        resist.m_Harmless = true;
        Check(!Content.SetRandomDebuffOutcomes(armor, armor, resist), "harmless outcome rejected"); resist.m_Harmless = false;
        Check(!Content.SetRandomDebuffOutcomes(new FTK_proficiencyTable { m_ID = "armor" }, armor, resist), "forged registered row rejected");
        Check(Content.SetResistanceDebuffDamageBonus(smite, new[] { resist }, 6), "register explicit resistance bonus");
        Check(!Content.SetResistanceDebuffDamageBonus(smite, new[] { armor }, 6), "armor provenance cannot qualify");
        Check(!Content.SetResistanceDebuffDamageBonus(smite, new[] { resist }, float.NaN), "NaN rejected");
        Check(!Content.SetResistanceDebuffDamageBonus(smite, new[] { resist }, float.PositiveInfinity), "infinity rejected");
        Check(!Content.SetResistanceDebuffDamageBonus(smite, new[] { resist }, 17), "upper bound enforced");
        Check(!Content.SetResistanceDebuffDamageBonus(smite, new[] { resist, resist }, 6), "duplicate sources rejected");
        Check(!Content.SetResistanceDebuffDamageBonus(armor, new[] { resist }, 6), "mixed capability rejected");

        FTK_playerGameStart heroClass = new FTK_playerGameStart { m_ID = "paladin" };
        FTK_items trinket = new FTK_items { m_ID = "trinket", m_ObjectType = FTK_itembase.ObjectType.trinket };
        Content.Items.Values.Add(200, trinket); Content.Items.Ids.Add(trinket.m_ID, 200); ContentRegistry.Custom.Add(200);
        Check(Content.AttachItemProficiencies(trinket, "smite"), "registered trinket grants action");
        Check(Content.AttachItemProficiencies(trinket, "smite") && ItemProficiencyRegistry.Get(200).Length == 1, "item grants idempotent");
        Check(!Content.AttachItemProficiencies(new FTK_items(), "smite"), "null item identity rejects without throwing");
        Check(!Content.AttachItemProficiencies(new FTK_items { m_ID = "trinket", m_ObjectType = FTK_itembase.ObjectType.trinket }, "smite"), "copied item rejected");
        Check(!Content.AttachItemProficiencies(trinket, "armor", "missing") && ItemProficiencyRegistry.Get(200).Length == 1, "missing action rejects entire item grant");
        trinket.m_ObjectType = FTK_itembase.ObjectType.tool;
        Check(!Content.AttachItemProficiencies(trinket, "smite"), "nonequipment cannot grant combat action");
        trinket.m_ObjectType = FTK_itembase.ObjectType.trinket;
        CharacterOverworld wearer = new CharacterOverworld();
        wearer.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack).m_CountDictionary[(FTK_itembase.ID)200] = 1;
        wearer.m_PlayerInventory.Get(PlayerInventory.ContainerID.Belt).m_CountDictionary[(FTK_itembase.ID)200] = 1;
        Check(ItemProficiencyRuntime.EquippedActions(wearer).Length == 0, "backpack and belt copies grant nothing");
        wearer.m_PlayerInventory.Get(PlayerInventory.ContainerID.Trinket).m_CountDictionary[(FTK_itembase.ID)200] = 1;
        Check(ItemProficiencyRuntime.EquippedActions(wearer).Length == 1 && ItemProficiencyRuntime.EquippedActions(wearer)[0] == 102, "equipped trinket grants Smite");
        wearer.m_PlayerInventory.Get(PlayerInventory.ContainerID.Neck).m_CountDictionary[(FTK_itembase.ID)200] = 1;
        Check(ItemProficiencyRuntime.EquippedActions(wearer).Length == 1, "overlapping equipped grants deduplicate");
        wearer.m_PlayerInventory.Get(PlayerInventory.ContainerID.Trinket).m_CountDictionary[(FTK_itembase.ID)200] = 0;
        wearer.m_PlayerInventory.Get(PlayerInventory.ContainerID.Neck).m_CountDictionary.Clear();
        Check(ItemProficiencyRuntime.EquippedActions(wearer).Length == 0, "unequip immediately removes grant despite owned copies");
        Check(ItemProficiencyRuntime.Description(200) == "Actions while equipped:\nsmite", "item detail names equipped-only action");
        Check(ItemProficiencyRuntime.Description(999) == "", "unregistered item detail unchanged");
        Action restoreItems = ItemProficiencyRegistry.SuspendForReload();
        Check(ItemProficiencyRegistry.Count == 0, "reload clears item grants"); restoreItems();
        Check(ItemProficiencyRegistry.Get(200)[0] == 102, "rollback restores item grants");
        Content.Classes.Values.Add(14, heroClass); Content.Classes.Ids.Add("paladin", 14); ContentRegistry.Custom.Add(14);
        Check(Content.AttachClassProficiencies(heroClass, "smite"), "class grant");
        Check(Content.AttachClassProficiencies(heroClass, "smite"), "idempotent grant");
        Check(ClassProficiencyRegistry.Get(14).Length == 1, "no duplicate actions");
        Check(!Content.AttachClassProficiencies(heroClass, "armor", "missing"), "missing action rejects whole grant");
        Check(ClassProficiencyRegistry.Get(14).Length == 1, "no partial class grant");
        Check(!Content.AttachClassProficiencies(new FTK_playerGameStart { m_ID = "paladin" }, "smite"), "forged class rejected");
        int[] owned = ClassProficiencyRegistry.Get(14); owned[0] = 999;
        Check(ClassProficiencyRegistry.Get(14)[0] == 102, "caller cannot mutate registered list");
        AddWeapon(300, "hammer.one"); AddWeapon(301, "hammer.two"); AddWeapon(302, "blade");
        Check(Content.AttachClassWeaponProficiencies(heroClass, new[] { "hammer.one", "hammer.two" },
            "censure", "smite"), "conditional class action group accepts exact weapons");
        Check(Content.AttachClassWeaponProficiencies(heroClass, new[] { "hammer.two" }, "censure"),
            "overlapping conditional group is idempotent");
        Check(ClassProficiencyRegistry.Get(14, 300).Length == 2 &&
            ClassProficiencyRegistry.Get(14, 301).Length == 2 &&
            ClassProficiencyRegistry.Get(14, 302).Length == 1,
            "matching weapon adds actions once; swap removes conditional action");
        Check(ClassProficiencyRegistry.Allows(14, 300, 103) &&
            !ClassProficiencyRegistry.Allows(14, 302, 103) &&
            !ClassProficiencyRegistry.Allows(15, 300, 103) &&
            ClassProficiencyRegistry.Allows(14, 302, 102),
            "wrong class and weapon denied while unconditional grant persists");
        Check(!Content.AttachClassWeaponProficiencies(heroClass, new[] { "hammer.one", "missing" },
            "censure") && !ClassProficiencyRegistry.Allows(14, 302, 103),
            "missing weapon rejects entire conditional grant");
        Check(!Content.AttachClassWeaponProficiencies(heroClass, new[] { "blade" },
            "censure", "missing") && !ClassProficiencyRegistry.Allows(14, 302, 103),
            "missing action rejects entire conditional grant");
        Check(!Content.AttachClassWeaponProficiencies(new FTK_playerGameStart { m_ID = "paladin" },
            new[] { "blade" }, "censure"), "copied class cannot register weapon group");
        Check(!Content.AttachClassWeaponProficiencies(heroClass, new[] { "blade", "blade" },
            "censure") && !Content.AttachClassWeaponProficiencies(heroClass, new[] { "blade" },
            "censure", "censure"), "duplicate IDs reject whole group");
        FTK_proficiencyTable vanilla = new FTK_proficiencyTable { m_ID = "native-action" };
        Content.Proficiencies.Values.Add(104, vanilla); Content.Proficiencies.Ids.Add(vanilla.m_ID, 104);
        Check(!Content.AttachClassWeaponProficiencies(heroClass, new[] { "blade" },
            "native-action"), "native shared actions cannot become class-gated");
        int[] conditionalCopy = ClassProficiencyRegistry.Get(14, 300);
        conditionalCopy[0] = 999;
        Check(ClassProficiencyRegistry.Get(14, 300)[0] == 102,
            "conditional action list is not caller mutable");
        CharacterOverworld conditionalCow = new CharacterOverworld();
        conditionalCow.m_CharacterStats.m_CharacterClass = 14;
        conditionalCow.m_WeaponID = (FTK_itembase.ID)300;
        Check(ClassWeaponProficiencyEligibility.Allows(conditionalCow, 103),
            "eligible class with eligible weapon may use gated action");
        conditionalCow.m_WeaponID = (FTK_itembase.ID)302;
        Check(!ClassWeaponProficiencyEligibility.Allows(conditionalCow, 103) &&
            ClassWeaponProficiencyEligibility.Allows(conditionalCow, 102),
            "weapon swap removes gated action but preserves unconditional grant");
        conditionalCow.m_WeaponID = (FTK_itembase.ID)300;
        conditionalCow.m_CharacterStats.m_CharacterClass = 15;
        Check(!ClassWeaponProficiencyEligibility.Allows(conditionalCow, 103) &&
            ClassWeaponProficiencyEligibility.Allows(conditionalCow, 100),
            "wrong class denied; unrelated action unaffected");
        FTK_items duplicateGrant = new FTK_items { m_ID = "duplicate-grant", m_ObjectType = FTK_itembase.ObjectType.trinket };
        Content.Items.Values.Add(201, duplicateGrant); Content.Items.Ids.Add(duplicateGrant.m_ID, 201);
        ContentRegistry.Custom.Add(201);
        Check(Content.AttachItemProficiencies(duplicateGrant, "censure"), "item can independently register same synthetic action");
        conditionalCow.m_PlayerInventory.Get(PlayerInventory.ContainerID.Trinket).m_CountDictionary[(FTK_itembase.ID)201] = 1;
        Check(ClassWeaponProficiencyEligibility.Allows(conditionalCow, 103),
            "explicit item grant remains available despite a separate class-weapon grant");
        conditionalCow.m_PlayerInventory.Get(PlayerInventory.ContainerID.Trinket).m_CountDictionary[(FTK_itembase.ID)201] = 0;
        Content.Weapons.GetEntryByInt(302).m_Prefab.CurrentWeapon.m_ProficiencyEffects.Add(
            new ProficiencyID { m_ID = "censure" }, null);
        conditionalCow.m_WeaponID = (FTK_itembase.ID)302;
        Check(ClassWeaponProficiencyEligibility.Allows(conditionalCow, 103),
            "explicit weapon action remains available despite separate class-weapon grant");
        FTK_playerGameStart isolatedClass = new FTK_playerGameStart { m_ID = "isolated-guardian" };
        Content.Classes.Values.Add(15, isolatedClass); Content.Classes.Ids.Add(isolatedClass.m_ID, 15);
        ContentRegistry.Custom.Add(15);
        Check(Content.AttachClassWeaponProficiencies(isolatedClass, new[] { "hammer.one" },
            "censure", "smite") && Content.AttachClassWeaponProficiencies(isolatedClass,
            new[] { "hammer.two" }, "censure.two", "smite"),
            "isolated class registers separate one- and two-hand action groups");
        Check(ClassProficiencyRegistry.Get(15).Length == 0 &&
            ClassProficiencyRegistry.Get(15, 300).Length == 2 &&
            ClassProficiencyRegistry.Get(15, 301).Length == 2 &&
            ClassProficiencyRegistry.Get(15, 302).Length == 0,
            "class has no unconditional actions and no button with unrelated weapon");
        CharacterOverworld isolatedCow = new CharacterOverworld();
        isolatedCow.m_CharacterStats.m_CharacterClass = 15;
        isolatedCow.m_WeaponID = (FTK_itembase.ID)300;
        Check(ClassWeaponProficiencyEligibility.Allows(isolatedCow, 103) &&
            ClassWeaponProficiencyEligibility.Allows(isolatedCow, 102) &&
            !ClassWeaponProficiencyEligibility.Allows(isolatedCow, 105),
            "one-hand Censure and Smite allowed, two-hand Censure denied");
        isolatedCow.m_WeaponID = (FTK_itembase.ID)301;
        Check(ClassWeaponProficiencyEligibility.Allows(isolatedCow, 105) &&
            ClassWeaponProficiencyEligibility.Allows(isolatedCow, 102) &&
            !ClassWeaponProficiencyEligibility.Allows(isolatedCow, 103),
            "two-hand Censure and Smite allowed, one-hand Censure denied");
        isolatedCow.m_WeaponID = (FTK_itembase.ID)302;
        Check(!ClassWeaponProficiencyEligibility.Allows(isolatedCow, 105) &&
            !ClassWeaponProficiencyEligibility.Allows(isolatedCow, 102),
            "weapon swap removes both class actions");
        isolatedCow.m_WeaponID = (FTK_itembase.ID)300;
        isolatedCow.m_CharacterStats.m_CharacterClass = 16;
        Check(!ClassWeaponProficiencyEligibility.Allows(isolatedCow, 102) &&
            !ClassWeaponProficiencyEligibility.Allows(isolatedCow, 103),
            "other class cannot use actions on same eligible hammer");

        CharacterDummy actor = new CharacterDummy { FID = new FTKPlayerID { m_TurnIndex = 1, m_PhotonID = 8 }, m_CharacterOverworld = new CharacterOverworld() };
        EnemyDummy victim = new EnemyDummy();
        EncounterSession.Instance = new EncounterSession { m_Random = new FTKRandom { OriginalSeed = 42 }, m_EncounterIndex = 3, Enemy = victim };
        EncounterSession.Instance.m_FightOrderVisual.Add(new EncounterSessionMC.FightOrderEntry { m_EntryID = 17, m_Pid = actor.FID });
        AttackAttempt attack = new AttackAttempt { m_AttackingDummy = actor, m_DamagedDummy = victim, m_AttackProficiency = (FTK_proficiencyTable.ID)100, m_ProfSuccess = true };
        float multiplier = .75f;
        CombatProficiencyRuntime.Prepare(ref attack, false);
        int branch = (int)attack.m_AttackProficiency;
        Check(branch == 100 || branch == 101, "branch is native registered outcome");
        attack.m_AttackProficiency = (FTK_proficiencyTable.ID)100;
        CombatProficiencyRuntime.Prepare(ref attack, false);
        Check((int)attack.m_AttackProficiency == branch && multiplier == .75f, "repeated owner evaluation stable");
        attack.m_DamagedDummy = new EnemyDummy(); attack.m_AttackProficiency = (FTK_proficiencyTable.ID)100;
        CombatProficiencyRuntime.Prepare(ref attack, false);
        Check((int)attack.m_AttackProficiency == branch, "target switching cannot reroll branch");
        attack.m_AttackProficiency = (FTK_proficiencyTable.ID)100; attack.m_ProfSuccess = false;
        CombatProficiencyRuntime.Prepare(ref attack, false);
        Check(!attack.m_ProfSuccess, "failed native proficiency attempt remains failed");
        attack.m_AttackProficiency = (FTK_proficiencyTable.ID)100; attack.m_ProfSuccess = true;
        CombatProficiencyRuntime.Prepare(ref attack, true);
        Check((int)attack.m_AttackProficiency == 100 && multiplier == .75f, "consumable path unchanged");
        actor.m_CharacterOverworld.IsOwner = false; attack.m_AttackProficiency = (FTK_proficiencyTable.ID)100;
        CombatProficiencyRuntime.Prepare(ref attack, false);
        Check((int)attack.m_AttackProficiency == 100, "nonowner never transforms outcomes");
        actor.m_CharacterOverworld.IsOwner = true;
        EncounterSession.Instance.m_FightOrderVisual.Clear();
        CombatProficiencyRuntime.Prepare(ref attack, false);
        Check(!attack.m_ProfSuccess && multiplier == .75f, "missing action identity suppresses effect, preserves strike");

        CharacterDummy.ProficiencyRecord record = new CharacterDummy.ProficiencyRecord { m_Count = 1,
            m_Proficiency = new ProficiencyResist { m_ProficiencyID = (FTK_proficiencyTable.ID)101, m_CustomValue = -4 } };
        victim.m_SufferingProficiencies.Add(ProficiencyBase.Category.Resist, record);
        attack.m_DamagedDummy = victim; attack.m_AttackProficiency = (FTK_proficiencyTable.ID)102;
        multiplier = .25f;
        CombatProficiencyRuntime.ApplyDamageBonus(attack, false, ref multiplier);
        Check(multiplier == 1.5f && record.m_Count == 1, "explicit sixfold bonus, status not consumed");
        multiplier = .25f * 1.5f;
        CombatProficiencyRuntime.ApplyDamageBonus(attack, false, ref multiplier);
        Check(multiplier == 2.25f, "existing Reckoning stacks multiplicatively");
        EnemyDummy secondary = new EnemyDummy();
        attack.m_DamagedDummy = secondary;
        multiplier = .125f;
        CombatProficiencyRuntime.ApplyDamageBonus(attack, false, ref multiplier);
        Check(multiplier == .125f, "unmarked secondary victim does not inherit main victim bonus");
        secondary.m_SufferingProficiencies.Add(ProficiencyBase.Category.Resist, record);
        CombatProficiencyRuntime.ApplyDamageBonus(attack, false, ref multiplier);
        Check(multiplier == .75f, "marked secondary victim receives its own bonus");
        attack.m_DamagedDummy = victim;
        multiplier = .25f; actor.m_CharacterOverworld.IsOwner = false;
        CombatProficiencyRuntime.ApplyDamageBonus(attack, false, ref multiplier);
        Check(multiplier == .25f, "nonowner does not calculate conditional damage");
        actor.m_CharacterOverworld.IsOwner = true;
        CombatProficiencyRuntime.ApplyDamageBonus(attack, true, ref multiplier);
        Check(multiplier == .25f, "consumable damage remains unchanged");
        record.m_Proficiency.m_CustomValue = -.5f;
        Check(CombatProficiencyRuntime.DamageBonus((FTK_proficiencyTable.ID)102, victim) == 1, "fractional value truncated to zero does not qualify");
        resist.m_CustomValue = -.5f;
        Check(!Content.SetResistanceDebuffDamageBonus(smite, new[] { resist }, 6), "zero effective native reduction rejected at registration");
        resist.m_CustomValue = -4;
        record.m_Proficiency.m_CustomValue = -4;
        record.m_Proficiency.m_ProficiencyID = (FTK_proficiencyTable.ID)999;
        Check(CombatProficiencyRuntime.DamageBonus((FTK_proficiencyTable.ID)102, victim) == 1, "unrelated resistance source excluded");
        record.m_Proficiency.m_ProficiencyID = (FTK_proficiencyTable.ID)101; record.m_Count = 0;
        Check(CombatProficiencyRuntime.DamageBonus((FTK_proficiencyTable.ID)102, victim) == 1, "expired record excluded");
        record.m_Count = 1; record.m_Proficiency.m_CustomValue = 4;
        Check(CombatProficiencyRuntime.DamageBonus((FTK_proficiencyTable.ID)102, victim) == 1, "resistance buff excluded");
        record.m_Proficiency.m_CustomValue = -4;
        uiBattleButton button = new uiBattleButton { m_ButtonType = uiBattleButton.BattleButtonType.proficiency };
        uiBattleStanceButtons ui = new uiBattleStanceButtons { CombatCow = actor.m_CharacterOverworld };
        ui.m_Proficiencies.Add(new uiBattleStanceButtons.ProfValues { m_Prof = (FTK_proficiencyTable.ID)102, m_Button = button });
        CombatProficiencyRuntime.ShowPreview(ui, button);
        Check(ui.m_InfoPanel.m_DamageValue.text == "46", "UI calculates bonus before rounding nominal .25 damage");
        victim.Frozen = true; GameFlow.Instance.m_FrozenDmgPercent = 1.2f;
        CombatProficiencyRuntime.ShowPreview(ui, button);
        Check(ui.m_InfoPanel.m_DamageValue.text == "55", "frozen preview rounds scaled damage before frozen modifier");
        victim.Frozen = false;
        victim.m_SufferingProficiencies.Clear();
        Check(CombatProficiencyRuntime.DamageBonus((FTK_proficiencyTable.ID)102, victim) == 1, "native status removal removes bonus");

        int zero = 0;
        int[] seeds = { 0, 1, 2, 3, 42, -1, int.MaxValue, int.MinValue };
        int[] expectedBranches = { 1, 1, 1, 0, 1, 1, 1, 0 };
        for (int i = 0; i < seeds.Length; i++)
            Check(CombatProficiencyPolicy.DebuffBranch(seeds[i], 3, 17, 1, 8) == expectedBranches[i], "stable cross-runtime hash vector " + i);
        for (int seed = 0; seed < 10000; seed++) zero += CombatProficiencyPolicy.DebuffBranch(seed, 3, 17, 1, 8) == 0 ? 1 : 0;
        Check(zero > 4800 && zero < 5200, "hash branches balanced across synchronized seeds");
        Action restoreClasses = ClassProficiencyRegistry.SuspendForReload();
        Action restoreCombat = CombatProficiencyRegistry.SuspendForReload();
        Check(ClassProficiencyRegistry.Count == 0 && CombatProficiencyRegistry.Count == 0 &&
            !ClassProficiencyRegistry.IsWeaponAction(103), "reload detaches all registrations");
        restoreClasses(); restoreCombat();
        Check(ClassProficiencyRegistry.Get(14)[0] == 102 &&
            ClassProficiencyRegistry.Allows(14, 300, 103) && CombatProficiencyRegistry.Count == 2,
            "rollback restores unconditional and conditional registrations");
        Console.WriteLine("PASS CombatProficiencies: " + checks + " checks");
    }
}
