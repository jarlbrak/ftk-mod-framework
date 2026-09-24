# Native story setup before dungeon staging

Automatic `run_case.py new-run` setup is retired. Prepare and dismiss story UI
through the [native-input harness](../../../harness/README.md). The dedicated
helper commands described below remain isolated fixture tools; they are not
ordinary gameplay evidence. The old entry-and-staging sequence later in this
note describes prior evidence, not a supported bridge action sequence.

`story-state` takes only the usual id/session/op fields. It observes the native
coordinator instance/current message ID/type/closed flag, private current-instance
presence, queued count and continuation, presenter instance/message/page/quest,
three native presenter FSMs, and the actual portrait and QuestConfirm component
and panel identities. It reports activeSelf/activeInHierarchy/fullyOpened/clickable
and the portrait's closeOnOkay. The portrait's legacy clickable flag is metadata
only: native EnableButton installs a callback without changing that flag. Actual
readiness requires the active FTKClickAnywhere singleton to own current input
focus, canClose, and exactly one native UseOkayButton delegate targeting this
HUD, with no alternate callback/FSM/object and a ready local self continuation. QuestConfirm has no closeOnOkay field; its separate
predicate requires matching presenter quest and a native continuation. A camera
coroutine wait flag is unavailable; pending page/FSM/panel metadata must not be
relabelled as proof of a camera waiter. No native state is changed by this read.

For manual diagnostic use, `story-submit` requires all of these exact values from
a fresh `story-state`; zero-based message/page IDs are valid and must not be guessed:

```json
{"surface":"portrait","coordinatorId":1,"messageId":0,"presenterId":2,"pageIndex":0,"questId":3,"heroInstanceId":4,"componentId":5,"panelId":6,"clickInstanceId":7,"clickContinuationId":8,"phase":"DeliverStartQuestMsgPartClosed","contentSha256":"<exact observed SHA256>"}
```

The numbers above are placeholders. `surface` is exactly `portrait` or
`questConfirm`. The helper re-observes and verifies the entire identity on the
Unity thread. Submission requires a living owned single-player overworld party,
an active session, both encounter sessions outside combat, no entered dungeon,
exact `StoryQuestMessage`, matching presenter message and quest, no choices or
unknown modal, and the exact fully-open panel with its verified native input callback. Portrait additionally
requires closeOnOkay; QuestConfirm additionally requires its native continuation,
matching presenter quest, its native click-enable flag, and no actionable portrait. There is no fallback.

A bounded64-entry claim is inserted **before** the one native FTKClickAnywhere.OnClick call. This closes native input focus before
invoking its exact UseOkayButton continuation; direct UseOkayButton is not used.
It is not removed on exception. A successful result says
`native-story-page-submitted`, not dismissed or complete: native closure is delayed.
Never retry an uncertain request under another command ID.

The runner waits for an observed page increase or current-message replacement/
absence before another known page. Requests also pin the exact native presenter
closure callback phase and a SHA256 of ordered native message content. The
verified MultiQuest-to-SubQuest path may reset page zero only with the exact
DeliverMultiQuestMsgClosed → DeliverSubQuestMsgClosed callback change, rebuilt
content hash, and a newly actionable native portrait. An unclassified content or
phase change stops. Simple StartQuest increments its final page before creating
QuestConfirm; that confirmation is a separate exact surface. Merely seeing the same panel become clickable
again does not permit resubmission. It waits through nonclickable/native camera
sequences without forcing them. Unknown choices/modal types stop it.

Completion requires current native message instance absent, queue empty, no current
continuation, all three presenter FSMs present/disabled, both panels closed, and
no global/other modal or choice. `MessageType.None` alone is insufficient: native
code sets that before completing its close path. Completion must remain stable
for2seconds of fresh snapshots, then is rechecked before dungeon entry.
`enter_dungeon` is still followed immediately by atomic `stage-enemy`; no reads,
waits, modal operations or extra actions are inserted between them.

After staging, an exact no-choice StoryQuestMessage yields
`pending_story_message` with binding matches and the final native state. The CLI
writes its result and exits nonzero. It never operates story during combat.
Unknown/missing post-stage modal signals stop instead. Recorder/exercise guards
are unchanged, and this pending result is not capture readiness. Preserve any
such case rather than relabelling it or retrying its staging actions.

Validation: mocked loop tests cover once-only submission, uncertainty, unchanged
pages, native nonclickable waits, identity changes, full coordinator completion,
and post-stage boundaries. Linked shipped-Newtonsoft predicate tests cover exact
IDs/page/panel, malformed integer input, combat/dungeon/party rejection, separate
QuestConfirm guards and false completion. These tests establish code behavior,
not successful live native story progression; a fresh controlled run is required.

The 4c2491ef candidate was held before deployment: source review caught native
EnableButton leaving m_Clickable false and direct UseOkayButton omitting native
focus cleanup. Corrected code uses actual focus/callback authority and OnClick.
No live success is attributed to that held candidate.

Native quest IDs are signed: GameLogic._assignQuestID negates IDs for quests with
a definition, so the first ordinary story quest may be -1. Observation reports
questPresent separately and requires the existing private _fullQuestTable entry
at that nonzero signed ID to be the exact presenter quest object. No table is
created; absent, unmapped, zero or mismatched references cannot authorize a page.
QuestConfirm still requires that same actual presenter quest reference.
