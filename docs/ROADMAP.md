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
- [x] prototype CI hardening, structural invariants, and 500-seed whole-run sweep

## Milestone 2 — expressive mechanics kernel

- [x] card-selection prompts
- [x] discard/exhaust/generated cards
- [x] variable/X costs
- [x] multi-target and repeated effects
- [x] shared player/enemy power model with globally ordered event subscribers
- [x] generic combat event registration and ordered power/relic scheduling; status ticking remains a provisional stage system
- [x] richer ordered enemy move-effect model; intent selection remains simple
- [x] combat-local card/relic/potion state containers, including stateful relic counters
- [x] temporary/generated combat card identities and exhaust lifetime

Hook/timing ordering must remain ruleset-driven or otherwise replaceable; avoid card-specific
central dispatch branches.

## Milestone 3 — broader Silent + richer run generation

- [x] persistent graph-shaped maps
- [x] act/floor/history-sensitive weighted encounter pools
- [x] rarity-aware card rewards
- [x] rarity-aware shops with one-use card removal
- [x] weighted event eligibility/history with repeat constraints
- [x] richer rest-site choices, including permanent max-HP training
- [x] substantially larger Silent card pool — 28 non-basic reward cards plus starter/generated cards
- [x] first expanded potion/relic pool
- [x] act-specific boss and later-elite encounter expansion
- [x] enough variety for first strategic AI experiments — 500-run sweep covers 28/28 reward cards, 8/8 relics, all events, and all acts

## Prototype AI handoff

- [x] player-facing observation DTO
- [x] stable semantic action IDs
- [x] deterministic reset/step/fork adapter
- [x] sibling expansion API for search
- [x] observation and canonical-state hashes
- [x] machine-readable capability manifest
- [x] deterministic multi-run sweep report
- [x] parent `sts2-ai` integration against this adapter via the versioned JSONL bridge

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

- [ ] representative AI/search benchmark suite — prototype observe/expand microbenchmarks added
- [ ] profile state copy/fork
- [ ] compact state layouts where justified
- [ ] structural sharing/copy-on-write where justified
- [ ] efficient transposition hashes
- [ ] allocation/GC reduction
- [ ] parallel/batch API — deterministic sibling expansion API added; parallel execution remains
- [ ] memory-per-branch budgets
- [ ] NativeAOT/interoperability evaluation

## Milestone 6 — stable consumer release

- [ ] versioned public engine API
- [ ] versioned state/action schemas — `prototype-ai-v0` adapter schema exists; cross-language schema remains
- [ ] deterministic save/load
- [ ] Python binding
- [ ] compatibility/version negotiation
- [x] prototype supported-mechanics/content capability manifest
- [ ] reproducible fidelity/performance report

## Maintenance

- track new STS2 builds;
- keep prototype assumptions clearly separated from verified semantics;
- characterize semantic diffs between game builds;
- retain minimized parity regressions;
- preserve benchmark history;
- version public bindings deliberately.
