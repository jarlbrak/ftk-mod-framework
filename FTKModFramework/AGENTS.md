# Framework instructions

Read the root `AGENTS.md` first.

- Runtime code targets Unity 2017.2.2p2, Mono, and .NET 3.5.
- Build from this directory with `dotnet build -c Release`.
- Game assembly references come from the local installation and must never enter output or git.
- Treat public `Content.*` compatibility as a product contract. Extend it deliberately.
- A successful build is game-free evidence only. Registration, UI, animation, save, and gameplay
  claims require the matching live-game check.
- Changes under `Core/`, `Content/`, and `Agent/` must also follow their nearest instructions.
