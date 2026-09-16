---
name: decompile-lookup
description: Verify exact For The King types, fields, enums, and method behavior from the installed assembly.
---

# Decompile lookup

Use when implementation or review depends on game internals.

1. Read `.local/agents/environment.md` if it exists. Obtain the configured managed directory
   without printing credentials or unrelated local configuration.
2. Confirm `Assembly-CSharp.dll` exists. If it does not, report the local prerequisite and stop;
   do not substitute remembered documentation.
3. Use `ilspycmd` against the local assembly. List types first when the exact name is uncertain.
4. For schemas, capture exact declarations and enum order. For behavior, read the complete method,
   including early returns, null guards, calls, and mutation order.
5. Compare the finding with framework assumptions and call out any divergence.
6. Return a compact anchor: assembly identity when available, type/member, exact relevant shape,
   control-flow summary, and implementation consequence.
7. Do not commit the DLL, decompiled output, or substantial copyrighted source.

If a private knowledge service is available, it may reduce repeated investigation, but the live
assembly remains authoritative and the public task must not depend on that service.
