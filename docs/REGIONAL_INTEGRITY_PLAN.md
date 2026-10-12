# Underdocks, Hive and Glory integrity plan

Target: single-player STS2 v0.111.0 / 41cef1ea. Initial execution character:
Silent, fully unlocked profile, A0–A10. Extend the existing engine and preserve
the versioned prototype consumer contract. This plan implements the regional
work in FULL_IMPLEMENTATION_ROADMAP.md; it does not replace its global scope.

## Completion standard

Match Overgrowth's delivered source-backed mechanics and regional integration,
then close documented regional gaps. Overgrowth itself is not certified native
parity. Track catalogued, implemented, integrated and native-validated separately.
No fixture registration, successful random run or synthetic source oracle alone
establishes native validation.

For each region require all ordinary and special encounters, every eligible event
branch and act-entry Ancient option, formations and summons, committed intents,
Ascension breakpoints, map/room constraints, encounter/event bags, rewards and
economy, persistent transfers, and terminal behavior. Every reachable mechanic
must execute without Unsupported exceptions or unrelated prototype substitutions.
Shared cards, curses, powers, enchantments, relics and potions needed by those
paths are part of this work. Broader character execution remains the separate
all-character roadmap track.

## 1. Establish the executable baseline

Before the Darv, Philosopher's Stone, Black Star and Ectoplasm slices, 1,847 C#
tests passed. The latest solution build and coverage-baseline check pass; the new
paths were not tested in these updates. Python snapshot/batch/replay checks
passed on the prior baseline.
The current v111 coverage report classifies 80 of 90 encounters as implemented
and integrated into regional encounter queues, with zero native-validated; its
event category now reports 50 of 66 event paths integrated into a regional or
forced quest route, twenty-seven with complete branch inventories, and zero
native-validated. The ten special/event fights are
not counted as ordinary queue integration. These classifications are
conservative and do not replace branch-level review, but they confirm that
registered rosters and the synthetic route are not regional completion
evidence.
Existing branches already deliver all ordinary encounter registrations, regional
experimental routing and Knowledge Demon enemy-side choices. Those implementations
are reused. Hive/Glory queues now prepare at initialization rather than act entry.
Underdocks normal and elite bags now refill for the native 12-normal/15-elite
history lengths while preserving the supported regional pools; refill and
formation checks cover A0, A8 and A9.
Eight synthetic route cases traverse Act 1 from both Overgrowth and Underdocks,
then complete Hive and Glory using test-only combat finishers. A0 routes cover
three seeds per starting region; A10 routes cover both starting regions and the
Glory double boss. This checks route plumbing and state persistence, not
unassisted victories or native RNG parity.
The legal natural-run CLI now preserves its original baseline policy and adds a
balanced coverage policy. On the same 256 A0 seeds (128 per starting region,
offset 256), the balanced policy reached Hive six times versus two for the
baseline, with zero engine blockers; neither policy reached Glory. All six
balanced Hive checkpoints resumed to ordinary combat defeats on floors 1–3,
and repeating the sweep reproduced all terminal hashes. This improves natural
later-act exercise but does not establish Hive completion or an unassisted
three-act victory. See the [natural-run sweep
ledger](reference-builds/v0.111.0-unassisted-silent-run-sweep.md).
Configured Silent runs now prepare all six act-local Ancient assignments at
run start. Hive opens Orobas, Pael or Tezcatara; Glory opens Nonupeipe, Tanx or
Vakuu before map choice. Existing acquisition and reward continuations are
reused. Orobas executes its eight Silent relic options with a locked third
pool when both starter dependencies are absent. Cross-character Sea Glass and
Prismatic Gem offers, plus Colorful Philosophers, are explicitly postponed by
the current user scope. Shared Darv now participates in revealed-profile
assignment and executes all twelve Silent relic branches. Philosopher's Stone
adds one energy per turn and gives living enemies one Strength at combat start.
Black Star adds one relic to elite rewards. Calling Bell adds its Eternal Curse
and three rarity-specific relic rewards; Dusty Tome adds an upgraded Wraith
Form; Pandora's Box transforms Strike and Defend cards. Darv offers Ectoplasm
and Sozu only in Hive. Its 50/50 Dusty Tome offer branch is implemented, while
native offer RNG and act-assignment parity remain for the fidelity pass.
Assignment uses prototype event randomness;
native UpFront scheduling and source offer parity are left for the RNG pass.
See [the assignment ledger](reference-builds/v0.111.0-ancient-assignment.md).
Seven
Orobas relic dependencies now have executable effects for Silent: Radiant
Pearl's temporary Luminesce, Sand Castle's six-card upgrade, Touch of Orobas's
starter replacement, Archaic Tooth's Neutralize-to-Suppress transformation,
Electric Shrymp's Skill-only Imbued acquisition and first-turn autoplay, and
Driftwood's once-per-generated-card-group reroll. Glass Eye also suspends its
acquisition reward and offers five independently selectable or skippable,
rarity-specific groups before returning to the interrupted reward.
Ring of the Drake's two-card draw bonus is exercised through combat turns 1–4.
Alchemical Coffer adds four potion slots and generates four distinct potions
into those slots, while preserving existing inventory and honoring Sozu. The
full-unlock Silent/shared pool follows its source order through native
`NextFloat`/`NextItem` draws. The supported Silent pool includes Cunning
Potion, Fysh Oil, Potion of Binding, Ship in a Bottle, Droplet of Precognition,
Explosive Ampoule, Bottled Potential, Heart of Iron, Shackling Potion,
Mazaleth's Gift, Radiant Tincture, Beetle Juice, Gigantification Potion,
Clarity, Colorless Potion, Powdered Demise, Lucky Tonic and Orobic Acid, and
excludes Ironclad-only Blood Potion and the off-pool legacy Explosive Potion;
all 48 pinned Silent/shared pool entries are now registered, with executable
effects for the newly covered entries. Remaining potion gaps are unlock-filter
coverage, live native cursor validation, and Colorless Potion/Orobic Acid
candidate card pools, which still use currently implemented cards. Prismatic
Gem now implements its persistent +1 energy component, but its foreign
card-pool effect and Sea Glass are deferred cross-character mechanics. See
[the Orobas dependency ledger](reference-builds/v0.111.0-hive-orobas-ancient.md).
Native-shaped Silent card reward and merchant generators now retain the pinned
Silent card-pool order while the legacy prototype reward pool remains sorted.
The desired RNG architecture is recorded in DESIRED_RNG_IMPLEMENTATION.md.
The experimental Glory event draw now filters Round Tea Party below 12 HP and
Grave of the Forgotten when no deck card can receive Souls Power. Boundary tests
and a 96-seed Unknown-room route test exercise these filters. The legacy fixture
remains able to force the Grave event so its locked choice can be tested.
Hive's Amalgamator now retains its source completion page after both card
removals and Ultimate-card creation; its seven pinned page/choice paths have
direct branch tests and a complete inventory in
[the Amalgamator ledger](reference-builds/v0.111.0-hive-amalgamator.md). The
coverage report now honors `branch_inventory_complete`; Amalgamator, Lost Wisp,
Spirit Grafter, Colossal Flower, Doors of Light and Dark, Drowning Beacon,
Sunken Treasury, Sunken Statue, Field of Man-Sized Holes, Reflections, Round
Tea Party, Hungry for Mushrooms, Tinker Time, and Grave of the Forgotten are
fourteen of 66 event
inventories marked complete. Tinker Time's type/rider offers and all nine Mad
Science effects have source-shaped event/card tests; native stream parity
remains unverified. Lost
Wisp now keeps its source result page after Claim/Search, with stored gold and
no extra result draw. See [the Lost Wisp ledger](reference-builds/v0.111.0-hive-lost-wisp.md).
Spirit Grafter now retains its result page after Let It In or Rejection,
including the optional card-upgrade step and unblockable HP loss. See [the
Spirit Grafter ledger](reference-builds/v0.111.0-hive-spirit-grafter.md).
Colossal Flower now retains the extraction or Pollinous Core result page after
its three damage-and-prize stages. See [the Colossal Flower ledger](reference-builds/v0.111.0-hive-colossal-flower.md).
Doors of Light and Dark now upgrades up to two distinct eligible cards on
Light, offers Dark's single removal, and preserves both result pages until
continuation. See [the Doors ledger](reference-builds/v0.111.0-underdocks-doors-light-dark.md).
Drowning Beacon's Bottle and Climb outcomes now preserve their completion page
after the optional potion decision or Fresnel Lens acquisition. See [the
Drowning Beacon ledger](reference-builds/v0.111.0-underdocks-drowning-beacon.md).
Sunken Treasury now preserves each chest result after settling its persisted
gold roll and, for the large chest, adding Greed. See [the Treasury ledger](reference-builds/v0.111.0-underdocks-sunken-treasury.md).
Sunken Statue now keeps its Sword or Dive result page after the fixed relic or
stored gold-and-damage outcome. See [the Statue ledger](reference-builds/v0.111.0-shared-sunken-statue.md).
Field of Man-Sized Holes now retains the Resist result after the two-card
removal and Normality grant, and the Perfect Fit result after enchantment.
See [the Field ledger](reference-builds/v0.111.0-hive-field-of-man-sized-holes.md).
Glory's Reflections now preserves the Touch a Mirror or Shatter result page
after its deck changes and Bad Luck grant. See [the Reflections ledger](reference-builds/v0.111.0-glory-reflections.md).
Round Tea Party now retains its terminal pages after Enjoy Tea or after the
second-stage fight, damage, and relic grant. See [the Tea Party ledger](reference-builds/v0.111.0-glory-round-tea-party.md).
Hungry for Mushrooms now preserves both relic outcomes after acquisition and
the Fragrant Mushroom damage/upgrade effects. The obsolete MEDLEY localization
page is excluded from active branches. See [the mushroom ledger](reference-builds/v0.111.0-glory-hungry-for-mushrooms.md).
The other event inventories still require source audits.
Prepared Hive/Glory boss selection now applies each act's source
`BossDiscoveryOrder` using `SimulationProfile.SeenBossEncounters`, while still
consuming the preselected boss draw; Glory's A10 second boss excludes the final
discovery-selected boss. Tests cover fresh and partially seen profiles, all
bosses seen, profile fork ownership, and the A9/A10 boundary. Native
`run.up_front` draw ownership and live discovery-state capture remain open.
Native certification and the completion standard below remain open.
Glory's Lantern Key chest now presents its two potions and two distinct relics
on one optional reward screen, with each offer independently selectable or
skippable. Its prototype potion/relic pools and native RNG ownership remain
unverified.

