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

Prioritize strategic run-level decisions:

1. richer shop inventory generation, including rarity/price variation;
2. event eligibility and event-history constraints;
3. larger potion/relic pools and pool-history rules;
4. elite/boss selection by act;
5. map-generation constraints beyond the current layered DAG;
6. card removal/transformation and additional rest-site choices where useful.

Keep generation state explicit so the future AI can reason from the complete observable run state.

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

Before serious learning/search work:

1. define compact observation/legal-action DTOs;
2. add deterministic reset/step/fork APIs suitable for batch consumers;
3. provide stable action identifiers independent of CLI formatting;
4. add rollout statistics and representative whole-run benchmarks;
5. keep canonical state/hash APIs available for verification.

The learning/search implementation itself belongs in the parent `sts2-ai` repository.

## Sprint D — connect real game data

Once the prototype supports varied strategic runs:

1. pin a reference STS2 build;
2. inventory real content/data sources and legally appropriate extraction/translation routes;
3. build the native reference bridge;
4. compare legal actions and decision-boundary states;
5. replace prototype generation, RNG, timing, and content through versioned rulesets.

## Sprint E — fidelity loop

Use differential replay to turn native observations into minimized regression cases. Treat every
verified ordering/RNG/content correction as version-pinned evidence rather than a global assumption.

## Sprint F — performance loop

After `sts2-ai` supplies representative workloads:

- benchmark full-run rollouts and branch-heavy search;
- profile state copying/forking and event dispatch;
- optimize layouts and structural sharing;
- add batch/parallel APIs;
- track memory per branch and transitions per second;
- evaluate NativeAOT/interoperability only after profiles justify it.
