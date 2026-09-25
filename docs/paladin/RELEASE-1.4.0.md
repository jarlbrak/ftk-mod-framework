# Paladin 1.4.0 class-card and starting-kit revision

Paladin 1.4.0 requires framework 1.2.1 for the compact class-card presentation.
Published 1.3.0 archive bytes remain unchanged.

## Player-facing changes

The class description is two sentences of flavor, with mechanics kept in the
ability description:

> An oath is measured by the lives it shelters. Through faith and resolve, the
> Paladin stands beside those who cannot stand alone.

Special Abilities follows the native English label format with the registered
names: `Skill: Guard` and `Passive Skill: Cleansing March`. These custom labels
and names are English-only. Guard's combat
proficiency description still explains damage reduction, focused healing and
lethal-hit rescue. Those mechanics belong to Guard rather than separate
registered skills. The card no longer moves the starting-equipment section to
make room for mechanics paragraphs.

New Paladins receive **Novice Hammer and Novice Aegis**. Novice Plate, Sabatons,
Helm and Tin Oath Token must be acquired through their existing shop and loot
eligibility. Removing those four grants removes four Armor, two Resistance and
one Vitality point from equipment. These are authored modifier deltas, not a
claim about final difficulty-adjusted character stats.

Censure remains available through the starting hammer. Smite becomes available
when a Paladin trinket is acquired and equipped. The base stats, starting gold,
combat mechanics, equipment properties and acquisition settings are unchanged.
The shield preserves the class's protection-oriented starting choice; the
reduced kit makes armor and accessories meaningful early acquisitions.

## Native comparison

Read-only inspection on 2026-09-25 used the installed original FTK assembly,
SHA-256 `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`,
and its matching serialized `FTK_playerGameStartDB` in `sharedassets1.assets`.
This is installed-data evidence, not a live gameplay observation.

`uiSelectCharacterInfo.ShowCharacterInfo` reads the class flavor through
`TextCharacters`, calls `CharacterSkills.GetSkillDisplay(false)` for ability
rows, and lists the start weapon, start items and difficulty-adjusted gold.
`TextMisc` stores whole localized skill-name rows. There is no generic localized
skill prefix to reuse for custom names. Native abilities are short entries such
as `Skill: Elite Ambush` and `Passive Skill: Steady`, without mechanics
paragraphs in this view.

Of thirteen released class descriptions, nine have two sentences. Busker has
three; Treasure Hunter, Gladiator and Astronomer have one each. Some flavor
hints at function, such as healing or absorbing blows, without numerical rules
or explicit skill names. Two sentences of narrative flavor is a well-supported
Paladin convention, not a universal engine requirement.

Item names below resolve through the installed `TextItems` English rows.
Gold is the class's authored base amount, before difficulty modifiers.

| Class | Start weapon | Additional items | Base gold |
| --- | --- | --- | ---: |
| Blacksmith | Blacksmith's Hammer | Simple Iron Shield | 6 |
| Hunter | Hunting Bow | Hermit Grass | 3 |
| Scholar | Scholar's Book | Teleport Scroll | 5 |
| Herbalist | Cane | Godsbeard | 3 |
| Trapper | Hunting Spear | Hide Buckler, Lockpicks | 2 |
| Minstrel | Simple Lute | None | 10 |
| Busker | Broken Lute | Lockpicks | 2 |
| Woodcutter | Woodcutter's Axe | Fahrul Mule | 4 |
| Monk | Bokken | Golden Root | 3 |
| Hobo | Sharpened Stick | Mead | 0 |
| Gladiator | Pugios | Wooden Parma, Worn Trident | 1 |
| Treasure Hunter | Handgun | Treasure Map | 1 |
| Astronomer | Star Wand | Cracked Spyglass, Astrolabe | 3 |

Nine classes start with one weapon and one extra item, three have two extras,
and Minstrel has only its weapon. None has a full armor loadout. Blacksmith,
Paladin's native template, supports the hammer-and-shield starting choice.
This comparison establishes a grant-size baseline, not equivalent item power
or campaign balance.

## Compatibility and evidence

Stable content IDs remain unchanged. The revision changes the new-character
loadout; it does not implement an inventory migration or remove equipment from
existing saves. Save/resume with the revised package still requires live testing.
The assets are unchanged and retain their original 1.3.0 provenance record.

Game-free validation passed:

- `python3 marketplace/packages/validate_paladin.py`: source package, acquisition
  declarations and unchanged pinned assets.
- `build_paladin.py` and helper `marketplace-validate`: candidate archive and
  descriptor; archive SHA-256
  `145545a47b99e128b4ab1481e677e0d26a6f9fa16d3781a13de1ea362c8c36fc`.
- `GuardianClassUi`: eight checks of names, native text preservation,
  idempotence, missing labels and unchanged layout using Unity stand-ins.
- `GuardianCombat`: 193 game-free regression checks.
- `PlayerMods`: compatibility, discovery and marketplace checks passed on a
  serial rerun. An initial concurrent run failed an existing helper-recovery
  process assertion without producing a helper result; no test was weakened.
- `dotnet build -c Release` in `FTKModFramework`: zero errors, seven existing
  `EnemyVisualPatch` field warnings.
- Relative links in changed documentation and `git diff --check`.

These checks do not prove visual fit or campaign balance.

Live gates remain: compare Paladin and native class cards in Party Select, check
that a new Paladin receives only the hammer and shield across difficulties,
confirm Censure is present and Smite appears only after equipping an acquired
trinket, then save/resume. Ordinary acquisition frequency, campaign balance,
Windows/Linux gameplay and co-op remain unverified. The authorized Steam deployment was observed loading framework 1.2.1,
registering all 112 enabled content entries with zero content errors and warnings,
and reporting `SELF-TEST PASS: data-content determinism`. The optional full
diagnostic suite was disabled. This establishes registration and the specific
determinism check, not visual fit or the remaining gameplay gates.