- Restore dependencies and run existing C#, Python consumer and coverage checks.
- Reconcile stale Act 1 documentation with executable registries and tests.
- Produce per-region encounter and event-page/branch ledgers from the pinned
  inventory and source audits. Include special fights, Ancients, dependencies,
  A0–A10 breakpoints, acquisition paths and known Unsupported boundaries.
- Record Overgrowth's actual delivered behavior and unresolved fidelity limits
  as the comparison baseline; do not copy undocumented approximations.
- Make regional completion reports distinguish gated fixtures from route content.

Gate: every remaining regional gap is named and has an implementation location,
source provenance and verification requirement.

## 2. Close Underdocks

Current executable check (2026-10-12): all 101 tests selected by
`FullyQualifiedName~PrototypeUnderdocks` pass. The eight synthetic full-route
cases also pass from both Act 1 regions through Hive and Glory, including A10
double boss. These checks establish supported branch execution and transition
plumbing; they do not establish exhaustive source branch inventory, unassisted
combat completion, or native RNG parity.

- Audit the existing 20 ordinary encounter models, formations and regional bags;
  correct missing phase, summon, ally-death and Ascension behavior.
- Audit all ten regional events page by page, including fatal choices, optional
  selections, duplicate relics, event combats and delayed reward continuations.
