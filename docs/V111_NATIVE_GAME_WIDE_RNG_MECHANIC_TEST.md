# Native v0.111 RNG + implemented-mechanic test: game-local handoff

This procedure applies to **single-player Silent** and the **review-only**
`feature/v111-native-rng-stream-checkpoint-readiness` branch stacked on
Inspector PRs #137–#140. It does not alter the active four-Act branch or
the user's existing `~/projects/sts2-emulator` checkout.

## What the experiment can establish

1. The actual installed `sts2.dll` matches a recorded SHA-256 pin.
2. An opt-in Inspector DLL compiles and loads in that game and its passive
   source hooks report their individual installation flags.
3. At combat attachment, between executor callbacks, and at combat end,
   **12 run + 3 player RNG states** are copied and verified for correct
   xoshiro advancement. Every observed primitive can be checked for the
   exact original outcome and for consistency with the checkpoint cursor.
4. The corpus reports all named streams, consumed steps, instrumented
   integer/double/bool steps, and **unobserved** steps. It distinguishes a
   source hook not installed, an installed but unexercised stream, a captured
   primitive with unknown semantic eligibility, and conflicting evidence.
5. The executable emulator registry provides a per-item list of *implemented
   and integrated* mechanics. Each is labelled according to whether the
   existing native Inspector/actual Step importer even offers a relevant
   comparison interface. Engine fixtures remain independent of native
   post-state and require complete importable pre-state.

**This is a coverage audit, not a certificate of full-game parity.**
Unknown contextual generators, map/rewards/events/shops outside the combat
observer, and non-integer/non-unit-double/non-boolean primitives can remain
unobserved. All original outcome pool order/weights, eligibility rules and
nontrivial hooks require their own native/engine differential evidence.
An item marked `CONDITIONAL_VISIBLE_CHECKPOINT_IMPORT_ONLY` may still be
blocked by hidden state, status effects or incomplete import semantics.

## 1. Isolated WSL build against the actual installed Windows game

Use **fish** in WSL. Clone outside `~/projects` so the active work is safe:

```fish
set runner (mktemp -d /tmp/sts2-rng-mechanic-check.XXXXXX)
git clone --depth 1 --single-branch \
  --branch feature/v111-native-rng-stream-checkpoint-readiness \
  https://github.com/nn971/sts2-emulator.git "$runner"
cd "$runner"
find /mnt/d/SteamLibrary -name sts2.dll -type f
```

Set `GAME_ROOT` to the real folder containing
`data_sts2_windows_x86_64`; the value below is an **example**.

```fish
set -gx GAME_ROOT "/mnt/d/SteamLibrary/steamapps/common/Slay the Spire 2"
set -l data "$GAME_ROOT/data_sts2_windows_x86_64"
set -l trace /mnt/d/SteamLibrary/sts2-inspector-traces
mkdir -p "$trace"
python tools/native_game_preflight.py "$data" --out "$trace/game-pin.json"
sha256sum "$data/sts2.dll"
scripts/build-native-inspector.fish "$GAME_ROOT"
```

The preflight tool must report `dependencies_present=true`.
**Stop if the native mod fails to compile.** Save full compiler diagnostics;
remote standalone .NET tests deliberately compile the non-native branch.
`build-native-inspector.fish` installs its mod only inside
`$GAME_ROOT/mods/Sts2ReferenceMod`.

## 2. Windows Steam environment and short recording

In Steam launch options, substitute the **actual continuous 64-character**
`sts2.dll` SHA-256 from the preceding command, with no spaces inside:

```text
cmd /c "set STS2_FIDELITY_CAPTURE=1&&set STS2_FIDELITY_RNG_HOOK=1&&set STS2_FIDELITY_RNG_STREAM_CHECKPOINTS=1&&set STS2_FIDELITY_EXPECT_SHA256=PUT_EXACT_64_CHARACTER_SHA256_HERE&&set STS2_FIDELITY_TRACE_DIR=D:\SteamLibrary\sts2-inspector-traces&&%command%"
```

