# Unified stochastic architecture

**Decision:** 2026-10-16. **Status:** accepted target contract; implemented only in part. **Target:** STS2 v0.111.0, single-player first.

This is the **canonical responsibility/ownership contract** for RNG, Nature, Chance Law, and the native bridge. [Desired RNG Implementation](DESIRED_RNG_IMPLEMENTATION.md) specifies the migration details; [staged roadmap](STOCHASTIC_ARCHITECTURE_ROADMAP.md) records implementation and acceptance. The code still contains legacy/prototype and isolated semantic implementations, described in the [migration matrix](DESIRED_RNG_MIGRATION_STATUS.md) and [Nature pilot history](NATURE_P0_SEMANTIC_JSONL_143.md). Those are implementation evidence, not alternate ownership rules.

## Responsibility boundaries

| Owner | Authoritative responsibility | May contain information hidden from player? |
| --- | --- | --- |
| **Game engine / mechanics** | Objects and stable IDs, zones and membership, move histories, eligibility and source weights, player actions, deterministic hooks, timing and consequences of selected outcomes | Yes: hidden **mechanical** history |
| **Native RNG provider** | Source primitive RNG, streams/cursors/seed derivation, committed unrevealed permutations and other stochastic realizations; immediate auto-resolution | Yes: hidden **stochastic** realizations |
| **Nature provider** | Deferred stochastic objects, unresolved permutation/selection constraints, previously committed/revealed constraints, validation and receipt of an externally chosen legal outcome | Yes: hidden **stochastic** constraints |
| **Chance Law** | Read-only conditional reference distribution of legal outcomes for a declared pre-outcome information/constraint state | It may inspect declared constraints, **never** a selected value or Native's sampled realization |
| **ReferenceNature** | Simple, nonlearned Nature policy that samples exactly from the Chance Law distribution | Only the Nature choice information supplied by its contract |
| **Scripted replay** | Substitute previously recorded semantic outcomes, checking operation/site/support/constraints | Recorded outcomes, with explicit provenance |
| **Observation/access layer** | Player-visible projection, Nature-choice projection, privileged inspector/debug projection | Separates audiences |
| **Native reference mod / Fidelity Inspector** | Capture installed-game evidence, audit source call/hook parity, compare native and emulator mechanics | Privileged diagnostics by explicit opt-in |
| **sts2-ai** | Learned Nature proposal distribution `q`, player policy, training/search/evaluation and corpus management | Only through negotiated interfaces |

**No runtime dependency** exists from Native RNG to Chance Law. The native mod does not call Chance Law. Chance Law is a callable analytical service primarily for Nature and tests; source-game captures can later *verify* it offline.

`tools/rng_callsite_inventory.py --check-no-native-chance-law-dependency` is a direct-reference regression guard for this boundary. It scans Native RNG provider/adapter sources and native mod C# sources for Chance Law symbols; it does not prove the absence of indirect dependencies. `--check-chance-law-outcome-blind` separately follows method-name calls from Chance Law queries through static methods on the `ChanceLawEngineV111` partial type, looking for provider sampling, cursor reads, boundary journals, replay captures, and engine steps. It also fails on identical masked method signatures duplicated across the partial files. Calls are still matched by name, so overload resolution, calls into other types, delegates, and reflection remain outside this source check.

Endless Conveyor now follows the same boundary. Its Chance Law builders accept a pre-draw Event state and a declared request ordinal. The ReferenceNature session projects the source action through the already selected same-action prefix, suspends at the unresolved typed request, and passes the captured pre-draw state to the pure query. The projection does not resolve that pending outcome or use its cursor to define the law.

Source ownership follows the same separation: `ChanceLawV111.cs` contains the Chance Law value model and shared query core, with event, offer, reward, relic-acquisition, merchant, Treasure, map-generation, and other source-family queries in owner-named `ChanceLaw*V111.cs` files. Native merchant and Alchemical Coffer queries, plus retained legacy Shop laws, live in `ChanceLawMerchantV111.cs`; Native and legacy Treasure queries live in `ChanceLawTreasureV111.cs`; combat-reward laws, including the hidden relic-identity law, live in `ChanceLawCombatRewardV111.cs`; combat-end hook laws live in `ChanceLawNativeEventShuffleV111.cs`; relic-acquisition and potion-fill laws live in `ChanceLawRelicAcquisitionV111.cs`; semantic combat, draw, and saved-choice laws live in `ChanceLawSemanticCombatV111.cs`; Ancient-offer laws live in `ChanceLawAncientOfferV111.cs`; legacy event-reward laws live in `ChanceLawEventRewardV111.cs`; legacy and Native potion-generation laws live in `ChanceLawPotionGenerationV111.cs`; Native combat-entry laws live in `ChanceLawCombatEntryV111.cs`; Native map, run-start encounter, and three-act assignment laws live in `ChanceLawMapGenerationV111.cs`. ReferenceNature sampling, cursor/session state, per-session SiteId lists, and its run-action, Ancient-offer, and event-choice sessions live in owner-named source files; the controlled run-action query lives in `ChanceLawRunActionV111.cs`, and controlled event-choice queries live in `ChanceLawNativeEventChoiceV111.cs`. Chance Law's existing session-list properties are delegating compatibility projections. These moves preserve public query APIs and snapshot schemas.

`NativeRandomChoiceV111` constructs typed Native index, bounded-integer, and unit-float requests through the provider-specific registration path. The Native index, Gaussian, Native event, and Punch Off site registrations come from source-operation and semantic-choice sets; they do not use direct Chance Law query coverage as a fallback. Native request validation checks the request shape and Native operation registry directly; the provider-neutral validator retains its separate legacy/query registration union. In particular, Native Gaussian-integer providers use `StochasticSiteRegistryV111` for source-shape validation, while Chance Law keeps a delegating query facade. The Chance Law query manifest is initialized only when its query-coverage API is used, so Native registry initialization does not materialize it. Source-operation registrations include standalone Native callbacks such as the Fishing Rod persistent-combat upgrade choice. Chance Law query coverage remains a separate analytical capability, and its Native map-Gaussian, event, and Punch Off query IDs project the shared stochastic registry entries.

## State ownership

The engine owns the **complete simulation lifecycle** (fork, snapshot, restore, replay, hashing) of both mechanical state and the chosen provider's private stochastic state. This does not make the engine the authoritative owner of hidden stochastic representation. In particular:

- The engine owns **which physical card instances are in the draw pile** and every known/public placement or movement consequence.
- The active provider owns **the private ordering, committed permutation, or unresolved order constraints** and updates them on mechanical insert/remove/reveal/shuffle operations.
- `SemanticDeckStateV111` and its pool, pending-event, transcript, event, and order values copy mutable arrays on construction and replacement and return defensive copies from array properties. Callers therefore cannot change Nature's hidden order constraints or candidate support through a shared array reference.
- `SemanticDeckPublicViewV111` owns its revealed-position array on construction and replacement and returns a defensive projection; its positional constructor, deconstruction, and JSON member remain stable.
- Semantic card-selection events, candidates, decisions, and completed selection frames own their candidate, sequential-prefix, and selected-card identity arrays on construction and replacement, and return defensive projections without changing their source-ordering or JSON shape.
- Semantic target, generated-card, potion-generation, cost, card-transform, hand-cost-selection, card-shortlist, pool-shuffle, draw-insertion, weighted-intent, random-pile-card, draw-hook, and Knowledge Demon events own their candidate supports on construction and replacement; decision forks preserve that isolated event support.
- `ControlledPlayableCombatStochasticStateV111` copies its typed decision transcripts, pending request support, and recycle continuation on construction, replacement, and projection, keeping a caller from rewriting the saved provider prefix. Live controlled outcomes append to their typed transcript and mixed chronology in one owner transition; the opening fixture shuffle advances its index, transcript, and pending request together.
- `RandomBoundaryEventV111.Fork` and `RandomBoundaryTranscriptEntryV111.Fork` own copies of nested request support and serialized payloads. `RandomBoundaryJournalV111` stores one globally ordered mixed primitive transcript; its typed operation lists are derived projections with independent per-operation sequence numbers, while weighted intent outcomes, physical card-instance identities, per-card energy-cost outcomes, Native pool-shuffle supports, generated-card model outcomes, card-transform outcomes, Native random-target outcomes, Native combat potion model outcomes, and Native draw-pile and enemy-pile insertion outcomes are annotations paired with their exact provider decisions. Each pool-shuffle annotation reuses `SemanticPoolShuffleEventV111` and records the ordered source identity support before its swap; the Native audit checks the descending swap chain against the primitive request sequence. Generated-card model annotations reuse `SemanticGeneratedCardEventV111` and retain the selected model beside its exact Native ticket and source operation. Transform annotations reuse `SemanticCardTransformEventV111` and retain the original model, ordered replacement pool, and selected replacement beside its exact Native ticket. Random-target annotations reuse `SemanticTargetEventV111` and `SemanticTargetDecisionV111` to retain ordered enemy identities and the selected identity beside the exact Native ticket. Combat potion annotations reuse `SemanticPotionGenerationEventV111` and `SemanticPotionGenerationDecisionV111` to retain the ordered potion-model pool and selected model beside the exact Native ticket. `SemanticNativePolicyAuditV111` owns its Native index decisions, mixed provider trace, weighted intent entries, and registered semantic outcome annotations, validating the trace fingerprint and its complete index projection plus each semantic result against the matching request, sampled value, and cursor span. Native-policy source-route entry and later audited player transitions retain these values through the same owner, with schema markers enforcing trace and annotation completeness for new audits. `SemanticSourceRouteAuditV111` owns its action and entry-decision arrays. `SemanticPilotStochasticStateV111` stores and returns forks from those value owners; the audit remains provider-private replay evidence.
- `SemanticCardEnergyCostAuditV111` reuses the existing semantic bounded-cost decision for the Native random-hand-cost site and both Native drawn-card cost hooks. Hand-cost entries retain the ordered eligible hand support; draw-hook entries retain the single physical card instance and its relic or Slither stage. Newly created Native-policy audits use the V2 cost schema and require one annotation per registered bounded-cost request in the mixed trace. V1 snapshots retain their original hand-only completeness rule. This captures selected outcomes and request provenance; source stream assignment and cursor parity remain separate.
- `SemanticPoolShuffleDecisionV111` also records the initial Native draw-pile shuffle over physical card identities, Native semantic recycle swaps over ordered discard identities, startup relic-bag swaps over ordered rarity queues, War Hammer's elite upgrade pool, and boss/Lava Rock relic offers, each with its current pre-swap order and exact provider decision. New Native-policy audits use pool-shuffle schema V4 for complete coverage of these pools; V3 snapshots retain combat-card, entry/recycle, and relic-queue completeness, V2 retains combat-card and relic-queue completeness, and V1 retains combat-only completeness.
- Native card-instance index audits use schema V2 to include Fishing Rod's `run.niche` combat-end selection alongside the established combat selection sites. The selected physical card is paired with the exact Native request; V1 snapshots keep the original combat-only completeness rule. `NativeNeowOfferAuditV111` remains the dedicated owner for Neow's offered choice IDs, RNG state, and typed decisions.
- Native reward card-model draws reuse `SemanticGeneratedCardEventV111` to retain the source-ordered offer pool and selected model beside the exact `card_rarity.encounter_model.index_draw` request. Generated-card audit schema V2 adds these `player.rewards` outcomes; V1 snapshots keep their original combat-only completeness rule.
- Native combat-reward `card_rarity.roll.unit_float` and `card_rarity.upgrade_roll.unit_float` draws retain their sampled values, rarity thresholds and offset change, or card upgrade threshold and result beside the exact `player.rewards` request. Card-odds audit schema V1 requires complete annotations for both float sites in newly marked Native-policy traces; older snapshots remain compatible.
- Native `reward.potion_odds.unit_float` decisions retain the offered result, strict threshold and pity update beside the exact `player.rewards` request. Potion-reward odds schema V1 requires one annotation per draw in newly marked Native-policy traces; forced rewards keep their draw-free source path and older snapshots remain compatible.
- Native combat-reward PotionFactory picks retain each `potion_factory.rarity_roll.unit_float` result, source-ordered same-rarity model pool, selected potion, and exact `potion_factory.model_within_rarity.index_draw`. PotionFactory audit schema V1 requires one paired outcome per rarity and model request on `player.rewards`; older snapshots remain compatible. Pool completeness and stream/cursor parity remain separate claims.
- Native combat rewards also retain the raw GoldReward amount and its exact bounded `player.rewards` decision, plus relic rarity, fallback tier, eligible committed queue support, and selected relic for each `relic_grab_bag.rarity.unit_float`. Their additive V1 schemas require one annotation per matching request in new Native-policy traces. Gold pool transforms and relic queue completeness remain separate from source parity; gold is recorded before later reward modifiers.
- The Native source-route baseline retains `NativeEncounterFormationAuditV111` with the generated formation and typed formation-index choices. Audit schema V2 also records each initial `run.niche` HP result with its enemy combat identity, ordered HP support, selected HP, and exact provider decision. Restore checks those outcomes against both the mixed Native trace and the source-route combat origin; V1 and older unmarked audits keep their prior requirements. These records expose the emulator's entry results without establishing source-pool eligibility or installed-game parity.
- `SemanticDrawInsertionNativeAuditV111` reuses the semantic draw-insertion event and decision for the three supported Native draw-pile insertion sites. `SemanticEnemyPileInsertionNativeAuditV111` records the same source outcome fields for Native enemy effects that insert cards into the player's draw or discard pile. Their separate additive Native-policy schemas require one outcome for each registered request; the Native summon-pool site remains fail-closed pending its source RNG audit.
- `RngBundle` copies its stream collection on construction and replacement, rejects null or unnamed streams and duplicate stream IDs, and returns a defensive copy from `Streams`. Runtime reads use `GetStream` or `TryGetStream` by stable ID, and `ReplaceStream` rejects a replacement whose ID differs from the named owner; array-index replacement remains available only for focused malformed-state fixtures. Each `RngStreamState` copies its opaque RNG byte buffer on construction and replacement and returns a defensive copy from `StateBytes`, so callers cannot mutate stored RNG state through exposed arrays.
- `RandomIndexRequestV111` owns its ordered support and returns a copy, so the central typed request cannot be changed after it has been accepted by a continuation or provider boundary.
- `ControlledStochasticDecisionV111` owns its optional ordered candidate labels, including when nested in a provider transcript. `ControlledStochasticContinuationStateV111` copies its decision prefix and pending labels on construction and replacement; the suspended-chance exception also returns a defensive candidate-label projection. These value boundaries cannot expose mutable support metadata through a shallow journal or pending request.
- `StochasticSessionCoverageV111`, `StochasticChanceLawQueryCoverageV111`, `StochasticCapabilityManifestV111`, and `ChanceLawV111` own their advertised site, operation, status, and outcome arrays. Validated capability coverage and read-only Chance Laws therefore cannot be rewritten through constructor inputs or returned array projections.
- Semantic and controlled event enumeration results own their ordered outcome arrays and outcome records; explicit result forks also fork child pilots or runs. This preserves support order while letting child sessions retain their own defensive state owners.
- `PlayerState` owns copies of its deck, relic, potion-slot, and character-resource values on construction and replacement. Those array projections, including `CharacterResourceState.Orbs`, return defensive copies while preserving the existing snapshot members.
- `RunConfiguration` and `SimulationProfile` own the configured act route, unlocked-character list, revealed-epoch list, optional Colorless epoch unlock IDs, and seen-boss history; `V111CharacterDefinition` owns its starting deck and relic lists. Their array projections cannot be edited through shared references.
- `MapState` owns its node collection and entry-node list, and each `MapNodeState` owns its outgoing-edge list. Map graph projections return defensive copies while preserving the existing snapshot members.
- `ShopState` owns its card, additional potion, and additional relic offer arrays. Its compatibility projections retain the established offer shape and do not expose mutable stored arrays.
- `PrototypePendingDeckChoiceState` and `PrototypePendingEventDeckChoiceState` own their candidate card-instance arrays; `PrototypePendingEventPotionReplacementState` owns its candidate slots. These continuations return defensive copies while preserving their positional constructor and deconstruction shapes, JSON names, optional parameters, record `with` replacement behavior, and fork semantics.
- `RewardState` owns its card/relic/potion options, bundles, completion and upgrade flags, enchantments, generator metadata, and returned-card arrays, including nested groups. Its public projections are defensive while reward transitions publish replacements.
- `EventMechanicsStateV111` owns queued potion IDs, pending deck-choice records, and offered choice IDs. `EventCommittedRandomStateV111` owns the Repy potion/relic rolls and Future of Potions offer collection, while `EventStochasticStateV111` forks its suspended controlled event-choice frame on access. `EventState` keeps its existing flattened members while these continuation values remain isolated.
- `PrototypeControlledEventChoiceMechanicsStateV111` owns its queued potion IDs and deep-forks queued deck-choice records. `ControlledPlayableCombatMechanicsStateV111` owns its action history and intent checkpoint and publishes each player action together with the resulting run. `SemanticPilotMechanicsStateV111` owns its opening fixture and typed continuation history, using the same decision-level fork logic at snapshot and fork boundaries; each pilot history entry validates its kind against exactly one supported payload before it is copied or replayed, and validated history appends now go through the mechanics owner.
- `CombatMechanicsStateV111` owns enemy, card, power, relic, potion, shield, autoplay, pet, and transformed-card collections, deep-copying nested values on construction, replacement, and projection. It also owns the configured fully-unlocked profile bit and optional Colorless epoch IDs needed by Native Colorless pool resolution; run validation checks configured values against `RunConfiguration`, while legacy snapshots without this metadata remain readable and fail closed if they reach that operation. `PrototypeCombatCardSnapshot` owns its `JsonElement` and keyword-override array, `PrototypeQueuedOperation` owns its deferred drawn-card identity prefix, `PrototypeCombatCounters` owns its played-card tag and identity histories, and `PrototypeKnowledgeDemonChoiceState` owns its ordered offer IDs. The combat owner forks these values plus pending choice trees and automatic-pipeline continuations across the flattened `CombatState` snapshot boundary. Nested Nature supports and continuation prefixes therefore cannot be changed through a shared array reference.
- `PrototypeEventSubscriberState` and `PrototypeEventDispatchContinuationState` own event-effect arrays, power-card payloads, current subscribers, and remaining subscriber queues. Dispatch frames now return defensive projections and fork the complete saved event boundary without changing their positional record or JSON shapes.
- `SelectCardsPayload` owns its selected physical-card identity array on construction and `with` replacement, and returns a defensive projection. The action's established JSON property, positional constructor, and deconstruction shape remain stable.
- `SimulationStateV111<TMechanics, TStochastic>` rejects null mechanics or provider owners on construction and `with` replacement, so every composed envelope uses the same complete-state invariant rather than relying on domain-specific guards.
- `GameAction` clones its `JsonElement` payload on construction and record replacement. The public payload remains immutable and safe after the source `JsonDocument` is disposed, including when actions enter saved histories or replay continuations.
- `TransitionResult` owns a fork of its `RunState` and a copy of its diagnostics on construction, replacement, and projection, so callers cannot mutate an engine result through shared run state or diagnostic arrays.
- `PrototypeContent` keeps its ruleset, starting deck/relic templates, shared card/potion/relic pools, Overgrowth encounter pools, and consolidated encounter catalog in private backing storage. The ruleset projection copies map-floor room pools, required-room pools, and the automatic pipeline; public card, potion, relic, power, enemy, status, and event dictionaries are clone-on-read views over private maps; nested mutable definition arrays are copied through the shared definition copier on lookup, value enumeration, and entry enumeration. Array projections return copies, and encounter definitions use the shared deep copier, so consumers cannot rewrite candidate support, route rules, or nested formation rules globally.
- `ExperimentalSilentThreeActRouteV111` keeps its Hive/Glory encounter, boss-discovery, and event pools private and returns copies from its existing array members; external route audits cannot rewrite the configured encounter or event supports.
- `NativeDarvEventV111`, `PrototypeNativeUnderdocksTrashHeap`, and `NativePaelRewardsV111` keep their supported relic, event card/relic, and Pael's Legion skin supports private; their existing array members return copies so configured choices remain stable across law queries and replay.
- `NativeGloryAncientsV111` keeps its modifier-generated curse support and Ancient card/relic catalogs private. Array projections clone the definitions and their nested trigger/effect/tag arrays, so callers cannot rewrite the Distinguished Cape law support or the configured Ancient offers.
- `PrototypeNativeOvergrowthEvents` keeps Neow's positive, cursed, and conditional relic pools plus the regional event catalog private. It returns defensive name/ID arrays and deep projections of relic and event definitions, including choice effects, so Neow laws and event generation share stable source supports.
- `PrototypeNativeHiveEvents` keeps its source-ordered event-name pool private and returns deep copies of its card, relic, power, enemy, encounter, and event catalogs. `PrototypeNativeUnderdocksEvents` returns copies of its native and supported event identity pools, so route selection and coverage checks cannot rewrite configured support.
- `PrototypeNativeLaterActs` keeps Hive/Glory encounter-name inventories and its card, event, power, enemy, and encounter catalogs private, returning deep copies through the shared definition copier.
- `PrototypeNativeHiveBosses`, `PrototypeNativeGloryWeak`, `PrototypeNativeGloryScrolls`, `PrototypeNativeGloryNormals`, and `PrototypeNativeAdditionalNormals` keep their power, card, enemy, and encounter catalogs private and return deep definition copies. Their nested enemy AI, effects, and formation support therefore cannot be rewritten by mutating a returned catalog array.
- `PrototypeNativeHiveNormals`, `PrototypeNativeHiveRemainingNormals`, `PrototypeNativeHiveElites`, `PrototypeNativeHiveSummoningNormals`, `PrototypeNativeGloryBosses`, `PrototypeNativeGloryElites`, and `PrototypeNativeGloryNewNormals` use the same private catalog and deep projection boundary.
- `PrototypeNativeGloryEvents`, `PrototypeNativeUnderdocksEvents`, the Endless Conveyor event-card catalog, and Trash Heap's relic/card/power catalogs now return private-backed deep definition copies. Fake Merchant's stock IDs, event definition, relic/power/enemy catalogs, and encounter definitions use the same defensive boundary; Punch Off's encounter projection is also detached.
- `PrototypeNativeUnderdocks` keeps native and supported encounter order pools private and deep-copies its power, enemy, and encounter catalogs. `PrototypeColorlessCards` keeps its native IDs, implemented and multiplayer-only definitions, and derived shop/reward/generation pools private; card definitions and array projections are copied on access.
- Source event and potion singletons, including Brain Leech, Self-Help Book, Stone of All Time, Future of Potions, Potion Courier, This or That, Tanx, Vakuu, Nonupeipe, Orobas, and Darv, return copied event/potion definitions rather than exposing nested mutable choices or effects.
- Fabricator's slot names and formation positions and the Native snapshot importer's required-field lists use private backing arrays with copy-returning properties; importer results also own their reported field lists.
- `NativeAncientRewardsV111`, `PrototypeNativeOrobasRelics`, `NativePaelRewardsV111`, `PrototypeNativeSilentRelics`, and `PrototypeNativeRestSiteRelics` keep their card/relic source catalogs private and return deep copies. Tezcatara and Pael event definitions also return copied choices and effects, preserving offer supports across event generation and law queries.
- `PrototypeDefinitionCopiesV111` centralizes deep value copies for card, potion, relic, power, enemy, encounter, and event definitions, including nested effects, AI branches, formation pools, and choice arrays; source catalog owners use this shared copier.
- `PrototypeLegacyShopPriceV111` keeps its shared potion/relic price outcome ranges private and returns copies, preserving the same bounded support for legacy shop generation and its Chance Laws.
- `PrototypeRelicBagPullPlanV111` owns the active fallback tier's candidate IDs on construction and replacement and returns defensive projections, so a saved pull plan cannot be changed through a shared support array.
- `RunWorldMechanicsStateV111` owns its encounter, event, and completed-room history arrays. `RunWorldStochasticStateV111` and its Act 1, regional encounter, relic, and prepared-bag records copy persistent queue arrays on construction, replacement, projection, and fork.
- `ControlledEventChoiceTraceV111` and `ControlledRunActionTraceV111` own their typed decision/request and nested candidate-label arrays on construction, replacement, and projection. `RunStochasticStateV111` also copies its controlled event-choice and run-action history arrays on construction, replacement, projection, and fork.
- ReferenceNature session envelopes and snapshots own their typed decision transcripts; the event-choice session also owns its ordered player/Nature steps. Merchant, Treasure, Native event-relic, Native Shovel, and combat-reward sessions own their provider prefixes, pending requests, and candidate labels; map-generation sessions own typed decisions, requests, and map outputs; Native event-shuffle sessions own their planned candidate identities. Other offer, event, reward, and map sessions own their decision arrays. Shared `ReferenceNatureSessionValueCopiesV111` helpers copy common arrays and typed requests; event-choice steps deep-copy action payloads.
- `NativeNeowOfferAuditV111`, `NativePaelOfferAuditV111`, and `NativePotionModelSelectionAuditV111` own their offered IDs, run/RNG snapshots, and typed decision arrays on construction, replacement, and projection. Their existing positional constructors, deconstruction, and JSON member names remain stable.
- The Orobas, Darv, Nonupeipe, Tanx, Vakuu, and Tezcatara offer audits, plus Native initial-HP and intent audits, fork their returned run/combat state and own their typed decisions on construction, replacement, and projection. Their positional constructors, deconstruction, and JSON members remain stable.
- `NativeFisherYatesResultV111` owns its ordered input, pre-hook result, and typed swap decisions across construction, replacement, and projection, keeping the source-shaped shuffle result detached from caller mutation.
- `NativeEncounterFormationV111` owns its selected enemy formation and monster-local RNG contexts; `NativeEncounterFormationAuditV111` owns its formation choices and typed initial-HP outcomes, including ordered supports and exact provider decisions. Context stream bytes stay protected by `RngStreamState`, and cursor advancement publishes a replacement context array.
- `ControlledInitialHpStateV111` owns its pending request and typed decision transcript, including each request's ordered support, across construction, replacement, projection, and fork.
- `ControlledIntentStateV111` owns its prior-HP and intent decision transcripts plus the active ordered request support. Combat and formation checkpoints continue through their mechanics owners, and named reference streams use `RngBundle` copy-on-replace.
- `ControlledTurnIntentStochasticStateV111` owns its typed transcript and pending request on construction and replacement and returns defensive transcript copies. This prevents an exposed suspended intent prefix from being rewritten.
- `NatureBoundaryEventV111`, `NatureReplayChoiceV111`, and `NatureBoundaryViewV111` own pending candidate supports, candidate model labels, and replay transcript arrays on construction, replacement, and projection. Mutating a Nature-facing view cannot alter its stored or nested boundary metadata.
- `ControlledRecycleShuffleV111` owns its baseline and partial permutation arrays plus its transcript and pending request support, so a saved live recycle cannot be rewritten through a projection.
- `SemanticOtherChanceStateV111` copies its named RNG bundle and typed decision transcript on construction, replacement, projection, and fork; mutation through a returned stream bundle or history array cannot alter the run-owned provider.
- `ScriptedRunActionRecordV111` owns its JSON action payload and typed decision prefix, while scripted run sessions and serialized snapshots own their completed-action arrays. The per-action scripted session and snapshot own their action payload and decision prefix, and `ScriptedRunActionOutcomeV111` owns candidate labels. Their positional constructors, deconstruction, JSON names, and `with` updates remain available without sharing mutable history arrays.
- The engine owns enemy history and eligibility filters; once an intent is resolved, the resulting committed enemy intent is mechanical state and becomes player-visible at the ordinary game boundary.
- Native provider state and Nature state are alternative realizations: a Nature counterfactual has no obligation to match a native seed or shadow cursor.
- A complete state fork or serialized snapshot must include both components **atomically**. No stale `CombatState.DrawPile` / `SemanticDeckStateV111` dual ownership is permitted in the target model.
- Exact snapshots and fingerprints are **privileged**; fair policy observations omit private order, RNG cursors, seeds and Nature-only candidate metadata.

All six typed stochastic request families validate their common envelope and a registered SiteId before provider resolution. The currently unaudited ranged-float, ranged-double, and Gaussian-double families have empty Native site registries and fail closed until source sites are audited and registered. Native control additionally checks the exact SiteId, stream, and operation triple. Typed decisions require both valid codec snapshots for Native provenance and reject cursor snapshots on counterfactual providers. Native typed decisions replay their declared operation from the saved before-cursor and validate the sampled value and after-cursor, including rejection draws for bounded Gaussian operations; this proves transcript self-consistency without establishing source call-site or seed parity. Each mixed transcript entry validates its typed payload through the same decision owner, including index/value agreement; both scripted providers, resumable run-action sessions, ordered comparison, and the Native-policy audit consume that validated payload.

The present run and combat types have overlapping representation/lifecycle responsibilities. Apply a conceptual `SimulationState { Mechanics, Stochastic }` envelope incrementally through `SimulationStateV111<TMechanics, TStochastic>` wherever the existing domain owners can be composed without rewriting or breaking versioned types. Make one authoritative write path and assert zone/constraint correspondence during migration. `RunState.Simulation` now pairs the complete `RunMechanicsStateV111` with `RunStochasticStateV111`, which owns the prototype RNG bundle, native stream bundle, ReferenceNature cursor, and controlled run-action/event-choice continuations and histories; `RunWorldState.Simulation` composes persistent world mechanics with `RunWorldStochasticStateV111` encounter/relic bag realizations; `CombatState.Simulation` composes the full combat mechanics state with `CombatStochasticStateV111` native RNG and semantic deck state; `PrototypeAutomaticPipelineContinuationState.Simulation` composes its mechanics cursor with controlled resume values in `PrototypeAutomaticPipelineStochasticStateV111`; the mechanics owner exposes `CurrentStepKind`, safe `IsAtStep`, valid-index, remaining-step, opening-stage-at-end, passed-dispatch, and immediately-after queries, plus validated step advancement, so semantic end-turn, controlled-playable, draw, Mayhem, and target-boundary adapters share cursor interpretation; it validates cursor/opening-stage boundaries; `ControlledTurnIntentsV111.Simulation` composes its combat checkpoint and cursor with the pending intent-provider request and transcript; `SemanticPilotCombatV111.Simulation` composes the playable run/action history and runless pending end-turn mechanics projection with `SemanticPilotStochasticStateV111` provider provenance and end-turn/draw transcripts; its flattened `PendingEndTurn` facade reconstructs the saved frame from the pilot's current run; `SemanticPilotCombatEngineV111.PublishRunContinuation` publishes opening, player-action, initial-intent, deck-reveal and other supported semantic-choice outcomes through one transition for the run, reward audit and optional pilot history; unrelated choices preserve the saved pending end-turn frame, resumed end turns replace it or clear it at completion, and callers may publish an updated `OtherChance` state; `SemanticEndTurnV111.Simulation` composes suspended run/intent/draw mechanics with end-turn and draw transcripts plus boundary chronology; `ControlledPlayableCombatV111.Simulation` composes its fixture/run/action history with provider cursors and choice history; its recycle stage owns the active shuffle transcript while provider state retains only completed recycle decisions, and the flattened `RecycleTranscript` projects both; active events compose `EventMechanicsStateV111` and `EventStochasticStateV111` in a `SimulationStateV111` envelope, with `EventCommittedRandomStateV111` holding local committed outcomes; `PrototypeControlledEventChoiceContinuationV111.Simulation` pairs the saved partial run/effect cursor and queued consequences with its chance prefix; and `PrototypeControlledEventRewardChoiceContinuationV111.Simulation` pairs the post-reward mechanics boundary with the same typed chance payload. The old `Rng`, `NativeRunRng`, `ReferenceNatureState`, controlled history/choice, encounter/relic bag, `NativeRng`, `SemanticDeck`, and event/pilot provider members remain compatibility projections. Existing Native snapshot shapes remain unchanged; Nature-owned relic identity sets serialize an optional order-mode discriminator so they cannot be restored as committed Native queues. Active event progress, queued consequences and suspended controlled effects now have explicit mechanics owners. Nested automatic-pipeline and specialized continuations retain their own boundary envelopes. The opt-in ReferenceNature sampler cursor persists only when its atomic semantic, run-action, event-choice, Ancient-offer, Neow-offer, Trash Heap, Tablet of Truth, Trial, native event shuffle, Native event entry, Native Shovel, Endless Conveyor, Glory Reflections, Infested Automaton, Punch Off fight-entry and rewards, native merchant, Treasure, or Shovel session commits; pending specialized continuations retain their matching origin cursor until completion. The Ancient-offer session samples Orobas, Pael and Tezcatara's direct offer laws; Nonupeipe, Tanx and Vakuu's conditioned Fisher-Yates swaps; or Darv's binary offer-variant law followed by its complete conditioned relic shuffle as one boundary. It replays the complete transcript through the corresponding event generator before publishing the run and cursor together. The Neow-offer session samples the modeled Act 1 option graph before replaying the full prefix through the existing generator. The Trash Heap session applies one fixed-pool card or nonlethal relic reward through the ordinary event mechanics and commits its sampler cursor atomically. The Tablet session handles one eligible physical-card upgrade draw per page, then commits the page result and cursor through the ordinary event action. The Trial session applies its three-way case draw through the existing event action and commits the case page with the sampler cursor. The Glory Reflections session accumulates downgrade and upgrade card picks against a projected deck, then commits the completed `touch_mirror` action with the sampler cursor. The Infested Automaton session conditions its single card index on the selected `study` or `touch_core` action, replays the ordinary choice, and commits the Nature cursor; its support is limited to the implemented Silent reward pool. The Punch Off fight-entry session samples both CombatId-owned 2–9 HP offsets before replaying the ordinary `fight` action, scripts only those monster-local requests, verifies monster and event cursors, then commits combat with the Nature cursor. The separate rewards session conditions the relic and potion choices in source call order, then replays a legal combat action only when it wins immediately and produces exactly those two `player.rewards` requests. It scripts only the two reward requests, preserves both the entry reward cursor and event cursor, and commits the reward state with the Nature cursor; `player.rewards` attribution remains a hypothesis.

