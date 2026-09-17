# Repository automation instructions

Read the root `AGENTS.md` first.

- CI must stay runnable without the game. Any check needing game assemblies belongs in a documented
  live gate, not in a workflow.
- The `guard` job enforces repository policy: the agent instruction graph and the rule that no
  copyrighted game assembly is ever tracked. Do not weaken either to make a build pass.
- Issue templates encode the epic, spec, and work-item hierarchy the project plans against. Keep
  them aligned with the matching skills.
- Pin actions by major version and keep the platform matrix honest. Dropping a platform from CI
  means dropping the support claim from the docs too.
- Pull request bodies carry no AI-attribution boilerplate.
