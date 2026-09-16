# Native trap choice

`trap-state` is read-only and issues the latest observation ticket, valid for five realtime seconds and at most600 Unity frames. It observes the actual active trap, sole owned living hero, native trap vote and UI. Missing native data returns `available:false` without a ticket. False gates remain explicit. It does not clear, replace or mutate the passive enemy lifetime/portrait watcher.

```json
{"id":"NEW_ID","session":"EXACT_NONCE","op":"trap-state"}
```

Choose exactly one observed native option. Copy the entire `identity` object and ticket unchanged:

```json
{"id":"NEW_ID","session":"EXACT_NONCE","op":"trap-submit","ticketId":"FROM_TRAP_STATE","identity":{},"option":"Proceed"}
```

The empty identity above is a placeholder, not a valid request. `Disarm` and `Proceed` are the only options. The helper re-resolves the actual hero/dummy/CEL, dungeon/diorama/trap, both Trap vote types, enabled `Show Vote Buttons` FSM, exact single-hero vote queue/votingFID/fightOrder, no-modal status and the selected owned active native Unity button. Core/helper/native assembly and nonce pins must match. MC combatfalse is not required: traps are native nonenemy encounters. SP vote-focus ownership is reported indirectly through native state but is not invented as a required invariant; native SP button display does not set that focus.

Before exactly one private parameterless `VoteButton.OnLeftClick`, a permanent session+dungeon+level+room+trap claim is recorded. Option, button and command IDs cannot bypass this claim. Native exceptions retain it. `submitted` means callback returned; `uncertain-native-submission` means it may have partially executed. Neither means the roll succeeded or the room completed. No raw RPC/FSM, forced slots, damage, trap destruction, encounter completion or fallback UI callback is used.

After submission, poll read-only trap-state/fixture-state for actual native outcome and later Ready. Native Disarm/Proceed can fail, injure or kill the hero. Stop on unknown modal or timeout. Never retry/change options automatically; even a later renewed vote in this same trap room is deliberately refused by this first version. No automatic Ready/next-room action follows. A newer observation invalidates the older ticket.

The result preserves the pre-invocation identity/gates and immediate after-frame/button state. Subsequent trap-state provides independently fresh after identity; callback return does not prove delayed native vote processing. Source and linked-policy tests are not native traversal acceptance. Keep the first live result/roll/health/Ready evidence separate.

When the native active trap disappears, `trap-state` can legitimately return `available:false`. This is not completion proof; switch to `fixture-state` and require its actual `strictReady.ok:true` plus the expected session/dungeon/room progression. The coordinator no-modal test checks the singular `MessageInstance m_CurrentMessageInstances` reference; native `ClearMessageInstance()` sets it to null, not an empty collection.

The observation also pins the actual nonnull `FTK_dungeonTrap` row reference returned by native `DungeonTrap.GetDB()`. Submission requires that same object, not merely matching enum/key text; `trapDbRowKey` is metadata, not a substitute for reference identity. The selected button must remain a descendant of its exact owned vote container. This ticket retains a native DB row briefly, never avatar/resource references and never resource ownership.
