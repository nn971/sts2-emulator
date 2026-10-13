# AI consumer interface

The canonical emulator API remains `IDeterministicEngine` over `RunState` and `GameAction`.
Search and learning code should normally use `PrototypeAiEnvironment` instead of depending on
CLI formatting or raw JSON layout.

## Goals

The adapter schema is explicitly versioned as `prototype-ai-v0`.

The adapter provides:

- deterministic reset from a seed;
- explicit forked states for branch search;
- player-facing observations;
- stable semantic action IDs;
- stepping by stable action ID;
- sibling expansion for all legal actions;
- observation and canonical-state hashes;
- ruleset/character metadata in every observation.

It does **not** change emulator truth. `RunState` remains the complete continuation state.

## Observation policy

`PrototypeAiObservation` intentionally exposes information a strategic player can use:

- phase, act, floor, HP/max HP, gold;
- Act 1 region and the preselected Act 1 boss encounter, because both are player-visible strategic information;
- persistent deck, relics, and occupied potion slots;
- the complete generated map graph and current node;
- visible combat hand/discard/exhaust contents;
- draw-pile **count**, not hidden draw order;
- energy, block, enemy HP/block, statuses, powers, and current prototype move/intent;
- rewards, shops, active event, and terminal outcome.

RNG state and the run seed are omitted from the observation. Search experiments that deliberately
use perfect emulator state may retain `RunState` separately.

## Stable action IDs

Every legal `GameAction` is mapped to

```text
<kind>:<sha256(canonical payload)>
```

The ID is independent of legal-action list position and CLI presentation. It is stable for the
same semantic action under the current canonical JSON protocol.

Consumers should treat IDs as opaque strings and negotiate schema/version changes rather than
parsing them.

## Typical search loop

```csharp
var env = new PrototypeAiEnvironment();
var root = env.Reset(seed);
var frame = env.Observe(root);

foreach (var child in env.Expand(root))
{
    // child.Action has the stable semantic ID.
    // child.State is an independent canonical successor.
    // evaluate or recurse
}
```

The adapter is currently prototype-specific. The eventual public binding should preserve the same
ideas—reset, observe, legal actions, step, fork/batch—behind a versioned cross-language schema.


## Capability manifest

`PrototypeCapabilities.Create()` returns a machine-readable manifest containing the active
ruleset ID, AI schema ID, character ID, supported content IDs, effect/event kinds, and run phases.

The developer CLI prints the same manifest with:

```text
dotnet run --project src/Sts2Emulator.Cli -- prototype-manifest
```

Experiment metadata in the parent AI project should record at least the emulator commit,
`RulesetId`, and `AiSchemaId`.

## Native-shaped Overgrowth training reset (live-run JSONL)

The long-lived CLI `prototype-ai-jsonl` server advertises
`nativeOvergrowthResetId: "prototype-native-overgrowth-reset-v1"`
in `hello`. The matching request uses `op: "reset_native_overgrowth"`,
a required `seed` string, and an optional integer `ascension` (default 0).
It calls `PrototypeNativeOvergrowthRunFactory.Create(seed, ascension)`
without reconstructing the state from ordinary `reset`.

The successful response contains `ok: true`, the original `requestId`,
`stateHandle`, and `schemaId: "prototype-native-overgrowth-reset-v1"`.
The resulting handle lives in the ordinary live handle table and supports
`observe`, `legal_actions`, `step`, `is_terminal`, `fork`,
`batch_step`, and `release_many`; it is **not** marked hypothetical.
The usual public-only observation policy and the live-handle guard
against hypothetical draw-order injection remain intact.

This is an opt-in **hybrid** training environment: source-shaped native
Act 1 Overgrowth through floor 16, followed by the existing prototype
six-room Acts 2 and 3. It deliberately does not alter the legacy six-floor
`reset` or promise native RNG/timing fidelity. The first exposed state is
the Neow event, with 13 Silent starting cards including Restlessness.
See `tools/native_overgrowth_reset_smoke.py` and
`tests/Sts2Emulator.Core.Tests/PrototypeNativeOvergrowthStarterTests.cs`
for black-box and direct public-observation regression coverage.

The AI consumer in `nn971/sts2-ai` independently verifies the advertised
capability, response schema, map profile, Neow event and deck before
initiating training. Updating the emulator repository alone does not
update the separate `sts2-ai` git-submodule pin.
