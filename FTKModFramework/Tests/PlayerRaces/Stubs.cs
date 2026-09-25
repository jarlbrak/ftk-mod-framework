using System;
using System.Collections.Generic;
namespace GridEditor
{
    public class FTK_skinset { public enum ID { None = -1, Native = 1 } public string m_ID; }
    public class FTK_playerGameStart
    {
        public enum SkinType { None = -1, Female, Male, Undead, Cat }
        public string m_ID;
        public SkinType m_DefaultSkinType;
        public FTK_skinset.ID[] m_Skinsets;
    }
    public class FTK_playerGameStartDB
    {
        public readonly Dictionary<string, FTK_playerGameStart> Rows = new Dictionary<string, FTK_playerGameStart>();
        public FTK_playerGameStart GetEntryByStringID(string id) { FTK_playerGameStart row; Rows.TryGetValue(id, out row); return row; }
    }
    public class FTK_skinsetDB
    {
        public readonly Dictionary<int, FTK_skinset> Rows = new Dictionary<int, FTK_skinset>();
        public FTK_skinset GetEntry(FTK_skinset.ID id) { FTK_skinset row; Rows.TryGetValue((int)id, out row); return row; }
        public int GetIntFromID(string id) { foreach (var pair in Rows) if (pair.Value.m_ID == id) return pair.Key; return -1; }
    }
    public class FTK_loreExtraUnlockDB
    {
        public static readonly FTK_loreExtraUnlockDB Instance = new FTK_loreExtraUnlockDB();
        public readonly HashSet<int> Locked = new HashSet<int>();
        public static FTK_loreExtraUnlockDB GetDB() { return Instance; }
        public bool IsSkinUnlocked(FTK_playerGameStart.SkinType id) { return !Locked.Contains((int)id); }
    }
}
public class uiQuickPlayerCreate
{
    public GridEditor.FTK_playerGameStart.SkinType m_SkinType;
    public GridEditor.FTK_playerGameStart Row;
    public GridEditor.FTK_playerGameStart GetClassDBEntry() { return Row; }
}
public class CharacterOverworld
{
    public GridEditor.FTK_playerGameStart.SkinType m_SkinType;
    public GridEditor.FTK_playerGameStart Row;
    public GridEditor.FTK_playerGameStart GetDBEntry() { return Row; }
}
namespace FTKModFramework.Core
{
    public class PlayerRendererMesh { }
    public class PlayerApparelMesh { }
    internal class PlayerMeshPlan
    {
        internal static bool TryCreate(PlayerRendererMesh[] body, PlayerApparelMesh[] apparel, out PlayerMeshPlan plan, out string error)
        { error = null; plan = body != null && body.Length > 0 && apparel != null ? new PlayerMeshPlan() : null; return plan != null; }
    }
    internal static class Content
    {
        internal static readonly GridEditor.FTK_skinsetDB Skins = new GridEditor.FTK_skinsetDB();
        internal static readonly GridEditor.FTK_playerGameStartDB Classes = new GridEditor.FTK_playerGameStartDB();
        public static T Db<T>() { return (T)(typeof(T) == typeof(GridEditor.FTK_skinsetDB) ? (object)Skins : Classes); }
    }
    internal static class ContentRegistry
    {
        internal static readonly Dictionary<string, int> Ids = new Dictionary<string, int>();
        internal static bool TryGetSyntheticId(string key, out int id, params Type[] types) { return Ids.TryGetValue(key, out id); }
        internal static object Register(GridEditor.FTK_skinsetDB db, string guid, string key, object donor)
        { int id = IdAllocator.Allocate(guid, "FTK_skinsetDB/" + key); Ids.Add(key, id); var row = new GridEditor.FTK_skinset { m_ID = key }; db.Rows.Add(id, row); return row; }
    }
    internal static class Plugin { internal static readonly Logger Log = new Logger(); }
    internal class Logger { internal void LogWarning(string text) { } }
}
