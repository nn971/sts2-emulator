# Live lockstep native-game fidelity: Silent A10

**Canonical plan (2026-10-31; proposed for review).** Tracking issue: [#122](https://github.com/nn971/sts2-emulator/issues/122). Target: a human launches the **installed, exactly pinned Slay the Spire 2 game** with `Sts2ReferenceMod`, starts **one Silent Ascension 10 run from a chosen seed**, and plays normally. A persistent `sts2-emulator` session independently runs **the same decisions with its Native RNG provider**. The **in-game Fidelity Inspector belongs to the reference mod** and displays coverage-aware comparisons and the first divergence.

This document supersedes the *priority/order and first-goal framing* of [NATIVE_FIDELITY_INSPECTOR_PLAN.md](NATIVE_FIDELITY_INSPECTOR_PLAN.md), [NEXT_STEPS.md](NEXT_STEPS.md), and earlier one-checkpoint/controlled-step handoffs **for the native-fidelity track**. Those documents remain historical evidence and implementation references. It does not supersede the independent training/Nature plans (#143, #174, #190) or alter running H15 experiments.

## 1. End-to-end contract

Native game (human plays) → `Sts2ReferenceMod` (read-only source observation, action and privileged RNG diagnostics) ⇄ versioned local transport ⇄ persistent emulator Native-RNG session (own initialized state, genuine mechanics) → structured differential result → native mod F8 Inspector and durable trace.

1. **Bootstrap.** Pin the game's exact assembly SHA-256, mod/repo commits, emulator revision and schema. Record seed as entered/interpreted by the *game*, character, ascension, any modifiers, run initialization and initial native checkpoint. Start a fresh independent emulator run with the equivalent configuration. Verify initial legal choices, canonical state and every available RNG cursor **before** classifying subsequent actions as aligned. A matching seed string by itself is not evidence of matching generated maps or streams.
2. **Decision.** Capture one stable semantic player boundary with native decision identity/revision, complete supported legal menu, canonical state/coverage, and relevant hidden diagnostic state on a **separately privileged channel**. Include map, Neow, rewards, shop, rest, event, boss/act and combat choices; an unsupported boundary must be explicit. Never infer readiness from a fixed frame delay.
3. **Selected action.** Observe the *accepted original-game human action* with actual target, card/relic/potion/choice instance identity, and its causal parent. Transmit the canonical action exactly once to the emulator. The mod **does not inject game actions** in this mode.
4. **Transition.** The game continues normally. The emulator uses its ordinary mechanics and **Native RNG provider** to execute the same action, including automatic chains and any stochastic outcomes. No native POST-state, native chosen RNG result or `ReferenceNature` sample may be substituted into the emulator's prediction.
5. **Comparison.** At the next stable boundary (or terminal), compare: legal actions; public and privileged continuation-relevant state where legitimately captured; ordered actual source effects vs emulator effects; RNG stream and contextual-generator cursors/call transcripts; and state/entity identities. Produce first differing canonical path or source event with native and emulator values, action identity, preconditions, coverage and provenance.
6. **Repeat.** Keep one session across combats, rooms, rewards and acts. After the **first** divergence, mark later steps `DOWNSTREAM_UNALIGNED` unless explicitly restarted; continue native evidence capture without presenting later comparisons as independent parity passes. No silent resynchronization using native post-state.

**Success criterion:** A complete pinned Silent A10 native run, from first run initialization to victory or defeat, whose supported independent emulator decisions, mechanics, state and exact Native-RNG continuations agree at *every* decision/terminal checkpoint, with no missing required coverage. A run ending at an unsupported screen is an incomplete acceptance case.

## 2. Responsibility and information boundaries

| Owner | Responsibilities |
| --- | --- |
| Installed game | Sole authority for actual selected human actions and actual game state |
| `native/Sts2ReferenceMod` | Read-only native source hooks, actual legal-menu/action/decision capture, canonical state + RNG snapshots, trace transport, F8 Inspector UI |
| `Sts2Emulator.Core` | Independent game initialization, persistent mechanics/hidden state, Native RNG implementation, fork-safe state and diagnostics |
| Emulator-side local bridge/CLI | Versioned session manager, action replay, result envelopes, differential comparator, offline same-transcript replay |
| `sts2-ai` | **No required dependency** for the human-operated fidelity session |

Expose **two distinct projections**: a public/player-fair observation and a privileged, versioned native diagnostic snapshot. The latter may contain hidden pile order, history and RNG state for parity diagnostics and must never be passed to policy observations. Native read-only source hooks may copy generator state without consuming it. Share DTO/schema semantics, not implementation of source mechanics.

The in-game F8 Inspector is a presenter/controller of *inspection* (status, trace export, differences); the comparison implementation may live out of process. Reuse the existing spectator-only authenticated loopback foundation from [#158](https://github.com/nn971/sts2-emulator/pull/158), but introduce a separate **outbound native-observation → emulator** protocol rather than enabling game-action POST routes. Keep auth, loopback binding, bounded queues, data limits, disconnection recovery, main-thread-only game access and passive no-perturbation checks. Prefer a single new transport/protocol owner over a second sidecar sampler or duplicate mechanics engine.

## 3. Session protocol (proposed v1)

Every message includes `schema_version`, `game_build_sha256`, `mod_revision`, `emulator_revision`, `run_id`, `session_epoch`, monotonically increasing `sequence` and a content fingerprint. Treat these as a protocol design, not existing APIs.

| Message | Required content |
| --- | --- |
| `begin_run` | Canonical seed/run input, Silent/A10, build pin, startup provenance, Native diagnostic stream inventory |
| `decision_boundary` | Stable boundary ID/revision, phase, native state + explicit field coverage, complete/partial menu, optional source event cursor |
| `accepted_action` | Exact boundary ID/revision, canonical action and stable instance/target mapping, one accepted action ID |
| `emulator_result` | Precondition check, predicted next boundary/terminal, predicted full state + coverage, ordered effects, RNG transcript/cursors, execution status |
| `comparison` | `MATCH`, `MISMATCH`, `CHANCE_UNALIGNED`, `UNSUPPORTED`, `INVALID_SETUP`, `INCOMPLETE_CAPTURE`, `DOWNSTREAM_UNALIGNED`; first difference and coverage manifest |
| `end_run` / `disconnect` | Terminal or last independently verified prefix, dropped records, export paths and reason |

Key invariants: one in-flight semantic action per session; stable native-to-emulator identity mapping (including card instances, spawned creatures and nested-choice options); exact-once effect under retries; no duplicate action on native executor resumption; action acceptance verified against both legal menus; observe source callback order separately from `CombatHistory` publication and frame deltas. Preserve append-only native evidence independently of transport availability. Event cursor and hash must be consistent across two readers (audit writer and live comparator).

No automatic pause/step, game-action injection or native-side intervention is required for first acceptance. Human gameplay must proceed unchanged if the emulator crashes or disconnects; UI shows `DISCONNECTED`, and trace capture continues.

## 4. Native RNG fidelity (independent prediction)

The emulator **must** derive Native RNG streams and contextual generators from the same supported seed/run configuration and advance them through its own mechanical call sites. The reference mod *observes*, but never supplies outcomes to repair predictions.

Audit separately:
- 12 persistent run + 3 player streams, exact seeding/ownership and xoshiro256** state; local map/encounter/monster/event/reward/shop/context RNG factories and lifecycle;
- primitive identity/parameters/return and exact pre/post cursor for `NextInt`, `NextDouble`, `NextBool`, `NextFloat` and any uninstrumented primitive;
- consumption order, call parent, semantic source site, ordered candidates and conditional weights where relevant; auto shuffles, intent selections and generated outcomes;
- initial divergence vs wrong owner/seed, skipped/extra call, different parameter/bound, different ordered candidate set, different result, different post-state or unavailable observation.

**Do not conflate** native-game exact RNG parity with emulator `Chance Law`, `ReferenceNature`, scripted/learned Nature, or inferred original-game distributions (N5). `ReferenceNature` is a separate sampler and may carry `UNVERIFIED` provenance. Game-exact fidelity evaluates the actual original generator and mechanics.

## 5. Comparison severity and evidence

A field comparison is meaningful only when both sides independently reach equivalent preconditions. Compare canonical legal actions **before** each selected action and post-transition state **after** it. Classify fields `SUPPORTED_AND_CHECKED`, `PRESENT_BUT_UNCHECKED`, `ABSENT` or `UNSUPPORTED`. Preserve source and emulator event streams for the first divergent action.

First-difference report: run and build pins, native/emulator commits, first differing decision/action, native legal menu vs emulator legal menu, first field/event/site mismatch, side-by-side values and RNG cursors, complete coverage manifest, ordered causal prefix, raw-trace references, and reproduction command. Keep an explicit earlier `CHANCE_UNALIGNED` classification where a random call was first wrong; later state deltas are downstream symptoms.

Use a seeded repeat of the same run and actions as reproducibility control. Native-game correctness also requires a passive on/off **nonperturbation** comparison when the game's input/event conditions can be matched; no tool-generated action side effects in normal inspection mode.

## 6. Milestones with acceptance gates

### L0 — Reconcile branches and pin reproducible native build

- [ ] Inventory reusable code against **current main** and Inspector review branch [#158](https://github.com/nn971/sts2-emulator/pull/158), based on [#153](https://github.com/nn971/sts2-emulator/pull/153) ← [#140](https://github.com/nn971/sts2-emulator/pull/140) ← [#139](https://github.com/nn971/sts2-emulator/pull/139) ← [#138](https://github.com/nn971/sts2-emulator/pull/138) ← [#137](https://github.com/nn971/sts2-emulator/pull/137).
- [ ] Record **exact** source tip, installed game assembly hashes and test statuses. Compare actual changed paths and preserve unique source-hook/engine-importer code. Do not bulk-merge historical [#132](https://github.com/nn971/sts2-emulator/pull/132)'s moving head: it acquired unrelated all-Act changes; use the last reviewed fidelity-only integration tip.
- [ ] Native `#if STS2_NATIVE` build/install, F8 overlay, authentic capture, hook-installed flags, bearer auth, game/no-mod behavior on the installed DLL.
- **Gate:** reproducible pinned local native install and complete provenance; focused CI plus a **real** game-local smoke, with no claims from standalone CI alone.

### L1 — Persistent live handshake and deterministic boundary replay

- [ ] Reuse passive recorder and spectator foundation, add ordered outward `begin_run` / native decision / accepted action / native next-boundary messages.
- [ ] Emulator persistent session initializes from seed/config rather than importing each native POST checkpoint. Implement stable action-ID/instance mapping and exact-once replay; report opening mismatch immediately.
- [ ] Native F8 viewer displays comparator status, first field/legal-menu difference and coverage; preserve offline replay of same transcript.
- [ ] Audit nested prompts (Survivor), player action resume and automatic end-turn chains to fix incomplete strict captures.
- **Gate:** one short human-driven Silent A10 encounter with multiple legal actions and no random divergence, showing independent emulator advancement and correct first-error reporting (inject a known diagnostic mismatch in a test).

### L2 — Exact source RNG through a complete combat

- [ ] Replay installed-game `NextFloat` hook against fresh multi-enemy capture; account for previously observed nine unexplained `run.monster_ai` advances, or log the precise remaining gap.
- [ ] Align game and emulator seed/initial stream cursors; verify primitive operations and typed ownership, including local/context generators and a draw/recycle/intent scenario.
- [ ] Match game source calls and engine provider journal without pretending `choose_index` candidate order is checked solely by matching raw `NextInt(n)`.
- **Gate:** full combat with deterministic native/emulator source-call and cursor alignment through all exercised RNG sites. Any unsupported primitive or unknown owner is an explicit partial gate.

### L3 — Complete-combat mechanics and strict comparison

- [ ] Capture/import full continuation-relevant combat state: powers, expiry/counters, enchantments/temporary costs, relic/potion mutable state, deck instance zones/order on privileged diagnostics, summon/death and internal intent history.
- [ ] Audit native effect/hook order against independently executed emulator order, including known Terror Eel double-Defend and Sucker Punch Weak cases.
- [ ] Replay an encounter from entry through victory/rewards with no post-state repair; expand encounter/seed coverage and preserve intentional mismatch fixtures.
- **Gate:** complete independently replayed combat, all required fields/events/legal menus checked, plus several structurally distinct encounters.

### L4 — Full-run Silent A10

- [ ] Extend source observation and independent emulator actions to Neow, map generation/choices, events, shops, rest, rewards, relic acquisition, boss/act transitions and all encountered stochastic families.
- [ ] Run one complete manually played fixed-seed run, including every transition and exact RNG checkpoints; store compact repro corpus and unsupported-site matrix.
- [ ] Repeat on at least two independent seeds with materially different action paths and include one intentionally divergent emulator regression.
- **Gate:** one full A10 terminal native run with no unexplained first difference, complete required coverage and reproducible durable evidence. Broader seed coverage is the following robustness stage, not a substitute for the first successful full run.

## 7. Existing work: canonical owner and historical consolidation

**One fidelity tracker:** [issue #122](https://github.com/nn971/sts2-emulator/issues/122). The following PRs are historical contributions or review stacks; their implementation claims remain specific to their own pinned branches.

| History | Classification / action |
| --- | --- |
| [#121](https://github.com/nn971/sts2-emulator/pull/121), [#130](https://github.com/nn971/sts2-emulator/pull/130), [#131](https://github.com/nn971/sts2-emulator/pull/131) | Closed unmerged source PRs; Inspector passive/sidecar/recovery logic incorporated into reviewed integration chain [#135](https://github.com/nn971/sts2-emulator/pull/135) → [#136](https://github.com/nn971/sts2-emulator/pull/136) → [#137](https://github.com/nn971/sts2-emulator/pull/137). Historical evidence, **not new independent work items**. |
| [#127](https://github.com/nn971/sts2-emulator/pull/127)–[#129](https://github.com/nn971/sts2-emulator/pull/129), [#132](https://github.com/nn971/sts2-emulator/pull/132) | Real engine checkpoint importer/event journal/batch. Authentic single Strike case was 47/47 covered observable checks. Broader importer has known partial power/hidden-state coverage. The last *clean* fidelity-only code was selected into integration; #132 is an open draft with unrelated later branch history—do not merge its head blindly. |
| [#123](https://github.com/nn971/sts2-emulator/pull/123)–[#126](https://github.com/nn971/sts2-emulator/pull/126), [#133](https://github.com/nn971/sts2-emulator/pull/133), [#134](https://github.com/nn971/sts2-emulator/pull/134) | RNG provider/controlled-continuation lineage. Historical alternate #125 was reconciled into canonical #124/#126 in review. Distinguish controlled Nature from original-game Native RNG. |
| [#137](https://github.com/nn971/sts2-emulator/pull/137) → [#138](https://github.com/nn971/sts2-emulator/pull/138) → [#139](https://github.com/nn971/sts2-emulator/pull/139) → [#140](https://github.com/nn971/sts2-emulator/pull/140) → [#153](https://github.com/nn971/sts2-emulator/pull/153) → [#158](https://github.com/nn971/sts2-emulator/pull/158) | **Open stacked native Inspector review line**: integrated all-Act, source probes, 15-stream checkpoints, `NextFloat`, then spectator-only live HTTP. Preserve branch lineage until content is reconciled and game-local tests pass; don't count stacked PRs as six alternative implementations. |
| [#173](https://github.com/nn971/sts2-emulator/pull/173) | Merged ownership handoff: native fidelity belongs to #122. |
| [#143](https://github.com/nn971/sts2-emulator/issues/143), [#174](https://github.com/nn971/sts2-emulator/issues/174), [#190](https://github.com/nn971/sts2-emulator/pull/190), [#191](https://github.com/nn971/sts2-emulator/pull/191), [#226](https://github.com/nn971/sts2-emulator/pull/226) | **Independent stochastic/AI work.** #190 and #191 overlap on *source-independent* acceptance; reconcile unique tests before considering #191 superseded. Neither is a native-fidelity gate. #226 H15 remains separate from the pinned training branch. |
| [#181](https://github.com/nn971/sts2-emulator/issues/181), [#185](https://github.com/nn971/sts2-emulator/issues/185) | Research fixture generator tracker #181 is canonical; #185 was **already closed as duplicate**. Different from real-game same-seed initialization. |
| [#182](https://github.com/nn971/sts2-emulator/issues/182), [#188](https://github.com/nn971/sts2-emulator/issues/188), [#192](https://github.com/nn971/sts2-emulator/issues/192) | Distinct research fixture/gameplay bug tickets, not duplicate Inspector requests. Preserve independent validation and closure requirements. |

**Consolidation rule:** A historical PR may be closed or marked superseded once its unique code/tests are incorporated and verified in the canonical integration branch. Never equate "closed PR" with "merged to main" or claim that a stacked ancestor's live behavior was validated from synthetic CI. Avoid rewriting frozen checkpoints, deleting history or changing active training pins just to tidy tracking.

## 8. Working rules and next action

Start with **L0**, using existing `~/projects/sts2-emulator`, `~/projects/sts2-ai`, and the single `~/projects/sts2-inspector` worktree; no new directories in `~/projects`. Keep game-local native DLLs and original Steam assets out of Git. Use isolated review branches, explicit source/build pins, focused regression diffs and small working patches. Full-suite failures are categorized against a pinned baseline, not silently waived.

**Immediate deliverable after this plan:** an L0 integration/code-ownership audit plus a native-game test of the existing #158 stack, including `NextFloat` hook flags and a fresh recording. Only then implement the L1 persistent live action-forwarding adapter. 
