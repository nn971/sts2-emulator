# Hypothetical draw-order operation v1

**Scope:** mechanics research in the independent STS2 emulator prototype
(`prototype/full-run-silent`). This is a conditional-transition *primitive*,
not an exact native-game belief-state sampler.

The JSONL bridge provides two new operations:

- `reset_hypothetical` with `search_seed` (32 hex digits) and optional
  `ascension`; returns a handle whose provenance is marked hypothetical.
  The caller must generate search seeds independently of any live game
  seed/RNG state. The prototype internally prefixes them with
  `fair-hypothetical:`.
- `hypothetical_draw_order` with `state_handle` and `ordered_cards`:
  an ordered list of `{"card_id": "ID", "upgrade_level": 0}` entries
  in **first card drawn first** order. Returns a *new* handle, schema ID
  `prototype-hypothetical-draw-order-v1` and unchanged public
  observation hash; it does not return the hidden card instance order.

The bridge refuses this operation on any handle from ordinary `reset`
and on hypothetical states not *directly* descended from a newly
entered combat. The fresh-combat certificate propagates through
zero-decision forks and this injection, but not through subsequent
combat actions.

At the Core layer, `PrototypeHypotheticalDrawOrder.Apply` validates
that the entire persistent deck is accounted for by the visible hand
plus the hidden draw pile, the draw pile card variants are unmodified
and not temporary/generated, it is Turn 1, the combat has no pending
choice or discard/exhaust/play pile, and the supplied permutation
exactly matches all remaining card IDs + upgrade levels.

It then forks the full hypothetical run and changes *only*
`Combat.DrawPile` to the specified order, reversed to match the
engine's **pop-from-end** draw semantics. All other combat state,
legal actions and RNG streams stay unchanged. The source handle
remains unchanged, and the next regular combat transition uses
the injected draw order through the unmodified engine.

## What this establishes

- A player-visible card-variant outcome can be realized as a
  **mechanically valid full simulator state transition** without
  exposing the original private draw order.
- Cards drawn on the next turn obey the same normal engine effects,
  HP changes, turn dispatch, discard bookkeeping and legal actions.
- The operation is reproducible for given hypothetical seed/ordered
  card variants; deterministic tie-breaking between otherwise
  identical card instances uses their instance IDs internally.

## What this does *not* establish

- **Not a posterior sampler:** it does not condition the other hidden
  streams, map generation, encounter selection or previous visible
  history. Neither live game RNG nor an oracle-exact fork is used in
  this *explicitly hypothetical* operation.
- **Not an exact joint chance law:** the underlying combat RNG
  stream is not advanced when the externally selected permutation
  is injected, so correlations between the overwritten shuffle and
  later uses of that same stream are not preserved. This is an
  intentionally idealized structural perturbation, not a valid
  full-game chance distribution.
- **Not every opening:** other effects can alter the combat zones
  during combat start; the strict inventory guards reject those.
- **Not a reason to label PUCT targets fair:** caller must establish
  source-handle provenance, public-history compatibility and a
  mechanically correct joint RNG distribution before feeding
  trajectories to teacher-free training.

Tests exercise multiset validation, source immutability, unchanged
public observations and RNG snapshots, and normal successor combat
actions. The CI integration smoke additionally checks the versioned
JSONL API, live-handle denial, invalid permutations, and loss of the
fresh-combat privilege after a combat action.

Next, introduce **chance-stream separation or a certified transition
kernel** for card draws that accounts for conditioning of the
concrete RNG streams. Then connect the versioned operation to the
parent `sts2-ai` search adapter with a deliberately named
`approximate-draw-injection` experiment regime; never register
it as `history-conditioned-fair-v1` without joint-law proof.
