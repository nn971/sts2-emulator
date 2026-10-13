# Actual-engine batch comparison of recovered native combat checkpoints

**Input evidence:** seven native single-player Silent combats in the
2026-10-12 Inspector corpus, processed by Inspector PR #131 using
`tools/native_prefix_checkpoints.py`. The original 14 raw JSONL files
are unchanged between the user's two uploaded archives; the newer
archive adds review reports and eight independently closed play-card
fixtures.

This batch uses the **actual unchanged** `PrototypeGameEngine.Step`
comparer of PRs #127–#129. It does not use an independent rule checker,
reconstruct a full run between fixtures, or consume a native RNG stream.

## Native fixture inventory

| Encounter | Decision | Native selected card | Native result |
| --- | ---: | --- | --- |
| Sludge Spinner | 1 | Sucker Punch | target 41→33 HP, Weak 0→1, energy 3→2 |
| Sludge Spinner | 2 | Defend | player block 0→5, energy 2→1 |
| Sludge Spinner | 3 | Strike | target 33→27 HP, energy 1→0 |
| Terror Eel | 1 | Neutralize+ | target 150→146 HP, Weak 0→2, energy unchanged |
| Terror Eel | 2 | Backflip | block 0→5, draw two cards, energy 3→2 |
| Terror Eel | 3 | Defend | player block 5→15, energy 2→1; **two** native card-play-start, block-gain and finish history entries, one energy payment |
| Terror Eel | 4 | Pinpoint | target 146→131 HP, energy unchanged |
| Terror Eel | 5 | Sucker Punch | target 131→123 HP, Weak 2→3, energy 1→0 |

The Terror Eel Defend fixture is especially important: the history
shows two 5-block gains during **one native action-executor envelope**.
An emulator that assigns 10 base block to Defend would be the wrong
causal explanation. The trigger of the second card execution is
**not identified** in the current native source-hook capture.

Seven of the eight fixtures begin with enemy powers already present
(Weak or Terror Eel's Shriek). The current strict importer **rejects
non-empty native pre-enemy powers**, rather than inventing state, so
these are expected to return `UNSUPPORTED_NATIVE_STATE` until we
implement and independently check power continuation. This is an
explicit limitation, **not a fidelity failure**.

The first Sucker Punch starts without enemy powers but the native
post-state has Weak. Previously, power effects were not compared, so
an otherwise matching checkpoint could misleadingly return
`CORE_ENGINE_CHECKPOINT_CONSISTENT`. The comparer now returns
`CORE_ENGINE_CHECKPOINT_PARTIAL` when any native visible creature
power remains unchecked, while preserving its compared fields and
ordered emulator event journal. This is not an assertion that Weak was
implemented correctly.

## New batch command

Keep the user's WSL top-level project directory unchanged: only
`sts2-ai`, `sts2-emulator`, and `sts2-inspector` (the latter is
a **linked Git worktree of the emulator**, not a separate repository).

Once the working `~/projects/sts2-emulator` is clean and on the batch
branch, run:

```fish
cd ~/projects/sts2-emulator
set fixtures /mnt/d/SteamLibrary/sts2-inspector-traces/fixtures

dotnet run -c Release --project src/Sts2Emulator.Cli -- \\
    native-engine-fidelity-batch "$fixtures" Overgrowth 0 \\
    > /mnt/d/SteamLibrary/sts2-inspector-traces/actual-engine-batch.json

jq '{status, total_fixtures, actually_stepped, consistent, mismatched,
    unsupported_or_invalid, entries: [.entries[] |
    {file, card_model, native_enemy_model, decision_id, status,
     first_mismatch_path, blockers, unverified_preconditions}]}' \\
    /mnt/d/SteamLibrary/sts2-inspector-traces/actual-engine-batch.json
```

`Overgrowth 0` denotes the **isolated engine sandbox** in every
comparison. It is **not** a verified assertion about the original
Terror Eel game's Act/ascension. The batch runs each fixture independently.

Statuses:

- `CORE_ENGINE_CHECKPOINT_CONSISTENT`: all fields covered by the
  **current** comparer agree; undisclosed continuation and native hook
  parity still `NOT_TESTED`.
- `CORE_ENGINE_CHECKPOINT_PARTIAL`: actual engine executed, other
  checked fields may agree, but at least one **visible power state** was
  not compared. Never count this as an all-effects match.
- `CORE_ENGINE_CHECKPOINT_MISMATCH`: an actual engine checkpoint
  differs on a supported field; see `first_mismatch_path`.
- `UNSUPPORTED_NATIVE_STATE` and other error statuses: do not
  interpret as engine divergence. Examine `blockers`.
- `ALL_SUPPORTED_CHECKPOINTS_CONSISTENT`, `CHECKPOINT_MISMATCHES_DETECTED`,
  `PARTIAL_COMPARISON_UNSUPPORTED`, `NO_FIXTURES`: batch summary;
  an empty batch or any partial/unsupported fixture cannot pass.

The batch includes all filenames, per-fixture blocker and unverified
precondition arrays, number of compared/matching fields, native enemy
and selected card names, and number of emulator journal observations.
For the **full actual engine event journal** on an eligible card,
use the existing single-fixture `native-engine-fidelity` CLI.

## Next steps

1. Run batch against the **actual** user-local fixture files and
   inspect blockers without weakening source identity or silently
   dropping enemies/powers.
2. Implement correct importer reconstruction and checks of native
   combat statuses/powers with matching application and expiry
   semantics. Preserve power ordering/application metadata if source
   effects require it.
3. Add independently observed native source-hook callbacks for
   automatic card replays, relic/potion triggers, damage/block, powers,
   hand/draw/discard/exhaust and enemy-turn transitions.
4. Test full native nested-choice identity instead of only inferring
   candidate discarded card IDs from zone deltas.
5. Repeat batch after each emulator change, first mismatching
   native/emulator event and state path recorded separately.
   **Exact native RNG stream parity and Nature training remain deferred.**
