using System;
using System.Collections;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    internal static class EnemyDropRuntime
    {
        private sealed class Pending
        {
            internal EncounterSessionData Session;
            internal int Index;
            internal readonly Dictionary<int, int> Rewards = new Dictionary<int, int>();
            internal readonly Dictionary<int, int> NextMisses = new Dictionary<int, int>();
            internal bool Committed;
        }

        private static readonly Dictionary<int, EnemyDropRule> Rules = new Dictionary<int, EnemyDropRule>();
        private static Pending active;
        private static Pending prepared;
        internal static bool HasRegistrations { get { return Rules.Count != 0; } }

        internal static void Register(int itemId, EnemyDropRule rule)
        {
            EnemyDropRules.Validate(rule);
            if (Rules.ContainsKey(itemId)) throw new ArgumentException("Enemy drop rule already registered for item.");
            Rules.Add(itemId, EnemyDropRules.Copy(rule));
        }

        internal static void Clear()
        {
            Rules.Clear();
            active = null;
            prepared = null;
        }

        internal static object Begin(EncounterSessionMC session, string[] enemies)
        {
            Pending previous = active;
            active = null;
            if (session == null || enemies == null || enemies.Length == 0 || Rules.Count == 0 ||
                !PhotonNetwork.isMasterClient || !session.IsMasterClient) return previous;
            Pending pending = new Pending();
            pending.Session = session.m_SessionData;
            pending.Index = session.m_ActiveEncounterIndex;
            if (prepared != null && object.ReferenceEquals(prepared.Session, pending.Session) &&
                prepared.Index == pending.Index)
            {
                active = prepared;
                return previous;
            }
            try
            {
                List<EnemyDropOpportunity> opportunities = new List<EnemyDropOpportunity>();
                List<string> ordered = new List<string>(enemies);
                ordered.Sort(StringComparer.Ordinal);
                string previousEnemy = null;
                int ordinal = 0;
                foreach (string enemy in ordered)
                {
                    if (enemy != previousEnemy) { ordinal = 0; previousEnemy = enemy; }
                    FTK_enemyCombat row = FTK_enemyCombatDB.GetDB().GetEntryByStringID(enemy);
                    if (row == null) continue;
                    opportunities.Add(new EnemyDropOpportunity { Identity = enemy + "/" + ordinal++,
                        EnemyId = enemy, DisplayedLevel = row.GetEnemyLevelDisplay(), NativeDropAdmitted = true });
                }
                Campaign.EnsureStore();
                if (GameLogic.Instance == null ||
                    !GameLogic.Instance.GetQuestTable().ContainsKey(Campaign.SentinelKey)) return previous;
                List<int> ruleIds = new List<int>(Rules.Keys);
                ruleIds.Sort();
                foreach (int ruleId in ruleIds)
                {
                    EnemyDropRule rule = Rules[ruleId];
                    string key = "ftkmf/enemy-drop/" + ruleId + "/misses";
                    EnemyDropDecision decision = EnemyDropRules.Resolve(rule, opportunities,
                        Campaign.GetFlag(key), delegate { return UnityEngine.Random.Range(0, 100); });
                    pending.Rewards.Add(ruleId, decision.AwardAt.Count);
                    pending.NextMisses.Add(ruleId, decision.Misses);
                }
                prepared = pending;
                active = pending;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Enemy drop reward skipped: " + ex);
            }
            return previous;
        }

        internal static void AddRewards(ArrayList loot)
        {
            if (active == null || loot == null || !PhotonNetwork.isMasterClient) return;
            int originalCount = loot.Count;
            try
            {
                List<int> itemIds = new List<int>(active.Rewards.Keys);
                itemIds.Sort();
                foreach (int itemId in itemIds)
                    for (int i = 0; i < active.Rewards[itemId]; i++)
                        loot.Add(((FTK_itembase.ID)itemId).ToString());
                if (!active.Committed)
                {
                    foreach (int itemId in itemIds)
                        Campaign.SetFlag("ftkmf/enemy-drop/" + itemId + "/misses", active.NextMisses[itemId]);
                    active.Committed = true;
                }
            }
            catch
            {
                while (loot.Count > originalCount) loot.RemoveAt(loot.Count - 1);
                throw;
            }
        }

        internal static void End(object previous) { active = previous as Pending; }
    }

    [HarmonyPatch(typeof(EncounterSessionMC), "CombatPlayerVictory")]
    internal static class EnemyDropVictoryPatch
    {
        private static void Prefix(EncounterSessionMC __instance, string[] _enemyDrop, out object __state)
        { __state = EnemyDropRuntime.Begin(__instance, _enemyDrop); }
        private static Exception Finalizer(Exception __exception, object __state)
        { EnemyDropRuntime.End(__state); return __exception; }
    }

    [HarmonyPatch(typeof(GameLogic), "FillLootDropList")]
    internal static class EnemyDropLootPatch
    {
        private static void Postfix(ArrayList _arrayList) { EnemyDropRuntime.AddRewards(_arrayList); }
    }
}
