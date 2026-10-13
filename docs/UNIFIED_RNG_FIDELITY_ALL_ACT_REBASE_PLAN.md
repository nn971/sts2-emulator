# RNG + Inspector Fidelity Integration on the Active Silent All-Act Code

**Recorded before implementation on this branch:** 2026-10-13.
**New dedicated work branch:** `integration/fidelity-rng-on-silent-all-act-v111`.
**Pinned all-Act snapshot / first-parent base:** `feature/v111-native-closed-prefix-batch@b2c7ae9a7499ae908f99866b70552e253ed04e76` (moving source PR #132, which has recently accumulated Silent all-Act changes).
**Feature-source second parent:** `integration/fidelity-rng-unified-v111@3bc739dafafaf6e8737e86a82613dff3262158b9` (completed RNG/Inspector unification).
**Protected source:** Do not push to the active all-Act branch, the older all-Act v20 branch, `main`, or either local working tree. No new directories under `~/projects`. Use remote GitHub commits and CI only; any local testing is an independent disposable environment outside `~/projects`.

## Why this baseline

The earlier unified effort used v20 (`4ea715c`) because it was a stable source. Since then, the moving fidelity-side head contains approximately 150+ later all-Act commits, including new mechanics relevant to RNG and parity. Building directly on its **pinned current snapshot** minimizes later mechanical conflicts. This integration advances as a separate child branch; all-Act development is free to continue independently. Future syncs should be incremental, never overwrite newer gameplay code with older snapshots.

## Integration sequence, acceptance gates

1. Commit this plan before implementation or merge manipulation.
2. **Preserve complete all-Act content:** use latest all-Act commit as *first parent*; merge the old unified RNG/Inspector as a second parent with a reviewed content tree. Add missing RNG providers, serializable HP/intent continuations, native Inspector recorder, trace schemas, diagnostic CLI, Python audit scripts, Python bridge and tests. For files already present on the all-Act branch, preserve newer game mechanics and apply only focused additional instrumentation or safety checks. No bulk replacement of all-Act `PrototypeCombat.cs`, `PrototypeGameEngine.cs`, `Program.cs`, protocol or CI.
3. CI gate: .NET Release build, tests, existing all-Act natural-run smoke, Inspector Python unit tests, opt-in Nature HP/intent JSONL tests and new RNG boundary-journal tests. Verify original all-Act ref SHA unchanged.
4. **Next native RNG oracle slice:** extend the read-only Inspector with a *versioned* RNG boundary event protocol. Native recording must originate at real source RNG primitive calls (owner, stream, call site, ordered eligible set or compact bounds, selected outcome, pre/post cursor). If hook/call-site inference is missing, mark it explicitly `UNATTRIBUTED`—never infer source-hook parity from `CombatHistory`. Do not call native RNG from observation code; defer game-local validation until source pin/assembly inspection.
5. Add source- and provider-level comparison to the unified checker. Report `MATCHED_BOUNDARY`, `MISMATCH`, `UNOBSERVED`, `UNSUPPORTED`, `CAPTURE_GAP` separately, with exact first divergence. Treat identical chosen outcomes as mechanistic evidence only on supported restored initial states.
6. Audit the eight authentic user native checkpoint fixtures (1 actual engine step, 49/49 currently checked fields, 7 blocked on enemy powers, Pinpoint counter/replay blockers). Extend power state, skill counters, nested player choices, and automatic actions with independently supported source semantics. Re-run native-to-engine comparisons without altering captured post-state.
7. Gradually migrate all single-player chance sites to shared injectable provider: initial HP/intents (already established), formations, shuffle/draw, targets, generation, rewards, map/events, potion/relic hooks. Exact native RNG cursor equality is separately tested in native-seeded mode. Controlled Nature is counterfactual; keep source legality and hook-order equivalence. Conditional reference probability laws must be audited before probability-weighted training.
8. Only after build, integration CI, game-local native Inspector capture/replay and coverage gates become green should this branch be considered usable for full-combat parity. This branch stays review-only and never automatically merges to an active gameplay branch.

## History rules

The older unified branch already consolidated draft #119, #121 and #123–#131; it includes source fidelity-only revision `5fa3208` from draft #132 while excluding unrelated all-Act commits. This new integration takes the **actual latest all-Act source snapshot first**, thereby recovering those new gameplay commits legitimately and keeping their code unchanged. Preserve feature ancestry and avoid force-pushing any source branch. `docs/UNIFIED_RNG_FIDELITY_STATUS.md` describes the existing observation boundaries and remaining limitations.

## Workspace

User-owned:
- `~/projects/sts2-ai` — unchanged.
- `~/projects/sts2-emulator` — **untouched** including Git metadata/branch operations.
- `~/projects/sts2-inspector` — pre-existing linked worktree; no Git operations needed for remote integration.
- `/mnt/d/SteamLibrary/sts2-inspector-traces` — native recordings and reports; never commit raw traces or proprietary binary assemblies.
