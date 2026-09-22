# Writing content with FTK Mod Framework

This framework is a BepInEx 5 plugin that exposes a small API for adding content to
**For The King** without touching game files. You write your own BepInEx plugin, depend on
this one, and register content from a single hook.

## 1. Project setup

A net35 BepInEx 5 plugin (see `FTKModFramework.csproj` for the reference setup). Add a
reference to `FTKModFramework.dll` and to the game's publicized `Assembly-CSharp`.

```csharp
[BepInPlugin("com.you.mymod", "My Mod", "1.0.0")]
[BepInDependency("com.ftkmf.framework")]   // depend on the framework
public class MyMod : BaseUnityPlugin
{
    internal static MyMod Instance;
    void Awake() { Instance = this; new Harmony("com.you.mymod").PatchAll(); }
}
```

## 2. The one hook: `TableManager.Initialize`

All content tables are populated by the time `TableManager.Initialize` returns, so register
everything from a postfix on it:

```csharp
[HarmonyPatch(typeof(GridEditor.TableManager), "Initialize")]
static class Register
{
    static bool _done;
    static void Postfix()
    {
        if (_done) return; _done = true;        // Initialize can run more than once
        // ... your Content.AddX calls ...
    }
}
```

## 3. The API (`FTKModFramework.Core.Content`)

For the development Guardian kit, custom equipment modifiers and item-scoped
original models, see [Guardian classes and original equipment](GUARDIAN-AND-EQUIPMENT.md).

Every helper clones an existing entry (so you inherit a valid icon/prefab/animations), lets you
tweak fields, and registers a display name. Pass YOUR plugin GUID so IDs never clash between mods.

```csharp
using FTKModFramework.Core;
using GridEditor;

// A weapon (cloned from the Shortsword)
FTK_weaponStats2 sword = Content.AddWeapon(
    "com.you.mymod", "mymod_flamesword", FTK_itembase.ID.bladeShortsword, "Flame Sword",
    w => { w._maxdmg += 5f; w.m_ItemRarity = FTK_itemRarityLevel.ID.rare; w.m_TownMarket = true; });

// A consumable / non-weapon item (cloned from an existing item)
FTK_items potion = Content.AddItem(
    "com.you.mymod", "mymod_megapotion", FTK_itembase.ID.healthPotion, "Mega Potion",
    i => { i._goldValue = 200; });

// A combat action / ability (cloned from an existing proficiency)
FTK_proficiencyTable lash = Content.AddProficiency(
    "com.you.mymod", "mymod_flamelash", FTK_proficiencyTable.ID.fire1, "Flame Lash",
    p => { p.m_DmgMultiplier = 1.5f; p.m_IgnoresArmor = true; });

// Give a weapon one or more brand-new abilities (its own prefab copy; the original is untouched)
Content.AttachProficiencies(sword, "mymod_flamelash" /*, "mymod_backstab", ... */);

// A playable class (cloned from the Gladiator). See §4 for the details that matter.
FTK_playerGameStart cls = Content.AddClass(
    "com.you.mymod", "mymod_blademaster", FTK_playerGameStart.ID.gladiator, "Blademaster",
    c => {
        c._quickness = 0.7f; c._toughness = 0.7f; c._vitality = 0.6f; // stats are floats ~0.3-0.8
        c.m_StartWeapon = (FTK_itembase.ID)Content.Db<FTK_weaponStats2DB>().GetIntFromID("mymod_flamesword");
        c.m_StartItems = new[] { FTK_itembase.ID.armorMagicLeather };
        c.m_DLC = FTK_dlc.ID.None; c.m_Release = true; // keep it unlocked on all build types
    });
Localization.SetClassFlavor("mymod_blademaster", "A relentless duelist who lives by the blade.");
```

`Content.Db<T>()` fetches a content table and makes sure its index is built (needed because the
DB components' own `Awake`/`MakeIndex` may not have run yet at registration time). Use it whenever
you read a table directly:

```csharp
var classes = Content.Db<FTK_playerGameStartDB>();
var blacksmith = classes.GetEntry(FTK_playerGameStart.ID.blacksmith);
```

## 4. Playable classes: things to know

`Content.AddClass` clones an existing class's `FTK_playerGameStart` row (so you inherit a valid 3D
model/skinset, portrait, and a sane field layout) and registers it. The character-select roster is
DB-driven, so your class appears automatically, but mind these:

- **It's id == array index.** Unlike other content (high-band synthetic ids), a class is registered
  with the next sequential enum value, because character-select uses the id as *both* an enum key and
  an array index. `AddClass` does this for you; just don't try to force a different id.
- **Stats** are floats, roughly `0.30–0.80`, displayed ×100. Fields: `_toughness` (Strength),
  `_fortitude` (Intelligence), `_awareness`, `_talent`, `_quickness` (Speed), `_vitality`. There is
  **no per-class Luck** (Luck is global). `_basefocus` (1–9) and `_startinggold` round it out.
- **Difficulty** adds a flat bonus to *every* class equally (Apprentice +5, Journeyman/Master 0), so
  one stat block is correct on all difficulties; don't try to tune per difficulty.
