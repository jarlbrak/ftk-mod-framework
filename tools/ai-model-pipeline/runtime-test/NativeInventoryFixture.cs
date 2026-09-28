using System;
using System.Collections;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static CharacterOverworld NativeInventoryHero(int id)
    {
        RequireSinglePlayer(); RequireOutsideCombat();
        CharacterOverworld hero = null;
        if (FTKHub.Instance == null) throw new InvalidOperationException("Current native party required.");
        foreach (CharacterOverworld member in FTKHub.Instance.m_CharacterOverworlds)
        {
            if (member == null) continue;
            object queue = BlacksmithRead(member, "m_MoveRPCQueue");
            if (member.m_CharacterStats == null || member.m_IsMoving || member.m_CharacterStats.m_IsInCombat ||
                (bool)BlacksmithRead(queue, "m_MoveCoroutineRunning") || ((ICollection)BlacksmithRead(queue, "m_Queue")).Count != 0)
                throw new InvalidOperationException("Current party must be outside combat with no active or queued movement.");
            if (member.GetInstanceID() == id)
            {
                if (hero != null) throw new InvalidOperationException("Ambiguous exact hero identity.");
                hero = member;
            }
        }
        if (hero == null || !SceneOwner(hero) || !hero.gameObject.activeInHierarchy || !hero.IsOwner ||
            hero.m_CharacterStats.m_HealthCurrent <= 0 || hero.m_WaitForRespawn || hero.m_PlayerInventory == null ||
            hero.m_Avatar == null || hero.m_Avatar.m_CharacterOverworld != hero || hero.m_UIPlayMainHud == null || hero.m_UIPlayMainHud.m_Cow != hero)
            throw new InvalidOperationException("Exact owned living current-party hero, native avatar and reciprocal HUD required.");
        return hero;
    }
    JObject NativeInventoryFixture(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "heroInstanceId");
        int id = LeaseObservationPin.ExactId(command, "heroInstanceId", false);
        string action = Str(command, "action");
        if (action != "open" && action != "close" && action != "inspect") throw new ArgumentException("action must be open, close or inspect");
        CharacterOverworld hero = NativeInventoryHero(id);
        uiPlayerInventory inventory = uiPlayerInventory.Instance;
        FTKUI ui = FTKUI.Instance;
        if (inventory == null || !SceneOwner(inventory) || ui == null || ui.m_PlayerInventory != inventory || FTKInput.Instance == null)
            throw new InvalidOperationException("Existing native inventory and input singleton required.");
        bool showing = inventory.m_IsShowing && inventory.gameObject.activeInHierarchy && inventory.m_InventoryRoot.gameObject.activeInHierarchy;
        bool zoomed = (bool)BlacksmithRead(inventory, "m_ZoomIn");
        JObject preserved = BlacksmithAppearancePreserved(hero);
        JObject stats = BlacksmithAppearanceScalars(hero.m_CharacterStats);
        CharacterEventListener avatar = hero.m_Avatar;
        string route = null;
        if (action == "open")
        {
            if (inventory.m_IsShowing || inventory.gameObject.activeInHierarchy || ui.IsModal ||
                !hero.m_UIPlayMainHud.gameObject.activeInHierarchy || hero.m_UIPlayMainHud.m_OpenInventory == null ||
                !hero.m_UIPlayMainHud.m_OpenInventory.gameObject.activeInHierarchy || !hero.m_UIPlayMainHud.m_OpenInventory.IsInteractable())
                throw new InvalidOperationException("Native HUD inventory button must be visible and interactable with no existing modal or inventory.");
            // Same native HUD callback that opens inventory from the player's inventory control.
            hero.m_UIPlayMainHud.OnInventoryToggle();
            route = "uiPlayerMainHud.OnInventoryToggle";
            if (!inventory.m_IsShowing || inventory.m_InventoryOwner != hero || !inventory.m_InventoryRoot.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Native callback did not open the exact hero inventory.");
        }
        else if (action == "close")
        {
            FTKInputFocus focus = FTKInput.Instance.m_CurrentInputFocus;
            if (!showing || inventory.m_InventoryOwner != hero || !zoomed || focus == null ||
                !focus.transform.IsChildOf(inventory.transform))
                throw new InvalidOperationException("Exact visible settled hero inventory with native inventory input focus required.");
            inventory.OnClose();
            route = "uiPlayerInventory.OnClose";
            if (inventory.m_IsShowing || inventory.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Native close callback did not close inventory.");
        }
        if (hero.m_Avatar != avatar || !BlacksmithPreservedEquals(preserved, BlacksmithAppearancePreserved(hero)) ||
            !BlacksmithPreservedEquals(stats, BlacksmithAppearanceScalars(hero.m_CharacterStats)))
            throw new InvalidOperationException("Inventory callback changed pinned avatar, class, equipment, outfit, colors or scalar stats; no correction attempted.");
        return new JObject {{"ok", true}, {"operation", "native-inventory"}, {"action", action}, {"nativeRoute", route}, {"session", sessionId},
            {"heroInstanceId", id}, {"celInstanceId", hero.m_Avatar.GetInstanceID()}, {"inventoryInstanceId", inventory.GetInstanceID()},
            {"ownerInstanceId", inventory.m_InventoryOwner == null ? 0 : inventory.m_InventoryOwner.GetInstanceID()},
            {"showing", inventory.m_IsShowing}, {"visible", inventory.gameObject.activeInHierarchy && inventory.m_InventoryRoot.gameObject.activeInHierarchy},
            {"zoomSettled", (bool)BlacksmithRead(inventory, "m_ZoomIn")}, {"paperDollUpdatePending", inventory.m_IsUpdatePaperDoll},
            {"preservationVerified", true}, {"scope", "Native inventory UI only; no gear, stats, class, movement or save mutation requested."}};
    }
}