`RunStochasticStateV111` validates and owns completed controlled event-choice and run-action trace appends. Event-reward completion replaces the latest event-choice trace through the same owner, and run-action completion publishes its trace while clearing the active continuation in one transition.

The controlled run-action continuation also covers the later legal
`choose_shop_relic_deck_card` and `choose_relic_deck_card` actions when they
resolve a pending random relic transform. The saved action binds the selected
card instance, original model, relic choice and ordered replacement pool; replay
checks the legal action and source request before committing the result and
ReferenceNature cursor. Fixed-result relic transforms and other deck-choice
owners continue through their existing paths.

The existing controlled run-action frame also saves and replays legal legacy
`take_reward_relic` and `take_reward_extra_relic` actions from Combat, Elite,
or Boss rewards when their acquisition hooks reach a registered
relic-acquisition site. It preserves the exact action and selected request
prefix across continuation snapshots. The specialized configured Native
session also covers primary and extra combat-reward groups for its four
registered relic-acquisition hooks. The same shared run-action owner also
captures legacy `rest_heal` reward generation when a relic creates card
offers, including its registered rarity and card-model requests.
For configured Native Act 1 Rest Heal, the same ReferenceNature run-action
session captures the Native `player.rewards` rarity float, encounter-model
index, and upgrade float in their source order. It conditions the rarity law
on the saved offset prefix and commits the action fingerprint, generated
reward, and Nature cursor through the existing typed transcript.

`RunStochasticStateV111.RebasePendingContinuationCheckpoints` now owns publication of active event-reward and run-action partial checkpoints after a step has committed mechanics and reconciled Native provider state. It builds each checkpoint with that continuation removed, updates the saved frame, and validates it against the complete run owner. `PrototypeGameEngine.FinalizeStep` delegates both rebases through this transition; the flattened snapshot members remain unchanged.

`RunStochasticStateV111.SuspendControlledRunActionChoice` and `SuspendControlledEventRewardChoice` validate a suspended frame against its exact partial run, attach it through the run stochastic owner, and verify the resulting complete owner. Start-run, map-node, Shovel, event-reward, replay-resuspension, and post-step rebasing use these transitions.

`RunStochasticStateV111.CommitReferenceNatureRunInitialization` publishes the run's Nature cursor, completed initialization transcript, and pending marker together. A completed transcript must replay to the committed cursor or an earlier prefix of the same seed. Configured run creation and ReferenceNature run-start completion use this transition, while the flat snapshot members remain compatibility projections.

`SemanticPilotCombatEngineV111.PublishRunContinuation` publishes the run-scoped `SemanticOtherChanceStateV111` and reward cursor audit through `RunStochasticStateV111.PublishSemanticPilotContinuation`, preserving the mechanics result and validating the engine-owned Native boundary provider before committing the pilot transition. The pilot's `OtherChance` and `RewardAudit` members remain compatibility projections.

`ScriptedRunStochasticStateV111` validates and appends completed action records while clearing the active action. The scripted run envelope publishes that stochastic update with the resulting run mechanics checkpoint.

`ControlledTurnIntentStochasticStateV111` validates a selected ticket against its saved pending request and ordinal, then appends it and clears the request as one transition. Both live resolution and transcript-prefix validation use this operation.

`ControlledRecycleShuffleV111.AdvanceSwap` validates each selected ticket against the saved request and updates the shuffled pile, swap cursor, transcript, and next request together. Live resolution and replay validation share this transition.

`ControlledInitialHpStateV111.ApplyChoice` validates the selected ticket against its saved request and publishes the enemy HP, enemy cursor, transcript, and next request together.

`ControlledIntentStateV111.AppendResolvedDecision` checks the selected ticket against the saved request and ordinal, appends it, and clears the request as one transition shared by live resolution and reachability replay.

`ReferenceNatureRunActionStochasticStateV111.Advance` publishes a ReferenceNature cursor advance with its decision transcript append. Run-action resolution and nested Native combat-entry, map-generation, Ancient-offer, and later-act continuations use the same owner transition.

`ReferenceNatureRunCursorV111.Commit` publishes through `RunStochasticStateV111.CommitReferenceNatureCursor`; it rejects changing a committed seed, moving the sample count backward, or replacing the cursor at the same sample count. Session owners still validate the complete decision prefix before committing an advanced cursor.

The flattened `RunState.ReferenceNatureState` projection routes non-null writes through the same guarded owner transition. Explicit cursor clearing uses `RunStochasticStateV111.ClearReferenceNatureCursor`.

`RunStochasticStateV111` validates and appends completed ReferenceNature map-generation audits and Ancient-offer snapshots, including when a legacy Native Ancient transition is replayed for validation.

Legacy run-action snapshot restoration also imports missing map-generation audits through `RunStochasticStateV111.AppendReferenceNatureMapGenerationAudit`; the compatibility upgrade no longer replaces the complete history array directly.

`RunWorldMechanicsStateV111.AppendEventId` owns event-history updates. Ordinary event entry, Native Ancient setup, and source-backed event fixtures publish the append through the world mechanics owner while retaining the flattened snapshot field.

`RunWorldStochasticStateV111.RelicBag` validates non-null bag rarity partitions, identities, and order mode on construction and replacement, including `WithRelicBag` calls from the flattened world projection. `CommitRelicBag` names runtime acquisition pruning and pull publication. Full bag-to-player ownership checks remain at the run boundary, where mechanics have completed the acquisition and cleanup sequence.

Live Act 1 encounter selection publishes weak, normal, and elite pool changes through `RunWorldStochasticStateV111.CommitActOneEncounterPool`, which validates each candidate against the current world region and encounter cursor.

The `RunWorldStochasticStateV111.ActOneEncounterPool` owner property validates the pool's region, cursor, remaining queues, and boss identity on construction and replacement; `Validate` additionally checks its correspondence with the current world region.

Experimental Hive/Glory combat entry publishes its selected encounter bag through `RunWorldStochasticStateV111.CommitLaterActEncounterBag`, which validates the configured route, active act, Ascension rules, and revealed Ancient support before publication.

Interactive and configured experimental run-start completion publish the future-act encounter bags through `RunWorldStochasticStateV111.CommitPreparedLaterActEncounterBags`, validating the complete ordered set before it becomes persistent run state.

Three-act continuation publishes the target act's active encounter bag, remaining future bags, and Act 1 pool clear through `RunWorldStochasticStateV111.CommitActTransition`. The owner validates the target queues and shared Darv assignment across both bag groups before publication.

The run-world stochastic owner also validates the structural shape of active and prepared later-act bags on construction and replacement. Run-boundary validation retains profile, Ascension, act-order, and shared-assignment checks that require the configured run.

The same world mechanics owner now appends encounter IDs and completed-room records. Ordinary and controlled-playable combat entry plus map-room completion use those transitions.

Native ReferenceNature sessions that project persistent relic bags now validate both their saved origin and replay state. Any present bag must use `NatureUnordered` and pass its canonical rarity, ownership, uniqueness, and sort invariants; the Native event-gold boundary may preserve an absent bag. Calling Bell event-choice, Native event-relic, Neow pickup, and Native Shovel replay apply the same validation when their projected bag is required.

The seeded comparator's engine-owned drawn-card-cost provider and its terminal-reward cursor audit are stored in `RunStochasticStateV111` with the other run-scoped providers. `SemanticOtherChanceStateV111` owns the complete fifteen-stream Native run bundle, its per-stream starting call counts and cursor snapshots, and a mixed typed Native boundary transcript with the existing index-decision array as a compatibility projection. It can resolve Native index, unit-float, ranged-float/double, and Gaussian-int/double requests when the site/stream/operation registry permits them; unregistered pairs remain fail-closed. The caller still applies each selected result to its saved mechanics continuation. Earlier index-only and cost-only provider snapshots remain readable. `RngBundle` resolves streams by stable ID and checks that replacements preserve the named owner; Native and prototype draws and the core stream-reset paths use that named access instead of carrying array indices across state changes. Its serialized stream arrays remain unchanged. The semantic pilot's established `OtherChance` and `RewardAudit` fields remain compatibility projections to that run owner, so provider state is forked and validated with its run and is not duplicated in the pilot stochastic envelope.

`RunStochasticStateV111` also owns the optional configured run-initialization transcript. It retains the initial and final ReferenceNature cursors, ordered Chance Laws, and decisions from the Act 1 boss choice and, for Overgrowth, a compact typed map-generation prefix and map fingerprint. The experimental three-act route adds Ancient assignment and prepared encounter bags. Configured Underdocks boss selection suspends and resumes through the existing `ReferenceNatureRunActionSessionV111`; configured Overgrowth composes its boss choice with the existing map-generation session inside the same run-action continuation. Experimental starts then compose the existing Ancient-assignment and bag generators into that continuation: Overgrowth starts preparation after map completion, while Underdocks starts it after boss selection. The same run-action session now composes the existing Hive/Glory map-generation session with configured experimental `continue_act`: the run remains at the transition boundary while map choices resolve, then the engine applies the completed map through the ordinary transition step once. The completed map's compact typed audit is retained in `RunStochasticStateV111` and replayed from snapshots. Assigned-Ancient transition effects retain their existing providers. The run and complete initialization transcript publish only after all start choices finish; the pending run snapshot marks initialization incomplete while the outer session owns its partial prefix and Nature cursor. Saved action-session validation replays transition maps and assigned Ancient offers from their typed prefixes. Snapshot validation replays the declared initialization laws in order, replays the Overgrowth map generator, later-act preparation generators, and retained transition maps, and verifies their cursors and fingerprints; individual initialization factories bind selected mechanics outcomes to their source-shaped boundaries before the transcript is attached.

Unconfigured prototype `continue_act` now uses the same controlled run-action continuation for its registered `map.legacy.*` requests. The saved frame checks the act-transition boundary and map stream, replays the exact selected prefix, and only exposes the completed map with the advanced prototype RNG and action trace. A `ReferenceNatureRunActionSessionV111` automatically reuses that action continuation and commits its cursor when map generation completes. This extends legacy prototype coverage; it does not claim Native map-generator coverage, source geometry or call-order fidelity, or calibrated legacy probabilities.

The same run-action session admits legal out-of-combat use of Fruit Juice, Blood Potion, and Neow's Ambergris through their exact deterministic modeled run effects, and Entropic Brew through a typed continuation that captures one pool draw for each empty slot. Entropic Brew is covered in non-event phases, before an Event choice, and after an event choice while its reward remains pending, provided no full-belt potion replacement is pending. The event-choice frame keeps post-choice fill outcomes and the player action in its existing ordered transcript. One shared request predicate checks the potion, stream, pool fingerprint, slot owner, and support across both session paths. Deterministic Event potion replacement uses the same event-specific source action; Entropic Brew replacement still requires its stochastic continuation. Legacy runs use the ordered prototype-pool Chance Law on `reward`; Native runs use the `player.rewards` bounded-index law. Native game pool ordering, trigger order, and stream attribution remain unverified pending the reference mod.

When a configured experimental transition resolves an assigned Ancient through ReferenceNature, `RunStochasticStateV111` also retains the completed existing Ancient-offer session snapshot. Run snapshot validation replays its ordered offer decisions, binds its opening boundary to the corresponding map audit and assigned Ancient, and requires later map and run cursors to follow its final Nature cursor. Older transitions that opened the Ancient through their original Native provider remain valid and are upgraded to the current map-audit shape on action-session restore.

`SemanticOtherChanceStateV111.Begin` retains the complete Native run bundle after validating its fifteen named streams. Each delegated Native typed request must pass source request validation and the registered engine-owned site/stream/operation check. The owner records the mixed operation transcript and verifies that each stream cursor follows continuously from its captured Native cursor origin. Native-labeled semantic hand-return swaps, supported draw-insertion positions, and saved enemy pile insertions use `run.shuffle`; supported summoned-enemy HP and isolated Tough Egg hatch HP requests use `run.niche`; drawn-card costs and random hand-cost values use `run.combat_energy_costs`; random hand-cost target selection, Jeweled Mask's random draw-pile card pick, Anointed's Rare-card shuffle, random-shortlist shuffle swaps, and saved random autoplay/replay card-instance selections use `run.combat_card_selection`; registered random-enemy targets use `run.combat_targets`; registered combat generated-card models, card-transform replacements, saved enemy-theft selections, and generated-card full-pool shuffle swaps use `run.combat_card_generation`; registered combat potion models use `run.combat_potion_generation`. Their saved semantic events still identify and resume the mechanics outcome, while the Native primitive transcript remains private to the engine-owned provider. Earlier index-only and cost-only snapshots remain readable. Other ordinary action boundaries remain separate integration work.

The opt-in native-policy comparator retains a provider-private, chronological mixed transcript of native-v111 index, unit-float, ranged-float, ranged-double, Gaussian-integer, and Gaussian-double boundaries. Each typed entry preserves its request, selected result, and before/after native cursor; the existing index-decision array remains a validated compatibility projection. The transcript is captured from setup, source-route intent entry, and native-policy actions, is deep-forked with the pilot state, and is never included in the fair player observation. `SemanticPilotStochasticStateV111` validates this audit alongside its other provider state. The native-policy baseline installs `NativeRandomBoundaryProviderV111` across all six typed primitives; its control guard admits exact SiteId/stream/operation triples, including the combat streams, combat-end War Hammer and Fishing Rod requests, Native summoned-creature HP and in-place Tough Egg hatchling HP requests on `run.niche`, the `player.rewards` draws made while a winning action creates its reward, and the `run.treasure_room_relics` queue shuffle used for an elite reward. PotionFactory rarity and model requests are explicitly paired with either `run.combat_potion_generation` or `player.rewards` according to their source context; all unlisted pairs fail closed even when both identifiers are individually registered. The legacy Pael's Tooth return remains a separately allowlisted engine-owned `reward` request; other prototype RNG requests fail closed during native control. Older snapshots with only the index projection remain readable. Its probability status stays `UNVERIFIED`; this audit data does not establish source call-site coverage or native parity.

Initial HP and Tough Egg hatch HP requests carry their numeric candidate lists
in the typed index support; initial attachment also returns
`NativeInitialHpAuditV111`. Their selected HP is therefore preserved in the
exact provider decision without a separate semantic outcome schema.

`RunSnapshot.Save` now writes the v4 simulation envelope using the existing
`RunState.Simulation` owner, so mechanics and stochastic state are serialized
together without flattening or duplicating the run provider. `RunSnapshot.Load`
continues to accept the established flattened v1, v2, and v3 envelopes. The v4
provider owner carries ReferenceNature cursors, Nature-owned relic-bag order
mode, and optional run-initialization transcripts in one versioned state.

The current S3 migration centralizes runtime draw-pile writes in `SemanticDrawPileAdapterV111`. It applies the mechanics membership change together with the already-committed provider transition, clones the physical identity array, and validates membership/order correspondence before returning a new combat state. The `CombatState.Mechanics` and public `CombatState.Simulation` initializers reject changed draw-pile contents whenever an old or new semantic deck is attached; the flattened `DrawPile` projection routes through those guards, while same-sequence assignments remain valid. The adapter uses `CommitSemanticDrawPileTransition` to publish the paired update after validating the candidate deck against the candidate physical pile. `CombatCardZonesV111` copies its arrays on construction and replacement and returns defensive copies from public projections, so in-place mutation and caller-owned array aliases cannot change stored zone membership. Its `Fork` retains an independent zone copy, while flattened snapshot properties and JSON names remain unchanged. `ResolveNatureDeckEvent` revalidates a saved draw, peek or scry request against the live Nature deck, resolves it, and commits the resulting provider state with its physical pile through the same adapter. `SemanticDeckEventV111.Validate` owns the common event-envelope rules shared by draw and reveal decisions, while each decision retains its operation-specific support checks. `SemanticDeckEngineV111.Inspect` validates each constructed event so law queries, observations and continuation consumers share that envelope check. Its `ValidateNatureDeckBoundary` helper also owns the common idle or pending Nature deck-to-zone membership check used by semantic player-action, source card-selection and end-turn replay, draw-continuation, automatic hand-draw entry and top-autoplay, after-shuffle continuations, and semantic effect-draw suspension/resume checks; other choice owners use the same helper while retaining their source support and boundary rules. `ValidateSynchronized` owns provider and exact-order validation at the idle Native policy boundary, Native top-autoplay and resumed source card selection. `ValidateProviderCorrespondence` owns checks against a candidate deck before it is attached to a combat snapshot. `RequireUnchangedDrawPile` centralizes the corresponding post-resume invariant for choices whose supported mechanics do not mutate the physical pile or provider deck. `RunState.Simulation` groups `RunMechanicsStateV111` with the run provider owner, while `RunState.Validate` owns run/world mechanics checks and delegates provider checks to the stochastic owners; those checks include controlled histories, named native event-context bindings, and suspended run-action/event-reward continuations. `RunWorldState.Simulation` groups persistent world mechanics with encounter/relic bags; the provider validates Act 1 and configured later-act encounter queues, shared Darv Ancient exclusivity, and relic queue membership, ownership and rarity partitions. `CombatState.Simulation` composes `CombatMechanicsStateV111` with native combat RNG and semantic deck state, while `CombatState.Validate` owns the mechanical combat invariant boundary and delegates provider checks to its stochastic owner. `PrototypeAutomaticPipelineContinuationState.Simulation` composes its mechanics cursor with stochastic resume values, validates its own step/opening cursor, and delegates recycle, semantic hand-draw, enemy-action and next-turn-intent checkpoint checks to the stochastic owner; `SemanticEndTurnV111.Intents`, `ControlledPlayableCombatV111.TurnIntents`, and `SemanticPilotCombatV111.PendingInitialIntents` are flattened snapshot projections of intent frames held by that continuation. `ControlledPlayableCombatV111.Simulation` pairs fixture mechanics with provider cursors and transcripts, and `ControlledPlayableCombatV111.Validate` delegates run invariants to `RunState.Validate`; `SemanticPilotCombatV111.Simulation` pairs its session mechanics and runless pending end-turn mechanics projection with `SemanticPilotStochasticStateV111` provider provenance and transcripts, and `SemanticPilotCombatV111.Validate` owns semantic boundary/provenance consistency; `SemanticEndTurnV111.Validate` and `SemanticCombatDrawV111.Validate` own specialized continuation checks; `PrototypeControlledRunActionContinuationV111.Simulation`, `PrototypeControlledEventChoiceContinuationV111.Simulation`, and `PrototypeControlledEventRewardChoiceContinuationV111.Simulation` pair saved boundary mechanics with the shared controlled chance payload. `EventState.Simulation` composes `EventMechanicsStateV111` and `EventStochasticStateV111`, and `EventState.Validate` owns active event invariant checks while delegating committed outcome checks to the stochastic owner. The event provider validates consistency of the suspended controlled choice and Future of Potions offers, Native event-entry result states, including gold rolls and selected potion slots, Tinker type selection, Conveyor dish progression, Repy's saved chest rewards and deferred card outcomes. These owners preserve their existing compatibility projections. RunSnapshot writes run mechanics and stochastic state in the v4 simulation envelope and continues to load flattened v1–v3 snapshots. `PrototypeMechanicsContinuationStateV111` now owns the shared run checkpoint across the major run-based continuation frames, and the ordinary automatic pipeline owns the source-route initial-intent and next-turn intent cursors across semantic-pilot, semantic end-turn, and controlled-playable facades. All identified gameplay draw-pile and card-zone writes now use the adapter boundary; broader draw-resume consolidation and cross-provider equivalence remain open.

`RunStochasticStateV111` now copies incoming `PrototypeRng` and `NativeRunRng` bundles and exposes defensive public projections. `NativeRunRngV111` and `NativeCombatRngEntryV111` read provider state through those defensive projections; their transitions fork before sampling or changing creature cursors, publish updated bundles through the stochastic owners, and validate the resulting state where the run and combat owners must agree. `NativeRunRngV111.SampleNextInt` publishes matching run/combat cursor copies together, and the ordinary engine's `ReconcileAfterStep` validates the pair after copying an active combat's advanced cursors to the run owner. Run configuration now copies the Native bundle from its input and prepares experimental later-act bags on a local prototype bundle before publishing the advanced copy. Overgrowth, Underdocks and later-act fixture initialization now fork prototype RNG through defensive projections, and ReferenceNature creation audits use defensive projections when checking unchanged legacy cursors. Map-node selection now advances unknown-room choices on a local prototype bundle and publishes it before room entry; later-act encounter selection reads the defensive projection and commits only the encounter bag, since that source operation makes no legacy RNG draw. Event entry now uses one local prototype bundle for event selection and legacy gold or potion-slot generation, then publishes its cursor before Native event-entry continuations resume. Shop inventory generation, card and potion prices, relic offers, and ShopEntered hooks now advance local prototype and optional Native run bundles and publish them with the completed shop state; shop purchases, relic hooks, and pending relic deck choices publish their local cursor with the action result. Combat reward generation now advances local prototype and optional Native run bundles through pity, gold, potion, card, and relic offers, then publishes them with the resulting reward state. Deferred custom event reward completion now publishes local prototype cursors with any acquired relic cards, additional card grants, or random curse grant and the returned event/reward state. Rest-site hatching, healing hooks, and Shovel relic rewards now use local prototype and optional Native run bundles and publish the advanced cursor with the completed rest or reward state. Out-of-combat potion effects now advance local prototype and optional Native run bundles and publish them with the resulting player state. Deferred event card creation, Neow relic-pickup rewards, Toy Box acquisition rewards, the Trial and Dense Vegetation event outcomes, Tea Master relic purchases, Hungry for Mushrooms relic acquisition, Tinker Time and Infested Automaton card grants, Colossal Flower relic acquisition, Glory Reflections Shatter and Round Tea Party outcomes, Battleworn Dummy reward offers, Driftwood card reward rerolls, War Historian Repy rewards and relic grants, Overgrowth treasure-room offers, Dream Catcher rest rewards, Fake Merchant purchases, Trash Heap card and relic rewards, Endless Conveyor dish resolution, Punch Off combat setup and rewards, History Course and Whispering Earring autoplay, persistent combat-end relic hooks, and deferred event reward construction now advance local prototype bundles and publish them with their event, reward, combat, or deck result; the Round Tea Party relic draw, Battleworn Dummy relic offer, Driftwood encounter-card reroll, Dream Catcher encounter-card rewards, Calling Bell rewards, Punch Off reward offers, and Repy chest offers also publish their optional Native run cursor. Native route checks and cursor audits across Chance Law, ReferenceNature sessions, controlled choices, event-context adapters, semantic-choice attribution, and semantic-pilot provenance/boundary validation now read defensive projections. Semantic pilot reward auditing and later-act ReferenceNature replay also use defensive prototype-RNG projections; controlled intent replay receives a defensive projection, while Native committed-intent auditing passes an empty legacy bundle and uses only its validated combat owner. The Core callsite census now finds mutable projections only inside their stochastic owner implementations. `CombatStochasticStateV111` applies the same copy-in and defensive-projection boundary to its native combat sidecar, with no `NativeRngMutable` use outside its owner.

`PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary` owns the shared clean run-level combat decision predicate used by semantic player-action, pilot, end-turn, and controlled-playable combat facades, player-action result validation, end-turn source replay, and initial-intent completion. The controlled-playable facade preserves its reward and terminal result boundaries while using the common predicate for live combat. `HasPendingControlledAutomaticPipelineChoice` classifies suspended enemy-action and controlled-target choices across automatic-pipeline stages; semantic end-turn entry/resume and controlled-playable staging share it. `IsAtSupportedAutomaticPipelineTargetBoundary` owns the admission policy for pre-AdvanceTurn targets and post-draw player-turn hooks/late autoplay, using the mechanics cursor owner’s valid-index, remaining-step, and passed-dispatch queries to interpret timing. End-turn completion separately requires the Nature deck to have no pending reveal.

`PrototypeAutomaticPipelineMechanicsStateV111.IsValidOrdinaryCursor`, `IsAtStepIndex`, and `RequireResumeStepIndex` own ordinary cursor validity, index matching, and validated step-index projection. Step-specific hand-draw, generation, intent, and target validators rely on the same cursor predicates instead of rechecking opening and post-dispatch fields. Runtime resume and saved enemy-action validation use the same index matcher. The resume projection preserves post-dispatch context for the caller while rejecting malformed or opening-stage cursors; serialized `NextStepIndex` remains unchanged.

The same automatic-pipeline boundary classifier includes Curse of Knowledge, whose source action suspends the `ResolveEnemyActions` step with a saved enemy identity, turn, curse index and two-card offer. `SemanticKnowledgeDemonChoiceV111` fingerprints the offered legal actions; end-turn stores the selected card in its chronological nested transcript and full restore replays the action before resuming that cursor. The choice itself consumes no RNG, so its Chance Law is not applicable; Native trigger-frequency and mechanics parity remain open.

`PrototypeGameEngine.IsAtCombatRewardOrTerminalBoundary` owns the shared classification for completed combat continuations and accepts only initialized rewards or closed defeats (with no live combat or reward). Semantic draw, cost, generated-card, transform, target, player-action, pilot, and end-turn adapters use it when accepting a reward or terminal result; each adapter keeps its own suspended-choice validation and reward-specific handling. `IsAtCombatActionResultBoundary` classifies one ordinary action's valid outcomes as a continuing combat, an initialized reward, or a closed defeat with no active combat/reward; the semantic pilot, Native policy baseline, and nested draw/target/generation/cost resumes use this shared result predicate. Draw insertion installs the exact engine-owned reward fallback for its resumed suffix, validates a completed reward/defeat without expecting an active `CombatState`, and clears the saved end-turn frame at that boundary.

`StochasticSiteRegistryV111.NatureChoiceSiteCandidates` owns the non-query SiteIds admitted by legacy random-choice forwarding. `LegacyFisherYatesSiteIds` is the narrower finite set accepted by the legacy shuffle wrapper: registered Native Fisher-Yates boundaries plus legacy-only shuffle sites. Typed requests at those sites require `bounded_integer`. `NatureChoiceSites` adds Nature-choice query and session coverage, including sites that do not use legacy index forwarding. The provider guard also admits the Chance Law query set and four internal Gaussian component subrequests; the inventory maps those subrequests to their parent Gaussian migration rows.

`SemanticDrawPileAdapterV111.InitializeFromCommittedOpening` centralizes transfer of an already-resolved physical opening pile into a Nature-mode deck. It owns the bottom-first physical to top-first semantic order mapping and preserves the committed permutation without creating a second shuffle or Nature request.

Nature draw recycling now uses one engine helper to derive Perfect Fit's known top identities from the current discard zone before the draw-pile adapter commits the recycle epoch. Ordinary draws, top-draw autoplay, and controlled hand-draw recycle keep their own suspension and continuation rules while sharing that mechanics rule and atomic pile/provider update.

Both immediately complete and player-resolved semantic opening-intent frames now pass through `CompleteNatureInitialCombatEntry`. It transfers the already-shuffled physical pile into a Nature deck before resuming the opening pipeline, preserving every committed position without a new shuffle or Nature draw. The entry publishes either a clean player boundary or an exact supported opening-pipeline choice; other suspended opening work remains fail-closed.

The specialized `SemanticCombatDrawV111.Simulation` pairs `SemanticCombatDrawMechanicsStateV111`'s run/count/stage checkpoint with `SemanticCombatDrawStochasticStateV111`'s random pile-card, generated-card, potion-generation, pool-shuffle, card-selection, card-transform, card-shortlist, random-hand-cost, hand-cost-selection, draw-insertion, Stratagem-selection, draw-hook, random-target and energy-cost transcripts. `SemanticCombatDrawV111.Validate` checks stage and deck correspondence while the stochastic owner validates typed history. Existing continuation properties remain flattened projections, and both owners fork with the frame. `SemanticEndTurnV111.Simulation` pairs its run and draw mechanics checkpoint with `SemanticEndTurnStochasticStateV111`; its live `HandDraw` facade projects the common end-turn run checkpoint and stores draw origin, counters and flags in the end-turn mechanics envelope and typed draw transcripts in `SemanticEndTurnStochasticStateV111`. A completed end-turn retains the prior draw-boundary run in the serialized `HandDraw` projection for replay while the common checkpoint holds the final run. `SemanticEndTurnV111.Validate` checks stage consistency and delegates transcript validation to the stochastic owner; end-turn restore replays that validated boundary chronology against live pending boundaries with one chronological nested-choice cursor and the engine’s common supported-choice predicate, while the stochastic owner validates per-kind indexes; full hand-draw transcript replay is shared with `SemanticCombatDrawEngineV111.Replay`. Newly begun, resumed and replayed hand-draw frames pass through `ContinueHandDraw`, which commits the run and facade together and performs the shared completion handoff at the draw boundary. `SemanticEndTurnEngineV111.CompleteAtBoundary` publishes the final run and completion flag; terminal automatic choices discard active intent/draw frames, while completed draw frames remain available for replay. The existing provider-neutral `PrototypeDrawCardsResumeResultV111` now carries the shared run and remaining-count progress for ordinary `DrawCards` resumes and automatic hand-draw stages, with an optional requested count when a hand-draw caller needs to publish stage progress. Typed decisions and SiteIds stay alongside that progress in choice results, and transcript history remains with its existing stochastic owners. `SemanticCombatDrawEngineV111.PublishDrawProgress` owns updates to the run and requested/remaining counts for selected card draws, insertion, random pile cards, costs, card selection, card transforms, draw-hook actions, target choices and Stratagem actions; each adapter retains its typed transcript. Automatic hand-draw results from semantic draw segments, nested semantic draws, drawn costs, draw hooks, random pile-card choices, targets, controlled and semantic insertions, generated-card choices, effect draws, and Stratagem selections pass through `ResumeSemanticAutomaticHandDrawAfterNestedChoice`, which classifies and validates the resulting cursor and request counts before publication. `AttachCompletedSemanticHandDrawReceipt` publishes the completed semantic draw receipt for both ordinary stage completion and the already-advanced resume path through one validated pipeline boundary. The source-route initial-intent frame is stored at the automatic pipeline's `InitialEnemyIntents` opening cursor; semantic pilot's existing `PendingInitialIntents` field remains a compatibility projection, and its getter selects the frame through the mechanics owner's validated opening-boundary query. Deserialization restores that compatibility field into the pipeline. Next-turn intent frames from `SemanticEndTurnV111` and `ControlledPlayableCombatV111` are stored in the same pipeline stochastic owner at the ordinary `AdvanceTurn` cursor. `ControlledTurnIntentsV111.Validate` replays its saved prefix against current enemy eligibility, and the pipeline owner validates both supported intent boundaries against the suspended mechanics checkpoint. `ResumeControlledTurnAfterIntents` now reads and validates that saved frame itself; semantic end-turn and controlled-playable facades pass only the run checkpoint, while semantic end-turn retains its stop-before-hand-draw policy. Semantic pilot, end-turn, draw, controlled playable, run-action, event-effect, event-reward, and automatic enemy-action frames use `PrototypeMechanicsContinuationStateV111` as their common run-checkpoint owner, preserving flattened compatibility fields. The automatic-pipeline stochastic owner requires the initial-intent payload at the source-route opening stage, requires every active hand-draw progress mode at `DrawPlayerHand` with no overlapping enemy-action or intent frame, and validates the semantic remaining/requested draw counters and the BeforeHandDraw marker against the saved combat checkpoint; the draw frame retains typed boundary transcripts and flattened snapshot projections. Controlled choice adapters and semantic end-turn hand-draw completion enter `PrototypeGameEngine.ResumeAutomaticPipelineContinuation` for the opening or ordinary pipeline handoff and terminal reward boundary. The entry point always returns `PrototypeAutomaticPipelineResumeResultV111` with the engine-classified boundary; adapters that need only mechanics project `Run` from that result. The broader common resume flow and cross-provider equivalence remain open.

