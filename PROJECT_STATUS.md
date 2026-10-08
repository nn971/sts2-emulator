# Project status

**Stage:** Single-player mechanics expansion, with Overgrowth as the initial high-coverage act; native-oracle fidelity is a separate follow-up.

**Project scope constraint:** this emulator targets **single-player STS2 only**. Multiplayer-only
cards and mechanics may remain in the pinned catalog as metadata, but they are not implementation,
fidelity, testing, or AI-training targets unless this scope is explicitly changed later.

Implemented on the current prototype branch:

- emulator charter and subrepo boundary;
- deterministic engine interfaces and canonical hashing;
- typed whole-run world state;
- restrictive Silent ruleset/content catalog;
- deterministic prototype RNG with named streams;
- persistent floor-constrained act DAGs with stable future routing;
- normal, elite and boss combats;
- card play, draw/discard/shuffle, block, simple statuses and enemy turns;
- potions and relics;
- combat rewards and persistent deck growth;
- rarity-aware shops, weighted events, and heal/upgrade/max-HP rest sites;
- three-act transition flow;
- terminal victory/death;
- suspendable combat card-selection prompts with resumable effect continuations;
- combat-local/generated card identities and exhaust-on-use;
- fixed/X costs, repeated effects, and all-enemy targeting;
- player powers with ruleset-ordered turn-stage triggers;
- enemy moves expressed as ordered effect lists;
- generic combat events with contextual targets/amounts and bounded trigger recursion;
- one ordered subscriber system for player powers, enemy powers, and stateful relics;
- combat-local potion mirrors with atomic consumption;
- act/floor/history-sensitive weighted encounters;
- prototype rarity-aware card rewards;
- weighted event eligibility/history;
- rarity-aware shops with one-use card removal;
- expanded Silent card/potion/relic pools and act-specific normal/elite/boss encounters;
- player-facing `prototype-ai-v0` observation/action adapter with stable action IDs and sibling expansion;
- machine-readable capability manifest;
- CLI whole-run smoke and deterministic multi-run sweep drivers;
- deterministic whole-run smoke tests;
- trace/diff and benchmark scaffolding.

Prototype semantics deliberately use `proto.*` content IDs and carry no native-fidelity claim.
See `docs/PROTOTYPE_SCOPE.md`.

Still to do:

- collect and analyze a compact corpus of ordinary Silent A0 Act 1 Overgrowth runs with the passive
  reference bridge;
- fix only clear, decision-relevant Overgrowth fidelity divergences found by that corpus;
- integrate the prototype AI adapter from the parent `sts2-ai` project immediately after that
  oracle pass;
- replace prototype content/generation from the pinned structured reference corpus;
- translate exact Silent mechanics from structured data plus decompiled source;
- native RNG/timing parity;
- legal-action and nested-choice parity;
- focused reference-bridge differential testing for unresolved runtime questions;
- performance work driven by representative AI/search workloads;
- production cross-language binding and compatibility negotiation.

Search, strategic databases, and AI learning remain outside this repository and belong in the
future parent project.


## Native fidelity target

The first pinned native oracle is STS2 `v0.111.0` / commit `41cef1ea`, fingerprint
`3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e`.

Implemented native-fidelity groundwork now includes build fingerprinting, metadata inspection,
the native-neutral reference-trace v0.2 contract, a single-DLL passive reference recorder staged
outside the clean oracle, emulator-side structured probe auditing for history/state/RNG shapes, and
a static-first reference-data strategy.

The exact Spire Codex beta archive for `v0.111.0` has been verified to contain structured cards,
characters, relics, potions, powers, monsters, encounters, events, acts, ascensions, afflictions,
enchantments, keywords, intents, orbs, modifiers, rest-site options and progression metadata.
The corpus sync is pinned to Spire Codex commit
`b59c6f96043051afee8683237d04f943309ced35` and verifies per-file Git blob hashes.

`ReferenceCorpus`, `ReferenceMechanicsGapAnalyzer`, and `ReferenceSourceSearch` now provide local
querying, Silent mechanic-gap analysis, parser-artifact warnings, and line-oriented search over the
one-time ILSpy source tree. The external corpus and decompiled source are deliberately gitignored
and must not be redistributed. See `docs/REFERENCE_DATA_STRATEGY.md` and
`docs/reference-builds/v0.111.0-mechanics-gap-001.md`.

The first safe static corrections have landed: the starter relic is Ring of the Snake; Blade Dance
Exhausts; Skewer is 8×X (+3); Slice is 6 (+3); and Sucker Punch is 8 (+2) with Weak 1 (+1).

Static-first Silent mechanics work has now progressed through twenty-five gap passes. The engine includes
source-backed random-target attacks, Sly, universal card keywords, card/local cost modifiers,
turn-scoped and delayed powers, draw events, native Power-card removal from combat, whole-card
play-series replay for Burst (including repeated nested choices), Tools of the Trade's
hand-draw/post-turn-start discard policy, and Nightmare's instanced delayed selected-card payload.

Combat-card state now includes typed Retain/Sly/Ethereal overrides plus a card-local Replay count.
Hand Trick and Expertise exercise selected-card and just-drawn-card mutation. Master Planner adds
persistent Sly by mutating the exact Skill instance carried by a CardPlayed event. Intrinsic Replay
shares the same play-series continuation as Burst, including repeated player choices, and card
snapshots such as Nightmare preserve Replay.

The value-query layer now also supports target-status-conditioned Attack modifiers and aggregate
enemy-status values. Source-backed Tracking applies +50% Attack damage per stack against Weak
targets, while Mirage gains Block from total Poison on living enemies. Mirage also adds the first
upgrade-sensitive removal of Exhaust.

Top-level automatic combat-event dispatch is now suspendable. Ordered power/relic subscribers
can pause for a player card choice, preserve the current subscriber cleanup and remaining subscriber
order, and then resume the remaining automatic turn pipeline. Tools of the Trade now uses its native
PlayerTurnStarted choice hook instead of the former stage-specific discard shortcut, including
stack-scaled discard counts.

Card-play completion events can now suspend as well. A suspended CardPlayed subscriber preserves
the remaining event subscribers, remaining completion events, source-card disposition/cleanup, and
the surrounding Replay/Burst play-series frame. Final-execution result-pile movement still occurs
before CardPlayed subscribers, while WhenPlayed cleanup waits until the suspended event finishes.
Power cards are also represented correctly while they occupy no pile and await final removal.

The remaining continuation boundary is events emitted from inside effect/zone loops such as
CardDrawn, EnemyDamaged/EnemyDefeated, CardDiscarded, and CardExhausted. Those need their enclosing
draw/damage/discard/exhaust loop positions represented before arbitrary subscribers on those events
can suspend. Continue that only when source-backed content requires it.

