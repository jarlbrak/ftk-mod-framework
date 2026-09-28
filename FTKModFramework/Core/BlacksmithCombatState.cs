using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Native integration supplies the host's timeline entry identity, never an animation counter.
    // State owns only this feature's Armor contribution; native and third-party Armor stay separate.
    internal sealed class BlacksmithCombatState
    {
        private sealed class Actor
        {
            internal int SetHammer, SetSource, Penalty, Temper, TemperTurns;
            internal string TemperAppliedDuring;
            internal bool TemperUsed;
        }
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> currentTurns = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> commits = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> starts = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> ends = new HashSet<string>(StringComparer.Ordinal);
        private Actor Get(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Actor identity required.");
            Actor actor;
            if (!actors.TryGetValue(id, out actor)) { actor = new Actor(); actors.Add(id, actor); }
            return actor;
        }
        internal bool TemperAvailable(string actor) { return !Get(actor).TemperUsed; }
        internal bool TryCommit(string actor, string turn, string action)
        {
            return !string.IsNullOrEmpty(turn) && CurrentTurn(actor) == turn && !string.IsNullOrEmpty(action) &&
                commits.Add(actor + ":" + turn + ":" + action);
        }
        internal string CurrentTurn(string actor)
        {
            string turn; return actor != null && currentTurns.TryGetValue(actor, out turn) ? turn : null;
        }
        internal void SetHammer(string actor, int source, int armor, bool positiveHit)
        {
            if (!positiveHit || armor <= 0) return;
            Actor a = Get(actor); a.SetHammer = armor; a.SetSource = source;
        }
        internal void Overhand(string actor, int penalty)
        {
            // Repeated heavy attacks never erase exposure, even if an external mod adds an action.
            Actor a = Get(actor); a.Penalty = Math.Max(a.Penalty, penalty);
        }
        internal bool Temper(string actor, string target, int armor, string currentTargetTurn)
        {
            Actor source = Get(actor);
            if (source.TemperUsed || armor <= 0) return false;
            source.TemperUsed = true;
            Actor a = Get(target);
            if (armor < a.Temper) return true;
            a.Temper = armor; a.TemperTurns = 2;
            a.TemperAppliedDuring = currentTargetTurn;
            return true;
        }
        internal void BeginTurn(string actor, string turn)
        {
            if (string.IsNullOrEmpty(turn) || !starts.Add(actor + ":" + turn)) return;
            currentTurns[actor] = turn;
            Actor a = Get(actor); a.SetHammer = 0; a.Penalty = 0;
        }
        internal void EndTurn(string actor, string turn)
        {
            if (string.IsNullOrEmpty(turn) || !ends.Add(actor + ":" + turn)) return;
            if (CurrentTurn(actor) == turn) currentTurns.Remove(actor);
            Actor a = Get(actor);
            if (a.Temper > 0 && a.TemperAppliedDuring != turn && --a.TemperTurns <= 0) a.Temper = 0;
        }
        internal int Armor(string actor)
        {
            Actor a = Get(actor); return Math.Max(a.SetHammer, a.Temper) - a.Penalty;
        }
        internal void ObserveEquipment(string actor, int setHammerSource, bool shield)
        {
            Actor a = Get(actor);
            if (!shield || setHammerSource != a.SetSource) a.SetHammer = 0;
            // Committed Temper, its encounter use, and Overhand survive equipment swaps.
        }
        internal void ClearPositive(string actor)
        {
            Actor a = Get(actor); a.SetHammer = a.Temper = 0;
        }
        internal void EndActor(string actor) { ClearPositive(actor); Get(actor).Penalty = 0; currentTurns.Remove(actor); }
        internal bool HasPositive(string actor) { Actor a = Get(actor); return a.SetHammer > 0 || a.Temper > 0; }
        internal bool HasPenalty(string actor) { return Get(actor).Penalty > 0; }
        internal string Status(string actor)
        {
            Actor a = Get(actor);
            List<string> text = new List<string>();
            if (a.SetHammer > 0) text.Add("Set Hammer +" + a.SetHammer + " Armor until next turn");
            if (a.Temper > 0) text.Add("Temper +" + a.Temper + " Armor (" + a.TemperTurns + " turns)");
            if (a.Penalty > 0) text.Add("Overhand -" + a.Penalty + " Armor until next turn");
            return string.Join("; ", text.ToArray());
        }
        internal void ResetEncounter() { actors.Clear(); currentTurns.Clear(); commits.Clear(); starts.Clear(); ends.Clear(); }
    }
}
