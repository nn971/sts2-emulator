# Nature #143: Live Draw-Recycle Continuation

**Implementation PR:** [#147](https://github.com/nn971/sts2-emulator/pull/147)
**Base:** dedicated RNG/Inspector integration branch. Frozen gameplay on `main` unchanged.

## Completed slice

The actual combat `DrawPlayerHand` stage now executes pre-draw hooks and prior card draws until the draw pile becomes empty while discarded cards are still eligible. It persists the current `RunState`, remaining hand-draw count, and automatic-pipeline cursor. The controller then exposes a series of ordered finite `combat.draw_pile_recycle.swap_index` tickets on `run.shuffle`.

Nature selects each Fisher–Yates swap index. The engine applies the original postshuffle PerfectFit ordering and after-shuffle relic effects, draws the remaining cards, applies end-of-hand-draw effects, and continues from the **next** pipeline stage. Previous effects are preserved, including health, block, draw triggers and consumed card positions.

The controlled snapshot records chronological live events spanning intent and recycle choices and verifies them by exact replay from the original HP/intent prefix and ordered player actions. Fork, stale-guard, invalid support, malformed snapshot and 15-worker deterministic tests are included.

## Accepted scope

- Supported: opt-in Hive/Glory starter combat fixture; controlled initial HP, initial intents, opening shuffle, next-turn intent tickets, and end-turn hand-draw recycling.
- Other calls to `DrawCards` that need recycling fail explicitly until their own resumable operation records exist.
- Stratagem, random-cost draw effects, generated after-shuffle relic inserts, random targeting/autoplay, summons and nested hooks still require dedicated control/migration.
- All source conditional probabilities remain `UNVERIFIED`; native parity remains untested here.

**Issue status:** #143 stays open. See [the implementation status](NATURE_ISSUE_143_IMPLEMENTATION_STATUS.md) for earlier milestones. The desired RNG and Inspector plans are unchanged.
