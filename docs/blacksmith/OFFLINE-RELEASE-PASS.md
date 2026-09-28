# Blacksmith offline release pass, 2026-09-26

## Result

Current source checks pass. Public compatibility fails on the actual latest
published macOS helper, v1.5.0. Distribution-rights evidence remains missing.
This review performs no publication and does not establish live gameplay.

| Check | Result |
|---|---|
| Package validator | 30 unrestricted items, four bands, 141 hashed assets passed |
| ClassAffinity | 25 assertions passed |
| HotReloadResources | 321 checks passed |
| PlayerMods | Passed |
| Framework Release build | Zero errors, seven existing CS0649 warnings |
| Helper `go test -count=1 ./...` | Passed, uncached |
| Runtime helper boundary tests | 72 passed before new companion fixture; three new fixture tests also passed |
| Current helper archive validation | Passed |
| Archive versus package | All 143 files match byte-for-byte |
| Markdown relative links and `git diff --check` | Passed |

Candidate ZIP SHA-256:
`dd48a48e0c2f385dca8b72802111c6f15c83b45300fe775b563d50a6e2e6b732`.
Compressed size: 75,427,490 bytes. Expanded size: 128,481,995 bytes.

## Published compatibility failure

The latest release checked is v1.5.0, published 2026-09-26T15:19:15Z,
commit `37e0ba8a2f1ff1cf8f8b5296918f9a3bd9be0a98`.
Its actual downloaded macOS helper matches the GitHub asset SHA-256
`f5d37810f21334eb55eb1af8c872e92e0edc58a31f1d55ab9a756233bba33f95`.
Validation of the exact candidate exits 1:

```text
content.json: json: unknown field "classAffinity"
```

Tagged ContentEntry has no affinity declaration and tagged marketplace decoding
rejects unknown fields. The declared package minimum 1.0.1 is therefore not a
verified compatible public release. Publish and verify the framework capability
before selecting a real minimum and rebuilding the candidate. No future version
number is assigned here.

## Rights

Campaign provenance and spending receipts establish lineage and quoted costs.
They contain no saved applicable distribution-rights grant or account entitlement.
Neither successful generation nor quoted spending establishes that entitlement.

See [release review](RELEASE-REVIEW.md) for live evidence and remaining gates.
