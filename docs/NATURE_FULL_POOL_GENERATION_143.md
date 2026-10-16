# Nature #143: Resumable full-pool card-generation shuffles

**Implementation:** [PR #151](https://github.com/nn971/sts2-emulator/pull/151), developed only on the RNG/Inspector integration branch.

## Source operations and boundaries

The actual `ResolveOperations` effect queue now pauses before `PrototypeRng.Shuffle` or `NativeRngV111.Shuffle` consumes the first swap of an eligible card-generation pool. The supported operations are:

- `ChooseGeneratedCards`, including Attack/Skill/Power Potions and Discovery;
- `ChooseGeneratedColorlessCards`, including Colorless Potion;
- `CreateDistinctColorlessCardsInHand`, including Jack of All Trades;
- `CreateDistinctCharacterCommonCardsInHand` (common-card generators).

Source `TakeRandom/GetDistinctForCombat` shuffles the **entire eligible pool** via descending Fisher–Yates, even if one to three cards are selected. Every swap has a compact `bounded_integer` ticket at `combat.generation.full_pool_shuffle.swap_index`. The candidate card models in their *current order* are exposed only to Nature as `pending.candidateCardIds`; the player's standard fair observation remains separate. Prototype RNG uses the `combat` stream; native-card-generation configured effects use `run.combat_card_generation` when a native run stream exists. These are source labels, not claims of seeded native cursor parity. All probability laws remain `UNVERIFIED`.

The private in-flight checkpoint retains the exact original filtered pool, current permutation, next index, source effect, source-card ID, selected prefix count and parent card/choice/event continuation. Every swap is checked against its stable owner/source/ordinal/support and the partial permutation is reconstructed from the **entire current swap transcript**, ruling out forged reorderings. On the last swap the engine creates the exact prefix without re-running earlier operations. Choice effects then yield a normal `select_cards` player action with cost/upgrade/retain and affliction handling; direct generation effects create the prefix in the player's hand and resume the earlier queued operations.

## Semantic JSONL/Python fixture bridge

The semantic P0 session can opt into this continuation with
`fixture_pool_generation_potion_id="proto.potion.colorless"`. It exposes each
bounded swap as a typed `generated_card_pool_shuffle` Nature event carrying
the current and original candidate order. Resolving the final swap resumes
the original Colorless Potion continuation and publishes its ordinary
`player_card_selection` action. The semantic transcript records and replays
the pool swaps and the subsequent player choice separately; candidate card
models stay out of the fair player observation. The fixture currently covers
this potion path and does not claim native potion-acquisition provenance or
verified probability laws.

## Validation and boundaries

Focused tests exercise Attack and Colorless Potions through the **real potion-use effect kernel** on a fixture-modified playable combat state, including a resumed player card-selection action, and the distinct-common operation through an internal operation fixture. They verify full-pool RNG call shape, source card-model order, source eligibility, invalid/stale swap decisions, mid-shuffle forgery checks, replay event IDs, and 15-worker independent execution.

The test fixture adds potion/card inputs outside the native starting deck; this validates the operation's mechanical continuation but is not a claim that the standard `controlled_begin` creates those consumables. Save/restore of these externally mutated starting states still requires explicit fixture-input provenance in the replay contract. Broad native parity and source-law calibration remain deferred.

Enemy-side draw/discard insertions, random cost effects, death/summon/nested random hooks, complete controlled terminal rollouts, and other chance-site migrations remain in [issue #143](https://github.com/nn971/sts2-emulator/issues/143). Frozen `main`, `docs/DESIRED_RNG_IMPLEMENTATION.md`, and `docs/NATIVE_FIDELITY_INSPECTOR_PLAN.md` remain unchanged.
