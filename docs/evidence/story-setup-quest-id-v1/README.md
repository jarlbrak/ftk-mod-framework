# Native story quest ID trial

The isolated 395-profile game started with 516 self-test passes and no failures.
The setup runner stopped at its bounded story timeout before entering the dungeon.
Across 331 read-only observations it submitted no story buttons. The fully open
first page had an exact active native callback, but the helper incorrectly
required a nonnegative quest ID.

Native `GameLogic._assignQuestID` deliberately negates definition-backed quest
IDs. The observed first authored quest ID `-1` is valid. The correction must
verify a present quest and the exact existing native quest-table reference,
including the final confirmation's matching quest object.

[The validation record](validation.json) and compressed journal preserve this
failure. A reviewed correction and fresh live run remain necessary. This record
contains no Duskquill combat or appearance acceptance.