The draw stochastic owner also includes card-shortlist choices in its typed
transcripts, mixed boundary chronology, and provenance replay. End-turn and
pilot facades route a shortlist opened by an active or pre-draw hand-draw hook
through that owner; pipeline-owned shortlists keep their end-turn transcript.

It also records randomized hand-cost values and selected hand-card identities
opened by active or pre-draw hand-draw hooks in the draw-owned chronology and
replays them against their live supports. End-turn and pilot facades route both
choice kinds through the active draw frame; pipeline-owned choices retain the
end-turn transcript.

`CommitAutomaticHandDrawProgress`, `CompleteAutomaticHandDrawStage`,
`AttachCompletedSemanticHandDrawReceipt`, and shared post-draw power timing
are owned by `PrototypeAutomaticHandDrawResumeV111.cs`. Controlled and
semantic provider adapters keep their distinct boundary and transcript
policies while publishing progress through these common mechanics operations.

`PrototypeDrawCardsResumeResultV111.EnsurePipelineBoundary` now owns completion of transient draw-progress classification: it preserves a supplied boundary and otherwise classifies the result run. Semantic draw, draw-hook, Stratagem and random pile-card adapters share this owner; no serialized field is added.

`SemanticChoiceResumeResultV111.EnsurePipelineBoundary` likewise owns conversion from a semantic choice handoff to the automatic-pipeline result used by end-turn publication, preserving its supplied classification or deriving one from the returned run.

`ResumeControlledEnemyAction` now returns the classified `PrototypeAutomaticPipelineResumeResultV111` from its existing pipeline handoff. Semantic end-turn and controlled-playable resume adapters preserve that boundary through transcript publication and staging; the shared selected-index executor returns its consumed `RandomIndexDecisionV111` beside the typed resume result. All controlled-playable indexed-choice adapters use this boundary-aware path.

Automatic semantic card-shortlist replay also carries the classified pipeline result through `SemanticChoiceResumeResultV111`. The shortlist mechanics owner and semantic-choice facade retain run-only projections for existing callers, while end-turn boundary publication receives the original classification.

Semantic random hand-cost choices also retain their classified automatic-pipeline result. Both indexed cost outcomes and selected physical hand-card outcomes pass the result through `SemanticChoiceResumeResultV111` to end-turn boundary publication.

Semantic card-effect draw costs now preserve the classified pipeline result through `PrototypeDrawCardsResumeResultV111` and `SemanticChoiceResumeResultV111`, including completed draw progress and remaining-count reporting.

Semantic random-target and hand-cost-selection facades also preserve their classified result through the shared semantic-choice resume owner, keeping source validation and boundary publication on one path.

Knowledge Demon semantic-choice replay now calls its typed engine action continuation and passes the classified automatic-pipeline result through `SemanticChoiceResumeResultV111`.

Automatic semantic card-selection replay uses the typed pending-choice resolver and carries its classified pipeline result beside the unchanged semantic selection frame. End-turn publication receives that result through `SemanticChoiceResumeResultV111`.

Typed semantic draw-insertion, generated-card, potion-generation, card-transform, and pool-shuffle resumes also return their classified pipeline results through `SemanticChoiceResumeResultV111`. Existing validators and run-only compatibility facades remain with their choice owners.

Semantic effect-draw resumes preserve their classified pipeline result through the validated draw-choice facade and `SemanticChoiceResumeResultV111`; hand-draw progress also carries that classification alongside its remaining count.

Controlled-playable draw-insertion resumes also retain the classified result when an insertion completes a nested draw or effect continuation. `ResumeControlledDrawInsertion` remains the run-only facade for semantic draw insertion, while the controlled-playable adapter uses `ResumeControlledDrawInsertionWithBoundary` to pass the existing engine classification into turn staging.

Controlled-playable generated-card resumes now preserve the pipeline result through the same selected-index executor and staging path. `ResumeControlledGeneratedCard` remains the run-only facade for semantic-choice consumers; `ResumeControlledGeneratedCardWithBoundary` returns the existing engine classification to controlled-playable.

Controlled-playable potion-generation resumes likewise preserve the classified automatic-pipeline result. `ResumeControlledPotionGeneration` remains the run-only semantic-choice facade, while `ResumeControlledPotionGenerationWithBoundary` carries the existing engine result through controlled-playable staging.

Controlled-playable pool-shuffle resumes now preserve the classified automatic-pipeline result through turn staging. `ResumeControlledPoolShuffle` remains the run-only semantic-choice facade, while `ResumeControlledPoolShuffleWithBoundary` carries the existing engine result through the shared selected-index executor.

Controlled-playable random-target resumes now preserve the classified automatic-pipeline result through turn staging. `ResumeControlledRandomTarget` remains the run-only semantic-choice facade, while `ResumeControlledRandomTargetWithBoundary` returns the engine classification to the shared selected-index executor.

Controlled-playable random-cost resumes now preserve the classified automatic-pipeline result through turn staging. `ResumeControlledRandomCost` remains the run-only facade for semantic-choice callers, while `ResumeControlledRandomCostWithBoundary` returns the engine classification through the shared selected-index executor.

Controlled-playable card-transform resumes now preserve the classified automatic-pipeline result through turn staging, including completion of the original card-selection continuation. `ResumeControlledCardTransform` remains the run-only semantic-choice facade, while `ResumeControlledCardTransformWithBoundary` returns the engine classification through the shared selected-index executor.

Controlled-playable draw-cost resumes retain the automatic-pipeline classification alongside transient draw progress. `ResumeControlledDrawCostWithBoundary` passes that result through the selected-index executor; run-only consumers keep the existing facade. This progress is ephemeral and does not change snapshot or transcript formats.

Automatic draw-cost resumption also carries its run and remaining count in `PrototypeDrawCardsResumeResultV111`; semantic hand-draw resumes retain the requested count while another cost stage is pending and through the resumed `DrawCards` boundary. The cost adapter retains its provider-specific suspension policy and transcript ownership.

Semantic automatic hand draws reached after a draw-hook action, Stratagem selection, a selected draw-pile identity, or after-shuffle insertion re-enter the source `DrawCards` kernel through `ResumeSemanticAutomaticHandDraw`. The helper owns the controlled guard and requires every caller to resume the same automatic-pipeline cursor and remaining count.

`SemanticDrawPileAdapterV111.ValidateSemanticHandDrawSourceDeck` and `IsSupportedSemanticHandDrawSourceDeck` own the shared source-deck admission rule for semantic end-turn and automatic hand-draw entry. They accept an idle Nature deck, or a providerless empty draw pile with a nonempty discard that can be recycled by source mechanics; end-turn validation and automatic draw entry use the same rule. Completed intent resolution now routes a reward or terminal result through `SemanticEndTurnEngineV111.CompleteAtBoundary`, which clears the active intent frame before publishing completion.

`PrototypeGameEngine.IsAtSupportedAutomaticPipelineTargetBoundary` owns the target-cursor admission rule, covering pre-AdvanceTurn combat work and the post-draw cursor from `PlayerTurnStarted` through late relic autoplay, including the final pipeline cursor. `SemanticTargetChoiceV111.ValidateBoundary` delegates that timing check to the engine. When the pipeline suspends on a controlled random target, semantic end-turn retains the completed draw frame and resumes the target through the same pipeline handoff. Target resolution uses the target owner's draw-pile/provider check for continuing combat results, while full end-turn restore replays the completed draw and automatic-pipeline prefix. It publishes completion only after the pipeline reaches the next player decision or a reward/terminal boundary. Other continuation families and cross-provider mechanical equivalence remain open.

`PrototypeGameEngine.HasPendingSupportedAutomaticPipelineChoice` provides the shared boundary-presence check for controlled enemy actions, random targets, source action prompts, and typed semantic choices. The individual choice owners retain their source-specific cursor and payload validation.

The automatic pipeline can suspend on supported nested semantic effect draws, draw costs, after-shuffle insertions, generated-card or combat-potion model selections, random hand-card cost outcomes, and fixed-cost hand-card targets under a saved autoplay card. The mechanics predicates admit these only at supported Mayhem or post-draw cursors, while each existing choice owner validates and resumes its own source operation. `SemanticEndTurnV111` records those typed outcomes in the same chronology as automatic target outcomes, and full restore replays that interleaving before accepting the saved frame. Other continuing combat results still pass through the unchanged draw-pile/provider invariant; unrelated pipeline-owned choices remain open.

An autoplay card can also suspend on a source `select_cards` prompt. The semantic card-selection owner validates the ordered action support and hidden-pile effect, while end turn records the selected action in the same chronology and replays it before resuming the pipeline. Admission covers saved autoplay card-play frames at supported Mayhem or post-draw cursors, plus the `DiscardAfterPlayerTurnStart` prompt immediately after `PlayerTurnStarted`; that prompt must match the live hand, discard destination, and forced selection count. This source action consumes no RNG, and Native trigger-frequency and mechanics parity remain open.

Tools of the Trade can suspend inside the `PlayerTurnStarted` subscriber dispatch before the post-dispatch policy runs. The semantic card-selection boundary now admits only that power's exact saved trigger, stack count, hand support, and dispatch cursor; it records and replays the selected discard before resuming the subscriber list and then the post-dispatch policy. The action consumes no RNG. Entropy's turn-start transform prompt is also supported at this dispatch: the card-selection and per-card transform decisions enter the end-turn chronology in order, and full restore replays both before resuming the subscriber list. Admission checks the exact power trigger and hand support, and rejects the prompt if any offered card lacks a supported transform pool. Toasty Mittens is supported at the opening and turn-two-or-later dispatch boundaries; its one-card exhaust action and queued Strength effect are validated and replayed in source order. Other trigger prompts and Native trigger-frequency/mechanics parity remain open.

`SemanticDrawPileAdapterV111.ResolveTopCard` is the shared provider-to-physical identity boundary for ordinary `DrawCards` and `AutoPlayTopDrawCards` under Native, Nature, and legacy providers. It verifies draw-pile membership and Native top order, then returns the provider consumption and matching remaining physical pile together; legacy mode resolves the mechanics pile's top identity, while an unresolved Nature reveal returns the pending request with unchanged membership. `CommitTopCardResolutions` replays the saved top-card sequence from its synchronized source checkpoint and publishes the provider result with the matching physical draw-pile and destination-zone changes. Ordinary draws and top autoplay use this paired commit. Ordinary `DrawCards` reloads its working pile lists from the committed zone owner after each `CardDrawn` hook and validates provider correspondence before continuing, so hook-driven zone changes are visible to the next draw. Top autoplay commits its staged play identities before recycling, so they remain represented in the zone owner across that transition. Specialized semantic draw resolution uses `ApplyCommittedPhysicalMutation` to publish its already-selected provider deck after physical correspondence validates. Generated draw insertion instead commits the selected provider position with `ApplyDrawPileInsertion` inside the guarded engine continuation; the semantic pilot validates the resumed boundary without replaying that provider write. Named continuation policies now make each caller's supported `DrawCards` boundaries explicit. `ResolveOperations` captures the source context and remaining operation suffix for resumable `DrawCards` and hand-return shuffle effects through one owner, including the saved draw-resume operation where needed. Legacy and semantic effect draws share `AccumulateDrawnCardInstanceIds` for the deferred card prefix and `ApplyDrawnCardKeywordOverride` for its eventual keyword effect. Callers still interpret draw boundaries and retain provider-specific hook policies; broader resume consolidation and cross-provider equivalence remain open.

`SemanticDrawChoiceV111` owns stable identity or ordered-index selection and commit of a deferred draw-card choice for both standalone semantic draw sessions and nested card-effect draws. It uses `SemanticDeckEventV111.Validate` for the common typed-event envelope and delegates live-request revalidation, provider resolution and physical-pile publication to `SemanticDrawPileAdapterV111.ResolveNatureDeckEvent`. It resumes nested card-effect draws through the ordinary engine under the caller's declared engine-owned reward policy. It validates the supported post-resume boundary through the semantic pilot's shared choice dispatcher and verifies that the provider transcript records the exact event and selected index in standalone draws and the pilot's direct and end-turn draw paths; callers retain their own subsequent draw, hook, and effect continuation policies. Nested effect-draw costs use the same post-resume dispatcher while retaining their own cost transcript and caller reward policy. `SemanticRandomPileCardEngineV111` owns stable-identity selection and pending-decision validation for the Jewelled Mask random pile-card event; the semantic draw owner resumes its outer continuation. `SemanticDeckRevealChoiceV111` owns stable-identity selection and delegates live-request revalidation plus atomic provider/physical-state commit to the same draw-pile adapter, with the pilot retaining only boundary freshness and decision history.

`SemanticDrawInsertionChoiceV111` owns pending insertion request and event-envelope construction, selection validation, and guarded resumption of the ordinary draw-insertion continuation for both semantic draw sessions and the semantic pilot. Each facade supplies its own owner prefix, ordinal, and pile-identity policy, and retains its different insertion-stage audits and transcript histories. `SemanticGenerationChoiceV111` similarly owns ordered card-model selection and guarded engine resumption for direct generation and the card-model stage of random draw-Attack insertion; the pilot retains its event history and reward audit. `SemanticPotionGenerationChoiceV111` owns ordered potion-model selection and the guarded ordinary-engine resume, including the invariant that potion generation preserves the provider-owned draw pile; the pilot retains its event history. `SemanticCardTransformChoiceV111` owns replacement-card selection, source event construction, live source-pool revalidation, and guarded continuation through the ordinary transform mechanics; the pilot and ControlledPlayable projection share its site identity, and its output may replace physical identities in any combat card zone. `SemanticCardShortlistChoiceV111` owns source event construction and site/operation mapping, ordered without-replacement selection and resumption for shortlist, random replay-card, and random autoplay operations, including their distinct phase and hidden-deck invariants. `SemanticHandCostSelectionChoiceV111` owns hand-cost event construction and site identity, stable hand-card selection, live eligibility and applied-cost revalidation, and guarded resumption of the random-cost effect. `SemanticRandomHandCostChoiceV111` describes each source-randomized hand-card cost event and resumes that controlled random-cost operation. `SemanticCostChoiceV111` constructs cost-event envelopes from caller-selected typed requests, describes nested effect-draw cost events from the live mechanics checkpoint, validates cost-event freshness, maps a selected cost to its typed decision, and checks that decision against the current event; it also owns the guarded resume and result-boundary checks for nested effect-draw costs under the caller's reward policy. Semantic draw and random-hand continuation owners retain their distinct count, hook, and pipeline policies. `SemanticPoolShuffleChoiceV111` owns request and event construction, bounded swap selection, site identity, live ordered-pool and source-operation validation, and guarded continuation with its phase and deck invariants. Target, generated-card, potion-generation, card-transform, draw-insertion, pool-shuffle, hand-cost, card-shortlist, effect-draw, and effect-draw-cost source-boundary validation now lives with the corresponding choice owner; the pilot's semantic-combat validator routes to those owners.

Card transformation commits the replacement zone identity, combat-card record, next instance ID, draw-provider constraint, and transformed persistent-card provenance in the same adapter transition. Temporary-card transforms preserve the established empty provenance projection when no persistent identity is involved.

`SemanticDrawPileAdapterV111.CommitZoneTransition` validates that every physical zone identity resolves to one registered combat-card record and that `NextCardInstanceId` remains fresh before publishing the transition. Cards held by a suspended choice may remain registered outside zones under their continuation owner; the run validator checks that full representation boundary.

For suspended initial or next-turn intents, `ControlledTurnIntentsV111.Validate` replays the resolved prefix from `BaselineCombat`; the automatic-pipeline owner checks its live checkpoint against the resulting `Combat` state. `SemanticIntentChoiceV111` constructs the weighted intent event, maps a stable intent ID through its support to the source ticket, validates the selected ticket against the live event, and owns a deep-fork of that typed decision. This preserves already committed enemy intent mechanics while the next source ticket is pending. The opt-in Native `CommitIntentsWithAudit` path accepts either registered initial or next-turn intent site, attaches the same typed support to each actual weighted draw, and records both the selected branch and exact sampled ticket in `SemanticIntentOutcomeV111`. `SemanticIntentAuditEntryV111` pairs that semantic result with the exact `RandomIndexDecisionV111`, including its Native cursor span, so provider state and branch meaning can be audited together. Audited Native intent commits fail closed if a sampled ticket cannot be paired with a weighted outcome; newly created Native-policy audits carry a schema marker that makes restore validation require one annotation for every intent-site decision, including when the annotation array is empty. Unmarked older snapshots remain readable. These audit outcomes make no probability claim beyond the declared source ticket intervals.

`ResolveRandomEnemyTarget` retains Native random-target outcomes through `SemanticTargetNativeAuditV111`. The audit reuses the existing semantic target event and decision to pair the exact ordered enemy-instance support and selected identity with the matching provider request and cursor span. The Native-policy schema requires one result annotation for every registered random-target decision in its mixed trace, including an empty set; older unmarked snapshots remain readable. This records the result returned by the shared resolver without establishing source eligibility, stream, seed, or cursor parity.

The in-combat `AcquireRandomCombatPotion` resolver similarly retains the
selected model and ordered potion pool through
`SemanticPotionGenerationNativeAuditV111`, reusing the semantic potion event
and decision. Its additive Native-policy schema covers the registered combat
potion model site completely in setup and player-action traces. Pre-combat
PotionFactory rarity/model requests use separate sites and remain under their
existing audit owner.

Choices nested inside the automatic semantic hand-draw stage and card-effect draw suffixes now return to their saved draw/effect continuation before terminal reward settlement. `ResumeAutomaticPipelineContinuation` advances or settles only at a clean boundary with no pending combat choice, Knowledge Demon choice, semantic deck reveal, or active pipeline hand-draw, enemy-action, or intent-provider payload.

`PrototypeAutomaticHandDrawProgressV111` is the typed stochastic checkpoint for one active `DrawPlayerHand` suspension. Boundary adapters create validated controlled-draw, draw-cost, semantic-draw, pre-draw-hook, or recycle-Stratagem progress values, then replace the checkpoint through `WithHandDrawProgress`; the flattened counters remain snapshot projections. This makes each saved hand-draw boundary one discriminated state transition instead of a sequence of partially overlapping counter writes.

`SimulationMechanicsProjectionEngineV111.Compare` reports a bounded set of JSON-pointer differences alongside both mechanics fingerprints. JSONL exposes it as `mechanics_compare`; each side accepts either `<side>_snapshot` or `<side>_semantic_handle`, with an optional `maximum_differences`. `hello` advertises the comparison schema, operation, and accepted inputs. The v17 projection retains normalized active automatic hand-draw remaining-count progress, suspended intent-pass progress through the next enemy cursor, and suspended enemy-action mechanics progress through its normalized operation and completed-choice count. It also retains pending run-action identity and action payload, post-reward event-action identity and decision-prefix length, and event-effect progress through its effect cursor, decision start, queued consequences, deferred reward, and accumulated losses. Semantic draw continuation is normalized to retain remaining count, source boundary, event depth, deferred keyword mechanics, Stratagem stage, and any player-selected draw-equal count, while omitting standalone/automatic provider segment flags. Saved pending-choice operations and recursively nested choice/event continuations receive the same normalization: Native generated-choice RNG policy and semantic draw/Stratagem routing flags are removed while queued operations, hook effects, and mechanics continuation order remain. It also retains pending draw-cost progress (card identity, remaining count, and cost stage), draw-insertion mechanics progress (operation, stage, selected model, after-shuffle relic sequence, and Personal Hive damage continuation), pending full-pool-shuffle progress (operation, completed swap count, and selected count), the active mechanics cursor for pending target, generated-card, potion-generation, card-transform, random-cost, and shortlist choices, plus the operation and occurrence ordinal for a pending random pile-card choice without its candidate identities or support fingerprint. The projection also includes the public draw-pile view after completed peek/scry reveals and the purpose/top offset of a pending semantic deck operation. It preserves draw-pile membership in a canonical order while removing the provider-owned physical order. Provider-specific hand-draw modes and automatic hand-draw requested-count metadata, candidate identities and supports, hidden deck constraints, draw-pile eligibility order, partial shuffle permutations, selected random outcomes, random cost bounds, and request metadata remain omitted. Equal projections compare committed mechanics, saved mechanics progress, and revealed player-visible deck knowledge; they do not establish native fidelity or identical stochastic choices. Callers must compare states reached after matching semantic outcomes.

`SemanticDrawPileAdapterV111.InitializeSemanticDrawPile` now attaches initial Nature or Native provider state to an existing physical pile only after membership and Native-order validation. `CommitNatureRecycle` commits the Nature recycle epoch and discard-to-draw-pile membership transfer together for ordinary draws, top autoplay, and a saved controlled recycle. Nature, Native, and legacy recycle helpers derive their exhausted draw and discard piles directly from the committed combat zone owner; callers cannot replace them with parallel local snapshots. Nature recycle validates the recycled identity set, Perfect Fit subset, and resulting physical membership before publishing the new state. `PrototypeGameEngine.CommitNativeSemanticRecycle` owns the Native stream shuffle and post-hook order for ordinary draws and top autoplay, while `SemanticDrawPileAdapterV111.CommitNativeRecycle` commits the provider epoch and physical zones. `SemanticDrawPileAdapterV111.CommitLegacyRecycle` now owns the discard-to-draw zone transfer for ordinary draws, top autoplay, controlled hand draws and controlled effect draws after their caller-owned shuffle and Perfect Fit hook. `SemanticDrawPileAdapterV111.CommitHandReturnShuffle` validates the returned identities against the committed hand, updates Native or Nature provider order, and commits the cleared hand and shuffled draw pile together; both ordinary and controlled hand-return paths use that operation. Caller-specific after-shuffle effects and controlled suspension policies remain in the engine, while `ApplyZoneTransition` and `ApplyDrawPileShuffle` publish mechanics membership and provider constraints together.

`CombatMechanicsStateV111.CardZones` owns Hand, DrawPile, DiscardPile, ExhaustPile, PlayPile, and ChoicePool membership in one `CombatCardZonesV111`. `CombatState`'s existing flattened fields project that owner, so its snapshot shape remains unchanged. `CombatCardZonesV111` copies arrays on construction and replacement, returns defensive copies, and deep-copies its backing arrays on `Fork`. Combat forking clones the zone owner once, the global membership validator enumerates that same owner, and generic `SetZone`, source-zone lookup, and pending-choice validation read or write through it. `SemanticDrawPileAdapterV111.ApplyZoneTransition` publishes identity-preserving zone changes only when physical DrawPile order is unchanged. `ApplyCardZoneMove` builds source and destination membership once and routes DrawPile insertion or removal through the matching provider-aware operation; the optional destination index preserves explicit ordering, including bottom-first DrawPile positions. `ApplyZoneIdentityDelta` requires explicit physical identities for intentional non-draw zone additions and removals, while `ApplyZoneIdentityReplacement` checks a replacement stays in the same zone and position. The generic provider-committed transition also requires global identity preservation; DrawPile removal can declare identities intentionally removed from combat, and fixture initialization remains a separate operation. Dedicated initialization, order-commit, insertion, removal, replacement, and shuffle methods update provider constraints and physical membership together. Engine card-zone mutations now use this boundary, including card moves, multi-zone selection, turn cleanup, hand return, generated-card placement, controlled draw outcomes, top autoplay, and Nature, Native, and legacy recycle commits. Ordinary legacy recycle now routes each swap through `LegacyFisherYatesV111` and the same descending shuffle mechanics core as Native; this preserves the PrototypeRng modulo stream sequence while recording each bounded-index choice through the typed legacy provider. Typed insertion and removal operations bind known identity changes to their physical index or preserved order, provider constraint update, and resulting zone owner; ordered insertion sequences validate their declared identities and removals while supporting multi-card top placement and preserving selected identities as publicly known provider constraints. The generic `SetZone` helper routes top insertions through that typed sequence operation and rejects other DrawPile edits that need a more specific transition. `ApplyDrawPileReplacement` binds a transformed identity to its physical slot and performs the provider substitution in the same commit. `ApplyDrawPileShuffle` validates a complete shuffle commit; the specialized Nature, Native, and legacy recycle operations derive their source identities from committed zones, and `CommitHandReturnShuffle` owns the paired hand-return provider and zone transition.

Controlled event-choice replay now uses `ControlledStochasticChanceReplayV111` for its saved-prefix cursor and exact request matching; event-owned ordinal normalization and candidate-label capture remain at that boundary. Run-action and automatic enemy-action frames keep their established index-prefix compatibility paths. Next-turn intent evaluation replays its full saved index prefix from `BaselineCombat` through `ControlledChanceContinuationExecutorV111`; both capture and selected-ticket resolution use the shared provider scope, which consumes the full prefix and validates the exact pending request. The intent frame retains its nullable pending-request envelope because completed frames keep a transcript without a pending request. `PrototypeControlledRunActionContinuationV111.Simulation` composes its partial run/action mechanics with `ControlledChanceContinuationStateV111`; persisted provider payload for event/run/enemy frames otherwise remains grouped by that shared chance state. These boundaries preserve their established flattened snapshot fields and rules: event replay normalizes prototype stream ordinals and attaches candidate labels, while combat replays check source requests against saved eligibility and the automatic pipeline.

`ControlledChanceContinuationExecutorV111` owns the replay-scope lifetime, exact saved-prefix consumption, capture of the next unresolved typed request, and label-to-index translation for scripted run-action, event-effect, event-reward, event-choice entry, event-reward advance, and automatic enemy-action mechanics. `ControlledStochasticChanceReplayV111` owns canonical request identity for all typed request classes and envelopes, plus null-aware ordered candidate-label equality; the executor's matching methods are facades over those rules. Request and label checks in controlled intent/initial-HP frames, configured ReferenceNature action sessions, controlled-playable replay, `ScriptedRandomBoundaryProviderV111`, semantic draw insertion, recycle and intent cursors, Native result annotations, and semantic end-turn replay use the same matchers rather than maintaining per-boundary comparators. These comparisons keep candidate identity separate from the stochastic request itself. `ScriptedRunActionSessionV111` is the mixed-primitive run-action continuation: its six-operation transcript and typed continuation survive validation, save, restore, and replay. The ordinary `PrototypeControlledRunActionContinuationV111` remains the index-choice adapter embedded in `RunState`; its action API exposes finite index selection, and its shared chance-state composition preserves the established flattened snapshot shape. The saved event-reward owner can replay legal Driftwood rerolls of event-generated card offers through the registered rarity-ticket and card-model sites, preserving the player action and selected prefix across snapshots. Automatic enemy-action replays its saved index prefix through the shared provider scope with the established site allowlist, then projects a pending request into the same saved pipeline frame. Its frame preserves optional candidate labels through the common chance-state owner; semantic end-turn exposes the labels for thief card identities, summon-pool entries, and other enemy-action outcomes, and accepts a label at the JSONL boundary while retaining index input for older clients. Restored prefixes validate saved labels when present; old unlabeled snapshots continue to validate their exact requests and indices. Next-turn intent evaluation also replays through its provider scope and exact-prefix handling while rebuilding the pure selector pass from `BaselineCombat`; `ResolveTypedSelectedIndex` validates and journals its selected ticket before replaying the complete saved prefix in the same executor call. The executor's `ResolveSelectedIndex` adapter wraps a boundary-owned logical index ticket in exact typed replay. Semantic draw costs, semantic end-turn automatic enemy-action selection, controlled-playable opening shuffle, draw and random-card costs, pool shuffle, insertion, enemy-action, generated-card, potion-generation, card-transform, target and recycle swap indices, controlled initial-enemy HP, and legacy committed-intent choices use it before their mechanics resume. Random-hand-cost target and value selections also share `SemanticRandomHandCostChoiceV111` for the guarded engine resume, source-boundary validation, and hidden draw-pile invariant. Their existing transcript modes and flattened snapshot fields remain the serialized compatibility projections. Automatic enemy-action and intent frames still have index-only saved continuations; typed provider outcomes require a matching typed controller boundary before those frames can accept them. Boundary owners still select checkpoints, enforce site allowlists, and persist their established continuation frames. `ReferenceNatureRunCursorV111` owns run-scoped cursor initialization, validation against the session transcript, and commit publication across semantic-combat, run-action, event-choice, Ancient-offer, Neow-offer, Trash Heap, Tablet of Truth, Trial, native event shuffle, Native event entry, Endless Conveyor, Glory Reflections, Infested Automaton, Punch Off fight-entry and rewards, native merchant, Treasure, and Shovel sessions. Boundary sessions retain their typed mechanics resolvers, while the sampler support check is shared by every ReferenceNature path.

`SemanticTargetChoiceV111` owns stable enemy-identity selection and the ordinary guarded target continuation, including the shared invariant that target resolution preserves draw-pile membership and semantic deck state. The shared engine continuation receives the selected enemy instance ID from semantic and controlled-playable choices; their typed provider decision retains the source-ordered index, which the engine derives from the validated live support only for mechanics that need that primitive position. Semantic pilot and end-turn facades keep their distinct fingerprints, transcripts, reward audits, and boundary progression while using the same target mechanics path. This is a partial migration toward semantic outcome identity; other continuation families remain open.

`SemanticEndTurnV111` exposes the next-turn weighted intent support as a semantic event and accepts the stable intent ID. The source selector still receives the first primitive ticket in that intent's weight interval; the end-turn stochastic owner stores both the semantic decision and its ticket, and restore checks the saved event support and mapping before replaying it. Existing snapshots that contain only the index transcript continue through the compatibility resolver.

Ordinary semantic generated-card continuations and the model-selection stage of draw insertion now consume the selected card model ID. The engine validates that ID against its live ordered source pool before applying it; controlled-playable index transcripts map to the ID at the adapter boundary. Draw-pile insertion positions remain bounded integer outcomes because the selected position is itself the mechanics result.