- **Availability:** keep `m_DLC = FTK_dlc.ID.None` and `m_Release = true`; add no lore-unlock entry and
  the class is unlocked + visible by default.
- **Model/portrait:** reusing the cloned `m_Skinsets` remains the simplest path. For original
  editor-free GLB body, hair, and conditional-apparel assignments, use
  `Content.SetClassBodyMeshesFromGlb` and follow the strict path, bind, equipment, and lifecycle
  requirements in [`MODEL-PLAYER-API.md`](MODEL-PLAYER-API.md).
- **Name & flavor:** the display name is the 4th `AddClass` arg; set the description with
  `Localization.SetClassFlavor(id, "...")`.

## 5. Passive class traits (`Content.AddPassive`)

A passive is a class-innate trait that fires deterministically at one closed trigger moment. Unlike
the other `Add*` helpers it writes no `FTK_*DB` row and mints no synthetic id: it binds behaviour
to a class you have already registered. The smallest complete example is the bundled Innkeeper
([`Content/InnkeeperClass.cs`](../FTKModFramework/Content/InnkeeperClass.cs)), trimmed here:

```csharp
using FTKModFramework.Core;
using GridEditor;

// 1) The class first: a passive binds to a registered row, so AddClass must run before AddPassive.
FTK_playerGameStart innkeeper = Content.AddClass(
    "com.you.mymod", "mymod_innkeeper", FTK_playerGameStart.ID.blacksmith, "Innkeeper",
    c =>
    {
        c._vitality += 0.10f; c._fortitude += 0.08f;   // built like a barrel
        c._quickness -= 0.12f; c._talent -= 0.10f;     // paid for in speed and finesse
        // Take a private copy before touching a flag: the clone is shallow and the
        // inherited CharacterSkills object is still the LIVE Blacksmith's (see §4).
        c.m_CharacterSkills = new CharacterSkills(c.m_CharacterSkills);
        c.m_CharacterSkills.m_SteadFast = false;
    });

// 2) The passives. The returned def's Key is modGuid + ":" + passiveId; that string is the
//    trait's stable identity in logs and in co-op, so pick the id once and keep it.
PassiveTraitDef ironBelly = Content.AddPassive(
    "com.you.mymod", "iron_belly", innkeeper, PassiveTrigger.ConsumableDebuff, "Iron Belly");
PassiveTraitDef roundOnTheHouse = Content.AddPassive(
    "com.you.mymod", "round_on_the_house", innkeeper, PassiveTrigger.ConsumableBuff, "Round on the House");

// 3) Feedback text the trigger patches read back: Key + ":hud" is the floating combat popup,
//    Key + ":log" is the combat-log line. Derive them from def.Key so they can never drift.
if (ironBelly != null)
{
    Localization.SetName(ironBelly.Key + ":hud", "Iron Belly!");
    Localization.SetName(ironBelly.Key + ":log", "The Innkeeper's cast-iron stomach shrugs it off.");
}
```

`AddPassive(modGuid, passiveId, classRow, trigger, displayName)` returns the registered
`PassiveTraitDef` (`Key`, `ClassId`, `Trigger`, `DisplayName`), or `null` when `classRow` is null or
not yet registered in the class DB (logged as an error, registry untouched). The display name is
registered through the normal `Localization` path under `Key`.

### The trigger vocabulary

`PassiveTrigger` is a closed set. There is no way to add a trigger from a mod; a new moment needs a
framework change in `Core/`.

| Trigger | Fires when | What the framework does |
|---|---|---|
| `IncomingAttack` | The owner is about to take a main-target hit | Zeroes the first qualifying hit each combat (once per fight, per character), then shows the `:hud` / `:log` feedback on the impact frame. The decision runs on the master only and the negated result travels to clients in the normal damage payload. |
| `ConsumableDebuff` | A consumable would apply a debuff to the owner | Strips the drink's downside (for example rum's `enConfuse`) and shows the feedback; the buffs still apply. |
| `ConsumableBuff` | The owner drinks a personal consumable | Shares the beneficial effects with the rest of the party. Orbs already target the party and are left alone; the shared copy carries buffs only, never the downside. |

### The no-probability rule

`PassiveTraitDef` has no chance field, and there is no percent parameter on `AddPassive`. A
passive either fires at its trigger or it is not bound; the once-per-combat gate on `IncomingAttack`
is the only limiter. This is deliberate: a probabilistic passive would need a shared roll to keep
host and clients agreeing (see §10), and vanilla already owns the random-negate lane with
Steadfast and shields. If a design needs a chance, model it as a vanilla `CharacterSkills` flag or a
proficiency `m_ChanceToAffect`, not as a passive.

### Gotchas

- **Register the class first.** `AddPassive` resolves `classRow` against the live class DB, so a
  row that has not gone through `AddClass` (or a `null` from a failed `AddClass`) returns `null`.
  Check the return before using `def.Key`.
