using System;
using System.Collections.Generic;
using UnityEngine;

namespace FTKModFramework.Core.HotReload
{
    // This is an allocation ledger for every managed data package, not a Unity isolation boundary. The coordinator must
    // quiesce native consumers before Suspend and restore their references before Rollback.
    internal static partial class PaladinResourceState
    {
        internal sealed class Snapshot
        {
            internal List<UnityEngine.Object> Objects;
            internal readonly List<Action> Restore = new List<Action>();
            internal bool Finished;
            internal Snapshot(List<UnityEngine.Object> objects) { Objects = objects; }
        }

        private static List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private static readonly List<UnityEngine.Object> destroying = new List<UnityEngine.Object>();
        private static Snapshot transaction;

        internal static void Own(UnityEngine.Object resource)
        {
            if (resource == null) throw new ArgumentNullException("resource");
            Prune(owned);
            foreach (UnityEngine.Object current in owned)
                if (object.ReferenceEquals(current, resource)) return;
            owned.Add(resource);
        }

        internal static int OwnedObjectCount { get { Prune(owned); return owned.Count; } }
        internal static int PendingDestroyCount { get { Prune(destroying); return destroying.Count; } }
        internal static int RendererLeaseCount { get { return EnemyMeshResources.ReloadLeaseCount; } }
        internal static int RendererResourceCount { get { return EnemyMeshResources.ReloadResourceCount; } }
        internal static bool TransactionOpen { get { return transaction != null; } }

        internal static Dictionary<string, object> Diagnostics()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["ownedObjects"] = OwnedObjectCount;
            result["pendingDestroy"] = PendingDestroyCount;
            result["rendererLeases"] = RendererLeaseCount;
            result["rendererResources"] = RendererResourceCount;
            result["guardianClasses"] = GuardianRuntime.ReloadClassCount;
            result["overworldAilmentClasses"] = OverworldAilmentImmunity.ReloadClassCount;
            result["classProficiencyClasses"] = ClassProficiencyRegistry.Count;
            result["itemProficiencyItems"] = ItemProficiencyRegistry.Count;
            result["combatProficiencies"] = CombatProficiencyRegistry.Count;
            result["guardianEquipment"] = GuardianRuntime.ReloadEquipmentCount;
            result["guardianTransientEmpty"] = GuardianRuntime.ReloadTransientStateEmpty;
            result["paths"] = PackageModelPaths.ReloadPathCount;
            result["icons"] = PackageIcons.ReloadIconCount;
            result["itemModels"] = ItemModelRegistry.ReloadModelCount;
            result["offHandModels"] = ItemModelRegistry.ReloadOffHandCount;
            result["displayModels"] = ItemModelRegistry.ReloadDisplayCount;
            result["apparelModels"] = ItemApparelRegistry.ReloadApparelCount;
            return result;
        }

        internal static void RequireQuiescent()
        {
            if (RendererLeaseCount != 0)
                throw new InvalidOperationException("Live custom renderer leases prevent hot activation.");
            if (PendingDestroyCount != 0)
                throw new InvalidOperationException("Owned resources are still awaiting Unity destruction.");
            if (!GuardianRuntime.ReloadTransientStateEmpty)
                throw new InvalidOperationException("Guardian gameplay state prevents hot activation.");
        }

        // All participant methods detach complete maps and keep their exact old references.
        // They perform no Unity allocation or native callbacks. Candidate allocations occur
        // only after this returns, inside the coordinator's main-thread publication lock.
        internal static Snapshot Suspend()
        {
            if (transaction != null) throw new InvalidOperationException("Resource transaction is already open.");
            RequireQuiescent();
            Snapshot snapshot = new Snapshot(owned);
            transaction = snapshot;
            owned = new List<UnityEngine.Object>();
            try
            {
                snapshot.Restore.Add(GuardianRuntime.SuspendForReload());
                snapshot.Restore.Add(OverworldAilmentImmunity.SuspendForReload());
                snapshot.Restore.Add(ClassProficiencyRegistry.SuspendForReload());
                snapshot.Restore.Add(ItemProficiencyRegistry.SuspendForReload());
                snapshot.Restore.Add(CombatProficiencyRegistry.SuspendForReload());
                snapshot.Restore.Add(ItemModelRegistry.SuspendForReload());
                snapshot.Restore.Add(ItemApparelRegistry.SuspendForReload());
                snapshot.Restore.Add(PackageModelPaths.SuspendForReload());
                snapshot.Restore.Add(PackageIcons.SuspendForReload());
                return snapshot;
            }
            catch
            {
                Rollback(snapshot);
                throw;
            }
        }

        // Native DB/cache/UI restoration must precede this call, so no candidate consumer
        // can use a scheduled-for-destruction object. Any failed restore keeps both ledgers.
        internal static void Rollback(Snapshot snapshot)
        {
            Validate(snapshot);
            if (RendererLeaseCount != 0)
                throw new InvalidOperationException("Cannot roll back resources while renderer consumers remain.");
            for (int i = snapshot.Restore.Count - 1; i >= 0; i--) snapshot.Restore[i]();
            DestroyOwned(owned);
            owned = snapshot.Objects;
            Finish(snapshot);
        }

        // Only after the durable decision and removal of all old DB/cache/UI references.
        // Deferred destruction is observable; the next activation remains blocked until drained.
        internal static void Retire(Snapshot snapshot)
        {
            Validate(snapshot);
            if (RendererLeaseCount != 0)
                throw new InvalidOperationException("Cannot retire resources while renderer consumers remain.");
            DestroyOwned(snapshot.Objects);
            Finish(snapshot);
        }

        private static void Validate(Snapshot snapshot)
        {
            if (snapshot == null || snapshot.Finished || !object.ReferenceEquals(transaction, snapshot))
                throw new InvalidOperationException("Unknown or completed resource transaction.");
        }

        private static void Finish(Snapshot snapshot)
        {
            snapshot.Restore.Clear();
            snapshot.Objects = null;
            snapshot.Finished = true;
            transaction = null;
        }

        // Renderer leases use this too: removing a lease is not proof that Unity has
        // completed destroying its meshes/materials/textures in the current frame.
        internal static void DestroyTracked(UnityEngine.Object resource)
        {
            Prune(destroying);
            if (resource == null) return;
            if (!destroying.Contains(resource)) destroying.Add(resource);
            UnityEngine.Object.Destroy(resource);
        }

        private static void DestroyOwned(List<UnityEngine.Object> objects)
        {
            foreach (UnityEngine.Object resource in objects)
            {
                if (resource == null) continue;
                // Retain wrappers until native destruction is observed, not merely requested.
                DestroyTracked(resource);
            }
            objects.Clear();
        }

        private static void Prune(List<UnityEngine.Object> objects)
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] == null) objects.RemoveAt(i);
        }
    }
}
