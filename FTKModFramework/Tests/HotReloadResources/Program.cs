using System;
using System.Collections.Generic;
using FTKModFramework.Core;
using FTKModFramework.Core.HotReload;

// These doubles exercise ledger ordering and deferred destruction, not Unity lifecycle behavior.
namespace UnityEngine
{
    internal class Object
    {
        private bool destroyed;
        private static readonly List<Object> pending = new List<Object>();
        internal static void Destroy(Object value) { if (!pending.Contains(value)) pending.Add(value); }
        internal static void EndFrame() { foreach (Object value in pending) value.destroyed = true; pending.Clear(); }
        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || a.destroyed;
            bool bn = ReferenceEquals(b, null) || b.destroyed;
            return an || bn ? an == bn : ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return base.GetHashCode(); }
    }
}
namespace FTKModFramework.Core
{
    internal static class EnemyMeshResources
    {
        internal static int ReloadLeaseCount;
        internal static int ReloadResourceCount;
    }
    internal static class GuardianRuntime
    {
        internal static bool ReloadTransientStateEmpty = true;
        internal static int ReloadClassCount;
        internal static int ReloadEquipmentCount;
        internal static Action SuspendForReload()
        {
            int old = ReloadClassCount; ReloadClassCount = 0;
            return delegate { ReloadClassCount = old; };
        }
    }
    internal static class ItemModelRegistry
    {
        internal static int ReloadModelCount;
        internal static int ReloadOffHandCount;
        internal static int ReloadDisplayCount;
        internal static Action SuspendForReload() { return delegate { }; }
    }
    internal static class ItemApparelRegistry
    {
        internal static int ReloadApparelCount;
        internal static Action SuspendForReload() { return delegate { }; }
    }
    internal static class PackageModelPaths
    {
        internal static int ReloadPathCount;
        internal static Action SuspendForReload() { return delegate { }; }
    }
    internal static class PackageIcons
    {
        internal static int ReloadIconCount;
        internal static bool FailSuspend;
        internal static Action SuspendForReload()
        {
            if (FailSuspend) throw new InvalidOperationException("injected participant failure");
            return delegate { };
        }
    }
}
internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++; if (!condition) throw new Exception(message);
    }
    private static void Reject(Action action, string message)
    {
        bool threw = false;
        try { action(); } catch (InvalidOperationException) { threw = true; }
        Check(threw, message);
    }
    private static void Main()
    {
        Check(StitcherPreflightRules.FlattenedPath("garment", 1) == "garment(Clone)", "Stitcher flattens source hierarchy to clone child.");
        Reject(delegate { StitcherPreflightRules.FlattenedPath("garment", 0); }, "Inactive-only source garment rejected.");
        Reject(delegate { StitcherPreflightRules.FlattenedPath("garment", 2); }, "Multiple flattened renderers rejected.");
        int[] remapped = StitcherPreflightRules.MapBones(new[] { "hip", "arm" }, new[] { "Avatar", "arm", "hip" });
        Check(remapped[0] == 2 && remapped[1] == 1, "Native name mapping preserves source bone order.");
        Reject(delegate { StitcherPreflightRules.MapBones(new[] { "hip" }, new[] { "Avatar", "arm" }); }, "Missing avatar bone fails closed.");
        Reject(delegate { StitcherPreflightRules.MapBones(new[] { "hip" }, new[] { "hip", "hip" }); }, "Duplicate native transform name fails like Stitcher catalog.");
        Reject(delegate { StitcherPreflightRules.MapBones(new string[0], new[] { "hip" }); }, "Empty source palette is not fabricated.");
        UnityEngine.Object old = new UnityEngine.Object();
        PaladinResourceState.Own(old); PaladinResourceState.Own(old);
        Check(PaladinResourceState.OwnedObjectCount == 1, "Ownership cannot duplicate a native object.");
        GuardianRuntime.ReloadClassCount = 1;
        PackageIcons.FailSuspend = true;
        Reject(delegate { PaladinResourceState.Suspend(); }, "Partial participant suspension rejects.");
        Check(!PaladinResourceState.TransactionOpen && GuardianRuntime.ReloadClassCount == 1 &&
            PaladinResourceState.OwnedObjectCount == 1 && old != null, "Partial suspension restores prior owner and binding.");
        PackageIcons.FailSuspend = false;
        var rollback = PaladinResourceState.Suspend();
        UnityEngine.Object rejected = new UnityEngine.Object(); PaladinResourceState.Own(rejected);
        GuardianRuntime.ReloadClassCount = 2;
        PaladinResourceState.Rollback(rollback);
        Check(old != null && PaladinResourceState.OwnedObjectCount == 1 && GuardianRuntime.ReloadClassCount == 1,
            "Rollback preserves exact old object and binding.");
        Check(PaladinResourceState.PendingDestroyCount == 1 && rejected != null, "Destroy request is not proof of native release.");
        Reject(delegate { PaladinResourceState.Suspend(); }, "Next activation waits for destruction.");
        UnityEngine.Object.EndFrame();
        Check(PaladinResourceState.PendingDestroyCount == 0 && rejected == null, "Native-null observation drains retirement.");
        Reject(delegate { PaladinResourceState.Rollback(rollback); }, "Completed token cannot roll back again.");
        for (int i = 0; i < 100; i++)
        {
            var snapshot = PaladinResourceState.Suspend();
            Reject(delegate { PaladinResourceState.Suspend(); }, "Nested activation rejects.");
            UnityEngine.Object current = new UnityEngine.Object(); PaladinResourceState.Own(current);
            PaladinResourceState.Retire(snapshot);
            Check(PaladinResourceState.OwnedObjectCount == 1 && PaladinResourceState.PendingDestroyCount == 1,
                "Cycle keeps candidate and schedules exactly one old object.");
            UnityEngine.Object.EndFrame();
            Check(PaladinResourceState.PendingDestroyCount == 0 && current != null, "Cycle retires only old generation.");
        }
        UnityEngine.Object leased = new UnityEngine.Object();
        PaladinResourceState.DestroyTracked(leased);
        Reject(delegate { PaladinResourceState.Suspend(); }, "Released renderer assets still require a destruction fence.");
        UnityEngine.Object.EndFrame();
        Check(PaladinResourceState.PendingDestroyCount == 0, "Renderer destruction fence drains after native destruction.");
        EnemyMeshResources.ReloadLeaseCount = 1;
        Reject(delegate { PaladinResourceState.Suspend(); }, "Live renderer lease closes boundary.");
        EnemyMeshResources.ReloadLeaseCount = 0;
        GuardianRuntime.ReloadTransientStateEmpty = false;
        Reject(delegate { PaladinResourceState.Suspend(); }, "Guardian gameplay closes boundary.");
        GuardianRuntime.ReloadTransientStateEmpty = true;
        var remove = PaladinResourceState.Suspend(); PaladinResourceState.Retire(remove);
        UnityEngine.Object.EndFrame();
        Check(PaladinResourceState.OwnedObjectCount == 0 && PaladinResourceState.PendingDestroyCount == 0,
            "Removal leaves no owned native allocations.");
        Console.WriteLine("PASS: " + checks + " resource ledger checks; Unity lifecycle remains a live gate.");
    }
}