- **Re-registration is first-wins.** Calling `AddPassive` again with the same `(modGuid, passiveId)`
  returns the existing def and logs a warning; a different trigger or name on the second call is
  ignored, never merged.
- **Inherited vanilla skills still fire.** A cloned class keeps its template's `CharacterSkills`;
  the Innkeeper clears `m_SteadFast` on a private copy because Steadfast would randomly pre-empt
  its own defence. Decide which vanilla flags your passive competes with and copy the skills
  object before you change them.
- **Missing feedback text falls back, loudly.** If `Key + ":hud"` or `Key + ":log"` is not
  registered the patch shows `DisplayName + "!"` and logs a warning each time. Set both.
- **Feedback is master-gated; outcomes are not.** In co-op the effect (negate, strip, share) is
  identical on every client because it rides the shared damage or buff payload; only the popup
  and log line are emitted by the master. Never add client-local randomness on top (see §10).
- **Self-test line to look for.** The framework's own startup probe emits
  `SELF-TEST PASS: AddPassive registry (bind, idempotent, reject)` in `BepInEx/LogOutput.log`;
  the Innkeeper adds `SELF-TEST PASS: Innkeeper passives bound (...)`. Mirror that idiom in your
  own mod: assert `def.Key`, `def.Trigger`, and `Localization.TryGetName(def.Key, ...)` at load.

Passives are class-scoped. A trait that should belong to a quest reward, an NPC, or a campaign
state is a different tool: see [`CAMPAIGNS.md`](CAMPAIGNS.md) for flags and quest logic, and
[`ADVENTURES.md`](ADVENTURES.md) for encounter-level effects.

## 6. Custom combat behaviour (a `ProficiencyBase` subclass)

Cloning a proficiency reuses an existing effect. For *new* behaviour, subclass `ProficiencyBase`,
override `AddToDummy`, and set an instance as the row's `m_ProficiencyPrefab` (the game
`Instantiate`s it via `ProficiencyManager`). `GetAttacker(_dummy)` is the user; `_dummy` is the target.

```csharp
public class MyZap : ProficiencyBase
{
    public override void AddToDummy(CharacterDummy _dummy)
    {
        var attacker = GetAttacker(_dummy);
        // ...do something: grant gold/items, buff the attacker, read the enemy's loot table, etc.
    }
}
// register: AddProficiency(..., p => p.m_ProficiencyPrefab = go.AddComponent<MyZap>());
```

Gotchas (learned the hard way building the Thief's Steal):
- A **0-damage** proficiency is auto-cancelled unless flagged `m_Harmless`. Whether the slot roll
  gates the effect is a *different* field, `m_FullSlots` (true means a perfect roll is required; the
  two only correlate in vanilla rows). For a HOSTILE effect where you want the **roll itself to be the
  gate** (so spending Focus guarantees it), give it a tiny chip of damage with **`m_IgnoresArmor =
  true`** (else armor reduces the chip to 0 and re-blocks it). Never use that chip on a
  friendly-targeted row; see §6.1.
- `m_SlotOverride = 1` makes it a single roll; `m_PerSlotSkillRoll` lowers the per-slot accuracy;
  `m_ChanceToAffect` is a separate flat apply-chance.
- For steal-category HUD, set `_dummy.m_DamageInfo.m_ProfHasAmount = true` on success (else the game
  shows "Nothing To Steal").

See `Content/ThiefStealProficiency.cs` for the full worked example.

### 6.1 Status effects: clone a status row (no subclass needed)

A combat status (Frozen, Bleeding, Taunting, Shocked, ...) IS a proficiency row in this engine. Its
duration, tick cadence, magnitude, proc chance, refresh semantics, immunity check, HUD icon, and
combat-log line all come from the vanilla `FTK_proficiencyTable` row plus the shared `ProficiencyBase`
prefab it points at. So a custom status is `Content.AddProficiency` with a vanilla status row as the
template and a few fields set in the configure lambda. No `ProficiencyBase` subclass, no Harmony patch,
no new API. The bundled `Content/HoarfrostMaul.cs` is the worked example: **Rimefall Strike** (a clone
of the player blunt `Category.Ice` row `bluntIceReg`, an enemy combat-math modifier) and **Warding
Roar** (a clone of the `Category.Taunt` row `taunt`, a self-applied targeting redirect), both carried
by one weapon.

```csharp
// An enemy status: any landed damaging hit leaves the target Frozen for 3 ticks.
Content.AddProficiency("com.you.mymod", "mymod_frostbite", FTK_proficiencyTable.ID.bluntIceReg, "Frostbite",
    p =>
    {
        p.m_RepeatCount = 3;        // DURATION in ticks
        p.m_FullSlots = false;      // any landed hit, not only a perfect roll (state this deliberately)
        p.m_ChanceToAffect = 1f;    // proc chance
    });

// A self-applied status: the wielder becomes the enemies' target for 2 ticks.
Content.AddProficiency("com.you.mymod", "mymod_challenge", FTK_proficiencyTable.ID.taunt, "Challenge",
    p =>
    {
        p.m_RepeatCount = 2;
        p.m_TargetFriendly = true;                   // the damaged dummy becomes the attacker (self)
        p.m_Target = CharacterDummy.TargetType.None; // one target, no friendly pick
        p.m_Harmless = true;                         // zero damage, exempt from the zero-damage cancel
        p.m_FullSlots = false;
        p.m_ChanceToAffect = 1f;
    });
Localization.SetProficiencyDescription("mymod_challenge", "Enemies turn their attacks on you.");
var maul = Content.AddWeapon("com.you.mymod", "mymod_maul", FTK_itembase.ID.bluntWarHammer, "Rime Maul");
Content.AttachProficiencies(maul, "mymod_frostbite", "mymod_challenge"); // two rows, never one
```

An `AddWeapon` clone carries the template's own action list: the maul above exposes the War
Hammer's `bluntShockwaveSplash` and `bluntStun` alongside the two attached rows (four actions in
combat). Pick a template whose actions you want, or accept them.

