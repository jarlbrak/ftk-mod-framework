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
        /// <summary>Cap on per-type ReflectionTypeLoadException diagnostic lines, so a pathological DLL
        /// (e.g. hundreds of types failing to resolve a missing dependency) cannot flood the log. After this
        /// many per-type lines a single "+K more" line summarizes the rest.</summary>
        private const int MaxLoaderExceptionLines = 10;

        /// <summary>
        /// For each mod with a resolved <see cref="DiscoveredMod.BehaviorDllPath"/>, load + reflect + register
        /// its behaviours. Records every tolerated problem on <paramref name="report"/>; never throws.
        ///
        /// Gated by <c>Plugin.EnableBehaviorLoading</c> (#35): when that flag is false the WHOLE external-DLL
        /// pre-pass is skipped, so NO Assembly.LoadFrom runs and ZERO DLL behaviours register. This gates ONLY
        /// the external-DLL path; the in-assembly behaviours (FrameworkBehaviors / com.ftkmf.sampledata:Steal)
        /// register on their own unconditional path and are unaffected. The flag is null-guarded so a test
        /// context where Plugin.Awake never ran defaults to running the pre-pass (matching DebugEncounterOverride
        /// / CutpurseEnemy, which guard <c>Plugin.&lt;cfg&gt; == null</c>).
        /// </summary>
        internal static void LoadAll(List<DiscoveredMod> mods, ValidationReport report)
        {
            if (mods == null) return;

            // Gate the external-DLL pre-pass. null => no Awake (test context): default to enabled.
            if (Plugin.EnableBehaviorLoading != null && !Plugin.EnableBehaviorLoading.Value)
            {
                Plugin.Log.LogInfo("BehaviorLoader: behavior loading disabled by config " +
                    "(EnableBehaviorLoading=false); 0 DLL behaviors loaded.");
                return;
            }

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
                // P0 (#33): keep every non-null entry, log, continue. NEVER rethrow. A partially loadable
                // assembly (e.g. one type references a member missing at runtime) still yields its loadable
                // behaviours. This keep-non-null-and-continue behavior is unchanged.
                Plugin.Log.LogWarning("[" + modGuid + "] ReflectionTypeLoadException loading behaviors; using the types that did load.");
                List<Type> ok = new List<Type>();
                if (ex.Types != null) foreach (Type t in ex.Types) if (t != null) ok.Add(t);
                types = ok.ToArray();

                // #35: per-failed-type diagnostic SUMMARY. Each non-null LoaderException names ONE type that
                // failed to resolve (e.g. a missing transitive dependency the type referenced). Log up to
                // MaxLoaderExceptionLines of them, then a single "+K more" line, so a pathological DLL with
                // hundreds of broken types cannot flood the log.
                //
                // This block is DELIBERATE defensive instrumentation that is INTENTIONALLY NOT EXERCISED by any
                // shipped fixture, and that is by design: the broken.dll fixture is a non-assembly/bad-format
                // file, so it fails earlier at Assembly.LoadFrom (the LoadFrom catch above), NOT at GetTypes,
                // and so never reaches here. THIS path fires only for a DLL that LOADS successfully but whose
                // individual types fail to resolve at GetTypes time (e.g. a missing transitive dependency).
                // We deliberately do NOT add a fixture for it: constructing a DLL that loads but has
                // individually-unresolvable types is fragile and over-engineered for the value, so the
                // instrumentation stands on inspection rather than on a self-test.
                Exception[] loaderExceptions = ex.LoaderExceptions;
                if (loaderExceptions != null)
                {
                    int logged = 0;
                    int total = 0;
                    foreach (Exception le in loaderExceptions)
                    {
                        if (le == null) continue; // a LoaderExceptions slot can be null; skip it.
                        total++;
                        if (logged < MaxLoaderExceptionLines)
                        {
                            Plugin.Log.LogWarning("[" + modGuid + "] behavior type load failure: " + le.Message);
                            logged++;
                        }
                    }
                    if (total > logged)
                        Plugin.Log.LogWarning("[" + modGuid + "] behavior type load failure: +" +
                            (total - logged) + " more (capped at " + MaxLoaderExceptionLines + ").");
                }
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
