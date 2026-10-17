# Nature #143: Live Draw-Recycle Continuation

**Implementation PR:** [#147](https://github.com/nn971/sts2-emulator/pull/147)
**Base:** dedicated RNG/Inspector integration branch. Frozen gameplay on `main` unchanged.

## Completed slice

### S0 stochastic site contract: `combat.draw_pile_recycle.swap_index`

At the supported end-turn hand-draw boundary, this site owns the descending
Fisher–Yates swaps over the remaining discard-pile card instances. The saved
draw-pile/hand counts, current source pool and next swap ordinal constrain
each Nature request. The player observation exposes drawn cards and visible
zones, not the unresolved permutation, remaining instance list, or swap
transcript.

`SemanticCombatDrawEngineV111` resumes the saved draw stage after the final
swap, applies Perfect Fit ordering and after-shuffle effects, then continues
the requested draw count. Other `DrawCards` callers that need a recycle do not
have this continuation and fail closed. `NatureBoundaryMetadataV111` now
attaches the generic request law's ID and status to each pending swap, and
the law status to each replay transcript entry. Canonical Native `run.shuffle`
requests have exact `NextInt` 53-bit grid masses and a `SOURCE_VERIFIED`
bounded transform law. Legacy requests and full seeded recycle parity remain
`UNVERIFIED`.

The actual combat `DrawPlayerHand` stage now executes pre-draw hooks and prior card draws until the draw pile becomes empty while discarded cards are still eligible. It persists the current `RunState`, remaining hand-draw count, and automatic-pipeline cursor. The controller then exposes a series of ordered finite `combat.draw_pile_recycle.swap_index` tickets on `run.shuffle`.

Nature selects each Fisher–Yates swap index. The engine applies the original postshuffle PerfectFit ordering and after-shuffle relic effects, draws the remaining cards, applies end-of-hand-draw effects, and continues from the **next** pipeline stage. Previous effects are preserved, including health, block, draw triggers and consumed card positions.

If a recycled hand draw triggers an after-shuffle relic insertion, Nature now chooses each generated card's draw-pile position in relic application order before the hand draw resumes. A recycled draw that triggers Stratagem pauses for its ordinary draw-pile selection before continuing the saved remaining count. Draw-cost tickets and another recycle reuse that same automatic-pipeline cursor. These compositions are build-verified; existing fixed hand-draw/recycle continuation coverage remains unchanged.

The controlled snapshot records chronological live events spanning intent and recycle choices and verifies them by exact replay from the original HP/intent prefix and ordered player actions. Fork, stale-guard, invalid support, malformed snapshot and 15-worker deterministic tests cover the previously implemented fixed hand-draw/recycle path.

## Accepted scope

- Supported: opt-in Hive/Glory starter combat fixture; controlled initial HP, initial intents, opening shuffle, next-turn intent tickets, and end-turn hand-draw recycling.
- Other calls to `DrawCards` that need recycling fail explicitly until their own resumable operation records exist.
- Stratagem and generated after-shuffle relic insertions after an end-turn recycle are supported. Random-cost draw effects outside the hand-draw stage, random targeting/autoplay, summons and nested hooks still require dedicated control/migration.
- The Native bounded-index transform is `SOURCE_VERIFIED`; native seeded recycle parity remains untested here. Other source conditional probabilities remain `UNVERIFIED`.

**Issue status:** #143 stays open. See [the implementation status](NATURE_ISSUE_143_IMPLEMENTATION_STATUS.md) for earlier milestones. The desired RNG and Inspector plans are unchanged.
