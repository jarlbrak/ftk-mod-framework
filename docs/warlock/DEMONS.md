# Warlock bound demons

Status: approved design, 2026-10-06. Delivery phase 3 in [Design](DESIGN.md). The reactive familiar is the approved representation; numbers are initial targets.

## The fantasy

In the endgame, a Warlock stops borrowing from their shadow and strikes a bargain with something that answers back. A full set of pact regalia binds one demon. The demon rides along through the overworld and into every fight, visibly beside the Warlock, and joins in whenever the Warlock casts.

## What the engine allows

Facts from [Native baseline](NATIVE-BASELINE.md) section 4:

- Nothing in FTK summons a combatant mid-fight. Enemy minions are fixed at encounter start.
- Both sides are capped at three combatants. Attack sequences carry at most three damage records.
- Every player-side combatant is a networked player with a party slot. There is no ally, companion, or pet concept.
- Usable hooks exist for automatic actions: timeline ticks on any dummy, extra-turn Rush, Interrupt, reflect damage, and attack-time special procs.

## Options considered

| Option | What it means | Verdict |
| --- | --- | --- |
| A. True combatant | The demon takes a combat slot, its own turns, and health | **Rejected.** Needs a networked player identity, a party slot, a diorama position, and AI that targets it. Collides with the three-slot cap and would rewrite combat and co-op. |
| B. Timeline familiar | The demon is an invisible record on the Warlock that acts on its own clock, like a damage-over-time tick in reverse | **Deferred.** Feels most independent, but needs deterministic target choice on every client, its own feedback timing, and careful kill attribution. Revisit after option C ships. |
| C. Reactive familiar | The demon has no turn and acts automatically in response to the Warlock's committed actions | **Recommended.** Every trigger rides an action the authority already resolves, so target, timing, and sync come for free. |
| D. Aura only | The demon is a cosmetic plus passive stat bonuses | **Rejected as the whole design.** Too little demon. Used for the two-piece minor bonuses only. |

## Recommended model: reactive familiar

A bound demon:

- has no turn, health, or combat slot, and cannot be targeted, damaged, stunned, or killed;
- acts only in response to its Warlock's committed actions, or to a defined combat event, with every trigger listed below;
- deals flat damage that ignores armor, like native damage over time, so its output does not need its own roll;
- never makes a direct hit, so Grave Bargain, Thief openings, and on-hit effects do not apply to its damage;
- is bound by equipment, not by a summon action, so there is no per-combat summon state and saving stores only equipment;
- appears only while its set is complete and the wearer is a Warlock.

A non-Warlock wearing a complete demon set receives the two-piece minor bonus only. Two complete sets cannot be worn at once, so a Warlock has at most one demon.

### Set state rules

- Two pieces each of two different sets grant both minor bonuses and no demon.
- Set state is evaluated at combat start and whenever equipment changes. Familiar abilities follow the current state, so removing a piece mid-combat unbinds the demon immediately.
- Per-combat counters (Feast, Stand Between) belong to the Warlock for the whole combat. Unequipping and re-equipping a set does not reset them.
- While the Warlock is defeated, the familiar does nothing. A revived Warlock's familiar resumes with its counters unchanged.
- Familiar triggers never fire for actions committed by anyone other than the bound Warlock.

## Set structure

Each demon set has four pieces: hood, robe, boots, and a binding trinket. The tome is free, so any Grimoire or Hexbook works with any set.

| Pieces worn | Bonus |
| --- | --- |
| 2 | Minor stat bonus from equipment modifiers |
| 4 | The demon is bound: appears and gains its abilities |

This mirrors Guardian set profiles but cannot reuse them directly: Guardian sets require a Guardian class and a fixed six-slot layout. [Gaps](GAPS.md) records the generic set primitive.

## The three demons

### Cinder Imp: Cindercall Regalia

A cheeky imp the size of a cat, perched on the Warlock's shoulder, juggling a coal. Supports the Grimoire.

| Pieces | Bonus |
| --- | --- |
| 2 | +1 Focus capacity, +5 Resistance |
| 4 | **Ember Toss.** When the Warlock's committed action deals a positive direct hit to an enemy that is burning after the hit resolves, the imp deals 6 flat damage to that enemy. Once per action, primary target only. |
| 4 | **Imp's Tithe.** Cinderstorm's Shadow's Due drops from 12% to 9%. |

### Gloam Hound: Gloamhunt Raiment

A lean shadow hound with lantern eyes, sitting at the Warlock's heel. Supports the Hexbook.

| Pieces | Bonus |
| --- | --- |
| 2 | +5 Awareness, +5 Resistance |
| 4 | **Devour.** When a Warlock curse is applied successfully, the hound strips that enemy's positive buffs, using native Debuff behavior. |
| 4 | **Feast.** When an enemy carrying the Warlock's Blight or Rot dies, the Warlock regains 1 Focus, at most twice per combat. |

### Hollow Warden: Hollowward Vestments

A tall hooded shade with no face, hovering behind the Warlock with its arms folded. Supports either path.

| Pieces | Bonus |
| --- | --- |
| 2 | +5 Vitality, +3 Armor |
| 4 | **Shared Burden.** Every Shadow's Due cost is reduced by a quarter: `max(m, ceil(baseCost * 3 / 4))`. |
| 4 | **Stand Between.** Once per combat, the first enemy direct attack that would leave the Warlock below 25% of maximum HP, after armor, resistance, and any Guard reduction, is halved before it applies. If a Paladin's Guard already reduced that hit, Stand Between does nothing and is not spent: the stronger protection applies once. |

## Presentation

| Demon | Placement | Motion | Trigger feedback |
| --- | --- | --- | --- |
| Cinder Imp | Mounted at the shoulder | Static pose acceptable; idle bob is a stretch goal | Native fire hit effect on the target; "Ember 6" combat text |
| Gloam Hound | Seated beside the Warlock, offset from the root | Static pose acceptable | "Devoured" text on buff strip; "Feast +1 Focus" text |
| Hollow Warden | Hovering behind the shoulders, translucent | Static pose acceptable; slow drift is a stretch goal | "Shared Burden" text on reduced costs; "Stand Between" text on the halved hit |

All three are original models under the [art direction](ART-DIRECTION.md). Native imp, hound, and wraith enemy models are reference only. Whether an equipment-bound model can be mounted at the shoulder, root, or back of a native avatar, in both overworld and combat, without disturbing native animation, is an open feasibility check that comes before modeling.

## Co-op

Every trigger rides an authoritative committed action or an authoritative death, so familiar effects resolve in the same order on every client. Flat damage needs no roll. Per-combat counters (Feast, Stand Between) reset at combat end and are owned by the encounter authority. Co-op is unverified until tested.

## Open questions

1. Can a static, equipment-bound familiar model be mounted on a native avatar at the shoulder, root offset, and back in both overworld and combat?
2. Do players want the demon visible in the overworld, or only in combat?
3. Should Stand Between reuse the Guardian damage-reduction path, or does it need its own narrow patch?
4. Would a later timeline familiar (option B) replace or extend these reactive abilities?
5. Can the runtime model route render the Hollow Warden translucent? If not, an opaque shade with a smoky silhouette is the fallback.
