# Warlock native baseline

Status: read-only design evidence, 2026-10-06. This records installed-game facts the [Warlock design](DESIGN.md) depends on. It contains findings, not native source, asset bytes, or a gameplay test.

Authority: installed original FTK `Assembly-CSharp.dll`, SHA-256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`, the same build as the [Paladin baseline](../paladin/NATIVE-BASELINE.md). Code facts come from a fresh ILSpy decompile kept outside the repository. Table values come from `sharedassets1.assets` and `resources.assets`, decoded with the decompiled field order.

Tags: **[V]** verified from the assembly, **[A]** decoded from serialized assets, **[I]** inference that still needs a targeted check.

## 1. Class rows and casters

[V] Classes are `GridEditor.FTK_playerGameStart` rows in `FTK_playerGameStartDB`. Fields include `_toughness` (Strength), `_fortitude` (Intelligence), `_awareness`, `_talent`, `_quickness` (Speed), `_vitality`, `_startinggold`, `_basefocus`, `m_PrimaryWeaponStat`, the 25-flag `m_CharacterSkills`, `m_StartWeapon`, `m_StartItems[]`, `m_DLC`, and `SkinType`. Intelligence is `SkillType.fortitude`. `SkinType` includes `Undead` and `Demon`.

| Class | STR | INT | AWR | TAL | SPD | VIT | Total | Focus | Skills | Start |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
| scholar | 42 | 78 | 66 | 70 | 70 | 60 | 386 | 4 | Refocus | spellbook, scrollteleport |
| herbalist | 44 | 76 | 70 | 58 | 64 | 52 | 364 | 3 | PartyHeal, FindHerb | staffCane, herbGodsbeard1 |
| astronomer | 50 | 76 | 68 | 46 | 62 | 74 | 376 | 4 | SupportRange, BlackHole | wandAstronomer, trinketSeeingAstronomer, shieldAstronomer |
| monk | 74 | 74 | 60 | 48 | 60 | 62 | 378 | 3 | PartyHeal, Discipline | bladeWood, herbFocus1 |

[A] Row values are stored as fractions (0.78) and shown here as percentages. [V] Refocus gains 1 Focus with probability `Fortitude * 0.65`, not while poisoned or in rain outside a dungeon.

[A] Starting caster weapons: `spellbook` 3 checks, 6 damage, magic, Intelligence. `staffCane` 3 checks, 6 damage. `wandAstronomer` 1 check, 5 damage.

## 2. Damage, targeting, and costs

- [V] `FTK_weaponStats2.DamageType` is `none`, `physical`, or `magic`. There is no fire or shadow element. Fire and shadow are presentation and status choices, not damage types.
- [V] `FTK_proficiencyTable.m_Target` is `None`, `Splash` (left and right neighbors), `Aoe` (every other living enemy), `PickFriendly`, or `OthersFriendly`. `m_DmgMultiplier` and `m_SlotOverride` tune an action.
- [V] The only native self-cost besides Focus is `m_Suicide`, which removes all current HP. There is no partial HP cost.
- [V] `FTK_characterModifier.m_HealthRegen` is added at end of turn and is signed. [I] A negative value would drain HP each turn.
- [V] LifeDrain heals the attacker `round(damageDealt * m_CustomValue)`. Native player rows include `magicDrain` and `magicDrainGroup`.

## 3. Status effects on enemies

[V] Records live in `m_SufferingProficiencies`, keyed by `ProficiencyBase.Category`. Reapplying a category replaces its record. Effects of the same category never stack. Different categories coexist.

[V] `ProficiencyBase.AddToDummy` creates a record with `m_Count = m_RepeatCount` and `m_Time = 1 / m_Quickness`. `EncounterSession.UpdateTime_CR` calls `CharacterDummy.UpdateProficiency(elapsed)` on every dummy as the timeline advances. On expiry the record runs `ApplyDamage`, which calls `EnemyDummy.TakeSecondaryDamage(m_DamagePerAttack)` (flat damage, ignoring armor), then `ProficiencyEvent`, decrements its count, and ends at zero.

[V] A proficiency is dropped when its hit deals zero damage unless the row is marked Harmless.

| Category | When a player inflicts it on an enemy | Evidence |
| --- | --- | --- |
| Fire | Works. Native burn damage over time; removes Ice; blocked by `m_ImmuneFire` | [V], [A] fire rows q 2.0, 6 ticks of 2, 4, or 6 |
| Bleed | Works. Damage over time; blocked by `m_ImmuneBleed` unless Wet | [V], [A] bleed rows q 0.55, 3 ticks of 8, 16, or 24 |
| Curse | No effect. `ProficiencyCurse.AddToDummy` returns for `EnemyDummy` | [V] |
| Disease | No effect | [V] |
| Poison | No effect. Requires an overworld character | [V] |
| Acid | No effect. Equipment destruction returns nothing for enemies | [V] |
| Death | Works and kills outright on its tick, with no boss guard | [V]. **Never give players a native Death row.** |
| Stunned, Dazed | Work. Push the target's next turn on the timeline | [V], [A] stun cv 1.6, daze cv 0.3 to 0.7 |
| Debuff | Works instantly. Strips non-negative Armor, Resist, Evade, Attack, Time, Taunt, Reflect, and Protect records | [V], [A] repeat 0 |
| Scare, Confuse | Record and HUD icon appear, but `EnemyDummy.EngageAttack` ignores them | [V], [I] PlayMaker graphs not inspected |
| Darkness | Only penalizes player rolls | [V] |
| LifeDrain | Heals the attacker; adds no record | [V] |

### Player-inflictable enemy debuffs

[A] Duration is `1 / q` timeline units with one repeat.

| Effect | Rows | Value | Duration |
| --- | --- | --- | --- |
| Armor down | `magicArmorDown`, `magicArmorDown2`, `magicArmorDown3` | -10, -20, -30 | 2.5 |
| Resistance down | `magicResistDown`, `magicResistDown2`, `magicResistDown3` | -10, -20, -30 | 2.5 |
| Speed down | `magicSpeedDown` | -0.25 | 4 |
| Attack down | `orbAttackDown` | -0.25 | 2.2 |
| Evasion down | `orbEvadeDown` and group variants | -1.0 | varies |

### Enemy status presentation

[V] `uiEachEnemyHud.RefreshStatusHudIcons` toggles a fixed set of images: Burning, Bleeding, Stunned, Shocked, Frozen, Scared, DeathMarked, Wet, the up and down arrows for each modifier, and immunity icons. There is no category-to-icon table. A new enemy icon needs a patch and a new image. In-world effects come from `CharacterDummyStatusFX.m_FollowFx[Category]`. Reusing a native category reuses its icon and effect.

## 4. Combatants and summoning

- [V] Nothing in the game summons a combatant mid-fight. `EncounterSession.InitEnemyDummiesForCombat` runs only at encounter or room start. Minion IDs such as `minionWraith` are ordinary roster entries.
- [V] `GameFlowMC.gMaxPlayers = 3` and `gMaxEnemies = 3`. `PlayAttackSequence` carries at most three damage records.
- [V] Every player-side combatant is an `FTKPlayerID` with a Photon ID that resolves through `GetCow()`. Every timeline entry requires a real dummy. Enemy AI targets only `m_AllCombtatantsAlive`.
- [V] There is no ally, companion, familiar, pet, or summon concept on the player side. Hildebrant is a talking head.
- [V] Automatic-action hooks exist: timeline ticks on any dummy, `Proficiency_RushInterrupt.Rush` (an extra turn at time zero), Interrupt (resets a target's turn time), reflect damage in the `DummyDamageInfo` constructor, and attack-time special procs (`CriticalStrike`, `CalledShot`, `Justice`).

## 5. Fire and demon content

[A] Native fire actions on player weapon prefabs include `firebolt1` and `firebolt2` (staffFire), `firestorm1` to `firestorm3` (fire tomes), `fireInterrupt1` and `fireInterrupt2`, `fireSmokeWall`, and `orbFire1` to `orbFire3` (Aoe, magic, 2, 4, or 6 per tick). `demonBlade` carries `bladeDemonFire` (Aoe, magic, x0.4). `boneDemon` carries `demonBlast` (Fire, 3 ticks of 4, x1.25) and `stunDaze`.

[A] Demon-themed enemy models: `enImpA` to `enImpD`, `enImpWizardA` and `B`, `enDemonA` and `B`, `enHellHoundA` and `B`, `enChaosWolf`, `enChaosSkull`, `enChaosBeast`, `enWraith`, `enWraithMinion`, and `enLichA`. Effects include `fxHitImpFire`, `ImpFireBomb`, and `ImpSlowGroup`. `Player_Demon` and `Player_DemonF` exist; their use is unverified.

[V] Reusable native apparel includes `armorCloak4` "Shadow Cloak", `helmetArcher4` "Shadow Hood", `bootsLight4` "Shadow Boots", `helmetDeathKnight` "Fallen Helm", and the pumpkin lore set (`loreArmorPumpkin` "Midnight Robes"). No lantern, candle, or skull item exists.

## Open checks

1. Whether the native `spellbook` is one-handed or two-handed, and whether slot overrides behave the same on books.
2. The synchronized self-HP mutation method and whether a 1 HP result triggers death or resist-death side effects.
3. The defeat field on `DummyDamageInfo` used for refunds.
4. Whether `UpdateProficiency` ticks records of every category, including a framework-owned Curse or Disease record on an enemy.
5. Which native enemies carry `m_ImmuneFire` and `m_ImmuneBleed`, especially undead and demon rows.
6. Whether a cloned native enemy model can be mounted on a player avatar without animation or renderer side effects.
