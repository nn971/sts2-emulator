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

Existing `reset`, `reset_native_overgrowth` and hypothetical adapters preserve
their legacy identities. `reset_v111` is opt-in and accepts `seed`, `character`
(default `silent`), `ascension` (0–10), and `first_act` (`Overgrowth` or
`Underdocks`). The route continues through Hive/Glory in configuration. Five
character configurations can be created/serialized, but starting unsupported
character mechanics fails. Native later-act and restricted-profile mechanics
also fail explicitly.

Configuration belongs to the state. Forks deep-copy profile/route arrays.
Build/schema/ruleset/character/act mismatches fail on Step and restore. Exact
snapshots encode enums as canonical numeric values; reset uses the names above.

## Operations

| Operation | Request fields | Result |
| --- | --- | --- |
| `v111_coverage` | none | `coverage` with native IDs, dimensions and gaps |
| `reset_v111` | `seed`, optional configuration | `stateHandle`, `exactHash`, schema/fidelity |
| `legal_actions` | `state_handle` | semantic `actions` and stable `actionId` |
| `observe` | `state_handle`, `policy_id: prototype-fair-v0` | fair `payloadJson` |
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
not authenticity. This version rejects unknown properties. Use `observe` for
player-visible information.

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
