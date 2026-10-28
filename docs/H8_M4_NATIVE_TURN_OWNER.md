# H8 M4: native automatic-turn owner — work and acceptance

This draft is stacked on #216 and is an **experimental native
continuation migration**, not the accepted training runtime.

## Goal and architectural split

H8 must eventually own the actual combat mechanics, the suspended
effect/choice cursor, and the provider decision sequence. The external
stochastic layer (native seeded RNG, ordinary reference Chance Law, and
learned Nature) must remain interchangeable, and decisions must be
reproducible from an explicit ordered journal.

The first M4 slice adds `H8NativeTurnContinuationV1` with a compact
`Cursor` carrying the live `RunState`, physical automatic hand-draw
progress and terminal/nested status. It directly resumes the real
`PrototypeGameEngine` automatic pipeline after a committed physical
draw and checks the next player boundary. Only at the output edge does
it reconstruct a v111 end-turn projection for still-unmigrated mechanics
and explicit exact-state auditing.

This does **not yet** eliminate the per-choice v111 projection. The
further architectural change is to let the H8 session retain this
cursor over multiple Nature/player decisions, replaying only compact
effects and constructing v111 output on explicit audit or unsupported
site fallback. That change requires strict equivalence for all
resumptions, including terminal transitions, nested stochastic
suspensions, and subsequent turns. There is no standalone speed
claim for M4.

## Known blocker

On the initial M4 commit `6c7c4d2`, CI built successfully but the
source-valid multi-boundary test
`DirectDrawAndM2BridgeRemainEquivalentAcrossSubsequentBoundaries`
failed on a canonical state fingerprint divergence, despite eight
single-draw parity matches and 32/33 passing targeted tests.
**Do not merge or train with M4 until that regression is fixed.**

Further commits added first-differing-snapshot diagnostic logging and
aligned the intermediate physical-hand-draw versus resumed-run write
order with the old semantic continuation. Those changes have not yet
established a passing parity result. Keep the last accepted M3 revision
`33bf8cc0318292357ca00da73407b1f5335a9731` for training and the
long steady-state experiment meanwhile.

## Hard acceptance gates

1. Build `Sts2Emulator.sln`, run
   `H8CombatRuntimeV1Tests`, `SemanticDeckStateV111Tests`,
   `CanonicalJsonTests` and the JSONL H8 replay integration test.
2. Verify multiple real A10 draws and subsequent turns against the
   old v111 mechanical state and exact journal after *each* decision,
   including stale token and fork independence checks.
3. Run 128 A10 combats/15 workers with the frozen shared Transformer:
   exact H7/H8 source decisions, weighted Chance Law, 128/128 terminal
   outcomes and 16/16 sampled replay, without allowing native
   reference-game fidelity checks to block ordinary training.
4. Only then perform an unprofiled pinned A–B–B–A comparison against
   M3 to test end-to-end rollout gain, with actor batch, collation,
   forward and GPU load controls. Preserve negative results.

## Validation (fish)

```fish
git -C ~/projects/sts2-emulator fetch origin
git -C ~/projects/sts2-emulator switch research/h8-native-turn-owner-20261028
dotnet build ~/projects/sts2-emulator/Sts2Emulator.sln -c Release -m:1
dotnet test ~/projects/sts2-emulator/Sts2Emulator.sln -c Release --no-build \
    --filter 'FullyQualifiedName~H8CombatRuntimeV1Tests|FullyQualifiedName~SemanticDeckStateV111Tests|FullyQualifiedName~CanonicalJsonTests'
```

**Current status:** first build succeeded; first extended-state test
failed; subsequent diagnostics/revision CI is still pending. The stable
benchmark in AI #126 deliberately targets M3, independently of M4.
