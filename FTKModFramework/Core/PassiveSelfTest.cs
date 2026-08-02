using System;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the Phase-1 passive-trait Core plumbing (spec #78, work item #79). It binds a
    /// THROWAWAY passive to an existing REGISTERED class row (the Thief's, present at registration time), then
    /// proves, WITHOUT gameplay, the three acceptance paths and emits one
    /// <c>SELF-TEST PASS: AddPassive registry (bind, idempotent, reject)</c> line (or a matching FAIL):
    /// <list type="bullet">
    /// <item><b>bind:</b> <see cref="Content.AddPassive"/> returns a def carrying the expected
    /// Key/ClassId/Trigger/DisplayName, the trait resolves back by (classId, trigger), and the display name
    /// resolves through the Localization path (<see cref="Localization.TryGetName"/>).</item>
    /// <item><b>idempotent:</b> a second AddPassive on the SAME (modGuid, passiveId) returns the FIRST def
    /// (trigger unchanged) and adds no second trait (warns).</item>
    /// <item><b>reject:</b> a null class row AND an unregistered (bare) class row each return null with no
    /// registry mutation (the original probe binding is untouched).</item>
    /// </list>
    /// It then CLEARS its probe via <see cref="PassiveRegistry.Remove"/> so NO trait stays bound to a real
    /// class. The cleanup is LOAD-BEARING: the trigger patches are live, so a leftover probe binding on the
    /// Thief (or any vanilla class) would become a real combat negate. Runs UNCONDITIONALLY from Plugin's
    /// framework self-test block, NOT from the sample-content block: FindProbeClassRow falls back to the first
    /// class row in the DB, so the probe binds with or without the bundled samples.
    /// </summary>
    internal static class PassiveSelfTest
    {
        private const string ProbeId = "_selftest_passive_probe";
        private const string ProbeName = "Self-Test Probe Passive";

        public static void Run()
        {
            string key = Plugin.Guid + ":" + ProbeId;
            try
            {
                FTK_playerGameStart classRow = FindProbeClassRow();
                if (classRow == null)
                {
                    Plugin.Log.LogError("SELF-TEST FAIL: AddPassive registry (bind, idempotent, reject); " +
                        "no registered class row to bind the probe to.");
                    return;
                }

                // --- bind: register the probe and prove it resolves back by (classId, trigger) ---
                PassiveTraitDef def = Content.AddPassive(
                    Plugin.Guid, ProbeId, classRow, PassiveTrigger.IncomingAttack, ProbeName);

                PassiveTraitDef resolved;
                bool bindOk = def != null
                    && def.Key == key
                    && def.Trigger == PassiveTrigger.IncomingAttack
                    && def.DisplayName == ProbeName
                    && PassiveRegistry.TryResolve(def.ClassId, PassiveTrigger.IncomingAttack, out resolved)
                    && resolved == def;

                // Display name resolves through the existing Localization path (keyed by the trait Key).
                string locName;
                bool localizationOk = Localization.TryGetName(key, out locName) && locName == ProbeName;

                // --- idempotent: same (modGuid, passiveId), different trigger/name => first def wins, no dup ---
                PassiveTraitDef again = Content.AddPassive(
                    Plugin.Guid, ProbeId, classRow, PassiveTrigger.ConsumableDebuff, "Should Be Ignored");
                PassiveTraitDef dupTrigger;
                bool idempotentOk = def != null
                    && again == def
                    && again.Trigger == PassiveTrigger.IncomingAttack // unchanged by the 2nd call
                    && !PassiveRegistry.TryResolve(def.ClassId, PassiveTrigger.ConsumableDebuff, out dupTrigger);

                // --- reject: a null class row, then an unregistered (bare) class row => null, no mutation ---
                // These two reject probes are INTENTIONALLY never registered (AddPassive returns null before
                // touching the registry), so unlike the bind probe above they need no PassiveRegistry.Remove cleanup.
                PassiveTraitDef nullDef = Content.AddPassive(
                    Plugin.Guid, "_selftest_passive_null", null, PassiveTrigger.IncomingAttack, "X");
                FTK_playerGameStart bareRow = new FTK_playerGameStart();
                bareRow.m_ID = "ftkmf_selftest_unregistered_class";
                PassiveTraitDef unregDef = Content.AddPassive(
                    Plugin.Guid, "_selftest_passive_unreg", bareRow, PassiveTrigger.IncomingAttack, "X");
                PassiveTraitDef stillThere;
                bool rejectOk = def != null
                    && nullDef == null
                    && unregDef == null
                    && PassiveRegistry.TryResolve(def.ClassId, PassiveTrigger.IncomingAttack, out stillThere)
                    && stillThere == def; // the original probe binding is untouched

                // --- cleanup (LOAD-BEARING): remove the probe so nothing stays bound to a real class ---
                bool removed = PassiveRegistry.Remove(key);
                PassiveTraitDef leftover;
                bool cleanupOk = def != null
                    && removed
                    && !PassiveRegistry.TryResolve(def.ClassId, PassiveTrigger.IncomingAttack, out leftover);

                int classId = def != null ? def.ClassId : -1;
                bool ok = bindOk && localizationOk && idempotentOk && rejectOk && cleanupOk;
                if (ok)
                    Plugin.Log.LogInfo("SELF-TEST PASS: AddPassive registry (bind, idempotent, reject) " +
                        "[classId=" + classId + ", trigger=IncomingAttack, localized name resolved, probe cleared].");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL: AddPassive registry (bind, idempotent, reject); " +
                        "bindOk=" + bindOk + " localizationOk=" + localizationOk + " idempotentOk=" + idempotentOk +
                        " rejectOk=" + rejectOk + " cleanupOk=" + cleanupOk + ".");
            }
            catch (Exception e)
            {
                // Never leave the probe bound, even on an assertion throw (the cleanup is load-bearing).
                PassiveRegistry.Remove(key);
                Plugin.Log.LogError("SELF-TEST FAIL: AddPassive registry (bind, idempotent, reject); " + e);
            }
        }

        // Prefer the Thief's row (the work-item's named probe target); fall back to the first row in the class
        // DB so the test still runs if the Thief is somehow absent. Either way the probe is removed at the end.
        private static FTK_playerGameStart FindProbeClassRow()
        {
            FTK_playerGameStartDB db = Content.Db<FTK_playerGameStartDB>();
            FTK_playerGameStart thief = db.GetEntryByStringID("ftkmf_thief");
            if (thief != null) return thief;

            Array arr = (Array)Reflect.GetField(db, "m_Array");
            if (arr != null && arr.Length > 0) return (FTK_playerGameStart)arr.GetValue(0);
            return null;
        }
    }
}
