# Lore Store Unlocked 1.0.0

Play with the whole Lore Store open. While this mod is installed, every Lore
Store entry you could buy reads as purchased: classes, items, encounters,
locations and cosmetics. The store shows each card as owned.

Requires framework 1.3.0 within major version 1. Restart after installation or
removal.

## Your own progress is untouched

The game records each purchase in your local profile and mirrors it to Steam the
moment it is written. This mod never writes there. It only changes the answer
when the game asks whether an entry is purchased, and only while it is loaded.
Removing the mod therefore restores exactly the unlocks you had, with no backup
or restore step.

- Lore points you earn while the mod is installed are real and stay yours.
  You cannot spend them while everything already reads as owned.
- Achievements and statistics earned in play are recorded as usual, including
  with content you have not bought yourself.
- Resetting lore from the game's options still resets your real progress.

## Limits

- Entries from free DLC packs unlock even if you have not claimed them on your
  store page. Entries from paid DLC you do not own, such as Lost Civilization,
  stay locked.
- Limited-time cloud entries unlock only while the game currently offers them.
- Entries the game hides from the store stay hidden.
- Encounters that reveal new Lore Store entries still appear, because reveals
  are left as genuine progress.
- Removing the mod during an adventure keeps that run's world and inventory,
  but later shops, loot and encounters follow your real purchases again.
- In online co-op most unlocks, such as classes and item pools, apply only to
  the player who has the mod. The game shares some world unlocks with the
  party, except entries it requires every player to own. Co-op is unverified.

## Attribution

Authored by JarlBrak. The banner is promotional imagegen artwork, not a
gameplay screenshot; its prompt is in [promo/PROMPT.md](promo/PROMPT.md). The
package contains no game assets. It uses the repository's MIT license.

## Build

Run `python3 marketplace/packages/build_lore_store_unlocked.py --output
scratch/lore-store-release --game-assembly /path/to/authorized/Assembly-CSharp.dll`.
The game assembly is read only to fingerprint it and is never archived. The
helper must validate the final archive and descriptor before publication. Do not
insert an unpublished descriptor into the production catalog.
