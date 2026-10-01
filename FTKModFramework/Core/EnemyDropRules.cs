using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>Author-supplied chance for one physical item in an admitted enemy reward.</summary>
    public sealed class EnemyDropRule
    {
        public int MinimumDisplayedLevel;
        public int OrdinaryChancePercent;
        public int BossChancePercent;
        public int GuaranteedByOpportunity;
        public string[][] NamedBossGroups;
    }

    internal sealed class EnemyDropOpportunity
    {
        internal string Identity;
        internal string EnemyId;
        internal int DisplayedLevel;
        internal bool NativeDropAdmitted;
    }

    internal sealed class EnemyDropDecision
    {
        internal readonly List<string> AwardAt = new List<string>();
        internal int Misses;
    }

    internal static class EnemyDropRules
    {
        internal static void Validate(EnemyDropRule rule)
        {
            if (rule == null || rule.MinimumDisplayedLevel < 0 || rule.OrdinaryChancePercent < 0 ||
                rule.OrdinaryChancePercent > 100 || rule.BossChancePercent < 0 ||
                rule.BossChancePercent > 100 || rule.GuaranteedByOpportunity < 1 || rule.GuaranteedByOpportunity > 1000000)
                throw new ArgumentException("Invalid enemy drop rule.");
            if (rule.NamedBossGroups == null) return;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string[] group in rule.NamedBossGroups)
            {
                if (group == null || group.Length == 0) throw new ArgumentException("Empty boss group.");
                foreach (string id in group)
                    if (string.IsNullOrEmpty(id) || !seen.Add(id))
                        throw new ArgumentException("Invalid or duplicate boss enemy identity.");
            }
        }

        internal static EnemyDropRule Copy(EnemyDropRule rule)
        {
            Validate(rule);
            EnemyDropRule copy = new EnemyDropRule {
                MinimumDisplayedLevel = rule.MinimumDisplayedLevel,
                OrdinaryChancePercent = rule.OrdinaryChancePercent,
                BossChancePercent = rule.BossChancePercent,
                GuaranteedByOpportunity = rule.GuaranteedByOpportunity
            };
            if (rule.NamedBossGroups != null)
            {
                copy.NamedBossGroups = new string[rule.NamedBossGroups.Length][];
                for (int i = 0; i < rule.NamedBossGroups.Length; i++)
                    copy.NamedBossGroups[i] = (string[])rule.NamedBossGroups[i].Clone();
            }
            return copy;
        }

        internal static EnemyDropDecision Resolve(EnemyDropRule rule, IList<EnemyDropOpportunity> opportunities,
            int misses, Func<int> rollPercent)
        {
            Validate(rule);
            if (rollPercent == null) throw new ArgumentNullException("rollPercent");
            List<EnemyDropOpportunity> eligible = new List<EnemyDropOpportunity>();
            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemyDropOpportunity opportunity in opportunities)
            {
                if (opportunity == null || !opportunity.NativeDropAdmitted ||
                    opportunity.DisplayedLevel < rule.MinimumDisplayedLevel) continue;
                if (string.IsNullOrEmpty(opportunity.Identity) || !identities.Add(opportunity.Identity))
                    throw new ArgumentException("Enemy reward identity must be unique and stable.");
                eligible.Add(opportunity);
            }
            eligible.Sort(delegate(EnemyDropOpportunity x, EnemyDropOpportunity y)
            { return StringComparer.Ordinal.Compare(x.Identity, y.Identity); });
            HashSet<int> usedBossGroups = new HashSet<int>();
            EnemyDropDecision result = new EnemyDropDecision();
            result.Misses = Math.Max(0, misses);
            foreach (EnemyDropOpportunity opportunity in eligible)
            {
                int group = BossGroup(rule, opportunity.EnemyId);
                if (group >= 0 && !usedBossGroups.Add(group)) continue;
                int chance = group >= 0 ? rule.BossChancePercent : rule.OrdinaryChancePercent;
                bool award = result.Misses + 1 >= rule.GuaranteedByOpportunity;
                if (!award)
                {
                    int roll = rollPercent();
                    if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException("rollPercent");
                    award = roll < chance;
                }
                if (award)
                {
                    result.AwardAt.Add(opportunity.Identity);
                    result.Misses = 0;
                }
                else result.Misses++;
            }
            return result;
        }

        private static int BossGroup(EnemyDropRule rule, string enemyId)
        {
            if (rule.NamedBossGroups == null) return -1;
            for (int i = 0; i < rule.NamedBossGroups.Length; i++)
                foreach (string id in rule.NamedBossGroups[i])
                    if (StringComparer.Ordinal.Equals(id, enemyId)) return i;
            return -1;
        }
    }
}
