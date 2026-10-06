# Warlock combat rules

Status: design rules accompanying [the class design](DESIGN.md), 2026-10-06. All percentages and tick values are initial design decisions. Bound demon rules are in [Demons](DEMONS.md). [Validation](VALIDATION.md) will separate tested behavior from remaining live gates.

## 1. Eligible actions and ownership

Warlock actions are authored proficiencies on Warlock tomes, granted through class-gated weapon proficiencies. They appear only when a Warlock holds a Warlock tome. A non-Warlock holding the same tome sees its basic attack, Umbral Bolt, and nothing else. Swapping tomes mid-combat cannot leave a stale Warlock action usable. Native tomes keep their native actions and gain none of these.

A **Pact action** is any Warlock action carrying a Shadow's Due cost. A **direct hit** is the damage a committed action deals at resolution, after native partial-success scaling, critical rules, armor, and resistance. Damage over time, reflection, splash to other targets, and familiar damage are not direct hits of the action.

## 2. Shadow's Due

Each Pact action declares a percentage `p` and a minimum `m`. At commit, the encounter authority computes:

```
cost = max(m, ceil(maxHP * p / 100))
```

using synchronized maximum HP at that moment, in integers.

- If current HP is **at or below** `cost`, the action cannot be committed. The cost therefore never defeats the Warlock.
- Targeting that is cancelled before commit costs nothing.
- The cost is charged once, at commit, through the native synchronized health path. It is not damage: armor, resistance, Guard, Slip Away, Divine Intervention, reflection, and evasion never apply to it, and it never triggers on-hit effects.
- A miss, a partial roll, an immune target, and a non-lethal hit all still pay.
- Healing received later in the turn does not refund it.
- A multi-target Pact action pays once, not per target.

| Action | `p` | `m` |
| --- | ---: | ---: |
| Cinderbrand | 5 | 1 |
| Hollow Fright | 6 | 1 |
| Hexfire | 10 | 2 |
| Cinderstorm | 12 | 2 |
| Blight | 4 | 1 |
| Rot | 6 | 1 |

Final-band tome alternatives and artifacts may change `p`, `m`, multipliers, and burn, curse, or affliction tiers. They never remove the at-or-below refusal rule.

## 3. Grave Bargain

When a Pact action's direct hit defeats **its primary target**, the Warlock regains exactly the HP that action charged, capped at maximum HP.

- Splash or Aoe damage defeating other enemies does not qualify, even from the same action.
- A burn or affliction tick, an ally's attack, or a familiar's ember defeating the target later does not qualify.
- The refund reads the authoritative defeat result of that action, never animation state.
- One refund per committed action.

## 4. Grimoire actions

| Action | Target | Multiplier | Extra effect |
| --- | --- | ---: | --- |
| Umbral Bolt | One enemy | 1.0x | None |
| Hexfire | One enemy | 1.35x | None |
| Cinderbrand | One enemy | 0.6x | Native burn at the tome's tier |
| Cinderstorm | Target and every other living enemy (native `Aoe`) | 0.7x | Native burn at the tome's tier on each enemy hit |

**Burn** is the native Fire damage over time, cloned from native fire rows. It ticks on the combat timeline, deals flat damage ignoring armor, and is blocked by native fire immunity. Reapplying burn replaces the existing record; it never stacks. The direct hit still lands on a fire-immune enemy.

| Tome band | Burn ticks | Damage per tick | Total |
| --- | ---: | ---: | ---: |
| 1 and 2 | 6 | 2 | 12 |
| 3 | 6 | 4 | 24 |
| 4 and final | 6 | 6 | 36 |

Burn uses the native tick rate (quickness 2.0, one tick every 0.5 timeline units). The proficiency is applied only when the hit deals positive damage, matching native behavior.

## 5. Hollow Fright

Available on Warlock tomes of both paths from band 2. One enemy, 0.5x magic. On a perfect roll only, a 50% chance to apply native Daze, which pushes the target's next turn later on the timeline. Native stun immunity blocks it. Daze is cloned from the native daze row; its timeline push is not changed.

## 6. Curses

Curses are free Hexbook actions that deal no damage. Curse tier follows tome band as burn does: bands 1 and 2 are tier 1, band 3 is tier 2, band 4 and final are tier 3. Lethargy and Feebleness have a single native value and do not scale; Hexbooks carrying them compensate elsewhere, as set out in [Equipment](EQUIPMENT.md). Each clones a native player-inflictable debuff and is marked Harmless so it applies without a damaging hit. The roll uses the tome's native Intelligence checks; a perfect roll applies the curse, anything less fails. Each curse reuses its native icon and arrow presentation.

| Curse | Native template family | Value by tier (1, 2, 3) | Duration (timeline units) |
| --- | --- | --- | ---: |
| Curse of Frailty | Armor down (`magicArmorDown`) | -10, -20, -30 | 2.5 |
| Curse of Ruin | Resistance down (`magicResistDown`) | -10, -20, -30 | 2.5 |
| Curse of Lethargy | Speed down (`magicSpeedDown`) | -0.25 | 4 |
| Curse of Feebleness | Attack down (`orbAttackDown`) | -0.25 | 2.2 |

