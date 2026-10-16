# Nature #143 — Bottled Potential hand-return shuffle

The guarded combat operation `ReturnHandToDrawAndShuffle` now suspends before
the first Fisher–Yates swap when Bottled Potential is used in a Nature semantic
combat. Its private continuation stores the original draw-pile-plus-hand pool,
the current permutation and next descending swap index, along with the
remaining operation queue and source completion state. Resolving every swap
commits the selected order once, moves the old hand into the draw pile, and
preserves Perfect Fit ordering.

The semantic deck appends the returned hand instances as private known slots,
then commits the Nature-selected top-first order and clears stale pools. The
normal continuation resumes Bottled Potential's five-card draw. If an active
relic inserts a generated card after the shuffle, each insertion uses the
existing `combat.relic.after_shuffle.generated_card.random_insert_index`
continuation and semantic event before the potion draw continues.

The semantic JSONL protocol exposes `hand_return_shuffle` with the site
`combat.hand_return.draw_pile.shuffle_swap_index`. This extends the existing
full-pool shuffle continuation and draw-insertion event; it does not claim a
new native reference-mod comparison. The native `run.shuffle` assignment,
seeded provider cursor parity, and reference probability law remain
unverified. Probability status is `UNVERIFIED`.