**The row fields that drive a status** (all on `FTK_proficiencyTable`, all settable in the lambda):

- `m_RepeatCount`: duration in ticks. `ProficiencyBase.AddToDummy` builds no record at all when it is
  0, so a status row must set it above 0.
- `m_Quickness`: tick interval, used as `1f / m_Quickness` seconds of combat time (higher is faster).
  Ignored when the shared prefab flags `m_IsEndOnTurn`, in which case the record counts turns instead.
  So whether `m_RepeatCount` means turns or real-time ticks depends on the prefab you inherit, which
  the bundled self-test logs: the vanilla Ice prefab is timed (`endOnTurn=false`, quickness 0.4, so 3
  ticks is about 7.5 seconds) and the Taunt prefab is end-on-turn (2 ticks is two turns).
- `m_DamagePerAttack`: damage dealt on every tick (`ApplyDamage`), 0 for a pure modifier.
- `m_CustomValue`: the per-status magnitude read by categories that have one (Armor, Attack, Evade,
  Resist, Time, LifeDrain, ...). It is NOT the Frozen multiplier: Frozen's magnitude is the vanilla
  global `GameFlow.m_FrozenDmgPercent`, which is read by every ice effect and by the battle-button
  damage preview. Never write it; tuning it to balance one status would silently rebalance vanilla.
- `m_ChanceToAffect`: proc chance in 0..1, rolled once on the acting side inside `DummyDamageInfo` and
  then Photon-serialized, so co-op determinism is inherited. Do not add a master-client guard.
- `m_FullSlots`: when true, `m_ProfSuccess` requires a PERFECT slot roll and a landed-but-imperfect
  swing applies nothing. Set it deliberately and write your test criterion against the value.

**Refresh, not stack.** `CharacterDummy.m_SufferingProficiencies` is a dictionary keyed by
`ProficiencyBase.Category`, and `AddToDummy` writes the new record into that slot unconditionally.
Re-applying the same row (or any row of the same Category) resets the duration to full; magnitudes
never stack. `ShouldOverwrite` exists as a virtual but has no call site in the shipped assembly: it is
dead code, do not rely on it. One latent consequence: when a second Ice row replaces the first, the
replaced instance's `End` is never called, so its follow FX and freeze SFX loop are not stopped. Watch
for a stuck visual when a vanilla ice effect and a custom one meet on the same target.

**What lives on the shared prefab is read-only to you.** `m_Category` and `m_IsEndOnTurn` sit on
`m_ProficiencyPrefab`, a `ProficiencyBase` instance SHARED by every vanilla row that uses it (the
clone copies the reference, not the object). Mutating them would alter every vanilla row using that
prefab. The only no-new-code lever is pointing `m_ProficiencyPrefab` at a *different* vanilla prefab
that already has the combination you want.

**Icons are hardcoded, so reuse an existing Category.** `uiEachEnemyHud.RefreshStatusHudIcons` and
`uiPlayerMainHudStatus.SetStatusIcons` toggle one hand-placed object per status off a hardcoded
`CharacterDummy` property; there is no map to extend. Categories with an icon: on enemies, Ice
(Frozen), Fire (Burning), Lightning (Shocked), Stunned, Bleed, Scare, Death (DeathMark), Water (Wet),
Reflect, Protect, and the armor/resist/evade/speed/attack up-and-down modifiers; on players, the same
set plus Confuse, Acid, Entangle, Shield, and ResistDeath. `Taunt` has no icon on either side: its only
feedback is the vanilla `STR_HudTaunt` float text and the combat-log line.

**Registering a brand-new Category is not viable without engine work.** It has no icon, matches no
immunity check (`CharacterStats.HasImmunity` and the enemy `m_Immune*` flags are per-Category), has no
`CharacterDummy` convenience property (`Frozen`, `Taunting`, ...) for the engine to branch on, and the
ability tooltip shows the raw placeholder `GetCategoryDescription #YourCategory#`.

**The Poison exception.** `ProficiencyPoison.AddToDummy` never calls the base: it goes through
`CharacterStats.SetPoison` and bypasses the `m_RepeatCount` record system entirely. A poison row is an
invalid template for any duration-driven status.

