# Nature #143 — semantic card draw through real combat mechanics

This slice connects the separate provider-owned
`sts2-v111-semantic-deck-v1` kernel to the actual
`PrototypeGameEngine.DrawCards` implementation, via the opt-in
`SemanticCombatDrawEngineV111` fixture envelope. It is deliberately a
**single draw-segment integration seam**, not yet a whole-turn or JSONL mode.

## Mechanics and continuation

- Input: an already constructed combat `RunState`, with physical card
  instances in `CombatState.DrawPile`, no pending choice or automatic turn
  continuation. The source is a *conditioned pre-draw combat*, not a
  previously Nature-shuffled controlled fixture.
- `Begin(source, n)` attaches `SemanticDeckStateV111.BeginNature` to the
  current draw-pile instance multiset. No Nature shuffle swaps are consumed.
- The **actual** `DrawCards` loop checks hand capacity and draw-prevention
  hooks, then calls `Require(deck, 0, "draw")`. At an unknown top position
  it suspends *before* moving the card, saving the current drawn state and
  remaining count. Nature sees an ordered physical card-instance candidate
  list and stable semantic event ID.
- `Resolve` checks the parent fingerprint, commits one chosen instance
  with `SemanticDeckEngineV111.Resolve`, and resumes the same
  `DrawCards` kernel. It moves **that instance** out of the physical pile,
  adds it to hand, and dispatches `CardDrawn` once, including its normal
  counters and deterministic hooks. Subsequent draws suspend individually.
- The draw pile's **physical membership** must equal the provider's union
  of known slots and unresolved pools at every consumption boundary.
  Mechanics may store arbitrary unrevealed physical order internally, but
  that order never resolves a semantic Nature choice.
- `SemanticCombatDrawV111` persists the untouched source snapshot, the
  live `RunState`, the requested draw count, remaining draw cursor, and
  semantic card, cost and insertion transcripts. `Restore` re-executes the
  actual mechanics from the conditioned source using those recorded
  decisions, then compares the entire resulting snapshot. Forks and
  stale/forged candidates fail closed.

## S0 stochastic boundary: physical card draw

`combat.draw_pile.draw_card` identifies the physical card instance consumed by
one source `DrawCards` iteration. The remaining pool identities and hidden
position constraints are private; a pending Nature event receives only the
ordered legal instance support. The fair player projection reveals the card
only after the ordinary draw mechanics commit it.

The saved draw resolver suspends before moving the unknown top card, applies
the selected physical identity once, dispatches `CardDrawn` once, and resumes
the remaining draw count and its saved hooks. Restore replays the source
segment from its conditioned checkpoint and validates the resulting run.

This continuation is supported only at boundaries that preserve the semantic
draw or automatic hand-draw frame. Other callers that cannot save and resume
the shared-pool constraints remain unsupported and fail closed. This row's
identity law remains `UNVERIFIED`; source stream parity and cursor behavior are
separate from the resolver contract.

## Supported first slice

- Consecutive ordinary draws and deterministic `CardDrawn` hooks in a
  given combat snapshot.
- Source-timed discard recycle with Perfect Fit ordering, after-shuffle relic
  insertions, Stratagem selection, and drawn-card cost decisions in the
  standalone segment; the automatic hand-draw integration supports
  after-shuffle insertions, Stratagem selection, and drawn-card cost stages.
- Ordinary source action choices opened by a standalone draw's `CardDrawn`
  hooks preserve the remaining draw operation and resume it after selection.
- Ordinary source action choices opened by automatic `DrawPlayerHand`
  `CardDrawn` hooks preserve the automatic stage cursor and resume the same
  hand-draw request after selection.
- Effect-triggered recycle swaps use the typed
  `combat.draw.recycle.shuffle_swap_index` continuation. Their per-swap Chance
  Law has exact `SOURCE_VERIFIED` Native `NextInt` masses on each declared
  bound; source-site attribution, pool completeness, stream/cursor parity, and
  seed frequencies remain open. The unresolved physical-card draw law stays
  `UNVERIFIED`.
- Exact card instance identities and no replacement within the pile.
- Draw count up to available pile/hand size; finite exhaustive
  two-draw/three-card enumeration.
- Strict snapshot round-trip/complete provenance replay, 15-worker
  deterministic scheduling, and no double shuffle/draw choice.

## Explicit next work

- Controlled nested chance boundaries in standalone and automatic draw frames
  still fail closed. Other draw contexts without a saved continuation remain
  unsupported.
- Opening-hand and ordinary player-action `DrawCards`, semantic deck mutation
  by card effects, public JSONL protocol negotiation, and source-audited native
  parity. The automatic hand-draw integration is a separate stage envelope,
  not a complete `end_turn` controller.
- The current fixture is not a complete playable Nature episode and uses
  a supplied conditioned combat `RunState`. Its source provenance is
  determined by the caller; only the subsequent semantic draw segment has
  complete replay verification.

The default `DrawCards`, native stream/cursor algorithms, old controlled
shuffle-ticket schema, and frozen four-Act `main` stay unchanged.
**Original conditional chance-law status: UNVERIFIED.**
