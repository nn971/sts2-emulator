# Project status

**Stage:** whole-run prototype development.

Implemented on the current prototype branch:

- emulator charter and subrepo boundary;
- deterministic engine interfaces and canonical hashing;
- typed whole-run world state;
- restrictive Silent ruleset/content catalog;
- deterministic prototype RNG with named streams;
- persistent floor-constrained act DAGs with stable future routing;
- normal, elite and boss combats;
- card play, draw/discard/shuffle, block, simple statuses and enemy turns;
- potions and relics;
- combat rewards and persistent deck growth;
- rarity-aware shops, weighted events, and heal/upgrade/max-HP rest sites;
- three-act transition flow;
- terminal victory/death;
- suspendable combat card-selection prompts with resumable effect continuations;
- combat-local/generated card identities and exhaust-on-use;
- fixed/X costs, repeated effects, and all-enemy targeting;
- player powers with ruleset-ordered turn-stage triggers;
- enemy moves expressed as ordered effect lists;
- generic combat events with contextual targets/amounts and bounded trigger recursion;
- one ordered subscriber system for player powers, enemy powers, and stateful relics;
- combat-local potion mirrors with atomic consumption;
- act/floor/history-sensitive weighted encounters;
- prototype rarity-aware card rewards;
- weighted event eligibility/history;
- rarity-aware shops with one-use card removal;
- expanded Silent card/potion/relic pools and act-specific normal/elite/boss encounters;
- player-facing `prototype-ai-v0` observation/action adapter with stable action IDs and sibling expansion;
- machine-readable capability manifest;
- CLI whole-run smoke and deterministic multi-run sweep drivers;
- deterministic whole-run smoke tests;
- trace/diff and benchmark scaffolding.

Prototype semantics deliberately use `proto.*` content IDs and carry no native-fidelity claim.
See `docs/PROTOTYPE_SCOPE.md`.

Still to do:

- integrate the prototype AI adapter from the parent `sts2-ai` project;
- replace prototype content/generation from the pinned structured reference corpus;
- translate exact Silent mechanics from structured data plus decompiled source;
- native RNG/timing parity;
- legal-action and nested-choice parity;
- focused reference-bridge differential testing for unresolved runtime questions;
- performance work driven by representative AI/search workloads;
- production cross-language binding and compatibility negotiation.

Search, strategic databases, and AI learning remain outside this repository and belong in the
future parent project.


## Native fidelity target

The first pinned native oracle is STS2 `v0.111.0` / commit `41cef1ea`, fingerprint
`3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e`.

Implemented native-fidelity groundwork now includes build fingerprinting, metadata inspection,
the native-neutral reference-trace v0.2 contract, a single-DLL passive reference recorder staged
outside the clean oracle, emulator-side structured probe auditing for history/state/RNG shapes, and
a static-first reference-data strategy.

The exact Spire Codex beta archive for `v0.111.0` has been verified to contain structured cards,
characters, relics, potions, powers, monsters, encounters, events, acts, ascensions, afflictions,
enchantments, keywords, intents, orbs, modifiers, rest-site options and progression metadata.
The corpus sync is pinned to Spire Codex commit
`b59c6f96043051afee8683237d04f943309ced35` and verifies per-file Git blob hashes.

`ReferenceCorpus`, `ReferenceMechanicsGapAnalyzer`, and `ReferenceSourceSearch` now provide local
querying, Silent mechanic-gap analysis, parser-artifact warnings, and line-oriented search over the
one-time ILSpy source tree. The external corpus and decompiled source are deliberately gitignored
and must not be redistributed. See `docs/REFERENCE_DATA_STRATEGY.md` and
`docs/reference-builds/v0.111.0-mechanics-gap-001.md`.

The first safe static corrections have landed: the starter relic is Ring of the Snake; Blade Dance
Exhausts; Skewer is 8×X (+3); Slice is 6 (+3); and Sucker Punch is 8 (+2) with Weak 1 (+1).

Static-first Silent mechanics work has now progressed through six gap passes. The engine includes
source-backed random-target attacks, Sly, universal card keywords, card/local cost modifiers,
turn-scoped and delayed powers, draw events, native Power-card removal from combat, whole-card
play-series replay for Burst (including repeated nested choices), and Tools of the Trade's
hand-draw/post-turn-start discard policy. See
`docs/reference-builds/v0.111.0-mechanics-gap-006.md`.

The next structural target is a typed delayed-card snapshot/payload, motivated by Nightmare's native
behavior: clone the selected card at selection time, store that clone in an instanced power, and
clone it into Hand before the next normal hand draw.


## First live native capture

The v0 passive recorder has now been validated in-game on two Ascension-0 Silent combats against
the pinned v0.111.0 / 41cef1ea oracle. The 54-record JSONL contained two coherent
setup/start/turn/win/end lifecycles and no capture failures.

The capture exposed three concrete next needs now implemented in probe v1:

- Player -> Creature discovery for player HP/block/power state;
- field-level projection of native SerializableRng state;
- passive CombatHistory.Changed capture for card-play/damage/energy/potion/monster-move granularity.

See `docs/reference-builds/v0.111.0-capture-001.md`.

A second live capture validated full Player -> Creature coverage, full SerializableRng state,
21 matched card-play start/finish lifecycles, one potion use, and six end-turn actions. It also
showed that card-finish and potion-used history entries are lifecycle markers rather than universal
decision boundaries, and that Survivor introduces a nested player-selection decision inside a card
play.

`ReferenceProbeActionExtractor` now emits evidence-backed observed action envelopes without
promoting gaps to parity claims. Probe v2 adds event catalogs plus richer card/potion/player state to
discover the native choice surface. See `docs/reference-builds/v0.111.0-capture-002.md`.
