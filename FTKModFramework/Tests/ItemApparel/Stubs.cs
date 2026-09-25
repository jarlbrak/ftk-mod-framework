using System;
using System.Collections.Generic;
using System.Linq;
using GridEditor;
using FTKModFramework.Core;

namespace UnityEngine
{
    public class Component
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : class { return gameObject.GetComponentsInChildren<T>(inactive); }
    }
    public class GameObject
    {
        public readonly Transform transform;
        private readonly List<Component> components = new List<Component>();
        public GameObject(string path) { transform = new Transform { gameObject = this, path = path }; }
        public T AddComponent<T>() where T : Component, new() { T c = new T { gameObject = this }; components.Add(c); return c; }
        public T GetComponent<T>() where T : class { return components.OfType<T>().FirstOrDefault(); }
        public T[] GetComponents<T>() where T : class { return components.OfType<T>().ToArray(); }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : class
        {
            var result = components.OfType<T>().ToList();
            if (transform is T) result.Add(transform as T);
            foreach (Transform child in transform.children) result.AddRange(child.gameObject.GetComponentsInChildren<T>(inactive));
            return result.ToArray();
        }
    }
    public class Transform : Component
    {
        public string path;
        public readonly List<Transform> children = new List<Transform>();
        public T[] GetComponents<T>() where T : class { return gameObject.GetComponents<T>(); }
    }
    public class Mesh { public string name; }
    public class Material { }
    public class Renderer : Component { public string name; }
    public class SkinnedMeshRenderer : Component { public Mesh sharedMesh; }
}
namespace GridEditor
{
    public class FTK_itembase
    {
        public enum ID { None, Armor, Boots, Unregistered }
        public enum ObjectSlot { armor, boot, other, equip }
        public enum ObjectType { armor, boots, other }
        public ObjectType m_ObjectType;
        public string m_ID;
        public ObjectSlot m_ObjectSlot;
    }
    public class FTK_characterModifier { public enum ID { None = -1 } }
    public class FTK_characterModifierDB { }
    public class FTK_weaponStats2 : FTK_itembase { }
    public class FTK_weaponStats2DB { public FTK_weaponStats2 GetEntry(FTK_itembase.ID id) { return null; } }
    public class FTK_items : FTK_itembase { public UnityEngine.GameObject m_WearablePrefab, m_WearablePrefabM; }
    public class FTK_skinset
    {
        public enum ID { Female, Male }
        public string m_ID;
        public UnityEngine.Component m_Armor;
        public UnityEngine.GameObject m_Boot;
    }
    public class FTK_playerGameStart { public string m_ID; public FTK_skinset.ID[] m_Skinsets; }
    public class FTK_itemsDB
    {
        public readonly Dictionary<int, FTK_items> Rows = new Dictionary<int, FTK_items>();
        public FTK_items GetEntry(FTK_itembase.ID id) { FTK_items item; Rows.TryGetValue((int)id, out item); return item; }
    }
    public class FTK_skinsetDB
    {
        public readonly Dictionary<int, FTK_skinset> Rows = new Dictionary<int, FTK_skinset>();
        public FTK_skinset GetEntry(FTK_skinset.ID id) { FTK_skinset skin; Rows.TryGetValue((int)id, out skin); return skin; }
        public int GetIntFromID(string id) { return (int)(FTK_skinset.ID)Enum.Parse(typeof(FTK_skinset.ID), id); }
    }
    public class FTK_playerGameStartDB
    {
        public readonly Dictionary<int, FTK_playerGameStart> Rows = new Dictionary<int, FTK_playerGameStart>();
        public FTK_playerGameStart GetEntryByInt(int id) { FTK_playerGameStart row; Rows.TryGetValue(id, out row); return row; }
    }
}
public class PlayerInventory
{
    public class Container { public FTK_itembase.ID Item; public FTK_itembase.ID GetOne() { return Item; } }
    public Container m_ContainerBody = new Container(), m_ContainerFoot = new Container();
}
public class CharacterOverworld
{
    public PlayerInventory m_PlayerInventory = new PlayerInventory();
    public FTK_playerGameStart GetDBEntry() { return null; }
    public FTK_skinset GetSkinset() { return null; }
}
public class uiQuickPlayerCreate
{
    public PlayerInventory m_PlayerInventory = new PlayerInventory();
    public FTK_playerGameStart GetClassDBEntry() { return null; }
    public FTK_skinset GetSkinset() { return null; }
}
public class CharacterEventListener : UnityEngine.Component { public UnityEngine.Transform m_Backpack; public CharacterOverworld m_CharacterOverworld; public uiQuickPlayerCreate m_uiQuickPlayerCreate; }
public class FTKHub { }
public class Weapon : UnityEngine.Component { public UnityEngine.GameObject m_OffHand; }
public class CharacterDummy { public CharacterOverworld m_CharacterOverworld; public CharacterEventListener m_EventListener; }
namespace HarmonyLib { public class HarmonyPatch : Attribute { public HarmonyPatch(Type t, string method, Type[] args) { } public HarmonyPatch(Type t, string method) { } } }
namespace FTKModFramework.Core
{
    internal static class PlayerRaceRegistry
    {
        internal static PlayerMeshPlan GetPlan(FTK_playerGameStart row, FTK_skinset skin) { return null; }
    }
    internal static class ExplicitMaterialOptions
    {
        internal static void PreservePalette(UnityEngine.Material material) { }
    }
    public static partial class Content
    {
        private static readonly Dictionary<Type, object> databases = new Dictionary<Type, object>();
        public static T Db<T>() where T : new() { object db; if (!databases.TryGetValue(typeof(T), out db)) databases[typeof(T)] = db = new T(); return (T)db; }
    }
    public static class ContentRegistry
    {
        public static readonly Dictionary<string, int> Bindings = new Dictionary<string, int>();
        public static bool TryGetSyntheticId(string key, out int id, params Type[] types) { return Bindings.TryGetValue(types[0].Name + key, out id); }
    }
    internal class EnemyMeshResources : UnityEngine.Component
    {
        public bool Owns(UnityEngine.Renderer r) { return false; }
        public bool RetainForDetachedRenderer(UnityEngine.Renderer r) { return false; }
        public bool Applied, VisualResourcesOnly, Valid = true;
        public int Retains;
        public bool EnsureRetained() { Retains++; return Valid; }
        public bool ValidLease() { return Valid; }
        public static void RetainHierarchy(UnityEngine.GameObject root) { }
    }
    internal static class Plugin { internal static readonly Logger Log = new Logger(); }
    internal class Logger { public void LogInfo(string s) { } public string LastWarning; public void LogWarning(string s) { LastWarning = s; } public void LogError(string s) { } }
    internal static class ExplicitEnemyMeshSwap
    {
        internal static int Calls;
        internal static UnityEngine.GameObject LastRoot;
        internal static bool ApplyToObject(string identity, UnityEngine.GameObject root, EnemyRendererMesh[] entries, object prepareMaterial = null, bool preserveAuthoredMainPalette = false) { Calls++; Last = entries; LastRoot = root; return true; }
        internal static EnemyRendererMesh[] Last;
        internal static bool ValidateAssignments(EnemyRendererMesh[] entries, out string error)
        {
            error = null;
            HashSet<string> paths = new HashSet<string>();
            return entries.Length > 0 && entries.All(e => e != null && !string.IsNullOrEmpty(e.RendererPath) && paths.Add(e.RendererPath));
        }
        internal static string RelativePath(UnityEngine.Transform root, UnityEngine.Transform child) { return child.path; }
        internal static bool Apply(string identity, CharacterEventListener avatar, EnemyRendererMesh[] entries, object prepareMaterial = null, bool preserveAuthoredMainPalette = false) { Calls++; Last = entries; return true; }
    }
}
