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

**Native source-hook installation is NOT implemented yet.** The installed
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
