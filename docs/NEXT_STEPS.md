# Immediate next steps

The near-term goal is a **useful complete-run emulator first**. Fidelity and speed are refinement
tracks; architectural choices should still keep native data, RNG and timing replaceable.

## Sprint 1 — stabilize the Silent whole-run prototype

1. Keep a seeded run playable from start to victory/death.
2. Exercise every top-level phase in tests.
3. Make invalid actions fail clearly.
4. Keep all provisional content under the `proto.*` namespace.
5. Preserve deterministic save/fork/hash behavior.

See [PROTOTYPE_SCOPE.md](PROTOTYPE_SCOPE.md).

## Sprint 2 — broaden semantic primitives

Add mechanics because they unlock families of cards/content:

- explicit card-selection prompts;
- choose-and-discard;
- exhaust and generated/temporary cards;
- X-cost and variable-cost cards;
- multi-hit and all-enemy effects;
- player/enemy powers;
- generic triggered effects;
- card/relic/potion state that persists within a combat;
- more enemy move types.

Prefer generic operations and trigger registrations over card-ID switches.

## Sprint 3 — make run generation more game-shaped

Replace the floor-by-floor choice generator with a graph-shaped map abstraction while preserving
semantic `choose_map_node` actions.

Then enrich:

- encounter pools by act/floor/history;
- elite/boss selection;
- reward pools and rarity;
- shop inventory;
- event eligibility/history;
- potion/relic pools.

## Sprint 4 — enlarge the Silent content slice

Grow from the restrictive development catalog toward enough cards, potions, relics and enemies to
produce varied strategic runs.

Content definitions should stay data-oriented wherever possible.

## Sprint 5 — connect real game data

Once the model can represent a substantial run:

1. pin a reference STS2 build;
2. import/translate real definitions where legally appropriate;
3. build the native reference bridge;
4. identify mechanics whose prototype semantics differ;
5. replace prototype RNG/timing/content through versioned rulesets rather than patches spread
   through the engine.

## Sprint 6 — fidelity loop

Add decision-boundary traces and differential replay. Native parity then becomes an iterative
correction process over an already useful emulator.

## Sprint 7 — performance loop

After the parent AI/search project provides representative workloads:

- benchmark full-run rollouts and branch-heavy search;
- profile allocations and copying;
- optimize state representation/forking;
- add batch APIs;
- keep canonical behavior unchanged while optimizing.
