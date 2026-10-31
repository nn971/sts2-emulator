# Documentation index

The [Silent all-Act gameplay baseline](SILENT_V111_FROZEN_BASELINE.md) is the frozen implementation checkpoint. Current development focuses on native fidelity checks and RNG infrastructure. This index separates **current working guidance**, **proposed designs**, **recorded evidence**, and **historical plans**.

## Start here

- [Repository overview](../README.md) — build and project scope.
- [Silent v111 gameplay baseline](SILENT_V111_FROZEN_BASELINE.md) — exact emulator revision, supported routes and known gaps.
- **[Live lockstep native fidelity plan](LIVE_LOCKSTEP_NATIVE_FIDELITY_PLAN.md)** — canonical Silent A10 same-seed native/emulator session, Inspector implementation gates and historical PR ownership (issue #122).
- [Current next steps](NEXT_STEPS.md) — previous broader priorities; live-fidelity ordering is in the canonical plan.
- [Development history](HISTORY.md) — dated milestones and source links.
- [Pinned source and mechanics evidence](reference-builds/INDEX.md) — reference-build note index.

## Engine, content and consumer contracts

- [Architecture](ARCHITECTURE.md), [state model](STATE_MODEL.md), [repository charter](PURPOSE.md), [architecture decisions](adr/INDEX.md).
- [V111 consumer interface](V111_INTERFACE.md), [AI observation interface](AI_INTERFACE.md), [subrepository integration](SUBREPO_INTEGRATION.md).
- [Silent card inventory](SILENT_CARD_CATALOG.md) and [colorless card generation](COLORLESS_GENERATION.md).
- [Implementation status](V111_IMPLEMENTATION_STATUS.md) — pointer to the archived cumulative status ledger; the [machine-readable coverage](../data/coverage/v111-baseline.json) supplies item classifications.

## Earlier RNG and fidelity-checker designs

These documents retain useful design/evidence context. For the active native-game same-seed live-inspector objective, use [the canonical lockstep plan](LIVE_LOCKSTEP_NATIVE_FIDELITY_PLAN.md) and [issue #122](https://github.com/nn971/sts2-emulator/issues/122); prior milestone ordering is historical.

- [Desired RNG implementation](DESIRED_RNG_IMPLEMENTATION.md).
- [Native Fidelity Inspector plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md).
- [RNG model](RNG.md) and [native RNG source/fidelity matrix](reference-builds/v0.111.0-native-rng-fidelity-matrix.md).
- [Fidelity contract](FIDELITY_CONTRACT.md), [parity testing](PARITY_TESTING.md), [reference bridge](REFERENCE_BRIDGE.md).
- [Native real-engine fidelity evidence](V111_NATIVE_ENGINE_FIDELITY.md) and [native prefix batch report](V111_NATIVE_PREFIX_BATCH.md).

## Long-term planning, research, and support

- [Full implementation roadmap](FULL_IMPLEMENTATION_ROADMAP.md) — eventual single-player all-character release scope.
- [Performance](PERFORMANCE.md), [reference data strategy](REFERENCE_DATA_STRATEGY.md), [external references](REFERENCES.md), [data policy](LEGAL_AND_DATA_POLICY.md).
- Earlier research: [factorized RNG prior](FACTORIZED_INITIAL_RNG_PRIOR.md), [hypothetical draw order](HYPOTHETICAL_DRAW_ORDER.md), [local combat-stream conditioning](LOCAL_COMBAT_STREAM_CONDITIONING.md), [pristine reward proposal](PRISTINE_REWARD_BRANCH.md).

## Development history and archived records

- [Readable development history](HISTORY.md).
- [Archive inventory](archive/INDEX.md) — superseded status reports, regional and prototype plans and historical integration handoffs.
- Prior filenames such as [ROADMAP.md](ROADMAP.md), [REGIONAL_INTEGRITY_PLAN.md](REGIONAL_INTEGRITY_PLAN.md), and [PROTOTYPE_SCOPE.md](PROTOTYPE_SCOPE.md) remain as short compatibility pointers.

## Maintenance convention

For new implementation evidence, record the pinned game version, emulator commit, claimed status (catalogued / implemented / integrated / native-validated), and supporting test/source. Historical notes retain their original claims and dates; current priorities and counts belong in the active baseline and coverage report.
