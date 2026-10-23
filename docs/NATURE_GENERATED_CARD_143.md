# Nature #143: Live random generated-card continuation

**Implementation branch:** `feature/nature-143-live-random-generated-cards` on the dedicated RNG/Inspector integration head.

## Scope

The ordinary `PrototypeGameEngine.ResolveOperations` kernel now suspends at a random generated-card draw for five source-shaped operation kinds:

- `CreateRandomFreeCharacterCardInHand` (Chaos-style, free during the turn)
- `AddRandomZeroCostCardsToHand`
- `CreateRandomCharacterAttackCardsInHand`
- `CreateRandomCharacterSkillCardsInHand`
- `CreateRandomCharacterPowerCardsInHand` (including Orobic Acid)

The engine saves the original effect, candidate card IDs in their *source-defined order*, the remaining generation count, remaining queued effects, and parent card/event/choice continuation. `combat.generated_card.select` exposes one finite `choose_index` ticket per selected generated card. Candidate indices are in the typed ticket; actual card model identifiers are available to Nature in the additional `pending.candidateCardIds` field. A stable pool fingerprint is part of the ticket owner, so different eligible pools cannot accidentally share event identity. The v1 semantic event retains its `prototype.combat` stream label for protocol compatibility. Native execution now uses `run.combat_card_generation`; the retained prototype profile uses `combat` through the typed legacy provider. Native cursor parity and the conditional law beyond the bounded-index transform remain unverified.

The persistent semantic pilot also admits Jackpot's three random zero-cost
card additions from its physical source-card identity. Each pick reuses the
existing generated-card event; the guard checks the saved `CardPlayed`
continuation, source upgrade level, and exact remaining-count suffix before
the next pick resumes. Fixture setup remains optional.

The read-only Chance Law reports exact Native `NextInt` 53-bit grid masses and `SOURCE_VERIFIED` for the bounded-index transform on the declared ordered model pool. This does not establish complete Native pool eligibility, stream attribution, seed frequencies, cursor parity, or equivalence with the retained legacy modulo provider. Resumption creates exactly one card using the original generated-card constructor, preserving the requested temporary cost and upgrade. For repeated generation it creates the next chance event; after the last choice it resumes the saved effect queue and parent card/play/event hook chain. Earlier operations never reexecute. Fair `prototype-fair-v0` observation is unavailable while a generation choice is pending.

## Verification and boundaries

The new focused tests cover a real Orobic Acid potion through its Attack, Skill and Power generation calls; result identity, ordered support, stale/illegal indices, independent branch selection, tampered support rejection, and deterministic fifteen-worker repetition. The test injects Orobic Acid as a fixture potion into an isolated playable combat for coverage. The original starter configuration is not asserted to include the potion, and a modified fixture cannot be treated as native-provenance initial state.

This slice intentionally leaves card-selection pool shuffles (`ChooseGeneratedCards`), distinct full-pool shuffles, random-generated insertion into draw piles, randomized card cost, nested death/summon hooks, native RNG parity, and terminal rollout for later stages of issue #143. Unsupported native/prototype calls fail closed.

The frozen Silent gameplay branch and `docs/DESIRED_RNG_IMPLEMENTATION.md`, `docs/NATIVE_FIDELITY_INSPECTOR_PLAN.md` are unchanged.
