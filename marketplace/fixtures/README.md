# Marketplace integration fixtures

`smoke-package` is test content, not a community catalog entry. It clones the same `bladeDagger` template used by the existing sample data mod and changes only its stable ID and display name. It does not add a playable class or touch saves.

Package only the JSON files from `smoke-package`. The successful in-game registration log is:

`Data: registered weapon 'marketplace_check_dagger' (template bladeDagger, 0 base field(s)).`

Keep this package out of published catalogs. Test its installation in an isolated managed state, then cancel or roll back the test selection before returning to normal play. The repository license applies to these original JSON fixtures; no game binaries or assets are included.
