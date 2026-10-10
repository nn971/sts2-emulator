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

## Seventh single-player batch

- **Volley:** X-cost attack, one independently random-targeted hit per X
  energy for 10 damage (14 upgraded). Uses the existing synthetic
  `combat_targets` stream.
- **Splash:** optionally choose among three distinct generated character
  attacks; the selected attack costs zero this turn. Upgraded Splash
  upgrades the generated attack. Solo character-pool limitations from
  Discovery still apply.
- **Anointed:** move randomly selected Rare cards from the draw pile to
  available hand slots (up to ten). These are pile transfers, so ordinary
  draw triggers do not fire. Upgrading grants Retain.
- **Gold Axe:** attack damage equals the total number of previously
  completed card plays in this combat, across turn boundaries. Upgrading
  grants Retain.

These mechanics are source-checked against the pinned game build.
Generation, shuffle and selection use synthetic RNG streams until the
native stream partition is implemented. Epoch-gated cards remain
conservatively excluded from synthetic combat generation by default.

## Eighth single-player batch (source-checked v0.111.0)

- Fisticuffs: attack for 7/9; Block scales with the executed hit's
  blocked/unblocked/overkill total. Damage-capped special encounters require
  parity fixtures; current result-based Block matches ordinary attacks.
- Bolas (3/4) and Thrumming Hatchet (11/14): independently track which
  *physical card instances* finished play last player turn, and move those
  instances from their current piles into hand before the next hand draw.
- Hidden Gem: one random eligible draw-pile card gains 2/3 extra replays;
  the native priority of playable Attack, Skill, Power candidates applies.
- Rend: 10/12 base damage, plus 5/8 per non-temporary debuff currently
  affecting the target.
- Jackpot: 25/30 damage then three random current-character 0-cost
  cards; upgrade also upgrades the generated cards. The synthetic pool is
  restricted to implemented solo Silent reward cards.
- Fasten: persistent 4/6 additive Block on cards tagged Defend.
- Prep Time: persistent 4/6 Vigor on each subsequent player-turn start,
  consumed after the next powered attack finishes.

Native RNG streams and unlock gates are still modeled conservatively. Rend
counts known prototype status debuffs and non-temporary enemy debuff powers;
additional native power taxonomies require future differential verification.

## Ninth single-player batch (pinned native v0.111.0)

- Alchemize creates one combat potion into the first free slot (cost 1,
  upgraded 0; Exhaust). Its generation is excluded from combat card pools.
- Automation is instanced: each independent application gains its amount
  in energy after every ten actual card draws, including hand draw.
- Beat Down sequentially auto-plays three (four upgraded) random playable
  Attack cards from discard, with fresh random enemy targets.
- Calamity creates an unlocked character Attack card after each Attack
  played; Calamity+ costs two instead of three.
- Catastrophe auto-plays two (three upgraded) random draw-pile cards,
  preferring playable cards and falling back to unplayable cards.
- Eternal Armor applies nine (twelve upgraded) Plating: gain unpowered
  Block at turn end; decay one stack on subsequent player turns.
- Nostalgia redirects the first Attack/Skill cards played each turn from
  discard to the top of the draw pile; Nostalgia+ costs zero.
- Omnislice attacks one enemy for eight (eleven upgraded), then deals
  unpowered splash to other enemies based on the primary damage result.
- Rolling Boulder is instanced: deal unpowered damage to every enemy at
  the beginning of the next player turn and grow by five each trigger.
- The Gambit grants 50 (75 upgraded) Block and applies a persistent
  downside: the first unblocked powered enemy attack is lethal.

Random generation uses the current synthetic combat stream; the full
native unlocked solo character card/potion candidate distributions and
stream partitions are not yet implemented. Selected damage-loss caps,
status interactions and unusual no-target autoplays still need oracle
differential coverage.

## Final solo colorless batch (pinned native v0.111.0)

**Entropy** (1 energy, rare Power; upgraded: Innate) prompts for one
hand card per stack at the beginning of each player turn, **after hand draw**.
Chosen cards transform in place into a random different eligible card in
the original card's pool. The combat-only replacement has no persistent-deck
identity, while the actual persistent deck retains its original card.
Until unlock data is connected, candidates are restricted to implemented
and solo-unlocked proxy pools; native combat-card-selection RNG parity
is not yet guaranteed.

**Stratagem** (1 energy, uncommon Power; upgraded: 0) prompts for a
draw-pile card after each shuffle, **before the next card is drawn**.
The selected card enters the hand directly (subject to ten-card capacity).
Draw effects and normal hand draw suspend and resume deterministically
through the generic combat choice continuation. Multiple stacks increase
the selection amount.

**Multiplayer-only (explicit unsupported):** Beacon of Hope, Believe in
You, Coordinate, Gang Up, Huddle Up, Intercept, Knockdown, Lift, Mimic,
Rally, Tag Team, The Ball. All twelve are registered as typed unsupported
native cards and excluded from every solo generation and merchant pool.

**Inventory coverage:** 65 native IDs = 53 playable single-player cards
+ 12 explicitly unsupported multiplayer-only cards. This is an inventory
and mechanic coverage statement, not a native RNG/fidelity certification.
