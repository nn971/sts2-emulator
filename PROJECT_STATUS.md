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
- continue Silent/content breadth only where it creates strategic variety;
- richer map topology/pool-history rules if AI experiments show value;
- pin and inventory a reference STS2 build;
- native STS2 data translation/import;
- native RNG/timing parity;
- reference bridge and differential testing;
- performance work driven by representative AI/search workloads;
- production cross-language binding and compatibility negotiation.

Search, strategic databases, and AI learning remain outside this repository and belong in the
future parent project.


## Native fidelity target

The first pinned native oracle is STS2 `v0.111.0` / commit `41cef1ea`, fingerprint
`3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e`.

Implemented native-fidelity groundwork now includes build fingerprinting, metadata inspection,
the native-neutral reference-trace v0.2 contract, and a single-DLL passive reference recorder
staged outside the clean oracle. The recorder still requires its first live in-game validation.
