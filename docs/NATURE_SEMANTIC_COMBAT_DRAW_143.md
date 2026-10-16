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

## Supported first slice

- Consecutive ordinary draws and deterministic `CardDrawn` hooks in a
  given combat snapshot.
- Source-timed discard recycle with Perfect Fit ordering, after-shuffle relic
  insertions, Stratagem selection, and drawn-card cost decisions in the
  standalone segment; the automatic hand-draw integration supports
  after-shuffle insertions, Stratagem selection, and drawn-card cost stages.
- Exact card instance identities and no replacement within the pile.
- Draw count up to available pile/hand size; finite exhaustive
  two-draw/three-card enumeration.
- Strict snapshot round-trip/complete provenance replay, 15-worker
  deterministic scheduling, and no double shuffle/draw choice.

## Explicit next work

- Nested `CardDrawn` choices in the standalone segment still fail closed.
- Nested `CardDrawn` choices in the automatic hand-draw pipeline and other
  draw contexts without a saved continuation remain unsupported.
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
