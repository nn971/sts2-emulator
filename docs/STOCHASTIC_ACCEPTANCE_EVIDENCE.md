# Source-independent stochastic acceptance: executable evidence

Base: feature/nature-143-native-policy-comparator at
`606f05cb31048fee06d192169f5836fc01a15744`.
The Fidelity Inspector and native game reference mod are **not** prerequisites.

## Deliverable 1: regression reconciliation

Run `stochastic-acceptance` workflow and download the
`stochastic-regression-evidence` artifact. Its full-suite TRX is classified
using `tools/stochastic_regression_report.py`. A current-only report
deliberately labels failures **unclassified**, not inherited.
To establish an inherited/new regression split, produce a TRX from the
actual comparison base and execute:

```sh
python tools/stochastic_regression_report.py \
  --current path/to/current.trx \
  --baseline path/to/base.trx \
  --output stochastic-regressions.json \
  --require-no-regressions
```

The baseline may expose different test IDs or tests added/removed since
the base; those are listed separately. An old aggregate count cannot
classify individual regressions.

## Deliverable 2: cross-provider differential acceptance

`StochasticProviderDifferentialAcceptanceTests` checks three successive
typed random-index requests over four seeded cases. A Native provider
and a scripted replay resolve exactly the same outcomes and chronological
boundary semantics, while scripted private RNG state stays unchanged.
This is **primitive-boundary acceptance only**. It does not establish
complete mechanical equivalence of ordinary game transitions, nested hooks,
or rewards. That requires shared-origin full action traces in all four
modes: Native, scripted, ReferenceNature, external Nature.

## Deliverable 3: determinism and isolation

The focused structural job runs `SemanticDeckStateV111Tests`, including
fifteen-worker local isolation, and `RandomBoundaryJournalV111Tests`.
Existing seeded semantic-player tests were found failing in the latest
general CI and cannot currently be counted as passing evidence.
Acceptance still requires full combat replay and independent 1-versus-15
external JSONL workers with saved origin and matching final exact hashes.

## Deliverable 4: executable coverage accounting

`stochastic_coverage_gate.py` parses the regenerated RNG census and
requires complete registered-site/migration-row coverage, zero missing or
duplicate registrations, no unspecified probability provenance, and
all five contract-locator facets. Its JSON names *full continuation
acceptance* and *source callsite completeness* as NOT_ESTABLISHED even if
all 185 registered sites have rows. The workflow also enforces source
guards for ambient entropy, Native-to-Chance-Law dependencies, query
outcome blindness, ownership, and contract links.

Next acceptance must be a **runtime table** of site × mechanics entry ×
legal support × provider choice × law × save/restore × unsupported behavior.
An inventory match alone is not runtime coverage.

## Exit criteria (all required)

- [ ] No **new** failures relative to a current reproducible baseline.
- [ ] Independent conditional-law/reference-sampler tests pass.
- [ ] Full mechanical projection equivalence for matching semantic outcomes
      in representative combat, nested draw, event, reward, and terminal traces.
- [ ] Full-combat save/restore and external 1-vs-15-worker replay pass.
- [ ] Every advertised executable site passes its runtime matrix contract
      or is explicitly, safely unsupported.

This PR adds independently runnable checks and a reproducible triage
mechanism. It does **not** claim these five exit criteria are already met.