Phantom Blades is now source-backed as well. The power grants Retain to Shiv-tagged cards and uses a
generic per-turn tagged-card play counter so only the first Shiv each turn receives its 9 (12)
additional damage. Tagged counters reset with the normal turn counter stage and naturally distinguish
Replay executions because CardPlayed is recorded after each full execution.

Typed persistent card Enchantment state has now started with Spiral and Glam. Persistent deck
cards carry enchantment identity into combat; combat cards track whether a single-use enchantment
has fired in the current fight; and Nightmare snapshots preserve both the enchantment and that
combat-local consumption state. Spiral adds Replay on every play, while Glam adds Replay only to
the first play each combat, and both compose additively with intrinsic Replay and Burst.

Typed combat-card Affliction state is now present for the seven native Affliction identities.
Nightmare expresses its v0.111.0 behavior as a generic selected-card snapshot transform: the
selected original retains its Affliction, while the stored payload and generated copies have it
cleared. Hexed is the first Affliction with active semantics and contributes Ethereal through the
ordinary effective-keyword query.

The first real Affliction source lifecycle is now implemented through Spectral Knight's Hex.
Player powers can be source-bound to an enemy and apply an Affliction across all eligible combat
cards. Hex is non-stacking, applies Hexed to every currently un-afflicted card in every pile, marks
generated cards while its source remains alive, and clears only its own source-tagged Hexed state
when that source dies. Nightmare still clears Affliction from its stored payload, but copies created
while Hex remains active are correctly re-Hexed on arrival.

Enemy move definitions support fixed prefixes, repeating suffixes, and constrained random pools.
A live pinned Knight capture disproved the earlier Spectral alternation assumption. Spectral Knight
now uses the source-backed policy: Hex, forced Soul Slash, then uniform random Soul Slash/Soul Flame
with Slash capped at two consecutive uses and Flame unable to repeat. Exact native RNG parity is
still separate from move-policy fidelity.

Player-power application now also has generic debuff blocking. Powers may declare themselves as
debuffs, while Artifact-like powers consume one stack to block the next incoming debuff before any
source ownership or card mutation is created. Spectral Knight's opening Hex is now blocked this way,
and ordinary positive player powers leave Artifact untouched.

Dampen is now the second source-bound whole-card-state lifecycle. When it lands, currently upgraded
combat cards are downgraded to level 0 while their suppressed Upgrade levels are stored. Cards
generated afterward are not retroactively downgraded. Multiple Dampen casters keep suppression
active until the last caster dies; final cleanup adds the stored levels back on top of any upgrades
gained during suppression. Nightmare materializes suppressed Upgrade into its stored payload so its
next-turn copies are upgraded and unsuppressed, matching the native Magi Knight interaction.

Magi Knight is now source-backed at A0 with its fixed sequence: Power Shield, Dampen, then the
Ram/Prep/Magic Bomb loop. Magic Bomb is represented as its native 35-damage attack rather than a
speculative delayed debuff.

Enemy move policy now also supports a fixed opener followed by uniformly random legal choices with
per-move consecutive-use limits. Flail Knight uses this generic policy: opener Ram, then uniform
War Chant/Flail/Ram choices after excluding illegal repeats. War Chant applies a generic enemy
Strength power, and enemy attack damage reads flat bonuses from enemy powers.

With Magi, Spectral, and Flail present, `proto.encounter.knight_gang` is now a reachable Act 3
elite. A combined five-turn regression exercises Hex, Dampen, Flail RNG/Strength, Magi block, and
the first Magic Bomb together. This is the prototype's first complete source-backed native
encounter.

Ascension is now typed run state and flows through combat initialization, enemy effect resolution,
CLI runs/sweeps, AI environment reset/fork, and JSONL reset. Enemy HP/effects use reusable cumulative
ascension deltas rather than encounter-specific branches. Knight Gang now carries its native A8 HP
and Magi Block increases plus all A9 attack increases, with an A9 combined five-turn regression.
See `docs/reference-builds/v0.111.0-mechanics-gap-021.md`.

## Active Act 1 combat-completeness milestone

The active fidelity axis is now **Overgrowth**, one of the two native Act 1 region variants.
Underdocks remains inventoried but is deliberately deferred until Overgrowth is complete. The
whole-run engine remains the integration harness.

`docs/ACT1_COMBAT_COMPLETENESS.md` retains both regions for bookkeeping while treating the 22
Overgrowth combat encounters as the current completion target. Enemy-model fidelity,
formation/slot semantics, encounter-pool fidelity, Ascension scaling, and oracle coverage remain
separate claims.

Act 1 work has also added native enemy HP ranges. Combat setup now rolls within a source-backed
inclusive HP range when a monster has one, while fixed-HP definitions remain unchanged. This models
the native value domain without claiming that the prototype combat RNG already matches the native
HP RNG stream/call order.

The source-backed Act 1 monster-model set currently includes:

- Seapunk;
- Twig Slime (S);
- Assassin Raider;
- Snapping Jaxfruit;
- Mawler;
- Toadpole;
- Leaf Slime (S);
- Leaf Slime (M);
- Twig Slime (M);
- Nibbit;
- Fuzzy Wurm Crawler;
- Shrinker Beetle;
- Axe Raider;
- Brute Raider;
- Crossbow Raider;
- Tracker Raider;
- Cubex Construct;
- Flyconid;
- Inklet;
- Vine Shambler;
- Slithering Strangler;
- Fogmog;
- Eye With Teeth.

These definitions are deliberately not used to claim their full native encounters yet. Encounter
multiplicity, slot layout, weak/normal pool membership and selection weights are promoted only when
source/oracle evidence supports them.

The enemy-AI kernel now also has a source-shaped state graph for Act 1 monsters whose native logic
cannot be flattened into one random pool. Move states and weighted random states carry explicit
transitions; random branches support typed repeat policies and runtime move-use accounting.
`CannotRepeat`, `UseOnlyOnce`, and `CanRepeatXTimes` are now all production-backed. The pinned
source interpretation is that `CanRepeatXTimes(n)` allows at most `n` consecutive executions of
that move. Twig Slime (M) is the first Act 1 production user: Sticky Shot opener, then random Pokey
Pounce / Sticky Shot with Pounce capped at two consecutive uses and Sticky Shot unable to repeat.
See `docs/reference-builds/v0.111.0-mechanics-gap-022.md`,
`docs/reference-builds/v0.111.0-mechanics-gap-023.md`, and
`docs/reference-builds/v0.111.0-mechanics-gap-028.md`.