- Verify map/unknown-room rules, preselected bosses, encounter exclusions,
  history, replenishment and native-shaped shops/rewards/treasures.
- Add direct reachability tests for every branch and deterministic regional runs.

Gate: Underdocks has the same implemented/integrated evidence standard as
Overgrowth, with no regional fallback or reachable unsupported behavior.

## 3. Complete shared later-act progression infrastructure

- Generalize regional state, map cursors, floor accounting, encounter/event bags,
  boss selection and room dispatch across all four native act identities.
- Preserve HP, deck-instance modifications, resources, per-instance relic state,
  reward/pity/removal counters, history and RNG ownership across act transitions.
- Implement regional act-entry/Ancient assignment and boss reward ordering from
  pinned source, including route-specific and difficulty-dependent endings.
  Supported Pael/Tezcatara opening execution is integrated once assigned. The
  source assignment call order is audited in
  [the Ancient assignment ledger](reference-builds/v0.111.0-ancient-assignment.md),
  with all three Hive act-local Ancients now executable. Shared Darv assignment
  and all twelve Silent relic branches are integrated; run-start UpFront
  integration remains open for the RNG work.
- Remove Hive/Glory initialization rejection only after supported regional
  policies exist. Keep unfinished paths explicitly gated during development.
- Establish an explicit deterministic test route through Act 1 → Hive → Glory
  early, without treating that fixture as complete random-route integration.

