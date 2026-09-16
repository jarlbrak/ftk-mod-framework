# Native trap runner: first live Proceed trial

The guarded runner submitted one native `VoteButton.OnLeftClick(Proceed)` in
`axesPoison` at level 0, room 3, and observed the same living hero at strict
Ready in room 4. The runner returned `native_ready_observed` with no errors.
Its exact request, result, journal, source, and surrounding observations are
preserved in [validation.json](validation.json) through 44 lossless mappings.

Repeat with the [trap runner instructions](../../../tools/ai-model-pipeline/runtime-test/TRAP-CASE.md).
Enter the native trap through its existing Ready button first. The queued room
type in this build is `Trap1`. An initial local guard expecting `Trap` stopped
before submission; that observation is retained. The subsequent Ready and
Proceed were each submitted once.

The hero's observed health changed from 959 before Proceed to 956 in a later
readout. These endpoints do not identify the exact damage source or roll.
This is one Proceed trial; Disarm and other trap types remain untested. Final
Ready evidence joins hero, session, level and room, but the fixture does not
expose an independent final dungeon object ID.

The separate legacy Reefstrider resource disposal had already occurred at
Ready room 3. Trap traversal is not presented as necessary to that disposal.
