# Skyharbor front-end background

Framework 1.4.0 includes an original 3D fortress and airship above a cloud sea, with a
`MODDED EDITION` subtitle beneath the main title. The airship releases its mooring lines,
raises the gangway, departs on a smooth curved route, returns and docks on a two-minute cycle.
Gradual acceleration, a wide turn, gentle heave and restrained lean suggest buoyancy and
inertia. The motion is authored presentation, not a force-based flight simulation.

The scenery persists behind Options, nested settings, New Game and Load Game menus,
Lore Store, Mods, dialogs and loading transitions. The subtitle follows the native main
logo. Party Select retains its native 3D character stage; gameplay and endgame world views
retain their own rendering. This visual scene does not add a playable sky campaign.

## Restore the original background

Exit the game and open `BepInEx/config/com.ftkmf.framework.cfg`. Under `[UI]`, set:

```ini
EnableSkyharborBackground = false
```

Set it back to `true` to restore Skyharbor. The default is `true`. This setting does not
change saves, classes, equipment or multiplayer state. A resource-loading or rendering
allocation failure also falls back to the native presentation for that session.

## Implementation and verification

The framework DLL embeds the scene and textures, so the existing launcher update transaction
delivers the entire feature. There is no extra plugin to install. A dedicated camera renders
owned geometry to an owned overlay canvas behind native UI and fades. Visibility follows
the native title camera and title set rather than the focused menu. Actual character-preview
UI ends replacement; leaving a menu or opening a dialog does not.

The pure flight checks exercise attachment order, clearance altitude, forward alignment,
velocity/acceleration/heading-rate continuity, bounded motion and cycle wrap. Resource checks
compare embedded bytes against the asset manifest and verify texture references and mesh
budgets. Neither check alone proves in-game appearance.

Native visual evidence is limited to the configured macOS game build and 16:9 composition.
Windows, Linux/Proton, other aspect ratios and font fallback appearance need native testing.
See [1.4.0 release notes](releases/v1.4.0.md) for the integrated release verification record.
