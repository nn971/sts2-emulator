# Nature #143 — persistent semantic deck across player actions

**Versioned adapter:** `sts2-v111-semantic-player-action-v1`.
**Code:** `SemanticPlayerActionV111.cs` and the retained-deck option
of `SemanticCombatDrawV111.cs` / `SemanticEndTurnV111.cs`.
**Scope:** restricted multi-turn single-player combat.

## Provider-owned hidden deck survives the player's decision

A completed `SemanticEndTurnV111` returns an ordinary, legal player
decision with the actual `CombatState.SemanticDeck` still attached.
The semantic state owns the unrevealed card-instance pools, every
fixed/revealed position, the source shuffle epoch and the **complete
previous Nature transcript**.

`SemanticPlayerActionEngineV111.Step` takes that real combat state
and one currently legal action ID, invokes the actual
`PrototypeAiEnvironment.Step` under the controlled RNG guard and
accepts the result only if the **private semantic deck fingerprint**
and the **physical draw-pile member sequence** stay exactly unchanged.
The allowlist covers draw-pile-neutral Silent actions: Strike, Defend,
Deflect, Neutralize, Slice, Poisoned Stab, Deadly Poison, Sucker Punch,
Dagger Spray, Dash, Cloak and Dagger, Blade Dance, Piercing Wail, Backstab,
Leg Sweep, Skewer, Footwork, Accuracy, Phantom Blades, Tracking, Infinite
Blades, Suppress, Anticipate, Snakebite, Assassinate, Catalyst, Expose,
Malaise, Crippling Cloud, Blur, Dodge and Roll, Sidestep, Pounce, Finisher,
Flechettes, Bubble Bubble, Strangle, Corrosive Wave, Leading Strike, Noxious
Fumes, Speedster, Serpent Form, Envenom, Afterimage, Untouchable, Flick-Flack,
Abrasive, Haze, Memento Mori, Murder, Precise Cut, Pinpoint, Grand Finale and
Die Die Die, Master Planner, Burst, Bullet Time, Accelerant, Blade of Ink,
Fan of Knives, Up My Sleeve and Predator. The playable pilot separately admits
Survivor, Hand Trick, Nightmare and Wish through saved source `select_cards`
choices. Action effects still use the real game mechanics, so card play, energy,
target, damage/block, fixed generated cards, relic hooks, hand and discard
changes occur normally.
Unmodeled chance sites abort without publishing a child, even for
an allowlisted card when other active game modifiers cause extra
RNG/deck mutations. Unsupported action kinds and draw-pile
modifications fail closed.

The action envelope forks, saves, validates and restores by executing
the actual player action again from its saved source RunState. The
semantic provider has no new Nature decision for the *player action*.

## Second turn retains previous stochastic commitments

A later `SemanticEndTurnEngineV111.BeginFromPlayerAction` accepts the
retained `SemanticDeck` if it is internally valid, has no pending
Nature decision, and exactly matches the physical draw-pile multiset.
The true end-turn action and existing source-shaped enemy intent
continuation execute once. The next automatic hand-draw stage now
**reuses that very semantic deck**:

- Already-known top cards are physically drawn without any new Nature
  identity event.
- Unresolved positions use the same previous private instance pools,
  with without-replacement constraints across both turns.
- When the source draw pile actually becomes empty, discard recycling
  begins the next unresolved shuffle epoch.
- New card Nature decisions append to the provider's cumulative
  transcript. The **per-draw-segment transcript** includes only choices
  after the segment's original stored source transcript length; this
  ensures snapshot restoration consumes no previous-turn choices again.
- All stages replay the original player action, source enemy intents
  and the real automatic pipeline before verifying the complete
  state and next legal player boundary.

The earlier `SemanticDeck=null` first-turn entry still requires a
truly **empty** initial draw pile and nonempty discard. It never
rerandomizes a precommitted, nonempty native/legacy shuffle.

## Test gates and limitations

Existing regression tests cover replaying a basic Silent action between two
genuine controlled Nature end turns, provider fingerprint invariance,
prior chance history preservation, accumulated vs segment-local
tickets, a fixed-top card persisting across an unrelated action and
being consumed without reroll, tampered hidden pools and physical
zones, pending/completed snapshots, and deterministic 15-worker runs.

This is a restricted **multiple-decision / multiple-turn** implementation;
the next phase is integrating remaining deck mutations and general
card-effect draws into the semantic provider, then expanding the JSONL
capability. Only supported ordinary actions and Bottled Potential's
hand-return continuation are accepted by the guarded semantic pilot.
Arbitrary other cards/potions, scry, known-position effects, other
shuffled-hand returns, nested `CardDrawn` chances, post-shuffle generated
cards outside the supported card-effect frame, Stratagem outside that frame,
weighted intent source-law verification and native stream parity are separate
acceptance gates. Cross-character and multiplayer mechanics remain postponed.
Source probability status remains **UNVERIFIED**.

The legacy swap-ticket protocol and frozen four-Act Silent `main`
branch are unchanged.