The three opt-in toggles and the expected hash must be set in the
**Windows game process**, not only in WSL. Keep the game single-player.
Check its log for `CAPTURE_STATUS=HOOK_ENABLED` and
`RNG_HOOK_STATUS=INSTALLED_BOUND_INT_OVERLOADS`, plus
`double_source_hook` and `bool_source_hook` flags. If either
optional scalar method is unavailable, preserve its log; coverage will
record the missing primitive family.

First perform a **short smoke**: start a Silent fight, play several cards,
end two turns and finish or leave combat. Keep at least one fight with
multiple enemies. Then exercise normal gameplay with:

- Initial shuffle, draw/discard recycling, exhaust and generated cards.
- Random monster intents and target selection, poison/powers, relic and
  potion interactions, death/summon/transform hooks, nested choices.
- Both Overgrowth and Underdocks when practical; later Hive and Glory
  fights where naturally reachable.
- Events, rewards, shops, rest sites and route choices **as coverage
  requests**: combat-only Inspector may not provide a replayable native
  decision for them. Their absence should remain visible in the matrix.

Different encounters and Ascensions are useful; annotate
`game SHA / seed / character / Ascension / region / encounter / actions`
for each captured scenario. Do not infer a global parity result from
one encounter.

## 3. Offline stream verification, corpus and mechanics matrix

Run from the isolated checkout (fish):

```fish
set trace /mnt/d/SteamLibrary/sts2-inspector-traces
for sidecar in "$trace"/*.actions.jsonl
    python tools/native_rng_boundary_verify.py "$sidecar" \
        --out "$sidecar.primitives.json" > /dev/null
    python tools/native_rng_checkpoint_audit.py "$sidecar" \
        --out "$sidecar.checkpoints.json" > /dev/null
end
python tools/native_action_envelope_batch.py "$trace" \
    --out "$trace/native-corpus.json" > /dev/null
dotnet run --project src/Sts2Emulator.Cli -c Release -- \
    v111-coverage > "$trace/live-emulator-coverage.json"
python tools/native_fidelity_testability_matrix.py \
    "$trace/live-emulator-coverage.json" --trace-dir "$trace" \
    --out "$trace/implemented-mechanic-testability.json" > /dev/null
jq '{files_checked,
     native_rng_hook_installed_sidecars,
     native_rng_stream_checkpoints_enabled_sidecars,
     native_rng_checkpoint_count,
     native_rng_checkpoint_errors,
     native_rng_checkpoint_unobserved_advances,
     native_rng_source_calls,
     native_rng_scalar_source_calls,
     native_rng_primitive_errors,
     native_rng_stream_conflicts,
     traces: [.traces[] | {
       file, status, native_rng_checkpoint_status,
       native_rng_checkpoint_unobserved_advances,
       native_rng_checkpoint_count}]}' "$trace/native-corpus.json"
jq '{implemented_integrated_items, native_game_validated_items,
     mechanics_exhaustively_testable_now,
     all_native_rng_streams_match_game_mechanics,
     by_kind}' "$trace/implemented-mechanic-testability.json"
```

Older trace files without checkpoint events will correctly report
`NATIVE_RNG_CHECKPOINTS_UNOBSERVED`. Review **new** sidecars by their
filenames and source-header installation flags. The report
`OBSERVED_PERSISTENT_STREAM_ADVANCES_ACCOUNTED_FOR` means the
observed persistent-stream checkpoint intervals are fully explained by
captured source primitives, **not** that those calls implement the
correct outcome pool or mechanic hook order. An all-zero stream is
unexercised: choose a mechanic actually consuming it.

For independent real-engine testing, generate the closed-prefix native
fixtures with the existing `native_prefix_checkpoints.py`, then run
`native-engine-fidelity-batch` or the unified gate. Consult
`docs/V111_NATIVE_ENGINE_FIDELITY.md` and
`docs/UNIFIED_RNG_FIDELITY_STATUS.md` for strict importer limitations.
Do not count unsupported fixtures as passed.

