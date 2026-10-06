# Prototype whole-run scope

The immediate development target is a **complete run lifecycle with deliberately restricted
content**, rather than native-fidelity coverage of individual systems.

The first playable ruleset is:

- character: Silent;
- ruleset ID: `prototype-silent-v0`;
- three acts;
- six rooms per act, with a boss on the final room;
- map choices, combat, elite, event, shop, rest, reward, act transition, victory and death;
- a small card, potion, relic, enemy, encounter, status and event catalog.

All prototype content IDs use the `proto.*` namespace. Their names and numbers are development
fixtures and carry no claim of exact STS2 data.

## Why this comes first

A whole-run emulator exposes architectural problems that isolated mechanic tests can hide:

- persistent deck/relic/potion mutation;
- transition between run phases;
- room generation and routing;
- encounter initialization and teardown;
- combat-local versus run-persistent state;
- rewards and shops;
- act boundaries;
- deterministic continuation from a seed;
- terminal victory/death.

Once this skeleton is useful to downstream AI/search code, fidelity can be improved subsystem by
subsystem without waiting for complete reverse engineering.

## Replaceable boundaries

Prototype assumptions are concentrated behind a few boundaries.

### Content

`PrototypeContent` owns cards, potions, relics, enemies, encounters, statuses, events, room pools,
and coarse run constants.

The engine refers to stable IDs and definitions instead of embedding card/enemy behavior in
phase dispatch.

Later work can introduce a native-data-backed catalog or a new versioned ruleset.

### RNG

`PrototypeRng` uses a deterministic development PRNG with independent named streams.

It is explicitly versioned as `prototype-splitmix64-v0`. Native RNG codecs can replace it without
changing the public run/action model.

### Timing and trigger order

The engine does not claim the prototype combat timing sequence is native STS2 order.

Automatic sequencing is supplied by the ruleset as an explicit pipeline. The current prototype
orders operations approximately as:

```text
enemy status start-stage
enemy block reset
enemy actions
enemy status end-stage
advance turn
player block reset
energy refresh
player-power start-turn triggers
draw
```

Status and power definitions declare stages at which they trigger. Cards, potions, powers, and
enemy moves are expressed as ordered semantic effects. The current sequence is a development
ruleset choice rather than a native-order claim.

When native timing is understood, introduce a new ruleset/timing model rather than scattering
special-case ordering checks throughout card implementations.

### Map structure

Each act now generates a complete layered DAG before the first route choice. The graph remains
stable while the player traverses the act; `MapState.CurrentNodeId` records the chosen route and
`choose_map_node` ranges only over outgoing neighbors. The topology and room-generation rules
remain prototype fixtures, so a native map generator can replace them behind the same state/action
boundary later.

## Current prototype content

The initial catalog intentionally stays small:

- basic Silent attacks and blocks;
- basic attacks/blocks plus draw, discard, poison, generated cards, AoE/multi-hit, X-cost, and power examples;
- two potions;
- a starting relic and one additional relic;
- normal, elite and boss encounters;
- poison and weak as generic status fixtures;
- two simple events.

Some familiar names are used to make development readable; behavior and numbers remain prototype
fixtures until verified against a pinned game build.

## Development rule

Prefer adding a reusable semantic primitive over adding a one-off card-specific branch.

A useful sequence for future work is:

1. make the missing run/combat phenomenon representable;
2. add one or two prototype content entries exercising it;
3. add deterministic tests;
4. later replace prototype semantics/data with native evidence.

Fidelity and performance remain important, but they are refinement tracks rather than blockers for
whole-run functionality.


## Expressive mechanics now implemented

The prototype now has a first generic suspension mechanism for player prompts:

- combat effects may request a card selection from a declared zone;
- the engine records the legal candidate set and selection bounds;
- unresolved effect operations are serialized as a continuation;
- resolving the player choice moves selected cards according to the resolution rule and resumes the continuation;
- ordinary combat actions are unavailable while a prompt is pending.

Survivor and Acrobatics exercise choose-and-discard through this mechanism.

Combat card identity is also separated from persistent deck identity. Each combat card records an
optional persistent-origin ID. This permits deterministic generated/temporary cards and combat-local
state without mutating the run deck. Blade Dance/Shiv exercise this path, including exhaust-on-use.

Additional generic mechanics now include:

- repeated and all-enemy effects through operation expansion;
- fixed and X-cost cards with effects that scale from energy spent;
- player powers with application order, stacking, block modifiers, and ruleset-stage triggers;
- enemy moves as ordered effect programs rather than enemy-ID branches;
- a persistent per-act DAG whose future nodes are generated before route decisions.

These structures are prototype semantics rather than claims about STS2's internal implementation.
They are designed so native behavior can later be represented without card-ID conditionals or
globally hard-coded hook order.
