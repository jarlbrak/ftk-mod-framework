# Thief artifact weapons

Status: three artifact weapon rows and original model/icon assets are authored in the local package. The signatures require the combat capability described in [Combat](COMBAT.md); their live behavior and balance are unverified.

## Common contract

All three use native Artifact rarity, physical damage, damage gain 1, no break chance, no passive stat bonuses, and ordinary Focus use. They are equippable by any class. Their signature effects require Opportunist; other wielders retain normal damage and available non-class actions. They do not grant the Thief class or its skills.

The two dagger artifacts are matched sets occupying both hands, with one damage result per attack. Neither accepts a shield or triggers twice because two blades are visible. They receive Twin Feint on a qualifying partial basic Strike, just like the ordinary paired sets.

All use item levels 4-6, ordinary loot plus night-market and dungeon-merchant eligibility, stock 1 where stocked, and no town-market stock. No new quest, Lore, crafting, class unlock, or guaranteed boss drop is required. Native paired-weapon DLC/ownership gates remain binding; the custom pair animation route needs a verified compatibility declaration. Actual rarity and acquisition must be tested through ordinary game paths; Artifact rarity alone does not create a loot route.

| Artifact | ID | Hands / stat / checks | Base damage | Base gold | Actions |
| --- | --- | --- | ---: | ---: | --- |
| The Skeleton Key | `thief_twins_skeleton_key` | 2 / Speed / 3 | 27 | 650 | Strike 1.00, Pierce 0.75 |
| Candle's End | `thief_twins_candles_end` | 2 / Speed / 3 | 28 | 750 | Strike 1.00, Feint 0.60; no Pierce |
| The Unlost Road | `thief_bow_unlost_road` | 2 / Awareness / 4 | 29 | 700 | Shoot 1.00, Thread the Needle 0.75 |

Damage figures are base values. Level growth and other native modifiers are applied before the signatures calculate their effects. Native criticals use the normal enhanced attack; no artifact grants critical chance or additional critical scaling.

## The Skeleton Key

**Burglar artifact: efficiency through a precise, deliberate hit.**

"A good thief always leaves with something."

A matched long-key/short-pick pair: narrow steel blades, related brass key-bow pommels, dark wrapped grips, and small turquoise insets. The longer blade has one stepped detail near its guard; the shorter is a practical pick-shaped dagger. Both must read as usable knives, with the pair's key-and-lock relationship visible in the item card. The story is an old guildmaster's tool: doors, purses, and carelessness all have a weak point.

### Borrowed Fortune

A damaging Sneak Attack with this weapon returns **exactly one Focus actually spent on that attack**, after damage resolves.

- At least one Focus must have been consumed by this committed attack. Zero spent means zero returned.
- Two or three Focus spent still return only one. Respect current capacity after any intervening stat change.
- The native attack must have all checks successful and leave the target with less HP. A partial result or fully absorbed hit returns nothing.
- One refund per attack, constrained by the class's once-per-own-turn Sneak Attack entitlement. Duplicate damage or UI events cannot grant more.
- The weapon must remain equipped through resolution. Feint, Pierce, DoT, reflection, and other excluded actions cannot trigger it.
- The refund is not a future credit and cannot exceed the Focus spent. No extra roll, kill reset, per-enemy ledger, or between-combat resource is needed.

This signature can recur on later turns. A one-Focus attempt may be resource-neutral on a damaging perfect result, while a fully focused three-check attack still costs two Focus net. It never generates net Focus, but can make a remaining point last longer. That is its intended artifact identity and a required long-fight balance test.

At player level 8 before other modifiers, its current damage is 35. A perfect Sneak Attack is 47.25 before native rounding, criticals, and armor. Nightglass Twins are 38 at that level and reach 51.3, so the artifact pays a continuing damage cost for the refund.

Feedback: **Borrowed Fortune: 1 Focus returned.** Preview text states the condition; it does not subtract the expected refund from the Focus cost before resolution. The ordinary attack animation remains authoritative; a small key-glint effect is optional polish, never a required gameplay signal.

## Candle's End

**Assassin artifact: win the first exchange, with less damage afterward.**

"Leave before the smoke remembers your face."

A matched candle-and-snuffer pair. One blackened leaf blade has an ivory grip and a narrow amber channel; the other has a charcoal grip and a restrained hooked brass guard. Their pommels share the snuffer motif, and their profiles read as related blades in two hands. A restrained amber flash marks the empowered strike. Avoid a persistent flame, smoke cloud, oversized skull, or blade silhouette that resembles an axe.

### Last Light

Once per wielder per combat, a committed Strike with this weapon against a **full-health enemy that qualifies for Sneak Attack** automatically replaces the ordinary +35% bonus with **+75%** on a perfect result. There is no separate toggle or extra action.

