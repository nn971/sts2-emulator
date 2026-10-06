# Emulator roadmap

This roadmap ends at a mature emulator and consumer interface. AI/search/training milestones live in the future parent project.

## Milestone 0 — scaffold

- [x] emulator charter and fidelity contract
- [x] deterministic engine interfaces
- [x] generic explicit RNG-state container
- [x] canonical JSON/hash bootstrap
- [x] JSONL trace envelope
- [x] first-divergence trace diff tool
- [x] unit-test and microbenchmark scaffolding
- [x] CI scaffold
- [x] documented parent/subrepo boundary

## Milestone 1A — native reference capture

- [ ] pin one STS2 build as the first reference target
- [ ] identify mod-loader/instrumentation route
- [ ] enumerate top-level run phases and decision boundaries
- [ ] capture canonical run state from live game
- [ ] capture legal actions
- [ ] inventory complete continuation-relevant RNG streams
- [ ] capture reproducible RNG stream state/fingerprints
- [ ] emit versioned JSONL traces
- [ ] record a small hand-verified trace corpus

## Milestone 1B — deterministic whole-run kernel

- [ ] initialize/load a canonical run state
- [ ] implement versioned RNG codecs/streams
- [ ] map generation and routing
- [ ] room/encounter generation
- [ ] rewards/card choices
- [ ] shops
- [ ] rest sites
- [ ] events
- [ ] potions/relic acquisition and persistent state
- [ ] act transitions
- [ ] explicit unsupported-mechanic failures

## Milestone 1C — high-fidelity combat

Study/reuse/adapt ideas from existing combat prediction infrastructure instead of casually rewriting combat.

- [ ] canonical combat snapshot
- [ ] fork-safe card/pile/creature/power state
- [ ] native-order trigger semantics
- [ ] enemy AI
- [ ] potion/relic combat interactions
- [ ] cross-turn histories/counters
- [ ] combat RNG parity
- [ ] native-vs-emulator closed-loop tests

## Milestone 1D — parity at scale

- [ ] replay runner
- [ ] legal-action parity
- [ ] first-divergence reports
- [ ] random-legal-action live traces
- [ ] targeted rare-mechanic traces
- [ ] minimized regression traces
- [ ] nightly fuzz/parity jobs
- [ ] 1,000-run milestone corpus with no unexplained divergence

## Milestone 1E — practical speed

- [ ] representative benchmark suite
- [ ] profile state copy/fork
- [ ] structural sharing/copy-on-write where measurements justify it
- [ ] efficient exact-state/transposition hashes
- [ ] reduce allocations and GC pressure
- [ ] parallel batch API
- [ ] establish memory-per-branch budgets
- [ ] evaluate NativeAOT and interop overhead
- [ ] publish reproducible performance report

## Milestone 1F — stable consumer release

- [ ] versioned public engine API
- [ ] versioned canonical state/action schemas
- [ ] stable native or managed batch interface
- [ ] Python binding suitable for research workloads
- [ ] compatibility/version-negotiation rules
- [ ] deterministic save/load snapshots
- [ ] benchmarked parent-project integration example
- [ ] release checklist documenting supported mechanics and known parity gaps

## Post-Milestone 1 maintenance

- track new STS2 game builds;
- characterize semantic diffs between builds;
- update native fixtures before changing emulator behavior;
- maintain backward-readable trace formats when practical;
- preserve benchmark history;
- keep public bindings stable or explicitly versioned.