## 4. Return evidence to the development thread

Please upload:

1. **Build/log result**: output from `native_game_preflight.py`, build
   diagnostics if any, and the game's `RNG_HOOK_STATUS` log line(s).
2. **Fresh captures**: at least two newly generated
   `passive-*.actions.jsonl` files, preferably one multi-enemy and one
   shuffle/power/potion encounter. Include matching strict
   `passive-*.jsonl` if available.
3. **Machine-readable audits**: `native-corpus.json`, the matching
   `*.primitives.json` and `*.checkpoints.json`, and
   `implemented-mechanic-testability.json`.
4. Short notes on the encounter/seed/Ascension and what mechanics were
   exercised. Existing long traces can remain on disk; prefer a small
   reproducible subset.

Before sending, inspect files for personally identifying data or
accidental local environment values. **Do not upload the game's DLLs.**

## Remaining fidelity gates after the first capture

- Exact installed-game `#if STS2_NATIVE` compilation and callback
  attachment, source-call observation and hook installation flags.
- Per-mechanic candidate pool order, conditional weighting, local/context
  generator identity and source call parentage.
- Native action + emulator step equivalence for **each implemented mechanic
  and variant**, including powers/relics/potions, bosses and automatic play.
- Original game RNG parity across context/map/events/rewards/shops,
  including source primitives not yet observed in the Inspector.
- Complete supported sequence coverage from combat entry to end, then
  whole-run parity on distinct seeds and Act regions.

Keep all statuses `NOT_TESTED` until independent evidence satisfies
the specific gate. Recording 15 stream snapshots is one necessary layer
for a comprehensive test, not the final layer.

## 2026-10-15: NextFloat replay to close the monster-AI RNG gap

Use the **new isolated review branch**
`feature/v111-native-nextfloat-source-fidelity` instead of the older
checkpoint-readiness branch in the clone command above. Rerun
`scripts/build-native-inspector.fish "$GAME_ROOT"` against your installed
`sts2.dll`, then restart Steam's game process. The usual
`STS2_FIDELITY_CAPTURE=1`, `STS2_FIDELITY_RNG_HOOK=1`,
`STS2_FIDELITY_RNG_STREAM_CHECKPOINTS=1`, and full
`STS2_FIDELITY_EXPECT_SHA256` pin remain required.

The pinned source has `Rng.NextFloat(float max)` delegate to
`NextFloat(float min,float max)`; the latter consumes one xoshiro step,
including when the bounds are equal. The new observer hooks only the
**two-argument method**, so a one-argument call emits one event.
`RandomBranchState.GetNextState` calls `NextFloat(max)` to sample the
weighted monster-intent interval. Hook installation logs now include
`float_source_hook=INSTALLED|UNAVAILABLE`, and the sidecar header has
`native_rng_float_hook_installed`.

**Targeted smoke test:** play a Silent combat with enemies whose moves
vary stochastically (preferably matching the earlier multi-enemy test),
allow multiple enemy turns, then close/end combat so the sidecar flushes.
Perform the analysis on **only the fresh capture(s)**, avoiding older
sidecars which predate this hook:

```fish
set trace /mnt/d/SteamLibrary/sts2-inspector-traces
set sidecar (ls -t "$trace"/passive-*.actions.jsonl | head -n 1)
python tools/native_rng_boundary_verify.py "$sidecar" \
    --out "$sidecar.primitives.json"
python tools/native_rng_checkpoint_audit.py "$sidecar" \
    --out "$sidecar.checkpoints.json"
jq '{status, source_calls, scalar_calls, float_calls,
     primitive_error_count, stream_conflict_count, stream_gap_count}' \
    "$sidecar.primitives.json"
jq '{status, checkpoint_count, error_count,
    monster_ai: .streams["run.monster_ai"],
    unobserved_rng_advances}' "$sidecar.checkpoints.json"
jq -c 'select(.type == "native_rng_scalar_boundary" and
    .data.operation_kind == "range_float") |
    {ordinal, action_id, stream: .data.stream_id,
     owner: .data.owner_id, bounds: [.data.float_min, .data.float_max],
     value: .data.selected_float}' "$sidecar"
```

