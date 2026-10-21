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

That paragraph describes the original fixture patch. The current semantic
deck and draw owners provide the separate provider-owned path described in
the [unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md);
the fixture snapshot and its legacy swap transcript remain compatible.

## Coverage and limitations

The original legacy controlled-playable fixture boundary covers automatic
end-turn `DrawPlayerHand` and resumable discard recycles. The current semantic
path also supports standalone semantic draws and nested card-effect draws
under a Nature-owned semantic deck. Their saved draw and outer card-play
continuations retain the remaining draw count, deferred keywords, and effect
queue across a drawn-card cost; supported `CardDrawn` action choices use the
existing hook-choice continuation. Automatic-pipeline support uses these
same draw and cost owners at its admitted cursors. Unsupported semantic
boundaries still fail closed, and standalone draws without a semantic provider
remain unsupported.

Tests use explicit Snecko/Slither fixture modifications; the mutable fixture
cannot claim native reward provenance or a normal origin-restorable snapshot.
The Chance Law reports exact Native `NextInt` grid masses and
`SOURCE_VERIFIED` for the bounded-cost transform on its declared support.
Native stream assignment, seed frequencies, cursor parity, and fixture
provenance remain separate.

The remaining work is broader resume consolidation and cross-provider
mechanical equivalence. Native stream assignment, seed frequencies, cursor
parity, and fixture provenance remain separate audits and are not established
by this semantic continuation.
