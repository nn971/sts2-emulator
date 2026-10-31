# Native Reference Mod and Fidelity Inspector: implementation plan

> **Historical design / milestone ordering superseded (2026-10-31).** The canonical active objective is a live, same-seed **human Silent A10 native game + independently advancing emulator Native-RNG session + in-mod Inspector**, specified in [LIVE_LOCKSTEP_NATIVE_FIDELITY_PLAN.md](LIVE_LOCKSTEP_NATIVE_FIDELITY_PLAN.md) and tracked by [issue #122](https://github.com/nn971/sts2-emulator/issues/122). This file retains historical interface detail; its original passive → isolated step → controller → later exact-RNG order is no longer the current priority sequence. The Inspector's viewer remains in `Sts2ReferenceMod`; the comparator may run in the emulator process.

**Status:** Planned; no implementation claimed by this document.
**Repository ownership:** `sts2-emulator`
**First application:** Native-versus-emulator fidelity checking for single-player Silent, beginning with Act 1 Overgrowth.
**Implementation location:** `native/Sts2ReferenceMod/`
**Priority:** Native evidence acquisition and deterministic parity before agent playback, exact RNG reproduction, or Nature-controlled training.

This is the execution plan for [REFERENCE_BRIDGE.md](REFERENCE_BRIDGE.md) and [PARITY_TESTING.md](PARITY_TESTING.md), under the standards of [FIDELITY_CONTRACT.md](FIDELITY_CONTRACT.md). It complements the desired RNG work rather than replacing it.

## 1. Objective and first demonstrable result

The native game is the semantic oracle. Instrument it to capture canonical states, legal actions, selected actions, and ordered semantic effects at genuine decision boundaries; reconstruct supported states inside the emulator; replay corresponding actions; report the *first substantiated difference* in a human-readable viewer and a machine-readable artifact.

**Initial vertical slice:** From a pinned native game build, record one Silent/Overgrowth ordinary combat decision, import a sufficiently complete combat snapshot, compare normalized legal actions, execute one deterministic card play, and compare energy, block/HP, powers, card zones, and relevant event order. Store a regression fixture with all versions and an explicit coverage manifest. A deliberately introduced emulator defect must be located at the first differing field/event.

After this succeeds: complete turn -> complete combat -> map/reward/event phases -> broader acts. Full-run *exact* parity is gated on the separately planned RNG fidelity work.

## 2. Ownership, layout, and isolation

Place the mod inside this repository, as an independently buildable native-game adapter:

    native/Sts2ReferenceMod/
      Sts2ReferenceMod.csproj
      Plugin.cs
      Decision/
        DecisionBoundaryDetector.cs
        DecisionCoordinator.cs
        NativeActionAdapter.cs
      Capture/
        NativeStateAdapter.cs
        LegalActionAdapter.cs
        SemanticEventCollector.cs
        EntityIdentityRegistry.cs
      Trace/
        EventJournal.cs
        TraceWriter.cs
      Ui/
        FidelityInspectorPanel.cs
      Transport/
        LocalBridgeServer.cs
    schemas/
      canonical-state.schema.json
      trace.schema.json
      native-parity-report.schema.json         # proposed
    tools/
      trace_diff.py
      native_parity.py                         # proposed comparison runner
    tests/
      ...                                      # contract/unit/integration fixtures
    docs/
      NATIVE_FIDELITY_INSPECTOR_PLAN.md

The tree is proposed, not already implemented. Name/placement of classes may change while retaining the contracts below.

**Dependency rules:**
- `Sts2Emulator.Core` and `Sts2Emulator.Trace` must compile and test without an installed game or Godot.
- Native game assemblies, Godot, mod loader, and Harmony patches are confined to `native/Sts2ReferenceMod/`.
- Native code and emulator code independently implement the **same versioned semantic state/action/trace contract**. Share DTO/schema packages where practical, but avoid native object references in the emulator core.
- `sts2-ai` is only an eventual consumer; no model, inference, or training dependency is required for native parity.
- Native build and packaging are separate commands and optional CI jobs requiring a local game installation. Default `dotnet test Sts2Emulator.sln` stays game-independent.
- The mod should be passive and read-only in recording mode; player/agent action execution is an explicit additional mode.

## 3. Decision boundary and stepping protocol

A decision is a semantic player choice. Examples: card play with target, potion, end turn, in-combat selection prompt, map choice, reward, event, shop, rest, act transition. An automatic chain of hooks, enemy actions, animations, and random effects belongs to the transition produced by one choice; if a new player selection is required during resolution, that selection is the next boundary.

Distinguish **control mode** (`PAUSED`, `RUNNING`, `STOPPING`) from **resolution phase** (`AT_DECISION`, `WAITING_FOR_ACTION`, `EXECUTING`, `RESOLVING`, `TERMINAL`, `ERROR`).

- `Step` authorizes exactly one player decision, follows native resolution, and stops at the next decision or terminal boundary.
- `Pause` while resolving takes effect at the next boundary; do not freeze the Godot scene tree or interrupt native hook execution.
- `Resume` repeatedly authorizes the next action; `Stop` releases agent control at a safe boundary.
- **First fidelity milestone uses passive human input:** capture real decisions and verify the recorded canonical action. The mod's action injection and UI step controller are added only after passive capture is reliable.
- Each decision has a monotonic `decision_id` and observation revision/hash. Commands carry both, and stale/duplicate submissions cannot execute an action twice.
- Exactly one controlled action may be in flight; repeated stability callbacks within one transition are deduplicated.
- State extraction and native actions run on the Godot main thread. Networking, parity comparison, and disk writes run off-thread.
- Timeouts, missed boundaries, action rejections, and unresolved prompts produce explicit incomplete/error trace records with diagnostic context.

Use semantic readiness (native input expected and a stable legal-action menu), rather than fixed frame delays, as the boundary condition. Auditing boundary detection is itself a milestone.

## 4. Canonical trace and event contract

Start from the existing `schemas/trace.schema.json`, `Sts2Emulator.Trace` DTOs, and JSONL workflow. Extend them in a backward-conscious way (a version bump where records change) instead of creating a competing inspector format.

A trace contains:
1. **Header:** exact native game build/hash as available, loader/mod version, bridge commit, emulator commit, schema/static-data versions, scenario/run/seed identifiers, capture mode, provenance.
2. **Decision record:** phase, stable canonical **native** snapshot, declared field coverage, canonical legal-action menu, visible observation and hash, optional privileged continuation state/fingerprints.
3. **Action record:** chosen canonical command, stable card/creature/target IDs, source (`human`, `agent`, `replay`), acceptance/rejection.
4. **Ordered event records:** one monotonically increasing `event_seq` at capture time; `decision_id`, semantic kind, actor/target instance ID, typed payload, origin/hook, optional causal parent, visibility classification.
5. **Transition record:** pre/post boundary references and hashes, covered state projection, event sequence range, completion status, elapsed timing for display only.
6. **Terminal/error record:** result or unfinished transition with the last meaningful checkpoint.

Keep detailed timestamps and UI animations out of semantic equality. Identity must be stable **within the scenario/encounter**; document lifetime rules for card instances, summoned creatures, and transient selections. Translate native IDs to canonical identity before comparing. Never serialize arbitrary Godot object graphs or pointer identities.

Initial semantic events include: card play/movement, energy/resource change, damage/block/HP, powers applied/changed/removed, creature spawn/death, auto-play, end/start turn, reward/room transitions, and selected hook-order markers. Gradually instrument additional native hooks when parity reports show missing causality.

**Important journal invariant:** readers have independent cursors. The inspector UI, JSONL writer, and parity transport consume the same append-only event sequence without destructive `DrainAll` behavior. Assign sequence IDs before asynchronous handoff. On write backpressure, pause controlled playback at a safe boundary and mark passive capture incomplete if durable storage cannot keep up; never silently drop parity-critical events.

**Information policy:** store privileged native diagnostics in a marked, access-controlled channel, separate from the player-visible `Observation` used by `sts2-ai`. Reading RNG for diagnostics must not consume randomness. Expose `RNG_UNAUDITED`/missing fields explicitly until fully inventoried.

## 5. Native-to-emulator snapshot import

Implement a `NativeScenarioImporter` or equivalent *in emulator-side tooling*, not in the game mod. It should accept canonical native snapshots and report exactly what can be reconstructed.

A successful import report includes:
- target phase and scenario ID, pinned game and emulator revisions;
- covered/verified semantic fields, omitted fields, and unsupported continuation state;
- normalized IDs and legal-action mappings;
- prerequisites for deterministic comparison (e.g. complete hand, relevant pile contents/order, counters, enemy conditional state, target identities);
- whether the scenario is `READY_FOR_DETERMINISTIC_STEP`, `PARTIAL`, or `UNSUPPORTED`.

A snapshot import is **not** automatically a valid exact continuation: missing encounter history, RNG, or hidden AI state can affect later steps. Report incomplete fields and reject claims requiring them. Initial scope is Silent ordinary combats and selected deterministic card interactions; expand by fixture coverage rather than pretending arbitrary full-run restoration already works.

## 6. Fidelity modes and honest classification

### Mode A: passive native capture (first)

Record human-played actions and native states/events. Audit schema, ordering, stability, and reproducibility. This mode makes no emulator claims and is the prerequisite for all later comparisons.

### Mode B: deterministic isolated step (first comparison mode)

Import a native state with *verified relevant continuation fields*, map identical legal actions, step the emulator, and compare the covered post-state and semantic event sequence. Use cases should deliberately avoid unresolved random branches. Initial cases: a simple attack/block card against a fixed combat snapshot, then powers/trigger-order cases.

### Mode C: recorded-path differential playback (incremental)

Replay the native action sequence in the emulator until a mechanically meaningful divergence. Compare legality at each boundary; compare deterministic prefixes around random sites. Stop at the first divergence and retain the trace prefix. When native and emulator random draws differ, record the random site and classify the remainder as unaligned **only with evidence of that cause**. A downstream mismatch after RNG divergence is not evidence of a mechanics bug by itself.

### Mode D: exact stream parity (future, RNG milestone)

Once exact native stream inventory/initialization/consumption is audited and the emulator's RNG streams are implemented, require identical chance results, RNG continuation fingerprints, legal actions, and canonical state for the full verified scope. Later Nature injection/testing may add conditional-outcome comparisons, but must remain separate from exact native parity.

**Comparison result vocabulary** (per field/transition, with scoped coverage):
- `MATCH`: all declared covered comparisons agree.
- `MISMATCH`: verified equivalent preconditions but a concrete semantic field/event differs.
- `CHANCE_UNALIGNED`: identified chance boundary prevents lockstep continuation under present RNG setup.
- `UNSUPPORTED`: mechanic or state field lacks implementation.
- `INVALID_SETUP`: native/emulator preconditions or import equivalence were insufficient.
- `INCOMPLETE_CAPTURE`: trace loss, missing boundary, or unfinished action.

Never count `CHANCE_UNALIGNED`, `UNSUPPORTED`, `INVALID_SETUP`, or `INCOMPLETE_CAPTURE` as parity passes. A baseline comparison produces both a result **and a coverage manifest**, preventing field filtering from making a test appear stronger than it is.

## 7. Inspector UI and developer output

The native in-game overlay is a **viewer/controller**, not the canonical source of truth. Begin with an out-of-game CLI report and JSONL output; implement the overlay when the comparator is stable.

Minimum overlay:
- session/build/fixture identity; current act/floor/phase/decision;
- native and emulator comparison status, audited field counts, RNG alignment status;
- `Pause`, `Step`, `Resume`, `Stop` when controller mode is enabled;
- first differing canonical JSON path, before/after values, action and target;
- expandable ordered native vs emulated event timelines with first different event highlighted;
- save regression fixture and export diagnostic report.

A report includes the shortest known failing prefix, exact revisions, imported snapshot and coverage, action, first differing path/event and values, and any native RNG fingerprints available. Reuse/extend `tools/trace_diff.py` for structural JSON differences; implement a semantic comparison layer to normalize IDs and respect documented comparison coverage.

The local transport may start with HTTP/JSON on an explicitly configured loopback endpoint and auth token. Its versioned commands/status/events should support later Python agent attachment, but neural inference and policy distributions are **deferred**.

## 8. Ordered work packages and acceptance gates

### P0 — pin reference and audit integration

- [ ] Select and record exact local STS2 build, mod loader, Godot/.NET assembly dependencies, and platform.
- [ ] Inspect existing native mod approaches and map actual STS2 decision/action hooks; document version-specific assumptions.
- [ ] Add optional `native/Sts2ReferenceMod` project and packaging/install instructions (fish shell examples); no source game files checked in.
- [ ] Add smoke test that launching an unmodified human-playable Silent run with passive mod enabled does not change game results.
**Gate:** reproducible local native build/install and a versioned startup manifest.

### P1 — passive canonical recorder

- [ ] Detect/deduplicate combat, map, reward, event, shop, rest, and terminal boundaries as supported; tag all missing phases.
- [ ] Record stable normalized legal actions, chosen human action, pre/post canonical states, event order and trace provenance.
- [ ] Version trace extensions and validate JSONL; test independent reader cursors and crash-safe partial logs.
- [ ] Verify native capture itself does not perturb RNG or hook order.
**Gate:** one short pinned native segment (map -> combat -> reward -> map), with repeatable semantic capture for controlled equivalent scenarios. Explicitly record any nondeterministic differences.

### P2 — native scenario importer and deterministic parity CLI

- [ ] Implement strict combat snapshot import plus `ImportCoverage` manifest and unsupported-field diagnostics.
- [ ] Build canonical action/identity normalization on both sides.
- [ ] Add deterministic single-step comparisons, legal-action comparison, first-difference path/event, and machine-readable report.
- [ ] Cover at least one Silent attack and one block card; test a known injected emulator regression.
**Gate:** one real native combat action is reconstructed and verified against emulator mechanics, with first-divergence demonstration and non-inflated coverage claims.

### P3 — step controller and in-game inspector

- [ ] Add pause/step/resume/stop with one-action-in-flight and idempotent decision IDs.
- [ ] Display synchronized action/state/event timelines and first meaningful discrepancy.
- [ ] Provide save/export buttons for trace, fixture, report; test failure/disconnection behavior.
**Gate:** a human can step through a supported native combat, see parity status after each action, and export a reproduction bundle.

### P4 — increase audited coverage

- [ ] Complete turn and full combat with explicit chance-boundary handling.
- [ ] Add deterministic shops/rewards/events/map transitions and more complex hooks.
- [ ] Expand from Overgrowth to Underdocks, Hive, Glory according to emulator support; single-player Silent first.
- [ ] Feed curated failing fixtures into CI; keep large/private game-derived traces outside source control.
**Gate:** growing version-pinned fixture corpus, complete first-difference reporting, and explicit per-mechanic parity confidence labels.

### P5 — future integrations (separate milestones)

- [ ] Exact native RNG stream inventory, codecs, cursor/fingerprint parity, hook consumption order.
- [ ] Full-run exact lockstep once RNG and state restoration requirements are met.
- [ ] Attach `sts2-ai` inference to the same decision/controller protocol for agent playback.
- [ ] Later extend to Nature-controlled training experiments; keep fidelity mode semantics distinct.

## 9. Required tests and quality bar

Automated unit/contract tests: trace schema versioning; decision-ID deduplication; one-action-in-flight; stale-command rejection; event order and independent cursors; crash-safe incomplete traces; canonical object/instance normalization; legal-action normalization; import coverage enforcement; first difference path/event; comparison status classification; absent RNG stream handling; schema compatibility.

Native integration checks (when a pinned game is available): passive/no-perturbation run, nested card-choice prompt, end-turn/enemy automatic chain, card auto-play, combat victory/reward/map sequence, disconnect during pause, repeated capture of an equivalent deterministic scenario.

Keep provenance and trace fixtures versioned. CI that lacks game assets should run synthetic/recorded fixtures and the emulator's normal test suite; game-installed integration jobs run separately. The trace corpus records confidence at the field/mechanic level, using the labels in [FIDELITY_CONTRACT.md](FIDELITY_CONTRACT.md).

## 10. Explicit non-goals for the first implementation

- Full arbitrary native state restoration or branching.
- Exact matching of native RNG seeds/cursors before the desired RNG implementation is built.
- Nature adversary, stochastic policy learning, or Python model inference.
- UI frame-by-frame stepping or game-engine timescale freezing.
- Multiplayer-only mechanics or cross-character features.
- Replacing existing semantic schemas or claiming parity from final HP alone.

**Definition of the first successful application:** An authentic, pinned native Silent combat trace can be imported into the emulator with explicit coverage; an identical deterministic action has matching legal choices, state effects, and ordered audited events; the inspector/CLI can surface an intentional mismatch immediately and export a reproducible report. The mod remains useful as a native oracle even where complete emulator import is not yet supported.
