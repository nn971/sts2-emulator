# References and prior art

These projects are especially relevant to emulator fidelity, RNG handling, state forking, and native parity. Re-check current versions before copying assumptions.

## CombatSolver — Torch1230

<https://github.com/Torch1230/CombatSolver>

Why it matters:

- combat prediction engine embedded in a real mod;
- explicit fork/state-store architecture;
- full RNG-state handling for prediction branches;
- separation between live game and prediction world;
- native model cloning lifecycle and differential verification work.

Read first:

- `docs/ARCHITECTURE.md`
- `src/Engine/README.md`

## sts2-simulator — yizhang-dream

<https://github.com/yizhang-dream/sts2-simulator>

Why it matters:

- whole-run scope;
- live-game C# bridge;
- parity replay harness;
- explicit data/build versioning lessons.

Its trace/parity workflow is useful prior art even if this repository keeps a different canonical engine design.

## slay-the-spire-2-emulator — Zamiell

<https://github.com/Zamiell/slay-the-spire-2-emulator>

Why it matters:

- C# parity-critical core;
- .NET 9 + NativeAOT direction;
- Python-facing native binding approach;
- explicit separation between deterministic mechanics and external research code.

## Design stance

Prior projects provide ideas, implementation lessons, and test cases. The authority for this repository is a pinned native game build plus reproducible differential evidence.
