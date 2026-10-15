# Nature #143 — draw-triggered cost continuation (legacy fixture)

## Source and ordering

The actual `DrawCards` loop moves a concrete card instance into the hand,
then checks drawn-card relic cost randomization, then Slither enchantment
randomization, and **only then** dispatches the ordinary `CardDrawn`
event/hook. The controlled hand-draw continuation persists after the physical
move and before the first random cost.

- Snecko Eye `RandomizeDrawnCardCostMaxExclusive` offers a
  `combat.card_draw.relic_energy_cost` bounded ticket when the original card
  has a fixed, nonnegative cost.
- Slither offers a separate `combat.card_draw.slither_energy_cost` ticket
  with four choices. When both apply, the second overrides the first, as in
  the existing native/prototype execution order.
- The original card identity, remaining draw count, hook depth and
  automatic-pipeline cursor are retained across both choices. Earlier draws,
  effects and cost changes are never re-executed.
- After the last ticket, `CardDrawn` dispatch runs exactly once and the
  automatic hand draw continues until the next pending chance or player turn.
  Each pending ticket is tied to the concrete card instance and rejects
  stale fingerprints or malformed source support.
- The default, unguarded `DrawCards` path keeps its previous RNG semantics.

## Relationship to the revised desired RNG architecture

The latest [desired implementation on main](https://github.com/nn971/sts2-emulator/blob/main/docs/DESIRED_RNG_IMPLEMENTATION.md)
separates **native exact hidden-order state** from **semantic deferred Nature
draw-card decisions**. This patch extends the existing explicitly versioned
`sts2-v111-controlled-playable-fixture-v1` only: it works on a deck that
was already shuffled through the legacy Nature swap tickets and adds *cost*
tickets for concrete physically drawn cards. It introduces **no extra
draw-card identity chance** and thus does not double-count an old shuffle.
A future opt-in semantic deck schema must own its own unresolved constraints,
forced-top placements, peeks, reveals, and per-card draw identities.
Legacy swap transcripts must remain independently replayable.

## Coverage and limitations

Only actual automatic end-turn `DrawPlayerHand` and its resumable discard
recycles support these cost tickets. Opening hand/ordinary card-effect draws
still fail closed when a cost RNG call is encountered, because their outer
continuations have not yet been migrated. Randomness inside nested
`CardDrawn` choice hooks still requires an additional continuation stage.
Tests use explicit Snecko/Slither fixture modifications; the mutable fixture
cannot claim native reward provenance or a normal origin-restorable snapshot.
All reference chance laws remain `UNVERIFIED`.

Next work should implement the new semantic provider-owned deck protocol,
then align mid-action card draws and nested `CardDrawn` hooks with that
state. Preserve complete native RNG stream/seed audits in parallel.
