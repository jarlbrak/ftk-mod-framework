# Adventures and encounters

The framework can add selectable adventures and overworld encounters. These are authoring APIs, separate from the [Paladin content package](../marketplace/packages/paladin/content.json), which contains a class and equipment rather than an adventure. See [write a content mod](WRITING-CONTENT.md) for the core entry format and [campaigns](CAMPAIGNS.md) for quests inside an adventure.

## Adventures

An adventure is a `GameDefinition` loaded from an installed `.ftk2` file, identified by its string `m_SaveFileName`. `Adventures.AddFromTemplate` takes the mod GUID, a unique save-file name, the name of a locally installed template, display text, and a JSON configuration callback. It clones the template at runtime and registers a new preview without shipping a copy of the game's source file.

The framework keeps registered names visible through the game's adventure-name gate. The game's generation and victory logic then read the cloned definition. Custom realms, bosses, and quest chains require their own route-specific validation; this simple example establishes none of them.

Saves store the adventure name as a string. Without the authoring mod, a custom-adventure save cannot load until the mod is restored. Co-op clients need the same adventure definition; identical bytes and a solo start do not establish co-op compatibility.

## Encounters

An overworld encounter is a `FTK_miniEncounterDB` row. `Content.AddEncounter` takes the mod GUID, a stable local ID, a vanilla template, a display name, and a configuration callback. It clones the row and registers it under a deterministic ID. The native selector considers eligible registered rows during its ordinary weighted draw.

`m_RealmInclude` and `m_RealmExclude` control realm eligibility. `m_Rarity` uses existing `Common`, `Uncommon`, `Rare`, or `SuperRare` draw buckets. Test ordinary acquisition and display in game; successful registration alone does not prove a row was selected.
