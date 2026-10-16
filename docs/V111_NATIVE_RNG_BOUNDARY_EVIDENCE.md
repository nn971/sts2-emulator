# Native RNG Source-Call Evidence — Interface and Acceptance Gate

**Implementation branch:** `integration/fidelity-rng-on-silent-all-act-v111`, based on the newer all-Act snapshot `b2c7ae9`, leaving its source branch untouched.

## Available now

`native/Sts2ReferenceMod/Trace/NativeRngBoundaryEvidenceV111.cs` declares the
typed **single bounded-integer primitive-call** evidence contract:

- Native v0.111 source call site (or explicit `UNATTRIBUTED`), named/derived
  stream, creature/owner, monotonically assigned call ordinal;
- exact `bounded_count`, source-returned `selected_index`;
- native RNG `Counter` and four state words **before** and **after** the primitive
  operation, checked for exactly one counter step;
- strict provenance `source_primitive_hook` and source version.

The existing `NativeActionEnvelopeRecorder.NativeRngBoundary(...)` can append
the event type `native_rng_boundary` to the same ordered JSONL stream as
executor and history callbacks. The new event is validated, then recorded
without making an RNG call or altering the combat state. Its action ID is the
current active executor action when one is available.

`tools/native_action_envelope_audit.py` accepts these events, verifies
basic source/operation/counter invariants and exposes
`native_rng_source_calls` and `native_rng_source_coverage`.
`tools/native_rng_boundary_compare.py` compares genuine source callback
events from **one explicit native action ID** against a real, independently
executed emulator single-fixture report, including the actual typed
`emulator_random_boundaries` journal. Site, stream, owner, ordinal,
primitive bound, selected index and version are required to match in
order. A native `NextInt(n)` matched against an emulator
`choose_index` request proves **only a matching primitive bound/index**;
the original ordered candidate set remains `ELIGIBILITY_UNVERIFIED`
until source-level semantic eligibility is separately captured.
Optional `--exact-native-cursors` checks all native cursor
words when both streams should share the exact same seeded state.

Example (once native source hooks and independent pre-state import have been
validated against the installed game):

```fish
set trace /mnt/d/SteamLibrary/sts2-inspector-traces
python tools/native_rng_boundary_compare.py \\
    "$trace/passive-EXAMPLE.actions.jsonl" \\
    "$trace/one-native-fixture-engine-step.json" \\
    --native-action-id 4 \\
    --out "$trace/one-action-rng-evidence.json"
```

The above filenames are illustrative. Use actual on-disk capture names and
the **single-fixture** report from the `native-engine-fidelity` CLI.

## Precisely unimplemented

**Native source-hook installation code for the two integer NextInt overloads is now implemented, but has not been built or exercised against the installed game.** The installed
game DLL is absent from remote synthetic CI; no pinned v0.111 native
`Rng.NextInt` Harmony or source hook has been built, attached or compared
against a real game run on this branch. Existing Inspector captures therefore
contain **zero** `native_rng_boundary` records, and the comparison tool
correctly reports `NATIVE_RNG_SOURCE_UNOBSERVED`, never success.

A bounded index describes a primitive RNG operation. It does not by itself
certify *semantic* candidate eligibility, original conditional likelihood,
other RNG primitives (e.g., float, weighted item, shuffle and rejection), or
complete source hook order. Cursor deltas between frame samples do not qualify
as source-call records. Action attribution can also be absent for random
choices during combat construction; in that case an independent verified
boundary correspondence is needed before pairing an emulator step.

## Next native milestone

1. Inspect the **installed pinned sts2.dll** to verify its exact source
   RNG method entry points, state snapshot API and run/creature stream
   owner mapping. Bind only against the audited binary fingerprint.
2. Install an opt-in **passive** RNG primitive hook. Capture the pre-state
   in its prefix and original result/post-state in postfix **without any
   extra draw**, event notification or mutation. Verify re-entrancy,
   callback lifecycle and behavior with/without Inspector.
3. Cover initial HP, enemy intent, shuffle, random targeting, summons,
   statuses, relics/potions and map/reward streams. Extend the evidence
   contract with specific verified variants for doubles/rejection and
   weighted support, keeping uninstrumented sites explicitly unsupported.
4. Run real-game trace against actual emulator `Step` and controlled
   scripted replay from independently imported state. Compare first source
   hook divergence and RNG request divergence separately. Never inject
   native post-state into the emulator to force agreement.

The current focused CI exercises synthetic source evidence, rejection of
frame-inferred events, exact comparator classifications and both providers.
It cannot substitute for a native-game-local build or full combat parity.

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

## 2026-10-13: adjacent source-call stream-continuity audit

The offline `native_rng_boundary_verify.py` additionally correlates
**successive individually valid calls on each known `run.*` or `player.*`
native RNG stream inside one combat sidecar**. It compares the previous
post-call counter and all four xoshiro state words against the next
pre-call snapshot, across different action and creature owner IDs.

- Exact match: `stream_pairs_contiguous` increments; this is direct evidence
  for this *observed pair*, not proof of all RNG primitive coverage.
- Forward counter gap: `UNOBSERVED_STREAM_ADVANCE` with the number of
  advances and exact source line numbers. Other RNG primitives, lost records
  and unattached native hooks are distinct possible causes. Report
  `OBSERVED_NATIVE_RNG_PRIMITIVES_WITH_STREAM_GAPS`, exit 2.
- Overlapping/rewound counter or different state at the same counter:
  `NATIVE_RNG_STREAM_ORDER_CONFLICT`, exit 1. Potential nested overload
  double-observation, stream reassignment or reset requires inspection.
- Context/unknown `UNATTRIBUTED` streams are explicitly excluded from the
  continuity check; their individual source primitive transitions remain
  verifiable, but no stable cross-call identity has been established.
- Calls failing individual source/xoshiro verification break the adjacency
  chain for that stream rather than creating fictitious continuity evidence.

The corpus auditor aggregates `native_rng_stream_gaps`,
`native_rng_stream_conflicts`, checked pairs and contiguous pairs.
The unified fidelity gate fails on stream order conflicts and returns an
explicit partial-coverage classification for forward gaps, even when a
supported isolated emulator checkpoint happens to agree. Both classifications
preserve `native_source_hook_order_parity=NOT_TESTED` and
`exact_native_rng_parity=NOT_TESTED`.

This is an **offline diagnostic**. The native hook, RNG consumption,
Silent four-Act engine and active gameplay branches are unchanged.

The standalone `native_rng_boundary_compare.py` now runs the same
source-primitive and full-sidecar persistent-stream continuity verifier before
matching an isolated action to an actual engine provider journal. It returns
`NATIVE_RNG_PRIMITIVE_MISMATCH` or `NATIVE_RNG_STREAM_ORDER_CONFLICT`
for invalid native evidence, and a **partial** verdict
`OBSERVED_RNG_PRIMITIVE_WITH_UNOBSERVED_STREAM_ADVANCES` when supported
bounds/results match but another observed call exposes uninstrumented stream
advances. This prevents a stand-alone local comparator from claiming agreement
on tampered source cursors or overlapped source callbacks.
