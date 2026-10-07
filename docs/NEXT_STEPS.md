# Immediate next steps

The emulator now has a complete-run skeleton and a reasonably expressive prototype mechanics
kernel. The near-term priority shifts from inventing engine primitives to making prototype runs
strategically varied enough to exercise the future AI project.

Fidelity and speed remain refinement tracks; prototype generation rules and semantics stay
explicitly versioned and replaceable.

## Completed foundation

The current branch already has:

- end-to-end seeded Silent runs;
- persistent graph-shaped act maps;
- deterministic named RNG streams;
- card-selection continuations;
- discard/exhaust/generated cards;
- fixed and X costs;
- repeated and all-enemy effects;
- generic contextual combat events;
- shared player/enemy powers;
- stateful relic event triggers;
- combat-local card/relic/potion state;
- ordered enemy move programs;
- structural invariants and a 500-seed whole-run sweep;
- act/floor/history-sensitive encounter selection;
- prototype rarity-aware card rewards.

See [PROTOTYPE_SCOPE.md](PROTOTYPE_SCOPE.md).

## Sprint A — finish prototype run generation

Most first-pass run-generation structure is now implemented:

- rarity/price-aware shops and one-use card removal;
- weighted event eligibility/history;
- expanded potion/relic pools;
- act-specific elite/boss content;
- floor-rule-driven map generation with constrained special-room placement;
- a guaranteed pre-boss rest layer;
- heal/upgrade/permanent-max-HP rest decisions.

Remaining work should focus on richer topology and pool-history behavior only when it creates useful
strategic distinctions for AI experiments.

## Sprint B — enlarge the Silent content slice

Add content in mechanic families rather than card-by-card order:

- attack/damage archetypes;
- poison;
- discard/draw;
- Shiv/generated-card play;
- defensive/dexterity;
- powers/reactive triggers;
- energy and X-cost interactions.

Add enough normal enemies, elites, bosses, potions, relics, and events that different routes and
reward choices produce materially different runs.

Prefer data definitions and reusable semantic effects. Add a new primitive only when several
content entries genuinely need it.

## Sprint C — expose an AI-oriented environment boundary

The first prototype adapter is complete:

- `prototype-ai-v0` observation schema;
- stable action IDs independent of legal-action ordering;
- reset / observe / step / fork;
- sibling expansion for search;
- observation and canonical-state hashes;
- AI observe/expand microbenchmarks;
- deterministic multi-run sweep reporting;
- machine-readable prototype capability manifest.

The next step is to consume this boundary from the parent `sts2-ai` repository and let real search
workloads determine which batching/performance improvements matter.

## Sprint D — parent AI integration

The prototype AI boundary is ready to consume from the parent `sts2-ai` project. Keep strategy,
search, and training outside this repository. Emulator work should stay focused on exact mechanics,
stable state/action semantics, and measured simulation performance.

## Sprint E — complete Act 1 combat

This is the active fidelity milestone.

The target is both native first-act regions, **Overgrowth** and **Underdocks**, comprising 42 pinned
combat encounters and 51 distinct monster models. Track monster mechanics, formation/slot semantics,
encounter-pool semantics, Ascension scaling, and oracle coverage independently.

Completed reusable Act 1 foundations include:

- native HP ranges and typed Ascension deltas;
- source-shaped enemy AI state graphs;
- `CannotRepeat` and `UseOnlyOnce` branch rules;
- enemy Strength and multi-hit attacks;
- player Vulnerable / Weak / Frail with owner-turn duration and multiplicative combat modifiers;
- source-backed Seapunk, Twig Slime (S), Assassin Raider, Snapping Jaxfruit, and Mawler.

Continue by mechanic dependency rather than encounter order:

1. conditional/slot-aware AI needed by Nibbit and Toadpole;
2. exact `CanRepeatXTimes` semantics before a production user depends on it;
3. summon/despawn/death primitives;
4. status-card creation and other shared Act 1 effects;
5. elites and bosses after their reusable mechanics exist;
6. native encounter formations and pools after participating monster models are trustworthy;
7. representative A0/A10 native differentials, then an Act 1 deterministic sweep.

Static sources come first: use the commit/blob-pinned Spire Codex corpus and the locally decompiled
pinned `sts2.dll`; use live Loadout-assisted oracle captures for runtime ordering, conditional
formation behavior, and RNG questions that static evidence leaves unresolved.

See [ACT1_COMBAT_COMPLETENESS.md](ACT1_COMBAT_COMPLETENESS.md),
[REFERENCE_DATA_STRATEGY.md](REFERENCE_DATA_STRATEGY.md), and
[pass 024](reference-builds/v0.111.0-mechanics-gap-024.md).

## Sprint F — performance loop

After `sts2-ai` supplies representative workloads:

- benchmark full-run rollouts and branch-heavy search;
- profile state copying/forking and event dispatch;
- optimize layouts and structural sharing;
- add batch/parallel APIs;
- track memory per branch and transitions per second;
- evaluate NativeAOT/interoperability only after profiles justify it.
