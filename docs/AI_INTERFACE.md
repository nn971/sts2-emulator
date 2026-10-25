# AI consumer interface

**Information boundaries:** [Unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md). The ordinary agent view remains player-visible; semantic Nature support is a separate negotiated view, and hidden provider state belongs to privileged diagnostics. The Chance Law and ReferenceNature APIs do not change `prototype-ai-v0`.

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
It also contains `stochasticV111`, a separately versioned capability envelope for the semantic
Nature interface (schema v2). The JSONL `hello` response advertises the same envelope as
`stochasticCapabilities`. It lists the supported Chance Law sites and provider modes. The legacy
`ProbabilityStatus` field remains the conservative `UNVERIFIED` default, while
`ProbabilityStatuses` enumerates statuses available on individual laws
(`UNVERIFIED` and `SOURCE_VERIFIED`); consumers should negotiate these instead
of inferring law support from a generic semantic session. `SOURCE_VERIFIED`
identifies source-audited sampling math under the law's declared input model,
not seeded parity or stream-derivation evidence.

The developer CLI prints the same manifest with:

```text
dotnet run --project src/Sts2Emulator.Cli -- prototype-manifest
```

Experiment metadata in the parent AI project should record at least the emulator commit,
`RulesetId`, and `AiSchemaId`.

Semantic session results include a typed `chanceLaw` inside the Nature-only `pending` payload for
supported enemy-intent, unresolved draw-card, and declared peek/scry reveal events. It carries stable outcome IDs, exact
integer weights, support and conditioning fingerprints, and explicit provenance. The ordinary
`semantic_observe` response remains the fair player projection and does not include this payload.
Semantic combat `pending` payloads also include the ordered provider-neutral
`semanticOutcomeIds`; the Nature-choice view exposes the same identities at
`eventMetadata.semanticOutcomeIds`. `SemanticCombat.resolve_outcome_id()`
checks selections against its saved pending support.
Deck-reveal choices have their own `semanticDeckRevealChoiceSchemaId` and resolver. The semantic
deck kernel and resolver support pending peek/scry state; source gameplay callsites that create
those requests remain outside the current playable mechanics coverage.

Configured Native combat reward generation has a separate atomic
`ReferenceNatureCombatRewardSessionV111`. Start it from a current legal combat
action that immediately wins and enters `Reward`; resolve each pending Chance
Law with `reference_nature_combat_reward_resolve_next`. The session exposes a
committed state only after the complete reward has replayed with its Nature
cursor. Its scope and source limits are recorded in the
[combat reward session contract](REFERENCE_NATURE_COMBAT_REWARD_D3.md).

The shared `nature_choice_observe` operation projects pending Chance Laws for the session kinds
advertised by `semanticNatureChoiceViewSessionKinds`. Its v2 view recursively removes provider
stream, seed, cursor, RNG-state, and call-count metadata while retaining semantic site identity,
support, conditioning, and boundary fingerprints. The Python binding's
`Session.observe_nature_choice(session_kind, session_handle)` negotiates that schema and kind
against `hello`, then checks the response identity. It remains separate from `State.observe()` and
the fair player observation.

The opt-in `semantic_reference_*` JSONL operations wrap the semantic combat state and the
ReferenceNature sampler cursor in one forkable/savable session. Begin with a `semantic_handle` and
an unsigned 64-bit `nature_seed` for a run that has no committed cursor; omit `nature_seed` to
continue the cursor persisted by an earlier committed ReferenceNature session.
`semantic_reference_resolve_next` auto-samples the pending Chance Law, while
`semantic_reference_step` advances a legal player action through the same semantic combat engine.
Status, observation, fork, save/load, and release operations use the separate
`reference_nature_handle`. Its observation operation uses the existing fair policy.
The additive `fair_observe` operation accepts this handle as `semantic_reference`,
so its public observation and legal action menu use the same versioned envelope as
ordinary, controlled-combat, and semantic-combat views. Python clients can begin
this mode with `Session.begin_semantic_reference_combat()` and use
`SemanticReferenceCombat.observe()`, `actions()`, `step()`, `resolve_next()`,
`observe_nature_choice()`, `fork()`, `save_snapshot()`, and `release()`; loading
uses `Session.load_semantic_reference_snapshot()`. Its fair observation and
Nature-only chance view remain separate operations.

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