Player Vulnerable, Weak, and Frail are now reusable timed powers. Their owner-turn countdown is
integrated into both normal and suspended event dispatch, and the shared value pipeline applies the
native 3/2 incoming-Attack, 3/4 outgoing-Attack, and 3/4 card-Block multipliers. Mawler is the first
production Act 1 user of the state graph: fixed Claw opener, then constrained random Rip and Tear /
Roar / Claw with Roar usable once. See
`docs/reference-builds/v0.111.0-mechanics-gap-024.md`.

Formation-sensitive enemy AI is now represented explicitly as well. Enemy combat state carries a
formation position; conditional AI states can test alone/front/not-front over living enemies.
Toadpole is the first production user, with source-backed front/back openers and its fixed follow-up
cycle. See `docs/reference-builds/v0.111.0-mechanics-gap-025.md`.

Enemy moves can now create combat-local status cards directly in the discard pile. The shared
insertion path preserves combat-card identity and active source-bound Afflictions such as Hex.
`Slimed` is defined with its pinned 1-energy Draw-1/Exhaust semantics. Leaf Slime (S) uses its
source-shaped random Tackle/Goop policy with no immediate repeats, while Leaf Slime (M) uses its
Sticky Shot / Clump Shot alternation. See
`docs/reference-builds/v0.111.0-mechanics-gap-027.md`.

Twig Slime (M) now closes the four-model slime family. The enemy-AI repeat kernel's
`CanRepeatXTimes(n)` rule is source-pinned as "at most n consecutive uses"; Twig M opens with one
Slimed, then randomly chooses Pokey Pounce or Sticky Shot with Pounce capped at two consecutive
uses and Sticky Shot unable to repeat. See
`docs/reference-builds/v0.111.0-mechanics-gap-028.md`.

Nibbit now uses the formation-aware conditional AI directly: alone -> Butt, front -> Slice, rear -> Hiss, then the fixed Butt/Slice/Hiss cycle. Its A8 HP/Block and A9 damage/Strength deltas are typed, and weight-0 reference definitions cover both the solo weak and paired normal formations. See
`docs/reference-builds/v0.111.0-mechanics-gap-030.md`.

Fuzzy Wurm Crawler is now source-backed with the native Acid Goop / Inhale / Acid Goop three-state cycle. Its asymmetric A8 HP range (58–59), A9 Acid Goop increase, persistent +7 Strength from Inhale, and singleton weak formation are all covered by focused regressions. See
`docs/reference-builds/v0.111.0-mechanics-gap-031.md`.

Shrinker Beetle now adds the source-bound Shrink lifecycle: a non-stacking player debuff that multiplies Attack damage by 7/10 and is removed when its source Beetle dies. The Beetle's Shrinker opener and alternating Chomp/Stomp loop, A8 HP, A9 damage, singleton weak formation, and Fuzzy+Beetle normal formation are covered by focused tests. See
`docs/reference-builds/v0.111.0-mechanics-gap-032.md`.

Overgrowth encounter selection now has typed, forkable Act-1 region state. New whole runs default to Overgrowth; the first three ordinary combats consume three distinct encounters from the native four-entry weak pool while elites and other room types leave that queue untouched. The selected region is exposed to the AI observation, while exact native encounter RNG parity remains separate. See
`docs/reference-builds/v0.111.0-mechanics-gap-033.md`.

A branch reconciliation found additional Overgrowth work already present: Axe, Brute, Crossbow, and Tracker Ruby Raiders plus Cubex Construct all have source-backed definitions and focused tests. Together with Assassin Raider this makes the Ruby Raider monster family model-complete. Mawler Normal and Cubex Construct Normal now have weight-0 source-backed singleton formations, and Ruby Raiders is represented as three distinct choices from its five-raider pool. The twelve-entry Overgrowth normal encounter inventory is now explicit, but activation still waits for the remaining normal encounters. See
`docs/reference-builds/v0.111.0-mechanics-gap-034.md`.

Flyconid is now source-backed with its weighted initial choice (Frail Spores 2/3, Smash 1/3), weighted no-repeat main loop (Vulnerable Spores 3/6, Frail Spores 2/6, Smash 1/6), A8 HP and A9 damage scaling. This also promotes both Shroom and Slime and Overgrowth Flora to source-backed normal formations, bringing Overgrowth normal model+formation coverage to eight of twelve encounters. See
`docs/reference-builds/v0.111.0-mechanics-gap-035.md`.

Inklet is now source-backed with the reusable Slippery HP-loss modifier: the next positive enemy HP loss is capped to 1 and consumes one stack, while fully blocked hits do not consume it. Three-Inklet formation slots drive the native outer-Jab / middle-Whirlwind opener, followed by Jab alternating with an equal Piercing Gaze / Whirlwind choice. This promotes Inklets Normal and leaves three unresolved Overgrowth normal encounters. See
`docs/reference-builds/v0.111.0-mechanics-gap-036.md`.

Vine Shambler and Slithering Strangler are now source-backed. Tangled reuses typed Entangled card afflictions to tax Attacks by +1 energy for exactly the next player turn, including generated Attacks, and countdown removal now clears afflictions generically. Constrict is a cumulative source-bound debuff that deals its stack count at PlayerTurnEnded and disappears when the Strangler dies. Encounter construction now also supports alternate formation variants, representing Strangler + Jaxfruit, Strangler + a random medium slime, or Strangler + both small slimes. This left Fogmog as the sole unresolved Overgrowth normal encounter. See
`docs/reference-builds/v0.111.0-mechanics-gap-037.md`.

Fogmog now closes the normal-monster layer. Enemy actions can summon combat-local enemies with starting powers and leader/minion ownership; Eye With Teeth uses this generic path, skips its summon turn, revives at its next enemy-action opportunity after defeat while Fogmog lives, and is removed with its leader. Fogmog's source-shaped summon/Thwack/Headbutt graph and Eye's 3-Dazed Distract are covered by focused tests. See
`docs/reference-builds/v0.111.0-mechanics-gap-038.md`.

The Overgrowth ordinary-combat selector now uses the full native-shape two-stage bags: three distinct weak fights first, then the twelve normal encounters without replacement. Known cross-pool and normal-pool adjacency exclusions are enforced before each draw, both bags are fork-safe run state, and invariants validate membership/count consistency. This completes the Overgrowth ordinary-combat layer at semantic fidelity. See
`docs/reference-builds/v0.111.0-mechanics-gap-039.md`.

Encounter construction now supports independent random slot groups with optional distinct selection.
The native-shape `SLIMES_WEAK` and `SLIMES_NORMAL` formations are represented as weight-0
reference encounters: weak uses two different small slimes around one random medium slime; normal
uses fixed Twig M + Leaf M followed by the two small slimes in random order. This promotes formation
fidelity while leaving native weak/normal pool selection explicitly pending. See
`docs/reference-builds/v0.111.0-mechanics-gap-029.md`.

## First live native capture

