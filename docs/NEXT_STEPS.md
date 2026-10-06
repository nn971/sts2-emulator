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

## Sprint D — parent AI integration, then connect real game data

Before deep fidelity work, add a thin `sts2-ai` integration that can reset, observe, enumerate stable
actions, fork/expand, and run deterministic prototype evaluations. Keep all strategy/search code in
the parent repository.

The native build is now pinned and the data-source inventory is substantially complete. The fidelity
workflow is now **static-first**:

1. sync/query the exact, commit- and blob-pinned Spire Codex `v0.111.0` corpus locally;
2. decompile once and search the pinned local `sts2.dll`;
3. derive generic primitives and versioned rules from those two sources, while treating parser-derived
   columns as hints that must survive text/source cross-checking;
4. compare legal actions and decision-boundary states against native evidence;
5. use live probes only for unresolved runtime ordering/RNG/readiness questions;
6. replace prototype generation, RNG, timing, and content through versioned rulesets.

The static mechanics program has now reached pass 007. Source-backed work includes Sly,
Innate/Retain/Ethereal/Unplayable/Eternal, random-target attacks, typed predicates/counters,
card-local and hand-local costs, turn-scoped/delayed powers, whole-card Burst replay, native
Power-card result-pile semantics, Tools of the Trade's turn-start draw/discard policy, and
Nightmare's instanced delayed selected-card payload.

The next source-driven target is typed combat-card keyword overrides. After that, prioritize
intrinsic Replay, general event-dispatch suspension, and aggregate/value-query effects.

See [REFERENCE_DATA_STRATEGY.md](REFERENCE_DATA_STRATEGY.md) and
[the latest gap report](reference-builds/v0.111.0-mechanics-gap-007.md).

## Sprint E — fidelity loop

Work subsystem-first rather than probe-first. Prioritize:

1. core combat/card/power primitives from the pinned corpus;
2. Silent-native content translation;
3. run generation/reward/shop/rest rules;
4. monster state machines;
5. nested player choices;
6. exact RNG consumption and hook ordering.

Use differential replay only after static sources have produced an implementation. Treat every
verified ordering/RNG/content correction as version-pinned evidence rather than a global assumption.

## Sprint F — performance loop

After `sts2-ai` supplies representative workloads:

- benchmark full-run rollouts and branch-heavy search;
- profile state copying/forking and event dispatch;
- optimize layouts and structural sharing;
- add batch/parallel APIs;
- track memory per branch and transitions per second;
- evaluate NativeAOT/interoperability only after profiles justify it.
