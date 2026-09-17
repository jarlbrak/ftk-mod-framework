# Model campaign instructions

Read the root `AGENTS.md` first.

- Each directory is one model campaign: authored geometry, its source description, its piece
  mapping, and its validation record. Keep those four consistent within a campaign.
- Committed `.glb`, `.json`, and `README.md` files are the durable record. Blender work files, bulk
  captures, video, and logs stay outside ordinary git history.
- A render is not integration. Do not describe a campaign as working in game without the route's
  live binding and motion evidence.
- Regenerate derived reports through the campaign's own build script rather than editing them.
- Preserve the stated scope of a campaign. Extending it to another rig is separate work with its own
  evidence.
