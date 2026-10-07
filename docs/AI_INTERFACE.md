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
