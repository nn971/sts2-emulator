# Native fidelity — first REAL emulator transition

**Scope:** v0.111 Silent, read-only native Inspector oracle, isolated combat
checkpoint against the **existing `PrototypeGameEngine.Step` implementation**.
No changes to Nature/RNG training. Based directly on the real v111 working
emulator branch, not the lightweight Inspector scaffold.

## Why this matters

The previous Inspector PR #121 successfully recorded one first-turn native
`STRIKE_SILENT` action against `SLUDGE_SPINNER` and reproduced its
observable effect through a *special-purpose Strike projection*.
That established the Inspector's capture/fixture/comparison structure,
but **not actual emulator fidelity**.

The new `NativeEngineFidelityV111.Compare`:

1. Requires the versioned `native-expectation-v1` fixture produced by
   `tools/native_expectation_fixture.py` in Inspector PR #121.
2. Reads **only native pre-state** to construct an isolated configured
   `RunState`, preserving real card instance IDs, zone order,
   documented card/creature identity, HP, energy and player state.
3. Resolves native IDs using **the pinned v111 inventory and executable
   registry**, never replacing unknown cards/monsters with a generic proxy.
4. Runs the native-selected legal action using **unchanged
   `PrototypeGameEngine.Step`** with the actual v111 card/effect pipeline.
5. Compares the resulting HP, max HP, block, energy, turn, gold,
   living enemy state, card-zone instance order, card identities/upgrades
   to the **separately recorded native post-state**.
6. Returns a status, all compared values and a first differing JSON path.
   It does NOT claim replay parity for the original complete game.

Initial trace: `passive-20261012-124318892-31fed163df2f4f03b417a53da0bb9428.jsonl`,
decision 1: Strike instance 0 hits Sludge Spinner (`creature:1`),
native HP 41 → 35, energy 3 → 2, card moves hand → discard.
The native checkpoint includes seven cards in hand and six in draw.
The native `SLUDGE_SPINNER` ID **does have a unique executable monster
mapping** in the pinned registry: a text search initially missed it.
A source-shaped synthetic regression exercises these 13 cards and a
real engine step, without putting the user's raw capture in Git.

## Run the actual user-local fixture

Run in the **working emulator** worktree, not the Inspector scaffold,
because the latter does not contain the full game engine:

```fish
git fetch origin feature/v111-native-fixture-engine-fidelity
git worktree add ~/projects/sts2-fidelity-engine \
    origin/feature/v111-native-fixture-engine-fidelity
cd ~/projects/sts2-fidelity-engine
set fixture ~/projects/sts2-inspector-stage/reports/first-strike-expectation.json

dotnet run -c Release --project src/Sts2Emulator.Cli -- \
    native-engine-fidelity "$fixture" Overgrowth 0 \
    | tee ~/projects/sts2-inspector-stage/reports/first-strike-actual-engine.json
```

`Overgrowth 0` selects an **isolated engine sandbox** (region and
ascension), not an assertion about the original game's unobserved run
configuration. Use the region and ascension actually played if known.

Exit codes: `0` = supported isolated engine checkpoint consistent;
`1` = supported isolated engine checkpoint disagrees;
`2` = unsupported/unimportable/native action rejected.
Inspect JSON `status`, `checked_fields`, `first_mismatch_path`,
`blockers` and `unverified_preconditions`.

No game/mod recompilation or new capture is required to run the first
emulator checkpoint comparison from the existing fixture.

## Acceptance levels (do not conflate)

| Level | Evidence | Current status |
| --- | --- | --- |
| Native capture structurally valid | Inspector 2 decisions, 2 actions, 8 events | Demonstrated on user machine |
| Restricted rule projection | 7 of 7 Strike fields | Demonstrated on user machine |
| Strict visible-slice projection | Full recorded JSON checkpoint | Demonstrated on user machine |
| **Actual engine checkpoint comparison** | Imported native pre-state → unchanged `PrototypeGameEngine.Step` → independent native post-state | **User-local command pending**; synthetic CI tests cover contract |
| Native effect/hook ordering | Independently observed phase/event-order correlation | **Not tested** |
| Complete native run parity | All player/combat/hidden state, RNG and event/choice semantics | **Not tested** |

Even when the actual engine checkpoint is consistent, the report
**always** says `native_game_parity=NOT_TESTED`. Missing native
continuation includes relics, potions, card enchantments, RNG state,
pending choice, earlier move history and pre-boundary hooks. An isolated
engine state built from visible data cannot settle these. The generated
prototype RNG from the sandbox is not the original run's native RNG.

## Strict creature identity checks

The fixture importer requires the pre-state's `creature:0` to be the
Silent player, with the same HP, max HP, block and alive flag as the
corresponding `player` fields. The selected card must occur in the
original hand. Invalid or ambiguous pre-states are reported as
`UNSUPPORTED_NATIVE_STATE` before executing the engine.

After a successful engine step, the comparison checks the complete
**multiset** of creature instance IDs (independent of presentation
order), the native species/model identity of each retained instance,
and its HP/max HP/block/alive status. Missing, duplicated or invented
post-state creatures produce `CORE_ENGINE_CHECKPOINT_MISMATCH`, even if
all remaining scalar fields happen to agree. An unrecognized creature
never aliases the player's state.

These checks are stricter about the **recorded visible snapshot**;
they still do not establish native hook ordering, hidden state fidelity,
or complete game parity. Diagnostic event types in the original
Inspector trace remain observations rather than a validated full
source-hook execution journal.

## Next gates for Inspector and emulator

- Expose trustworthy battle/encounter identity, actual act/ascension,
  inventory/potions/relic triggers and pending-choice state at each
  stable decision (while distinguishing hidden vs policy-visible data).
- Harden semantic player-action boundaries. Existing incomplete trace
  reasons repeatedly include `action started without a stable boundary`
  and unfinished transitions. The stop/start frame heuristic should not
  be used to assert ordered hook parity.
- Add native `Defend`, `Neutralize`, `Survivor` nested discard,
  end-turn and enemy action to the genuine engine fixture bridge with
  **explicit** continuation support, not a fallback rule shortcut.
- Collect **native events with actual source hook order** and compare
  against an independently instrumented emulator event journal.
- For RNG-dependent actions, compare deterministic effects under the
  same supplied outcomes and separately audit native stream ownership.
  Nature-policy training remains out of scope.

The Inspector itself remains the independent, passive native oracle.
Do not edit its recorded post-state to match the emulator output.