Semantic potion-generation and card-transform continuations also consume selected model IDs, with the ordinary engine validating each ID against the saved source support. Card-shortlist and automatic random-pile-card continuations consume selected card instance IDs against their live supports. Controlled-playable resolution now also accepts generated-card, potion, transform, and target identities; it requires one unique match in the live source support, then uses the existing indexed decision and transcript path. Its weighted intent choice also accepts the stable intent ID and maps it to the first legal ticket in that candidate's weight interval. Enemy-action candidate-label lookup is shared by semantic end-turn and controlled-playable through the typed continuation executor. Existing snapshots and decision records remain unchanged. These changes leave the typed primitive journal intact while extending semantic identity through more mechanics boundaries; other indexed continuations are still migration work.

Ordinary draw, controlled card-effect draw, and deck-reveal commits now pass the selected card instance ID through the draw-pile adapter into the semantic deck owner. The owner derives the ordered transcript index from the saved support, preserving existing transcript and snapshot shapes while keeping the continuation contract identity-based. Semantic hand-cost selection likewise passes its selected card instance ID into the shared random-cost engine path; the low-level controlled decision retains its index ticket.

Native-backed random hand-cost selection events now report
`run.combat_card_selection`, and per-card random energy-cost events report
`run.combat_energy_costs`; prototype-only runs keep `prototype.combat`.
Stream attribution preserves existing event IDs and saved decisions. Native
probability and cursor parity remain open.

`ControlledStochasticChanceReplayV111` now replays chronological prefixes across all six typed random primitives: index, unit float, ranged float, ranged double, Gaussian integer, and Gaussian double. `CaptureTyped` and `ResolveTyped` accept any mechanics checkpoint/result pair; their RunState wrappers return the established run continuation result, while map generation and Neow offer generation use the same exact-prefix executor without manufacturing a synthetic run checkpoint. The opt-in v2 envelope preserves all six request and outcome types, while v1 remains limited to index and unit-float fields. Request-filtered provider scopes evaluate the filter against all six request types, so the shared executor can replace explicit providers without changing request identity. Native merchant, Treasure, configured Native event-gold, map-generation, Neow-offer generation, Ancient-offer generation, standard Native event-shuffle sessions, Infested Automaton, Tablet of Truth, Trial, Trash Heap, Glory Reflections, Endless Conveyor index, unit-float, and mixed index/ticket replays, and Punch Off fight/reward replays use the typed continuation executor while retaining their boundary-owned snapshot fields and request policies. Native event-shuffle combat-end exact prefixes and dynamically discovered Native or legacy tails use the shared typed continuation executor with a filtered index fallback; War Hammer, Fake Merchant, Battleworn, and Repy use the shared executor while keeping their extra cursor and request audits. Endless Conveyor's unit-float and mixed index/ticket paths retain their pre-draw capture audits. Punch Off keeps a codec-aware index fallback for unselected requests and its CombatId, reward-cursor, event-cursor, and reward-identity checks. Ancient-offer generation now replays its full event/shuffle prefix through `CaptureStochastic`; a small scoped adapter routes the generators’ explicit provider arguments into the common replay, preserving their `scripted-v111` marker and saved session fields. The shared Native-event replay helper checks exact index-prefix consumption and preserves the Native event cursor for those event sessions. `FromOrderedIndexSelection` verifies ordered semantic outcome labels against their source index support, including card identities and named Trial cases. The shared replay no longer converts its result back to the older pending-exception shape. It supplies no Chance Law or site allowlist; broader session persistence migration remains open.

`ScriptedRunActionSessionV111` uses that mixed executor to control one ordinary legal action from its original `RunState` checkpoint. Its mechanics and typed stochastic prefix are grouped by `SimulationStateV111`, with suspended partial mechanics reconstructed through action replay. Scripted action and whole-run schemas are v2 to identify their six-primitive decision transcripts; restore accepts v1 snapshots and upgrades legacy index/unit-float continuation envelopes before replay validation. Completed `ControlledRunActionTraceV111` payloads carry an additive transcript schema ID: v1 identifies index-only traces and v2 identifies the typed decision union, while older snapshots infer the format from their optional typed payload. `ScriptedRunSessionV111` composes a shared origin/current run checkpoint with completed-action history and the active action; it chains action frames across ordinary legal player actions, retains the run at its last committed action boundary while a typed random outcome is pending, and replays completed action records from the original run when validating or restoring the session. JSONL and the standard-library Python binding expose begin/status/action-begin/resolve/fork/save/load/release and advertise the schema and operations. Scripted outcomes carry `UNVERIFIED` probability status and do not advance or validate Native RNG cursors; cross-provider mechanical-equivalence review remains open.

The existing index-only `ControlledChanceContinuationStateV111` is now a compatibility projection over `ControlledStochasticContinuationStateV111`. Its mechanics-facing index and candidate-label properties preserve the flattened continuation accessors, while the typed stochastic envelope owns the actual provider prefix and pending request. This lets existing controlled event, run-action, and enemy-action frames share the mixed replay state model without changing their snapshot fields. Scripted sessions use their v2 schema marker for six-primitive transcripts and migrate earlier v1 snapshots through the same mixed continuation owner.

Controlled run-action restoration and pending-frame source validation now pass saved per-decision candidate labels through the existing event/run-action replay owner. Each labeled prefix decision is matched against the live ordered support before its selected index is reused; this protects map entry, combat entry, Shop, reward, rest, and act-transition continuations from same-sized support reordering. Prefixes from older callers that have no label transcript retain their prior request/index matching.

`ResumeAutomaticHandDrawBoundary` routes completed `DrawPlayerHand` results to the existing semantic or legacy controlled hand-draw adapter, keeping provider-specific boundary and transcript policy out of the pipeline step. `ResumeSemanticAutomaticHandDrawStep` handles saved `BeforeHandDraw` action choices, random-pile hook choices, and initial semantic hand-draw entry; it interprets semantic suspension and deterministic source-ordered completion, while `CompleteAutomaticHandDrawStage` advances the stage cursor and keeps requested/remaining counts with the pipeline when mechanics suspend again. `PrototypeAutomaticPipelineStochasticStateV111.TryGetAutomaticHandDrawRemainder` defines which tagged progress modes can resume a saved card count, and the semantic-specific progress query builds on the same stage/count interpretation. `PrototypeAutomaticPipelineMechanicsStateV111.IsImmediatelyAfterStep`, `IsImmediatelyAfterDispatch`, and `AdvancePastStep` own cursor queries and advancement for ordinary steps and post-dispatch event continuations; mechanics validation also requires a saved post-dispatch event marker to match the dispatch immediately before its cursor and an active choice to suspend that dispatch. `AttachValidatedAutomaticPipelineContinuation` and `ClearAutomaticPipelineContinuation` are the common runtime publication and consumption paths for automatic-pipeline cursors. The attach path validates each composed continuation against candidate live combat; opening, intent, enemy-action, and hand-draw writers use it, while end-turn setup, resume, extra turns, and cursor-free intent/enemy-action checkpoint projections clear through the shared owner. `ClassifyAutomaticPipelineResumeBoundary` gives end-turn and controlled-playable facades one engine-owned result classification for supported choices, player decisions, reward/terminal results, and the next draw or intent stage while each facade retains its own transcript policy. `PrototypeAutomaticPipelineResumeResultV111` owns the ephemeral resumed-run and classified-boundary pair returned by `ResumeAutomaticPipelineContinuation`, `ResumeControlledTurnAfterIntents`, indexed controlled-choice replay, and semantic target or nested-choice resumes. These paths pass the already-classified result into facade staging without changing saved fields. Controlled-playable indexed-choice replay passes this shared result from `ResolveIndexAndRun` into next-turn staging. `SemanticChoiceResumeV111.ResumeTarget` and `ResumeAutomaticPipelineChoice` also return this shared result for end-turn staging; replay ownership remains shared while typed chronology stays in the facade. `BeginAutomaticPipelineIntents` now owns both supported entry sites: the semantic source-route opening requires a provider-free checkpoint, while end-turn and controlled-playable continuations require the clean ordinary `AdvanceTurn` cursor. The site ID and its distinct transcript policy remain explicit. Hand-draw and Mayhem continuation validation no longer repeat raw `NextStepIndex` arithmetic. `PublishDrawMechanicsState` is the shared atomic player/combat publisher for draw-result, active-progress, and completion transitions; `PublishDrawCardsResult` adapts a tagged result to it before boundary interpretation or continuation. `CreateSemanticDrawResultFrame` also provides the shared empty semantic frame for standalone hook-choice suspension. `ResumeAutomaticHandDrawCards` uses the stochastic owner query, clears the active progress value, and re-enters `DrawCards`; legacy and semantic adapters retain their suspension profiles and interpret the tagged result. Clean completion from the semantic automatic-hand-draw adapter, completion after its resumed `BeforeHandDraw` hook, and deferred completion now use the same `CompleteAutomaticHandDrawStage` owner as controlled draws; it validates and clears the saved cursor, applies post-stage timing, advances through the mechanics owner, and records the requested-count receipt before the semantic facade clears its compatibility projection. `PrototypeRuleset.MaxHandSize` is the shared capacity rule, with `HasHandCapacity` and `RemainingHandCapacity` used by draws, card movement, selection clamping, and Stratagem support; `HandSize` remains the initial draw target. `PrototypeGameEngine` also owns remaining-count interpretation across standalone draw operations, nested draw/cost/insertion continuations, and the active automatic pipeline. Its `ExpectedStratagemSelectionCount` rule owns selection operation construction, availability, and bounds across draw/recycle boundaries, saved automatic cursors, and semantic choice validation; `SemanticCombatDrawV111` delegates to that mechanics rule. The semantic draw and end-turn facades still choose which continuation to publish and own typed outcome history and their saved schema. Broader automatic draw-resume consolidation and cross-provider mechanical equivalence remain open.

`PrototypeGameEngine.GetCardsDrawnSince` validates the monotonic combat draw
count delta used by semantic automatic hand-draw completion and partial
semantic draw-resume interpretation. Both paths now derive requested or
remaining progress from the same saved card-count checkpoint.

Legacy controlled and semantic automatic hand-draw adapters return the same
`PrototypeDrawCardsResumeResultV111` to `ResumeAutomaticHandDrawBoundary`.
Suspended paths preserve the remaining count and completed paths report zero;
semantic paths additionally retain the requested count when the stage needs a
completion receipt. The router no longer reconstructs the legacy result from a
separate run value. Provider-specific suspension and transcript policies stay
with their adapters.

The shared automatic-pipeline classifier reports `AdvanceTurn` or
`DrawPlayerHand` only after `IsReadyForAutomaticPipelineResume` validates a
clean cursor with no active progress or unresolved choice or Nature request.
Supported choices keep their typed classification, while unknown pending
payloads remain suspended so controlled-playable turn-intent staging cannot
mistake them for a completed boundary. Semantic draw entry uses the same
classification to admit a clean hand-draw checkpoint, and end-turn origin
validation uses it for the clean `AdvanceTurn` checkpoint. Semantic end-turn
also sends the post-intent checkpoint through its shared boundary publisher,
preserving supported choices that suspend before `DrawPlayerHand`; it begins
hand draw only when that stage is the classified result. The initial end-turn
action uses the same rule for `AdvanceTurn`, and both paths reject unexpected
resume boundaries explicitly.

The ordinary pipeline resumer also consumes the tagged semantic
`BeforeHandDraw` continuation after a source action clears its pending choice.
Pool-shuffle adapters use the same handoff as other automatic choices instead
of selecting a provider-specific resume mode; semantic draw counts and
transcript publication remain with their existing owners.

Initial and post-cost semantic draws and nested effect draws now pass their `DrawCards` results through `ResolveControlledSemanticDrawResult`. It owns tagged-boundary dispatch, after-shuffle insertion, nested CardDrawn choice, Stratagem selection, draw/cost continuation attachment, deferred keyword application, and completion of the saved effect suffix. The ordinary automatic pipeline uses the same interpreter for semantic after-shuffle, hook-choice, Stratagem, semantic-draw, and draw-cost boundaries; `SaveAutomaticSemanticHandDrawBoundary` owns the initial requested/remaining cursor when a boundary first suspends. Initial legacy controlled draws now suspend at both `BeforeHandDraw` player choices and draw-cost, recycle, and `CardDrawn` hook-choice boundaries. The tagged `ControlledBeforeHandDrawHookChoice` progress value resumes the same pipeline stage after its event continuation settles, without replaying the hook; the tagged `ControlledDrawHookChoice` value preserves the remaining draw count while its hook choice and any nested choices resolve, then resumes DrawCards before applying hand-draw timing or advancing the pipeline. Resumed recycle, Stratagem, after-shuffle insertion, and draw-cost paths keep hook-choice suspension enabled. Nested legacy effect draws without a semantic deck also suspend on supported discard-pile shuffle and `CardDrawn` hook-choice boundaries, using the existing tagged result and outer choice continuation to resume the saved draw/effect suffix. Standalone semantic draw entry without a semantic provider remains fail-closed. Facades retain their selected outcome history and saved schema.

`CreateEffectContinuation`, `ResumeChoiceResolutionContinuation`, and its
outer/parent-frame composition helpers now live in
`PrototypeChoiceResolutionContinuationV111`. Effect execution supplies the
source context and remaining queue to this owner, which resumes pending discard
events, Sly autoplay, queued effect suffixes, event dispatch, card-play series,
and parent continuations after a nested choice; the combat resolver retains
ordinary effect execution.

Source-route `DrawOpeningHand` now sends semantic draw, draw-cost, nested
`CardDrawn`, and after-shuffle insertion boundaries through
`ResolveControlledSemanticDrawResult` with its opening draw frame. The final
card hook remains a direct pending hook because it has no draw remainder to
save. This keeps opening boundary attachment with the common draw dispatcher.

When a Nature-owned opening draw recycles into a live Stratagem selection,
the tagged `ShuffleSelection` boundary also uses that dispatcher. Its existing
semantic `select_cards` event is admitted only at the opening `AfterHandDraw`
cursor with the exact expected pick count, current draw-pile support, and one
positive saved hand-draw operation. Resolving the selection continues that
operation before the opening pipeline advances; other opening selection and
nested hook boundaries remain separately gated.

Nested semantic effect draws resumed after an after-shuffle insertion now keep `CardDrawn` hook-choice suspension enabled, so the next tagged result is interpreted by the same saved effect continuation.

`DrawCards` constructs every tagged result through `CreateDrawCardsResult`,
which validates the mechanical shape of the complete, recycle, cost,
semantic-choice, insertion, Stratagem, or nested CardDrawn boundary before
the result reaches an adapter. Automatic pipeline calls also require the
shared DrawPlayerHand cursor. Provider adapters retain their distinct
continuation and transcript policies.

`PrototypeDrawCardsResumeResultV111` is the shared handoff for a resumed
draw's run and remaining count, with requested count when an automatic
hand-draw caller needs to publish stage progress. It carries no decision transcript;
semantic and controlled adapters continue to own their provider-specific
choices and chronology. The value is transient and does not change snapshots.

`ResumeAutomaticHandDrawBoundary` now handles both first suspension at the
ordinary `DrawPlayerHand` source step and resumption of an already-saved
automatic draw segment. Source-step calls route only pending boundaries so
the pipeline can finish its ordinary stage work; saved-segment calls also
route completion through the same semantic or controlled continuation owner.
Both paths return the same provider-neutral draw-resume result.
`ResumeAutomaticHandDrawCost` owns cost replay and delegates its completed
result to this router.
Saved controlled hook, recycle-Stratagem, and insertion resumes also use the
same route before their legacy adapter handles any boundary outside its
shared automatic contract.
The source `DrawPlayerHand` stage now passes every result to
`ResumeAutomaticHandDrawSourceResult`, which owns that provider handoff,
ordinary Stratagem completion, deferred stage completion, and post-draw power
timing. A typed source-result context distinguishes ordinary pipeline work,
deferred semantic completion, and a resumed semantic `BeforeHandDraw` hook;
the pipeline step accepts only the first two and derives the third from its
saved progress marker. Deferred semantic work requires the controlled RNG
guard and an active semantic deck both before drawing and when its result is
published. A deferred result with a pending choice must be consumed by the
shared provider router; an escaped pending result fails closed rather than
advancing the stage or applying post-draw timing. The pipeline switch no
longer interprets the draw result boundary.

`DrawCards` now returns one tagged boundary value for completion, recycle, Stratagem selection, draw cost, semantic draw, after-shuffle insertion, or nested `CardDrawn` choice. Pending counts and after-shuffle insertion payload belong to that value, so conflicting continuation kinds cannot be represented by the result. Callers declare suspension support through named `PrototypeDrawCardsSuspensionPolicyV111` profiles for automatic hand draws, legacy continuations, semantic draws, and nested effects. Automatic hand draw, opening draw, semantic draw, and nested effect-draw adapters all dispatch through its shared discriminator; each adapter still applies its own saved mechanics frame and source hook policy.

Legacy automatic hand-draw cost, semantic hand-draw cost, and nested effect-draw cost now share `ResolveDrawCostAndDispatchCardDrawn` for validating the selected bound, applying the cost, advancing Relic to Slither when required, and dispatching `CardDrawn` exactly once. Automatic legacy and semantic cost resumes also share `ResumeAutomaticHandDrawAfterCost` for validating the active `DrawPlayerHand` cursor, provider progress kind, saved remaining count and cost instance before applying the outcome; it returns the validated semantic request count with the tagged result, then handles cost-stage suspension, post-cost hook boundary formation, and re-entry into the saved draw count. `ResumeAutomaticHandDrawCost` owns the post-cost automatic hand-draw dispatch for both providers, routing semantic results through the semantic draw interpreter and legacy results through the controlled hand-draw adapter while preserving each provider's saved frame and policy. Both providers publish automatic `CardDrawn` hook-choice progress through `SuspendAutomaticHandDrawAtCardDrawnChoice`, which applies the provider marker and tagged cursor as one validated checkpoint. Their automatic Stratagem selections now commit progress through `CommitAutomaticHandDrawStratagemSelection` after the ordinary draw-pile choice is formed; prompt construction and selection replay stay in the provider adapters. The initial and resumed automatic hand-draw paths also share post-stage power timing through `ApplyAutomaticHandDrawPostStagePowerTiming`; both provider adapters now commit their tagged progress and latest player/combat state through `CommitAutomaticHandDrawProgress`. Resumed legacy and semantic automatic segments use `ResumeAutomaticHandDrawCards` for saved-count validation, progress clearing, and the ordinary `DrawCards` entry. Legacy automatic draws suspend and resume nested hook choices through the tagged pipeline progress; semantic facades retain their distinct saved continuation and resume rules.

Ordinary `DrawCards` and resumed cost choices now share `DispatchCardDrawnAfterCost` to construct the event from the live physical card after cost resolution. The callers retain their provider-specific cost selection and hook-suspension policies.

Opening Nature hand draws now route Stratagem's `ShuffleSelection` boundary through the semantic draw-result owner and its saved draw continuation. Providerless and Native opening draws retain their operation-queue path.

`ResolveOperations` now delegates each `DrawCards` operation and `ReturnHandToDrawAndShuffle` boundary to the draw-continuation owner. It retains the source context and remaining operation suffix while routing legacy recycle, hook, cost, semantic, insertion, and Stratagem boundaries; when hand-return shuffle suspends on an after-shuffle insertion, it stops the operation loop so the saved suffix runs only after that choice resumes.

Opening and automatic source-stage hand draws now build the Stratagem choice plus remaining `DrawCards` suffix through `CreateHandDrawStratagemResumeOperations`, preserving one operation shape across both saved entry points.

`ContinueSemanticDrawCost` owns the shared post-cost continuation for standalone semantic hand draws and nested semantic effect draws: it resumes `CardDrawn` hook suspension, re-enters `DrawCards`, and routes the tagged result through the shared interpreter. Automatic semantic hand draws hand their tagged result to `ResumeAutomaticHandDrawCost`, alongside the legacy controlled resume. The standalone effect-draw cost adapter carries the cost owner's classified pipeline boundary on transient draw progress. Semantic standalone and effect draw callers retain their saved-frame and automatic-pipeline handoff policies.

`SemanticChoiceResumeV111.ResumeDrawHookChoice` now owns the post-action continuation for standalone and automatic semantic draw-hook choices. The `PrototypeGameEngine.ResumeSemanticDrawHookChoice` compatibility facade delegates to it. It resumes the saved `BeforeHandDraw` step, retains an active automatic draw cursor, or continues the saved draw count through the engine; `SemanticCombatDrawV111` retains the typed action transcript.

`SemanticChoiceResumeV111.ResumeStratagemSelection` now owns selection-event replay and the post-selection continuation for automatic or standalone semantic draws. The `PrototypeGameEngine.ResumeSemanticStratagemSelection` compatibility facade delegates to it. The semantic draw facade retains the typed selection history and calls the shared owner directly for resumed draw-count handling. The adapter carries the selection's classified automatic-pipeline boundary on transient draw progress; when it resumes more hand-draw work, it carries the typed downstream boundary or classifies the resulting run.

Semantic hand draws and nested effect draws also suspend after-shuffle insertion through `SuspendDrawCardsAfterShuffleInsertion`. That owner carries forward the remaining count, deferred keyword identities, Stratagem condition, and insertion suffix. Automatic Nature hand draws admit an after-shuffle relic insertion only when the saved semantic draw count matches the active `DrawPlayerHand` cursor; this lets the existing insertion owner resume Biiig Hug's Soot insertion after a recycle. Source-route opening draws use the same owner at the exact `AfterHandDraw` opening cursor.

Semantic draws and nested effect draws now route Stratagem's post-shuffle selection through `SuspendDrawCardsForStratagemSelection`. The shared owner validates the tagged boundary and eligibility, carries the remaining count, clears the pending-selection marker, and resumes the controlled choice. Nested effects retain their outer choice continuation and accumulate drawn identities; standalone and automatic semantic draws retain their own frame policy, with the automatic hand-draw caller alone updating pipeline progress.

Standalone semantic draws and nested effect draws now also route a suspended `CardDrawn` choice through `SuspendDrawCardsAtCardDrawnChoice`. It builds and attaches one draw-resume operation, carrying the effect's deferred keyword identities when present; standalone choices keep their marker and empty outer continuation policy. Automatic hand draws still resume through their pipeline cursor without adding a card-effect operation.

Initial nested effect draws in `ResolveOperations` now use the same
`AttachDrawCardsAtCardDrawnChoice` mechanics owner as resumed semantic effect
draws. It advances the remaining count and deferred physical-card identities,
prepends the saved draw operation to the effect suffix, and applies the
standalone hook marker at one boundary; the resolver still owns its queue and
RNG state, and semantic transcripts remain in their provider owner.

The state-only frame advancement and choice attachment helpers live in
`PrototypeDrawCardsContinuationV111`; run publication stays in the adapter.
That owner also builds the draw-resume prefix on an outer choice frame and
accumulates distinct deferred drawn-card identities; generic choice-parent
composition remains in the shared choice-continuation helper.
Initial effect draws also use `AdvanceControlledSemanticDrawFrame` to combine
newly drawn identities with the saved keyword prefix and Stratagem state before
creating semantic draw, cost, or after-shuffle choices.
`CreateAdvancedEffectDrawFrame` now supplies that transition for all
resumable effect-draw boundaries, including recycle, nested hook, and
post-shuffle Stratagem paths; normal completion uses the same identity prefix
when applying drawn-card keyword overrides. The continuation owner also
constructs each queued draw-resume operation from that validated frame; the
scalar operation factory no longer lives in the general combat resolver.
`CreateDrawResumeOperation(frame)` now projects every saved draw field through
one factory, including the draw-equal selection count, across effect recycle,
nested hook, cost, and Stratagem continuations.

The semantic draw continuation boundaries now use `AdvanceControlledSemanticDrawFrame` for remaining-count and deferred-identity progress. After-shuffle insertion, Stratagem selection, nested `CardDrawn` choice, and nested-effect completion therefore advance the same mechanics frame before applying their boundary-specific continuation policy.

When the resumed ordinary pipeline ends in Reward or Terminal after hand draw, `SemanticEndTurnV111` now completes with that engine result and keeps the completed draw projection for transcript replay. It still rejects an unfinished draw and checks the next-player boundary for combat-phase completion.

`PrototypeGameEngine.ResumeAutomaticPipelineContinuation` now owns the shared handoff after controlled choices and semantic end-turn hand draw. Its explicit guarded semantic pre-draw entry also resumes Hello World's admitted shuffle continuation through the saved `DrawPlayerHand` stage. The common owner uses `IsReadyForAutomaticPipelineResume` to validate the mechanics cursor and provider payload and to wait for unresolved deck, hand-draw, enemy-action, or intent outcomes before clearing the cursor. It then runs remaining mechanics and settles terminal combat rewards; the initial `PlayerSideStart` cursor has its own entry-boundary validation. `ResumeOpeningCombat` leaves cursor clearing and reward publication to this owner, and the established choice method remains a compatibility facade.

Controlled automatic enemy-action replay also enters this owner at its saved `ResolveEnemyActions` cursor. The handoff validates that the cursor-only run matches the typed replay checkpoint, then passes the saved decision prefix to the ordinary pipeline runner; enemy-action request capture and site validation remain with their existing typed continuation.

Ordinary end-turn and extra-player-turn pipeline starts now attach their existing start index through `SetAutomaticPipelineContinuation` and enter the same resume owner. Extra turns retain their `ResetPlayerBlock` start and skip the normal `AdvanceTurn` intent stage.

After a controlled `BeforeHandDraw` hook choice, the saved `DrawPlayerHand` stage still resumes with its provider marker so the hook is not replayed; its result then re-enters the common owner for any clean next cursor and terminal reward settlement.

Initial combat entry now publishes `PlayerSideStart` as an opening cursor and enters the same owner. Suspended opening choices use one cursor writer for their next opening stage, so initial execution and restored opening choices share the common handoff.

Controlled and semantic `DrawPlayerHand` continuations share `PrototypeGameEngine.CompleteAutomaticHandDrawStage` for after-hand power cleanup and pipeline-cursor advancement, including completion from an already-committed opening order, a resumed `BeforeHandDraw` hook, or a deterministic zero-card result, which completes without a Nature ticket or active draw-progress receipt. Clean guarded source-stage draws also enter this owner, so controlled and provider-managed completion use the same cleanup and cursor transition; unguarded ordinary draws retain normal pipeline advancement. Semantic end-turn now enters the same `ResumeAutomaticPipelineContinuation` used by controlled choices and draws, while its facade validates the next-player or terminal boundary. At a completed combat player boundary, the facade also validates the idle Nature deck and its physical draw-pile membership through `SemanticDrawPileAdapterV111`; terminal rewards remain on the terminal path. Provider-specific choice selection and transcript replay remain with their adapters; broader resume consolidation and cross-provider mechanical equivalence remain open.

Semantic end-turn enemy-action, random-target, and completed hand-draw paths now share one facade owner for terminal outcomes and supported pending pipeline choices. The boundary-choice handoff still handles source-specific Crossbow draw entry, ordinary hand-draw completion, and `AdvanceTurn` setup, and carries choice transcripts forward in chronological order. This keeps boundary progression consistent and prevents a later enemy-action resume from dropping earlier target decisions. The adapters still select choices and replay transcripts; complete resume consolidation and cross-provider mechanical equivalence remain open.

Curse of Knowledge uses the same handoff after its typed source action resolves. The saved two-card offer and selected legal action are validated against the original enemy-action frame, then the ordinary pipeline resumes. This action has no RNG request or Chance Law; source trigger frequency and effect parity remain outside the verified evidence.

For a live automatic hand draw, `PrototypeAutomaticPipelineStochasticStateV111` is now the sole owner of requested/remaining counts. `SemanticCombatDrawV111` and the end-turn/pilot facades project those counts from the pipeline; their flattened snapshot fields remain populated, while local fallback metadata is retained only for standalone or completed draw segments.

`PrototypeDrawCardsContinuationV111.cs` owns the provider-neutral `GetCardsDrawnSince`, `GetSemanticDrawRemainingCount`, standalone draw-operation lookup, and `ExpectedStratagemSelectionCount` mechanics. `PrototypeAutomaticHandDrawResumeV111.cs` owns the shared `AutomaticHandDrawStepIndex`; it is no longer named or defined by the semantic adapter. Automatic, semantic, controlled, and opening draw boundaries now use the same step, monotonic draw-count, saved-operation, and Stratagem-capacity rules.

The pipeline stochastic owner provides `TryGetActiveAutomaticHandDrawProgress` as the common count-bearing cursor query. It validates the saved `DrawPlayerHand` stage and returns the tagged progress mode with its counts; `TryGetAutomaticHandDrawRemainder` and `TryGetSemanticHandDrawProgress` project from that value. Automatic draw-cost validation reads the tag and count together once. `TryGetSemanticHandDrawProjection` exposes the zero-count `BeforeHandDraw` suspension to the semantic draw facade, while `IsAtSemanticBeforeHandDraw` provides an explicit stage-and-payload check to combat and semantic draw-hook validators, the Mayhem cursor guard, and semantic pre-draw resume paths. `IsAtControlledBeforeHandDraw` provides the corresponding checked query for the controlled hook resume, and `IsAtControlledDrawHookChoice` validates the automatic draw-hook handoff. Source-stage preparation and pipeline resume dispatch use these queries instead of reading the flattened hand-draw tag directly, keeping tagged-stage interpretation with the owner of the saved cursor.

Controlled-playable draw-cost validation and post-choice turn-resume classification, controlled draw insertion validation, and controlled hand-draw hook and recycle resumption also use `TryGetActiveAutomaticHandDrawProgress`. The active `DrawPlayerHand` cursor is validated with the mode before matching the saved remaining count or dispatching controlled recycle work. The semantic pre-draw stage check uses `IsAtSemanticBeforeHandDraw`. These consumers leave tagged-stage interpretation with the pipeline stochastic owner while retaining the serialized progress tag as its payload.

`HasSavedHandDrawProgress` owns payload-presence checks at clean boundaries; controlled and semantic draw completion, opening-cursor, generation, autoplay, and combat-boundary validators no longer inspect the tag directly. Mode and stage decisions continue through the validated owner queries above. The diagnostic mechanics projection reads the payload to publish its existing projected count.

`ResumeSemanticAutomaticHandDrawStep` owns classification of the semantic
stage's active draw, pending pre-draw choice, and completed zero or positive
draw outcomes. `BeginSemanticAutomaticHandDraw` returns that validated
progress directly, leaving the helper as the single stage-result interpreter.
Clean guarded source results that have no pending boundary also complete via
`CompleteAutomaticHandDrawStage`, rather than repeating its post-draw power
timing inline and leaving cursor advancement to the pipeline loop.
The initial semantic draw facade publishes its requested/remaining projection
from this result without reinterpreting `BeforeHandDraw` random-pile choices.
`ResumeAutomaticHandDrawBoundary` distinguishes initial source results, saved
controlled resumes, and saved semantic resumes with a typed context; only the
semantic context carries a semantic draw frame.
`PrepareAutomaticHandDrawStage` interprets the saved controlled or semantic
pre-draw hook tag, validates deferred semantic entry, clears that marker, and
selects the result context before the ordinary step switch executes hook or
draw mechanics. `ResumeAutomaticHandDrawHookContinuation` owns the matching
resume dispatch for controlled and semantic pre-draw hooks and controlled
draw-hook choices. The pipeline handoff delegates these cursor decisions to
that owner rather than reading the saved hook-progress tags.
The transient `PrototypeDrawCardsResumeResultV111` now validates its run and
nonnegative progress counts in its constructor, so every provider-neutral draw
resume result has the same invariant; its explicit validation also catches a
default value before publication. It retains the existing positional values
and deconstruction shape and adds no saved schema.
The pipeline stochastic validator checks the shared `DrawPlayerHand` stage
once for every active progress tag; mode-specific checks cover the corresponding
provider state and pending choice.

