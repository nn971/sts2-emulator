# Unified v111 RNG + Fidelity Inspector: integration status

**2026-10-13.** Branch: `integration/fidelity-rng-unified-v111`. This is an isolated **review-only** integration branch; do not merge into `main` or into evolving Silent-all-Act development. Frozen untouched source baseline is `agent/v111-hive-glory-pregenerated-encounter-queues-v20@4ea715cbd3b42a071f7e63c845311dd5115bf93b`. The development repository working tree at `~/projects/sts2-emulator` and shared local Git metadata were not accessed. Never add directories under `~/projects`.

## Consolidated provenance (real commit ancestry)

- Desired design **#119** and provider/continuation stack **#123→#124→#126** (native index provider, scripted/controlled providers; serializable HP→intent boundaries; isolated JSONL/Python Nature interface).
- Alternate initial-HP **#125** is an **ours-resolved historical merge**: canonical implementation is #124/#126. We retained its historical document and ported its independently useful duplicate-creature-`CombatId` rejection as a negative test; alternative incompatible API was not copied over.
- Native Inspector **#121→#130→#131** (strict passive decisions, action-envelope sidecar, nested-choice resumption, revised audits and closed-prefix extraction). Older scaffold-vs-v111 CI/CLI/test-project collisions were reconciled by preserving the full working v111 implementation, retaining Inspector tools and native mod code, and combining their interfaces.
- Engine fidelity line **#127→#128→#129→#132**, merged at the **last clean fidelity-only source tip `5fa3208eff590bb5ab5622ea9e849db7b39d9c2e`**. The remote #132 branch later gained unrelated Silent-all-Act commits. **Do not use its moving head as an integration merge parent**; that would falsely mark unrelated game features merged while excluding their content. We selectively included the latest fidelity-only checker code, actual Step importer, source-site emulator journal, and fixture batch CLI. This policy protects later legitimate merges from the active all-Act development line.
- The final merge tree contains only inspected fidelity/Inspector/provider changes plus the frozen base—not subsequently developed Silent-all-Act content.

## First new implementation after integration

1. `RandomBoundaryJournalV111` now passively records typed chance requests **at the actual provider resolution call**, with site/stream/owner/version/occurrence/candidate order, selected index/value, mode and optional native cursor snapshots. The journal is diagnostic-only and opt-in via scoped `AsyncLocal`; the ordinary native/legacy gameplay API is unchanged.
2. All three initial `IRandomIndexProviderV111` implementations (native, scripted and controlled) emit observations only after successful validation/resolution. The journal never requests a new draw or changes a stream; invalid selections emit nothing.
3. `RandomBoundaryComparisonV111` checks exact support order, owner, stream, call site/sequence, source version and selected outcome; optionally requires identical native pre/post cursors. Native vs counterfactual scripted outcomes may match despite different provider mode and cursor.
4. The **actual**, unchanged `PrototypeGameEngine.Step` checkpoint importer now captures both its existing ordered mechanics journal and the nested typed RNG provider journal, returning `emulator_random_boundaries` and `random_boundary_coverage=MIGRATED_PROVIDER_SITES_ONLY`. Batch summaries include per-fixture provider boundary counts.
5. The emulator RNG journal now also emits `emulator_random_transcript`, one global sequence across index, float/double, and Gaussian provider operations. The existing typed arrays remain available. This restores mixed-operation order for migrated provider calls; it does not show native calls or unported emulator sites.
6. Negative/regression tests cover provider parity vs scripted, candidate order and request count mismatch, invalid outcomes, nested capture scopes, untouched controlled RNG, and duplicate creature owner IDs inherited from #125. A journal with zero migrated calls is **not** evidence that the native game made no random calls.

## Known limits, precisely classified