Gate: transitions, snapshots, forks and consumer observations work across all
three acts; final victory and death occur at the correct boundaries.

## 4. Complete and integrate Hive

- Audit all 20 ordinary encounters and every special/event fight. Reuse existing
  models, closing reactive hooks, minion identity, revival and phase gaps.
- Preserve the implemented Knowledge Demon enemy-origin choice continuation.
  Its three source option sets, committed-move resume, snapshot/fork safety,
  invalid-option rejection and no-auto-pick behavior are directly tested in
  `V111HiveBossFirstSliceTests`; native draw-order validation remains open.
- Close all regional events, Ancient options and content dependencies.
  Colorful Philosophers depends on source-audited non-Silent card pools; the
  current reward catalogue is Silent-only, so the event cannot be completed by
  a new event handler alone.
- Promote audited formations into regional weak/normal/elite/boss bags with
  correct eligibility, ordering, history and map routing.
- Integrate rewards, shops, treasure, rest and event combat completion.

Gate: a normal configured Silent run enters and completes Hive, and every
regional entry/branch has direct integration and semantic coverage.

## 5. Complete and integrate Glory

- Audit all 18 ordinary encounters and special fights, including multi-body
  Knights, Queen ownership/interrupts, Test Subject resurrections and Aeonglass
  card-instance mutation and cleanup.
- Close all regional events and Ancient options, including Lantern Key/Repy
  carryover, event-only content and timed Battleworn Dummy fights.