`SemanticCombatDrawMechanicsStateV111` owns the draw facade's fallback count projections and the state transition rules when its `Run`, requested/remaining counts, or automatic-draw mode changes. `SemanticCombatDrawV111` keeps the established flattened snapshot properties as delegates, and end-turn's saved draw projection asks the mechanics owner whether the pipeline owns its counts.

Semantic automatic-hand-draw entry, continuation, and cost handling now read tagged progress through `PrototypeAutomaticPipelineContinuationState.Stochastic`; draw-stage and opening-cursor checks read through its mechanics owner. Flattened progress members remain snapshot projections.

Automatic pipeline execution, validation, and semantic-pilot checkpoint projection now read intent, enemy-action, and hand-draw progress through `PrototypeAutomaticPipelineContinuationState.Stochastic`. Intent and enemy-action frames are published through that owner; intent resumption also clears its frame there. Flattened continuation members remain snapshot projections, and semantic-end-turn and controlled-playable facades keep their established adapter projections.

Automatic-pipeline execution and validation also read step index, opening stage, and post-dispatch event through `PrototypeAutomaticPipelineContinuationState.Mechanics`. This includes controlled enemy-action replay, hand-draw boundaries, and the stochastic owner's checks against the suspended cursor; flattened mechanics members remain snapshot projections.

`SemanticCombatDrawStochasticStateV111` owns typed random-pile-card, insertion, Stratagem, draw-hook, random-target, and cost transcripts, including validated append transitions. It copies decisions on construction and replacement and when a transcript projection is read, so callers cannot rewrite a suspended provider prefix through a shared array. Its versioned `BoundaryTranscript` records ordered references across those arrays and semantic deck choices. `SemanticCombatDrawReplayCursorV111` checks the saved cross-type order against the next live mechanics boundary while consuming each typed history; legacy snapshots without this chronology field remain readable and are upgraded by source replay. `SemanticChoiceResumeResultV111` carries the common run-and-site handoff for semantic choice resumes that have no extra progress fields; draw resumes with requested/remaining counts or typed decisions keep those boundary-specific fields. Random pile-card outcomes enter through `SemanticChoiceResumeV111`, which forks the checkpoint and typed decision before calling the existing automatic-hook mechanics owner; its transient draw progress carries the typed downstream boundary or classifies the resulting run. Ordinary semantic draw outcomes use the same resume owner to validate and commit the deck event, enter the real draw kernel, and verify the recorded physical identity; their transient progress also carries the typed automatic-pipeline boundary or classifies the resulting run. Nested card-effect draws retain their distinct controlled continuation. Draw-hook actions likewise use the shared typed resume owner for the legal-action replay, then apply their saved draw or pipeline continuation; a supported active hand-draw random-target child follows the same draw transcript and chronology. Cross-provider mechanical equivalence remains open. `SemanticEndTurnStochasticStateV111` applies the same boundary to intent, enemy-action, target, boundary, and nested-choice chronology, and validates and appends semantic intent annotations after the primitive intent ticket advances. The resolver also checks that each primitive intent resolution extends its prior prefix by exactly one decision before the owner accepts the controlled ticket.

`SemanticEndTurnStochasticStateV111` also appends enemy-action, target, and nested-choice decisions together with their ordered boundary references. New end-turn frames mark chronology version 1 and require one ordered boundary reference for every such decision, including when the transcript is empty. Versionless legacy snapshots retain their compatibility path; source replay fills the chronology and version marker before returning the restored frame. Completed boundary chronology carries into the next intent frame while the new frame owns its fresh intent transcript.

Automatic-pipeline `DrawPlayerHand` progress now has one tagged owner, `PrototypeAutomaticHandDrawProgressV111`, for controlled draw, controlled draw-hook choice, controlled `BeforeHandDraw` choice, draw-cost, semantic pre-draw, semantic draw, and recycle-Stratagem modes. `CommitAutomaticHandDrawProgress` validates the ordinary draw-stage checkpoint and publishes or clears the tagged value with the current player and combat mechanics for both providers, including when a resumed hook returns to `DrawCards`. The completed semantic requested-count receipt is published through its own provider-state transition after active progress clears, including when semantic resume receives a pipeline cursor that already advanced past DrawPlayerHand; its flattened continuation member keeps the established snapshot shape. Loading verifies that the receipt is at the post-draw cursor with an idle Nature deck and rejects combinations that claim multiple active modes. Pipeline hand-draw handoff, controlled hook and recycle resumption, semantic automatic resume, target-boundary eligibility, semantic hook and draw-insertion validation, Mayhem cursor admission, controlled-playable staging, and semantic draw-frame validation now read the tagged progress owner or its shared remainder queries; flattened counters remain snapshot projections. Nested semantic choices share `SemanticChoiceResumeV111` across semantic end-turn replay and pilot resolution; target and random pile-card resumes fork their typed decisions and mechanics checkpoint in that owner while retaining their existing specialized transcript and boundary chronology. The semantic draw frame now also saves one chronology across draw, cost, random-pile-card, insertion, Stratagem, draw-hook, and random-target choices, and validates it during full source replay; snapshots without the additive target transcript or chronology field replay through the existing typed-history compatibility paths. Runtime progress reads now resolve through the tag owner; cross-provider mechanical equivalence remains separate work.

Act 1 Neow offer generation now has a versioned Chance Law and an atomic `ReferenceNatureNeowOfferSessionV111` over the existing run-start state and offer generator. It conditions the positive-pool swaps on the selected curse, paired choices, and previous swaps, then replays the complete prefix through the existing scripted provider before committing the run and sampler cursor. Its bounded-index transforms are `SOURCE_VERIFIED` on their declared supports; default-pool completeness, unlock filtering, source call order, Native cursor parity, and broader S4 equivalence remain open.

Tablet of Truth now has a one-page ReferenceNatureTabletOfTruthSessionV111 for each eligible-card draw on pages 0 through 3. Its Chance Law preserves ordered physical-card identities and the session replays the ordinary event action before publishing the page mechanics and sampler cursor together. Configured Native bounded-index laws are `SOURCE_VERIFIED` on each declared support; Native-regional fallback uses the saved legacy `event` call count and equal-index `UNVERIFIED` laws. Fallback replay leaves the selected call unadvanced and creates no Native event cursor or provider marker. Page 4, lethal choices, and empty eligible pools remain draw-free; source filtering, Native cursor parity, and broader S4 equivalence remain open.

The Glory Trial acceptance draw now has a one-choice ReferenceNature session. It samples the current three-case law, replays the ordinary acceptance action, and commits the selected case page and sampler cursor. Configured Native replay preserves the Native event cursor; Native-regional fallback uses the saved legacy `event` call count, reports equal-index `UNVERIFIED` weights, and leaves the selected call unadvanced without creating a Native cursor or provider marker. The Native bounded-index transform is `SOURCE_VERIFIED`; later case actions retain the ordinary event flow, while source reward fidelity, Native cursor parity, and broader S4 equivalence remain open.

Native Fisher–Yates shuffles use `ReferenceNatureNativeEventShuffleSessionV111`; Doors of Light and Dark, Fragrant Mushroom, Tinker Time, Battleworn Dummy setting 2, Fake Merchant, War Historian Repy's chest offers, and War Hammer are admitted resolvers. Each conditional law includes its complete preceding choice prefix. Fragrant Mushroom is available only when its 15 HP cost is survivable; Tinker Time resolves the initial type offer and, after that Nature result, the chosen type's rider offer through the ordinary event action. Battleworn accepts a legal combat action only when it wins setting 2 immediately, routing other combat index requests through their existing stream providers and scripting only the event shuffle. Doors, Fragrant Mushroom, both Tinker stages, and Battleworn also admit Native-regional runs without `NativeRunRng`: their laws use equal-index `UNVERIFIED` weights, typed ordinals come from the saved legacy `event` call count plus the swap offset, and replay leaves the selected calls unadvanced without adding a Native event cursor or provider marker. Configured Native event shuffles preserve their Native event cursor. Fake Merchant accepts a legal map Event-node action only when ordinary weighted event selection resolves to Fake Merchant; that event-selection request retains its existing provider and only the six-of-nine stock shuffle is scripted. Repy accepts only its initial `unlock_chest` action in configured Native or Native-regional mode; it scripts the relic shuffle and both potion-index choices in one session, with the full relic-swap prefix conditioning each potion law. Configured Native bounded-index transforms are `SOURCE_VERIFIED` on their declared supports; Native-regional fallback laws are equal-index `UNVERIFIED`. The configured `player.rewards` cursor or legacy `reward` call count stays at entry, and the event RNG field remains absent. Source pool fidelity, stream attribution, seed frequencies, and seeded cursor parity remain unverified. War Hammer accepts one active copy and an action that immediately wins an ordinary Elite fight; it scripts the `run.niche` shuffle and preserves that stream at its captured pre-draw cursor, rejecting actions with subsequent draws from that stream. Reflections uses a separate `ReferenceNatureGloryReflectionsSessionV111` for its ordered downgrade and upgrade card-identity picks. It recomputes the projected deck between draws, replays the ordinary `touch_mirror` action after the final pick, then commits the mechanics and sampler cursor together. Configured Native replay preserves the Native event cursor; Native-regional fallback derives each typed request ordinal from the saved legacy `event` call count plus its prefix offset, uses equal-index `UNVERIFIED` laws, and leaves the Nature-selected calls unadvanced. Draw-free states remain outside the session. Other shuffles, Native seeded-result parity, source probability calibration, and broader S4 equivalence remain open.

The same saved native replay session also handles Fishing Rod's `run.niche` index choice during a legal ordinary-combat action that wins immediately. It recaptures later Rod supports after earlier upgrades, samples one exact bounded-index law per actual trigger, and commits the complete deck change and Nature cursor together while leaving the Native `run.niche` cursor at entry. The session intentionally excludes event combats, draw-free victories, legacy `event` stream runs, and victories with other unexplained `run.niche` calls; source trigger/support fidelity and Native cursor parity remain open.

That saved victory replay also handles Mad Science Improvement's legacy `combat` index requests when a legal combat action wins immediately. It samples one `UNVERIFIED` index law per actual pick, removes each selected card before rebuilding the next support, and commits the ordinary upgrade hook and Nature cursor together while leaving the legacy combat cursor at entry. It rejects victories with other unexplained `combat` draws; source pool completeness and native stream behavior remain open.

The same saved victory replay handles Pael's Tooth's legacy `reward` index requests when a legal combat action wins immediately and at least one nonempty Tooth queue is returned. It samples one `UNVERIFIED` law per non-melted Tooth in relic-slot order and commits the returned cards, saved-card removals, later ordinary reward mechanics, and Nature cursor together. A direct generic law also accepts the current saved-card support and source-shaped owner. Unrelated `reward` draws before or between Tooth returns are rejected; reward draws after the final return keep their legacy provider. The law and session do not establish Native stream ownership or source probability.

Pael's Legion acquisition uses the existing ReferenceNature run-action and event-choice sessions. A legal Shop `buy_relic` action or a supported event reward can suspend at its four-skin legacy index, sample an `UNVERIFIED` law, and commit the acquired relic with its saved skin. The emulator derives that choice from an isolated legacy bundle seeded by the reward-stream snapshot and relic slot; source seed derivation remains unverified.

`ReferenceNatureNativeEventGoldSessionV111` is the existing atomic Event-node entry replay for generated gold, Stone of All Time's potion-slot choice, and The Future of Potions' ordered card-type offers. Configured Native runs retain the exact Native event-context laws. Native-regional runs without `NativeRunRng` use the same session for supported legacy generated-gold, Stone potion-slot, and Future card-type index requests, preserving their saved `event` stream ordinals and applying equal-support `UNVERIFIED` laws. Event selection and other requests keep their existing providers, and unsupported branches fail closed. Sunken Treasury and Jungle Maze consume primary and secondary gold laws in order; Future of Potions consumes one law per owned potion in slot order; every other supported branch consumes one. The session commits the event-entry result with the Nature cursor after replay completes. Exact Native grid mappings are `SOURCE_VERIFIED` for the declared formulas; legacy weights, event-pool completeness, stream attribution, seed frequencies and Native cursor parity remain open. See the [Native event-entry session contract](REFERENCE_NATURE_NATIVE_EVENT_GOLD_D3.md).

`ReferenceNatureNativeEventRelicSessionV111` controls relic pulls from supported configured Native event choices, including direct `GainRandomRelic` effects and Toy Box Wax rewards, Round Tea Party's `continue_fight` reward, Trial's `merchant_guilty` reward, Neow Small Capsule and Large Capsule pickups, Neow's Bones relic offers, Hefty Tablet and Lead Paperweight card offers, Lost Coffer card and potion offers, Scroll Boxes card bundles, Arcane Scroll's Rare-card pickup, Phial Holster's two potion pickups, Punch Off's `nab` relic offer, Battleworn Dummy setting-three victory, and Pael's Wing's every-second-sacrifice reward. It replays the ordinary event or reward action, validates each sorted relic support against the canonical remaining bag and prior pulls, and commits the event, pickup, victory, or sacrifice with the Nature cursor after the complete typed prefix. Small Capsule and Punch Off's `nab` `OfferRandomRelic` requests use the existing `unowned_relic_selection` SiteId and validate their ordered unowned relic supports. Neow's Bones, Hefty Tablet, and Lead Paperweight pickup effects capture the full descending `run.random_pool_distinct.shuffle_swap_index` prefix over their reconstructed ordered pools on legacy `reward`; Lead Paperweight's Colorless pool uses configured epoch unlocks. Scroll Boxes captures its full Common then Uncommon descending shuffle through `event.reward.card_bundle.shuffle_swap_index`; Lost Coffer captures its Act-weighted card rarity tickets, card-model indices, and optional potion-model index through the same saved action. Arcane Scroll and Phial Holster capture their relic-acquisition model requests on Native `player.rewards`, replaying the Rare card pool and first-open-potion-slot owner from the picked-up relic and saved prefix. The Native bounded-index transforms are `SOURCE_VERIFIED` for those declared pools; pool fidelity and Native cursor parity remain open. These legacy `reward` equal-index laws remain `UNVERIFIED`. A rarity request may finish without an identity request when the bag has no eligible relic. Relic-bag rarity threshold transforms are `SOURCE_VERIFIED` on their declared requests; hidden queue identity probabilities, legacy shuffle weights, unregistered mixed stochastic effects, source-pool completeness, and Native cursor parity remain open. Legacy event rewards and choices with unsupported fixed relic or stochastic effects remain on their existing paths. See the [Native event relic contract](REFERENCE_NATURE_NATIVE_EVENT_RELIC_D3.md).

`ReferenceNatureNativeShovelSessionV111` controls the rarity and identity requests for one legal configured Native Rest `dig` action. It replays the ordinary action against the canonical unordered relic bag and commits the Reward with the Nature cursor after both requests resolve. The Native threshold and bounded-index transforms are `SOURCE_VERIFIED` on their declared requests; source-pool completeness, hidden queue identity probabilities, and Native cursor parity remain open. See the [Native Shovel contract](REFERENCE_NATURE_NATIVE_SHOVEL_D3.md).

Unconfigured legacy regional Unknown-node entry is also an atomic ReferenceNature run-action boundary when the run has no `NativeRunRng`. It reuses the saved typed 1000-way emulator ticket and ordinary room resolver, including the forced-Event draw and the evolving odds update. This equal ticket law approximates the pinned Native unit-float behavior and remains `UNVERIFIED`; configured Native streams and cursor parity remain outside the session.

Infested Automaton’s `study` and `touch_core` choices each expose a choice-conditioned card-index law and one-draw ReferenceNature session. Configured Native replay preserves the Native event cursor; Native-regional runs without `NativeRunRng` derive the typed request ordinal from the saved legacy `event` call count and use equal-index `UNVERIFIED` laws. The choice's semantic draw ordinal remains 0. Replay commits the ordinary event action and sampler cursor together, leaving the Nature-selected legacy request unadvanced. The configured Native bounded-index transform is `SOURCE_VERIFIED` on the declared support, which is the implemented Silent reward pool; source pool fidelity, seed frequencies, and seeded result/cursor parity remain open.

Sand Castle, Beautiful Bracelet, Distinguished Cape, and Fur Coat acquisition randomness reuses `ReferenceNatureControlledEventChoiceSessionV111` when the relic is selected through an event effect; none adds a duplicate relic-acquisition session. The generic event-effect continuation suspends at each descending Fisher–Yates swap or curse-index draw, labels the current support with physical card-instance IDs, map-node IDs, or the remaining curse IDs, and replays the remaining random effects before committing the event choice and sampler cursor together. Its saved controlled prefix now carries those ordered labels into replay and checks the live support before consuming each selected index, so a same-sized but reordered support cannot silently change the selected identity. Generic requests on canonical Native streams use exact `NextInt` grid masses for the bounded transform; legacy `event` requests retain equal-index `UNVERIFIED` laws. Fur Coat keeps its relic-owned Native content cursor absent in Nature mode so its selected map marks are not paired with a Native realization. Empty and singleton shuffle pools are draw-free and stay outside the saved Nature boundary. Source pool eligibility, Native stream ownership, and seeded parity remain unverified.

Endless Conveyor's weighted dish tickets have a read-only Chance Law query and an atomic `ReferenceNatureEndlessConveyorSessionV111` for the initial Event-node entry and each legal active-event action that reaches exactly one roll. The same session covers the first-dish Observe upgrade and ordered mixed prefixes for Spicy Snappy Grab upgrade-plus-ticket and Fried Eel Colorless-card-plus-ticket actions. Fried Eel and its replay/query paths use the same ordered implemented solo pool filtered by the configured Colorless epoch unlocks; a fully unlocked profile retains the full supported pool, while unknown partial-profile data fails closed. It also covers Suspicious Condiment's uniform potion model request during Grab, commits the queued potion with the Nature cursor, and leaves the deferred next-dish ticket for a separate potion-resolution action. That full-pool model law remains `UNVERIFIED`. Configured Native dish laws record pre-draw eligibility and exact rounded-grid masses for the source-shaped weighted ticket mapping; candidate-pool fidelity and stream attribution remain separate. Native-regional runs without `NativeRunRng` use the same atomic session with captured legacy bounded-ticket and index requests. Their equal-index laws remain `UNVERIFIED`; ordinary event selection and other non-session requests keep their existing providers. Card laws bind to the Native event cursor on configured runs, while fallback laws use the legacy `event` and `reward` streams. Replay scripts the complete typed prefix and commits event mechanics with the Nature cursor, preserving the Native event cursor when present and leaving Nature-selected legacy calls unadvanced. Forced fifth dishes and empty upgrade support are draw-free.

`ReferenceNatureChanceLawDecisionV111` maps sampled one-to-one index laws to typed index outcomes and maps configured merchant, Alchemical Coffer, and Treasure rarity laws to representative source floats, plus merchant rounded-price laws to floats that produce the selected integer price. It also maps Glory's `rest_quota` outcomes back to the generator's bounded index and rounded/truncated map Gaussian outcomes to typed Gaussian integers. The price mapper searches the source conversion functions used by ordinary Native shop generation. `ReferenceNatureMerchantSessionV111` replays the actual Shop-node map action and configured or Native-regional Shop purchase actions, including Alchemical Coffer and supported acquisition draws; `ReferenceNatureTreasureSessionV111` replays the actual Treasure-node map action. Configured Colorless Shop offers are validated against the same epoch-filtered rarity pool used by generation and Chance Laws, while unconfigured legacy states retain catalog-wide validation. `ReferenceNatureMapGenerationSessionV111` replays source-shaped Overgrowth, Hive, or Glory generation from its seed under a complete ordered typed prefix and exposes the map only after the full map and sampler cursor validate together. These sessions use the mixed typed provider and query conditional laws from their saved origin and sampled prefix. Both `ReferenceNatureChanceLawDecisionV111` conversion entry points share a validator for the selected law outcome and its typed request site, owner, and ordinal; the ordered-index entry point additionally checks the declared outcome order before conversion. The map session now uses the same generic capture/resolve executor as run-based sessions. See the [map-generation session contract](REFERENCE_NATURE_MAP_GENERATION_D3.md). Representatives preserve selected mechanic results. Exact grid weights are source-verified for bounded-index laws, configured merchant and Coffer potion-factory rarity laws, granular Treasure rarity laws, and supported merchant rounded-price mappings; compact Treasure identity mixtures and map Gaussian approximations remain `UNVERIFIED`.

The configured native merchant runs `StartShop` inside
`StochasticProviderScopeV111`. The scope routes default `NativePlayerRngV111`
calls and explicitly passed typed providers through one replaceable boundary.
`IRandomBoundaryProviderV111` composes index, unit-float, ranged-float,
ranged-double, Gaussian-integer, and Gaussian-double providers, and
`ScriptedRandomBoundaryProviderV111` can be installed as one mixed provider.
The Native Overgrowth and later-act map generators now route their explicit
index and Gaussian-integer providers through this scope. Existing event-choice
filters still apply to their versioned index/unit-float request types; an
unfiltered boundary scope covers every primitive. The scope tracks logical
per-stream ordinals for index/unit-float requests when a replacement provider
leaves the native cursor untouched. Local Native providers remain the default.
Source-side explicit index adapters in combat, Native monster HP and hatch
hooks, encounter formation, native event offers, and semantic pilot setup now
use the same resolver. Their current explicit providers remain the defaults;
an enclosing boundary can replace them. Replay validators retain direct Native
provider calls so they independently verify saved outcomes. Combat adapters
whose request ordinal comes from the Native cursor read the scope's logical
ordinal during counterfactual replay, so repeated scoped requests retain their
transcript order even when the provider leaves Native state unchanged.
The controlled replay executor, configured Shop entry, Native-policy player
actions, and every ordinary `PrototypeGameEngine.Step` install the shared
scope, which lets an enclosing controller replace providers across one
mechanics action. Proving cross-provider mechanical equivalence remains open.

The semantic pilot now resumes Distilled Chaos and Mayhem top-card autoplay
through the same saved semantic draw choice used by card-effect draws. It
preserves staged identities and the effect queue, then uses the ordinary
autoplay operation for each card. Mayhem's automatic-pipeline cursor resumes
after `AutoPrePlay`, including the History Course post-dispatch policy. A
discard recycle creates a deferred Nature shuffle and resumes through the same
top-card event; after-shuffle generated-card insertions use the existing
controlled insertion continuation before autoplay resumes. Semantic draws and
draw-cost choices reached from an autoplayed card keep that card-play frame
and the Mayhem pipeline cursor until their hooks finish. Nested semantic
card-effect draws reuse `DrawCards`' Nature recycle and after-shuffle
continuations. A legacy recycle without a semantic deck now saves each
conditioned discard-pile swap and resumes the same draw/effect suffix before
the Mayhem pipeline advances. This integration does not claim source-calibrated
draw probabilities.

Autoplay can also suspend on a source-validated combat-card transform. The
automatic-pipeline owner admits the transform only when its original selection
retains a supported Mayhem or post-draw autoplay frame. Each replacement stays
in the end-turn boundary chronology, including repeated transforms from one
multi-card selection, and snapshot restore replays the saved ordered pool and
selected model before continuing the autoplay suffix.

Source-validated random auto-play and replay-card shortlists reached by
autoplay also use that chronology. Each without-replacement pick retains the
ordered physical-card support and selected prefix; the ordinary shortlist
continuation resumes only after the saved prefix reaches its required count.
Restore checks each pick against the live remaining support before continuing
the autoplayed card.

These semantic shortlist-choice events use `run.combat_card_selection` when
the combat state has a Native RNG sidecar, matching the source stream used by
random shortlist, autoplay, and replay-card selection. Prototype-only runs
retain `prototype.combat`; previously saved prototype-labeled decisions remain
replayable because the stream label is outside the event identity. This
attribution does not establish source-pool completeness, probability parity,
or cursor parity.

Automatic source-pool shuffles reached by autoplay also retain each bounded
swap in the end-turn chronology. Restore checks the current permutation and
swap bound at every step before resuming the original operation; random
shortlist shuffles that lead to a separate player card selection stay on their
existing interactive path.
`ReferenceNatureMerchantSessionV111`,
`ReferenceNatureTreasureSessionV111`,
`ReferenceNatureCombatRewardSessionV111`,
`ReferenceNatureNativeEventGoldSessionV111`,
`ReferenceNatureNativeEventRelicSessionV111`, and
`ReferenceNatureNativeShovelSessionV111` provide serialized continuations
and use the shared law-to-source mapping. Their opening projections sort the existing
relic bag queues so Native's committed hidden queue order is not retained in
Nature snapshots. Merchant and Treasure sessions seed sorted eligible rarity
sets if the source run has no bag; merchant purchase replay consumes Toy Box's
five relic-bag pulls against the captured sorted residual support for both
configured Native purchases and Native-regional fallback purchases with no
Native RNG. The fallback path retains its 100-way legacy rarity tickets and
uses equal weights over each sorted residual identity support. The Native
event-gold entry replay preserves an absent bag and the Native event-relic,
Neow pickup, and Shovel sessions create a canonical bag when needed. Nature-owned sets
serialize an optional `NatureUnordered` tag in the v3 snapshot schema, while
Native bags omit the field and keep their current snapshot shape. The merchant,
Treasure, and combat reward sessions do not model Native cursor advancement;
the combat reward session replays registered reward requests from a legal
immediate winning action and commits the reward and Nature cursor only after
the full prefix completes. Its retained Prototype route controls a registered
subset of `reward`-stream index requests with equal-index `UNVERIFIED` laws.
Configured Overgrowth and Underdocks ReferenceNature run starts now create
canonical unordered relic rarity sets directly, omitting the Native queue
shuffle; ordinary Native run starts retain that queue construction.
Unregistered legacy reward hooks, ordinary Native relic-bag queue construction, and
other unregistered requests remain outside its coverage. The
merchant also handles the four mixed rarity-float and model-index picks from a
legal Alchemical Coffer purchase, plus registered Native `player.rewards`
bounded-index draws for relic and potion model selection on configured
purchases. Native-regional purchases use the same session's existing
registered purchase-index allowlist with its permitted legacy streams. Source
replay binds each request and its ordered support to the purchase prefix; only
the bounded transform is `SOURCE_VERIFIED`, while pool fidelity and stream
assignment remain separate. The same session now routes every remaining
registered Shop-purchase index site through the shared typed-index law after
checking the action mechanics fingerprint, request, ordered candidate labels,
and prior prefix. This gives the continuation a legal law for those indices
without claiming their source pools or stream assignments are correct.
The same merchant session now handles a Native-regional fallback Toy Box
purchase with no Native RNG: five retained rarity tickets are followed by
five identity choices from the sorted residual bag. The fallback ticket and
identity index weights remain `UNVERIFIED`. Other unregistered stochastic
purchase effects and continued relic-bag pulls remain outside those boundaries.
It also validates all three legacy weighted-potion offer pairs during fallback
Shop entry, and accepts a Native-regional Alchemical Coffer purchase with four
legacy rarity-ticket/model-index pairs. The Coffer and Shop fallback model
indices use uniform weights over source-ordered, rarity-conditioned residual
pools; those weights and legacy stream behavior remain `UNVERIFIED`. The
generic ReferenceNature run-action session wraps deterministic Shop actions
and a registered subset of index-only relic-acquisition hooks; it also
continues legal legacy Act 1 Overgrowth and Underdocks Combat/Elite map-node
actions through room entry while controlling their registered encounter-pool
and shared `combat`-stream indices, plus the two legacy `event`-stream
potion-factory indices used by pre-combat relic hooks.
Other requests retain their existing providers, and unregistered combat
requests fail closed. Deterministic actions consume no Nature sample, while the controlled
index laws remain `UNVERIFIED`. Ordinary queue-front/back pulls reject
Nature-owned sets unless a controlled Chance Law replay selects an identity.

`PrototypeNativeRelicGrabBag.PlanPull` is now the shared source fallback
resolver used by ordinary relic pulls and native Treasure laws. It retains
queue order for Native while exposing the selected fallback
tier and eligible identities separately. `PlanNaturePull` derives canonical
sorted support only from a `NatureUnordered` bag, and `PullNatureSelected`
revalidates that support before applying the selected removal. Event-relic,
Shovel, and combat-reward replay use those operations to rebuild later
supports from the earlier selected identities. The merchant,
Treasure, and combat reward sessions own their relic identity choices during
their respective room/reward boundaries; queue construction and later Treasure
draws after purchases remain outside those boundaries except for Toy Box's
five relic-bag draws. The merchant session also handles Alchemical Coffer's
four rarity/model pairs, Toy Box's five rarity/identity pairs on configured
and Native-regional fallback purchase routes, and registered index-only
Shop-purchase effects;
unregistered stochastic purchase effects remain outside its coverage. Nature
residual sets retain canonical identity support only; unsupported queue-order
pulls fail closed until their matching Chance Law continuation is implemented.

When a mixed controlled replay is active, merchant `Draw` now inserts the
Nature-only typed site `shop.merchant.relic_model.index_draw` before removing
the selected merchant relic. It labels the sorted support and applies the
chosen identity through the shared pull plan; the matching Chance Law query
conditions on previous same-shop relic selections without exposing Native
queue order. During Treasure replay, `Draw` exposes the Nature-only typed site
`treasure.relic_grab_bag.model.index_draw` after the rarity float, labels the
sorted fallback support, and applies the selected identity through the shared
Nature pull owner. The merchant and Treasure sessions consume their respective sites
as steps in their atomic room-entry continuations; the merchant purchase path
also consumes the same identity site for each of Toy Box's five wax relics.
The combat reward session consumes the same identity site after each
registered reward rarity request and prior pull. The Native Shovel session consumes the same typed rarity and
identity requests for one legal configured Native Rest `dig` action, validates
the selected support against the canonical bag, and commits the Reward with its
Nature cursor after replay completes.

`SemanticDrawPileAdapterV111.ApplyNewDrawPileInsertion` now publishes a newly created `CombatCardInstance`, its draw-pile membership, the next instance ID, and the provider constraint in one committed transition. `ApplyNewCardZoneInsertion` commits the corresponding card record, next identity, and membership for Hand, DiscardPile, PlayPile, ExhaustPile, and ChoicePool. Enemy, Personal Hive, generated-card, controlled insertion, Tea of Discourtesy, and generated-choice paths now use these owners. Draw-pile and non-draw zone deltas reject identities without registered card records. Transform replacement commits the old-card removal and replacement-card record with its zone and provider update; declared source-selection deletions also remove card records in the same transition. Existing-card moves now enter the same boundary, which validates the source, preserves the requested destination order, and synchronizes the provider for DrawPile changes. The tagged `DrawCards` result, boundary constructors, and mechanical result validation are co-located in `PrototypeDrawCardsBoundaryV111.cs`; provider adapters consume that validated value and retain their own resume policies. Broader draw-resume consolidation and cross-provider equivalence remain open.

## Semantic lifecycle and provider interchangeability

`ReferenceNatureRunActionSessionV111`, `ReferenceNatureMerchantSessionV111`,
`ReferenceNatureCombatRewardSessionV111`, and
`ReferenceNatureNativeEventRelicSessionV111`,
`ReferenceNatureTreasureSessionV111`, and
`ReferenceNatureNativeShovelSessionV111` compose their source/action/current run
checkpoint with stochastic state through `SimulationStateV111`. They reuse
`PrototypeMechanicsContinuationStateV111` for run identity and origin/current
validation. Merchant, combat-reward, Native event-relic, Treasure, and Native
Shovel sessions share the generic controlled-action stochastic-prefix owner;
the run-action session keeps its typed Native combat-entry continuation in its
specialized owner. Each session retains its typed law and replay validation.
Existing flattened session fields and snapshot schemas remain intact, as do the
run-action session's nested map/preparation/Ancient continuation owners.