Each Hexbook carries one curse, so the curse is a tome choice. Different curses on one enemy coexist because they use different native categories. Reapplying the same curse refreshes it. Native Debuff effects that strip enemy buffs do not remove curses, because curses carry negative values.

Curse of Ruin deliberately does not grant a Hexfire bonus. Mark-then-cash-out belongs to the Paladin's Censure and Smite.

## 7. Siphon Soul

A free Hexbook action. One enemy, 0.6x magic. The Warlock heals `round(directHitDamage * 0.5)` through native LifeDrain, capped at maximum HP. A missed or fully absorbed hit heals nothing. Siphon Soul is not a Pact action, so Grave Bargain never applies to it.

## 8. Afflictions

Blight and Rot are framework-owned damage over time on enemies. They exist because native Curse, Disease, and Poison records do nothing to enemies. Each uses a native category as its storage key so it coexists with burn, bleed, and curses, but its behavior is owned by the framework.

| Affliction | Storage category | Applied by | Ticks | Tick interval | Damage per tick by tier (1, 2, 3) |
| --- | --- | --- | ---: | ---: | --- |
| Blight | Curse | Blight: Harmless, no direct hit | 4 | 1.25 units | 4, 7, 10 (totals 16, 28, 40) |
| Rot | Disease | Rot: 0.4x hit; applied only if the hit deals positive damage | 6 | 2.0 units | 3, 5, 7 (totals 18, 30, 42) |

Tier follows tome band as burn does: bands 1 and 2 are tier 1, band 3 is tier 2, band 4 and final are tier 3. Rot appears from band 3. Phase 1 Hexbooks ship without Blight; the phase 2 package update adds Blight to them without changing their IDs, so saved copies gain the action.

- Ticks deal flat damage ignoring armor, through the same path as native damage over time.
- Blight's roll uses native checks; a perfect roll applies it. Rot applies on any damaging hit.
- Reapplying refreshes the record; it never stacks. A Warlock's Blight replaces another Warlock's Blight on the same enemy, and ownership transfers to the newer caster.
- Afflictions record the applying Warlock for Soul Harvest.
- Release one has no affliction immunity. Bosses are affected normally.
- Afflictions end when their ticks run out, when the enemy dies, or when combat ends. They do not persist into later encounters.
- Each enemy HUD shows a framework-owned Blight or Rot icon. Native icons are not reused because no native enemy icon means "cursed" or "diseased".

## 9. Soul Harvest

When an enemy dies, for any reason, while carrying a living Blight or Rot record owned by a Warlock, that Warlock regains `max(1, ceil(maxHP * 5 / 100))` HP, capped at maximum HP. An enemy pays out once, even if it carried both afflictions. The owner must be alive and in the combat. Ally kills qualify; that is the point.

## 10. Timing and resets

- Shadow's Due and Grave Bargain settle within one committed action. No state crosses a turn boundary.
- Burn, curses, and afflictions live on the enemy as native-style records and end with the enemy or the combat.
- Soul Harvest tracks only which dead enemies have already paid out, reset at combat end.
- Save, resume, and reconnect between fights need no Warlock state. Mid-combat resume follows native record restoration; whether framework affliction records survive it is a validation gate.

## 11. Interactions

- **Thief:** burn, afflictions, and familiar damage do not create Thief openings, matching Thief rules for damage over time and companion damage. Warlock direct hits do.
- **Paladin:** Guard and Divine Intervention never reduce Shadow's Due. Mercy heals and Paladin protection keep a Warlock's HP budget healthy.
- **Herbalist and party healing** restore HP normally.
- **Poison on the Warlock** stacks with self-cost. The at-or-below refusal still prevents self-defeat by cost alone.

## 12. Feedback

- Every Pact action tooltip states its cost and Grave Bargain: "Costs 10% max HP. Defeat the target to be repaid."
- Combat text: "Shadow's Due -N" on commit, "Debt repaid +N" on Grave Bargain, "Soul Harvest +N" on harvest.
- An unaffordable Pact action is refused with a message. Greying it out or printing its cost on the button needs a native UI hook and is not required for release one.
- Blight and Rot show their own enemy icons and in-world effects. Burn and curses use native presentation.

## 13. Co-op determinism

- No new randomness. Daze, curse success, and Blight application use native roll and proc paths.
- Costs use integer math from synchronized maximum HP and are charged exactly once by the authority. Peers replaying the outcome must not re-apply cost or refund.
- Refunds and harvests read authoritative defeat results.
- Affliction ticks are flat values with no rolls, so each client's local tick produces the same result. Death from a tick follows the native owner-sent enemy death path.
- Co-op remains unverified until tested and must be labeled that way in every release.
