using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using FTKModFramework.Core;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// The WHOLE combat-resolve loop for a SOLO fight, run as a Unity coroutine on <see cref="BridgeHost"/>
    /// (net35: no Task/async, so we poll EncounterSession turn state across frames). It REPLACES the old
    /// resolve_turn{cheat:KillAll} one-shot that left in-dungeon combat STUCK (enemy hp==0 but m_IsAlive==true,
    /// m_IsInCombat stays true, turn never resolves). <c>win_combat</c>/<c>force_win</c>/<c>auto_combat</c> only
    /// ARM this driver and return immediately; the agent polls <c>/state.combat.active==false</c>.
    ///
    /// WHY THE OLD PATH HUNG (the fix): CheatKillAll was fired WITHOUT all three commit gates holding. On the
    /// overworld it happened to land in "Wait For Stance" and resolved; inside the crypt it fired before the
    /// stance UI was ready (the EngageBattle banner/camera intro had not finished initializing
    /// uiBattleStanceButtons), so ComputeAttackSlotResults ran in a not-ready state, corrupted the turn-commit
    /// FSM, and combat hung. The driver eliminates that by committing ONLY when
    /// <c>inCombat &amp;&amp; m_FightOrder[0].m_Pid.IsPlayer() &amp;&amp; HeroTurnReady()</c> all hold, and by
    /// WAITING OUT each commit (the targeted enemy goes m_IsAlive==false, or the turn leaves the player) before
    /// it loops; it never re-fires mid-commit (re-entrancy = the corruption).
    ///
    /// PER-ENEMY KillSingle, not KillAll: each enemy gets its own clean lethal through the SAME real
    /// damage + CombatEnemyDie path (uiBattleStanceButtons.CheatKillSingle ->
    /// m_PlayerSlots.m_CheatAttack=KillSingle + ComputeAttackSlotResults(CombatCow,true) -> StartEngageAttack ->
    /// damage -> CombatEnemyDie RPC -> the dummy FSM advances and the turn ENDS). KillAll fires a firestorm2 AoE
    /// whose per-enemy death RPCs were not verified to all fire in one in-dungeon commit; the looped KillSingle
    /// is the verified-safe resolve and the in-dungeon stuck fix. Defeat also flips m_IsInCombat false, so a
    /// lost fight resolves clean too.
    ///
    /// Verified game members (ilspycmd, Assembly-CSharp, Jun 2026):
    ///   FTKUI.Instance.m_BattleStanceButtons (uiBattleStanceButtons). m_Initialized (public bool, private set;
    ///     set true only inside Initialize(), reached after the EngageBattle banner/camera intro completes).
    ///   uiBattleStanceButtons.CombatCow => GameLogic.Instance.GetCurrentCombatCOW().
    ///   uiBattleStanceButtons.SelectEnemyDummy(FTKPlayerID, FTK_itembase.ID=None).
    ///   uiBattleStanceButtons.CheatKillSingle(): m_PlayerSlots.m_CheatAttack=KillSingle;
    ///     ComputeAttackSlotResults(CombatCow,true); BattleButtonsOff(false). Routes the real damage+death path.
    ///   SlotControl.AttackCheatType { None=0, Miss=1, KillSingle=2, KillAll=3, TriggerAbility=4 }.
    ///   GameLogic.GetCurrentCombatCOW().m_CurrentDummy.m_CharacterDummyFSM.ActiveStateName == "Wait For Stance".
    ///   EncounterSessionMC.m_IsInCombat / m_FightOrder[0].m_Pid (FTKPlayerID, IsPlayer()).
    ///   EncounterSession.m_IsInCombat / m_EnemyDummies (Dictionary&lt;FTKPlayerID,EnemyDummy&gt;).
    ///   EnemyDummy : EnemyInfo : CharacterDummy. CharacterDummy.m_IsAlive (bool, cleared by the CombatEnemyDie
    ///     RPC via RemoveCombatant). EnemyInfo.m_CurrentHealth (int).
    /// </summary>
    internal static class CombatDriver
    {
        // Frame budgets (~60fps).
        private const int MaxTurnFrames = 600;     // ~10s: a single commit must resolve (enemy dies / turn ends).
        private const int MaxTotalFrames = 36000;  // ~10min absolute ceiling for the whole fight.

        private static bool _running;
        private static string _lastError;   // null on clean resolve; "at <action>.<step>" terminus otherwise
        private static bool _forceWin;       // distinguishes force_win (intent/logging) from win_combat

        public static bool IsRunning { get { return _running; } }
        public static string LastError { get { return _lastError; } }

        /// <summary>
        /// Arm the resolve coroutine on the BridgeHost (must be called on the main thread). Idempotent: returns
        /// false if already running. <paramref name="forceWin"/> only changes intent/logging (both paths loop
        /// gated KillSingle per living enemy); kept distinct so force_win vs win_combat is observable.
        /// </summary>
        public static bool Arm(bool forceWin)
        {
            if (_running) return false;
            BridgeHost host = BridgeHost.Instance;
            if (host == null)
            {
                Plugin.Log.LogError("[agent] combat resolve failed at arm: no BridgeHost (no session host)");
                return false;
            }
            _forceWin = forceWin;
            _lastError = null;
            _running = true;
            try { host.StartCoroutine(Drive()); }
            catch (Exception e)
            {
                _running = false;
                _lastError = "at " + Tag() + ".arm: " + e.Message;
                Plugin.Log.LogError("[agent] combat " + _lastError);
                return false;
            }
            return true;
        }

        private static string Tag() { return _forceWin ? "force_win" : "win_combat"; }

        private static IEnumerator Drive()
        {
            Plugin.Log.LogInfo("[agent] combat: " + Tag() + " driver armed (per-enemy KillSingle, gated).");

            int total = 0;

            // MAIN LOOP: per-enemy clean lethal through the real turn-commit path until combat resolves.
            while (true)
            {
                // G0: combat resolved? (win or lose -> m_IsInCombat flips false). Clean exit.
                if (!CombatActive()) break;

                if (total >= MaxTotalFrames)
                {
                    DungeonOps.DismissPopups();
                    Done("at " + Tag() + ".budget: total frame ceiling hit, combat unresolved");
                    yield break;
                }

                // G1: whose turn. Enemy/banner turn (or m_FightOrder empty mid-transition) -> WAIT, never commit.
                // Committing on an enemy turn is the original hang.
                object active = ActiveTurnFid();
                if (active == null || !FidIsPlayer(active))
                {
                    total++;
                    yield return null;
                    continue;
                }

                // G2: stance UI ready (m_Initialized && dummy FSM == "Wait For Stance"). Until BOTH hold the
                // commit would corrupt the turn, so WAIT.
                if (!HeroTurnReady())
                {
                    total++;
                    yield return null;
                    continue;
                }

                // G3: pick the first live enemy. None -> victory is settling (death RPCs in flight); wait a frame
                // and re-check G0.
                object enemyFid = FirstLiveEnemyFid();
                if (enemyFid == null)
                {
                    total++;
                    yield return null;
                    continue;
                }

                object bsb = BattleStanceButtons();
                if (bsb == null)
                {
                    // The UI vanished though HeroTurnReady passed a frame ago: treat as transient, wait.
                    total++;
                    yield return null;
                    continue;
                }

                // G4: select the victim, then YIELD ONE FRAME so the selection settles before the commit (the
                // BridgeHost marshals on its own coroutine; select + cheat want a frame between them).
                SafeInvokeArgs(bsb, "SelectEnemyDummy", new[] { enemyFid.GetType() }, new object[] { enemyFid });
                Plugin.Log.LogInfo("[agent] combat: " + Tag() + " selected enemy " + FidLabel(enemyFid)
                                   + " (committing KillSingle).");
                yield return null;

                // Re-confirm the gates AFTER the settle frame: if the enemy turn started or the stance closed in
                // that frame, do NOT commit (that mismatch is exactly what corrupts the turn).
                if (!CombatActive()) break;
                active = ActiveTurnFid();
                if (active == null || !FidIsPlayer(active) || !HeroTurnReady())
                {
                    total++;
                    yield return null;
                    continue;
                }

                // G5: COMMIT exactly once via the verified turn-ending path. CheatKillSingle sets
                // m_PlayerSlots.m_CheatAttack=KillSingle then ComputeAttackSlotResults(CombatCow,true), which runs
                // StartEngageAttack -> applies damage -> fires CombatEnemyDie when the enemy dies -> the dummy FSM
                // advances and the turn ENDS. (Identical to manually setting m_CheatAttack + Attack(), but atomic
                // and game-authored.) We do NOT re-fire; G6 waits this commit out.
                CommitKillSingle(bsb);

                // G6: WAIT OUT the commit. It resolves when the targeted enemy goes !m_IsAlive, OR the turn leaves
                // the player (the commit ended our turn), OR combat ends. Never re-fire mid-commit.
                int turnFrames = 0;
                while (turnFrames < MaxTurnFrames && total < MaxTotalFrames)
                {
                    if (!CombatActive()) break;                       // win/lose -> G0 breaks next outer check.
                    if (!EnemyAlive(enemyFid)) break;                 // this enemy died: commit landed cleanly.
                    object cur = ActiveTurnFid();
                    if (cur == null || !FidIsPlayer(cur)) break;       // turn left the player: commit ended turn.
                    turnFrames++; total++;
                    yield return null;
                }

                if (turnFrames >= MaxTurnFrames)
                {
                    // The commit did not resolve (the stuck signature). Surface it rather than re-firing (which
                    // would deepen the corruption). The harness reads combat.stuck and aborts the run.
                    DungeonOps.DismissPopups();
                    Done("at " + Tag() + ".turnStall: commit did not resolve in budget (enemy "
                         + FidLabel(enemyFid) + " hp may be 0 with alive=true)");
                    yield break;
                }

                // Settle a couple frames so the death RPC / next-turn ordering lands before we re-poll.
                yield return null;
                yield return null;
                total += 2;
            }

            // CLEAN RESOLVE: dismiss any post-fight / reward popup so the harness lands on the overworld/next room.
            DungeonOps.DismissPopups();
            yield return null;
            DungeonOps.DismissPopups();
            Plugin.Log.LogInfo("[agent] combat: " + Tag() + " RESOLVED (m_IsInCombat=false).");
            Done(null);
        }

        // ------------------------------------------------------------- commit ---------------------------

        // Commit a clean, turn-ending lethal on the currently selected enemy via the game's own cheat method.
        // CheatKillSingle routes through ComputeAttackSlotResults(CombatCow,true) (the REAL damage + CombatEnemyDie
        // path) and BattleButtonsOff, so it is the verified turn-commit, not a HUD-only no-op. Best-effort; the
        // outer loop's G6 wait detects whether it actually resolved.
        private static void CommitKillSingle(object bsb)
        {
            try { Reflect.Invoke(bsb, "CheatKillSingle"); }
            catch (Exception e) { Plugin.Log.LogWarning("[agent] combat: CheatKillSingle threw: " + e.Message); }
        }

        // ------------------------------------------------------------- completion ------------------------

        private static void Done(string error)
        {
            _running = false;
            _lastError = error;
            if (error == null)
                Plugin.Log.LogInfo("[agent] combat: " + Tag() + " done. combat resolved clean.");
            else
                Plugin.Log.LogError("[agent] combat: " + error);
        }

        // ------------------------------------------------------------- state probes ----------------------

        private static bool CombatActive()
        {
            object mc = StaticInstance("EncounterSessionMC");
            object es = StaticInstance("EncounterSession");
            if (mc != null && ToBool(SafeField(mc, "m_IsInCombat"))) return true;
            if (es != null && ToBool(SafeField(es, "m_IsInCombat"))) return true;
            return false;
        }

        // EncounterSessionMC.m_FightOrder[0].m_Pid (an FTKPlayerID). Null on any miss / empty order.
        private static object ActiveTurnFid()
        {
            object mc = StaticInstance("EncounterSessionMC");
            if (mc == null) return null;
            IList fo = SafeField(mc, "m_FightOrder") as IList;
            if (fo == null || fo.Count == 0) return null;
            object first = fo[0];
            if (first == null) return null;
            return SafeField(first, "m_Pid");
        }

        private static bool FidIsPlayer(object fid)
        {
            object r = SafeInvoke(fid, "IsPlayer");
            return r is bool && (bool)r;
        }

        // m_BattleStanceButtons.m_Initialized && GetCurrentCombatCOW().m_CurrentDummy.m_CharacterDummyFSM
        // .ActiveStateName == "Wait For Stance".
        private static bool HeroTurnReady()
        {
            try
            {
                object bsb = BattleStanceButtons();
                if (bsb == null) return false;
                // The authoritative readiness is the active dummy FSM reaching "Wait For Stance" (kb_9f7c454b):
                // committing there routes the kill through the real damage RPC safely. m_Initialized (the
                // stance-buttons UI flag) is a belt-and-suspenders signal that is reliably true on the overworld,
                // but the in-dungeon forced-ack path (DungeonScrollComplete skips the FSM's button Initialize step)
                // leaves it false while the dummy IS genuinely in "Wait For Stance". So require m_Initialized on the
                // overworld, but inside a dungeon gate on the FSM state alone.
                if (!DungeonOps.InDungeon() && !ToBool(SafeField(bsb, "m_Initialized"))) return false;
                object gl = StaticInstance("GameLogic");
                if (gl == null) return false;
                object cow = SafeInvoke(gl, "GetCurrentCombatCOW");
                if (cow == null) return false;
                object dummy = SafeField(cow, "m_CurrentDummy");
                if (dummy == null) return false;
                object fsm = SafeField(dummy, "m_CharacterDummyFSM");
                if (fsm == null) return false;
                object stateName = SafeProp(fsm, "ActiveStateName");
                return stateName is string && (string)stateName == "Wait For Stance";
            }
            catch { return false; }
        }

        // First live enemy FID from EncounterSession.m_EnemyDummies, keyed by m_IsAlive && m_CurrentHealth>0.
        private static object FirstLiveEnemyFid()
        {
            object es = StaticInstance("EncounterSession");
            if (es == null) return null;
            IDictionary dummies = SafeField(es, "m_EnemyDummies") as IDictionary;
            if (dummies == null) return null;
            foreach (DictionaryEntry de in dummies)
            {
                object dummy = de.Value;
                if (dummy == null) continue;
                if (!ToBool(SafeField(dummy, "m_IsAlive"))) continue;
                int? hp = ToInt(SafeField(dummy, "m_CurrentHealth"));
                if (hp.HasValue && hp.Value <= 0) continue; // dying (hp 0 but alive flag not yet cleared): skip.
                return de.Key;
            }
            return null;
        }

        // True iff the enemy keyed by fid is still m_IsAlive in EncounterSession.m_EnemyDummies.
        private static bool EnemyAlive(object fid)
        {
            object es = StaticInstance("EncounterSession");
            if (es == null) return false;
            IDictionary dummies = SafeField(es, "m_EnemyDummies") as IDictionary;
            if (dummies == null) return false;
            foreach (DictionaryEntry de in dummies)
            {
                if (!FidEquals(de.Key, fid)) continue;
                object dummy = de.Value;
                if (dummy == null) return false;
                return ToBool(SafeField(dummy, "m_IsAlive"));
            }
            return false; // key gone -> treat as dead.
        }

        private static bool FidEquals(object a, object b)
        {
            if (a == null || b == null) return false;
            int? at = ToInt(SafeField(a, "m_TurnIndex"));
            int? ap = ToInt(SafeField(a, "m_PhotonID"));
            int? bt = ToInt(SafeField(b, "m_TurnIndex"));
            int? bp = ToInt(SafeField(b, "m_PhotonID"));
            return at.HasValue && bt.HasValue && at.Value == bt.Value
                   && (ap ?? 0) == (bp ?? 0);
        }

        private static object BattleStanceButtons()
        {
            object ui = StaticInstance("FTKUI");
            if (ui == null) return null;
            return SafeField(ui, "m_BattleStanceButtons");
        }

        private static string FidLabel(object fid)
        {
            if (fid == null) return "?";
            return (ToInt(SafeField(fid, "m_TurnIndex")) ?? -1) + ":" + (ToInt(SafeField(fid, "m_PhotonID")) ?? 0);
        }

        // ------------------------------------------------------------- reflection utils -----------------

        private static object StaticInstance(string typeName)
        {
            Type t = AccessTools.TypeByName(typeName);
            if (t == null) return null;
            PropertyInfo pi = t.GetProperty("Instance", Reflect.All);
            if (pi != null) return pi.GetValue(null, null);
            FieldInfo fi = t.GetField("Instance", Reflect.All);
            if (fi != null) return fi.GetValue(null);
            return null;
        }

        private static object SafeField(object obj, string name)
        {
            if (obj == null) return null;
            try { return Reflect.GetField(obj, name); } catch { return null; }
        }

        private static object SafeProp(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                for (Type cur = obj.GetType(); cur != null; cur = cur.BaseType)
                {
                    PropertyInfo pi = cur.GetProperty(name, Reflect.All | BindingFlags.DeclaredOnly);
                    if (pi != null && pi.CanRead) return pi.GetValue(obj, null);
                }
            }
            catch { }
            return null;
        }

        private static object SafeInvoke(object obj, string name)
        {
            if (obj == null) return null;
            try { return Reflect.Invoke(obj, name); } catch { return null; }
        }

        private static object SafeInvokeArgs(object obj, string name, Type[] sig, object[] args)
        {
            if (obj == null) return null;
            try { return Reflect.InvokeArgs(obj, name, sig, args); } catch { return null; }
        }

        private static bool ToBool(object o) { return o is bool && (bool)o; }

        private static int? ToInt(object o)
        {
            if (o == null) return null;
            try
            {
                if (o is int) return (int)o;
                if (o is long) return (int)(long)o;
                if (o is short || o is byte) return Convert.ToInt32(o);
                if (o is Enum) return Convert.ToInt32(o);
            }
            catch { }
            return null;
        }
    }
}
