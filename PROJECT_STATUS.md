# Project status

**Stage:** Milestone 0 scaffold.

Implemented now:

- emulator charter and subrepo boundary;
- C# canonical engine interfaces;
- generic forkable RNG-state container;
- canonical JSON/hash bootstrap;
- trace DTOs/schema;
- first-divergence trace diff;
- invariant tests;
- microbenchmark scaffold;
- CI and issue templates.

Not implemented yet:

- native game bridge;
- STS2 RNG codec(s);
- actual STS2 mechanics;
- whole-run replay engine;
- production language binding.

Search, strategic databases, and AI learning are intentionally outside this repository and belong in the future parent project.

The current semantic emptiness is intentional: the emulator should not manufacture fake coverage before the native reference pipeline exists.
