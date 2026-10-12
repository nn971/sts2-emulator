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

Current validation (2026-10-12): 1,783 C# tests pass, executable coverage
matches its reviewed baseline, and Python snapshot/batch/replay checks pass.
The current v111 coverage report classifies 80 of 90 encounters as implemented
and integrated into regional encounter queues, with zero native-validated; its
event category reports zero complete entries. The ten special/event fights are
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
When the prepared Hive history contains an assigned Pael or Tezcatara Ancient,
Act 1→Hive now opens that Ancient before map choice. The persisted assignment,
offer generation, event context and snapshot restore are covered. Run-start
Ancient selection, Orobas's event and exact `run.up_front` assignment draws
remain unimplemented; this does not enable normal Ancient assignment. Seven
Orobas relic dependencies now have executable effects for Silent: Radiant
Pearl's temporary Luminesce, Sand Castle's six-card upgrade, Touch of Orobas's
starter replacement, Archaic Tooth's Neutralize-to-Suppress transformation,
Electric Shrymp's Skill-only Imbued acquisition and first-turn autoplay, and
Driftwood's once-per-generated-card-group reroll. Glass Eye also suspends its
acquisition reward and offers five independently selectable or skippable,
rarity-specific groups before returning to the interrupted reward.
Ring of the Drake's two-card draw bonus is exercised through combat turns 1–4.
Alchemical Coffer also has a partial acquisition hook: it adds four potion slots
and generates four distinct potions into those slots, while preserving existing
inventory and honoring Sozu. The supported Silent pool includes Cunning
Potion, Fysh Oil, Potion of Binding, Ship in a Bottle, Droplet of Precognition,
Explosive Ampoule, Bottled Potential, Heart of Iron, Shackling Potion,
Mazaleth's Gift, Radiant Tincture, Beetle Juice, Gigantification Potion,
Clarity, Colorless Potion, Powdered Demise, Lucky Tonic and Orobic Acid, and
excludes Ironclad-only Blood Potion and the off-pool legacy Explosive Potion;
all 48 pinned Silent/shared pool entries are now registered, with executable
effects for the newly covered entries. The remaining potion gaps are the
Alchemical Coffer's source acquisition/offer flow and RNG parity; Colorless
Potion and Orobic Acid also draw only from currently implemented card
catalogues. Prismatic Gem now implements
its persistent +1 energy component, but its foreign card-pool effect is still
missing; Sea Glass remains unimplemented. See [the Orobas
dependency ledger](reference-builds/v0.111.0-hive-orobas-ancient.md).
The desired RNG architecture is recorded in DESIRED_RNG_IMPLEMENTATION.md.
The experimental Glory event draw now filters Round Tea Party below 12 HP and
Grave of the Forgotten when no deck card can receive Souls Power. Boundary tests
and a 96-seed Unknown-room route test exercise these filters. The legacy fixture
remains able to force the Grave event so its locked choice can be tested.
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
  but run-start UpFront integration and the remaining Hive Ancient are still
  open.
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
  Ancient fixture; ordinary Glory Ancient assignment remains gated. This
  does not add Nonupeipe or Vakuu, or complete normal Tanx run integration. See the
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
  available. Until then, keep native validation unverified and continue source
  implementation and integration work.

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
