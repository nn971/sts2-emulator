# Colorless combat generation: native reference and current support

Pinned game source: STS2 v0.111.0, `Flaaax/STS2-source` at
`706ef1c9fa2219220065849fd5265328607ece20`.

## Native contracts

- **Discovery:** source pool is the current character's unlocked card pool,
  restricted by the run's multiplayer setting; select **three distinct**
  combat-generatable models with `CombatCardGeneration`; the player
  may decline the choice. The selected card is created in hand and is
  free this turn. Unupgraded Discovery exhausts; upgraded does not.
- **Jack of All Trades:** generate 1 or 2 (upgraded) **distinct** colorless
  cards directly into hand. It excludes Jack itself, applies native
  `CardFactory.FilterForCombat` plus solo restrictions, and uses
  `CombatCardGeneration`.
- **Seeker Strike:** after its damage, randomly shortlist up to three
  draw-pile cards using `CombatCardSelection`; choose one to hand.
- **Colorless epochs:** `Colorless1Epoch` through `Colorless5Epoch`
  each gate three native card IDs; these 15 IDs are tracked explicitly.

## Supported implementation and deliberate differences

- All generated copies are temporary combat cards, with distinct IDs,
  owner/player affiliation, upgrade level 0, and existing source-bound
  affliction handling. The total hand size is limited to 10.
- Discovery's choices use the current fully implemented Silent reward
  card list (not a complete native character unlock registry).
- Jack's candidate list is the supported single-player colorless subset
  with `CanBeGeneratedInCombat`, non-basic rarity, **and no epoch gates**.
  This deliberately removes unknown, unavailable or not-yet-implemented
  candidates rather than silently manufacturing unsupported cards.
  It does **not** reflect all cards of a fully unlocked account.
- The synthetic adapter currently provides six SplitMix64 streams.
  Native `CombatCardGeneration` and `CombatCardSelection` instead
  share the emulator's synthetic `combat` stream, as does existing
  generated-card logic. This preserves deterministic reset/fork
  reproducibility but **does not assert exact native RNG parity**.
- Merchant colorless selection also needs epoch-aware filtering at
  run level; catalog membership and implemented mechanics alone
  do not imply a player has unlocked a card.

## Integration work remaining

Introduce native unlock-state/epoch metadata in the run state (or in
the run initialization capabilities), character card-pool membership,
and separate combat-generation/selection RNG streams under an explicit
schema/version migration. Then compare actual native candidate IDs
and stream consumption using the passive oracle before asserting
seed-for-seed fidelity.
