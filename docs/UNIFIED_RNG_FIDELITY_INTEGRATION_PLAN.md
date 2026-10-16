# Unified RNG and Inspector Fidelity Program (v0.111, Silent single-player)

**Recorded before integration:** 2026-10-13.  
**Work branch:** `integration/fidelity-rng-unified-v111` (new, isolated).  
**Frozen, read-only development baseline:** `agent/v111-hive-glory-pregenerated-encounter-queues-v20` at `4ea715cbd3b42a071f7e63c845311dd5115bf93b`.  
**Local workspace constraint:** NEVER operate on the user's `~/projects/sts2-emulator` checkout or its Git metadata; never create a new top-level directory under `~/projects`. User's existing `~/projects/sts2-inspector` is a linked worktree; prefer remote PR commits and, for local tests, a completely separate /tmp clone. **Do not merge into main or any ongoing Silent-all-Act branch.**

## Objective

Integrate the *desired RNG implementation* with a real-game read-only **Fidelity Inspector** into one long-lived branch. One source-ordered mechanic/hook path must resolve all stochastic decisions via interchangeable **native v111 RNG / scripted outcomes / controlled Nature** providers; Inspector records the independent native oracle. Build a **complete checker** by progressively replaying genuinely captured legal player actions and choice transcripts through the actual existing engine, comparing states, source-hook order and stochastic request identity. Separate coverage/unsupported from proven matches; preserve native RNG seeded parity as a separate gate.

**No Nature neural-network training**, multiplayer mechanics or cross-character completeness claims in this program. Exact native cursor equality is optional for controlled counterfactual trajectories, **mandatory for explicit native seeded replay tests**. Conditional chance-law calibration remains a later distinct gate.

## Existing draft branches (inventory; retain original branches for provenance)

| Line | Draft PRs / source | Integration intent |
| --- | --- | --- |
| Desired design | #119 `docs/desired-rng-implementation-v111` | Include the intended specification without overwriting later implementation-specific edits |
| RNG primary | #123 → #124 → #126 (`feature/desired-rng-d3-intents-bridge`) | Merge D1 typed provider, D2 HP/intent, D3 serializable continuation and isolated D4 JSONL/Python bridge |
| RNG alternative | #125 `feature/desired-rng-controlled-hp-v111-d3` from #123 | **Diverges from #124**; audit 4 changed files and reconcile unique behavior/tests. Do not overwrite #124/#126 continuation blindly or duplicate its HP API |
| Inspector | #121 → #130 → #131 (`feature/v111-native-resume-cleanup-recovery`) | Read-only native v111 capture, executor/history/frames, choice resume, legacy recovery and closed-prefix extraction |
| Engine checker | #127 → #128 → #129 → #132 (`feature/v111-native-closed-prefix-batch`) | Actual `PrototypeGameEngine.Step` fixture importer, creature/zone identity, ordered emulator journal, batch comparisons and fail-closed powers/replay |
| Continuing all-Act | #120 baseline plus unrelated newer branches | **Read-only.** Import no ongoing content commits or merge into these branches |

### Merge discipline

1. Make this plan commit **first** on the isolated integration branch.
2. Bring in primary RNG #126, retaining all its ancestor commits #123 and #124. Verify CI and the original exact cursor/hash regressions.
3. Review #125's divergent 4-file delta separately; preserve useful unique checks after reconciling rather than using a blind overwrite of the canonical controlled-HP implementation. Record any rejected duplicated content.
4. Bring in Inspector #131, retaining #121/#130 history. Inspector was branched from older `main`, whereas this integration is a full v111 codebase; resolve conflicts against the working engine without deleting unrelated content. Avoid nested repositories or extra worktrees.
5. Bring in checker #132, retaining #127/#128/#129 history. Reconcile overlaps in `Program.cs`, CI, test project and documentation with provider and Inspector CLI; do not replace the currently working run logic with another branch's stale copy.
6. Integrate #119 spec's nonduplicated historical content. If already superseded, record it as incorporated/superseded without reverting updated docs.
7. Preserve **original PR branches** until everything is CI green and dependencies have been documented; mark old PRs as superseded/consolidated, don't delete their evidence. Merge/squash only to **this** integration target, never to `main` or the evolving emulator branch. No blind rebases of other authors' heads.
8. Take an isolated CI baseline after each large merge. If GitHub reports merge conflicts, resolve on a disposable integration-only merge branch or via a reviewed content-level merge. Never force-update any source branch.

