# Registered row recreation checks

Run `dotnet run --project FTKModFramework/Tests/RegisteredRows -c Release`.

The production Unity-free row ledger is tested against fresh native-shaped arrays:
14 vanilla classes followed by retained custom rows, repeated scene recreation,
partial custom suffixes, post-registration authoring, and collisions/type/gap
rejection without input mutation. Retaining exact row objects preserves capability
registrations and saved positional class IDs without invoking author callbacks.

Native evidence: `TableManager.Initialize` instantiates database prefabs;
`GEDataArray<T>.Awake` and `MakeIndex` rebuild lookup dictionaries from `m_Array`.
The existing Initialize postfix restores registered rows on later initialization.
No native assemblies or decompiled source are included in this test.

The live gate is same-process resume after a native exit to title, including
custom equipment, icons, model resources, and Guardian behavior. Restoration is
validated per table, not a transaction across all databases. Conflicting table
positions are rejected and logged rather than overwriting another row.