The v0 passive recorder has now been validated in-game on two Ascension-0 Silent combats against
the pinned v0.111.0 / 41cef1ea oracle. The 54-record JSONL contained two coherent
setup/start/turn/win/end lifecycles and no capture failures.

The capture exposed three concrete next needs now implemented in probe v1:

- Player -> Creature discovery for player HP/block/power state;
- field-level projection of native SerializableRng state;
- passive CombatHistory.Changed capture for card-play/damage/energy/potion/monster-move granularity.

See `docs/reference-builds/v0.111.0-capture-001.md`.

A second live capture validated full Player -> Creature coverage, full SerializableRng state,
21 matched card-play start/finish lifecycles, one potion use, and six end-turn actions. It also
showed that card-finish and potion-used history entries are lifecycle markers rather than universal
decision boundaries, and that Survivor introduces a nested player-selection decision inside a card
play.

`ReferenceProbeActionExtractor` now emits evidence-backed observed action envelopes without
promoting gaps to parity claims. Probe v2 adds event catalogs plus richer card/potion/player state to
discover the native choice surface. Enemy snapshots now also catalog creature/model layouts and opportunistically project
current/next move, intent, move index and move history when the pinned runtime exposes those
properties. Probe v3 additionally projects card Affliction/modifier/Upgrade state for Hex/Dampen
work.

`reference-knight-gang-audit` extracts matching native combats into normalized checkpoints
covering enemy state, card-pile mutation, typed CombatHistory evidence, and monster-AI RNG
fingerprints. It compares only explicitly observed native fields and reports missing move/intent/RNG
surfaces as instrumentation gaps.

A subsequent user-owned Loadout-assisted A10 capture supplied the three Knights inside a synthetic
combat wrapper. It is therefore evidence for monster mechanics rather than native Knight Gang
encounter construction. The capture confirmed the important A9+ numeric values and exposed the
Spectral Knight move-policy bug corrected in pass 022. Exact encounter-wrapper initialization and
native monster-AI RNG parity remain unresolved; neither blocks the new Act 1 enemy-model milestone.
See `docs/reference-builds/v0.111.0-knight-gang-differential.md`.


## Silent card catalog milestone

The pinned v0.111.0 native `SilentCardPool` is represented explicitly as **91 unique cards**.
Eighty-three currently map to implemented prototype mechanics; the remaining 8 have source-backed
cost/type/rarity/keyword metadata and are gated with `MechanicsImplemented = false` so they cannot
silently behave as no-op cards. The five multiplayer-only Silent cards are typed separately, excluded from single-player rewards,
and intentionally out of scope for mechanics implementation.

Five older compatibility definitions remain available to focused tests but are excluded from the
pinned native pool and therefore from rewards: Quick Slash, Concentrate, Catalyst, Crippling Cloud,
and Die Die Die. Shiv remains a generated token outside the 91-card pool.

See `docs/SILENT_CARD_CATALOG.md`.

## Overgrowth fidelity-audit baseline

Overgrowth is now the active Act 1 target; Underdocks is deferred.

All 22 Overgrowth combat encounters have source-backed mechanics/composition plus typed weak, normal,
elite, and boss selection state at the current semantic-fidelity target. The next phase is no longer
broad content implementation but seeded/native differential auditing.

The AI observation now exposes the preselected Act 1 boss encounter alongside the Act 1 region.
The developer CLI also provides `prototype-overgrowth-audit [n] [ascension]`, which drives the
deterministic smoke policy over many seeds and reports:

- coverage across the 22 Overgrowth combat encounters;
- boss-selection frequencies;
- how many runs leave Act 1;
- weak-pool no-replacement violations;
- missing encounters under the sampled policy.

This is a structural audit and does not claim native RNG parity. See
`docs/reference-builds/v0.111.0-mechanics-gap-043.md`.


### Silent backlog pass: Echoing Slash / Fan of Knives / Well-Laid Plans

Source-backed mechanics now include Echoing Slash's kill-propagating all-enemy attack,
Fan of Knives' persistent Shiv all-enemy targeting plus 4 (5) generated Shivs, and
Well-Laid Plans' end-turn whole-hand preservation with Ethereal still resolving first.
These mechanics use generic power capabilities rather than card-ID branches.


### Silent backlog pass: Blade of Ink

Blade of Ink is now source-backed: it creates 2 (3 upgraded) Shivs carrying the Inky enchantment.
Generated-card effects can attach a typed enchantment payload, and the generic card-play series
runs Inky after the card's own effect body on every play/replay, matching the native CardModel
ordering. Inky applies 1 Weak to the actual target set, including all living enemies when Fan of
Knives promotes Shiv to all-enemy targeting.

The active single-player Silent card backlog is now Knife Trap and The Hunt. Multiplayer-only
cards remain intentionally out of scope.


### Silent backlog pass: Knife Trap

Knife Trap is now source-backed as a generic tagged-card autoplay operation. It snapshots matching
cards from Exhaust, removes each from that zone, optionally upgrades it when Knife Trap is upgraded,
and resolves it through the ordinary card-play series before returning it to its normal result pile.
This preserves normal attack modifiers, Replay/enchantment execution, CardPlayed events, and
Fan-of-Knives target promotion rather than substituting direct damage.

The tagged-autoplay primitive deliberately fails fast if a nested autoplay opens a player-choice
suspension, because preserving the outer effect queue across that nested continuation is a separate
continuation-boundary feature. Native Knife Trap only selects Shiv-tagged cards, so current
single-player content does not require that extension.

The active single-player Silent card backlog is now only The Hunt.


### Silent backlog pass: The Hunt / single-player catalog completion

The Hunt is now source-backed at 10 (15) damage with Exhaust and cannot be generated in combat.
A successful Fatal records a separate extra 3-card reward and adds the counter-style Hunt power.
Fatal eligibility is evaluated before the hit against the target's powers; Minion is now explicitly
marked as preventing Fatal, matching the native power contract.

Reward state now represents extra card-reward groups separately and resolves each group as its own
choose-or-skip decision rather than merging extra options into the ordinary combat card reward.
Exact native reward RNG ordering remains an oracle-fidelity question, but the strategic decision
structure is represented.

All pinned v0.111.0 **single-player** Silent cards now have mechanics implemented. The five
multiplayer-only Silent cards remain catalog metadata only and are intentionally out of scope.


## Mechanics-expansion pass: combat lifecycle relics and potions

Fidelity is deliberately not the blocker for this phase. The engine now has explicit CombatStarted
and CombatWon events, a generic HealPlayer combat effect, HP-threshold relic triggers, and generic
player Strength as an additive Attack modifier. These are reusable mechanics rather than item-ID
branches.

The content pool now includes Anchor, Vajra, Blood Vial, Meat on the Bone, Strength Potion, and
Regen Potion. Regen is represented as a decrementing player power that heals on PlayerTurnStarted.
Combat victory is centralized through a victory-event dispatch before reward creation so future
post-combat relics can share the same path.


