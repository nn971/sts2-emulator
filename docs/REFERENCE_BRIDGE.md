# Native reference bridge

The bridge is tied to an **installed build fingerprint**, not to a version string copied into source.
Before inspecting or building against the game, run:

```fish
dotnet run --project src/Sts2Emulator.Cli -- reference-preflight "/path/to/Slay the Spire 2"
```

If the install contains more than one `data_sts2_*` directory, pass the intended data directory
as the second argument.

The preflight manifest records:

- the full `release_info.json` value;
- the target framework from `sts2.runtimeconfig.json`;
- SHA-256 and size for `sts2.dll`, `0Harmony.dll`, and `GodotSharp.dll`;
- a stable build fingerprint that excludes local paths and capture time.

This follows the same maintenance principle as current STS2 modding guidance: compile against the
assemblies shipped by the exact installed build and re-check release/runtime metadata after game
updates. The repository does **not** vendor those game assemblies.

## Trace protocol

New native capture work should emit `schemas/reference-trace-v0.2.schema.json` using the
`Reference*V02` models in `Sts2Emulator.Trace`.

The v0.2 trace is intentionally native-neutral:

- semantic state is JSON rather than `RunState`;
- legal actions are normalized semantic action objects;
- RNG streams carry semantic IDs, codec IDs, fingerprints, and optional full codec state;
- the trace header embeds the build identity and stable fingerprint.

The older bootstrap trace model remains available for existing tests/tools, but native bridge work
should not force live-game state into the prototype emulator model.

The first implementation sprint should build a small instrumentation mod whose job is to turn the live game into a semantic oracle.

## Responsibilities

The bridge should be deliberately narrow:

1. identify stable **decision boundaries**;
2. enumerate/normalize legal actions at that boundary;
3. serialize canonical continuation-relevant state;
4. serialize/fingerprint all relevant RNG stream state;
5. observe the selected action;
6. capture the canonical state after the action has settled at the next decision boundary;
7. emit a versioned trace.

It should not contain AI strategy.

## Capture modes

### Passive recording

Record a human-played run. This is the safest first mode because it changes no decisions.

### Controlled random policy

Once the bridge can identify legal actions reliably, allow an external harness to choose among them to generate diverse parity traces.

### Scenario restoration / branch testing

Useful later, but only after we can prove that restoring a captured state faithfully recreates native continuation. We should not assume arbitrary save-state restoration is safe.

## Decision boundary rule

Capture state only when the game is semantically ready for a player decision, or at an explicitly defined automatic transition checkpoint.

Avoid tying trace semantics to frame timing, animations, Godot nodes, or UI widget lifetimes.

Potential top-level boundary classes:

- map node selection;
- card/potion/relic reward selection;
- shop action/leave;
- rest-site option;
- event option;
- combat action/target/card-selection prompt;
- end-turn where it is a player action;
- boss relic / act transition choices;
- terminal run state.

The actual list must be audited against the game build.

## Canonical state adapter

Do not dump arbitrary object graphs wholesale. Create a bridge-side adapter that extracts semantic fields into our versioned state schema.

Benefits:

- avoids UI/process-global noise;
- makes diffs intelligible;
- avoids object-reference/address instability;
- lets the emulator implement the same schema independently;
- keeps trace compatibility manageable when internal classes change.

## RNG capture

Treat RNG capture as a first-class deliverable. For each stream determine:

- stable semantic name;
- initialization/ownership;
- internal full state representation;
- lazy materialization behavior;
- whether serialization itself can advance it (it must not);
- canonical fingerprint/codec version.

The bridge should never call a random method merely to inspect RNG.

## Action normalization

The native UI may expose actions through many different classes/widgets. Normalize them to semantic commands such as:

```json
{"kind":"choose_map_node","payload":{"node_id":"..."}}
{"kind":"play_card","payload":{"card_instance_id":17,"target_id":3}}
{"kind":"buy_shop_item","payload":{"slot":2}}
{"kind":"choose_reward_card","payload":{"index":1}}
{"kind":"skip_reward","payload":{}}
```

Prefer stable semantic identifiers over screen coordinates.

## Output

Start with JSONL because it is inspectable and robust during schema churn. Optimize storage only after traces are stable and large enough for I/O to matter.

## First bridge success criterion

Capture one short real run segment containing at least:

1. a map choice;
2. one combat decision sequence;
3. combat completion;
4. a reward choice;
5. another map choice;

and produce enough canonical/RNG information that repeating the same real-game decisions under the same initial state yields identical trace hashes.
