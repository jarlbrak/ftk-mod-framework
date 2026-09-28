using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type target, string method) { }
    }
}

namespace GridEditor
{
    public sealed class FTK_playerGameStart
    {
        public enum ID { blacksmith = 10, hunter = 11 }
        public string m_ID;
        public string DisplayName;
        public string GetDisplayName() { return DisplayName; }
    }

    public sealed class FTK_characterModifier
    {
        public enum ID { item_vitality = 8101, item_armor = 8102, item_extra = 8103 }
    }

    public sealed class FTK_itembase
    {
        public enum ID { item_vitality = 8101, custom = 9101 }
        public string m_ID;
    }

    public sealed class FTK_itemsDB { }
    public sealed class FTK_weaponStats2DB { }

    public sealed class CharacterStats
    {
        public List<FTK_characterModifier.ID> m_CharacterMods = new List<FTK_characterModifier.ID>();
        public FTK_playerGameStart.ID m_CharacterClass;
        public float m_ModVitality;
        private float _ModQuickness = 0f;
        public int m_ModDefensePhysical;
        public int m_ModDefenseMagic;
        public int m_ReflectDamage;
        public float Quickness { get { return _ModQuickness; } }
    }

    public sealed class ActiveObject { public bool activeSelf = true; }
    public sealed class PanelObject { public ActiveObject gameObject = new ActiveObject(); }
    public sealed class TextField { public string text; }
    public sealed class uiItemDetail
    {
        public PanelObject m_ArmorPanel = new PanelObject();
        public TextField m_EquippableProperties = new TextField();
    }
    public sealed class uiWeaponDetail { public TextField m_WeaponStatDisplay = new TextField(); }
}

namespace FTKModFramework.Core
{
    internal static class Reflect
    {
        internal static object GetField(object instance, string name)
        {
            if (instance == null) return null;
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance);
        }
        internal static void SetField(object instance, string name, object value)
        {
            if (instance == null) return;
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) field.SetValue(instance, value);
        }
    }

    internal static class Plugin
    {
        internal static readonly Logger Log = new Logger();
        internal sealed class Logger { internal void LogWarning(string value) { } }
    }

    internal static class ContentRegistry
    {
        internal static bool TryGetSyntheticId(string name, out int id, params Type[] tables)
        {
            id = name == "affinity_item" ? 8101 : name == "custom" ? 9101 : -1;
            return id >= 0;
        }
    }
}
