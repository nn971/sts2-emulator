# Configured v111 consumer contract

Build in Release, then launch
`dotnet src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll prototype-ai-jsonl`.
Each UTF-8 request and response is one JSON line. Requests use snake_case;
wire responses use camelCase. Exact snapshots and CLI reports use canonical
snake_case JSON.

## Compatibility and fidelity

`hello` preserves `wireSchemaId: prototype-ai-jsonl-v0` and the public intent
capability. Additive fields negotiate `snapshotSchemaId: sts2-exact-snapshot-v1`,
`batchSchemaId: sts2-deterministic-batch-v1`, `coverageSchemaId:
sts2-v111-coverage-v1`, `v111StateSchemaId: sts2-run-v111-v1` and
`v111FidelityId: v111-mechanics-prototype-rng-v1`. Check the capabilities your
consumer requires. The build pin identifies the mechanics target; fidelity
explicitly identifies prototype RNG.

`hello` also advertises `referenceNatureStateSnapshotSchemaId` for snapshots
with a committed sampler cursor and `natureRelicBagSnapshotSchemaId` for the
opt-in v3 snapshot carrying an unordered Nature relic identity set. The v3
schema takes precedence when both capabilities apply; ordinary v1 and cursor
only v2 snapshots retain their established envelopes. Configured Nature-seeded
run starts add an optional initialization transcript within the existing v2
state payload; older snapshots without it remain readable.

Existing `reset`, `reset_native_overgrowth` and hypothetical adapters preserve
their legacy identities. `reset_v111` is opt-in and accepts `seed`, `character`
(default `silent`), `ascension` (0–10), and `first_act` (`Overgrowth` or
`Underdocks`). Optional `unlocked_colorless_epochs` supplies an exact Colorless
epoch override while leaving the rest of the profile fully unlocked. An empty
list means no Colorless epochs are unlocked; omitting the field admits all
five. It also accepts optional unsigned `nature_seed` to attach a forkable
ReferenceNature cursor before `start_run`; the Python `Session.reset(...)`
method exposes the same parameters. For either Act 1 route,
run initialization uses it for the ordered boss-pool choice. Overgrowth also
uses the same cursor for each source-shaped Act 1 map-generation request before
the Neow offer is entered; Neow offers retain their existing Native event
context. Experimental Silent three-act runs then use it for the conditional
Hive/Glory Darv partition and local Ancient choices when `start_run` prepares
those acts, followed by Hive/Glory encounter-bag choices and Glory's Ascension
10 second boss. Five character configurations can be created/serialized, but
starting unsupported character mechanics fails. Other restricted-profile
mechanics and Native later-act mechanics also fail explicitly.

Configured runs with `nature_seed` retain an optional
`reference_nature_run_initialization_transcript` in exact run snapshots. It
records the initial and final sampler cursors plus the ordered Chance Laws and
sampled decisions from synchronous initialization. Overgrowth also retains the
typed map request prefix and a fingerprint of its generated map; snapshot
validation replays both the sampler prefix and map generator. This is an audit
record, not a resumable `start_run` session.

For the **experimental** first whole-route slice, `reset_v111` additionally
accepts `fidelity_id: "v111-experimental-silent-three-act-route-v1"`.
The thin Python binding exposes the same `fidelity_id` keyword. This mode permits
Silent Act 1 → Hive → Glory through the fully catalogued
Hive/Glory encounter rosters and available regional events with provisional
selection weights, runtime gaps and non-native combat RNG; see
[its scoped evidence note](reference-builds/v0.111.0-experimental-silent-three-act-route.md).
Omitting `fidelity_id` preserves the existing configured v111 default.
Unknown fidelity IDs are rejected. Multi-character and nonexperimental
later-act execution remain gated.

Configuration belongs to the state. Forks deep-copy profile/route arrays.
Build/schema/ruleset/character/act mismatches fail on Step and restore. Exact
snapshots encode enums as canonical numeric values; reset uses the names above.

## Operations

| Operation | Request fields | Result |
| --- | --- | --- |
| `v111_coverage` | none | `coverage` with native IDs, dimensions and gaps |
| `reset_v111` | `seed`, `character`, `ascension`, `first_act`, optional `unlocked_colorless_epochs`, `fidelity_id`, and `nature_seed` | `stateHandle`, `exactHash`, schema/fidelity, optional ReferenceNature state schema |
| `legal_actions` | `state_handle` | semantic `actions` and stable `actionId` |
| `observe` | `state_handle`, `policy_id: prototype-fair-v0` | fair `payloadJson` |
| `fair_observe` | `view_kind`, `view_handle` | versioned common fair observation, action menu, observation hash and terminal flag |
| `step` | `state_handle`, `action_id` | independent `child`, preserved `parent` |
| `fork` | `state_handle` | independent `child` with equal hash |
| `save_snapshot` | `state_handle` | exact hidden-state string `snapshot` |
| `load_snapshot` | `snapshot` | restored `stateHandle`, verified `exactHash` |
| `step_batch` | `steps: [{state_handle, action_id}]`, optional positive `parallelism` | ordered `children` |
| `release_many` | `state_handles` | number `released` |
| `close` | none | closes process |

Batches resolve every action before execution and publish children only after
all transitions succeed. Failure leaves parents and the handle table unchanged.
Parallel scheduling preserves ordering and hashes. This operation is additive;
existing `batch_step` and frame APIs keep their original contract.

Snapshots contain hidden RNG state, draw order and pending continuations.
They are trusted emulator state exchange. Restore checks schema, build/state
identity, canonical hash and engine invariants; hashes establish consistency,
not authenticity. This version rejects unknown properties. Use `observe` or
the additive `fair_observe` frame for player-visible information.

`fair_observe` negotiates `sts2-v111-fair-observation-envelope-v1` and accepts
`run_state`, `controlled_combat`, `semantic_combat`, or `semantic_reference`
handles. Its payload uses the unchanged `prototype-ai-v0` observation schema;
the envelope binds the view kind, handle, policy, observation hash, legal
action menu, and terminal flag. All four views are produced by the same
`PrototypeAiEnvironment`. Clients should accept additional advertised view
kinds while continuing to reject unknown envelope schema IDs.

## Replay

`replay <trace.jsonl>` checks normalized canonical schema 0.1 transitions,
chain continuity, hashes, legal actions, state and RNG. v111 transitions must
record `legal_actions_before` and `legal_actions_after`. The first mismatch
returns exit 1 with decision index, dimension and JSON path. Invalid
headers/builds/hashes/indexes fail explicitly.

Passive native schema 0.2 captures require a normalization adapter; they cannot
be substituted directly for engine state. Native build validation is available
through `V111ReferenceTraceIdentity`. Engine-generated replay is a determinism
test and does not establish native agreement.

The [Python binding](../bindings/python/README.md) owns transport and handles;
all game behavior remains in C#.
