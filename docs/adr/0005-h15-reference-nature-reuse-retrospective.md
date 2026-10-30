# ADR 0005: H15 must reuse the existing stochastic-provider stack

- **Date:** 2026-10-30
- **Status:** accepted; migration tracked on independent H15 research branches
- **Supersedes:** treating H15's diagnostic Python law sampler as a second authoritative Nature implementation
- **Related:** [ADR 0004](0004-provider-owned-stochastic-state-and-chance-law.md), [canonical architecture](../UNIFIED_STOCHASTIC_ARCHITECTURE.md)

## The error and its consequences

While extending H15 combat search, we repeatedly reasoned from the latest
H15 milestone and introduced independent stochastic-control code without
first examining the existing **full** emulator provider inventory.

In particular, we introduced `sts2_ai/training/h15_experimental_rollout.py::_sample_law`
as a new weighted Nature sampler and allowed H15's cursor/decision-history
logic to grow separately, even though the emulator already provides
`ChanceLawV111`, `ReferenceNatureV111.SampleValidated`, atomic
`ReferenceNatureSessionV111` / `ReferenceNatureRunActionSessionV111`,
specialized reward/event sessions, and H8's fast local reference provider.
We also described public-history conditioning as generally missing even
though `H8ReferenceNaturePosterior` already implements a restricted
**fixed-fixture, combat-local** conditional distribution.

The resulting duplication made unsupported sites appear to require new
systems instead of routing work through established semantic continuation
owners. An independent H15 prototype-RNG candidate was repeatedly
described without enough emphasis on its distinct distributional provenance.
The fix is architectural consolidation, not an additional ad hoc probe.

## Non-negotiable engineering check before modifying stochastic behavior

1. **Read the ownership contract first:** `docs/UNIFIED_STOCHASTIC_ARCHITECTURE.md`,
   this ADR and ADR 0004; then inspect `StochasticSiteRegistryV111`,
   `ChanceLawV111`, `ReferenceNatureV111`, the existing reference session
   family and actual caller sites. This check applies even to seemingly
   local H15 patches.
2. **Classify the operation's owner:** deterministic mechanics; native
   provider; reference/learned/scripted Nature; read-only law query;
   player observation; privileged inspector. A new path must name the
   missing owner/capability, rather than replicate an existing one.
3. **Reuse ReferenceNature for sampling:** no Python or H15-specific
   duplicate integer-weight roulette sampling when the same declared law
   can be passed to `ReferenceNatureV111.SampleValidated`. Preserve
   an explicit `UNVERIFIED` law and independent sampler state where
   source calibration is absent.
4. **Reuse semantic continuations for effects:** do not add a new card,
   potion, shuffle, deck-constraint, random-target, or reward resolver if
   a typed source-mechanics continuation already exists. An old resolver
   may have narrower admission/provenance; establish that constraint
   and add the missing adapter rather than bypassing it.
5. **Keep three independent claims separate:** legal controlled mechanics,
   `SOURCE_VERIFIED` event-local probability laws, and a
   history-conditioned posterior for the *specified prior*. A fixed
   fixture posterior must never be renamed as the complete authentic
   native-run posterior. Native-structure/prototype-RNG source records
   must not be relabeled native-combat-RNG.
6. **Keep strict fail-closed boundaries:** unexpected mechanics/hidden
   state rejection must not silently switch providers; never turn
   incomplete/unsupported simulations into critic/PUCT targets.
7. **Check whether docs are current:** distinguish append-only dated
   research history from accepted architecture. Update the current
   implementation crosswalk and this ADR when modifying the provider
   interfaces.
8. **Protect experiments:** do not modify the pinned branches or working
   directories used by a running training experiment. Changes go to a
   separate research branch with its own revision.

## Concrete acceptance for removing the H15 duplication

- H15 diagnostic reference selections use the emulator's
  `ReferenceNatureV111.SampleValidated` and an atomic, forkable
  `ReferenceNatureStateV111` cursor.
- The Python diagnostic must not contain a second weighted-law RNG
  implementation. External `choose` remains distinct for scripted
  or learned Nature and does not advance the reference cursor.
- Session-level failure retains the pre-choice cursor and mechanics
  state; the journal records exact event/law/outcome/provider identity.
- Unsupported stochastic sites remain identified, never implicitly
  delegated to legacy RNG or misreported as source-calibrated.
- Regression tests exercise reproducible reference selection, refusal
  of stale versions and incorrect law, and no ReferenceNature cursor
  consumption by ordinary player actions or external scripted choices.

This ADR records a concrete **assistant design mistake** and a required
code-review checklist. It is not evidence that all stochastic sites or
native-game probability calibration are complete.