- Native v0.111 game-local mod **has not been rebuilt or tested** against the user's installed original DLL after branch consolidation; native code is excluded from no-game-assets CI.
- The current Inspector sidecar captures executor callbacks, history and frame changes, **not** the exact native RNG call-site/ownership/result/cursor event sequence. Captured CombatHistory publication order is not necessarily hook invocation order.
- Only limited v111 RNG sites (initial/summon HP, Tough Egg hatch HP, committed enemy intents, and integrated encounter-formation index draws) use the shared provider. Controlled HP→intent execution is an **isolated combat-entry continuation**, not arbitrary player-action `Step`, mid-card hooks, shuffles, rewards or a complete Nature-driven run.
- Existing authentic eight-play-card corpus: 8 native expectation fixtures; only 1 isolated actual engine step, all 49 compared fields match, but its visible Weak effect remained unchecked; the other 7 are blocked by unimported enemy powers, with 3 also Pinpoint cost/skill-history blockers and one unexplained double Defend execution. **Full native combat parity is NOT_TESTED.**
- Controlled counterfactual trajectories need not match native cursors; **exact native seeded replay still requires source-specific RNG ownership and cursor parity** in dedicated tests.
- Exact probability kernels conditional on player information are not calibrated. Don't label unverified chance weights as the native reference distribution or use importance ratios as exact.

## Next acceptance-gated patches

1. **Native read-only RNG source probes** at v111 `Rng.NextInt` / shuffle / contextual owners. Record actual source call-site, generator instance owner, support/weight identity, selected outcome, source causal parent, and native cursor before/after. Native instrumentation must be measured for no perturbation.
2. Match each native source call to the typed emulator provider-boundary transcript; classify request mismatch, eligible support mismatch, outcome mismatch, uninstrumented site, missing owner, exact-seed cursor divergence **separately**.
3. Extend strict pre-state importer and exact mechanics step to combat powers and their order/expiry, cards with variable displayed costs and verified turn counters, nested player-choice selection, automatic replay cause. Never inject native post-state into engine execution.
4. Migrate audited chance sites in small native-zero-regression batches: encounter/local formation/spawns, draw/shuffle, targeting/card generation, relic/potion hooks, rewards, maps/events. Preserve unsupported-site strict failures.
5. Test complete combat and whole-run trajectories with both providers, source hook-order parity and chance transcript parity, then calibrate conditional probability laws and measure throughput.

The [integration plan](UNIFIED_RNG_FIDELITY_INTEGRATION_PLAN.md) is the source of acceptance gates. Experimental Nature network training and multiplayer content remain out of scope.

## Integrated checker command (evidence-only unless engine executes)

`tools/unified_native_rng_fidelity_gate.py` now orchestrates the native
action-envelope corpus audit, strict-trace triage, independently closed
decision-prefix fixture extraction, and optionally the **actual** .NET engine
fixture comparer. It never repairs raw traces, presumes hidden engine state,
or infers RNG/hook parity.

In an **isolated checkout outside `~/projects`** containing this integration
branch and a previously built CLI DLL:

```fish
set trace /mnt/d/SteamLibrary/sts2-inspector-traces
python tools/unified_native_rng_fidelity_gate.py "$trace" \\
    --fixtures-dir "$trace/fixtures" \\
    --engine-dll src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll \\
    --region Overgrowth --ascension 0 \\
    --out "$trace/unified-native-fidelity-report.json"
```

Without `--engine-dll`, it produces a native-evidence report but **cannot**
return an engine agreement. It refuses stale/foreign fixture files not
generated from the current closed-prefix manifest. An actual engine mismatch
takes precedence over incomplete capture, while incomplete/unsupported
mechanics cannot be labelled a successful complete fidelity check.

The native-side RNG call-site probe and source-hook comparator remain **not
implemented**. The new shared provider journal reports only already migrated
emulator RNG sites. Neither an empty journal nor a consistent supported
checkpoint certifies native-game or original conditional-probability parity.

## 2026-10-13: promoted onto current Silent all-Act first-parent branch