**The self-target recipe.** `m_TargetFriendly = true` makes `DamageCalculator.StartEngageAttack`
reassign the damaged dummy to the attacker (and zero the evade rating, so a hero cannot dodge his own
buff). Pair it with `m_Harmless = true`, which zeroes the damage modifier and exempts the action from
the zero-damage auto-cancel. Never pair it with the `m_IgnoresArmor` chip from §6: on a
friendly-targeted row that forces the hero's OWN armor to zero and deals him unmitigated self-damage.

**Two gates, two fields.** `m_Harmless` exempts a zero-damage action from the auto-cancel.
`m_FullSlots` is what governs the perfect-roll requirement. They correlate in vanilla rows but are
independent; set both on purpose.

**Timing.** `ProficiencyManager.Start` builds its id-to-instance cache ONCE per combat scene from the
table as it stands, with no rebuild API. Register status rows from the `TableManager.Initialize` hook
(§2); a row registered after the first combat scene has loaded never applies. A row whose
`FTK_proficiencyTable.GetEnum(m_ID)` does not round-trip is cached under `ID.None` and silently never
applies either; the framework's `GetEnum` patch covers registered ids, and the bundled self-test asserts
the round-trip as a regression guard.

**Never apply a `Category.Taunt` row to an enemy.** `ProficiencyTaunt.AddToDummy` and `End` both
dereference `m_CharacterOverworld`, which is null on an `EnemyDummy`: a NullReferenceException inside
combat resolution and again on expiry. The self-target field set is what keeps it on the hero.

**When your path differs from the template's, set every field it reads.** The vanilla `taunt` row is
applied from the taunt button, which never reads `m_TargetFriendly`, `m_Target`, `m_Harmless`, or
`m_FullSlots`. A weapon action reads all four. Inherited values on a path the template never exercised
are the fields most likely to be silently wrong, so set them explicitly and assert them in a self-test.

**Name-based engine branches.** `DummyDamageInfo` sets `m_IsAOE` by testing whether the proficiency
id's `ToString()` contains `"Aoe"`. A synthetic id stringifies to its integer, so a custom status is
always treated as non-AoE. Correct for single-target statuses; a future AoE status would silently lose
that branch.

Scope note for Frozen: `CharacterDummy.CanUseAbility` reads `Frozen`, but its only callers are
player-side (the ability trigger, `CanDistract`, `CanEncourage`). A frozen ENEMY still acts and still
uses its proficiencies; freezing it yields the incoming-damage multiplier and the HUD icon. Wet
overrides ALL immunity (`IsImmune` returns false first), so an immunity test needs a dry enemy.

## 7. Enemies

`Content.AddEnemy` clones an existing enemy's `FTK_enemyCombat` row (so you inherit a valid 3D body,
weapon, and animations) and registers it with a high-band synthetic id. Unlike classes, enemies are
**not** id == array index: every enemy lookup is dictionary/string-based, and selection round-trips the
id through its decimal string over Photon. After registering, `AddEnemy` flips
`GameCache.Enemies.NeedsRebuild` so the game's level-bucketed spawn pool re-reads the DB and your enemy
becomes eligible for ordinary overworld/dungeon fights: **no spawn-selection patch needed.**

```csharp
FTK_enemyCombat cutpurse = Content.AddEnemy(
    "com.you.mymod", "mymod_cutpurse", FTK_enemyCombat.ID.banditA, "Cutpurse",
    e => {
        e.m_EnemyLevel = 1;                          // which level bucket it spawns in
        e.m_HealthTotal = 26; e.m_EvadeRating = 0.20f;
        e.m_ArchType = FTK_enemyCombat.EnemyArchType.Evade;
        e.m_ChanceToProf = 0.5f;                     // how often it uses a proficiency vs a normal attack
        e.m_Rarity = "Common";                       // draw weight (FTK_encounterDrawChanceDB)
        e.m_SpawnDay = e.m_SpawnNight = e.m_SpawnLand = e.m_SpawnDungeon = true;
        e.m_RealmInclude = new FTK_realm.ID[0];      // empty => eligible in every realm
        // custom loot: AddEnemy deep-copies the cloned row's m_ItemDrops, so edit it directly
        e.m_ItemDrops._golddrop = 25;
        e.m_ItemDrops.m_AlwaysDropItems = new[] { FTK_itembase.ID.conLockpicks };
    });
Localization.SetEnemyDescription("mymod_cutpurse", "A nimble thief who robs the unwary.");

// give it a custom/cloned attack (its own private weapon copy; vanilla enemies untouched)
Content.AttachEnemyProficiencies(cutpurse, "mymod_pilfer");
```

Things that matter:
- **It must pass the spawn-pool filter or it's silently dropped:** clone a template that is **not a boss,
  not a scourge, and not in `FTK_enemyScaleDB`**, and keep its `m_EnemyAsset` non-null.