- Glory's Signet Ring (+888 gold), Looming Fruit (+31 current and maximum HP),
  Diamond Diadem (20 opening Block plus Blur), Sai (7 Block per player-side
  turn start), Spiked Gauntlets (+1 energy and +1 Power cost), Crossbow
  (one free generated Attack at each player-side turn start), and Iron Club
  (draw a card every fourth card played), Throwing Axe (repeat the first card
  each combat), Meat Cleaver (remove two cards at rest to gain 5 Max HP),
  Tanx's Whistle (adds a 2-cost, 33-damage Stun attack), and Tri Boomerang
  (enchant three Attacks with Instinct, doubling powered attack damage), and
  Claws (transform up to six chosen deck cards into escalating two-hit Mauls),
  and War Hammer (upgrade up to four random cards after Elite victories)
  dependencies are implemented and tested through reward/combat resolution.
  Tanx's offer generation and all ten relic branches run through a named
  Ancient fixture and now also through normal act-local assignment.
  Native RNG scheduling remains deferred. Individual Nonupeipe
  dependencies now include Blessed Antler, Beautiful Bracelet, Fur Coat,
  Glitter, Jewelry Box/Apotheosis, Brilliant Scarf and Delicate Frond.
  Nonupeipe's conditional offer sequence and all ten relic branches execute
  through a named Glory Ancient fixture and normal act-local assignment. Fiddle's opening hand bonus and player-turn extra-draw suppression,
  Blood-Soaked Rose's energy and Enthralled acquisition, Whispering Earring's
  first-turn paid autoplay, and Sere Talon's Max HP loss plus Wish cards are
  implemented as dependencies. Vakuu now samples one relic from each of its
  three source pools using the native event stream, and Lord's Parasol runs
  free shop purchases through existing acquisition hooks before offering a
  potion replacement or forced card-removal choice. These branches use a
  named Ancient fixture and normal act-local assignment. Preserved
  Fog now removes up to
  three selected cards before adding its Innate, Ethereal, Eternal Folly curse
  through reward, shop and event deck-choice continuations. Music Box also
  copies the first Attack played each player turn into hand as Ethereal and
  resets its once-per-turn guard at the next player-turn start. Jeweled Mask
  moves a non-Innate Power from draw pile to hand before the opening draw and
  makes it free for the turn. Choices Paradox now offers five distinct Retain
  cards on the opening player turn, with a snapshot-safe choice; its current
  candidate pool is limited to implemented Silent card mechanics.
  Distinguished Cape now adds two distinct modifier-eligible curses using the
  native `run.niche` stream, then adds three Apparitions. All ten
  source-eligible curses are modeled; its reward test checks selected curse
  IDs and the native stream cursor. Vakuu's three-pool event, native offer
  stream, and Lord's Parasol shop continuation are implemented through the
  named Ancient fixture and normal act-local assignment.
  [Glory Ancient source ledger](reference-builds/v0.111.0-glory-ancients.md).
- Integrate regional bags, map routing, rewards and difficulty-dependent boss
  sequence/terminal rules from source.

Gate: both Overgrowth → Hive → Glory and Underdocks → Hive → Glory complete
through supported configured execution, with all regional branches reachable.

## 6. RNG and evidence closure throughout the work

- Follow the existing native RNG fidelity matrix. Audit stream ownership,
  source pool order and draw order for maps, formations, HP/AI, summons, events,
  Ancients, rewards and shops before promoting native paths.
- Preserve cursor state through choices, deaths, transitions, forks and restore;
  explicitly report remaining prototype RNG boundaries.
- Extend native entry beyond Act 1 weak/normal encounters as audited content
  requires it. Do not equate native PRNG use with end-to-end seed parity.
- Add live reference fixtures when a pinned installation/capture corpus becomes
  available. Native validation is deferred; continue implementation and integration work.

## Verification and delivery

For each reviewed batch run focused semantic/integration tests, existing relevant
regressions and coverage consistency checks. At region gates run the full suite,
Python snapshot/batch compatibility checks and deterministic multi-seed sweeps
for both Act 1 routes. Direct fixtures cover rare branches, A0–A10 rule changes,
fatal outcomes, pool exhaustion, suspended choices and snapshot/fork isolation;
sweeps supplement these rather than replacing them.

Deliver code and tests in dependency-ordered batches, updating the regional
ledger and current implementation report each time. Enable normal regional
generation only after its executable content and routing gate passes. Report
implementation/integration completion separately from live-game fidelity.

The final native release corpus and performance gates in the full roadmap remain
in force. Missing real-game evidence prevents native certification, not completion
of the authorized regional implementation work.

Grave of the Forgotten now retains both result pages after its existing curse,
enchantment or relic effects. Its nine source paths are audited, with locked
selection, snapshot/fork and explicit continuation coverage. See the
[Grave ledger](reference-builds/v0.111.0-glory-grave-of-forgotten.md).
