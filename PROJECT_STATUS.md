# Project status

**Stage:** Overgrowth Act 1 combat fidelity on the whole-run prototype skeleton.

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

- integrate the prototype AI adapter from the parent `sts2-ai` project;
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
