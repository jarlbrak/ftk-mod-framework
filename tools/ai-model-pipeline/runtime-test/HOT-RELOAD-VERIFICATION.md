# Protected hot-reload verification runner

`hot_reload_live.py` talks only to an **already running** protected game at this
checkout's `scratch/hot-reload-game`. It does not launch, deploy, load saves, start
an adventure, or repair an uncertain state. Setup and launching require a separate
explicitly authorized procedure.

Prerequisites:

- The protected profile, session and owned-process receipts agree.
- The current runtime is empty or disabled, at the pristine-title boundary.
- Offline fixture archives/catalog have been seeded with `hot_reload_fixtures.py`.
- No other process or person uses the runtime-test command bridge for the entire run.
- Read the current session token from `model-test-session.json` and pass it explicitly.

From the repository root, replace the placeholders with the actual protected-copy
assembly path, fixture directory and session token:

```bash
python3 tools/ai-model-pipeline/runtime-test/hot_reload_live.py \
  --root "$PWD/scratch/hot-reload-game" \
  --fixtures "$PWD/scratch/hot-reload-fixture-tooling/packages-final" \
  --helper "$PWD/scratch/hot-reload-game/BepInEx/ftkmf/ftkmf-launcher-helper" \
  --game-assembly "$PWD/scratch/hot-reload-game/<test-app>/Contents/Resources/Data/Managed/Assembly-CSharp.dll" \
  --session '<current-session-token>' \
  --cycles 100 --transactions \
  --output "$PWD/scratch/hot-reload-evidence/new-run"
```

The output directory must not exist. The runner uses an exclusive cooperative lock,
rejects an unresolved prior command, checks session/PID throughout, and fails if
another producer replaces a command. Existing command tools do not honor this lock;
exclusive bridge ownership remains a prerequisite. After a timeout, inspect the
outstanding result and coordinator before retrying. Do not delete a lock belonging
to another active runner.

Each cycle prepares and applies enabled then disabled selections with the normal
helper. Checks cover the unchanged PID, epoch advancement, prepared generation,
64/zero identities, repeatable IDs, base resource counts, zero retired resources,
proficiency instance/child counts, and complete exact native row lookups in all five
affected tables, including vanilla definitions. `--transactions` additionally tests four
injected rollback points, install, update, invalid update rollback, removal and
reinstall. Updates use the diagnostic fixture's changed weapon stat and Guardian
binding removal. The run ends disabled.

JSONL files contain requests/results, helper preparation, transitions and full audits.
Audits include process RSS, managed/native allocation samples and Unity object counts.
A successful summary means the programmed assertions passed. It does not establish
an acceptable memory-growth bound, fresh-adventure behavior, arbitrary-package
support, multiplayer support, or post-adventure activation. Those remain separate gates.

Offline syntax check, which does not contact a game:

```bash
python3 tools/ai-model-pipeline/runtime-test/hot_reload_live.py --help
python3 -m py_compile tools/ai-model-pipeline/runtime-test/hot_reload_live.py
```
