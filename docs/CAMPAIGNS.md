# Campaigns (data-authored quest engine)

How to author multi-stage, branching questlines on top of FTK's existing embedded-quest model. This is
the modder guide for the Quest & Campaign Engine (spec #37, epic #5); for how adventures load and register
see [`ADVENTURES.md`](ADVENTURES.md).

## What the engine is

FTK already models a campaign as native `QuestDefBase` objects embedded in `GameStage.m_Quests` inside a
`GameDefinition` `.ftk2`. The framework leans on that model and adds only three pieces of net-new runtime
machinery, so completion-detection, rewards, win-condition, save persistence, and co-op all come from the
game unchanged:

- a **branch router** (a host-only Postfix on `GameDefinition.GetNextQuest`) that redirects the otherwise
  linear story chain at flag-conditioned branch points,
- a **custom-objective-verb resolver** (a Prefix on `QuestLogicBase.GetQuestLogicTypeInstanceFromQuestDef`)
  that substitutes a mod-supplied `QuestLogicBase` for a quest carrying a registered verb key, and
- a **Core-owned flag store** (`CampaignStateQuest`, an invisible dummy quest carrying a single
  `Dictionary<string,int>`) that drives branch conditions and rides FTK's existing disk + RPC save channels.

Everything is **data-authored and save-safe by construction**: campaign position persists as string keys
(`m_StoryQuestID`, `m_StageID`), flags as `Dictionary<string,int>`, and **no** quest/stage/flag value ever
passes through `IdAllocator` or an `FTK_*.ID` enum band. Saves are portable across machines and co-op
sessions.

## Authoring API (public surface)

A campaign is authored by cloning an installed adventure and editing its `m_Stages`, via a strict superset
of `Adventures.AddFromTemplate`:

```csharp
Adventures.AddCampaignFromTemplate(
    modGuid, saveFileName, templateSaveFileName: "DungeonCrawl",
    displayName, infoText,
    configure: campaign =>
    {
        var stage1 = campaign.AddStage("Stage1");
        stage1.AddKillQuest("q1_kill", enemySet: "bounty1A", specifiedRealm: "GuardianForest");
        stage1.AddCollectQuest("q2_collect", FTK_itembase.ID.townTeleport, count: 2, specifiedRealm: "GuardianForest")
              .OnCompleteSetFlag("relics_found", "set", 1);
        stage1.AddEncounterQuest("q3_enc", miniEncounterId: "TreasureChest", specifiedRealm: "GuardianForest")
              .BranchTo("q5_clear", new BranchCondition { Flag = "relics_found", Op = "eq", Value = 1 });

        var stage2 = campaign.AddStage("Stage2");
        stage2.AddVisitQuest("q4_visit", specifiedRealm: "GuardianForest");
        stage2.AddClearDungeonQuest("q5_clear", dungeonId: "Cave", specifiedRealm: "GuardianForest"); // last quest = victory
    });
```

| Method | Emits / records | Notes |
|---|---|---|
| `CampaignBuilder.AddStage(stageId)` | a `GameStage` (realm scaffolding cloned from the template's first stage) | unique `stageId` |
| `StageBuilder.AddKillQuest(id, enemySet, realm)` | `BountyQuestDef` | kill |
| `StageBuilder.AddVisitQuest(id, realm)` | `VisitQuestDef` | reach a destination |
| `StageBuilder.AddClearDungeonQuest(id, dungeonId, realm)` | `DungeonQuestDef` | clear |
| `StageBuilder.AddEncounterQuest(id, miniEncounterId, realm)` | `MiniEncounterQuestDef` | encounter |
| `StageBuilder.AddCollectQuest(id, FTK_itembase.ID, count, realm)` | `ModQuestDef` (collect-N verb) | completes when the **party** collectively holds `count` of the item; `count >= 1` |
| `QuestBuilder.BranchTo(nextQuestId, params BranchCondition[])` | a branch edge in the sidecar | first-match wins; empty conditions = unconditional default edge |
| `QuestBuilder.OnCompleteSetFlag(flag, op, value)` | an on-complete flag mutation in the sidecar | applied before the branch resolves |
| `Campaign.SetFlag/GetFlag/HasFlag` | reads/writes the flag store | **writes are host-authoritative** |

**Quest order** is stage order x within-stage order, flattened; **victory** is the last quest of the last
stage. Quest keys (`m_StoryQuestID`) must be globally unique.

### Branch conditions and flag ops (closed, non-DSL)

Two small, disjoint, **closed** operator vocabularies — there is deliberately no expression language:

- `BranchCondition.Op` (comparison): `eq` / `ne` / `ge` / `le`. (`gt`/`lt` are intentionally omitted —
  express strictly-greater/less via `ge`/`le` on the adjacent integer.)
- `FlagOp.Op` (mutation): `set` / `add`.

There is no AND/OR, nesting, or arithmetic. **Conjunction is expressed by chaining quests**, not by
composing predicates. An unknown operator is rejected at load with a precise diagnostic.

### Custom objective verbs

`collect-N` (`CollectNQuestLogic`) is the shipped example of a custom verb. New verbs are added by
registering a `QuestLogicBase` subclass under a `modGuid:verbName` key through the P3 behavior framework
(the `questlogic` kind, instantiated via `Activator.CreateInstance`); the resolver Prefix swaps it in for
a `ModQuestDef` carrying that key. An unregistered key is a load-time WARN and the quest is skipped.

## Load-time validation

Every authored campaign is validated at registration through the existing `ValidationReport`/`LogSummary`
channel. The headline check is **victory-reachability**: every quest reachable from the start must be able
to reach the victory quest (a dead-end or a branch cycle with no exit toward victory is a load-time FAIL).
Other diagnostics:

| Condition | Result |
|---|---|
| Duplicate `m_StoryQuestID` | FAIL |
| Referenced enemy/enemySet/realm/dungeon/encounter/item id does not resolve | FAIL |
| `collect-N count < 1` | FAIL |
| Unknown comparison/mutation operator | FAIL |
| Branch path cannot reach victory / branch cycle with no exit | FAIL |
| `NextQuestId`/`QuestId` not present among the campaign's quests | WARN (router falls through to vanilla linear) |
| Cross-stage branch skip (target stage is not the current or immediately-next stage) | WARN |
| Unregistered custom-verb key | WARN (quest skipped) |

## The `EnableCampaignEngine` config flag

The whole engine is gated behind `[Campaign] EnableCampaignEngine` (default **on**), mirroring
`EnableBehaviorLoading`. The branch router and verb-resolver patches are `Prepare()`-gated on it, so with
the flag **off neither patch is installed** and behavior is identical to vanilla. The patches are inert on
any vanilla quest even when the flag is on (they act only on `ModQuestDef`s / quests with a sidecar rule).

> **Author-when-off:** with `EnableCampaignEngine` off, a campaign still *registers* and is selectable, but
> its branch routing, custom verbs, and load-time validation are inactive. Turn the flag on to play a
> campaign that uses these features.

## Known limitation: resume-without-mod (NFR-5)

Resuming a custom-campaign save **without the authoring mod installed** throws on `GetPreview` — the same
limitation custom adventures already have (see [`ADVENTURES.md`](ADVENTURES.md)). The framework does **not**
attempt graceful degradation: the run is unloadable until the mod is reinstalled (it is not corrupt — reinstall
the mod and the save loads). This is honest scope, not a bug; a host/client mod-parity check is deferred
with co-op verification.

## Co-op (designed-for; 2-client verification deferred)

All branch evaluation, flag writes, and objective completion run **host-authoritative**; the resulting
`m_StoryQuestID` propagates to clients via the engine's existing `SyncProgress`/`SyncProgressRPC` string
sync, so clients never recompute branch decisions. The custom `QuestLogicBase` types round-trip both the
disk serializer (FullSerializer) and the co-op RPC serializer (Newtonsoft `TypeNameHandling.Auto`).
2-client verification is deferred alongside the Adventures Slice D co-op work.

## Performance

The engine reuses FTK's embedded-quest model, so a long campaign is just a larger `GameDefinition`. A
synthetic 500-quest campaign (250x vanilla Dungeon Crawl's 2 quests) loads and persists well within the P5
scale budget (load-time, managed-heap, and real save-size). See [`SCALE-BUDGET.md`](SCALE-BUDGET.md); the
campaign scenario emits a `SCALE-BUDGET ... [campaign]` line.