## Mechanics-expansion pass: potion-use interactions and next-card duplication

The combat event layer now exposes PotionUsed after the consumed potion has been removed from both
persistent and combat-local potion slots. Relics can subscribe to this event like any other combat
event.

Power-based play-count modifiers now support an any-card-type replay mode. Duplicator uses that
primitive to replay the next card once and consume one stack; Flex Potion and Reptile Trinket use a
shared temporary-Strength power that contributes to Attack damage and is removed at player turn end.
Reptile Trinket grants 3 temporary Strength whenever a potion is used in combat.


## Mechanics-expansion pass: generated-card choices and card retrieval

Combat now has a temporary ChoicePool zone that participates in normal state invariants and the
existing select_cards action protocol. Generic generated-card choices can sample distinct,
implemented single-player cards by card type, make the candidates free for the current turn, allow
choose-one-or-skip, move the selected card into Hand, and delete unselected temporary candidates.

Card-selection resolution now supports MoveToHand, optional cleanup of unselected source cards, and
a selected-card temporary cost mutation. Attack Potion, Skill Potion, Power Potion, and Liquid
Memories are implemented on these primitives. This is intentionally a broad mechanics substrate;
native generation pools/RNG ordering remain deferred fidelity work.


## Mechanics-expansion pass: any-time and stateful potions

Fidelity remains deliberately secondary. Potion definitions can now carry run-level effects and
declare that they are usable outside combat. Noncombat phases expose those potion actions without
disturbing their normal room decisions, and combat use shares the same persistent-player mutation
path before synchronizing combat-local potion slots.

New reusable mechanics include max-HP gain, percent-max-HP healing, filling empty potion slots, and
upgrading all upgradable cards currently in hand. Temporary Dexterity is represented as a generic
turn-scoped power.

The expanded potion pool now includes Blessing of the Forge, Speed Potion, Vulnerable Potion,
Liquid Bronze, Ghost in a Jar, Fruit Juice, Blood Potion, and Entropic Brew. These intentionally
favor broad strategic semantics over exact native RNG/timing parity.


## Mechanics-expansion pass: run-event and card-add relics

The run layer now has generic relic triggers for relic acquisition, entering a shop, and adding a
card to the persistent deck. Run effects share the same typed heal, full-heal, max-HP, gold, and
other player-state mutation substrate used by any-time potions.

Old Coin, Mango, Lee's Waffle, Meal Ticket, Lucky Fysh, and Darkstone Periapt now exercise these
hooks. Card addition is centralized, so reward, shop, and event additions compose with the same relic
logic.

Relics can also transform a card while it is entering the deck by card type. Molten Egg, Toxic Egg,
and Frozen Egg use that generic modifier to upgrade newly added Attacks, Skills, and Powers
respectively. Reward-option presentation is still simplified; the strategically relevant persistent
card is upgraded on acquisition.


## Mechanics-expansion pass: conditional combat relics

Combat relic triggers now support reusable turn-number and zero-block conditions in addition to the
existing event, cadence, and HP-threshold filters. This keeps timing/condition logic in the trigger
model instead of item-ID branches.

The relic pool now also includes Bronze Scales, Oddly Smooth Stone, Bag of Marbles, Gremlin Horn,
Mercury Hourglass, Orichalcum, Horn Cleat, and Captain's Wheel. They exercise combat-start power and
status application, enemy-defeat draw/energy, player-turn-start area damage, zero-block end-turn
block, and exact-turn block triggers.


## Mechanics-expansion pass: typed relic counters and energy policies

Relic CardPlayed triggers can now filter by source card type and optionally reset their cadence
counter at each player turn. Combat counters also preserve the previous turn's Attack count, and
relics may opt into retaining unused energy across the normal energy refresh boundary.

Kunai and Shuriken use per-turn every-three-Attack counters; Ice Cream preserves unused energy; Art
of War grants energy on turns following a no-Attack turn. Ornamental Fan and Letter Opener now use
the same typed per-turn counter machinery, while Nunchaku counts Attacks with its longer cadence.

Choice-opening potions now keep PotionUsed inside the same effect continuation. The potion is
consumed before its effect resolves, generated-card/retrieval choices may suspend safely, and
PotionUsed relic hooks fire only after that choice completes. This removes a sweep-only crash that
appeared once generated-choice potions entered the ordinary potion pool.


## Mechanics-expansion pass: discard-reactive relics

Discard effects now feed the generic combat-event subscriber layer into reusable relic mechanics.
Relic trigger counters can cap how many times a trigger fires inside a reset window while still
counting later matching events, so first-discard/first-action style relics no longer need item-ID
branches.

Tough Bandages gains 3 Block for each actual discard, Tingsha deals 3 damage to a random living
enemy for each actual discard, and Hovering Kite gains 1 Energy on only the first actual discard
each player turn. Ordinary end-of-turn hand cleanup remains separate from CardDiscarded and does
not trigger these relics.


## Mechanics-expansion pass: run economy relics

Reward and shop generation now consult generic relic capabilities rather than item-ID branches.
Relics can add card choices to every card-reward group, add extra card-reward groups after ordinary
combats, multiply shop prices, override the undiscounted card-removal price, and prevent acquisition
of new potions. Shop offers retain their undiscounted prices so a pricing relic purchased inside a
shop can immediately reprice every remaining offer without compounding an already-discounted value.

Question Card adds one card to each reward choice; Prayer Wheel adds one extra ordinary-combat card
reward; Membership Card halves shop prices; Smiling Mask sets the pre-discount removal service to
50 gold; and Sozu reuses the existing energy-per-turn bonus while preventing reward/shop/generated
potion acquisition. Existing potions remain usable.

The AI reward observation now projects the currently active card-reward group and the number of
extra reward groups remaining, fixing the previous stale-first-group view when The Hunt or Prayer
Wheel creates sequential reward choices.


## Mechanics-expansion pass: boss relic choices and strategic downsides

Boss combat rewards now expose a genuine three-option relic decision. The reward model carries an
indexed relic-option set, legal actions select a specific option, the AI observation exposes the
same option set, and reward invariants reject duplicate or unknown relic choices. Elite rewards keep
their existing single-relic structure. Owned boss relics are excluded from future boss-relic rolls.

The first boss-relic pool is deliberately mechanics-diverse: Sozu, Coffee Dripper, Fusion Hammer,
Velvet Choker, and Runic Dome. All grant +1 Energy per turn through the existing generic energy
capability. Their downsides are represented as reusable relic capabilities: blocking potion
acquisition, blocking rest-site healing, blocking rest-site card upgrades, limiting cards played per
turn, and hiding enemy intents from the AI/player-facing observation while leaving the canonical
combat state intact.

