# Project status

**Stage:** whole-run prototype development.

Implemented on the current prototype branch:

- emulator charter and subrepo boundary;
- deterministic engine interfaces and canonical hashing;
- typed whole-run world state;
- restrictive Silent ruleset/content catalog;
- deterministic prototype RNG with named streams;
- generated floor-by-floor map choices;
- normal, elite and boss combats;
- card play, draw/discard/shuffle, block, simple statuses and enemy turns;
- potions and relics;
- combat rewards and persistent deck growth;
- shops, events and rest sites;
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
- expanded potion/relic pools and act-specific later encounters/bosses;
- CLI whole-run smoke driver;
- deterministic whole-run smoke tests;
- trace/diff and benchmark scaffolding.

Prototype semantics deliberately use `proto.*` content IDs and carry no native-fidelity claim.
See `docs/PROTOTYPE_SCOPE.md`.

Still to do:

- compile/CI hardening of the prototype branch;
- broaden the semantic primitive set and content coverage;
- richer map topology and encounter generation;
- card-selection/discard/exhaust/temporary-card prompts;
- powers/relic triggers and general trigger scheduling;
- native STS2 data import;
- native RNG/timing parity;
- reference bridge and differential testing;
- performance work after representative AI workloads exist;
- production language binding.

Search, strategic databases, and AI learning remain outside this repository and belong in the
future parent project.
