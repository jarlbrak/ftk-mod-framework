using System;
using System.Collections.Generic;
using GridEditor;
using UnityEngine;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>
        /// Grant a native Blacksmith one equipment action. Weapon registrations replace inherited
        /// proficiency menus with the ordinary basic attack; the class action uses the native UI.
        /// </summary>
        public static bool SetBlacksmithEquipment(string modGuid, FTK_itembase item, BlacksmithEquipmentBonuses bonuses)
        {
            int id;
            if (string.IsNullOrEmpty(modGuid) || item == null || bonuses == null ||
                !ContentRegistry.TryGetSyntheticId(item.m_ID, out id, typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB))) return false;
            FTK_weaponStats2 weapon = item as FTK_weaponStats2;
            FTK_itembase registered = weapon != null ? (FTK_itembase)Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)id)
                : Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id);
            if (!object.ReferenceEquals(item, registered) || (weapon == null) != (bonuses.TemperArmor > 0)) return false;
            if (weapon != null && ((bonuses.SetHammerArmor > 0 && item.m_WeaponHands != 1) ||
                (bonuses.OverhandArmorPenalty > 0 && item.m_WeaponHands != 2))) return false;
            if (bonuses.TemperArmor > 0 && item.m_ObjectType != FTK_itembase.ObjectType.trinket) return false;
            if (BlacksmithRuntime.IsRegistered(id)) return BlacksmithRuntime.SameRegistration(id, bonuses);
            if (weapon != null && (weapon.m_Prefab == null || weapon.m_Prefab.GetComponentInChildren<Weapon>(true) == null)) return false;
            FTK_proficiencyTable.ID action = BlacksmithRuntime.EnsureAction(item.m_ID, bonuses);
            if (action == FTK_proficiencyTable.ID.None) return false;
            if (weapon != null)
            {
                GameObject copy = UnityEngine.Object.Instantiate(weapon.m_Prefab);
                HotReload.PaladinResourceState.Own(copy);
                try
                {
                    UnityEngine.Object.DontDestroyOnLoad(copy);
                    copy.name = weapon.m_Prefab.name + "_blacksmith";
                    copy.transform.position = new Vector3(0f, -100000f, 0f);
                    Weapon component = copy.GetComponentInChildren<Weapon>(true);
                    component.m_ProficiencyEffects = new Dictionary<ProficiencyID, HitEffect>();
                    component.SaveState();
                    weapon.m_NoRegularAttack = false;
                    weapon.m_Prefab = copy;
                }
                catch { HotReload.PaladinResourceState.DestroyTracked(copy); throw; }
            }
            BlacksmithRuntime.Register(id, bonuses, action);
            return true;
        }
    }
}
