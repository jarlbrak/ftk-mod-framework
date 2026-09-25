## What this changes

<!-- One or two sentences. For each fully resolved issue, include "Fixes #123" or
"Closes #123" in the PR body outside this comment. Use "Refs #123" for partial work
or unmet acceptance gates. Closure occurs when the fix reaches the default branch. -->

## Checklist

- [ ] Every fully resolved issue has a closing keyword in this PR body; partial work uses `Refs` (or no issue applies). Verify issue closure after merge.
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