- **`m_EnemyAsset`** (a `CharacterEventListener`) is the 3D body and **`m_WeaponAsset`** (a `Weapon`
  component) carries the attacks; both are reference fields, so cloning reuses them and the enemy renders
  and fights for free. `m_ArchType` is only a *stat* archetype, not the model.
- **Abilities:** `AttachEnemyProficiencies` instantiates a private copy of `m_WeaponAsset`, adds your
  proficiency, strips any `AttackSchedule` (so the RNG attack path can pick it), and `SaveState()`s it.
  Set `m_ChanceToProf > 0` so the AI actually fires it. A custom `ProficiencyBase` behaviour (§6) works
  when the enemy is the attacker; guard any shared-state mutation (gold, etc.) with
  `PhotonNetwork.isMasterClient` so co-op applies it once.
- **Spawn gating:** `m_EnemyLevel` (which bucket), `m_Rarity` (draw weight), `m_SpawnDay/Night/Land/Water/Dungeon`,
  and `m_RealmInclude`/`m_RealmExclude` decide *where/when* it appears. Cloning a template that already
  spawns gives sane defaults.
- **Multiplayer:** enemy spawns are master-authoritative and cross the wire as the enemy's id *string*;
  the deterministic synthetic id round-trips, so co-op stays in sync as long as every player has the mod.

There's also a DEBUG config (`Enemies/ForceCustomEnemy`) that replaces every overworld land enemy with the
Cutpurse, so you can verify a custom enemy fights and drops loot without waiting on the weighted draw.

See `Content/CutpurseEnemy.cs` (+ `Content/CutpurseStealProficiency.cs`) for the full worked example.

## 8. New adventures & encounters

> Design reference + the full how-it-works: [`ADVENTURES.md`](ADVENTURES.md).

A whole **adventure / game-mode** is not a DB row: it's a `GameDefinition` deserialized from a
`.ftk2` JSON file in the game's `StreamingAssets/mods`. `Adventures.AddFromTemplate` clones one of the
player's *installed* adventures at runtime (so it ships no game content), retunes a few JSON fields, and
registers it. The one required Harmony patch whitelists the name through `FTKHub.IsValidSaveFileName`
(the single hardcoded gate the start screen checks). World generation, win condition, and saves are all
data-driven off the cloned definition, so an adventure built from existing realms needs **no** generator
patch and appears on the start screen automatically.

```csharp
using FTKModFramework.Core;
using Newtonsoft.Json.Linq;

// A new selectable adventure, cloned from the installed DungeonCrawl and retuned.
Adventures.AddFromTemplate(
    "com.you.mymod", "MyRun", "DungeonCrawl",
    "My Run", "A richer romp across Fahrul.",
    jo => { jo["m_GoldMultiplier"] = 1.5; jo["m_SelectionPriority"] = 250; });
```

A new **overworld encounter/event** *is* a DB row (`FTK_miniEncounterDB`), so it injects with the same
clone-register pattern as items. The selector (`GameLogic.GetMiniEncounter`) walks the whole table and
weight-rolls every eligible row, so a freshly registered one is automatically a candidate (no generator
patch needed). An empty `m_RealmInclude` means "every realm"; `m_Rarity` reuses an existing draw-chance bucket
(`Common`/`Uncommon`/`Rare`/`SuperRare`); display strings show verbatim (the game's text lookup returns
the key itself when it has no row).

```csharp
Content.AddEncounter("com.you.mymod", "mymod_cache", FTK_miniEncounter.ID.TreasureChest, "Hidden Cache",
    e => { e.m_Rarity = "Common"; e.m_RealmInclude = new FTK_realm.ID[0]; });
```

> Registering any custom **class** also installs a small guard on `uiQuickPlayerCreate.CanUseClass`: an
> out-of-range class id in the party lobby falls back to a default class (the game's own intent) instead
> of throwing and breaking the character-create screen.

## 9. How it works (why it's safe)

- **IDs**: the `FTK_*.ID` enums are compile-time fixed. `IdAllocator` mints a deterministic
  synthetic int per `(modGuid, contentKey)` in a high band (`0x40000000+`), identical on every
  machine, so saves and co-op stay in sync. `DbLookupPatcher` + the `GetEnum` prefixes make the
  game's lookups resolve those synthetic ids.
- **Names & text**: `Localization` patches the game's text lookups (item/weapon `GetLocalizedName`,
  proficiency `GetLocalizedDisplayName`/`DisplayTitle`, class `GetDisplayName`, class flavor, enemy
  `GetEnemyDisplay`/`GetEnemyDescription`, and proficiency tooltip descriptions) to return what you
  registered; the game otherwise reads from Google2u text tables it doesn't have entries for.
- **Routing**: a patch on `FTK_itembase.GetItemBase` keeps custom items resolvable despite the
  `id >= 100000 -> weapon DB` rule.
- **Save-safety**: the framework sets `FullSerializer.fsConfig.SerializeEnumsAsInteger = true`.

## 10. Multiplayer

Co-op is Photon and has **no asset streaming**: every player must have the same mods installed.
Synthetic IDs are deterministic precisely so host/client agree on what each id means.

## 11. Data-authored mods

