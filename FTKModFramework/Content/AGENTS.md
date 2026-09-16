# Bundled content instructions

Read the root and `FTKModFramework/AGENTS.md` first.

- Author bundled examples as consumers of the public `Content.*` and adventure APIs.
- Do not reach into `Core/` to bypass a missing public primitive. Propose the smallest API extension
  and have it reviewed first.
- Clone the closest verified vanilla template and change only intended fields.
- Use stable string keys and framework allocation. Never hard-code custom enum integers.
- Keep samples gated by the existing sample-content configuration.
- Add focused self-test coverage for registration and resolution, then verify visible behavior in
  game before claiming the example works.
