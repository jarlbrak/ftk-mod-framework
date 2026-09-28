using System;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static FTK_playerGameStart ExactClassRow(string key, int id)
    {
        FTK_playerGameStartDB db = FTK_playerGameStartDB.GetDB();
        if (string.IsNullOrEmpty(key) || db == null || id < 0 || id >= db.GetCount())
            throw new InvalidOperationException("Exact installed class key and nonnegative ID required.");
        FTK_playerGameStart row = db.GetEntryByInt(id);
        if (row == null || row.m_ID != key || db.GetIntFromID(key) != id ||
            !ReferenceEquals(row, db.GetEntryByIndex(id)) || !ReferenceEquals(row, db.GetEntryByStringID(key)))
            throw new InvalidOperationException("Class key, numeric ID, index and row identity differ.");
        bool native = Enum.IsDefined(typeof(FTK_playerGameStart.ID), key);
        if (native)
        {
            if ((int)(FTK_playerGameStart.ID)Enum.Parse(typeof(FTK_playerGameStart.ID), key, false) != id)
                throw new InvalidOperationException("Native class enum identity differs.");
        }
        else
        {
            Type registry = CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.ContentRegistry", true);
            object[] args = { key, -1, new[] { typeof(FTK_playerGameStartDB) } };
            if (!(bool)registry.GetMethod("TryGetSyntheticId", Statics).Invoke(null, args) || (int)args[1] != id ||
                Enum.IsDefined(typeof(FTK_playerGameStart.ID), id))
                throw new InvalidOperationException("Custom class registration identity differs.");
        }
        return row;
    }

    static JArray ExactClassSkins(FTK_playerGameStart row)
    {
        JArray skins = new JArray();
        for (int i = 0; i < 7; i++)
        {
            FTK_skinset skin = row.m_Skinsets == null || i >= row.m_Skinsets.Length || row.m_Skinsets[i] == FTK_skinset.ID.None
                ? null : FTK_skinsetDB.Get(row.m_Skinsets[i]);
            skins.Add(new JObject { { "skinType", i }, { "name", ((FTK_playerGameStart.SkinType)i).ToString() },
                { "skinset", skin == null ? null : skin.m_ID }, { "supported", skin != null },
                { "nativeUnlocked", FTK_loreExtraUnlockDB.GetDB().IsSkinUnlocked((FTK_playerGameStart.SkinType)i) } });
        }
        return skins;
    }

    JObject ClassAppearanceRoster(JObject command)
    {
        CatalogKeys(command, "id", "session", "op");
        CatalogNoLinks(root);
        FTK_playerGameStartDB db = FTK_playerGameStartDB.GetDB();
        if (db == null || db.GetCount() < 1 || db.GetCount() > 512) throw new InvalidOperationException("Bounded installed class DB required.");
        string build = FTKVersion.Instance.m_BuildType.ToString();
        JArray rows = new JArray();
        for (int i = 0; i < db.GetCount(); i++)
        {
            FTK_playerGameStart row = db.GetEntryByIndex(i);
            if (row == null) throw new InvalidOperationException("Missing class row.");
            ExactClassRow(row.m_ID, i);
            bool dlc = row.m_DLC == FTK_dlc.ID.None || PublishPlatform.Instance.HasDLC(FTK_dlcDB.Get(row.m_DLC));
            bool usable = dlc && ((build == "Release" || build == "Experimental") ? row.m_Release
                : build == "PublicTest" ? row.m_PublicTest : build == "Development" && row.m_Development);
            rows.Add(new JObject { { "classKey", row.m_ID }, { "classId", i },
                { "native", Enum.IsDefined(typeof(FTK_playerGameStart.ID), i) }, { "hasDlc", dlc }, { "usable", usable },
                { "revealed", db.IsReveal((FTK_playerGameStart.ID)i, true) }, { "unlocked", db.IsUnlock((FTK_playerGameStart.ID)i, true) },
                { "defaultSkinType", (int)row.m_DefaultSkinType }, { "skins", ExactClassSkins(row) } });
        }
        return new JObject { { "ok", true }, { "readOnly", true }, { "session", sessionId }, { "classes", rows },
            { "scope", "Installed row support and native availability only. Renderer identity, equipped fit and motion require exact current-avatar observations." } };
    }
}