A content mod can be a folder under `<game>/BepInEx/plugins/` with no compiled DLL. Give it a
`manifest.json` and one or more other `.json` files containing `entries` arrays. The loader
registers entries in a deterministic ordinal `(modGuid, id)` order rather than filename order, so
every co-op client mints positional IDs identically. Cross-file references resolve in a later
phase, so a class can point at a weapon declared in another file whichever one is read first.
Supported kinds are items, weapons, proficiencies, classes, enemies, and encounters. The checked-in
[`SampleData/com.ftkmf.sampledata`](../FTKModFramework/SampleData/com.ftkmf.sampledata) folder is
the canonical working example; its intentionally broken and validation-only files demonstrate
fault isolation and should not be copied into a real package.

At minimum, a manifest declares stable identity, the mod's own numeric version, and the earliest
framework release on which the author confirmed it works:

```json
{
  "modGuid": "com.example.wayfarer",
  "name": "Wayfarer",
  "version": "1.0.0",
  "frameworkVersion": "0.1.3"
}
```

`version` and `frameworkVersion` are separate. A missing, invalid, older, or different-major
framework declaration leaves the mod visible but blocks its content and declared behavior DLL.
Read [`MOD-VERSIONING.md`](MOD-VERSIONING.md) before publishing. Marketplace packages add the
reviewed descriptor, immutable archive, and validation requirements in
[`MARKETPLACE.md`](MARKETPLACE.md).

Optional manifest fields are `description`, `author`, `developmentOnly` (hides the mod from
release-build players; the bundled samples set it), and `behaviorDll` (see below).

### Content entries

