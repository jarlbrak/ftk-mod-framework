using System;
using System.Collections.Generic;
using FTKModFramework.Core;

internal static class Program
{
    private static EnemyDropOpportunity O(string identity, string enemy, int level, bool admitted)
    { return new EnemyDropOpportunity { Identity = identity, EnemyId = enemy, DisplayedLevel = level, NativeDropAdmitted = admitted }; }
    private static void Equal(int expected, int actual, string label)
    { if (expected != actual) throw new Exception(label + ": " + actual + " != " + expected); }
    private static void Main()
    {
        EnemyDropRule rule = new EnemyDropRule { MinimumDisplayedLevel = 8, OrdinaryChancePercent = 10,
            BossChancePercent = 50, GuaranteedByOpportunity = 6,
            NamedBossGroups = new[] { new[] { "krakenHead", "krakenTentacle" } } };
        EnemyDropRule frozen = EnemyDropRules.Copy(rule);
        rule.NamedBossGroups[0][0] = "changed";
        rule.OrdinaryChancePercent = 99;
        Equal(10, frozen.OrdinaryChancePercent, "registered rate copy");
        if (frozen.NamedBossGroups[0][0] != "krakenHead") throw new Exception("boss group was not copied");
        rule.NamedBossGroups[0][0] = "krakenHead";
        rule.OrdinaryChancePercent = 10;
        int rolls = 0;
        Func<int> miss = delegate { rolls++; return 99; };
        var excluded = EnemyDropRules.Resolve(rule, new[] { O("1", "ordinary", 7, true),
            O("2", "ordinary", 8, false) }, 0, miss);
        Equal(0, excluded.Misses, "native admission and display boundary");
        Equal(0, rolls, "excluded rolls");
        var sixth = EnemyDropRules.Resolve(rule, new[] { O("z", "ordinary", 8, true),
            O("a", "ordinary", 8, true) }, 4, miss);
        Equal(1, sixth.AwardAt.Count, "sixth opportunity award");
        if (sixth.AwardAt[0] != "z") throw new Exception("stable identity order");
        Equal(0, sixth.Misses, "guarantee reset");
        Equal(1, rolls, "guarantee consumes no RNG");
        var boss = EnemyDropRules.Resolve(rule, new[] { O("b", "krakenTentacle", 9, true),
            O("a", "krakenHead", 9, true), O("c", "ordinary", 8, true) }, 0, miss);
        Equal(2, rolls - 1, "one boss and one ordinary roll");
        Equal(2, boss.Misses, "boss group replaces ordinary chance");
        var bossHit = EnemyDropRules.Resolve(rule, new[] { O("a", "krakenHead", 8, true) }, 0,
            delegate { return 49; });
        Equal(1, bossHit.AwardAt.Count, "boss 50 percent hit");
        var bossMiss = EnemyDropRules.Resolve(rule, new[] { O("a", "krakenHead", 8, true) }, 0,
            delegate { return 50; });
        Equal(0, bossMiss.AwardAt.Count, "boss 50 percent miss");
        rolls = 0;
        var hit = EnemyDropRules.Resolve(rule, new[] { O("x", "ordinary", 8, true) }, 3,
            delegate { rolls++; return 9; });
        Equal(1, hit.AwardAt.Count, "ordinary percentage success");
        Equal(0, hit.Misses, "success reset");
        Console.WriteLine("EnemyDropRules passed");
    }
}