Rest-site legal actions now honor healing and upgrade restrictions. Card-play limits compose across
powers and relics by taking the tightest positive cap. Runic Dome affects information projection
rather than enemy AI state, preserving deterministic simulation while changing the information
available to a decision policy.


## Mechanics-expansion pass: acquisition-time deck choices

Relic acquisition can now suspend a reward on a generic persistent-deck choice. The continuation is stored canonically in RewardState, exposes one action per eligible persistent card, survives forking, is validated against the owned source relic and live deck, and is projected into the AI observation. Choices are sequential rather than enumerating all card subsets, so selecting three cards from a twenty-card deck exposes roughly twenty actions at each step instead of 1,140 three-card combinations.

Empty Cage uses this capability to remove two chosen non-Eternal cards. Astrolabe uses the same capability to transform three chosen cards into deterministic random reward-pool cards and upgrades the transformed results. Transformations preserve persistent card instance IDs while replacing card identity/state, which keeps action/history references stable for search consumers. If the deck has fewer eligible cards than a relic requests, the choice automatically contracts to the eligible deck size.

The boss relic pool now contains both modifier-style relics and acquisition-choice relics. Existing boss reward tests deliberately choose a non-choice relic when testing the act-transition path, while dedicated tests cover the multi-step Empty Cage and Astrolabe continuations.


## Mechanics-expansion pass: potion inventory replacement

Potion rewards and shop purchases now remain strategically available when every potion slot is occupied. Instead of making the new potion unavailable, the engine exposes one replacement action per occupied slot. Reward replacement is free; shop replacement charges the offer price and marks the potion offer sold. The selected old potion is discarded atomically as the new potion enters that slot.

When an empty slot exists, the action surface stays compact: rewards expose the ordinary take action and shops expose the ordinary buy action, with no dominated replacement alternatives. Sozu suppresses both ordinary acquisition and replacement. The AI layer needs no special continuation state because the existing potion observation identifies slot contents and the generic semantic action payload identifies the replaced slot.

A generic discard-potion action is intentionally not exposed in every run phase yet. For the current strategic surface, direct replacement captures the meaningful full-belt decision without adding dominated discard branches to every state.


## Mechanics-expansion pass: expanded strategic shops

Generated shops now expose five typed card offers (two Attacks, two Skills, and one Power), three distinct potion offers, three distinct relic offers, and the existing removal service. Potion and relic offer IDs occupy stable ranges, and generated relic offers exclude relics already owned by the player.

ShopState keeps the original primary potion/relic fields for compatibility with older handcrafted states while exposing unified PotionOffers and RelicOffers collections over the primary and additional offers. All purchase logic, sold-state updates, Membership Card-style repricing, potion replacement, invariants, forking, and AI observation operate on the unified collections, so secondary offers are mechanically equivalent to primary offers.

The AI shop observation now exposes the complete potion and relic offer arrays in addition to the compatibility fields. Regression coverage verifies the typed 5-card layout, distinct multi-offer inventory, secondary potion purchases, buying Membership Card from a secondary relic slot and repricing every remaining offer, and exclusion of already-owned relics from generated shop stock.


## Mechanics-expansion pass: strategic event deck choices

Events can now suspend after a branch is chosen and expose a sequential persistent-deck choice. The event continuation records the chosen option, choice kind, remaining selection count, and eligible persistent card instance IDs canonically in EventState; it survives forks, is validated against the event definition and live deck, and is projected into the AI observation. Supported event deck operations are remove, upgrade, and transform, with optional upgrading of transformed results.

Event branch costs are applied exactly once before the deck-choice continuation begins. Gold loss is now a first-class run effect, and event legality suppresses branches the player cannot afford, branches whose HP cost would be lethal, and paid deck-shaping branches with no eligible card. Multi-card choices remain sequential rather than enumerating subsets, keeping the action surface approximately linear in deck size.

Two prototype strategic events exercise the capability. Forgotten Altar offers 50-gold card removal, 7-HP card upgrading, or a small gold exit. Warped Mirror offers a 9-HP two-card transform, a 35-gold transform-and-upgrade, or no effect. Regression coverage verifies single-payment semantics, AI visibility, legality filtering, persistent instance-ID preservation, sequential two-card transformation, and upgraded transform results.


## Mechanics-expansion pass: event item/relic/card tradeoffs

Event run effects now cover max-HP loss, relic acquisition, and named potion acquisition in addition to the existing HP, gold, card, and deck-shaping effects. Relics acquired through events use the normal RelicAcquired run-event path, so acquisition hooks such as Old Coin fire exactly as they do from other acquisition surfaces. If an acquired relic itself requires a persistent-deck choice, the event reuses the canonical sequential event deck-choice continuation and records the source relic in both canonical and AI-visible state.

Named potion acquisition uses an empty slot immediately when available. On a full belt, the event suspends on a canonical potion-replacement continuation containing the offered potion and eligible occupied slots; the player/AI then explicitly chooses which potion to replace. Sozu suppresses potion-acquisition event branches. Event state can carry both a potion replacement and a deck continuation, and each continuation completes without reapplying the original event costs.

Caged Vault exercises max-HP-for-Empty-Cage, HP-for-Old-Coin, gold-for-Strength-Potion, and a safe gold exit. Forbidden Archive exercises paired persistent card acquisition by offering Wraith Form together with an Infection downside card. Regression coverage verifies relic-sourced Empty Cage removals, Old Coin's acquisition trigger, full-belt potion replacement and AI exposure, empty-slot potion acquisition, Sozu legality filtering, and paired card addition with stable persistent IDs.


## Mechanics-expansion pass: stateful strategic relics

The emulator now supports four additional reusable relic mechanics. Chemical X contributes a
relic-level X-value bonus without changing the actual energy paid. Mummified Hand uses a generic
random-hand temporary-cost mutation after Power plays. Unceasing Top exercises a state-dependent
relic trigger that draws after a card play leaves the hand empty. Runic Pyramid extends the generic
end-turn hand-preservation policy from powers to relics while still resolving Ethereal first.

These are intentionally mechanics-first implementations: exact native RNG stream details and edge
timing remain deferred, but the strategic state transitions are represented through reusable fields
and effects rather than relic-ID branches.


## Mechanics-expansion pass: draw-time cost randomization

Snecko Eye now exercises two reusable relic modifiers: a persistent hand-draw bonus and random
absolute energy-cost assignment when cards are drawn. Draw-time cost mutation uses the existing
combat RNG, skips X/unplayable negative-cost cards, and persists with the card until a later draw
overwrites it. This keeps randomized costs visible in canonical combat state and therefore directly
usable by AI/search code.


## Mechanics-expansion pass: exhaust-driven relics

