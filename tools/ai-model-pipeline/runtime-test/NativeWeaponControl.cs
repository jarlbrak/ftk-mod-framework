using System;
using System.Collections.Generic;
using GridEditor;
using Newtonsoft.Json.Linq;

// One inspection authorizes one submission. Failed native changes retain the shared uncertainty latch.
internal sealed class NativeWeaponControlClaim
{
    string token;
    public string Inspect() { token = Guid.NewGuid().ToString("N"); return token; }
    public void Consume(string supplied, bool unchanged)
    {
        if (string.IsNullOrEmpty(token) || token != supplied || !unchanged)
            throw new InvalidOperationException("Native control inspection is absent, consumed or stale.");
        token = null;
    }
}

public sealed partial class RuntimeModelTest
{
    readonly NativeWeaponControlClaim nativeWeaponControlClaim = new NativeWeaponControlClaim();
    JObject nativeWeaponControlPins;

    static JObject NativeControlPartySnapshot()
    {
        JObject result = new JObject();
        foreach (CharacterOverworld hero in FTKHub.Instance.m_CharacterOverworlds)
            if (hero != null)
                result[hero.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                    new JObject { { "preserved", BlacksmithAppearancePreserved(hero) },
                        { "stats", BlacksmithAppearanceScalars(hero.m_CharacterStats) },
                        { "skin", (int)hero.m_SkinType } };
        return result;
    }

    JObject NativeWeaponControl(JObject command)
    {
        if (packageFitUncertain) throw new InvalidOperationException("Uncertain session; restart before native control setup.");
        try
        {
            JObject result = NativeWeaponControlInner(command);
            if (packageFitUncertain && packageFitMutationVerified && (bool)result["ok"]) packageFitUncertain = false;
            return result;
        }
        catch (Exception error)
        {
            if (!packageFitUncertain) throw;
            return PackageFitFailureReceipt(error);
        }
    }

    JObject NativeWeaponControlInner(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "heroInstanceId", "classKey", "classId", "configSha256", "item", "token");
        string action = Str(command, "action");
        if (action != "inspect" && action != "submit") throw new ArgumentException("inspect or submit required.");
        if (Str(command, "item") != "bluntWarHammer") throw new ArgumentException("Only the exact native bluntWarHammer control is supported.");
        RequirePackageFitIsolation();
        PackageFitItems(Str(command, "configSha256"));
        if (blacksmithAppearance != null || previewRace != null) throw new InvalidOperationException("Restore every appearance fixture first.");
        CharacterOverworld hero = ExactPackageFitHero(LeaseObservationPin.ExactId(command, "heroInstanceId", false));
        if (!ReferenceEquals(hero.GetDBEntry(), ExactClassRow(Str(command, "classKey"), PreviewRaceIndex(command, "classId"))))
            throw new InvalidOperationException("Requested class does not match current owned hero.");
        if (uiPlayerInventory.Instance != null && uiPlayerInventory.Instance.m_IsShowing)
            throw new InvalidOperationException("Close inventory before native control setup.");
        FTK_itembase.ID id = FTK_itembase.ID.bluntWarHammer;
        FTK_itembase row = FTK_itembase.GetItemBase(id);
        if (row == null || row.m_ID != "bluntWarHammer" || !row.m_Equippable ||
            !ReferenceEquals(row, FTK_weaponStats2DB.GetDB().GetEntryByInt((int)id)) ||
            row.m_ObjectType != FTK_itembase.ObjectType.weapon || row.m_ObjectSlot != FTK_itembase.ObjectSlot.twoHands)
            throw new InvalidOperationException("Exact installed native two-hand weapon row required.");
        var entry = new BlacksmithGearEntry { stringId = row.m_ID, itemId = id, numericId = (int)id,
            row = row, container = PlayerInventory.ContainerID.RightHand };
        ItemContainer backpack = hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack);
        ItemContainer right = hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.RightHand);
        ItemContainer left = hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.LeftHand);
        if (backpack == null || right == null || left == null) throw new InvalidOperationException("Native equipment containers required.");
        int owned = OwnedAcrossEquipment(hero, id);
        if (owned < 0 || owned > 1 || (owned == 1 && ExactCount(backpack, id) + ExactCount(right, id) != 1) ||
            (owned == 0 && !backpack.CanAdd(id))) throw new InvalidOperationException("Unambiguous zero/one control ownership and capacity required.");
        foreach (ItemContainer hand in new[] { right, left })
        {
            int occupied = 0;
            foreach (KeyValuePair<FTK_itembase.ID, int> old in hand.m_CountDictionary)
                if (old.Value != 0 && (old.Value != 1 || ++occupied > 1 || !backpack.CanAdd(old.Key)))
                    throw new InvalidOperationException("Displaced hand equipment must fit in Backpack.");
        }
        JObject party = NativeControlPartySnapshot();
        JObject pins = new JObject { { "session", sessionId }, { "configSha256", packageGearConfigHash },
            { "heroInstanceId", hero.GetInstanceID() }, { "classKey", hero.GetDBEntry().m_ID },
            { "classId", (int)hero.m_CharacterStats.m_CharacterClass }, { "item", row.m_ID },
            { "nativeItemId", (int)id }, { "party", party } };
        if (action == "inspect")
        {
            nativeWeaponControlPins = pins;
            return new JObject { { "ok", true }, { "readOnly", true }, { "token", nativeWeaponControlClaim.Inspect() },
                { "pins", pins }, { "ownedCount", owned }, { "equipment", EquipmentView(hero) },
                { "scope", "Read-only native weapon control preflight; submit once with unchanged pins. No natural acquisition claim." } };
        }
        nativeWeaponControlClaim.Consume(Str(command, "token"), nativeWeaponControlPins != null && BlacksmithGrantPreservedEquals(nativeWeaponControlPins, pins));
        nativeWeaponControlPins = null;
        JObject before = BlacksmithAppearancePreserved(hero);
        JObject statsBefore = BlacksmithAppearanceScalars(hero.m_CharacterStats);
        CharacterStats statsOwner = hero.m_CharacterStats;
        PlayerInventory inventoryOwner = hero.m_PlayerInventory;
        FTK_playerGameStart.SkinType skin = hero.m_SkinType;
        var totals = BlacksmithInventoryTotals(hero);
        if (owned == 0) totals.Add(id, 1);
        PackageFitBeginMutation(hero, "native-weapon-control");
        if (owned == 0 && (!backpack.Add(id) || ExactCount(backpack, id) != 1))
            throw new InvalidOperationException("Native control Add failed; fresh session required.");
        PreflightOwnedBlacksmithItem(hero, entry);
        EquipOwnedBlacksmithItem(hero, entry, CreateEquipRpcTrace(command, 0));
        RequireBlacksmithInventoryPreserved(totals, BlacksmithInventoryTotals(hero));
        foreach (KeyValuePair<FTK_itembase.ID, int> item in left.m_CountDictionary)
            if (item.Value != 0) throw new InvalidOperationException("Native two-hand control left occupied offhand.");
        JObject after = BlacksmithAppearancePreserved(hero);
        JObject statsAfter = BlacksmithAppearanceScalars(hero.m_CharacterStats);
        foreach (JProperty slot in ((JObject)before["inventory"]).Properties())
            if (slot.Name != "Backpack" && slot.Name != "RightHand" && slot.Name != "LeftHand" &&
                !BlacksmithGrantPreservedEquals(slot.Value, after["inventory"][slot.Name]))
                throw new InvalidOperationException("Native control changed an unrelated equipment slot.");
        JObject expectedState = (JObject)before.DeepClone(), actualState = (JObject)after.DeepClone();
        expectedState.Remove("inventory"); actualState.Remove("inventory");
        bool preserved = ReferenceEquals(statsOwner, hero.m_CharacterStats) && ReferenceEquals(inventoryOwner, hero.m_PlayerInventory) && skin == hero.m_SkinType &&
            BlacksmithGrantPreservedEquals(expectedState, actualState) &&
            BlacksmithGrantPreservedEquals(PackageFitStableStats(statsBefore), PackageFitStableStats(statsAfter));
        JObject partyAfter = NativeControlPartySnapshot();
        foreach (JProperty member in party.Properties())
            if (member.Name != hero.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                !BlacksmithGrantPreservedEquals(member.Value, partyAfter[member.Name])) preserved = false;
        packageFitMutationDiagnostics = new JObject { { "preservationVerified", preserved },
            { "stateDifferences", PackageFitDifferences(expectedState, actualState) },
            { "checkedStatDifferences", PackageFitDifferences(PackageFitStableStats(statsBefore), PackageFitStableStats(statsAfter)) },
            { "partyBefore", party }, { "partyAfter", partyAfter } };
        if (!preserved) throw new InvalidOperationException("Native control preservation failed; session remains uncertain.");
        packageFitMutationVerified = true;
        return new JObject { { "ok", true }, { "heroInstanceId", hero.GetInstanceID() }, { "item", row.m_ID },
            { "added", owned == 0 }, { "preservationVerified", true }, { "before", before }, { "after", after },
            { "equipment", EquipmentView(hero) }, { "diagnostics", packageFitMutationDiagnostics },
            { "scope", "Single native War Hammer control: native Backpack.Add if missing and native ForceEquip. Wait for avatar settling. No package registry, database, save, input or camera modification; no art acceptance implied." } };
    }
}
