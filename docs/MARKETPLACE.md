# In-game Mods marketplace

Open **Mods** on the title screen to discover and manage curated community
content. The 1.0.0 catalog contains [Paladin](../marketplace/packages/paladin/README.md)
for the verified macOS game build. Select a mod on the left to see its banner,
description, requirements and version details on the right. **Installed** also
shows managed and manually installed content.

## Install and manage a mod

Select a listing, review the proposed package and dependency changes, then
confirm. The Mods menu reports what is installed, enabled or pending and offers
the next available action. Install, update, enable, disable and remove normally
take effect on the **next game launch**; they do not change registrations already
loaded in the current adventure. You can cancel a prepared change before it
takes effect.

The optional [title-screen activation mode](HOT-RELOAD.md) supports
same-process changes for Paladin or an empty managed selection on the audited
macOS build. It must be enabled before launch and used before adventure setup,
save loading or Lore Store entry. It uses separate save libraries and does not
support multiplayer. Other platforms and mod sets use next-launch activation.

Existing saves may depend on their exact framework and mod versions. Start a new
run when changing content, or restore the matching set for an existing run.
Removing a mod never edits the save. A mod-list export records versions and
archive hashes, but cannot prove save or multiplayer compatibility.

## Repair and recovery

**Install / Repair** restores a missing or mismatched marketplace helper.
The launcher bundles that helper with the framework; they must be compatible.
If an update is interrupted or an installed version is damaged, close the game
and use **Restore bundled** from the launcher package. See the
[installation guide](INSTALL.md) for setup and troubleshooting.

The menu keeps previous managed package generations for recovery. Rollback
selects the previous generation for a later launch. Do not manually edit
marketplace state while the game runs.

## Submit a mod

The marketplace accepts reviewed JSON content, approved PNG/JPEG artwork and
bounded GLB models, including supported Guardian and equipment declarations.
It does not distribute executable behavior DLLs, arbitrary BepInEx plugins,
native libraries, scripts or campaign files. Paladin's
[package source](../marketplace/packages/paladin/README.md) is the shipped example.

Use the [package submission template](../.github/ISSUE_TEMPLATE/package_submission.yml)
to provide permanent identity, author and license, version and framework
compatibility, game/platform evidence, content description, assets and rights,
and support links. Maintainers validate the exact archive, review its content
and test advertised behavior in the game before catalog publication. Each
published version keeps its original bytes and SHA-256; corrections require a
new version. The [mod versioning guide](MOD-VERSIONING.md) explains compatibility,
and the [catalog guide](../marketplace/README.md) describes maintainer validation.

Catalog previews may include up to three approved PNG or JPEG images. Put the
cover image first. Each image is limited to 2 MiB, 4096 pixels per side and
8,388,608 pixels in total. Credit creators and confirm distribution rights.

## Scope of the 1.0.0 evidence

Paladin registration, native mod management, a new game and fresh-process
resume, a controlled Guard hit, and controlled loot collection and cross-class
equip were observed on the verified macOS game build. Controlled fixtures do
not establish natural drop frequency, every accessory presentation or full
campaign balance. Windows and Linux/Proton gameplay and online co-op remain
unverified. See the [release notes](releases/v1.0.0.md) for the release scope.