The **previous authentic combat recording** had 55 verified integer calls,
88 native 15-stream checkpoints and **nine unobserved monster-AI
advances**. This is a *baseline diagnostic*, not an identical-seed replay.
On the new capture, require:

- `float_source_hook=INSTALLED` in the game log and
  `native_rng_float_hook_installed=true` in the sidecar header;
- `range_float` source events on `run.monster_ai` for every weighted
  random intent roll actually exercised;
- zero `primitive_error_count` and zero `stream_conflict_count`;
- zero unexplained `run.monster_ai` advances **within captured checkpoint
  windows**, or explicit investigation of any remaining gap;
- the sampled native float values and xoshiro transitions checked exactly
  by the verifier, while original move weights/candidate pools remain
  semantically `NOT_TESTED` until separate native eligibility evidence exists.

If the new `run.monster_ai` gap closes, continue with additional acts,
status/power interactions and other streams via the full coverage plan.
Upload the fresh `*.actions.jsonl`, two `*.json` verification reports,
and the hook installation log. Native effects and all implemented mechanics
still need their own differential tests; this pass certifies only the
source RNG observation of the exercised operations.

## Relationship to issue #143 and the revised semantic Nature design (Oct 15)

**Authoritative specification:** [Desired RNG Implementation](DESIRED_RNG_IMPLEMENTATION.md),
updated in PR #154 and incorporated into the independent playable Nature
work tracked in [issue #143](https://github.com/nn971/sts2-emulator/issues/143).
The source-level `NextFloat` probe in this Inspector branch is a
**read-only native RNG primitive audit**. Its emission is not a Nature
`weighted_enemy_intent` choice, and it must not create an additional
controlled-chance ticket or change the emulator provider state.

The following contracts stay separate:

- **Native fidelity mode:** faithfully observe one `Rng.NextFloat(float,float)`
  source step, its original bounds/result, persistent RNG owner and exact
  xoshiro cursor. A sample taken inside `MonsterModel.RollMove` establishes
  source RNG consumption and creature attribution. This by itself does
  not establish the conditional weights, eligible moves, or resulting intent.
- **Semantic controlled-Nature mode (#143):** select from an
  explicitly enumerated legal weighted intent support computed by the
  common mechanics engine at the actual commitment boundary, retaining
  enemy move history, anti-repeat filters and visibility. An intent
  selected here becomes public before the next player choice. The source
  RNG cursor is neither advanced nor shadowed merely to match a
  counterfactual Nature outcome; `probabilityStatus=UNVERIFIED` remains
  until the reference law is audited.
- **Provider-owned deck state:** native RNG may commit the shuffled deck
  order ahead of time, while an opt-in Nature provider can maintain
  unresolved constrained card-instance positions and issue `draw_card`
  or `reveal_card` events on demand. Exact insertions, returned known-top
  cards and previously revealed positions remain forced. The **legacy**
  controlled fixture's Fisher–Yates swap tickets and the new semantic
  deck provider's deferred-card tickets are separate protocols and
  must never both charge the same shuffle realization.
- **Mechanical fidelity:** use the actual effects, hook order, pending
  continuations and fair player observation for both modes. Native RNG
  cursor agreement, mechanically legal controlled outcomes and
  calibrated conditional chance laws are **separate** acceptance gates.

PR #153 is stacked on the older Inspector checkpoint review line (#140),
while issue #143 advances in the dedicated integration head (#137).
Review the changes together when integrating: preserve all current
`controlled_*`/semantic-deck capabilities and existing versioned
protocols; do not silently import old chance semantics from the
source-probe branch. This experiment never changes game RNG results,
Nature handles, controlled continuations or the four-Act gameplay branch.
