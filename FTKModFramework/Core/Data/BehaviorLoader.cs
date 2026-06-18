using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FTKModFramework.Behaviors;
using FTKModFramework.Core;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// The behaviour-DLL pre-pass (#33, FR-3/FR-4/FR-7). For every discovered mod that declares a
    /// (traversal-guarded) <c>behaviorDll</c>, this loads that external assembly, reflects on its
    /// <c>[ContentBehavior]</c>-attributed <c>ProficiencyBase</c> subclasses, and registers each under
    /// (modGuid + ":" + attr.Name) in <see cref="BehaviorRegistry"/>.
    ///
    /// SEQUENCING (FR-7): <see cref="ContentLoader.Load"/> calls <see cref="LoadAll"/> ONCE, after
    /// discovery and BEFORE any content-registration phase. So every DLL-supplied behaviour key is in the
    /// registry before Phase 2's <c>WireBehavior</c> (#31) resolves <c>modGuid:behavior</c>: the resolution
    /// can never run ahead of the pre-pass.
    ///
    /// FAULT ISOLATION is total (FR-4): a missing file, a <c>LoadFrom</c> throw, a
    /// <c>ReflectionTypeLoadException</c>, and an attributed-but-not-<c>ProficiencyBase</c> type each
    /// produce a recorded problem and CONTINUE; every other mod still loads. The whole per-mod load is
    /// wrapped so nothing ever throws out of <see cref="LoadAll"/>.
    ///
    /// net35 / NFR-4: this uses only <c>Assembly.LoadFrom</c> + plain reflection. There is deliberately NO
    /// AppDomain isolation and NO sandbox: co-op requires every client to run identical mod code anyway, so
    /// an isolation boundary would buy nothing and break the shared-type identity the registry depends on.
    /// </summary>
    internal static class BehaviorLoader
    {
        /// <summary>
        /// For each mod with a resolved <see cref="DiscoveredMod.BehaviorDllPath"/>, load + reflect + register
        /// its behaviours. Records every tolerated problem on <paramref name="report"/>; never throws.
        /// </summary>
        internal static void LoadAll(List<DiscoveredMod> mods, ValidationReport report)
        {
            if (mods == null) return;

            foreach (DiscoveredMod mod in mods)
            {
                string path = mod.BehaviorDllPath;
                if (path == null) continue; // no behaviour DLL declared (or its path was rejected by the #32 guard).

                string modGuid = mod.Manifest.ModGuid;

                // Outer guard: a behaviour load NEVER throws out of LoadAll (FR-4 final clause). Even an
                // unexpected reflection fault on one DLL leaves every other mod's behaviours loadable.
                try
                {
                    LoadOne(modGuid, path, report);
                }
                catch (Exception e)
                {
                    report.Error("[" + modGuid + "] unexpected error loading behaviorDll: " + e.Message + " (skipped).");
                }
            }
        }

        private static void LoadOne(string modGuid, string path, ValidationReport report)
        {
            // #32 validated the path SHAPE only (traversal guard), not existence. A declared-but-missing DLL
            // is an author error: report it and move on.
            if (!File.Exists(path))
            {
                report.Error("[" + modGuid + "] behaviorDll not found: " + path + " (skipped).");
                return;
            }

            Assembly asm;
            try
            {
                asm = Assembly.LoadFrom(path);
            }
            catch (Exception e)
            {
                // A non-assembly file, a bad-format DLL, a load policy refusal, etc. Skip this mod's DLL; its
                // CONTENT still loads later and every other mod still loads its own behaviours.
                report.Error("[" + modGuid + "] Assembly.LoadFrom failed: " + e.Message + " (skipped).");
                return;
            }

            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // P0: keep every non-null entry, log, continue. NEVER rethrow. A partially loadable assembly
                // (e.g. one type references a member missing at runtime) still yields its loadable behaviours.
                // The per-failed-type diagnostic SUMMARY is deferred to #35; here we only keep + continue.
                Plugin.Log.LogWarning("[" + modGuid + "] ReflectionTypeLoadException loading behaviors; using the types that did load.");
                List<Type> ok = new List<Type>();
                if (ex.Types != null) foreach (Type t in ex.Types) if (t != null) ok.Add(t);
                types = ok.ToArray();
            }

            // Collect the attributed types, then SORT by Type.FullName (ordinal) BEFORE registration so the
            // registration order is identical on every machine and the registry's first-wins tiebreak for a
            // duplicate Name within this DLL is deterministic (first by sorted full name wins).
            List<Type> attributed = new List<Type>();
            foreach (Type t in types)
            {
                if (t == null) continue;
                if (Attribute.GetCustomAttribute(t, typeof(ContentBehaviorAttribute)) != null)
                    attributed.Add(t);
            }
            attributed.Sort(CompareByFullName);

            int registered = 0;
            foreach (Type t in attributed)
            {
                ContentBehaviorAttribute attr =
                    (ContentBehaviorAttribute)Attribute.GetCustomAttribute(t, typeof(ContentBehaviorAttribute));

                if (!typeof(ProficiencyBase).IsAssignableFrom(t))
                {
                    // Author intent was to register a behaviour; the type just isn't hostable. Report it (not
                    // silent) and do NOT register. (FR-4.)
                    report.Warning("[" + modGuid + "] '" + t.FullName +
                        "' has [ContentBehavior] but is not a ProficiencyBase (skipped).");
                    continue;
                }

                // BehaviorRegistry is first-wins-with-warning, so a duplicate Name within this DLL keeps the
                // first (deterministic by the sort above) and warns; nothing here needs to dedupe.
                BehaviorRegistry.Register(modGuid, attr.Name, t);
                Plugin.Log.LogInfo("BehaviorLoader: registered '" + modGuid + ":" + attr.Name + "' -> " + t.FullName + ".");
                registered++;
            }

            Plugin.Log.LogInfo("[" + modGuid + "] behaviorDll loaded: " + registered + " behavior(s) registered from " + path + ".");
        }

        /// <summary>Ordinal compare by <see cref="Type.FullName"/> for a deterministic registration order.</summary>
        private static int CompareByFullName(Type a, Type b)
        {
            return string.CompareOrdinal(a.FullName, b.FullName);
        }
    }
}
