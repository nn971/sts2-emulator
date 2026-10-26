# Source independent stochastic acceptance

The [`stochastic-source-independent-acceptance` workflow](../.github/workflows/stochastic-acceptance.yml)
runs emulator-side checks without the native reference mod, installed game,
Fidelity Inspector, or learned policy.

## Evidence emitted by CI

- **Site declarations:** the RNG callsite inventory checks registry and matrix
  coverage, contract links, entropy boundaries, Chance Law query purity, and
  draw-kernel ownership. The JSON declaration ledger reports each site's law
  provenance and missing proof facets. It explicitly keeps source-callsite
  completeness and runtime continuation acceptance `NOT_ESTABLISHED`.
- **Provider and mechanics checks:** focused tests compare Native and scripted
  providers at typed random boundaries, verify private provider state does not
  change, check source-independent fixture subscriber ordering, and ensure the
  mechanics projection ignores provider-private RNG while detecting a gold
  change. Independent law, deck, and lifecycle tests run beside them.
  Deck acceptance independently compares exact constrained-order
  enumeration with brute-force physical permutations and checks draw, peek,
  and scry laws with duplicate card models.
- **JSONL isolation:** seeded semantic combat, save/restore, the fixed-policy
  two-site Nature/control experiment, and the five documented source-route
  victory pairs run in separate emulator processes. Public-view checks inspect
  field names so a harmless identifier containing `rng` is not mistaken for
  private state.
- **Regression classification:** complete test suites from frozen main
  `3ce2ded034e8be94cafcb8ff08cd3040bfad7922` and the candidate are compared by
  test identity. Failures on main are reported as shared failures; only a test
  that passed on main and fails on the candidate is classified as a confirmed
  regression. Candidate-only failures remain visible in the report, and the
  normal CI test job still fails on them.

The current inventory reports 185 registered sites mapped to 185 matrix rows,
185 contract-evidence links, and no unspecified reference-law status. Those
counts establish declaration consistency, not executable coverage of every
source callsite or continuation.

## Latest local run

On 2026-10-27, the full .NET suite passed 1,841 tests. The pinned frozen-main
comparison found 0 regressions among 1,625 shared test identities; 323 shared
failures were repaired, with four baseline-only tests and two passing
candidate-only tests. The seeded semantic worker smoke, 15-process matched
A/B replay, and all five documented source-route victory pairs also passed.
The A/B report still says `trainingEvidence=false` and
`probabilityStatus=UNVERIFIED`.

## Remaining acceptance work

- Build the runtime site matrix across mechanics entry, legal support, provider,
  law, save/restore, and fail-closed behavior for every advertised executable
  stochastic site.
- Compare complete mechanics continuations for matching semantic outcomes
  through representative combat, nested draw, event, reward, and terminal
  traces across providers.
- Establish source callsite completeness and Native stream/cursor parity from
  game evidence. Those checks remain outside this source-independent workflow.
- Run and evaluate learned policies in `sts2-ai`; the fixed-policy A/B replay
  is protocol and determinism evidence, not training evidence.

The workflow's artifacts are evidence for these bounded checks only. They do
not certify Native game fidelity or calibrated source probabilities.