- At commitment, target HP must equal its current maximum HP, and the actor must have an unused Sneak Attack entitlement and either an ordinary opening or Prepared.
- The artifact charge is spent on that eligible commitment, even on a partial result or a fully absorbed hit. Canceled targeting spends nothing. An ineligible ordinary attack does not waste the charge.
- A partial result receives ordinary fractional Strike damage. A perfect result uses 1.75 times current weapon damage before the usual critical and mitigation sequence. Do not multiply 1.35 by 1.75 or add both bonuses.
- No automatic kill, extra attack, armor bypass, poison, bleed, or damage over time is added.
- Unequipping, swapping copies, moving the weapon between inventories, or reviving cannot refresh that wielder's spent Last Light in the current combat. Track the budget by wielder and effect, not item instance.
- A different Thief has its own per-combat budget. Normal equipment-transfer action costs remain; this is not an extra turn or an exemption from them.

A full-health enemy before its first turn is the natural target. Ally damage opens a later opportunity but usually removes full health. A near-miss basic Strike can prepare a later attack through Twin Feint; Slip Away can prepare without damaging anyone. An enemy healed to full can qualify later if the charge remains and an opening or Prepared exists. The rule does not rely on a permanent once-injured flag.

At level 8 its current damage is 36. Last Light reaches 63 before native rounding/mitigation, compared with Nightglass Twins' ordinary Sneak Attack at 51.3. Later Sneak Attacks from Candle's End reach 48.6. Over five perfect Sneak Attacks it totals 257.4 versus Nightglass Twins' 256.5; at six it falls behind, 306 versus 307.8, before per-hit rounding and mitigation. The artifact shifts value into the opening and loses its advantage in longer fights. These are arithmetic illustrations, not expected encounter damage.

Feedback: the action preview shows **Last Light ready** only when all commitment conditions hold, then **Last Light spent** for the rest of combat. A miss still shows the spent state. Full health and opening status must be visible without guessing from the blade's glow.

## The Unlost Road

**Scout artifact: commit to the shot, then survive the response.**

"Every path has a way home."

A compact recurved shortbow of weathered dark wood, pale reinforcement strips, and a small brass trail-marker shape at the grip. A split green cloth binding identifies it at distance. Short curved limb tips distinguish it from a longbow. No hanging object should obstruct the string or require extra simulated physics.

### Loose and Leave

A damaging Sneak Attack grants **+8 Evasion points** after it resolves, until the start of the wielder's next scheduled turn.

- Add 0.08 to the ordinary native evade modifier, subject to the native calculation and ceiling. This is eight percentage points, not an eight-percent multiplier or a guaranteed dodge.
- Reapplication refreshes expiry, never magnitude. The class's once-per-turn entitlement prevents repeated ordinary triggering.
- Ends immediately on unequip, incapacity, combat exit, or the next own turn boundary, including a skipped turn. Equipment reconstruction must not duplicate the modifier.
- No initiative bonus, extra move, enemy accuracy penalty, targeting immunity, or protection from damage over time is granted.
- A perfect enemy attack or other native rule that bypasses evasion continues to do so. Do not override native dodge eligibility.
- A partial result, fully absorbed attack, or Thread the Needle grants nothing. A killing Sneak Attack can grant the buff because another enemy may still act.

At level 8 current damage is 37 and Sneak Attack reaches 49.95 before rounding/mitigation. Farstep reaches 51.3 and has stronger penetration; Blackthorn reaches 54 with fewer actions. The artifact pays damage and action-quality costs for protection without a shield. Bows do not receive Twin Feint; the defensive signature is this artifact's particular reason to invest in Awareness.

Feedback: **Loose and Leave: +8 Evasion until your next turn.** Use a small boot-and-arrow status icon with a clear expiry description. The effect never makes the character transparent or pretends enemies cannot target them.

## Why these remain alternatives

Both dagger artifacts require the two-handed paired loadout; no one-handed artifact variant is included. The Skeleton Key rewards managing Focus over repeated openings. Candle's End rewards initiative and target selection. The Unlost Road rewards a bow user who accepts lower damage to survive the enemy response. Ordinary Locksmith preparation and Wayfarer penetration remain stronger versions of those weapon actions; ordinary Nightblade remains the raw sustained-damage choice. No artifact requires matching armor, another artifact, or a Paladin in the party.

Before accepting balance, test each against its strongest ordinary alternative in short and long encounters, with low and high armor, scarce and abundant Focus, multiple Thieves, a solo Thief, and another class using its base weapon. Test charge/refund/buff lifecycle separately from art and acquisition.
