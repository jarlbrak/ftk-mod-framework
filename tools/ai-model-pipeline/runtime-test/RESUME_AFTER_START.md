# Narrow continuation after accepted start

`resume_after_start.py` resumes only a stopped `new-run` journal whose single
`start_run` request has an explicit successful `starting` response and whose
subsequent requests contain only read-only `/state` observations. Supply the
absolute isolated `--root`, original `--port`, `--origin-journal`, and its exact
`--origin-sha256`. The originating process must still be running, and the
operator must establish that no other caller has advanced its state.

The script preserves the original journal and fresh-process claim. It compares
session, registration, profile, asset, and measured on-disk binary pins; creates
an exclusive continuation claim; then waits for the living single-player party
and checks the selected class and overworld state. Only previously unsent steps
follow: session tutorial suppression, health fixture, native intro dismissal,
dungeon entry, immediate atomic staging, and binding inventory verification.
It never submits `start_run`, retries actions, or resumes from an uncertain
mutation. A failure keeps the continuation claim and journal for inspection.
The resumed wait uses its own label and does not rely on startup-log attributes
initialized by the original fresh-process claim. Only HTTP 500 with the exact native JSON body `{"error":"state read timed out (main thread busy)"}` permits another read-only `/state` observation. Each attempt rechecks source inputs and is logged; the total budget is `operation_timeout`, with 250 ms between attempts. Other HTTP errors, malformed responses, transport timeouts, and all action failures stop without retry.

Validation is offline: `python3 -m unittest discover -s
 tools/ai-model-pipeline/runtime-test -p test_resume_after_start.py` (put the
command on one line). These tests cover uncertain starts, later mutations,
changed configuration, existing combat, once-only uncertain entry, and immediate
staging order. They do not themselves operate the game.
