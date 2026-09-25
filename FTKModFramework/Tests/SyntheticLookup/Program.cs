using System;
using System.Collections.Generic;
using System.Reflection;
using FTKModFramework.Core;
using GridEditor;

internal static class Program
{
    private sealed class FirstTable { }
    private sealed class SecondTable { }
    private sealed class AbsentTable { }
    private static int _checks;
    private static int _sink;

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _checks++;
    }

    private static void Main()
    {
        CheckCanonicalItemNames();
        ContentRegistry.CustomIds.Add(typeof(FirstTable), new Dictionary<string, int>
        {
            { "first", 101 }, { "shared", 102 }
        });
        ContentRegistry.CustomIds.Add(typeof(SecondTable), new Dictionary<string, int>
        {
            { "second", 201 }, { "shared", 202 }
        });

        string[] keys = { "first", "second", "shared", "vanilla", "FIRST", "", "101" };
        Type[] types = { typeof(FirstTable), typeof(SecondTable), typeof(AbsentTable) };
        foreach (string key in keys)
        foreach (Type first in types)
        {
            int expectedId, actualId;
            bool expected = ContentRegistry.TryGetSyntheticId(key, out expectedId, new[] { first });
            bool actual = ContentRegistry.TryGetSyntheticId(key, out actualId, first);
            Check(expected == actual && expectedId == actualId, "single table parity: " + key);
            foreach (Type second in types)
            {
                expected = ContentRegistry.TryGetSyntheticId(key, out expectedId, new[] { first, second });
                actual = ContentRegistry.TryGetSyntheticId(key, out actualId, first, second);
                Check(expected == actual && expectedId == actualId, "ordered two-table parity: " + key);
            }
        }

        int id;
        Check(ContentRegistry.TryGetSyntheticId("shared", out id, typeof(FirstTable), typeof(SecondTable)) && id == 102,
            "first registered table wins collisions");
        Check(!ContentRegistry.TryGetSyntheticId("vanilla", out id, typeof(FirstTable), typeof(SecondTable)) && id == -1,
            "vanilla fallthrough uses -1 sentinel");
        Check(!ContentRegistry.TryGetSyntheticId("first", out id, new Type[0]) && id == -1,
            "public empty-array contract preserved");
        Check(!ContentRegistry.TryGetSyntheticId(null, out id, typeof(AbsentTable)) && id == -1,
            "absent table does not inspect null key");
        Check(ContentRegistry.TryGetSyntheticId("first", out id, typeof(FirstTable), (Type)null) && id == 101,
            "successful first table does not inspect second type");
        RejectNullKey(delegate { ContentRegistry.TryGetSyntheticId(null, out id, typeof(FirstTable)); });
        RejectNullKey(delegate { ContentRegistry.TryGetSyntheticId(null, out id, typeof(AbsentTable), typeof(FirstTable)); });

        // Warm both call paths before counting per-thread allocations. This is game-free CLR
        // evidence of removed arrays, not a Unity frame-time or native Mono timing benchmark.
        RunLookups(false, 10000);
        RunLookups(true, 10000);
        const int iterations = 100000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        RunLookups(false, iterations);
        long fixedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        RunLookups(true, iterations);
        long arrayBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(fixedBytes == 0, "fixed-arity hit and miss lookups allocate zero bytes");
        Check(arrayBytes > fixedBytes, "legacy params expansion allocates arrays");
        Console.WriteLine(_checks + " synthetic lookup checks passed");
        Console.WriteLine("Allocation comparison (" + iterations + " iterations, four lookups each): fixed=" +
            fixedBytes + " bytes, params=" + arrayBytes + " bytes; checksum=" + _sink);
    }

    private delegate bool ItemPrefix(string name, ref FTK_itembase.ID result);

    private static void CheckCanonicalItemNames()
    {
        ItemPrefix prefix = (ItemPrefix)Delegate.CreateDelegate(typeof(ItemPrefix),
            typeof(ItemGetEnum_Patch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static));
        foreach (string name in Enum.GetNames(typeof(FTK_itembase.ID)))
        {
            FTK_itembase.ID expected = (FTK_itembase.ID)Enum.Parse(typeof(FTK_itembase.ID), name, true);
            FTK_itembase.ID actual = (FTK_itembase.ID)999;
            Check(!prefix(name, ref actual) && actual == expected, "canonical native parser parity: " + name);
        }
        string[] fallback = { null, "", "first", "FIRST", "1", "-1", "  First", "First ", "First, Second", "unknown" };
        foreach (string name in fallback)
        {
            FTK_itembase.ID result = (FTK_itembase.ID)999;
            Check(prefix(name, ref result) && (int)result == 999, "noncanonical input delegates without mutation: " + name);
        }
        int invalid;
        Check(!CanonicalEnumLookup<int>.TryGetValue("anything", out invalid), "incompatible type disables optional cache");

        var items = new Dictionary<string, int> { { "First", 9001 } };
        var weapons = new Dictionary<string, int> { { "First", 9002 }, { "custom", 9003 } };
        ContentRegistry.CustomIds.Add(typeof(FTK_itemsDB), items);
        ContentRegistry.CustomIds.Add(typeof(FTK_weaponStats2DB), weapons);
        FTK_itembase.ID customResult = FTK_itembase.ID.None;
        Check(!prefix("First", ref customResult) && (int)customResult == 9001, "custom items precede warmed native cache and weapon table");
        items["First"] = 9004;
        Check(!prefix("First", ref customResult) && (int)customResult == 9004, "custom updates remain visible");
        items.Remove("First");
        Check(!prefix("First", ref customResult) && (int)customResult == 9002, "custom weapon precedes warmed native cache");
        Check(!prefix("custom", ref customResult) && (int)customResult == 9003, "noncanonical custom name resolves");
        weapons.Remove("First");
        Check(!prefix("First", ref customResult) && customResult == FTK_itembase.ID.First, "removed custom name returns to native value");
        ContentRegistry.CustomIds.Remove(typeof(FTK_itemsDB));
        ContentRegistry.CustomIds.Remove(typeof(FTK_weaponStats2DB));
    }

    private static void RejectNullKey(Action action)
    {
        try { action(); }
        catch (ArgumentNullException) { _checks++; return; }
        throw new Exception("registered table must retain null-key rejection");
    }

    private static void RunLookups(bool arrays, int iterations)
    {
        int id;
        for (int i = 0; i < iterations; i++)
        {
            if (arrays)
            {
                ContentRegistry.TryGetSyntheticId("first", out id, new[] { typeof(FirstTable) }); _sink += id;
                ContentRegistry.TryGetSyntheticId("vanilla", out id, new[] { typeof(FirstTable) }); _sink += id;
                ContentRegistry.TryGetSyntheticId("second", out id, new[] { typeof(FirstTable), typeof(SecondTable) }); _sink += id;
                ContentRegistry.TryGetSyntheticId("vanilla", out id, new[] { typeof(FirstTable), typeof(SecondTable) }); _sink += id;
            }
            else
            {
                ContentRegistry.TryGetSyntheticId("first", out id, typeof(FirstTable)); _sink += id;
                ContentRegistry.TryGetSyntheticId("vanilla", out id, typeof(FirstTable)); _sink += id;
                ContentRegistry.TryGetSyntheticId("second", out id, typeof(FirstTable), typeof(SecondTable)); _sink += id;
                ContentRegistry.TryGetSyntheticId("vanilla", out id, typeof(FirstTable), typeof(SecondTable)); _sink += id;
            }
        }
    }
}