`ReferenceNatureStochasticStateV111<TDecision>` owns the initial/current
sampler cursor and defensive typed transcript used by the run-action and
controlled-action stochastic owners. Native event-gold, Glory Reflections,
Neow-offer, Tablet of Truth, Trial, Trash Heap, Infested Automaton, Endless
Conveyor, Punch Off fight/reward, and Native event-shuffle sessions compose it
directly with `PrototypeMechanicsContinuationStateV111` under the
`ReferenceNatureRunSession` continuation kind; each retains its event-specific
law replay and run-cursor checks.
The base semantic `ReferenceNatureSessionV111` composes the
`SemanticPilotCombatV111` checkpoint with its independent sampler cursor
through `SimulationStateV111`, while retaining the established `Combat` and
`NatureState` snapshot members.
Map-generation and later-act preparation sessions now compose their
source-specific generator mechanics and stochastic prefix owners through
`SimulationStateV111`. Their mechanics owners hold generator inputs and
completed map/bag outputs; their stochastic owners combine the shared cursor
owner with typed provider prefixes and pending requests/laws. The completed
map audit and the run-initialization transcript reuse it for their cursor and
decision history;
the map fingerprint, Chance Laws, and nested preparation metadata keep their
existing owners. The Ancient-offer session and later-act assignment and
encounter-preparation results use it as well. The Ancient-offer session also
composes it with `SimulationStateV111` and the shared run mechanics checkpoint.
Underdocks and Overgrowth run results and native encounter-formation results
keep their mechanics, laws, map audit, and Native formation audit alongside the
shared cursor/transcript owner. The controlled event-choice session composes
the shared run mechanics checkpoint with a stochastic owner for its cursors
and ordered mixed player-action/Nature steps. Its Nature-decision transcript
is projected from those steps, so the chronology has one stored history.
Transient Overgrowth, Underdocks, encounter-formation, and later-act Nature
providers also use the shared cursor/transcript owner; `Advance` applies one
sampled cursor and appends its decision as a single owner transition. Run
initialization appends each validated segment through that owner, which checks
that the next segment begins at the current cursor before joining histories.
Flattened serialized properties remain stable; snapshot types continue to own
their explicit flat representations.

`ScriptedRunActionSessionV111` composes an action mechanics owner with a typed
stochastic-prefix owner through `SimulationStateV111`; while suspended, its
partial mechanics state is reconstructed from the origin and decisions, so
the owner retains the action and publishes a run only when the action
completes. `ScriptedRunSessionV111` composes the shared origin/current run
checkpoint with completed-action history and the active action. Both sessions
retain their flat snapshot members and JSON schemas; replay validation remains
with the session facades.

The **same game mechanics** issues semantic stochastic operations and applies their resolved outcomes regardless of provider:

1. **Commit / initialize** a source-triggered shuffle, offer, or intent decision. Native may immediately record a complete hidden result; Nature may record an unresolved object with constraints.
2. **Query / reveal** only when mechanics require a value. A committed value is deterministic and must not generate a second chance ticket. Nature may be asked for a value from an unresolved object; the choice must satisfy the constraint state.
3. **Consume / apply** the resulting physical identity or move through the ordinary engine; run its hooks **exactly once**, and continue to the next mechanical/chance/player boundary.
4. **Update / reset** the provider's ordering constraints in response to exact insertions, reveals, peeks/scry, removals, card creation, discard/recycle and source-time reshuffles.

The JSONL bridge keeps `semantic_observe` as the fair player projection and exposes Nature-only pending chance boundaries through the separately versioned `SemanticNatureChoiceViewV111` contract. The additive `fair_observe` operation uses `PrototypeAiFairObservationEnvelopeV111` to bind the existing public observation schema, legal actions, observation hash, and terminal flag across ordinary run, controlled-combat, semantic-combat, and semantic ReferenceNature handles; each view is projected by the same `PrototypeAiEnvironment`. Existing mode-specific endpoints remain available. The Python `SemanticReferenceCombat` wrapper uses `observe()` and `actions()` for that shared player frame and `observe_nature_choice()` for the separate pending-law view on the same handle. `nature_choice_observe` reuses the existing pending-law projections for semantic combat, ReferenceNature combat, run-action, event-choice, Ancient-offer, Neow-offer, Trash Heap, Tablet of Truth, Trial, native event shuffle, Native event entry, Endless Conveyor, Glory Reflections, Infested Automaton, Punch Off fight and reward, merchant, Treasure, combat reward, map generation, and controlled event/run-action sessions; `semantic_nature_observe` remains a semantic-combat alias. The v2 view binds event identity and event/law support fingerprints to the Chance Law conditioning state; it filters forbidden provider fields and current selected-result fields at the event envelope and recursively removes provider stream, seed, cursor, RNG-state, and call-count metadata plus named or structurally recognized raw run snapshots. Validation rejects any such metadata that remains. The capability manifest advertises the schema, operations, and accepted session kinds. The JSONL `hello` response builds its manifest from the same validated coverage object it publishes, and session-coverage validation requires every advertised session SiteId to appear in the Nature-choice capability union. The Python bridge validates the advertised manifest against session and query coverage at handshake, then checks direct query response SiteId and ordinal plus direct and nested Chance Law schemas, identities, probability status, outcome weights, support fingerprints, and law identifiers before exposing a law. The session-kind registry also drives JSONL's unsupported-kind classification, while per-kind dispatch remains explicit so an advertised kind without a handler fails closed.

The shared fair-observation envelope also advertises `semantic_native_policy` for baseline-enabled semantic handles at player boundaries. It reuses the common player projection and does not include native-policy audit state; the Python `SemanticCombat` wrapper selects this view when it detects the baseline provider.

A native Fisher–Yates shuffle and Nature's later `draw_card` events can express the **same underlying random permutation**. Never expose or likelihood-weight both for that single randomization. Source-native low-level primitive calls (including singleton draws) remain available for native parity instrumentation, independently of semantic Nature events. Preserve stable card **instance** identity and history-dependent eligibility.

## Chance Law and information conditioning

A chance-law query takes a **pre-outcome** semantic operation, legal support/weights from mechanics, and a declared information/constraint state. It returns a versioned conditional distribution `p` with ordered stable outcome IDs, exact/compact weights or probabilities, support/constraint fingerprints, conditioning policy and probability **provenance** (e.g., `UNVERIFIED` or source-verified).

Probability status belongs to each Chance Law. `UNVERIFIED` means its mapping or
sampling law has not been independently established. `SOURCE_VERIFIED` means
the pinned source's sampling transform has been audited mathematically under
the provenance's declared input model. Each `SOURCE_VERIFIED` law must either
account for the complete 2^53 Native input grid or encode one deterministic
outcome; the envelope validator enforces this normalization. It does not by
itself establish a seeded stream's frequency distribution, context-seed
derivation, or result and cursor parity; those remain separate fidelity
claims. Capability manifests advertise supported statuses, while a legacy
single-status field may remain a conservative default for compatibility.

For the pinned Native `NextInt(maxExclusive)`, the source maps a 53-bit
`NextDouble` grid by multiplication and truncation. Exact integer weights can
be computed from each grid point's preimage, including the binary64 rounding
boundary before truncation. Power-of-two bounds yield equal masses; other
bounds may be slightly uneven. Laws that use these exact source masses are
`SOURCE_VERIFIED` under the declared uniform-grid input model. Equal-weight
approximations at other bounds remain `UNVERIFIED`.

The v1 `ChanceLawV111` wire model stores an explicit outcome array and limits
that representation to 65,536 outcomes. Version 2 adds an opt-in compact
uniform-index-range descriptor for large bounded integer requests. The range
law fingerprints its descriptor, emits outcome IDs as needed, and lets
ReferenceNature sample the raw index without materializing an array. The
legacy map convergence key uses this representation for its `Int32.MaxValue`
bound; its selected raw key still enters the existing stable ordering logic.
Version 3 adds a compact Native bounded-index law for large canonical
`NextInt(bound)` supports. It fingerprints the bound and uniform 53-bit input
grid, samples one grid point, and applies the same multiply-and-truncate
transform; the law retains exact grid masses and `SOURCE_VERIFIED` status
without enumerating outcomes. It does not establish stream-seed derivation or
cursor parity. The v1 schema and fingerprints for existing explicit laws are
unchanged. The v4 stochastic capability manifest retains the primary v1
schema field and advertises all supported Chance Law schemas in
`chanceLawSchemaIds`; the Python binding accepts the prior v3 manifest and
validates all three law forms. The legacy uniform range remains `UNVERIFIED`
because it does not establish the legacy map stream's seed distribution or
map-generation parity.

The `combat.initial_unique_hp` direct query accepts `owner_id` in the form
`creature:<CombatId>`, the ordered HP support, and the source ordinal. It
returns the exact `run.niche` `NextInt` grid law over support indices and
binds the declared values and order into the request fingerprint. The caller
still owns the live HP support; pool construction and order, seed frequencies,
and end-to-end cursor parity remain separate evidence.

The `combat.tough_egg.hatchling_hp` direct query accepts the same canonical
creature owner, Ascension, and source ordinal zero. It derives the pinned
19–22 or 20–23 HP interval and returns exact `run.niche` `NextInt` grid
masses over support indices. This covers the in-place hatch draw only; event
eligibility, the saved creature boundary, seed frequencies, and cursor parity
remain separate evidence.

- Its result must be independent of the outcome eventually selected by Native, ReferenceNature or learned Nature.
- Native provider's already-committed hidden realization is **not** a Chance Law input for Nature's pre-revelation distribution. Conditioning on a complete private realization would instead produce a degenerate distribution; it answers a different question.
- For uniform unknown permutations, sequential sampling over remaining eligible physical instances can produce the correct joint law, subject to known positions, shared pools, forced insertions, observations and other constraints. Compute the conditional law of the remaining random object, not merely an arbitrary uniform distribution over listed candidates.
- `SemanticDeckEngineV111.EnumeratePossibleOrders` provides a bounded exact combinatorial enumerator for Nature-owned deck constraints. It permutes each remaining physical pool across all positions that reference it, including ranges split by known insertions. Equal completion weights are explicitly `UNVERIFIED`; committed Native order is rejected as input, and the enumerator does not calibrate a Chance Law.
- `SemanticDeckEngineV111.EnumeratePrefixDistribution` groups those exact completions by an ordered top-first physical-card prefix and returns integer completion counts. `AuditPrefixDistributionAgainstSequentialChanceLaws` traverses known positions and composes each unresolved draw's `QueryDraw` support, then checks that the exact joint counts match full-order enumeration up to Core's advertised audit limit (currently 64 draws). JSONL exposes the same bounded result through the opt-in `semantic_deck_prefix_distribution` analysis operation, which accepts a run handle with an idle Nature-owned combat deck and returns the versioned distribution. Its optional `audit_sequential` input requests the sequential check and reports `sequentialChanceLawAudit: "matched"` only after an exact match. `hello` advertises the numeric audit limit, and the Python binding enforces that negotiated value. The operation is Nature-only and is excluded from fair player observations. This validates the declared uniform-pool model independently of ReferenceNature's pseudorandom cursor; the assumptions remain `UNVERIFIED` and do not establish the original game's draw law.
- For enemy intents, reuse exactly the engine's current post-filter eligible moves and relative weights. Those weights' **original-game fidelity remains UNVERIFIED** until separately audited.
- For a saved controlled random-index continuation, the conditioning fingerprint binds the typed request and saved boundary. Candidate labels map indices to mechanics results; they do not independently define a probability distribution over those results. Requests on recognized Native `run.*` and `player.*` streams, and context streams created by `NativeRngV111Contexts`, use exact `NextInt(request.SupportCount)` 53-bit grid masses and report `SOURCE_VERIFIED` for that bounded transform. Legacy and unknown stream IDs retain the equal-index `UNVERIFIED` model. This does not establish source-pool completeness, stream assignment, seed frequencies, or cursor parity.
- Native encounter-formation index queries accept `native_encounter_entry`, `total_floor`, `support_count`, and `ordinal`. They bind one declared index to the encounter-local context stream reconstructed from the model entry and floor, then use exact `NextInt` grid masses with `SOURCE_VERIFIED` provenance. `NativeEncounterFormationFactoryV111.GenerateWithReferenceNature` injects those laws into the existing source-shaped factory and returns the formation, initial/final Nature cursors, laws, and replayable decisions as one validated result. The existing ReferenceNature run-action session suspends on these formation requests during configured experimental Native Act 1 combat entry, reuses the ordinary map-node mechanics replay, and commits the Nature cursor with the resulting combat. That same continuation samples the supported Overgrowth and Underdocks weak, normal, and elite encounter-pool indices from their current ordered supports; their equal-index laws remain `UNVERIFIED`. When a pre-combat potion relic has an open slot, the same run-action owner captures the Native PotionFactory rarity `NextFloat` and tier-model `NextInt` as one typed prefix on `run.combat_potion_generation`, binding the model support to its preceding rarity and map-node potion slot. Those laws verify only the declared transforms and conditioned pool, not source-pool completeness, stream attribution, seed frequencies, or cursor parity. Other combat-entry requests keep their current providers. The caller's source resolver still owns formation support and request order; neither the formation query nor factory derives the stream seed or validates earlier `GenerateMonsters` calls. The legacy caller-bound formation law remains `UNVERIFIED`.
- Fishing Rod's persistent combat-end upgrade has a direct query for the same exact `run.niche` bounded-index transform. The caller declares support size; the query does not validate trigger timing or pool completeness. The existing native victory replay also supports a configured immediate ordinary-combat win, scripts each triggered Rod index in order, and applies the ordinary relic hook before recapturing the next support.
- Mad Science Improvement uses the same saved victory replay for its current legacy `combat` index request. A direct caller-bound law accepts the declared support and owner when `stream_id` is `combat`; omitting `stream_id` retains the state-bound query that captures the request during a legal immediate victory. Both index laws remain `UNVERIFIED`, and the session scripts each without-replacement pick while the ordinary Improvement hook rebuilds support before the next one.
- Pael's Tooth uses that saved replay for each legacy `reward` index request during an immediate combat victory. The direct query and pending laws remain `UNVERIFIED`; unrelated reward draws before or between Tooth returns are rejected, and later reward-generation draws keep their legacy provider.
- Pael's Legion uses the existing shop-purchase and event-choice continuations for its four-skin acquisition request. Its dedicated direct query fixes the `reward` index support at four and requires the relic-instance owner at isolated ordinal zero; equal-index weights and the emulator's relic-owned seed derivation remain `UNVERIFIED`.
- Native combat-gold rewards have a read-only law over inclusive amount outcomes, conditioned on room, Ascension, and the declared gold proportion for normal Combat. It uses the exact Native bounded-index grid transform and is `SOURCE_VERIFIED`; the query does not validate the earlier reward prefix, stream assignment, or cursor parity.
- Configured combat potion presence has a read-only Native `NextFloat()` threshold law conditioned on the caller-declared persistent pity value, Elite bonus, and non-forced branch. It counts exact source-rounded 53-bit grid inputs and is `SOURCE_VERIFIED`; forced rewards bypass the draw, while reward-prefix validation, stream assignment, and cursor parity remain open.
- The legacy `reward.potion_odds.ticket` has a separate offered/not-offered law over its 1,000-way integer ticket. Callers declare current pity, Elite status, force-hook status, and Rewards ordinal; forced branches are rejected because they bypass the ticket. Legacy ticket weights remain `UNVERIFIED`; the registered combat reward session now consumes this and the other listed reward-stream index requests atomically.
- The legacy `event.unknown_room.odds.ticket` query exposes all 1,000 request indices for its caller-declared Event owner. It fixes the support and operation but does not map tickets to room outcomes or verify the source `NextFloat` odds; the legacy approximation remains `UNVERIFIED`.
- The retained legacy combat-potion drop ticket has a separate offered/not-offered law for Combat, Elite, or Boss reward context when the caller declares that potion acquisition is allowed. It binds act, floor, room, and Rewards ordinal; the equal two-ticket model remains `UNVERIFIED` and is consumed by the atomic combat reward session on retained legacy runs.
- The retained legacy `reward.random_potion.model` query returns the ordered prototype potion pool with equal index weights after callers declare a successful drop and available potion capacity. It binds act, floor, room, pool fingerprint, and Rewards ordinal; pool completeness and Native parity remain `UNVERIFIED`.
- The legacy `potion.uncommon_model.index_draw` query returns identities from the ordered implemented Uncommon-potion pool for a caller-declared `event` or `reward` stream, owner prefix, and ordinal. The query binds the exact pool fingerprint and leaves pool completeness and equal-index weighting `UNVERIFIED`.
- The legacy `potion.model_within_rarity.index_draw` query returns the source-ordered candidate identities after a caller declares rarity, exclusions, stream (`event`, `reward`, or `shop`), owner prefix, and ordinal. It derives the same owner suffix from the remaining-pool fingerprint; candidate completeness and equal-index weighting remain `UNVERIFIED`.
- The legacy `potion.uniform_model.index_draw` query returns identities from the full ordered potion pool for a declared `event` or `reward` stream, owner prefix, and ordinal. It binds the same pool fingerprint into the request owner; event-choice controls Wellspring's Native-shaped `bottle` route and the combat reward session controls the registered retained reward-stream request. Source-pool completeness and equal-index weighting remain `UNVERIFIED`.
- The generic legacy `event.reward.random_potion.model` query returns identities from the ordered prototype potion pool for caller-declared `event_id`, selected `action_id` (choice ID), and ordinal. Wellspring and Whispering Hollow use separate Native-shaped routes; the existing event-choice continuation controls their typed uniform or rarity-and-model requests, with equal-index laws remaining `UNVERIFIED`.
- The legacy `event.reward.random_rare_card.model` query returns identities from the ordered prototype reward pool filtered to Rare cards, keyed by declared event ID and selected choice ID. Its distribution remains `UNVERIFIED` because the prototype pool is not the Native character pool.
- The relic-acquisition model queries expose the fixed prototype Rare-card pool on legacy `reward` and the fixed potion pool on legacy `event`, with source-shaped owners and pool fingerprints. The potion query requires the caller to declare the first open slot. Both also accept an explicit `player.rewards` owner and support to query exact Native `NextInt` masses; this verifies only the bounded transform, while source pool and stream attribution remain open.
- Punch Off reward laws map exact Native `NextInt` masses onto the configured plan's ordered relic and potion identities, including the potion choice's preceding relic-index condition. A direct legacy `reward` query accepts the caller-declared current support and owner. Native transform masses are `SOURCE_VERIFIED`; pool fidelity and `player.rewards` attribution remain hypotheses.
- The legacy `run.potion.fill_empty_slots.model` law exposes one ordered prototype potion pool per caller-declared empty slot and rejects the no-capacity branch. Its owner binds the slot and pool fingerprint. Both the typed ReferenceNature run-action continuation and the saved post-choice event-reward continuation consume this law for legacy Entropic Brew refills. The Native `player.rewards` query provides exact `NextInt` masses for the captured owner and support and is used by both paths. Native pool ordering, trigger order, and source stream attribution remain open pending the reference mod.
- The legacy `event.reward.offer_potion.model.index_draw` query returns the ordered prototype potion pool for a caller-declared custom reward owner and Rewards ordinal, provided potion acquisition is allowed. Its pool fingerprint matches the owner suffix used by the source-shaped reward generator; pool fidelity and equal-index weighting remain `UNVERIFIED`.
- The legacy `event.reward.random_modifier_curse.index_draw` query returns the implemented ordered modifier-curse pool for a declared event ID and Rewards ordinal. Equal-index weighting, complete source reward audit, and Native parity remain `UNVERIFIED`.
- The legacy `event.potion.fill_empty_slots.model` query returns the ordered prototype potion pool for a caller-declared empty slot when potion acquisition is allowed. The owner matches the slot-index pool fingerprint; pool fidelity and equal-index weighting remain `UNVERIFIED`.
- The legacy `shop.card_rarity.ticket` query returns source-shaped Act rarity weights after filtering tiers absent from the remaining caller-declared card-type pool. Its ticket-index weights and prototype pool fidelity remain `UNVERIFIED`.
- The legacy `shop.card_model.index_draw` query returns remaining ordered prototype card identities for the declared Act, card type, rarity, and prior offers in that type group. Its owner fingerprint matches legacy Shop generation; pool fidelity and equal-index weighting remain `UNVERIFIED`.
- The legacy Shop card, potion, and relic price queries return their source-shaped integer price outcomes for a declared model/offer owner. Card prices use the selected card's rarity range; equal request-index weighting remains `UNVERIFIED`.
- Registered legacy bounded-index queries reuse `RandomIndexRequestV111` and `QueryRandomIndex` for one caller-declared support and owner, including map-generation, encounter-selection, `event.selection.weighted_ticket`, and `combat.enemy.summon_pool.select` requests. The site registry fixes each request's stream and operation; these laws do not validate event weights, summon-pool eligibility, or the surrounding queue prefix, and legacy equal-index weights remain `UNVERIFIED`.
- `relic.paels_legion.skin.index_draw` retains its dedicated four-outcome legacy query, which validates the relic-instance owner and isolated `reward` ordinal zero. The query describes indices only; equal-index weighting, Native stream parity, and relic seed derivation remain `UNVERIFIED`.
- `relic.unowned_random_selection.index_draw` has a direct caller-bound query for legacy `event` and `reward` supports and the hypothesized Native `player.rewards` route. Callers provide the eligible support count and owner, including its pool fingerprint. Native bounded-index masses are exact for the declared bound; single-copy filtering, prefix attribution, legacy weights, and cursor parity remain open.
- The legacy `reward.elite_relic.model` query returns identities from the fixed ordered prototype relic pool for a declared Act, floor, and Rewards ordinal. Configured Native runs use the relic grab bag; prototype pool fidelity and parity with that bag remain `UNVERIFIED`.
- The legacy `treasure.relic_model.index_draw` query returns the ordered prototype relic pool after excluding caller-declared owned relic IDs. Its owner binds Act, floor, and the remaining pool fingerprint; empty pools are rejected because that branch consumes no index draw. Configured Native runs use the separate relic grab bag, and pool fidelity and equal-index weighting remain `UNVERIFIED`.
- The legacy `reward.card_model.index_draw` query returns the remaining ordered prototype card identities within a caller-declared rarity, after prior picks from the same reward group are removed. Its request owner matches the source-shaped group/card/rarity/pool fingerprint; pool fidelity and equal-index weighting remain `UNVERIFIED`.
- The legacy `reward.card_transform.replacement.index_draw` query returns the ordered prototype reward pool excluding the transformed instance's original model. It reconstructs the legacy owner from the card instance, original model, and remaining-pool fingerprint; pool fidelity and equal-index weighting remain `UNVERIFIED`. A controlled Shop or Reward relic deck-transform continuation reuses those `card:<model>` outcomes after checking them against the saved request and candidate labels, while its event ID remains bound to the exact legal action boundary. The same run-action query reuses the existing semantic `card:<model>`, `potion:<id>`, and `relic:<id>` laws for supported legacy relic-acquisition draws, empty-slot potion fills, Treasure entry, and legacy Rest Heal card models. Rest Heal laws bind the selected rarity and saved prior card picks; the rarity-ticket law preserves each raw ticket position as a unique outcome and annotates the rarity bucket reached by that position, so action replay still uses the original bounded index. Pool fidelity and ticket weighting remain `UNVERIFIED`. Native `player.rewards` requests retain their bounded-index masses.
- Native card-rarity queries accept a caller-declared source, current offset, Ascension, owner and ordinal, and return exact source-rounded `NextFloat()` masses for the raw rolled rarity before available-pool fallback. The bounded threshold transform is `SOURCE_VERIFIED`; reward-prefix validation, pool fidelity, stream assignment and cursor parity remain open.
- The retained legacy card, potion and relic rarity-ticket sites have separate read-only categorical laws through `chance_law_query` (`state_handle`, `site_id`, and `ordinal`). Card tickets also require `card_rarity_source`, `current_offset_basis_points`, and `ascension`; potion tickets require `stream_id` (`reward`, `event`, or `shop`) and `owner_id`; relic tickets require `owner_id` and a `merchant` flag to reconstruct the request owner. The weights total 10,000 for card tickets and 100 for potion/relic tickets. These integer-ticket weights remain `UNVERIFIED` against the Native RNG; the combat reward session consumes its registered reward-stream requests, while other callers remain outside that continuation.
- The legacy card-upgrade ticket has a read-only law for the raw upgrade comparison flag, conditioned on `card_id`, `act`, `ascension`, and the `reward` ordinal. It models the strict integer-ticket comparison and totals 10,000 weights; the combat reward session controls the registered legacy reward-stream request, while the law and session remain `UNVERIFIED` against Native RNG behavior.
- The legacy `reward.card_rarity.ticket` query uses `chance_law_query` with `state_handle`, site, ordinal, group `owner_id`, Act, and ordered `prior_card_ids` from the same reward group; the first pick passes an empty array. It returns act-weighted rarity outcomes after filtering empty tiers from the remaining prototype pool. The resulting owner and available-pool fingerprint match the legacy ticket construction. Pool fidelity, ticket weighting and Native parity remain `UNVERIFIED`.
- Native combat card-upgrade queries return the raw comparison flag for a declared supported card, Act and Ascension. They count exact `NextFloat() <= threshold` grid masses, including the single zero-valued grid input at a zero threshold; this does not claim the card mutation or verify the reward prefix and cursor.
- PotionFactory rarity has a cursor-blind query over its inclusive 0.1 and 0.35 Native float thresholds. The paired model-index query accepts the same registered `run.combat_potion_generation`, `player.rewards`, and `player.shops` streams and validates the source-ordered tier support after the caller supplies rarity and exclusions. Exact rounded-grid and bounded-index masses are `SOURCE_VERIFIED`; source-call order, pool completeness, and cursor parity remain open.
- Relic rarity has a standalone `player.rewards` query for raw Common/Uncommon/Rare thresholds at strict 0.50 and 0.83 boundaries. It counts exact rounded-grid masses without inspecting the bag; hidden queue identity and call-prefix/cursor parity remain separate.
- Encounter card-model selection has a cursor-blind query for exact Native bounded-index masses over a caller-declared positive support size, owner, and Rewards-stream ordinal. The `SOURCE_VERIFIED` law covers the index transform only; it does not validate the remaining ordered card pool, reward prefix, stream attribution, or cursor parity.
- Ordinary Native relic-bag queue construction has a per-swap law for exact `NextInt(bound)` masses on `run.treasure_room_relics`. The caller declares the current Fisher–Yates bound, queue owner, and swap ordinal; eligibility, earlier swaps, full queue identity, stream-prefix validation, and cursor parity remain open. ReferenceNature run starts keep this order unresolved in canonical rarity sets.
- Ordinary draw-pile Fisher–Yates swaps also have a cursor-blind Native `run.shuffle` law for the caller-declared bound, owner, and ordinal. It verifies only one `NextInt` transform; pile order, call prefix, legacy routes, and cursor parity remain open.
- The supported end-turn discard-recycle shuffle has the same direct Native `run.shuffle` bounded-index law at `combat.draw_pile_recycle.swap_index`. The caller declares `player:0`, the current swap bound, and ordinal; the ordered discard pool, draw prefix, legacy route, and cursor parity remain outside this query.
- Native bounded-index queries cover supported draw-pile insertion requests on `run.shuffle`: ordinary generated cards, after-shuffle relic cards, enemy draw/discard effects, and Personal Hive Dazed. Callers declare the source-specific owner, current pile-position count, and request ordinal; source stream calibration, the preceding operation prefix, legacy laws, and cursor parity remain open.
- Boss relic-offer and Lava Rock extra-relic construction have per-swap laws on `player.rewards`, with the current bound, source owner, and swap ordinal declared. These verify Native `NextInt` weights only; eligible pools, reward prefix, legacy routes, and cursor parity are not checked.
- The fixed Act 1 Overgrowth and Underdocks boss pools have pre-run direct Chance Law queries over their source-ordered encounter identities. Configured initialization samples either boss through its existing factory when the run-start state already owns a Nature cursor. The configured Underdocks boss choice suspends and resumes through `ReferenceNatureRunActionSessionV111`; Overgrowth uses that same session for its boss choice and nested typed map generation. Other startup draws keep their existing providers, and Neow offer generation retains its Native event context. `V111RunFactory.CreateWithReferenceNature` and JSONL `reset_v111`'s optional `nature_seed` make the cursor available before the start action. The configured run persists and replays its ordered initialization transcript, including the compact typed Overgrowth map audit and, on the experimental route, later-act Ancient assignment and bag decisions. The read-only equal-index models remain `UNVERIFIED`, and Native source-pool/map fidelity remains open.
- Glory Ascension 10 second-boss assignment has a direct law query over the two ordered alternatives after the first boss. Configured experimental Silent run initialization consumes it through the ReferenceNature provider after the prepared queue calls and conditions it on the actual first boss after the undiscovered-boss preference. The equal-index law remains `UNVERIFIED`.
- Hive and Glory's complete prepared encounter bags now consume the existing weak, normal, elite, and boss selection boundaries through a scoped ReferenceNature provider, including Glory's conditional A10 second boss. The queue laws retain dynamic ordered supports and are replayed through the existing generator; discovered-boss preference is applied after the sampled raw boss index. The scope checks each queue/boss ordinal and leaves the legacy prototype RNG unchanged. Legacy equal-index queue/boss models remain `UNVERIFIED`; native UpFront ordering and source fidelity remain open. Configured experimental starts now replay these choices one at a time inside `ReferenceNatureRunActionSessionV111`; completed Act 1 snapshots rebuild the generator prefix against the persisted bags.
- Later-act Darv partition and local Ancient assignment have conditional queries over declared run-start inputs, with ordinals on the legacy `event` stream. When a configured experimental Silent run starts with a ReferenceNature cursor, those existing assignment calls now use the queried laws through a scoped ReferenceNature index provider; the Glory Darv draw still occurs with bound one after Hive takes Darv. The assignments, advanced cursor, and ordered transcript are retained in the prepared run state; configured experimental starts now suspend and resume these requests in the same run-action session. The equal-index laws remain `UNVERIFIED`.
- Act 1 Overgrowth and Hive/Glory later-act path-start calls have read-only `SOURCE_VERIFIED` Native laws for each raw `NextInt(7)` draw over columns 0–6. Their three-entry delta shuffles expose per-swap `NextInt(3)`/`NextInt(2)` laws, room-assignment shuffles expose the exact bounded transform on the current support, and Glory's rest-count law returns exact `NextInt(2)` masses for quotas 5 or 6. The atomic `ReferenceNatureMapGenerationSessionV111` replays these requests in generator order. Configured Overgrowth run initialization uses that session after boss selection and retains its typed prefix plus the generated map fingerprint in the run-initialization transcript. Configured experimental `continue_act` composes the same Hive/Glory session with the ordinary engine transition step, publishing the map only after its typed prefix completes. Retry prefixes, dynamic room-assignment supports, and resulting geometry replay through the existing generator. Source pool/geometry fidelity and Native cursor parity remain separate.
- Native Overgrowth, Hive, and Glory Gaussian map-quota sites have read-only `gaussian_int` laws over their declared rounded and truncated integer supports. They use a continuous truncated-normal approximation bound to the source-shaped site, context stream, owner, and ordinal zero; the shared law-to-provider adapter maps `integer:<value>` outcomes to the existing controlled Gaussian-integer decision. The map session includes those outcomes in its saved prefix. Finite Native-grid masses, source call order, and the legacy Box–Muller component route remain `UNVERIFIED`; ordinary Native map generation retains its source-owned path, while configured Overgrowth starts and experimental later-act transitions use the counterfactual session.
- The automatic enemy-action index event uses that same request law. Random enemy draw/discard insertion requests on canonical `run.shuffle` therefore carry exact Native bounded-index masses; legacy `combat` requests retain the equal-index `UNVERIFIED` model. The bounded transform does not verify enemy pool eligibility, stream assignment, or cursor parity.
- A source random-pile shortlist preserves its Native descending Fisher–Yates sequence as typed per-swap pool-shuffle events, then exposes the shuffled prefix through the ordinary player card choice. Each recognized Native `run.combat_card_selection` swap has an exact bounded-index law; legacy and compatibility shortlist paths remain `UNVERIFIED`.
- Supported draw-pile insertion events carry `run.shuffle` when a Native combat stream owner is present and `prototype.combat` on the legacy path. Their Native position laws use exact bounded-index grid masses; the legacy equal-position model remains `UNVERIFIED`.
- The Native-regional Shop fallback with no `NativeRunRng` now uses `ReferenceNatureMerchantSessionV111` for atomic Shop setup. It captures the legacy sale-slot, card rarity/model and no-effect upgrade, Colorless-card, weighted potion, relic rarity/identity, and price requests from the legal Shop-entry action. Legacy `shop` and `reward` request-index weights remain `UNVERIFIED`; the session samples relic identities from the canonical Nature bag support without exposing queue order.
- Native merchant laws are queryable at a pre-entry configured Shop boundary and are consumed by `ReferenceNatureMerchantSessionV111` during atomic Shop generation. Sale-slot and card, Colorless-card, and merchant-potion model indices use exact Native bounded-index masses; merchant Colorless support is filtered by the run's configured unlocked epochs and shared with ordinary Shop generation. Merchant card, potion, and relic rarity thresholds use exact preimages of the rounded Native `NextFloat()` grid; supported rounded-price mappings count exact preimages through the source modifier and price conversion. The same mixed merchant session also accepts legal configured and Native-regional Shop purchases, including Alchemical Coffer's four PotionFactory rarity/model pairs, registered player reward-index models, and Toy Box's five relic-bag rarity/identity pulls. Native-regional fallback Toy Box purchases with no Native RNG use the same session with equal-index laws for five rarity tickets and five sorted residual identity requests. The fallback Shop entry validates its three legacy weighted-potion offer pairs, and fallback Alchemical Coffer purchases replay four legacy rarity-ticket/model-index pairs from the `event` stream. Their ticket and model weights remain `UNVERIFIED`; model support follows the source-ordered rarity-conditioned residual pool. The existing registered purchase-index allowlist also applies to Native-regional purchases; configured Toy Box rarity uses exact rounded Native threshold masses, while identity choices use equal weights over each captured sorted residual support and remain `UNVERIFIED`. Other registered purchase index requests use the shared typed-index law with the purchase mechanics boundary, request, ordered labels, and prior transcript in its event fingerprint; the session and controlled run-action continuation share a per-site stream allowlist. Generic request-index weights remain `UNVERIFIED` on noncanonical streams; source-pool completeness, trigger eligibility, seed frequencies, and cursor parity remain separate. Unregistered stochastic purchase hooks stay fail-closed.
- Native Treasure laws are available before a reachable, non-skipped configured Treasure node. The compact `treasure.relic_grab_bag.draw` query integrates nominal rarity thresholds with the hidden uniform permutation of remaining rarity queues. The atomic `ReferenceNatureTreasureSessionV111` samples the rarity float and then, when eligible, a conditional relic identity, applying the shared source fallback plan without exposing queue order. For Native-regional Treasure or Shovel entry when `NativeRunRng` is absent, the generic `ReferenceNatureRunActionSessionV111` controls the retained `relic_grab_bag.rarity.ticket` and conditional `treasure.relic_grab_bag.model.index_draw` requests using sorted Nature support. These legacy index laws remain `UNVERIFIED`. Ordinary Native entry retains its source-owned rarity roll and queue-front draw.
- Combat victory has an atomic `ReferenceNatureCombatRewardSessionV111` for configured Native requests and a registered subset of retained Prototype `reward`-stream index requests. Begin with a legal Combat, Elite, or Boss action that immediately enters `Reward`; replay captures the supported pity, gold, potion, card, relic, boss-offer, and Lava Rock prefix, and publishes the reward state with the Nature cursor only after replay validation. Native bounded indices and threshold floats use exact source-grid laws; hidden relic identities use equal weights over the remaining canonical bag support and remain `UNVERIFIED`. Legacy requests use equal-index `UNVERIFIED` laws. Ordinary Native relic-bag queue construction, unlisted requests, source-pool completeness, stream attribution, seeded Native parity, and Native cursor parity remain outside the claim. See the [combat reward session contract](REFERENCE_NATURE_COMBAT_REWARD_D3.md).
- Native Lost Wisp and Jungle Maze Adventure unit-float mappings and the pinned generated-gold bounded-index mappings have conditional read-only laws with exact source-grid masses. Callers supply `event_id` as an explicit branch condition; queries do not inspect event-selection results. `ReferenceNatureNativeEventGoldSessionV111` consumes those requests during configured Event-node entry and also captures supported Native-regional legacy generated-gold index requests on the saved `event` stream. Those fallback laws preserve the typed request ordinal, validate each ordered event-stage support, and assign equal `UNVERIFIED` weights. The session does not model the complete Event-node selection law; other legacy entry draws, event-pool completeness, seed frequencies, stream attribution, and cursor parity remain open.
- Stone of All Time `push_ticket` and `lift_ticket` queries condition on a legal event action and return one `no_effect` outcome because the pinned resolver discards each bounded result. Native tickets use declared source ordinal 1 after the entry potion-slot draw; legacy callers supply the request ordinal. Queries do not inspect the saved `event` stream cursor. Replay validates the typed request against the saved mechanics boundary and chronological provider prefix. The atomic controlled event-choice session covers configured Native, retained legacy, and Native-regional fallback routes: lift suspends before potion/Max HP mutation, while push first records the player deck-card choice and then suspends at its ticket. A lethal push HP cost ends the run before the ticket. Controlled outcomes advance neither provider cursor; seeded stream parity remains open.
- Stone of All Time's entry-time potion-slot query conditions on an explicit `event_id` branch and a legal Event-node action. Native reports exact bounded-index masses over the ordered occupied slots with ordinal 0; legacy ReferenceNature reports equal support weights and remains `UNVERIFIED`. The read-only query does not inspect the selected event or legacy cursor. The Native event-entry session consumes configured Native and Native-regional fallback requests, preserving the saved legacy `event` ordinal. The existing push/lift continuation composes afterward at Native ordinal 1 or on the legacy `event` stream. Native cursor parity remains open.
- Native Darv's Calling Bell acquisition now uses the existing controlled event-choice continuation for its three forced-rarity relic-bag identity draws. The choice session canonicalizes the remaining Nature bag, replays each ordered support through the source action, and commits the resulting reward with the Nature cursor. Native bounded-index masses are `SOURCE_VERIFIED`; hidden-queue identity, source-pool completeness, and Native cursor parity remain open.
- The existing Native relic-grant session also handles a legal configured combat-reward pickup of Beautiful Bracelet, Distinguished Cape, Pael's Legion, or Sand Castle. It replays the selected relic's existing acquisition hook and conditions each registered request on the saved action, ordered support labels, and earlier choices. `run.niche` bounded-index laws use exact Native masses; the retained `event` and `reward` requests remain equal-index `UNVERIFIED`. Other combat-reward acquisitions stay outside this boundary.
- Infested Automaton and Glory Reflections card-pick laws derive the configured Native request stream ID from the declared event identity without reading the run seed or saved Native cursor. Native-regional legacy fallbacks derive typed request ordinals from the saved `event` call count and any prior Reflections prefix; equal-index laws are `UNVERIFIED`, and logical choice ordinals remain distinct from typed request ordinals.
- Future of Potions exposes one bounded-index law for each owned potion slot after the caller declares the event branch. The ordered support follows the potion rarity mapping and source card-type order; configured Native requests use exact source-grid masses, while Native-regional legacy requests use equal `UNVERIFIED` weights and the saved `event` ordinal. The atomic Native event-entry session consumes both routes in slot order while keeping Event selection on its existing provider.
- The Glory Trial law counts exact Native `NextInt(3)` preimages and is `SOURCE_VERIFIED` under the uniform-grid input model. The Chance Law query does not read the Native cursor; the atomic session separately checks its replay origin at the event-entry cursor. Native-regional fallback reports the current legacy `event` call count as the typed request ordinal, uses equal-index `UNVERIFIED` weights, and leaves the selected request unadvanced.
- Trash Heap's card and nonlethal relic reward laws count exact Native bounded-index preimages on their declared source-ordered pools. Native-regional runs without `NativeRunRng` use the same atomic session with the current legacy `event` request and equal-index `UNVERIFIED` laws. The configured Native query does not inspect its saved event cursor; the fallback query reads only the legacy `event` call count needed for its typed request ordinal. Session admission validates the Native entry cursor or fallback legacy route before replay.
- Tablet of Truth counts exact Native bounded-index preimages on each ordered eligible physical-card support. Its configured Native law uses no run seed or saved cursor; the one-page session preserves the Native cursor from that page's action boundary. Native-regional fallback reports the current legacy `event` call count as the typed request ordinal, uses equal-index `UNVERIFIED` weights, and leaves the selected request unadvanced.
- Probability computation is **optional in normal Native execution**: Native invokes its own source sampling procedures, with no Chance Law round trip.
- `ReferenceNature` samples from reported `p`; learned Nature supplies `q`. Their selected outcomes enter the same continuation. Unverified `p` can support structural tests and exploratory training; unbiased original-game `p/q` claims require independent N5 calibration.