## First engineering batches after code consolidation

**I0 — Integration integrity.** The new branch builds against .NET 9, passes existing source-audited v111 tests and Inspector synthetic protocol/audit tests. `PrototypeGameEngine.Step`, legacy `prototype-ai-jsonl` and existing AI interface remain unchanged by default. Confirm the exact baseline commit remains unchanged.

**I1 — Inspector RNG oracle.** Add passive game-native instrumentation around source RNG request/resolve *without consuming RNG*: call-site ID, owning named/derived stream, native creature/card owner, call sequence, ordered eligible set or compact bounds/weights, raw result, native pre/post counter and optionally state words, and **precise causal hook/action parent**. Record real selected nested player choices and replay causes. Avoid assuming `CombatHistory.Changed` order equals hook invocation order. Version trace and redact hidden seed/stream state from player-visible AI observations.

**I2 — Mechanics importer.** Extend canonical snapshot with continuation-relevant powers, relics/potions, card cost modifiers, turn counters, pending effects and choices; identify visible power IDs (`WEAK_POWER`, `SHRIEK_POWER`), correct source ordering, stacking and expiry. Unverified semantics remain blockers. Re-run authentic eight-fixture corpus: current report has 8 fixtures, only one actual engine step (49/49 checked visible fields, `PARTIAL` because native Weak unverified); seven blocked by enemy powers, three also by Pinpoint cost, and one by two native Defend executions. Inspect source cause rather than giving Defend 10 base block. No assertion of full parity.

**I3 — Shared random hooks.** Migrate source-audited random selection sites one by one (first HP/intents from #126, then formation/spawn, shuffle/draw, targets, random card generation, map/events/rewards). Shared mechanics code requests chance decisions at the actual source location. Native mode proves identical old/new seeded stream/cursor and result; controlled mode proves legality/fork/save/restore and unchanged mechanics under identical scripted outcomes. Every unported site must fail closed in controlled mode.

**I4 — Complete differential checker.** Take a versioned native Inspector corpus of **whole combats**, including enemy turns, player nested choices, relic/potion triggers, auto-plays, powers, death/summon and stochastic calls. Construct emulator pre-state exclusively from independent native before-state plus audited hidden continuation when available; never cheat with native post-state. Run actual emulator `Step` / controlled continuation with matched legal chance outcomes. Compare canonical visible+hidden fields, legal menu, per-hook/operation ordered event pairs, stream calls, first divergence and provenance. Classify `MATCHED`, `MISMATCH`, `CHANCE_OUTCOME_UNMATCHED`, `UNSUPPORTED`, `CAPTURE_GAP`, `STATE_IMPORT_GAP` separately.

**I5 — Probability and performance.** Audit conditional reference outcome laws given player observations, then permit probability-sensitive Nature training. Measure replay/observer perturbation and performance. Do not confuse a scripted feasible trajectory with verified original conditional probabilities.

## Independent acceptance gates

| Gate | Evidence required | Do not infer |
| --- | --- | --- |
| Capture integrity | No dropped records, exact action/resume/choice identity, complete terminal; observer doesn't alter game | Native semantic fidelity |
| Engine mechanics | Same actual engine action + supplied chance choices yields state and ordered hooks equal to independent native oracle | Exact native RNG stream |
| Native seeded RNG | Exact owner/call count/algorithm/result/post cursor/stream words on same pinned initial state | Original conditional law |
| Controlled replay | Legal chance supports, intermediate persistence, fork isolation, explicit counterfactual provenance and no shadow cursor | Native seed compatibility |
| Conditional distribution | Source-audited state/history constraints plus distribution checks with player-information conditioning | Unbiased importance weights until certified |
| Completion | All v111 supported single-player combat mechanics enumerated and tested; every unsupported case explicit | Fidelity based on one sampled combat |

## Sources

- #119 desired RNG architecture, #123/#124/#125/#126 provider/continuation drafts.
- #121/#130/#131 Inspector and capture recovery.
- #127/#128/#129/#132 actual engine checkpoint and journal drafts.
- Real seven-combat v111 Inspector corpus and `actual-engine-batch.json` findings retained **outside Git** in user's `/mnt/d/SteamLibrary/sts2-inspector-traces`. Never commit raw private/native traces or game binaries.
