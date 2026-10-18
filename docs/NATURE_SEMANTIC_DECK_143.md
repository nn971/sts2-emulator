# Semantic draw-pile state — opt-in v1 (#143)

**Proposed semantic contract:** `docs/DESIRED_RNG_IMPLEMENTATION.md` (updated
2026-10-15 in PR #154). **Code:** `SemanticDeckStateV111.cs`.
**Schema:** `sts2-v111-semantic-deck-v1`.
**Scope:** isolated, pure RNG provider-state kernel. Gameplay integration
and JSONL capability negotiation remain separate milestones.

## Two provider-owned representations

- `native-exact-v1`: every top-first slot is an exact known card instance.
  The native engine still runs source-pinned Fisher–Yates and stream/cursor
  updates; `CommitNativeShuffle` stores its resulting order. Requiring a
  concrete slot produces no Nature chance ticket.
- `nature-deferred-v1`: a source-triggered shuffle creates an unresolved
  pool and slots that refer to it. The engine requests a card when mechanics
  need a specific hidden position. Nature receives the **ordered remaining
  card instance IDs**, selects one, and the provider locks it into that
  position. A previous reveal or forced placement is reused without a
  second chance decision.

Both paths use the same top-first slot/instance operations. Their private
realizations differ by design. No shadow native RNG advances are claimed
for Nature interventions.

## Shared-pool positional constraints

A position can be a known instance or a reference to a named unresolved
pool. A pool lists the remaining distinct identities, and the number of
referencing slots equals the number of remaining identities.

For example, after `BeginNature([a,b,c,d])` and
`InsertKnown(2, x)` the state is:

```text
top -> [pool P] [pool P] [known x] [pool P] [pool P] -> bottom
P.remaining = {a,b,c,d}
```

The insertion fixes `x` exactly at the specified position without
arbitrarily assigning which two original cards are above it. Drawing
any card from `P` removes that identity from **the same** pool used on
both sides of `x`. With otherwise exchangeable shuffled positions, this
is the conditional law of a once-uniformly-shuffled permutation, and it
supports stepwise enumeration without constructing all permutations.

### Supported semantics

- Source-triggered `ReshuffleNature` resets the permutation constraint
  at a new epoch. Native `CommitNativeShuffle` accepts only a permutation
  of current identities.
- `Require(state, 0, "draw")` requests one unknown top identity or reuses
  a committed identity; `Resolve` changes provider state only. The engine
  must run its usual draw/event/hooks exactly once after `DrawKnown`.
- `Require(state, index, "peek"|"scry")` locks and marks one
  position public. Requiring the same revealed position again consumes no
  chance decision. `Observe` exposes only the card count and positions
  already explicitly revealed/public, never the unresolved pool.
- `InsertKnown`, `PlaceTop`, `PlaceBottom`, `MoveKnownToTop`, and
  `RemoveKnown` preserve card-instance identities, including mutations
  held by the game engine. Exact insertions in the middle preserve both
  unknown ranges' shared pool constraints.
- `Fork`, `Save`, and `Restore` round-trip the complete private state,
  pending choice, epoch, and ordered Nature transcript. Resolution validates
  the source state fingerprint, selected index, and current candidate pool
  before publishing a child.
- A hidden card removed or moved from an unresolved pool without knowing
  its old position can change how many cards lie on either side of an
  inserted anchor. Those operations **fail atomically** with
  `UNSUPPORTED_SEMANTIC_DECK` until the relevant random-position
  continuation is modeled.

### Explicit protocol separation

The previously implemented
`controlled-v111-playable-fixture-v1` still commits a card order by
legacy Nature Fisher–Yates swap tickets. Its transcripts are unchanged.
`SemanticDeckStateV111` uses a new schema and **does not emit swap
tickets for its unresolved shuffle**. Each unknown identity is chosen
only when revealed; the same deck randomization is never counted at
both swap and reveal boundaries.

A future versioned playable bridge will own a deck provider state inside
the serializable `RunState` continuation and call this kernel at actual
`DrawCards`, peek/scry, forced insertion, discard recycle and draw-trigger
hooks. The current PR intentionally keeps the kernel isolated from the
legacy public `controlled_step` / `nature_resolve` protocol and from
native game RNG state, so existing consumers are unaffected.

## Test gates in this slice

- Source-ordered Nature instances without replacement and concrete native
  top-first order.
- Known-top/bottom and middle insertions, shared pool coupling, peeks,
  scry discards, contiguous unresolved-pool removal, and rejection of removal
  from a pool split around known anchors.
- Nature shuffle resets previously revealed positions; native shuffle
  preserves the exact card multiset.
- Exact enumeration for a 3-card shuffle with a fixed middle insertion:
  every permitted final order appears with probability `1/3!` **under an
  explicitly assumed uniform synthetic permutation law**. This is a
  combinatorial regression, not native conditional-law calibration.
- Stale event rejection, tampered eligible support, malformed pool sizes,
  fork/save/restore round-trips, and schedule-independent 15-worker events.

## Next integration milestones

Manual Distilled Chaos now uses the `draw` purpose to stage selected top-card
identities and resume the saved autoplay operation queue. This covers the
non-recycling player-action boundary; Mayhem's automatic-pipeline path and
discard recycle remain unsupported. No current gameplay callsite initiates a
`peek` or `scry` request, so those reveal APIs remain kernel-only.

1. Integrate the semantic provider with the actual `DrawCards` action/
   automatic/initial hand-draw continuations, preserving one
   `CardDrawn` hook per physical draw and source-timed recycle hooks.
2. Support forced/random insertions and repeated reveals across multi-card
   peeks/scry, including player-controlled scry discard/reordering.
3. Expose the new schema and capability through a separate opt-in JSONL
   handle with fair observation and semantic Nature tickets.
4. Introduce weighted, source-auditable visible-intent metadata, then run
   native seeded parity and complete controlled terminal rollouts.

Native source probabilities, enemy intents, full-game mechanical parity and
the model's conditional reference distributions remain `UNVERIFIED`.

## S0 stochastic boundary: deck reveal

`combat.draw_pile.reveal_card` resolves a physical identity when a peek or
scry operation needs an unknown position in a shared draw-pile pool. The
Nature request contains the remaining legal card instances and the position
being revealed; pool order and unresolved positions remain private. The player
projection receives only identities made public by the completed peek or scry
mechanic, not the remaining hidden order.

The deck provider locks the chosen identity into that position and marks the
revealed slot public. The original reveal action then resumes through its
ordinary resolver once; requiring the same revealed position again does not
create a second Nature choice. Mutations that split an unresolved shared pool
remain unsupported and fail closed. This contract adds no privileged
per-choice inspector transcript.

## S0 stochastic boundary: ordinary draw-pile shuffle

The `combat.draw_pile.shuffle.swap` row records native primitive shuffle
swaps over the physical draw-pile instance order. Native commits the shuffled
order through its source RNG and primitive journal. Nature represents an
unresolved shuffle as a provider-owned permutation constraint and chooses a
physical identity only when mechanics reveal a position; the same shuffle
must not generate both swap tickets and card-identity decisions.

The player projection exposes pile counts and ordinarily revealed cards, not
the unresolved order or provider state. The draw-pile adapter applies the
mechanics membership change and provider transition together; the ordinary
draw resolver runs the card and hook effects once after an identity is
committed. A controlled request at an arbitrary shuffle callsite without
that saved continuation fails closed. A read-only law now reports exact Native
`NextInt` masses for one declared swap bound, owner, and ordinal; it verifies
the bounded transform only and is distinct from the deferred card-draw law.
The query does not provide a privileged per-swap transcript to Nature or a
complete shuffle continuation.