## Three independent validation tracks

1. **Structure and declared-law consistency:** legal normalized Chance Law; exact small-state enumeration; ReferenceNature sampled vs independently specified joint distributions; common mechanics; atomic forks/replay and scheduling independence. No original game or exact native streams required.
2. **Two-site Nature vs control training experiment** — [issue #143](https://github.com/nn971/sts2-emulator/issues/143): `enemy_intent` and unresolved `draw_card` only under Nature; all other randomness engine-owned. Same fair player/action contract, matched reproducible emulator A/B, 15 isolated workers. The control may use the declared-law ReferenceNature as a transparent sampling baseline; a separate native-style commit-once provider is also a useful implementation cross-check. Neither implies native cursor parity.
3. **Native-game fidelity** — [issue #122](https://github.com/nn971/sts2-emulator/issues/122) and [Inspector plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md): installed-game captures, mechanics/hook/stream ordering, exact RNG call/cursor provenance. **N5 original-game probability calibration** uses independently sourced rules/evidence and is separately gated.

`SimulationMechanicsProjectionEngineV111` forks and projects the existing run, world, combat, and event mechanics owners into one provider-independent view, then fingerprints that view. It omits RNG seeds/cursors, hidden deck constraints, physical draw-pile order, and unresolved chance supports and decisions, while retaining pending deck-operation purpose/position, draw-pile membership, and completed peek/scry results through the public draw-pile view. It retains deterministic saved draw progress, including normalized automatic hand-draw remaining count and pending draw-cost identity and stage, so differences in the remaining mechanics continuation stay visible. Use it after applying the same semantic outcomes. It is a comparison aid, not evidence that two trajectories used the same choices or that either provider matches the installed game.
Before projecting, it validates the complete `RunState`, including provider and
mechanics correspondence. Raw-state comparisons and mechanics fingerprints
therefore reject malformed or unsynchronized inputs before stochastic data is
removed from the view.
The projection also retains pending run-action, post-reward event-action,
event-effect, and enemy-defeat continuation mechanics from their existing
mechanics owners, including action identity, effect cursor, queued
consequences, and the saved fatal follow-up, while omitting their random
requests and decision payloads. The mechanics comparison schema advances to
v19 for this expanded projection; the provider replay comparison remains v3.
`SimulationMechanicsComparisonV111` copies the difference array on
construction and replacement, then returns defensive copies to keep the
diagnostic result immutable to callers.

`SimulationProviderReplayCaptureV111` groups an origin run, player-action
sequence, ordered typed random-boundary trace, and result run in the existing
`SimulationStateV111` envelope. Its v2 schema includes capture provenance:
explicit fields are marked caller-supplied and unverified, while scripted and
Native-policy factories mark the histories they replay-validate.
`SimulationProviderReplayComparisonEngineV111` compares two such captures by
fingerprinting their action sequences, comparing origin mechanics, comparing
every supplied typed boundary in order, and comparing the resulting mechanics,
returning each agreement and both capture provenance values separately.
Callers must provide captures covering the same interval and all typed
boundaries in it; caller-supplied provenance remains an assertion and does not
prove capture completeness. Native cursor equality is an optional additional
boundary check. This is diagnostic evidence under the supplied captures, not
proof of source-hook parity, complete Native coverage, or Chance Law fidelity.
`SimulationProviderReplayCaptureV111.FromScriptedRunSession` and
`FromScriptedRunActionSession` provide a stronger path for scripted replays:
each session replays its completed action history from the origin, and the
existing typed boundary journal materializes its saved outcomes into the
capture. They accept only sessions at committed action boundaries.
`FromSemanticNativePolicySession` provides a corresponding path for a
replay-validated Native-policy baseline. It reconstructs the initial player
boundary from the seeded entry, verifies that the saved provider trace begins
with the same setup and committed-intent prefix, and replays the saved player
actions to recover their exact mechanics actions. The capture starts at that
initial player boundary, so route setup and initial-intent decisions are
excluded; its boundary trace contains only subsequent player-action outcomes.
The JSONL operation accepts `<side>_semantic_native_policy_handle` for this
capture path. `FromReferenceNatureEventChoiceSession` restores and replays the
session's interleaved player-action/Nature chronology, checks the completed
controlled-choice trace against its Nature outcomes and prior history, then
materializes its typed requests through the same boundary journal. JSONL accepts
`<side>_reference_nature_event_choice_handle` for this path. It does not
establish Native source-hook parity or law fidelity.
`FromReferenceNatureControlledActionSession` uses one shared interface for
completed Merchant, Treasure, combat-reward, Native Shovel, Native event-relic,
Native event-gold, Infested Automaton, Glory Reflections, Trial, Trash Heap,
Tablet of Truth, Native event-shuffle, Endless Conveyor, and Punch Off fight
and reward sessions. Each session first replays
its own action and typed stochastic prefix; the capture then records that
prefix through the same journal. Native event-gold reuses its validated typed
prefix. Infested Automaton, Glory Reflections, Trial, Trash Heap, Tablet of
Truth, Native event-shuffle, Endless Conveyor, and Punch Off derive typed
decisions from their existing validated Chance Law decisions, exact requests,
and ordered outcome supports.
JSONL accepts the matching
`<side>_reference_nature_{merchant,treasure,combat_reward,native_shovel,native_event_relic,native_event_gold,infested_automaton,glory_reflections,trial,trash_heap,tablet_of_truth,native_event_shuffle,endless_conveyor,punch_off_fight,punch_off_reward}_handle`
inputs. This adds replay-capture coverage without changing the action or
provider owners.
`FromReferenceNatureRunActionSession` accepts a completed ReferenceNature
run action when the appended controlled action trace identifies the exact
saved action and covers every saved Nature decision. For configured run starts,
it combines the validated boss trace with the completed Act 1 map audit when
those requests together cover the initialization transcript. It replays and
validates the session, preserves the prior action-history prefix, and
materializes the typed prefix as the interval boundary trace. Completed
deterministic actions with no appended trace and an empty Nature transcript
remain valid. It rejects specialized continuations with incomplete traces and
experimental starts whose saved later-act prefix does not replay completely.
That prefix is rebuilt from the saved initial prototype RNG, configuration,
and Nature transcript through the existing later-act generator; no new
snapshot field is required. Completed experimental `continue_act` sessions
without an appended action trace reconstruct their boundary prefix from the
new map audit and optional replay-validated assigned Ancient-offer audit.
Legacy Native Ancient completions replay the saved map transcript, rerun the
assigned Native Ancient through its audited opening generator, and require the
committed result to match the saved run before adding the Native provider
decisions and cursor spans after the map decisions in the capture.
JSONL accepts
`<side>_reference_nature_run_action_handle` for this path.
`FromReferenceNatureRunSetupSession` shares one capture path for completed
Neow-offer and Ancient-offer setup sessions. Each session rebuilds its exact
ordered request/support prefix from its validated outcomes, and the capture
records the origin-to-result setup interval with no player actions. JSONL
accepts `<side>_reference_nature_neow_offer_handle` and
`<side>_reference_nature_ancient_offer_handle` for these setup intervals.
The JSONL `provider_replay_compare` operation constructs a current capture for
each side either from a completed scripted-run or scripted-action handle, or
from replay-validated semantic Native-policy, ReferenceNature event-choice,
controlled-action or run-action handles, or from exact origin/result snapshots
plus action and boundary arrays,
with optional Native cursor equality and a mechanics-difference bound.
`hello` advertises its capture and result schemas, operation, and inputs. These
provider traces are privileged diagnostic data and are not part of the fair
player observation.

**ReferenceNature cannot certify its own Chance Law** merely by sampling from it. Correctness of `p` needs an independent oracle: exact enumeration for small decks, separately specified analytic weights/rules, and eventually pinned original-game evidence. A statistical comparison between Native and ReferenceNature is helpful only when Native's distribution at the compared information boundary is well specified.

## Compatibility rules

- No schema change to existing v111 `nature_*`, `controlled_*`, `semantic_*` or ordinary JSONL/Python interfaces merely to introduce the design. New capabilities and law/provenance fields are **opt-in and versioned**.
- Preserve existing snapshots, immutable forks and replay invariants; old histories retain their original meaning and probability labels.
- Make unsupported stochastic operations **fail atomically** and report the site; no undocumented prototype/hash fallback in a new claimed ReferenceNature/native control profile.
- Keep `sts2-ai` training policies out of the C# engine; a deterministic seeded ReferenceNature test sampler is emulator infrastructure.
- Preserve the ongoing Silent four-Act work, reference mods and inspector PR stacks. Any implementation migration must identify its own tests and feature gates.

## Current S4 continuation support

The semantic draw stochastic owner also records random free-attack model
selections reached from an active `CardDrawn` or pre-draw hook. The selected
model is followed by the existing draw-pile insertion-position choice; the
draw frame owns both decisions and their mixed chronology. It also owns
ordinary generated-card, potion-generation, pool-shuffle, and supported
`select_cards` choices opened by a nested draw-hook action, then resumes the
saved draw cursor after the child effect. The card-selection child uses the
existing semantic card-selection event and decision owner; admission requires
the exact outer choice continuation and a valid pre-draw or active hand-draw
cursor, and excludes card-selection prompts already owned by the automatic
pipeline. Chronology versions 2, 3, and 4 added generated-card,
potion-generation, and pool-shuffle decisions; version 5 added card-selection
decisions; version 6 adds card-transform decisions that follow a supported
TransformRandom selection; and version 7 adds supported random autoplay and
replay-card shortlist decisions; version 8 adds random-hand-cost and
hand-cost-selection decisions. Replay continues to accept version 1 snapshots
without generated-card, potion, pool-shuffle, card-selection, card-transform,
or card-shortlist decisions; version 2 snapshots without potion, pool-shuffle,
card-selection, card-transform, or card-shortlist decisions; version 3
snapshots without pool-shuffle, card-selection, card-transform, or card-shortlist
decisions; version 4 snapshots without card-selection, card-transform, or
card-shortlist decisions; version 5 snapshots without card-transform or
card-shortlist decisions; version 6 snapshots without card-shortlist decisions; and version 7
snapshots without random-hand-cost or hand-cost-selection decisions. End-turn
forwards these pending choices to its active draw frame,
and pilot history records each external choice once. Other child-choice kinds,
unsupported selection specifications, random attack pool fidelity, and Native
stream parity remain open pending reference-mod evidence.

`PrototypeAutomaticPipelineResumeResultV111` is the single owner for
classifying an ephemeral run-resume boundary. Draw-progress and semantic-choice
results pass any saved classification through that owner, which checks it
against the returned run before publication. This keeps pipeline routing
consistent across adapters without adding serialized fields or moving typed
transcript ownership.

`SemanticDrawPileAdapterV111.IsUnchangedDrawPile` owns the combined physical
order and semantic-deck comparison used when a supported continuation must
preserve the hidden pile. `RequireUnchangedDrawPile` delegates to that
predicate; semantic player-action validation, pilot action and potion checks,
and full-pool shuffle use it for conditional mutation admission. Their
source-specific allowlists and failure policies remain at their existing
boundaries.

An `EnemyDamaged` or `EnemyDefeated` subscriber can now suspend either a
`DamageEnemy` operation or `DamageAllEnemiesRepeatPerKill` (including Echoing
Slash) through the existing outer choice continuation. Both operations use
one post-hit event dispatcher and death follow-up; the saved frame resumes
remaining damage events, death summons, fatal rewards, and queued operations
in source order. Repeated damage also retains the current round's target
snapshot, next target index, and pending extra kill rounds, so earlier hits
are not replayed after a hook choice. Pending event replay uses the same
resumable dispatcher, and restore validates the defeated target against the
saved follow-up and repeated-damage cursor. The mechanics comparison retains
this progress under schema v19; provider replay comparison remains v3.

Toolbox's first-turn `BeforeHandDraw` generated offer now composes the
existing full-pool shuffle and ChoicePool card-selection continuations. Exact
relic-trigger, source-pool, and `DrawPlayerHand` cursor checks admit each
boundary. Selection completes the saved event dispatcher, then the hand-draw
owner resumes without replaying `BeforeHandDraw`; the generated selection is
kept out of the draw-hook action projection. Its exact generated-choice
subscriber and physical-offer checks are shared with the opening Toolbox and
Choices Paradox continuations, while each owner retains its own cursor check.
Hello World's existing pre-draw common-card shuffle shares the extracted
source-frame validation. No event or snapshot schema changed.

Ordinary combat sampling and the resumable generated-card choice now use the same source-pool resolver for the supported one-at-a-time hand-generation effects. Random draw-Attack insertion has its own shared resolver for Native sampling, Nature suspension, and replay validation. When Native combat run streams are present, supported generated-model and transform events carry Native's `run.combat_card_generation` stream label, and random combat-potion events carry `run.combat_potion_generation`; prototype-only runs retain `prototype.combat`, and older prototype-labeled decisions remain replayable. Full-pool generated offers, distinct Common cards, and Colorless generation likewise share one resolver between ordinary execution and Nature replay. The emulated Native Colorless generation, Colorless transforms, and Fried Eel offer use implemented solo cards whose epoch is unlocked by the configured profile; with no explicit epoch list a fully unlocked profile admits all eligible epochs, while any explicit list selects its matching cards. Unknown partial-profile data remains fail-closed. Synthetic generation keeps its conservative no-epoch pool. These paths therefore share their exact ordered eligibility results; source-pool completeness and seeded parity remain separate audits.

Semantic end-turn now admits a source `CardDrawn` hook action reached inside an autoplayed card's semantic effect draw, but only at the supported Mayhem or late player-turn cursor. The hook event uses a new schema version for its automatic-pipeline context, and the end-turn transcript preserves its order with nested draw, cost, insertion, selection, and other autoplay outcomes. Full restore replays the source action before resuming the pipeline. Existing standalone and automatic hand-draw hook choices retain their prior event schema and identity. The automatic hand-draw continuation owner decides whether saved count progress or a pre-draw choice keeps the pipeline at `DrawPlayerHand`; the generic pipeline loop does not interpret those hand-draw tags. Resolving an automatic `BeforeHandDraw` action may also open Jeweled Mask's random power draw-pile choice; that outcome stays with the existing semantic random pile-card event and hand-draw transcript, so restore replays the action then the pile-card choice. A `CardDrawn` hook action may also open that random pile-card child after hand-draw progress has started; the draw owner retains its requested and remaining counts, the existing random pile-card owner resolves the choice, then passes the result through the common hand-draw handoff. The end-turn selected-index resolver routes the active hand draw's semantic card event through the draw frame. It also routes a random pile-card child through that frame. A supported random-target choice opened by a `BeforeHandDraw` action keeps the saved pre-draw cursor, resolves through the existing target owner, and then continues into the hand draw. A source `CardDrawn` hook action may also suspend on a random-target child after automatic hand-draw progress has started. The active draw frame owns that target transcript and its `random_target` chronology entry; end-turn restore replays the hook action and target inside the same draw replay before continuing the hand draw. Admission requires the tagged automatic hand-draw target and unchanged requested/remaining counts. Further random-target choices opened while resuming that target are supported through the same frame and chronology; the final target resume uses the common engine-owned nested-choice handoff to validate and publish the resulting hand-draw cursor, while the target owner retains transcript and chained-target rules. Other nested hook choices and pipeline-owned action choices remain open.

Semantic draw-hook replay now returns its classified automatic-pipeline boundary alongside the run, and the end-turn nested-choice adapter carries it directly to boundary publication. The shared draw-hook resume validates that classification against the resulting run on standalone, pre-draw, and active hand-draw paths; resuming an active hand draw also retains and checks its requested count. A saved random pile-card child is classified as a semantic choice only at the validated pre-draw or active hand-draw cursor. The run-only compatibility facade remains available to existing callers. Active hand-draw target children use an additive optional target transcript and pending-choice marker; resolving one may open another supported target, which is retagged and replayed in the same draw frame and chronology. Potion-generation children now use the same draw-owned replay and continuation path, with chronology version 3 preserving version 1 and 2 snapshots that do not contain them. Supported pool-shuffle children now use the draw-owned transcript and replay path for distinct Common-card and Colorless generation, moving random Rare cards to hand, and returning the hand to a shuffled draw pile, with chronology version 4 preserving older snapshots that do not contain them. Supported `select_cards` children opened by a draw-hook action now use the existing semantic card-selection owner and draw-owned version 5 transcript; the source selection specification and outer continuation remain subject to the existing fail-closed allowlist. A supported TransformRandom selection child may continue into the existing card-transform owner, whose decisions are recorded in the draw-owned version 6 transcript and replayed against the live replacement pool. Existing pipeline-owned shuffle and card-selection choices keep their end-turn chronology. Other unsupported child-choice kinds remain open.

Supported random autoplay and replay-card shortlist children opened by a
draw-hook action now use the existing card-shortlist event and decision owner
and the draw-owned version 7 transcript. Their source support is checked after
each pick, and the active draw cursor resumes after the child operation.
Pipeline-owned shortlists retain their existing end-turn chronology.

Supported random hand-cost values and hand-card selections opened by a
draw-hook action now use their existing event owners and the draw-owned version
8 chronology. Each resume validates the selected cost or physical card against
the live source operation before continuing the active draw cursor. Pipeline-
owned hand-cost choices retain their end-turn chronology.

A selected `BeforeHandDraw` action may also suspend on its source operation's
semantic effect-draw ticket or drawn-card cost. The saved pre-draw cursor stays
with the semantic draw frame while the existing draw or cost owner resolves the
child; replay records it in the same version 8 chronology, then resumes the
saved pre-draw stage and continues into the hand draw. The requested count may
advance from the zero-count pre-draw frame only when that saved cursor enters
`DrawPlayerHand`; an already-active hand draw still requires its original
requested count. No event or snapshot schema changed. Other pre-draw nested
choice kinds remain fail-closed.

The engine's semantic automatic-hand-draw owner now has one handoff for
draw-hook results and nested draw or drawn-cost children. The hook and child
adapters pass the source and resumed run plus the classified boundary to that
owner, which validates the source request and publishes the resulting active,
pre-draw, or completed cursor. The pre-draw-to-active count transition and
active count preservation therefore use the same check. Child-specific
boundary validation remains with the draw-hook and choice owners.

Runtime controlled hand-draw remainder reads now pass the mechanics cursor through the stochastic owner's query, which validates the active `DrawPlayerHand` progress before projecting its count. The no-cursor overload remains only for the flattened compatibility snapshot projection.

The persistent semantic combat pilot also admits direct card-draw potion actions for Glowwater, Clarity, Cure All, Snecko Oil, and Swift. Their card identities use the existing provider-owned draw event and nested effect continuation; Snecko Oil's following hand-cost randomization stays with the existing semantic cost owner. The same path admits Restlessness, Slimed, Calculated Gamble, Finesse, Flash of Steel, Impatience, Master of Strategy, and Scrawl, including the implemented Colorless Restlessness card. The Restlessness cards retain their source enqueue conditions, Impatience retains its no-Attack condition, Calculated Gamble retains its source-queued draw count across the preceding discard, and Scrawl keeps the ordinary hand-cap behavior. These exact modeled effects preserve the physical-pile/provider boundary checks; other potion and card effects, opening draws, and Native cursor parity remain separate work.

Attack, Skill, Power, Touch of Insanity, Droplet of Precognition, Gambler's Brew, and Liquid Memories all enter through the existing semantic selection-potion admission. Attack, Skill, and Power reuse the full-pool shuffle, generated-card model event, and ChoicePool card-selection event. Touch of Insanity and Liquid Memories reuse the card-selection event with their source temporary-cost effects; Droplet of Precognition selects a physical draw-pile identity and commits its move through the existing deck adapter. Gambler's Brew uses the existing sequential selection event and resumes its draw for the selected count. The hidden pile is validated by the existing choice owner, and no chance-event or snapshot schema was added; other potion effects and Native cursor parity remain open.

Bottled Potential also uses the existing full-pool hand-return shuffle continuation. It returns the current hand to the draw pile, commits the selected permutation through the semantic deck adapter, resolves any after-shuffle relic insertions through the existing insertion event, then resumes the potion's five-card draw. This reuses the saved shuffle and draw owners without a new continuation schema; Native stream attribution and cursor parity remain separate audits. The `ResolveOperations` kernel now checks after every operation that no pending choice was left behind, enforcing that a suspending operation must return before its saved suffix can execute.

Gambler's Brew now uses the existing v2 sequential card-selection event to discard hand cards one at a time, then resumes the source `DrawCards` continuation for exactly the selected count. The narrow admission requires the saved potion-use completion event, no outer choice, and the source hand/discard specification; its draw remains with the existing semantic draw owner and no schema was added.

Orobic Acid now uses its existing source continuation without requiring a seeded-fixture identity. It reuses three sequential generated-card model events for its Attack, Skill, and Power, each with the validated source pool and unchanged hidden draw pile. Colorless Potion now enters the semantic pilot from any matching combat slot. Its exact generated-Colorless operation, `PotionUsed` completion, and empty nested continuation reuse the existing full-pool shuffle and ChoicePool selection owners; fixture setup remains available but is no longer required. Anointed's Rare draw-pile shuffle also enters from the physical source-card identity, matching its amount-ten operation, `CardPlayed` completion, and saved card-play frame; the seeded fixture remains optional for supplying Anointed and a Rare pile. Seeker Strike's three-card draw-pile shortlist also enters by exact source-card and selection specification, then reuses the existing full-pool shuffle and player-selection events. Its fixture remains optional for arranging the source card and pile. Alchemize now reaches the existing potion-generation event from its physical source-card identity, exact amount-one operation, and saved `CardPlayed` continuation; the fixture is optional for supplying the card. Broader potion effects, native acquisition, and Native cursor parity remain open.

The semantic combat pilot also admits the exact deterministic potion effects already modeled by the combat engine. The no-target set is Foul Potion, Neow's Ambergris, Blood Potion, Cunning, Binding, Ship in a Bottle, Heart of Iron, Gigantification, Shackling, Mazaleth's Gift, Radiant Tincture, Lucky Tonic, Block, Explosive, Explosive Ampoule, Dexterity, Fysh Oil, Energy, Strength, Regen, Duplicator, Flex, Blessing of the Forge, Fruit Juice, Speed, Liquid Bronze, Ghost in a Jar, Fortifier, and Stable Serum. Blood Potion and Fruit Juice apply their modeled heal and Max HP run effects when used during combat. Foul Potion deals its modeled 12 damage to all enemies and may complete combat through the existing reward boundary. Ambergris retains its modeled heal and queued extra turn through the ordinary action and turn pipeline. Beetle Juice, Powdered Demise, Poison, Weak, Vulnerable, and Fire also enter when the source action names a legal living enemy. These actions retain the ordinary combat boundary and hidden draw-pile checks under the controlled-chance guard. Distilled Chaos and the deterministic damage potions may reach the existing combat reward boundary, using its narrow engine-owned reward allowance. No chance owner or snapshot schema is added; draw, choice, outside-combat, and other potion effects remain separate work.

The semantic combat pilot also admits the existing effect-draw continuation when a player card triggers Gremlin Horn on `EnemyDefeated`, Ink Bottle on its third `CardPlayed`, Iron Club on its fourth `CardPlayed`, Unceasing Top when the play empties the hand, or Joss Paper on its fifth `CardExhausted`. Admission checks the pending one-card draw, exact saved relic subscriber and trigger, and active source card identity where the source event is `CardPlayed`. The suspended first draw requires a nonempty source pile; any later nested draw remains subject to its own continuation checks. Draw resolution uses the existing semantic draw event and replay owner without a schema change. The modeled trigger census found no additional relic `DrawCards` source; Native trigger and cursor parity remain separate audits.

Mad Science now enters the semantic pilot for each saved Tinker type and rider whose runtime effects are modeled. Wisdom's draw remains on the effect-draw continuation; Skill/Chaos suspends after its Block effect on the existing generated-card model event, guarded by the exact saved card, operation, and manual `CardPlayed` frame. Attack and Power riders use ordinary source-card resolution. The draw eligibility query still uses the runtime card definition, so only Wisdom is admitted as a draw card. No event or snapshot schema changed.

Brightest Flame also enters the existing effect-draw continuation from its physical Ancient reward card identity. The source operation draws before applying its modeled Max HP loss, so replay resumes the remaining card effects in source order without changing the Nature draw event or hidden-pile ownership.

A source census of modeled `DrawCards` operations now accounts for the standard and Colorless cards, Brightest Flame, Mad Science's Wisdom rider, direct-draw combat potions, and every currently modeled action-relic draw trigger. Those sources route through the existing semantic effect-draw, automatic hand-draw, autoplay, or selection continuation that owns their operation. This closes the modeled direct-draw admission list; additional game-content triggers and Native stream/cursor parity remain separate audits.

`SemanticPlayerActionEngineV111` now admits 32 modeled deterministic Colorless card actions, including legal enemy-target actions. Rend uses its modeled target-debuff count; Bolas and Thrumming Hatchet reuse the existing last-turn rebound hook and semantic draw-pile adapter. Calamity installs its deterministic power, whose later Attack trigger reuses the existing generated-card model owner. Nostalgia's attack and skill returns are admitted only when the completed action appends that physical source card to the draw pile under the active power's stack condition; the existing adapter commits the known insertion. Every action runs under the controlled-chance guard and validates the physical draw-pile order against the provider deck after resolution. Mayhem, Entropy, and Stratagem continue through their existing autoplay, turn-start selection, and recycle owners. Other random generation, direct draws, and interactive card choices remain with their specialized continuations.

A source-pool cross-check finds routes for all 53 cards in `PrototypeColorlessCards.Implemented`: 32 use the direct semantic player-action gate and 21 use the semantic pilot's draw, selection, autoplay, generation, or other specialized continuations. Of the 93 IDs in `NativeSilentCardPool`, all 88 models with implemented solo mechanics have a direct or specialized route. Blade Symphony, Concoct, Fade, Flanking, and Sneaky remain explicitly `MechanicsImplemented:false` and `MultiplayerOnly:true`. This is an admission census over the current model arrays; it does not establish Native pool acquisition completeness or RNG parity.

The same semantic player-action gate admits the modeled deterministic event cards Byrd Swoop, Peck, and Toric Toughness. Their damage and block effects use ordinary source combat resolution under the controlled-chance guard and preserve the existing hidden draw-pile checks; no chance owner or snapshot schema is added.
The gate also admits deterministic modeled regional cards Feeding Frenzy, Enlightenment, Exterminate, Squash, Luminesce, Frantic Escape, Toxic, and Beckon. Their power, cost, energy, damage, Sandpit, and exhaust effects stay in ordinary source combat resolution under the same controlled-chance and hidden draw-pile checks; no chance owner or snapshot schema was added. Beckon joins Toxic and Frantic Escape in the modeled playable status-card set; unplayable Burn, Dazed, Wound, Infection, and Wither remain excluded.

The saved ReferenceNature run-action path admits the modeled deterministic outside-combat potions at its supported map-choice, Shop, Rest, act-transition, non-event Reward, and pre-choice Event boundaries. It also handles their use while an Event has a pending full-belt replacement as one source mechanics step that consumes the potion and installs the pending event potion. Post-choice Event rewards retain deterministic potion actions in the controlled event-reward transcript. Entropic Brew uses the run-action continuation during ordinary and pre-choice Event use when no full-belt replacement is pending, and the event-reward continuation saves and replays its action while each empty-slot potion draw resolves. Combat use now enters the same typed run-action continuation: legacy runs use the ordered `reward`-stream potion pool, while Native run ownership uses the `player.rewards` law; when a Native combat sidecar is active, the run-effect cursor is mirrored to it before the remaining potion operations and published back with the completed action. Native pool ordering, trigger timing and seeded cursor parity remain subject to reference evidence. Stochastic full-belt replacement remains with its event-specific continuation.

Discovery's optional three-card generated offer now enters from its physical
source-card identity and exact `CardPlayed` continuation, reusing the existing
full-pool shuffle and ChoicePool selection owners.

Splash's optional three-Attack generated offer now enters from its physical
source-card identity and source-upgrade state, reusing the same full-pool
shuffle and ChoicePool selection owners.

Jackpot's three random zero-cost card additions now enter through its physical
source-card identity and the existing generated-card event. Each pick validates
the saved `CardPlayed` continuation, source upgrade level, and remaining-count
suffix before resuming the next generation.

Volley now admits its random-target action from its physical source-card
identity. Each paid X hit uses the existing target event after validating the
manual `CardPlayed` frame and remaining Volley operation sequence.

Jack of All Trades now enters its distinct Colorless-card shuffle from its
physical source-card identity and exact upgraded amount, reusing the existing
full-pool shuffle owner.

Secret Technique, Secret Weapon, Purity, and Scrawl now enter their source
card-selection prompts through the existing event and replay owner. Draw-pile
removals retain the known or contiguous-pool checks. Thinking Ahead resumes its
two-card draw and `MoveToDrawTop` choice through the existing draw and
selection owners.

Hidden Daggers now uses that same source card-selection owner for its two-card
hand discard, then resumes its queued Shiv creation. Bouncing Flask enters the
semantic pilot from its physical source card and resolves each repeated Poison
target through the existing saved random-target owner. Ricochet now enters from
its physical source card as well, resolving its four or five repeated hits
through that same owner according to its upgrade level. These paths add no new
provider or event schema; Native target-pool and stream parity remain open.
Concentrate uses the same selection owner for its three-card hand discard and
then resumes the queued energy gain.
Tingsha now reuses the random-target event when a manual Storm of Steel,
Shadow Step, Survivor, Hidden Daggers, Concentrate, Dagger Throw, Acrobatics,
Prepared, or Calculated Gamble play discards cards. Admission checks the live
Tingsha subscriber, exact discard event, physical card-play frame and
source-card destination, plus the queued Shiv-creation, power, Energy, draw, or
selection-completion suffix; each result resumes the original discard-event
order. End-turn hand-discard targets continue through the existing saved
automatic-pipeline target transcript. A source census of card-definition
discard effects finds these nine are all currently modeled direct player-card
origins; unmodeled mechanics and Native target-pool, stream, and cursor parity
remain open.
Tingsha targets nested under an autoplayed card also reuse the discard target
owner when the saved event is the exact live Tingsha subscriber and the
active autoplay series and automatic-pipeline boundary remain present.
Gambler's Brew and opening Gambling Chip also reuse the discard target owner
for each selected-card discard when the saved parent contains the matching
selected-count draw and its remaining discard queue still names cards in the
live discard pile. Gambling Chip also requires its exact opening subscriber and
cursor; Gambler's Brew requires its potion completion event. Queued Sly card
identities are checked against live Sly cards in discard, and Gambling Chip's
resulting Sly target uses that same exact opening boundary. Random-effect
targets nested within those Sly plays also require the active autoplay series
and the exact Gambling Chip subscriber in the saved continuation chain.
Tools of the Trade turn-start discards also reuse the target event when the
saved parent resumes its exact live `PlayerTurnStarted` power subscriber at
the post-dispatch cursor, with the remaining discard queue and Sly identities
validated against live cards and the power's stack bound.
Manual Distilled Chaos also admits Tingsha targets nested in its top-three
autoplay when the saved root has the exact `PotionUsed` completion and only
the remaining potion-sourced autoplay operations for live play-pile cards.
This route requires an idle Nature deck and no automatic pipeline. A source
census of the modeled potion effects finds Gambler's Brew and Distilled Chaos
as the only potion origins for card-discard events; both use the existing
selection or autoplay target continuation. Unmodeled potion mechanics and
Native target-pool, stream, and cursor parity remain open.
Knife Trap also enters from its physical source card and reuses the saved
autoplay-target continuation if an earlier Shiv kills the chosen enemy before
the remaining Shiv queue finishes.
Metamorphosis now enters from its physical source card and reuses the existing
typed generated-Attack model and draw-insertion events. Admission checks the
manual Exhausting card-play frame, the exact 3 (5 upgraded) operation and its
remaining-count suffix; generated models still use the implemented Silent
Attack pool. No event or snapshot schema changed.
Trash Heap's Distraction now reuses the generated-Skill model event after
validating its Exhausting card-play frame and exact source operation. Rip and
Tear reuses the random-target event for its two random-enemy hits after
validating the source card and remaining hit operation. These paths add no new
event or snapshot schema.
The six fixed-effect Trash Heap cards now use the semantic player-action
boundary. Dual Wield uses the existing card-selection event after checks bind
the pending choice to its physical source card, upgraded copy count, and exact
eligible hand candidates. Rebound's draw-pile return is admitted only when
the played physical card is appended and the active return powers consume the
expected stacks. The shared draw-pile adapter remains the mutation owner; no
event or snapshot schema changed.
Shadow Step now uses the card-selection owner for its hand discard and queued
power application, while Echoing Slash and Outbreak use the ordinary
deterministic semantic player-action boundary.

Apparition, Apotheosis, Maul, and Whistle from the modeled Glory Ancient
rewards now use that same deterministic player-action boundary. Enthralled,
the source-playable no-effect curse added by Blood-Soaked Rose, is admitted as
well. These actions stay in source combat resolution under the
controlled-chance guard; they do not introduce a provider-owned draw-pile
change or a new chance continuation. Wish remains with its card-selection
owner, while unplayable Ancient curses remain excluded. Pael's Horn's Relax
card also uses this boundary for its fixed Block and next-turn powers.

After a semantic `BeforeHandDraw` choice resolves, the common automatic-pipeline resumer now replays the saved `DrawPlayerHand` stage and re-enters the same handoff at its advanced cursor. Remaining supported stages and terminal reward publication therefore continue through the shared path, while a newly suspended choice remains pending for its owning adapter.

`ResumeControlledTurnAfterIntents` now publishes its completed `AdvanceTurn` cursor back to `ResumeAutomaticPipelineContinuation`. The shared handoff resumes subsequent automatic stages and preserves the existing stop-before-semantic-hand-draw boundary used by the end-turn facade.

Semantic card selection also supports Neow's Fury's optional discard-pile-to-hand choice through the existing source card-selection event and replay owner; it does not change the hidden draw pile. Neow's Abundance reuses the full-pool shuffle and ChoicePool selection continuation for its required three upgraded Power offers, guarded by the exact source card instance and effect. Gambling Chip's exact opening `OpeningPlayerTurnStarted` prompt remains supported in Nature mode. Its sequential event uses schema v2 to fingerprint the accumulated card prefix while ordinary card-selection events retain v1 identity. Completing the prompt runs the saved discard-event continuation, then draws the number selected; only the matching opening cursor and its tagged semantic draw or draw-cost continuation are admitted. Toolbox's exact turn-one `BeforeHandDraw` subscriber and Choices Paradox's exact turn-one `OpeningPlayerTurnStarted` subscriber now use the existing full-pool shuffle event, followed by a ChoicePool card-selection event at their respective saved opening cursors. Each offer guard reuses its source pool and validates the generated physical instances, including Choices Paradox's retained character-card offer, before publishing the pick. The opening recycled-draw Stratagem selection uses the existing draw-pile selection event and saved draw operation described above. Other opening selections and nested hook choices remain unsupported.

Entropy's `PlayerTurnStarted` prompt now uses the existing semantic card-selection event to choose its eligible hand identities, followed by the existing semantic card-transform event for each replacement. When Native combat run streams are present, its replacement event uses `run.combat_card_generation`; prototype-only runs keep `prototype.combat`, and saved prototype-labeled decisions remain replayable. The automatic end-turn boundary stores and replays these decisions in source order while the saved event-dispatch continuation remains suspended. The predicate admits only the exact Entropy subscriber and only when all offered cards have implemented transform pools; probability status and Native cursor parity remain unverified.

Turn-start hand-discard prompts also use the existing semantic card-selection event. This includes the source-driven discard count from player powers and Tools of the Trade's per-stack selection; each continuation validates the saved `PlayerTurnStarted` dispatch, current hand identities, and resulting discard action before the shared end-turn chronology records or replays it. Other turn-start selection prompts remain unsupported.

Toasty Mittens' `OpeningPlayerTurnStarted` and later `PlayerTurnStarted` prompts now share the semantic card-selection event. The continuation admits only its exact relic trigger, hand support, and opening or turn-start cursor, and verifies that exhausting one card resumes the saved `ApplyPlayerPower(strength, 1)` operation before the subscriber list continues. This action consumes no RNG; other event-subscriber selections remain unsupported.

Toolbox's turn-one generated offer now runs as a typed pool shuffle at the opening `BeforeHandDraw` subscriber, followed by the existing semantic `select_cards` event over the generated ChoicePool. Choices Paradox uses the same continuation at `OpeningPlayerTurnStarted`: the full eligible character-card pool is shuffled on the card-generation stream, five free, retained physical offers are created, and the player must take one. Each path keeps its own opening cursor until the required selection or optional Toolbox pick/skip; restore resumes the saved subscriber without redispatching it. Both admissions require the exact relic trigger, generated-card operation, source pool, and generated offer attributes. No new event schema or continuation owner is introduced.

Blessed Antler's turn-one `BeforeHandDraw` hook now uses the existing typed draw-insertion event for each of its three Dazed positions. The continuation admits only the saved relic subscriber at the opening `DrawHand` cursor, validates the fixed-card operation and remaining count, then resumes the same subscriber until all insertions finish before drawing the hand. The semantic-pilot history records each position through the existing insertion owner, which commits the physical and provider states together; other `BeforeHandDraw` insertion effects remain unsupported.

Biiig Hug's after-shuffle Soot insertion now also resumes inside a Nature-owned automatic hand draw. Admission checks the saved semantic draw frame against the active `DrawPlayerHand` count and validates the ordered after-shuffle relic suffix through the existing insertion owner. Resolving the insertion resumes the remaining hand draw through the same automatic pipeline; no event or snapshot schema changed.

From turn two onward, Hello World's `BeforeHandDraw` hook now uses the existing typed full-pool shuffle for its distinct character Common-card generation. The operation count follows the live power stacks, while the selected count follows the saved turn-start stack snapshot, capped by the eligible pool size. Native run-stream snapshots use `run.combat_card_generation`, matching the source shuffle call. Each swap remains in the existing nested end-turn pool-shuffle chronology; after the final swap, the common automatic-pipeline resumer resumes the saved subscriber before the hand draw continues. Admission checks the exact Hello World trigger and source pool; other automatic `BeforeHandDraw` shuffle hooks remain unsupported.

Crossbow's `PlayerSideTurnStarted` effect now admits both the turn-one opening `BeforeHandDraw` cursor and the turn-two-or-later `DrawPlayerHand` cursor for its existing typed generated-card model choice. Source-route Nature entry attaches the already-committed pile before opening-stage replay, so this exact turn-one prompt can be published while its known opening order stays fixed. After the generated Attack is selected, the saved opening or automatic pipeline resumes before the hand draw. Restore replays the trigger and selection before drawing. Later subscribers are admitted only when their saved relic triggers and effects are source-reachable deterministic energy, gold-for-energy, block, or player-power operations; unsupported random or choice effects fail closed. Native-backed instances of this and other supported generated-card operations use the source `run.combat_card_generation` label while retaining `UNVERIFIED` probability status. Earlier `prototype.combat` decisions remain valid during restore; Native cursor parity remains open. Other automatic-pipeline generation continuations remain unsupported.

The current repository's automatic-trigger census accounts for the modeled stochastic opening choices, hand-draw-entry hooks, and turn-start/pre-play continuations: Choices Paradox and Gambling Chip at `OpeningPlayerTurnStarted`; Toolbox's ChoicePool offer, Jeweled Mask's random Power instance, Blessed Antler's Dazed insertions, and Hello World's Common-card pool shuffle at `BeforeHandDraw`; Entropy and Tools of the Trade at `PlayerTurnStarted`; Toasty Mittens at opening and later `PlayerTurnStarted`; Crossbow at `PlayerSideTurnStarted`; Biiig Hug's after-shuffle Soot insertion; and Mayhem, History Course, Whispering Earring, and Imbued autoplay through their saved automatic-pipeline cursors. These use the existing card-selection, draw-pile identity, draw-insertion, pool-shuffle, generated-card, and random-target owners. The same source census found only deterministic hand creation for the modeled Infinite Blades, Nightmare, and Ninja Scroll `BeforeHandDraw` hooks. This closes the repository inventory for these trigger families, not installed-game source completeness: additional Native hooks, trigger order, and stream attribution still require the Native reference mod or Fidelity Inspector. Unmodeled continuations remain fail-closed.

Source-route opening combat now gives `DrawOpeningHand` the shared semantic-draw suspension profile when its already committed pile is held by a Nature deck. Known opening identities are consumed through the same `DrawCards` kernel without creating a second draw request, then player/combat mechanics publish through `PublishDrawMechanicsState`. `PrototypeDrawCardsBoundaryV111.cs` owns that shared publisher alongside tagged-result construction and validation. Opening semantic draw, draw-cost, nested `CardDrawn`, after-shuffle insertion, and recycled-draw Stratagem selection boundaries use the existing common draw dispatcher or card-selection owner. The opening Stratagem choice requires the exact `AfterHandDraw` cursor, current draw-pile support, expected pick count, and one saved hand-draw operation. Nested choices attach the remaining draw operation to their existing choice continuation, and a nested choice after the final card needs no saved draw operation. Nature recycle keeps its shuffled-set constraints in the semantic deck owner; after-shuffle relic insertion resumes through the existing typed insertion continuation. Native recycle order and source-hook completeness remain separate evidence questions.

`PrototypeDrawCardsContinuationV111.cs` also owns the provider-neutral saved automatic hand-draw kernel: it validates the active count against the `DrawPlayerHand` cursor, clears progress through the shared publication boundary, and re-enters `DrawCards` with the caller's provider policy and local RNG. Controlled and semantic adapters now use this same operation before interpreting their tagged result.

The automatic hand-draw owner also classifies active and pre-draw saved progress in one shared check before suspending at Stratagem or nested `CardDrawn` choices. Both paths reject a saved hand-draw tag that no longer belongs to its source stage.

Semantic end-turn routes a supported choice resume at `DrawPlayerHand` through the same validated hand-draw entry used after next-turn intent resolution. Turn-one Crossbow resumes through its saved opening stage and reaches the opening hand draw through the shared opening-combat resumer. The source predicate validates Crossbow's exact relic trigger and generated-card operation at either cursor before exposing the choice.

Whispering Earring's first-turn opening autoplay now uses the shared automatic
target boundary only when its saved `OpeningStage.Complete` continuation contains
the exact relic driver and an autoplay card-play frame. The same cursor admits
the exact pre-play target operation. Existing typed choice owners resume nested
card effects before the opening pipeline completes; other opening-stage
choices remain separately gated.

The opening target boundary also admits the initial target for Mayhem's turn-one
top autoplay at `AfterAutoPrePlayPolicy`, but only when the pending target names
the first saved Power-sourced `AutoPlayCombatCard` operation and its live enemy
support matches. The common target resolver can then resume the staged autoplay
and opening pipeline through their existing owners; no event or snapshot schema
changed.

The same Mayhem opening cursor now admits a nested Sly autoplay target when its
discarded source card is live, carries the Sly keyword, and targets an enemy;
the saved continuation must also retain an active autoplay card-play series
whose source card has left the play pile to resolve its effects. Nested
ordinary random-effect targets from that same active autoplay series use the
existing effect-target continuation as well. Whispering Earring and Imbued
retain their separate source-specific opening checks.

History Course autoplay after `AutoPrePlay` and Whispering Earring's opening
autoplay both pass random target selection through the shared
`ResolveRandomEnemyTarget` owner. With a Native combat sidecar, that owner forks
and advances `run.combat_targets`, then publishes the updated sidecar; ordinary
step finalization reconciles the matching run-level bundle. Their local
`prototype.combat` bundle remains a separate provider and is published with
the mechanics result. This records the existing handoff without claiming
cross-provider probability or cursor parity.

The same opening target boundary now admits Imbued autoplay when the saved
cursor is `LateRelicAutoPlay` and the continuation's driver queue matches the
still-active triggered Imbued cards in persistent-card order. A target must
name the queue head; a nested choice must retain the active Imbued card's
autoplay frame and the exact remaining driver queue, including after that
source card leaves the play pile to resolve its effects. The existing typed
target and nested-choice owners resume the saved sequence without rebuilding
or reordering it.

`SemanticChoiceResumeV111` applies recorded nested draw and cost,
insertion, generated-card and potion, transform, shortlist, pool-shuffle,
hand-cost-selection, card-selection, draw-hook, and Knowledge Demon decisions
through one mechanics dispatcher. Semantic end-turn restore uses it to replay
the full nested chronology, and live end-turn choices enter it through one
resume helper before committing that chronology. Semantic pilot resolution
uses `ResumeAndPublishNestedChoice` for card selection, generated card and
potion, transform, cost, shortlist, pool shuffle, hand-cost selection, and
nested effect draw/cost outcomes. It dispatches mechanics through the shared
owner, continues a saved end-turn frame when present, then publishes pilot
history and the pending-frame transition together, taking the run from the
completed end-turn continuation when it advances beyond the immediate choice
resume. Non-end-turn draw insertion now publishes through the same pilot helper
while retaining its special paired-zone commit and validation path; end-turn
insertion continues through its hand-draw facade. Live card-selection validates
the typed event and action first, then uses
the same dispatcher that replay uses. End-turn and pilot target resolution
also share `SemanticChoiceResumeV111.ResumeTarget`, while preserving
the target-specific transcript and boundary chronology.
`SemanticChoiceResumeV111` exposes typed overloads for every nested choice:
draw and cost, insertion, generated card and potion, transform, shortlist,
pool shuffle, hand-cost selection, card selection, draw hook, and Knowledge
Demon. The end-turn transcript switch delegates each saved kind to those same
overloads. Deferred peek and scry replay also enters this owner through its
typed deck-reveal decision, so pilot publication and history replay share the
same checkpoint and decision-copy boundary. Standalone draw and Stratagem
facades call the relevant overloads directly instead of constructing the
end-turn transcript union. The draw
facade keeps draw counts and typed histories and preserves its stricter
engine-owned reward policy for controlled effect draws. Its ordinary and
standalone effect-draw cost continuations enter the shared resume owner, which
keeps their existing reward guard and remaining-count rules. End-turn restore
retains per-kind chronology and pending-choice guards, while the shared
continuation validates each typed decision against its source event before
mechanics replay. Knowledge Demon replay now consumes its typed decision
directly: it re-derives the saved offer, validates the current legal action
payload, then applies that action without repeating the live-selection step.
Card-selection replay does the same through its typed resume: it re-derives
the pending source event, validates the saved selected identities against the
live support, and applies that action. The string-action `Step` entry point
composes selection with the same mechanics resume core.
`ControlledChanceGuardV111.ExecuteGuarded` owns the synchronous guard scope
and optional engine-owned Native-provider and prototype-reward allowances.
Typed semantic-choice resumes, semantic-pilot and end-turn transitions,
controlled-playable resumes, semantic combat-draw entry/resume, controlled
run-action and event-choice continuations, and the Native-policy action path
use this runner; their source validation, result policies, and provider
journals remain with their existing owners.
Cross-provider mechanical equivalence remains open.

Pending controlled random-target validation shares one live-support check
across semantic target boundaries, combat-state validation,
controlled-playable validation, and target resume. It compares ordered living
enemy identities and Sly/autoplay source identities against the suspended
continuation; a Sly source must still be in discard, carry the live Sly
keyword, and have an enemy target definition. The choice's saved source-card
identity and destination must also match its outer continuation, and an
effect target must retain the first unresolved action-target operation.

`PendingCombatChoiceState.HasValidControlledPayloadShape` owns the
discriminator invariant for the flattened controlled-choice snapshot fields.
Each controlled choice ID must match exactly one typed payload; effect-draw
cost is the only supported paired payload, with its matching semantic draw
frame. The four Semantic Stratagem and CardDrawn-hook mode flags are mutually
exclusive and cannot overlap typed payloads. Combat-state validation and
semantic draw-cost validation share this check without changing snapshot
fields.

The shared resume path propagates the SiteId from each typed draw and
draw-insertion event into automatic-pipeline boundary handling, preserving the
actual supported insertion site in diagnostics and continuation handoff.

The semantic pilot now constructs typed decisions for nested automatic-pipeline
`CardDrawn` and Knowledge Demon actions, then uses
`ResumeAndPublishNestedChoice`; the end-turn owner appends each decision to its
existing chronological boundary transcript. When a saved end-turn frame exists,
the pilot enters the end-turn owner's validate-then-resume path before publishing
the resulting run and frame together. Standalone and automatic hand-draw hook
progress remains with the draw facade.

Generic event-choice resolution now uses local prototype and optional Native run RNG bundles for event effects and event deck-card choices, publishing provider updates with each completed effect checkpoint and the resulting event state. An early lethal effect also retains the updated world bag and card-instance cursor before ending the run. A controlled chance suspension retains the cursor from the start of its current effect in the saved partial state, so resumption does not publish a partial effect.

Reward-step resolution now reads defensive prototype and optional Native run RNG projections, passes the local bundles through card grants and relic-acquisition hooks, and publishes the advanced providers with ordinary reward progress, completed rewards, or lethal relic deck-choice outcomes. Controlled reward continuations, Pael sacrifices, and Driftwood rerolls remain owned by their dedicated transitions.

Combat entry now reads defensive run RNG projections and keeps encounter selection, pre-combat potion relics, initial shuffle/HP draws, and first-intent commitment on local bundles. The entry publishes the latest prototype and Native run cursors alongside the initialized combat and intent continuation; resuming that continuation reprojects its Native run cursor through the run owner.

Combat card plays, potion use, automatic death prevention, and combat-win dispatch now use local prototype bundles and publish them with their player/combat results. Combat-win dispatch publishes before persistent relic hooks, and Mad Science reopens the resulting projection so its deck improvement follows those hooks in source order.

Combat Imbued autoplay and the opening player-turn event dispatch also use local prototype bundles and publish their cursor with the resulting combat state, including suspended autoplay or event choices.

Automatic turn steps now pass a local prototype bundle through combat-event dispatch, hand effects, enemy status/death summons, enemy actions, intent commitment, and hand draw. They publish the cursor with each step result or before handing a suspended draw to its saved continuation; late relic autoplay refreshes its local projection after the dedicated transition returns.

Controlled recycle resumption now uses a local prototype bundle for after-shuffle relic hooks and remaining hand draws, publishing it before transferring to a stratagem or hand-draw continuation.

Combat resolution now reads Native combat RNG through its defensive projection. Native stream consumers fork run streams locally and attach the updated combat RNG through `CombatStochasticStateV111.WithNativeRng`, so formation checks and Native combat choices do not expose the owner's mutable sidecar.

Pending combat card selections and controlled target/card-draw resumes now use local prototype bundles and publish them before resuming nested choice or automatic-pipeline continuations.

Opening combat event dispatch and the opening hand draw also use local prototype bundles and publish them with the corresponding combat stage result.

Room completion relic hooks, Pael's rest and combat-reward alternatives, and act-transition map generation now publish local prototype bundle updates with the state they produce. The run-start and hypothetical proposal paths read defensive projections when they copy or replace streams.

Controlled combat cost, potion/card generation, card shortlist, and pool-shuffle continuations now resume from local prototype bundles and publish the updated cursor before the automatic pipeline continues.

Semantic draws, drawn-card cost hooks, controlled draw insertions, and automatic hand-draw resumes now carry a local prototype bundle through their nested draw/event continuations and publish it with the continuation state. Native semantic recycle reads the defensive combat RNG projection before forking its Native stream owner.

The Core callsite audit now finds no direct mutable run or combat RNG owner views outside their stochastic owner implementations. `RunStochasticStateV111` owns and validates `SemanticOtherChanceStateV111`; the pilot's `OtherChance` member remains its wire-compatibility projection. Exact run snapshots now use the versioned v4 mechanics/provider envelope while preserving v1–v3 load compatibility.

Automatic enemy-action continuations now use the existing chance-state owner to persist optional ordered candidate labels beside their index prefix. Source card theft labels physical card instances, summon-pool labels retain the enemy model and pool slot, enemy draw/discard insertion labels the generated card and its gap among physical pile identities, and other registered enemy-action sites label their bounded values. The semantic end-turn event includes the labels in its fingerprint and can resolve by `candidate_label`; `selected_index` remains compatible. The end-turn stochastic owner records the label support alongside each selected index and validates that support during source replay. During replay, the controlled chance owner exposes the selected label for the exact consumed request, so card theft applies the selected physical instance ID, summon-pool selection applies the labeled enemy model after verifying its live pool slot, and draw/discard insertion resolves the saved physical gap against the current pile before committing it through the semantic zone adapter. The index remains in the provider transcript, and labels-free legacy snapshots keep their index path. This adds semantic result identity to the enemy-action boundary without changing its source RNG request or claiming Native stream parity.

The controlled-playable session also persists optional ordered candidate labels beside its chronological live index transcript. It derives labels from the live identity support for enemy actions, generated cards, potions, transforms, card-model insertion, random hand-card selection, targets, and weighted intents; restore compares each saved vector with the support reached at that replay boundary. Numeric-only choices retain null entries. Snapshots without the optional field keep the legacy index-only replay path, while new snapshots reject reordered same-sized labeled support.

Pending controlled random-target validation now shares one live-support check across semantic target boundaries, combat-state validation, controlled-playable validation, and target resume. It compares ordered living-enemy identities and the Sly/autoplay source identity against the suspended continuation.

A selected action from an active semantic `CardDrawn` hook may suspend on its
source card's semantic effect-draw, drawn-cost, or fixed draw-insertion ticket
when the automatic hand-draw cursor still carries the same requested and
remaining counts. The semantic combat-draw and end-turn facades reuse the
existing draw, cost, insertion, transcript, and replay owners, preserving the
outer hand-draw continuation without adding choice or snapshot fields.

When a `BeforeHandDraw` action opens a fixed draw-pile insertion, the
semantic draw frame retains the existing pre-draw cursor and insertion
transcript. Resolving the insertion resumes the ordinary hand-draw step
through its pipeline owner; no choice or snapshot fields were added.

## Reading order

1. This architecture contract.
2. [Roadmap and acceptance tests](STOCHASTIC_ARCHITECTURE_ROADMAP.md), with the current [site and state ownership inventory](STOCHASTIC_SITE_OWNERSHIP_INVENTORY.md).
3. [Desired RNG implementation](DESIRED_RNG_IMPLEMENTATION.md) and [RNG model](RNG.md).
4. [Nature pilot API](NATURE_P0_SEMANTIC_JSONL_143.md) and [migration status](DESIRED_RNG_MIGRATION_STATUS.md).
5. [Native Fidelity Inspector plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md) and [status](NATIVE_FIDELITY_INSPECTOR_IMPLEMENTATION_STATUS.md).