Every other `.json` file in the folder is a content file: an object with an `entries` array. The
smallest complete entry is a weapon (from the sample's `z_shadowfang_weapon.json`):

```json
{
  "entries": [
    {
      "kind": "weapon",
      "id": "wayfarer_shadowfang",
      "template": "bladeDagger",
      "displayName": "Shadowfang",
      "fields": { "damage": 8, "damageGain": 0.6, "slots": 3, "rarity": "rare", "goldValue": 120 },
      "proficiencies": [ "wayfarer_backstab" ]
    }
  ]
}
```

| Key | Required | Meaning |
|---|---|---|
| `kind` | yes | `item`, `weapon`, `proficiency`, `class`, `enemy`, or `encounter` |
| `id` | yes | Stable local id, unique within the mod; the synthetic id is minted from `(modGuid, id)` |
| `template` | yes | The vanilla enum member to clone, for example `bladeDagger` or `treasureHunter` |
| `displayName` | yes | Registered through the same `Localization` path as `Content.Add*` |
| `fields` | no | Member overrides applied to the cloned row (see the next subsection) |
| `proficiencies` | no | Weapon only: local ids of proficiency entries to attach (same as `Content.AttachProficiencies`) |
| `flavor` | no | Class only: the class-select flavor text |
| `description` | no | Proficiency only: the tooltip description |
| `behavior`, `behaviorCategory` | no | Proficiency only: a behaviour name and its category from a behaviour DLL (see below) |

References to another entry in the same mod use that local string id wherever a content-id field is
expected (`startWeapon`, `startItems`, `proficiencies`). Cross-file references resolve after every
file has been read, so declaration order does not matter. A vanilla enum member name is also
accepted in those fields (`"armorMagicLeather"`); a raw integer in the custom id band is not.

### Field aliases and overrides

`fields` is applied by the override engine to the cloned row. Each key is either the raw serialized
field name of the game type (`_maxdmg`, `m_Release`) or a friendly alias curated per kind, resolved
case-insensitively before the lookup, so `damage` and `_maxdmg` write the same member:

- **weapon** and **item**: `rarity`, `goldValue`, `minLevel`, `maxLevel`, `dropable`, `townMarket`,
  `dlc`; weapons add `damage`, `damageType`, `skill`, `slots`, `damageGain`.
- **proficiency**: `damage` (multiplier), `ignoresArmor`, `chanceToAffect`, `slots`.
- **class**: `strength`, `intelligence`, `awareness`, `talent`, `speed`, `vitality`,
  `startingGold`, `focus`, `primaryStat`, `startWeapon`, `startItems`, `skills`, `dlc`.

Scalars are coerced to the member type (`"rare"` to `FTK_itemRarityLevel.ID.rare`, `0.6` to a
float). `skills` on a class takes an object of `CharacterSkills` flags (`"m_Sneak": true`) and is
written to a private copy, so a data mod cannot leak a flag into the vanilla template. An unknown
key is a warning and is skipped; a value that cannot be coerced is an error for that entry only.

### Validation output

Every load ends with one summary line in `BepInEx/LogOutput.log`, followed by one line per problem:

```
Data content load complete: 7/12 entries registered, 4 error(s), 2 warning(s), 31 ms.
Data warning: [com.ftkmf.sampledata] weapon 'sampledata_unknown_field': unknown field 'thisFieldDoesNotExist'
Data error: [com.ftkmf.sampledata] duplicate id 'sampledata_backstab' (second skipped)
```

Errors (duplicate id, unknown kind or template, coercion failure, dangling reference, malformed
file) drop the offending entry or file; everything else in the mod still registers. The sample's
`_validation_seed.json` and `broken.json` trip every category on purpose so you can see each
message once. A mod blocked by its `frameworkVersion` logs `Blocked mod '<guid>': <reason>` and
registers nothing.

### Behaviour DLLs

A data mod can ship compiled combat logic without becoming a BepInEx plugin. Declare
`"behaviorDll": "MyBehaviors.dll"` in the manifest and place the DLL in the mod folder. Inside it,
mark each `ProficiencyBase` subclass with `[ContentBehavior("Steal")]` from
`FTKModFramework.Behaviors`; that attribute is the only framework type the DLL needs to reference.
The framework keys the type as `modGuid:Name`, and a proficiency entry binds it with
`"behavior": "Steal"` plus a `behaviorCategory`. The checked-in
[`SampleData/com.ftkmf.samplebehaviormod`](../FTKModFramework/SampleData/com.ftkmf.samplebehaviormod)
is the working reference; `com.ftkmf.brokendll` and `com.ftkmf.danglingbehavior` next to it show
how a bad DLL or a missing behaviour name is isolated.

The whole external-DLL pass is gated by the `[Data] EnableBehaviorLoading` config entry (default
on, inert when no manifest declares a `behaviorDll`). With it off, entries that name a behaviour
register as plain data and log a skip. Behaviour kinds are closed: `proficiency` is wired into the
proficiency row, and `questlogic` (a `QuestLogicBase` subclass for custom objective verbs) is
consumed by the campaign engine described in [`CAMPAIGNS.md`](CAMPAIGNS.md), not by a proficiency
row. Encounter-level content authored in code is covered in [`ADVENTURES.md`](ADVENTURES.md).

## 12. The in-game Mods browser: changes, saves, and co-op

The title screen's **Mods** panel separates Discover, Installed, and Updates. Installed includes
bundled, marketplace-managed, and manually installed content. Enable or package changes are
prepared safely and take effect on restart because content registration happens once during
startup. The panel also explains compatibility blocks and supports recovery for managed installs.
See [`MARKETPLACE.md`](MARKETPLACE.md) for the player and package-author contract.

Two hazards remain important:

- **Disabling a mod can break a save that references its content.** A save stores entities by their id.
  The sharpest case is a playable class: classes register at `id == array index` (see §9), so a saved
  party member of a custom class is stored by that index. Turn the class's mod off and relaunch, and
  that index now resolves to a different class or to nothing, which can fail to load or corrupt the
  save. The same applies to any saved item, weapon, or in-progress encounter whose synthetic id is no
  longer registered because its mod is off. The toggle does not check whether a mod's content is
  referenced by an existing save; if you turned a mod off for a save that uses it, turn it back on to
  recover that save.

- **Co-op requires every player to enable the identical mod set.** Co-op is Photon with no asset
  streaming (see §10): each client resolves IDs and assets locally. Framework-version preflight and
  package hashes do not prove that two players have the same complete installation. Agree on the
  enabled set with the other players before starting a co-op run.

## 13. Public capability map

The full inventory of the 57 `FTK_*DB` tables (items, weapons, proficiencies, hit effects,
classes, skinsets, enemies, realms, encounters, quests, ...) is in
[`PHASE0-TYPE-INVENTORY.md`](PHASE0-TYPE-INVENTORY.md).

| Capability | Public entry point | Guide |
|---|---|---|
| Items, weapons, proficiencies, classes, enemies, encounters | `Content.Add*`, `Content.Attach*` | §3, §4, §7, §8 |
| Combat status effects (duration, refresh, targeting, icons) | `Content.AddProficiency` with a vanilla status row as the template | §6.1 and the bundled Hoarfrost Maul |
| Class-innate passive traits (`PassiveTrigger` closed set, no probability) | `Content.AddPassive` | §5 and the bundled Innkeeper |
| Data-authored JSON mods, field aliases, behaviour DLLs | `manifest.json` + content `.json` (`Core/Data`) | §11 and `SampleData/com.ftkmf.sampledata` |
| Adventures, campaigns, quests, NPCs, end-game art | `Adventures.*`, `CampaignBuilder`, `QuestBuilder` | [`ADVENTURES.md`](ADVENTURES.md), [`CAMPAIGNS.md`](CAMPAIGNS.md) |
| Enemy visuals, meshes, materials, portraits, fall-off | `Content.SetEnemy*` | [`CUSTOM-MODELS.md`](CUSTOM-MODELS.md), [`MODEL-RENDERER-API.md`](MODEL-RENDERER-API.md) |
| Player body, hair, and conditional apparel | `Content.SetClassBodyMeshesFromGlb` | [`MODEL-PLAYER-API.md`](MODEL-PLAYER-API.md) |

For an unwrapped table, framework contributors can register through `ContentRegistry`; mod authors
should prefer the public `Content.*` and adventure APIs so validation and compatibility behavior
remain centralized.
