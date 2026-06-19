## What this changes

<!-- One or two sentences. Link the issue this closes: "Closes #123". -->

## Checklist

- [ ] The framework builds in Release (`cd FTKModFramework && dotnet build -c Release`).
- [ ] Verified in-game: the `SELF-TEST PASS` lines appear in `BepInEx/LogOutput.log` (paste the relevant lines below).
- [ ] No game DLLs are staged (`Assembly-CSharp*.dll`, `UnityEngine*.dll`, `Newtonsoft.Json.dll`).
- [ ] All custom content IDs go through `IdAllocator` (no hard-coded integer IDs).
- [ ] Docs updated if behavior changed.
- [ ] No em dashes anywhere in the diff.

## In-game evidence

```
(paste the SELF-TEST PASS log lines here)
```