CardExhausted now supports a generic event-source cloning effect, allowing relics to create a
temporary copy of the exact exhausted card instance while preserving upgrades, enchantments,
afflictions, replay state, and combat-local cost/keyword mutations. Burning Sticks uses this once per
combat for exhausted Skills.

Charon's Ashes, Joss Paper, and Forgotten Soul now exercise the existing CardExhausted event with
all-enemy damage, thresholded draw, and random-target damage respectively. Exact ethereal deferral
timing is intentionally postponed; the strategic exhaust-trigger behavior is represented now.


## Mechanics-expansion pass: combat-start generated choices

CombatStarted event dispatch can now suspend on a player choice and resume its remaining subscriber
chain afterward. Generated-card choices can draw from the full implemented single-player reward pool
instead of requiring a specific card type, and the "free this turn" modifier is now an independent
option rather than hard-wired into every generated choice.

Toolbox exercises this path by offering three temporary cards at combat start and adding the chosen
card to hand at its normal cost. The current prototype places this choice after the opening hand draw;
exact pre-draw native timing remains intentionally deferred.


## Mechanics-expansion pass: utility combat potions

Three additional shared potions are modeled from the pinned v0.111 source snapshot. Cure All combines
the existing energy and draw primitives. Fortifier uses a reusable block multiplier and triples the
player's current Block without applying card/Dexterity block modifiers.

Stable Serum introduces a reusable post-hand-cleanup power timer. Its Retain Hand power preserves all
non-Ethereal cards during hand cleanup, then decrements only after that cleanup has used the power.
Applying two stacks therefore retains the hand across exactly two end-of-turn cleanups instead of
using an off-by-one PlayerTurnEnded decrement or a potion-specific branch.


## Mechanics-expansion pass: strategic map topology

Procedurally generated acts now use the `prototype-strategic-map-v1` profile instead of a fixed modulo-lane graph. Floor widths are seeded: floor 1 has 3 entries; floors 2-4 have 3-4 nodes; floor 5 has 2-3 rests; and floor 6 has one boss. Node IDs are stable act/floor/index identifiers and no longer encode room type, so changing room assignment does not perturb graph identity.

Adjacent layers are connected by a sparse seeded topology. Every nonterminal node has one or two outgoing edges, every node after floor 1 has an incoming edge, every generated node is reachable from an entry and can reach the boss, and the graph guarantees meaningful branching and pre-boss reconvergence. Additional optional second edges add seed-dependent topology variety without making each floor nearly complete.

Floor rules now support width bounds, required room types, and special-room predecessor constraints. The current profile guarantees Event+Shop opportunities on floor 2, Event+Elite on floor 3, Shop+Rest on floor 4, Rest on floor 5, and Boss on floor 6. Floors 3 and 4 avoid placing a special room directly after the same special room along an edge. Existing same-floor duplicate-special restrictions remain in force.

Generated maps carry their profile ID canonically, while hand-authored test maps remain unprofiled so small fixtures stay convenient. The state invariant layer applies strict profile-specific width, room, connectivity, branch, convergence, and reachability checks to generated maps. The AI observation continues to expose the complete graph and now also exposes the map generation profile ID. Map regression coverage checks topology persistence after route selection, room/edge constraints, at least ten distinct maps across thirty seeds, and boss reachability for every node across one hundred seeds.

The broader route distribution exposed an old CLI sweep-policy assumption: it attempted to heal whenever below half HP even when Coffee Dripper removed the heal action. The prototype sweep policy is now capability-aware and falls back to upgrade/train/other legal rest actions rather than assuming a specific rest option exists.


## Mechanics-expansion pass: persistent card-cost and hand-randomization potions

Touch of Insanity now uses the generic combat card-choice continuation with an energy-costing-card
filter. The selected card receives a zero temporary cost with no expiry flags, so it remains free for
the rest of the combat while still living in ordinary canonical card state.

Snecko Oil now draws up to seven cards and then randomizes every fixed-cost playable card currently
in hand to 0..3 until end of turn or until played. X-cost and unplayable cards are left untouched.
The whole-hand randomization is a reusable combat effect and shares the combat RNG stream with other
combat-local random effects.


## Mechanics-expansion pass: automatic death-prevention potions

Potion definitions can now be marked automatic and carry a death-prevention heal percentage. Automatic
potions are omitted from manual combat actions and direct manual-use attempts are rejected. When
combat damage would leave the player at zero HP, the engine consumes the first eligible automatic
potion from both persistent and combat-local potion state, restores HP, and emits PotionUsed so
ordinary potion-use relic hooks still observe the consumption.

Fairy in a Bottle exercises this path and restores 30% of max HP (with a minimum of 1). Immediate
card-effect and attack-retaliation damage use the death-prevention path inside operation resolution;
the automatic end-turn pipeline also applies it before declaring defeat. Exact native animation and
multi-enemy timing are intentionally outside this strategic prototype.


## Mechanics-expansion pass: sequential optional combat choices

Combat card selections now support an AI-friendly sequential optional mode. Instead of materializing
every subset of a hand, the pending choice exposes one action per remaining card plus a finish action;
selected cards remain in their source zone until the sequence is finalized. The accumulated selection
is canonical state and is covered by combat invariants and forking.

Gambler's Brew uses this mode for its choose-any-number discard. Once the player finishes, all chosen
cards are discarded together and the emulator draws the same number. This keeps the branching factor
linear in hand size (11 actions at a ten-card hand rather than 1,024 subset actions). Sly discard
autoplay still follows the prototype's existing generic discard-continuation order; exact native hook
ordering remains a later fidelity refinement.


## Mechanics-expansion pass: Gambling Chip reuse

Gambling Chip now reuses the same sequential optional discard-and-refill continuation as Gambler's
Brew, but starts it from the combat-start relic event after the opening hand has been drawn. This is
a useful cross-check that the linearized choice state survives event-subscriber suspension and resumes
the combat-start dispatch correctly; no relic-specific choice branch is required.


## Mechanics-expansion pass: completed route history and conditional run effects

Canonical `RunWorldState.CompletedRoomHistory` records (act, floor, stable map node ID, room type) only when a room is fully resolved: the final event/rest/shop action, a non-boss reward's leave action, or a boss reward's leave action. Entering a room or fighting an unfinished combat does not count. Boss completion is recorded before transitioning acts and clears `ActiveRoom`, while history survives act transitions, deterministic forks, canonical state hashes, and AI observation projection. Hand-authored map-free fixture states remain supported; the generated-map profile requires an active matching node at completion.