The active integration is now `integration/fidelity-rng-on-silent-all-act-v111`
([draft PR #137](https://github.com/nn971/sts2-emulator/pull/137)).
Its plan was committed in
[`UNIFIED_RNG_FIDELITY_ALL_ACT_REBASE_PLAN.md`](UNIFIED_RNG_FIDELITY_ALL_ACT_REBASE_PLAN.md)
**before** the merge. The first-parent snapshot was `b2c7ae9`; a
conflict-free second-parent sync subsequently incorporated the newer
all-Act Future of Potions commit `db751d8` (12 original all-Act
files, no overlaps). The user's active all-Act source branch, `main`,
and the local `~/projects/sts2-emulator` checkout remain unchanged.

The newer full game/CLI and combat mechanics were retained. The integration
ported typed native/scripted/controlled providers and HP→intent serializable
Nature continuations onto the newer combat selector, exposed isolated
Nature JSONL operations and their Python `NatureBoundary` bindings,
and kept the actual-engine Inspector checkpoint importer.

A **separate** `.github/workflows/fidelity-integration.yml` runs
targeted .NET RNG/Inspector/checkpoint tests, Python capture tests,
unified-gate rejection tests, and the isolated Nature HP/intent bridge.
This is necessary because the pinned active all-Act source snapshot's
**original global CI was already red (296/1,629 failing .NET tests)**.
The initial integrated global CI showed the **same 296 failing test
identities**, with no new failures and more passing tests. Those unrelated
baseline failures belong to the ongoing all-Act development.

New passive source-RNG evidence interface:

- `NativeRngBoundaryEvidenceV111` and
  `NativeActionEnvelopeRecorder.NativeRngBoundary`: validated
  single primitive `NextInt` events with source hook provenance,
  owner/stream/site, bound/index and exact pre/post cursor words.
- `tools/native_rng_boundary_compare.py`: compares **one explicit
  native action identity** to an actual-engine single-fixture provider
  journal, stops at first source/order/candidate/cursor mismatch.
- The audit and unified gate report native source RNG call counts,
  missing ownership and unpaired boundaries. Raw `NextInt(n)`
  agreement with emulator `choose_index` is correctly labelled
  **primitive bound/index only**, with semantic candidate ordering
  still unverified.
- Synthetic .NET/Python unit tests exercise rejection of invalid and
  frame-inferred RNG events.

**Native RNG source hooks are NOT yet installed** in the game DLL.
The contracts are implemented and unit-tested, but real native RNG call
observations still require v0.111 native assembly inspection and
read-only game-local patch validation. Existing captures continue to
report `NATIVE_RNG_SOURCE_UNOBSERVED`; no game-wide native RNG,
source hook order, full mechanics, or original conditional-law parity
is certified.

Implementation and validation notes:
[`V111_NATIVE_RNG_BOUNDARY_EVIDENCE.md`](V111_NATIVE_RNG_BOUNDARY_EVIDENCE.md).

## 2026-10-13 next implementation: opt-in native primitive hook source code

Implemented `native/Sts2ReferenceMod/NativeV111RngHook.cs`, enabled only with
`STS2_FIDELITY_CAPTURE=1` **and** `STS2_FIDELITY_RNG_HOOK=1`, after the
existing runtime SHA-256 game-pin check. Harmony targets the exact pinned
`Rng.NextInt(int)` and `Rng.NextInt(int,int)` method signatures; a
pure `ToSerializable()` prefix snapshots four xoshiro state words/counter,
and the postfix copies the original returned value and new snapshot.

`NativeRngSourceRegistryV111` identifies the 12 run + 3 player streams
by **native object reference**, bound from the one combat player's actual
`RunState.Rng` and `PlayerRng`. Context RNGs and source mechanic sites
are explicitly `UNATTRIBUTED`. The hook is restricted to observed
single-player combats after the first frame; early combat-entry, different
threads, and non-`NextInt` operations remain unverified.

Sidecar headers record `native_rng_hook_installed`, while the corpus
auditor distinguishes `SOURCE_RNG_UNINSTRUMENTED`,
`HOOK_INSTALLED_NO_SOURCE_CALLS_OBSERVED`, and source calls actually
captured. Failure to install a hook leaves ordinary passive Inspector
capture functioning. Synthetic tests cover owner reference identity,
call ordinals, source snapshot validation and audit classification.

**Evidence gate: native game-local build and execution are still untested.**
The raw game binary is absent from CI. Source-level hookup is complete
for these overloads, but claiming an installed/running hook, no-perturbation,
full call-site coverage, semantic candidate eligibility or native RNG parity
requires real-game traces. Use
[`V111_NATIVE_RNG_HOOK_LOCAL_VALIDATION.md`](V111_NATIVE_RNG_HOOK_LOCAL_VALIDATION.md)
for the isolated WSL checkout and Windows process setup.

### Native monster-intent source scope (implementation added, runtime validation pending)

`NativeV111RngHook` additionally attempts an independent, **optional**
Harmony prefix/postfix/finalizer on source-audited
`MonsterModel.RollMove(IEnumerable<Creature>)`. In the pinned source
this method synchronously delegates to the state machine with
`RunRng.MonsterAi`. A nested native `NextInt` observed in this scope
is assigned `site_id=combat.committed_intent.index_draw`, the native
`creature:<CombatId>`, and a **zero-based per-RollMove** call ordinal.
Other primitives preserve `UNATTRIBUTED` source-site provenance.

This scope has a separate installed flag
`native_monster_ai_scope_installed` in the combat sidecar header,
and a dedicated `NativeRngCallsiteScopesV111Tests` regression suite
checks stream filtering, nested source scopes and LIFO restoration.
Failures to install the optional game scope leave the primitive
`NextInt` observer operational and keep intent attribution unsupported.
**No installed native game build or paired real-game/emulator intent
fidelity test has yet certified this attribution.**

Local procedure and exact capture fields:
[`V111_NATIVE_RNG_HOOK_LOCAL_VALIDATION.md`](V111_NATIVE_RNG_HOOK_LOCAL_VALIDATION.md).


### 2026-10-13: actual-engine scripted shuffle replay gate

The integrated real `PrototypeGameEngine.Step` now has a **strictly
opt-in** source-shuffle replay seam at the two ordinary
discard-to-draw recycle paths (`DrawCards`, `AutoPlayTopDrawCards`).
Normal AI training and no-replay engine behavior continue using
`PrototypeRng.Shuffle`; the scoped source provider never silently
substitutes native post-states. The replay compares source-mapped
eligible cards at the **actual engine shuffle boundary**, all recorded
Fisher–Yates swaps, the engine's computed pre-hook order and its
`ApplyPerfectFitShuffleOrder` post-hook order. Mismatches carry an
explicit stage and source/engine IDs.

`NativeShuffleSourceImportV111` accepts only real v0.111 source
`native_shuffle_operation` events with an **explicit one-to-one
native card object token → emulator instance ID mapping**, unique
registered source card IDs, source-stated prior state eligibility,
and independently recomputed xoshiro256** transitions/results.
It rejects duplicate identities, missing source cards, missing
operations, incorrect source bounds, invalid models or cursors.

`NativeShuffleStepFidelityV111.Compare` executes a **real**
`PrototypeGameEngine.Step`, preserving generated cards, enemy
mechanics and subsequent card piles; the opt-in diagnostic
`BeginEngineEligibilityProbe` captures synthetic engine-only
candidate lists for tests and is **never native evidence**.
Dedicated end-turn test exercises recycling **after an enemy
generated an extra card**, proving the scope attaches at the
actual shuffle boundary rather than a guessed turn-start pool.
The new CLI `native-shuffle-step-fidelity` consumes
`native-shuffle-step-fixture-v1` input and returns the first
observed shuffle-stage mismatch and compact real engine successor.

Full fixture contract and replay limits:
[`V111_NATIVE_SHUFFLE_ENGINE_STEP_REPLAY.md`](V111_NATIVE_SHUFFLE_ENGINE_STEP_REPLAY.md).
A full combat imported from native game remains pending verified
pre-state reconstruction, stable source→engine card/creature identity
linking, complete native powers/relics/hooks and initial RNG cursors.
`NativeGameParity` and `ExactNativeRngParity` remain
`NOT_TESTED` in every replay report. Old native trace has no
source shuffle-operation records, so the real-game acceptance gate
requires a new source-captured shuffle record when ready.
