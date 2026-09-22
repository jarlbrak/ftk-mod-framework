using System;
using System.Collections.Generic;
using FTKModFramework.Core;
using FTKModFramework.Core.HotReload;
using GridEditor;
using UnityEngine;

internal static class Program
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void ExpectLookupFailure(TableManager db, string message)
    {
        bool rejected = false;
        try { DefinitionState.ValidateLookups(db); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "accepted " + message);
    }
    private static void Main()
    {
        TableManager db = TableManager.Instance;
        Type[] types = { typeof(FTK_playerGameStartDB), typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB), typeof(FTK_proficiencyTableDB), typeof(FTK_characterModifierDB) };
        foreach (Type type in types) db.Tables.Add(type, (GEDataArrayBase)Activator.CreateInstance(type));
        foreach (Type type in types) Check(db.Get(type).m_Dictionary == null, "fixture must start before lazy indexing");
        DefinitionState.CaptureBaseline(db);
        foreach (Type type in types) Check(db.Get(type).IndexBuilds == 1, "baseline must normalize every early-null index once");
        DefinitionState.ValidateLookups(db);
        object vanilla = db.Get(typeof(FTK_itemsDB)).m_Array;
        int expected = -1;
        for (int i = 0; i < 100; i++)
        {
            DefinitionState.RestoreBaseline(db);
            Check(object.ReferenceEquals(vanilla, db.Get(typeof(FTK_itemsDB)).m_Array), "vanilla array reference");
            Check(ContentRegistry.CustomIds.Count == 0 && IdAllocator.CustomIdCount == 0, "baseline maps empty");
            int id = IdAllocator.Allocate("paladin", "item");
            if (expected < 0) expected = id;
            Check(id == expected, "identity changed with history");
            ContentRegistry.CustomIds.Add(typeof(FTK_itemsDB), new Dictionary<string,int> { { "item", id } });
            object oldRow = new TestRow("item");
            object vanillaItem = ((object[])vanilla)[0];
            object[] activeRows = { vanillaItem, oldRow };
            object activeIndex = new Dictionary<int, object> { { 1, vanillaItem }, { id, oldRow } };
            db.Get(typeof(FTK_itemsDB)).m_Array = activeRows;
            db.Get(typeof(FTK_itemsDB)).m_Dictionary = activeIndex;
            ContentRegistry.RetainedRows.Add(typeof(FTK_itemsDB), oldRow);
            Localization.Names.Add("item", "old");
            DefinitionState.ValidateLookups(db);
            DefinitionState active = DefinitionState.Capture(db);
            DefinitionState.RestoreBaseline(db);
            DefinitionState.ValidateLookups(db);
            Check(object.ReferenceEquals(db.Get(typeof(FTK_itemsDB)).GetEntryByInt(1), vanillaItem), "hot disable restores usable vanilla lookup");
            ContentRegistry.CustomIds.Add(typeof(FTK_itemsDB), new Dictionary<string,int> { { "new", 7 } });
            Localization.Names.Add("item", "new");
            IdAllocator.Allocate("other", "collision-history");
            active.Restore(db);
            Check(object.ReferenceEquals(activeRows, db.Get(typeof(FTK_itemsDB)).m_Array), "rollback array ref");
            Check(object.ReferenceEquals(activeIndex, db.Get(typeof(FTK_itemsDB)).m_Dictionary), "rollback index ref");
            Check(ContentRegistry.CustomIds[typeof(FTK_itemsDB)].ContainsKey("item"), "nested old map changed");
            Check(object.ReferenceEquals(oldRow, ContentRegistry.RetainedRows[typeof(FTK_itemsDB)]), "ledger changed");
            Check(Localization.Names["item"] == "old" && IdAllocator.CustomIdCount == 1, "rollback localization/allocator");
        }
        DefinitionState.RestoreBaseline(db);
        // Validation must fail, rather than repair a malformed candidate before commit.
        GEDataArrayBase items = db.Get(typeof(FTK_itemsDB));
        object savedIndex = items.m_Dictionary;
        items.m_Dictionary = null;
        ExpectLookupFailure(db, "null candidate index");
        Check(items.m_Dictionary == null, "validation must not lazily repair a candidate");
        items.m_Dictionary = new Dictionary<int, object>();
        ExpectLookupFailure(db, "missing vanilla row");
        items.m_Dictionary = new Dictionary<int, object> { { 1, new TestRow("1") } };
        ExpectLookupFailure(db, "different row object under correct vanilla id");
        items.m_Dictionary = new Dictionary<int, object> { { 1, ((object[])vanilla)[0] }, { 2, new TestRow("removed") } };
        ExpectLookupFailure(db, "extra retired index entry");
        items.m_Dictionary = savedIndex;
        ContentRegistry.CustomIds.Add(typeof(FTK_itemsDB), new Dictionary<string, int> { { "orphan", 7 } });
        ExpectLookupFailure(db, "custom map without indexed row");
            ContentRegistry.CustomIds.Clear();
            DefinitionState.ValidateLookups(db);
            Console.WriteLine("PASS: early-null baseline indexes become complete; malformed candidates fail closed without repair");
        // These two real keys collide in the allocator's 29-bit band. Canonical rebuilding
        // must discard reverse-history probe reservations rather than append to them.
        int collisionFirst = IdAllocator.Allocate("paladin", "collision_118664");
        int collisionSecond = IdAllocator.Allocate("paladin", "collision_159300");
        Check(collisionSecond == collisionFirst + 1, "fixture must exercise linear probing");
        DefinitionState.RestoreBaseline(db);
        IdAllocator.Allocate("paladin", "collision_159300");
        IdAllocator.Allocate("paladin", "collision_118664");
        DefinitionState.RestoreBaseline(db);
        Check(IdAllocator.Allocate("paladin", "collision_118664") == collisionFirst &&
            IdAllocator.Allocate("paladin", "collision_159300") == collisionSecond, "collision history leaked into canonical rebuild");
        DefinitionState.RestoreBaseline(db);
        DefinitionState captured = DefinitionState.Capture(db);
        GEDataArrayBase original = db.Tables[typeof(FTK_itemsDB)];
        db.Tables[typeof(FTK_itemsDB)] = new FTK_itemsDB();
        bool denied = false; try { captured.Restore(db); } catch (InvalidOperationException) { denied = true; }
        Check(denied, "component replacement must reject before mutation");
        db.Tables[typeof(FTK_itemsDB)] = original;
        FTK_proficiencyTable vanillaProf = new FTK_proficiencyTable { m_ID = "1", m_ProficiencyPrefab = new ProficiencyBase() };
        db.Get<FTK_proficiencyTableDB>().m_Array = new[] { vanillaProf };
        ProficiencyBase old = new ProficiencyBase { m_ProficiencyData = vanillaProf };
        var oldTable = new Dictionary<FTK_proficiencyTable.ID, ProficiencyBase> { { (FTK_proficiencyTable.ID)1, old } };
        ProficiencyManager.Instance.m_ProficiencyTable = oldTable;
        GameCache.Cache.Items.Initialize();
        object oldCache = GameCache.Cache.Items.GetMap();
        HotReloadNativeCaches caches = HotReloadNativeCaches.Capture();
        db.Get<FTK_proficiencyTableDB>().m_Array = new[] { vanillaProf, new FTK_proficiencyTable { m_ID="2", m_ProficiencyPrefab=new ProficiencyBase { FailInit=true } } };
        bool failed = false; try { caches.Rebuild(); } catch (InvalidOperationException) { failed = true; }
        Check(failed, "malformed prefab Init must fail"); caches.Restore();
        Check(object.ReferenceEquals(oldTable, ProficiencyManager.Instance.m_ProficiencyTable), "proficiency dictionary exact rollback");
        Check(object.ReferenceEquals(oldCache, GameCache.Cache.Items.GetMap()), "item cache exact rollback");
        Check(UnityEngine.Object.Destroyed == 1 && !UnityEngine.Object.LastDestroyed.Active, "failed candidate host cleanup");
        Check(!old.gameObject.Destroyed, "vanilla behavior preserved");
        db.Get<FTK_proficiencyTableDB>().m_Array = new[] { vanillaProf };
        HotReloadNativeCaches successful = HotReloadNativeCaches.Capture();
        FTK_proficiencyTable customProf = new FTK_proficiencyTable { m_ID="2", m_ProficiencyPrefab=new ProficiencyBase() };
        db.Get<FTK_proficiencyTableDB>().m_Array = new[] { vanillaProf, customProf };
        successful.Rebuild(); successful.Validate(); successful.Retire();
        Check(successful.ProficiencyCount == 2 && !old.gameObject.Destroyed, "commit preserves vanilla");
        ProficiencyBase custom = ProficiencyManager.Instance.m_ProficiencyTable[(FTK_proficiencyTable.ID)2];
        HotReloadNativeCaches removed = HotReloadNativeCaches.Capture();
        db.Get<FTK_proficiencyTableDB>().m_Array = new[] { vanillaProf };
        removed.Rebuild(); removed.Validate(); removed.Retire();
        Check(custom.gameObject.Destroyed && !old.gameObject.Destroyed, "remove retires only replaced behavior");
        Console.WriteLine("PASS: forced hash collision canonical rebuild, 100 baseline/rollback histories, exact references, replacement rejection, malformed prefab cleanup, cache commit and retirement");
    }
}
namespace GridEditor {
 public class GEDataArrayBase {
  public object m_Array = new object[] { new TestRow("1") }; public object m_Dictionary; public int IndexBuilds;
  public void CheckAndMakeIndex() {
   if (m_Dictionary != null) return;
   IndexBuilds++; var index = new Dictionary<int,object>();
   foreach(object row in (Array)Reflect.GetField(this,"m_Array")) index.Add(GetIntFromID((string)Reflect.GetField(row,"m_ID")),row);
   m_Dictionary=index;
  }
  public int GetIntFromID(string key) { Dictionary<string,int> map; int id;
   if(ContentRegistry.CustomIds.TryGetValue(GetType(),out map)&&map.TryGetValue(key,out id))return id;
   return int.TryParse(key,out id)?id:-1;
  }
  public object GetEntryByInt(int id) { object row; var index=m_Dictionary as Dictionary<int,object>; return index!=null&&index.TryGetValue(id,out row)?row:null; }
 }
 public class TableManager { public static readonly TableManager Instance=new TableManager(); public readonly Dictionary<Type,GEDataArrayBase> Tables=new Dictionary<Type,GEDataArrayBase>(); public GEDataArrayBase Get(Type t){return Tables[t];} public T Get<T>() where T:GEDataArrayBase {return (T)Get(typeof(T));} }
}
public class TestRow { public string m_ID; public TestRow(string id){m_ID=id;} }
public class FTK_playerGameStartDB:GEDataArrayBase{}
public class FTK_itemsDB:GEDataArrayBase{}
public class FTK_weaponStats2DB:GEDataArrayBase{}
public class FTK_characterModifierDB:GEDataArrayBase{}
public class FTK_proficiencyTableDB:GEDataArrayBase {public new FTK_proficiencyTable[] m_Array=new[] { new FTK_proficiencyTable { m_ID="1" } };}
public class FTK_proficiencyTable {public enum ID{None} public string m_ID;public ProficiencyBase m_ProficiencyPrefab;public static ID GetEnum(string s){return (ID)int.Parse(s);}}
public class ProficiencyBase { public FTK_proficiencyTable m_ProficiencyData;public bool FailInit;public GameObject gameObject=new GameObject();public Transform transform=new Transform();public void Init(FTK_proficiencyTable.ID id){if(FailInit)throw new InvalidOperationException("injected Init fault");foreach(var row in TableManager.Instance.Get<FTK_proficiencyTableDB>().m_Array)if(FTK_proficiencyTable.GetEnum(row.m_ID)==id)m_ProficiencyData=row;}}
public class ProficiencyManager {public static ProficiencyManager Instance=new ProficiencyManager();public Dictionary<FTK_proficiencyTable.ID,ProficiencyBase> m_ProficiencyTable;public Transform transform=new Transform();}
namespace UnityEngine {
 public class Object { public static int Destroyed;public static GameObject LastDestroyed;public static ProficiencyBase Instantiate(ProficiencyBase p){return new ProficiencyBase{FailInit=p.FailInit};}public static void Destroy(GameObject g){Destroyed++;LastDestroyed=g;g.Destroyed=true;} }
 public class GameObject {public bool Active=true,Destroyed;public void SetActive(bool a){Active=a;}}
 public class Transform {public void SetParent(Transform t){}}
}
namespace GameCache {public static class Cache {public static class Items {
 private static object _itemPrefabs,_itemIcons,_itemIconsNonClickable,_itemsByCategory;private static bool _isInitialized;
 public static void Initialize(){_itemPrefabs=new object();_itemIcons=new object();_itemIconsNonClickable=new object();_itemsByCategory=new Dictionary<int,List<object>>();_isInitialized=true;}
 public static object GetMap(){return _itemPrefabs;}
}}}
namespace FTKModFramework.Core {
 public static class ContentRegistry {public static Dictionary<Type,Dictionary<string,int>> CustomIds=new Dictionary<Type,Dictionary<string,int>>();public static Dictionary<Type,object> RetainedRows=new Dictionary<Type,object>();public static object _batchDirty;}
 public static class Localization {public static Dictionary<string,string> Names=new Dictionary<string,string>(),RealmDisplayKeys=new Dictionary<string,string>(),ClassFlavors=new Dictionary<string,string>(),ProficiencyDescriptions=new Dictionary<string,string>(),EnemyDescriptions=new Dictionary<string,string>();}
}
namespace FTKModFramework.Core.Data {internal static class ModRegistry {internal sealed class Snapshot {internal void Restore(){}}internal static Snapshot Capture(){return new Snapshot();}}}

namespace FTKModFramework.Core.HotReload {internal static class PaladinResourceState {internal static void DestroyTracked(UnityEngine.GameObject host){UnityEngine.Object.Destroy(host);}}}
