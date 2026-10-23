# Nature #143 — Bottled Potential hand-return shuffle

## S0 stochastic site contract

`combat.hand_return.draw_pile.shuffle_swap_index` is conditioned on the saved
draw-pile-plus-hand instance order, current permutation and next descending
swap ordinal. The same continuation can publish
`combat.relic.after_shuffle.generated_card.random_insert_index` for each
generated insertion in relic application order. Those pool constraints and
swap transcripts are private; the fair player observation does not expose
them or the unresolved deck order.

The saved mechanics queue resumes only after the final shuffle swap and
insertion choice, then executes the ordinary hand draw. An operation that
cannot preserve the shared unresolved-pool constraints fails closed before a
partial state is published. The per-swap law is `SOURCE_VERIFIED` for the
pinned Native `NextInt` transform on the declared bound; stream assignment,
pool completeness, seed frequencies, and cursor parity are not certified here.

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
new native reference-mod comparison. Each per-swap law is `SOURCE_VERIFIED`
for the pinned Native `NextInt` transform on the declared bound. The native
`run.shuffle` assignment, source-pool completeness, seeded provider cursor
parity, and seed frequencies remain unverified. When the saved event carries
Native `run.shuffle` ownership, the run-owned `SemanticOtherChanceStateV111`
delegates each swap through that stream and resumes the same continuation
without adding a Nature history choice. The source owner prefix and ordered
pool fingerprint match `ReturnHandToDrawAndShuffle`; this does not establish
installed-game stream or cursor parity.
