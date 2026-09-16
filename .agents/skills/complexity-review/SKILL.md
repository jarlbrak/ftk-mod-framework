---
name: complexity-review
description: Review an FTK design or diff for accidental complexity and a smaller faithful alternative.
---

# Complexity review

1. State the behavior and invariant the design must preserve.
2. Trace concepts, state, effects, boundaries, and recovery paths introduced by the change.
3. Flag duplicated policy, hidden state, speculative extension points, single-caller abstractions,
   unnecessary hooks, and representations that can disagree.
4. Distinguish essential complexity imposed by FTK/Unity from accidental framework complexity.
5. Recommend the smallest alternative that preserves behavior and proof quality.

Return concrete findings with files and tradeoffs, not a style preference score.
