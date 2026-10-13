# Unified v111 RNG + Fidelity Inspector: integration status

**2026-10-13.** Branch: `integration/fidelity-rng-unified-v111`. This is an isolated **review-only** integration branch; do not merge into `main` or into evolving Silent-all-Act development. Frozen untouched source baseline is `agent/v111-hive-glory-pregenerated-encounter-queues-v20@4ea715cbd3b42a071f7e63c845311dd5115bf93b`. The development repository working tree at `~/projects/sts2-emulator` and shared local Git metadata were not accessed. Never add directories under `~/projects`.

## Consolidated provenance (real commit ancestry)

- Desired design **#119** and provider/continuation stack **#123→#124→#126** (native index provider, scripted/controlled providers; serializable HP→intent boundaries; isolated JSONL/Python Nature interface).
- Alternate initial-HP **#125** is an **ours-resolved historical merge**: canonical implementation is #124/#126. We retained its historical document and ported its independently useful duplicate-creature-`CombatId` rejection as a negative test; alternative incompatible API was not copied over.
- Native Inspector **#121→#130→#131** (strict passive decisions, action-envelope sidecar, nested-choice resumption, revised audits and closed-prefix extraction). Older scaffold-vs-v111 CI/CLI/test-project collisions were reconciled by preserving the full working v111 implementation, retaining Inspector tools and native mod code, and combining their interfaces.
- Engine fidelity line **#127→#128→#129→#132**, merged at the **last clean fidelity-only source tip `5fa3208eff590bb5ab5622ea9e849db7b39d9c2e`**. The remote #132 branch later gained unrelated Silent-all-Act commits. **Do not use its moving head as an integration merge parent**; that would falsely mark unrelated game features merged while excluding their content. We selectively included the latest fidelity-only checker code, actual Step importer, source-site emulator journal, and fixture batch CLI. This policy protects later legitimate merges from the active all-Act development line.
- The final merge tree contains only inspected fidelity/Inspector/provider changes plus the frozen base—not subsequently developed Silent-all-Act content.

## First new implementation after integration

1. `RandomBoundaryJournalV111` now passively records typed chance requests **at the actual provider resolution call**, with site/stream/owner/version/occurrence/candidate order, selected index/value, mode and optional native cursor snapshots. The journal is diagnostic-only and opt-in via scoped `AsyncLocal`; the ordinary native/legacy gameplay API is unchanged.
2. All three initial `IRandomIndexProviderV111` implementations (native, scripted and controlled) emit observations only after successful validation/resolution. The journal never requests a new draw or changes a stream; invalid selections emit nothing.
3. `RandomBoundaryComparisonV111` checks exact support order, owner, stream, call site/sequence, source version and selected outcome; optionally requires identical native pre/post cursors. Native vs counterfactual scripted outcomes may match despite different provider mode and cursor.
4. The **actual**, unchanged `PrototypeGameEngine.Step` checkpoint importer now captures both its existing ordered mechanics journal and the nested typed RNG provider journal, returning `emulator_random_boundaries` and `random_boundary_coverage=MIGRATED_PROVIDER_SITES_ONLY`. Batch summaries include per-fixture provider boundary counts.
5. Negative/regression tests cover provider parity vs scripted, candidate order and request count mismatch, invalid outcomes, nested capture scopes, untouched controlled RNG, and duplicate creature owner IDs inherited from #125. A journal with zero migrated calls is **not** evidence that the native game made no random calls.

## Known limits, precisely classified

- Native v0.111 game-local mod **has not been rebuilt or tested** against the user's installed original DLL after branch consolidation; native code is excluded from no-game-assets CI.
- The current Inspector sidecar captures executor callbacks, history and frame changes, **not** the exact native RNG call-site/ownership/result/cursor event sequence. Captured CombatHistory publication order is not necessarily hook invocation order.
- Only limited v111 RNG sites (initial HP and committed enemy intents) use the shared provider. Controlled HP→intent execution is an **isolated combat-entry continuation**, not arbitrary player-action `Step`, mid-card hooks, shuffles, rewards or a complete Nature-driven run.
- Existing authentic eight-play-card corpus: 8 native expectation fixtures; only 1 isolated actual engine step, all 49 compared fields match, but its visible Weak effect remained unchecked; the other 7 are blocked by unimported enemy powers, with 3 also Pinpoint cost/skill-history blockers and one unexplained double Defend execution. **Full native combat parity is NOT_TESTED.**
- Controlled counterfactual trajectories need not match native cursors; **exact native seeded replay still requires source-specific RNG ownership and cursor parity** in dedicated tests.
- Exact probability kernels conditional on player information are not calibrated. Don't label unverified chance weights as the native reference distribution or use importance ratios as exact.

## Next acceptance-gated patches

1. **Native read-only RNG source probes** at v111 `Rng.NextInt` / shuffle / contextual owners. Record actual source call-site, generator instance owner, support/weight identity, selected outcome, source causal parent, and native cursor before/after. Native instrumentation must be measured for no perturbation.
2. Match each native source call to the typed emulator provider-boundary transcript; classify request mismatch, eligible support mismatch, outcome mismatch, uninstrumented site, missing owner, exact-seed cursor divergence **separately**.
3. Extend strict pre-state importer and exact mechanics step to combat powers and their order/expiry, cards with variable displayed costs and verified turn counters, nested player-choice selection, automatic replay cause. Never inject native post-state into engine execution.
4. Migrate audited chance sites in small native-zero-regression batches: encounter/local formation/spawns, draw/shuffle, targeting/card generation, relic/potion hooks, rewards, maps/events. Preserve unsupported-site strict failures.
5. Test complete combat and whole-run trajectories with both providers, source hook-order parity and chance transcript parity, then calibrate conditional probability laws and measure throughput.

The [integration plan](UNIFIED_RNG_FIDELITY_INTEGRATION_PLAN.md) is the source of acceptance gates. Experimental Nature network training and multiplayer content remain out of scope.