`PrototypeRouteConditionSpec` is a reusable declarative predicate on the completed route. It supports room-type matching, a minimum visit count, consecutive ending streaks, every-Nth matching visit, current-act-only scoping, and act/floor gates. Event choices can carry a route condition; both legal-action enumeration and direct action validation enforce it. Relic run triggers can carry the same condition and the new `RoomCompleted` run event is dispatched once after appending the completed room to history, making an every-second-combat trigger see the newly completed fight. Context-free run events continue to use the existing generic relic trigger machinery.

The prototype Trail Ledger relic grants 20 gold on every second completed ordinary Combat room within each act. The Path Broker event offers a 75-gold elite dividend unlocked by a completed Elite in the current act; a +5 Max HP combat-march reward unlocked by two consecutive completed Combats; and an unconditional +15 gold exit. These examples are mechanics breadth probes rather than claims of native game fidelity.

Route-history invariants reject out-of-order/duplicate act-floor completions, future acts, malformed locations, and—in generated maps—visits not matching the selected map or skipped floors, plus already-completed active rooms. Regression coverage checks no counting on room entry, recording only after reward completion, fork isolation and AI history visibility, boss completion/act transition, current-act relic counter reset, conditional event legality, and chronology rejection.


## Mechanics-expansion pass: staged draw-pile autoplay

Combat state now has an explicit PlayPile zone. Distilled Chaos first removes up to three cards from
the top of DrawPile (shuffling Discard into Draw when necessary) and stages all selected cards in
PlayPile before any resolve. Each staged card is then an ordinary queued autoplay: enemy-targeted
cards choose through the combat-target RNG, X-cost cards capture current Energy without spending it,
and card play/replay/exhaust hooks continue through the normal card-play series.

The queued representation also preserves the outer autoplay sequence across nested card choices.
For example, if Distilled Chaos reaches Survivor, the discard choice can suspend with later staged
cards still represented in PlayPile; resolving the choice resumes the remaining autoplay operations
and finally the potion's PotionUsed event. The AI combat observation now exposes PlayPile explicitly.
Tagged zone autoplay was routed through the same staged-card primitive, removing its former nested-
choice limitation.


## Mechanics-expansion pass: composable event reward continuations

Event reward effects can now produce **multiple suspended decisions** without rejecting
their combination. Canonical `EventState` stores the currently active prompt alongside
queued potion IDs and requested deck-choice continuations. Resolution consumes all pending
potion replacements in acquisition order, followed by relic-acquisition deck choices in
effect order, followed by any explicit event deck choice. Immediate effects, HP/gold
costs, relic acquisition hooks, and preexisting available-slot potion pickups are
applied only once when the event option is chosen.

A queued deck request records the *requested* number of selections and the source
relic/option, but calculates its eligible persistent card instance IDs only when the
prompt activates. This means earlier removal, transform, upgrade, or card-add effects
cannot leave stale candidate IDs or prematurely suppress a later deck decision. If the
deck no longer contains eligible cards, that request is skipped automatically.
At every interactive step, the event offers exactly one prompt type with a linear
number of legal actions.

The chosen-event invariant checks queued reward provenance, source ownership and
specification agreement, prompt exclusivity, and valid potion candidates. Forking
deep-copies all queued continuations, and the player-facing event observation includes
queued potion IDs and compact deck-choice descriptions without exposing stale
candidate sets.

Collector's Annex is a deliberately synthetic Act 2+ mechanics test event:
one option purchases two deck-shaping relics, two potions and an explicit upgrade
in a single payment, and another grants a deck-choice relic *before* a new card is
added. Regression tests cover full-belt double replacement, empty-slot pickup
followed by replacement, the source order of Empty Cage/Astrolabe/upgrade,
post-removal candidate refresh, exhausted requests, state fork isolation,
and legal-action suppression for unaffordable or duplicate-relic options.

This is a generic event state-machine improvement, not a native-v0.111.0 event
fidelity claim. Neither the emulator's AI training parent nor its code is changed
as part of this pass.


## Mechanics-expansion pass: acquisition-driven deck choices in shops

Buying a relic with an `AcquisitionDeckChoice` (notably Empty Cage or
Astrolabe) now suspends the shop on a canonical persistent-card selection
prompt, just as acquiring the same relic from a combat reward does.
The shop purchase is paid and marked sold once; the relic and its
acquisition run hooks are applied once; subsequent deck-choice actions
change only the persistent deck. The shop remains open when the last
selection is resolved, with its remaining offers and prices intact.

Shop/reward relic selections share the same reusable resolver to prevent
divergence in eligibility checks, persistent instance ID retention, random
transformation, upgrade outcome and remaining-candidate handling.
The pending choice is deeply forked in `ShopState`, validated against
the owned relic, its sold offer, the source specification and live deck,
and projected into the existing `PrototypeAiDeckChoice` observation
shape. While the prompt is pending, further shop purchases/removal/exit
are blocked. If no eligible cards exist, acquiring the relic leaves the
shop interactive without a spurious empty prompt.

Regression tests cover Empty Cage's two removals, Astrolabe's three
upgraded transforms, cost charged only once, the restoration of normal
shop actions, observation visibility, fork isolation, absence of
empty prompts and rejection of stale candidate IDs.

This is a targeted consistency improvement in the single-player
emulator; no parent AI repository was modified.


## Mechanics-expansion pass: event potion inventory and card-added hook interactions

Full-belt potion rewards from events now offer three ways to resolve the
currently offered potion: replace an occupied belt slot, decline that
individual potion without forfeiting subsequent event rewards, or consume
an existing out-of-combat-usable potion to create an empty slot. In the
last case, the offered potion enters the vacated slot automatically and
the event proceeds to its next queued reward. This eliminates a
previously possible inconsistent pending-replacement state referring to
a slot that an out-of-combat potion action had emptied. If a consumed
potion has an immediate refill effect (e.g. Entropic Brew), the belt
remains full and the original replacement/decline choice stays active.

All of these transitions preserve the one-time payment and acquisition
semantics of compound event choices, do not replay relic hooks, and
ultimately flow through the existing event continuation state machine.
The choice is modeled as a semantic `skip_event_potion` legal action
in addition to `replace_event_potion`; it does not silently discard
an incoming potion.

Card gains from events, shops and rewards now explicitly forward the
canonical run RNG to `CardAdded` relic hooks. Existing deterministic
triggers remain unchanged, but future triggers whose generic run
effects require RNG (e.g. filling potion slots) can safely fire after
card acquisition without an absent-RNG failure. Regression tests
exercise card-acquisition effects from Lucky Fysh and Toxic Egg,
out-of-combat Blood Potion use while a full-belt event reward is
pending, Entropic Brew's refill during the same continuation, declining
sequential potions while preserving subsequent relic deck choices,
and state invariant validity throughout.

These are prototype mechanics and interaction-consistency checks rather
than independently oracle-verified assertions about native StS2 UI
availability of potion actions during event dialogs. No `sts2-ai`
repository modifications are part of this pass.
