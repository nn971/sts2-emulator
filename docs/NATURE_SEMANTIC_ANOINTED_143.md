# Semantic Anointed rare-card shuffle

## Supported operation

The semantic pilot now supports Anointed's
`combat.draw_pile.random_rare_to_hand.shuffle_swap_index` operation. Its
existing full-pool shuffle continuation saves the real effect queue before
the first swap and publishes one bounded Nature event per source-ordered
Fisher–Yates swap. The pool contains only Rare physical card instances in
draw-pile order. Event identities use stable tokens of the form
`card-instance:<instanceId>:<cardModelId>` so duplicate card models remain
distinct.

After the last swap, the engine takes the source prefix up to the card's
amount and remaining hand capacity. It moves those instances from draw pile
to hand without dispatching ordinary draw hooks, matching the source effect.
The semantic deck removes the same identities. If that removal would split
hidden positional constraints in a way the current deck model cannot
represent, the continuation fails closed.

When the run-owned Native provider is active, each saved swap is delegated to
`run.combat_card_selection` and the same Anointed continuation resumes without
a Nature history choice. The provider transcript records the bounded Native
request privately; Native stream attribution and cursor parity remain open.

The source action now enters this continuation from the physical Anointed
card and its exact amount-ten Rare-to-hand operation; it does not require a
seeded-fixture marker. The seeded semantic-pilot fixture remains available to
replace its first opening-hand card with Anointed and populate the remaining
opening draw pile with supported Rare cards so the multi-card shuffle is
reachable. The fixture does not claim native card-acquisition history. JSONL clients resolve each event with
`semantic_resolve_pool_shuffle(swap_index)` under the pending kind
`random_rare_to_hand_shuffle`.

## Limits and verification

Only the player-action card route is covered. Its per-swap Chance Law uses
exact `SOURCE_VERIFIED` masses for the pinned Native `NextInt` transform on
the declared bound. The migration does not establish native stream assignment,
pool completeness, seeded cursor parity, or seed frequencies. The Release solution build succeeded with zero warnings and errors for
this change. Tests and mechanics regressions were not run, so controlled
mechanics remain unverified.

## S0 stochastic boundary: Anointed rare-card shuffle

The shuffle support is the ordered set of physical Rare card instances in the
draw pile at the saved effect boundary. Duplicate models remain distinct by
instance identity. The player-visible projection shows the resulting cards
moved to hand, not the source pool order or swap prefix; Nature receives the
current swap support in the serialized pool-shuffle event.

The final shuffle resumes the original effect queue and moves the selected
physical instances through the normal card-zone mechanics. Hidden-position
removals that would split unresolved deck constraints fail closed. The
operation remains in its opt-in source action route. Its per-swap law is
**SOURCE_VERIFIED** for the pinned Native `NextInt` transform on the declared
bound; native stream assignment, pool completeness and cursor parity remain
open.
