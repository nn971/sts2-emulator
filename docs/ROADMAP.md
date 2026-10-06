# Emulator roadmap

The emulator should become useful for whole-run AI experimentation early, then converge toward
native fidelity and high throughput.

## Milestone 0 — scaffold

- [x] emulator charter and repository boundary
- [x] deterministic engine interface
- [x] explicit RNG-state container
- [x] canonical JSON/hash bootstrap
- [x] trace/diff scaffolding
- [x] unit-test and benchmark scaffolding
- [x] CI scaffold

## Milestone 1 — restrictive Silent whole-run MVP

- [x] typed persistent run/world state
- [x] deterministic prototype ruleset and RNG
- [x] map choices and room entry
- [x] normal/elite/boss combat
- [x] card rewards and deck growth
- [x] potions and relics
- [x] shops
- [x] events
- [x] rest sites and upgrades
- [x] act transitions
- [x] victory/death terminal states
- [x] CLI whole-run smoke driver
- [x] deterministic whole-run smoke tests
- [ ] harden CI and edge-case validation

## Milestone 2 — expressive mechanics kernel

- [x] card-selection prompts
- [x] discard/exhaust/generated cards
- [x] variable/X costs
- [x] multi-target and repeated effects
- [ ] general player/enemy power model — player powers are implemented; enemy-side unification remains
- [ ] generic trigger registration and scheduling — ruleset turn pipeline + player-power stage triggers are implemented; broader event hooks remain
- [x] richer ordered enemy move-effect model; intent selection remains simple
- [ ] combat-local mutable card/relic/potion state — combat-local card identity exists; mutation/relic/potion state remains
- [x] temporary/generated combat card identities and exhaust lifetime

Hook/timing ordering must remain ruleset-driven or otherwise replaceable; avoid card-specific
central dispatch branches.

## Milestone 3 — broader Silent + richer run generation

- [x] persistent graph-shaped maps
- [ ] act/floor/history-sensitive encounter pools
- [ ] rarity-aware card rewards
- [ ] richer shops/events/rest choices
- [ ] substantially larger Silent card pool
- [ ] larger potion/relic pool
- [ ] larger enemy/elite/boss pool
- [ ] enough variety for meaningful strategic AI experiments

## Milestone 4 — native-data and fidelity convergence

- [ ] pin a reference STS2 build
- [ ] native reference bridge
- [ ] real content-data translation/import where appropriate
- [ ] native RNG stream inventory/codecs
- [ ] native timing/trigger semantics
- [ ] legal-action parity
- [ ] decision-boundary state parity
- [ ] minimized regression traces
- [ ] large version-pinned parity corpus

## Milestone 5 — practical speed

- [ ] representative AI/search benchmark suite
- [ ] profile state copy/fork
- [ ] compact state layouts where justified
- [ ] structural sharing/copy-on-write where justified
- [ ] efficient transposition hashes
- [ ] allocation/GC reduction
- [ ] parallel/batch API
- [ ] memory-per-branch budgets
- [ ] NativeAOT/interoperability evaluation

## Milestone 6 — stable consumer release

- [ ] versioned public engine API
- [ ] versioned state/action schemas
- [ ] deterministic save/load
- [ ] Python binding
- [ ] compatibility/version negotiation
- [ ] supported-mechanics manifest
- [ ] reproducible fidelity/performance report

## Maintenance

- track new STS2 builds;
- keep prototype assumptions clearly separated from verified semantics;
- characterize semantic diffs between game builds;
- retain minimized parity regressions;
- preserve benchmark history;
- version public bindings deliberately.
