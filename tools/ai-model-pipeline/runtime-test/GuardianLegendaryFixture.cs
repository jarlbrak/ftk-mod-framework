using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    readonly HashSet<string> guardianLegendaryClaims = new HashSet<string>();

    static CharacterDummy GuardianLegendaryHero(EncounterSession encounter, int id)
    {
        CharacterDummy found = null;
        foreach (CharacterDummy candidate in encounter.m_PlayerDummies.Values)
            if (candidate != null && candidate.GetInstanceID() == id)
            {
                if (found != null) throw new InvalidOperationException("Ambiguous hero pin.");
                found = candidate;
            }
        if (found == null || found.m_CharacterOverworld == null || !found.m_CharacterOverworld.IsOwner
            || found.m_CharacterOverworld.m_CharacterStats == null)
            throw new InvalidOperationException("Exact owned current encounter hero required.");
        return found;
    }

    static JObject GuardianLegendaryView(CharacterDummy hero)
    {
        CharacterStats stats = hero.m_CharacterOverworld.m_CharacterStats;
        Type runtime = GuardianFixtureRuntime();
        FieldInfo field = runtime.GetField("Legendary", Statics);
        object legendary = field == null ? null : field.GetValue(null);
        return new JObject {
            {"dummyInstanceId", hero.GetInstanceID()}, {"heroInstanceId", hero.m_CharacterOverworld.GetInstanceID()},
            {"alive", hero.m_IsAlive}, {"hp", hero.GetCurrentHealth()},
            {"focus", stats.m_FocusPoints}, {"maxFocus", stats.MaxFocus}, {"spentFocus", stats.SpentFocus},
            {"poisonLevels", stats.m_PoisonLvl},
            {"stunned", hero.m_SufferingProficiencies.ContainsKey(ProficiencyBase.Category.Stunned)},
            {"dazed", hero.m_SufferingProficiencies.ContainsKey(ProficiencyBase.Category.Dazed)},
            {"activeCurses", stats.m_ActiveCurses.Count},
            {"reckoningCharged", legendary == null ? new JValue((object)null) :
                new JValue((bool)GuardianQuery(legendary.GetType(), legendary, "IsCharged", GuardianFixtureIdentity(hero)))}
        };
    }

    JObject GuardianLegendaryFixture(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "encounterInstanceId", "actorInstanceId",
            "targetInstanceId", "focusDeficit", "poisonLevels");
        CatalogNoLinks(root);
        RequireSinglePlayer();
        string action = Str(command, "action");
        if (action != "inspect" && action != "setup") throw new ArgumentException("Action must be inspect or setup.");
        EncounterSession encounter = EncounterSession.Instance;
        if (encounter == null || !encounter.m_IsInCombat ||
            encounter.GetInstanceID() != LeaseObservationPin.ExactId(command, "encounterInstanceId", true))
            throw new InvalidOperationException("Exact active encounter required.");
        CharacterDummy actor = GuardianLegendaryHero(encounter, LeaseObservationPin.ExactId(command, "actorInstanceId", true));
        CharacterDummy target = GuardianLegendaryHero(encounter, LeaseObservationPin.ExactId(command, "targetInstanceId", true));
        if (actor == target) throw new ArgumentException("Distinct actor and ally required.");
        if (action == "inspect")
        {
            if (command["focusDeficit"] != null || command["poisonLevels"] != null)
                throw new ArgumentException("Inspection accepts identity pins only.");
            return new JObject {{"ok", true}, {"fixture", "guardian-legendary"}, {"action", action},
                {"encounterInstanceId", encounter.GetInstanceID()}, {"actor", GuardianLegendaryView(actor)},
                {"target", GuardianLegendaryView(target)}, {"scope", "Read-only live native Focus, status and legendary charge observation."}};
        }
        uiBattleStanceButtons buttons = FTKUI.Instance.m_BattleStanceButtons;
        if (buttons == null || !buttons.m_Initialized || buttons.CombatCow != actor.m_CharacterOverworld
            || actor.m_CharacterDummyFSM == null || actor.m_CharacterDummyFSM.ActiveStateName != "Wait For Stance"
            || !actor.m_IsAlive || !target.m_IsAlive || actor.m_DidFlee || target.m_DidFlee
            || actor.GetCurrentHealth() <= 0 || target.GetCurrentHealth() <= 0
            || !(bool)GuardianQuery(GuardianFixtureRuntime(), null, "IsGuardian", actor)
            || !(bool)GuardianQuery(GuardianFixtureRuntime(), null, "CanAct", actor))
            throw new InvalidOperationException("Exact living owned Guardian stance and living ally required.");
        if (guardianDamageReceipt != null && guardianDamageReceipt.Policy.Active)
            throw new InvalidOperationException("Finish or disarm the active damage fixture first.");
        if (command["focusDeficit"] == null || command["focusDeficit"].Type != JTokenType.Integer
            || command["poisonLevels"] == null || command["poisonLevels"].Type != JTokenType.Integer)
            throw new ArgumentException("Explicit integer focusDeficit and poisonLevels required.");
        int deficit = (int)command["focusDeficit"], poison = (int)command["poisonLevels"];
        CharacterStats stats = target.m_CharacterOverworld.m_CharacterStats;
        if (deficit < 0 || deficit > 3 || deficit > stats.MaxFocus || poison < 0 || poison > 1 || deficit + poison == 0)
            throw new ArgumentException("Focus deficit must be 0..3 and poison 0..1, with at least one fixture effect.");
        int focus = deficit == 0 ? stats.m_FocusPoints : stats.MaxFocus - deficit;
        if (focus > stats.m_FocusPoints || stats.SpentFocus != 0 || stats.m_PoisonLvl != 0
            || target.m_SufferingProficiencies.Count != 0 || stats.m_ActiveCurses.Count != 0
            || poison > 0 && stats.HasImmunity(ProficiencyBase.Category.Poison))
            throw new InvalidOperationException("Unspent Focus, no existing statuses and no Focus grant or poison immunity required.");
        JObject before = GuardianLegendaryView(target);
        string claim = encounter.GetInstanceID() + ":" + target.GetInstanceID();
        if (!guardianLegendaryClaims.Add(claim))
            throw new InvalidOperationException("This ally fixture was already attempted in this encounter.");
        // Claim before native mutation. Setup only creates observable inputs; native Guard and
        // incoming-hit playback remain separate actions performed by the caller.
        if (focus != stats.m_FocusPoints) stats.UpdateFocusPoints(focus - stats.m_FocusPoints, true);
        if (poison != 0) stats.SetPoison(poison, false, false);
        JObject after = GuardianLegendaryView(target);
        bool applied = stats.m_FocusPoints == focus && stats.m_PoisonLvl == poison;
        return new JObject {{"ok", applied}, {"fixture", "guardian-legendary"}, {"action", action},
            {"encounterInstanceId", encounter.GetInstanceID()}, {"actor", GuardianLegendaryView(actor)},
            {"before", before}, {"after", after},
            {"scope", "Explicit one-use native Focus reduction and Poison setup. No Guard, attack, item grant or Guardian state mutation. No automatic restoration; discard this isolated trial instead of saving fixture state."}};
    }
}
