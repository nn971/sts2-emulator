## Current Silent regional execution update (2026-10-13)

The executable coverage report now recognizes all four configured act routes
as implemented and integrated. This check validates the Silent route
configuration, encounter registrations against each act inventory, regional
event registration (with Colorful Philosophers still deferred), and the
act-entry Ancient definitions. It records route integration, not complete
branch coverage or native parity. War Historian Repy is also recognized as
implemented across its nine inventoried branches. The overall report currently
records 62/66 events implemented and 64/66 integrated.

Byrdonis's Territorial Strength now resolves at enemy side-turn end, so a
stunned or otherwise skipped action no longer suppresses the gain.
Native Phrog Parasite deaths also handle Poison at enemy-turn start: the four
Wrigglers skip the current action pass, then present their source-shaped Stun
move before their alternating slot openers.
Vantom's Slippery still caps raw HP loss, but spends a stack only for damage
events, matching its separate `ModifyHpLostAfterOsty` and `AfterDamageReceived`
hooks.
Trash Heap now grants Dream Catcher, Hand Drill, Maw Bank, and The Boot with
their canonical relic identities. The event rarity keeps these rewards out of
ordinary relic drops; The Boot's powered-attack floor reuses its existing
damage modifier. Darkstone Periapt is also wired as a Trash Heap event relic;
each Curse added to the deck grants 6 Max HP. See
[mechanics gap 055](reference-builds/v0.111.0-mechanics-gap-055.md).
Centennial Puzzle is implemented as a common shared relic: the first
unblocked player damage event in combat draws three cards. See the
[pinned relic audit](reference-builds/v0.111.0-mechanics-gap-050.md) for the
event timing boundary retained for later fidelity work.
Tungsten Rod is implemented as a Rare shared relic: it reduces each owner HP
loss by one across combat and configured event/run effects. Its pinned
mechanic is recorded in
[mechanics gap 051](reference-builds/v0.111.0-mechanics-gap-051.md).
Sword of Stone now stores its five-elite transformation rule as relic
metadata. Each active copy becomes Sword of Jade in its relic slot after its
fifth elite victory; Jade grants 3 Strength at combat start. Coverage links
both Overgrowth and Underdocks Sunken Statue offers to the transformation and
combat trigger. See
[mechanics gap 057](reference-builds/v0.111.0-mechanics-gap-057.md).
Coverage also recognizes Astrolabe, Sozu, Snecko Eye, and Velvet Choker in the
configured boss reward pool, checking each relic's acquisition or combat
metadata. See
[mechanics gap 058](reference-builds/v0.111.0-mechanics-gap-058.md).
Pocketwatch is now a Rare shared relic: if its owner played at most three
cards on the preceding turn, it adds three cards to the opening hand from turn
two onward. The play count persists across turn transitions and resets with
combat state. See
[mechanics gap 059](reference-builds/v0.111.0-mechanics-gap-059.md).
The Abacus is now a Shop relic: each owner draw-pile shuffle grants 6 Block
through the shared Block modifier path. See
[mechanics gap 060](reference-builds/v0.111.0-mechanics-gap-060.md).
Tuning Fork is now an Uncommon relic that grants 7 Block for each tenth Skill
played, with its per-instance counter persisted across combats. See
[mechanics gap 061](reference-builds/v0.111.0-mechanics-gap-061.md).
Venerable Tea Set is now in the Common relic pool. Rest Site entry charges it
for +2 Energy at the next combat's opening energy reset; the charge is then
cleared. See
[mechanics gap 062](reference-builds/v0.111.0-mechanics-gap-062.md).
Lizard Tail now prevents one fatal HP loss, restores half of Max HP (minimum
1), and persists its used state. Run effects and combat death prevention share
the hook, with automatic combat potions resolving first. See
[mechanics gap 063](reference-builds/v0.111.0-mechanics-gap-063.md).
Sling of Courage is now a Shop relic that adds 2 Strength on Elite room
entry, using the shared combat-start hook for Overgrowth, Underdocks, Hive,
and Glory. Melted copies do not trigger. See
[mechanics gap 064](reference-builds/v0.111.0-mechanics-gap-064.md).
Red Mask is now a Common relic that applies 1 Weak to all enemies on the
owner's first player-side turn. See
[mechanics gap 065](reference-builds/v0.111.0-mechanics-gap-065.md).
Stone Calendar is now a Rare relic that deals 52 damage to all enemies at
the end of turn 7. See
[mechanics gap 066](reference-builds/v0.111.0-mechanics-gap-066.md).
Pollinous Core is now coverage-verified through Act 2's Colossal Flower
reward path. Its fourth hand draw adds 2 cards and resets its saved counter;
melted copies no longer advance it. See
[mechanics gap 067](reference-builds/v0.111.0-mechanics-gap-067.md).
Royal Poison deals four unblockable HP damage on its owner's first turn of
each combat after Round Tea Party's Enjoy Tea choice. Melted copies are
inactive. See [mechanics gap 052](reference-builds/v0.111.0-mechanics-gap-052.md).
Hungry for Mushrooms now models both relic acquisitions: Big Mushroom grants
Max HP and keeps its first-turn draw penalty; Fragrant Mushroom applies its
HP loss before upgrading two distinct cards. See
[mechanics gaps 053](reference-builds/v0.111.0-mechanics-gap-053.md) and
[054](reference-builds/v0.111.0-mechanics-gap-054.md).
Lost Wisp is integrated through Hive's Claim choice; its event relic deals 8
unpowered damage to all enemies after each owner-played Power card. See
[mechanics gap 056](reference-builds/v0.111.0-mechanics-gap-056.md).

Configured three-act runs now persist Hive and Glory Ancient assignments at
run start and dispatch all six act-local Ancient openings at act entry.
Supported Hive and Glory Ancient relic choices now retain their finished page
after acquisition and queued rewards, then return to map choice without
counting the opening as a map room.
Orobas's eight Silent relic offers reuse the existing acquisition
handlers and continuations. Coffer capacity metadata now records its four
added potion slots. Potion Belt adds two persistent potion slots on pickup.
Cross-character card mechanics (including Sea Glass,
Prismatic Gem's foreign pools and Colorful Philosophers) are postponed by user
scope. Shared Darv now has revealed-profile assignment and all twelve Silent
relic branches. Calling Bell adds its Eternal Curse and Common, Uncommon and
Rare relic rewards; Dusty Tome adds an upgraded Wraith Form; Pandora's Box
transforms Strike and Defend cards. Ectoplasm and Sozu are offered only in
Hive. Exact RNG and fidelity inspection are follow-up work; act assignment
still uses prototype event randomness. Aroma of Chaos preserves its
choice-specific follow-up page after transforming or upgrading a card. Brain
Leech is integrated in Overgrowth, Underdocks and Hive: Share Knowledge offers
five Silent cards and requires one selection, while Rip the Leech Off costs
5 HP and offers three implemented colorless cards.
Shovel and Girya now enter the shared Silent relic pool. Shovel's Dig reward
uses the run relic bag and the ordinary relic acquisition continuation;
Girya's Lift count persists on the relic, caps at three, and grants that much
Strength at each combat entry. All eight single-player rest-site option
identifiers now map to executable actions. See the
[rest-site Dig and Lift slice](reference-builds/v0.111.0-rest-site-dig-lift.md).
Coverage now recognizes behavior-bearing relic definitions in the ordinary
Silent/shared pool through their typed mechanics and the common rarity-bag
acquisition path. This records emulator integration only; native relic
behavior and RNG parity remain unverified.
Neow's Lead Paperweight now suspends the opening for a one-of-two supported
Colorless card reward drawn from all implemented single-player Colorless
reward cards. Fully unlocked runs include the epoch-gated cards; restricted
profiles are unsupported, and native reward RNG remains open.
Glory's Test Subject now advances its alternating Nemesis Intangible at the
enemy side-turn boundary, including when a stun or scripted skip prevents its
move from executing.
Neow now retains the selected positive or cursed option's finished page after
its relic effect and queued rewards resolve; dismissing the pre-map opening
returns to map choice without completing a room. Choice generation still
approximates native unlock filtering and RNG. The implemented opening-choice
branches are covered except for Kaleidoscope's deferred foreign card pools.
Neow's Bones adds one random Curse from the ten-card modifier-generated Curse
pool after both relic pickups and any relic acquisition continuations resolve;
its draw still uses prototype reward RNG, with native stream parity deferred.
Dowsing Rod's Neow pickup, Dowsing's persistent quest progress, and the
Abundance transformation are now counted as integrated through their actual
acquisition chain. Abundance's generated Power choice still uses prototype
card-generation pools and RNG.
The Legends Were True is integrated in both Act 1 regions. Nab the Map adds
Spoils Map, which assigns the first generated Hive Treasure node as its quest
and grants 600 gold on entry. Slowly Find an Exit loses 8 HP and offers a
random potion. The visible map quest marker, native treasure traversal,
potion pool and RNG parity remain open; see the
[pinned event and card audit](reference-builds/v0.111.0-legends-were-true.md).
Strawberry is integrated as a Common shared relic. Its pickup adds 7 Max HP
and the emulator includes it in the Silent common relic queue. Relic RNG
parity remains open; see the
[pinned Strawberry audit](reference-builds/v0.111.0-strawberry.md).
Pear is integrated as an Uncommon shared relic; its pickup adds 10 Max HP and
it enters the Silent uncommon relic queue. Relic RNG parity remains open; see
the [pinned Pear audit](reference-builds/v0.111.0-pear.md).
Whetstone and War Paint are integrated as Common shared relics, randomly
upgrading up to two upgradable Attacks or Skills on pickup, respectively.
The candidate selection uses prototype RNG; see the
[pinned pickup audit](reference-builds/v0.111.0-whetstone-war-paint.md).
Eternal Feather heals 3 HP per five deck cards on entering a Rest Site, even
when the player chooses an option other than healing. It is in the Silent
uncommon relic queue; see the
[pinned room-entry audit](reference-builds/v0.111.0-eternal-feather.md).
Petrified Toad now procures a 15-damage Potion Shaped Rock before each combat
when potion acquisition is allowed and a slot is open. The token potion is
excluded from ordinary pools; see the
[pinned relic and potion audit](reference-builds/v0.111.0-petrified-toad.md).
Juzu Bracelet removes Monster from Unknown-room outcomes across Overgrowth,
Underdocks, Hive and Glory. Its excluded odds stop increasing, while the
remaining room weights preserve their source thresholds. The roll still uses
prototype RNG; see the [pinned map relic audit](reference-builds/v0.111.0-juzu-bracelet.md).
Pantograph heals 25 HP before each Boss combat across the four acts, capped at
Max HP; see the
[pinned boss-entry audit](reference-builds/v0.111.0-pantograph.md).
Winged Boots now allows next-row map travel, consuming its three persistent
uses only when the selected point skips a normal map edge.
Ring of the Snake is assigned in the Silent starting loadout and adds two
cards to each combat's opening hand.
Fake Merchant is integrated into the configured later-act Silent route. Its
six stocked fake relics can be bought for 50 gold each; throwing Foul Potion
starts the one-merchant event fight, whose victory offers the fixed 300 gold,
Fake Merchant's Rug, and all unsold stocked relics. All nine fake relic effects
are executable, including the Tea Set's next-combat energy after a Rest Site.
Foul Potion now deals its source 12 damage to all enemies without damaging the
player. Prototype RNG is used for the inventory and fight, so native parity
remains open; see the [Fake Merchant implementation note](reference-builds/v0.111.0-fake-merchant.md).
This or That is registered as a shared event in Overgrowth, Underdocks, Hive,
and Glory. Its plain choice deals 6 HP damage before awarding the saved 41–68
gold roll. Its ornate choice obtains a random relic, resolves any relic
acquisition deck choice, then adds Clumsy.
The Future of Potions is integrated in Hive and Glory for runs with at least
two potions. Trading one consumes it and offers three upgraded Silent cards
with the potion's mapped rarity and event-entry rolled card type. The offer
and card-reward generator persist through state forks; potion use stays
disabled until the event closes, and Driftwood retains the same rarity/type
filter when rerolling. Source RNG and reward-hook fidelity remain unverified.
See the [pinned source audit](reference-builds/v0.111.0-future-of-potions.md).
Relic Trader is integrated in Hive and Glory when at least five relics pass
the modeled tradability check. It persists three shuffled owned/replacement
pairs, then removes the selected relic and applies the replacement's
acquisition effects. The supported Silent relic bag supplies replacements;
source rarity and relic pool parity remain unverified. See the
[Relic Trader audit](reference-builds/v0.111.0-relic-trader.md).
After the final Glory boss reward, configured Silent runs now enter The
Architect's one-choice ending and become victorious after Proceed. The
ending links to a zero-weight scene encounter with a 9999 HP Architect actor.
Profile-specific dialogue and animations remain omitted from the headless
state; see the [ending audit](reference-builds/v0.111.0-the-architect.md).
Akabeko is now in the shared Silent Uncommon relic pool and grants 8 Vigor at
the start of its owner's first combat turn. Card-backed Attack cards consume
Vigor after the completed attack; cardless powered attacks remain unmodeled.
See the [pinned Akabeko and Vigor audit](reference-builds/v0.111.0-akabeko.md).
Stone of All Time is integrated in Hive. Lift discards the potion chosen on
event entry and grants 10 Max HP. Push deals 6 HP damage, then offers one
eligible Attack for Vigorous 8. Vigorous adds its amount to powered Attack
damage and disables itself after the card's first completed play.
Infested Automaton is integrated in Hive for Silent. Study adds one eligible
Power and Touch Core adds one eligible zero cost card when the card generator
returns one; both choices retain their result page even when no card is added.
Doll Room is integrated in Hive for Silent. The free choice takes a random doll;
Take Some Time loses 5 HP and offers two dolls, while Examine loses 15 HP and
offers all three. Daughter of the Wind grants 1 Block per Attack, Mr. Struggles
damages all enemies by the current turn number, and Bing Bong copies each new
deck card to the bottom of the deck. Doll reward sampling uses prototype RNG;
native RNG parity remains open. See [mechanics gap
086](reference-builds/v0.111.0-mechanics-gap-086.md).
Crystal Sphere is integrated in Hive and Glory for Silent. Uncover Future pays
the rolled 51–99 gold cost for three divinations; Payment Plan adds Debt and
grants six. The forkable 11×11 board supports the small and large reveal tools,
and resolves revealed gold, card, potion, relic and Doubt rewards through the
normal reward pipeline. Board and reward sampling still use prototype pools
and RNG. See [mechanics gap
087](reference-builds/v0.111.0-mechanics-gap-087.md).
The Lantern Key now retains its return result page and exits to the map after
the optional quest card reward from its fight.
Zen Weaver retains its selected result page after the breathing-technique
reward or after each requested card removal and gold payment completes.
Battleworn Dummy now retains its Victory or Defeat result page after the
selected training combat and any setting reward resolve.
Trial now retains the selected court's outcome page after its curse, relic,
deck choice, or card reward continuations finish.
Tea Master is integrated in Glory for runs with at least 150 gold. Bone Tea
upgrades eligible cards in the opening hand of the next combat, Ember Tea
grants 2 Strength on each of the next five combat room entries, and Tea of
Discourtesy inserts two Dazed cards into the next combat's draw pile. The tea
charge counters persist across combats; event and combat RNG parity remain
open.
Silver Crucible now consumes its first three group charges on supported custom
event card rewards as well as combat rewards.
Silken Tress now persists its one-shot Glam enchantment through the first
combat card reward.
Ambergris now queues its combat extra turn through the shared end-turn
pipeline, skipping enemy actions while preserving turn counters and snapshots.
Self-Help Book is integrated as a shared event in all three Acts. It applies
Sharp 2 to an Attack, Nimble 2 to a block-producing Skill, or Swift 2 to a
Power, with a Move On path when no eligible card exists. Sharp now adds its
enchantment amount to each powered Attack hit.
Symbiote is now in the Hive and Glory event pools. Its Corrupted enchantment
adds 50% damage to powered Attacks and costs 2 HP when played; its alternative
transforms one deck card. The transformation still uses prototype card and
reward RNG pools; see the [Symbiote mechanics audit](reference-builds/v0.111.0-symbiote-corrupted.md).
Before the Darv, Philosopher's Stone, Black Star and Ectoplasm slices, 1,847 C# tests
passed, including the eight synthetic full-route cases and Ancient
acquisition/assignment coverage. The latest solution build and coverage-baseline
check pass. Potion Courier is integrated into Hive and Glory. Morphic Grove
retains the Group and Loner result pages. Wellspring and Whispering Hollow
retain their result pages after potion and card continuations. Waterlogged
Scriptorium now keeps its result page after each optional Steady selection;
Trash Heap retains its result page after either successful reward, and
Hand Drill now applies Vulnerable on actual player-caused block breaks,
including Expose's explicit block removal. Pet attack actions remain
unmodeled; see the [Trash Heap relic mechanics audit](reference-builds/v0.111.0-trash-heap-relic-mechanics.md).
Endless Conveyor retains its Observe Chef/Leave results. Spiraling Whirlpool
retains its Observe or Drink result after the selected enchantment or healing
completes. The coverage report
records 50 integrated event paths, thirty-five complete event implementations,
and 34 events with complete branch inventories. Sapphire Seed and Unrest Site
now retain their choice-specific result pages after their effects resolve.
Jungle Maze Adventure now retains its Solo or Join result page after the
persisted gold and damage effects resolve; the pinned source confirms its two
choices and both terminal pages.
Tablet of Truth now retains its Smash, Give Up, or fifth-Decipher result page;
Byrdonis Nest and Wood Carvings now retain their choice-specific result pages.
Punch Off retains its Nab result page after the relic reward resolves. Native
validation remains unverified.

# v111 implementation status

Target: [FULL_IMPLEMENTATION_ROADMAP.md](FULL_IMPLEMENTATION_ROADMAP.md).
Pin: v0.111.0 / 41cef1ea and the fingerprint in
[the reference manifest](../data/reference_builds/v0.111.0-41cef1ea.json).
This is an intermediate implementation. Final release gates remain open.

Tezcatara now has ten executable reward dependencies behind explicit named
fixtures: Very Hot Cocoa, Yummy Cookie, Toasty Mittens, Seal of Gold, Pumpkin
Candle, Storybook, Biiig Hug, Nutritious Soup, Golden Compass and Toy Box. Shared
side-turn, acquisition, map, shuffle and choice continuations provide their effects.
Toy Box uses the current restricted relic bag and persists wax/melted lifecycle
state. The configured Tezcatara opening samples all three complete pools,
including the conditional Soup offer, and all ten relic choices retain their
finished page after acquisition continuations. The three draws and ordered
offers persist through snapshots. Native UpFront assignment, unlock-profile
filtering and RNG cursor parity remain for the fidelity pass. Mittens/Gambling
Chip callbacks use a separate resumable post-draw stage. Toolbox suspends before
the opening draw. Storybook grants Brightest Flame through shared relic
acquisition; coverage checks its upgraded/base card definition and melted-copy
behavior. Toy Box's wax offers resume the acquiring event, shop or parent reward;
see [Tezcatara dependency audit](reference-builds/v0.111.0-tezcatara-reward-dependencies.md).
Biiig Hug coverage checks its Tezcatara reward, four-card removal selection,
Soot insertion after discard recycling, and melted-copy behavior.
Very Hot Cocoa coverage checks its Tezcatara reward, turn-one Energy grant,
later-turn gating, and melted-copy behavior. Yummy Cookie coverage resolves its
four-card acquisition choice and verifies four distinct upgrades while the
fifth eligible card remains unchanged; see [mechanics gap 079](reference-builds/v0.111.0-mechanics-gap-079.md).
Toasty Mittens coverage resolves the opening and next two turn prompts, checks
exhaust and Strength progression, and confirms melted-copy suppression; see
[mechanics gap 080](reference-builds/v0.111.0-mechanics-gap-080.md).
Seal of Gold coverage checks the affordability boundary, ordered duplicate
triggers and melted-copy suppression; see [mechanics gap 081](reference-builds/v0.111.0-mechanics-gap-081.md).
Pumpkin Candle coverage checks its five-charge acquisition, Energy bonus,
depletion across six idempotent combat exits, re-kindling and melted-copy
suppression; see [mechanics gap 082](reference-builds/v0.111.0-mechanics-gap-082.md).
Golden Compass coverage checks the Tezcatara acquisition, all map rooms and
edges, saved acquisition act, act gating and mid-act node handoff; see
[mechanics gap 083](reference-builds/v0.111.0-mechanics-gap-083.md).
Nutritious Soup coverage checks Tezcatara's conditional offer and acquisition
enchantment across eligible and ineligible Strike instances; see
[mechanics gap 084](reference-builds/v0.111.0-mechanics-gap-084.md).
Toy Box coverage follows its Tezcatara reward through five wax relic pickups
and checks ordered melting through combat fifteen, idempotence, the cap and
melted-copy suppression; see
[mechanics gap 085](reference-builds/v0.111.0-mechanics-gap-085.md).

Pael now has named Horn, Flesh, Blood, Tears, Wing, Claw, Growth, Tooth, Eye and Legion reward fixtures. Horn adds two Relax
cards through shared acquisition; Flesh modifies energy reset from turn three;
Blood modifies every hand draw, and Tears carries leftover energy into the next
side-start bonus. Wing sacrifices card rewards for every-second front-bag relic
obtains. Claw enchants eligible Defends with Goopy, including persistent growth
after each play. Growth marks a card for the repeatable Clone rest action. Tooth
removes five upgradable cards, stores their complete instances, and returns one
random card upgraded after each surviving combat. Eye exhausts the hand when a
turn has no manual plays and grants one enemy-skipping extra turn per combat;
autoplay is tracked separately. Legion saves one of four skins and its combat
pet identity, doubles positive player card block while ready, then cools down
for two player turns. Pael's act-entry fixture now builds the three ordered
source pools, applies the deck and event-pet eligibility rules, samples with
three persisted native event-context draws, and validates the exact offers and
cursor after snapshot/fork. Configured run-start assignments dispatch Pael at
the Hive boundary; native UpFront assignment and offer cursor parity remain
open. Pael's conditional Legion offer and
Overgrowth's Byrdonis Nest now use the same source `HasEventPet` rule. See the
[Pael dependency audit](reference-builds/v0.111.0-pael-reward-dependencies.md).

Latest regional preparation work moves the existing Hive/Glory encounter
histories to run initialization and persists untouched future-act queues.
Act entry transfers them without selection draws. See
[run-start regional history](reference-builds/v0.111.0-upfront-regional-history.md)
for verification, experimental snapshot compatibility and remaining native
upfront/Ancient/RNG gaps. Earlier act-entry queue descriptions are historical.

The experimental route now enters both later acts and uses pre-generated
encounter histories, supported regional events, and map/room dispatch. Current
event sampling applies audited Hive eligibility plus Glory's Round Tea Party
HP and Grave of the Forgotten deck predicates. Specific event and encounter
documents below may describe earlier fixture-only milestones; the
[regional integration audit](reference-builds/v0.111.0-regional-integration-bags.md)
and [regional integrity plan](REGIONAL_INTEGRITY_PLAN.md) carry current status.
Colorful Philosophers and foreign card pools,
remaining event eligibility, RNG parity, natural full-run victories and live
oracle traces are still open. The source Ancient assignment call order is
audited and partially integrated in the [Ancient assignment ledger](reference-builds/v0.111.0-ancient-assignment.md);
its native UpFront call schedule remains open.
Orobas remains the least complete Hive Ancient; its three-offer generation and
ten relic dependencies are pinned in the
[Orobas dependency audit](reference-builds/v0.111.0-hive-orobas-ancient.md).
Seven Orobas dependencies have executable effects for Silent: Electric
Shrymp/Imbued, Radiant Pearl/Luminesce, Sand Castle, Touch of Orobas/Ring of
the Drake, Archaic Tooth/Suppress, Driftwood's per-group card reroll, and Glass
Eye's five independently selectable rarity-specific card reward groups.
Electric Shrymp now suspends acquisition for one eligible Skill, applies
Imbued to the selected persistent card, and auto-plays it through the opening
turn pipeline in each combat. Radiant Pearl creates a retained, exhaust-on-play
Luminesce in each combat;
Sand Castle upgrades up to six eligible deck instances; Touch replaces Silent's
starter relic and adds two draws through turn three; Archaic Tooth transforms
Neutralize while preserving its card-instance data. Coverage now recognizes
Suppress's base and upgraded effects through that Ancient acquisition path;
it remains outside ordinary card rewards, with source parity unverified.
Alchemical Coffer adds
four slots and generates four distinct potions from the full-unlock
Silent/shared pool in pinned source order. With native run RNG enabled it uses
`run.combat_potion_generation`; Sozu blocks procurement after generation, so
the source draws still occur. Restricted-profile unlock filtering and live
cursor validation remain open. Native-shaped card reward and merchant
generators also preserve the pinned Silent card-pool order, while the legacy
prototype pool remains sorted. All 48 pinned Silent/shared potion IDs have
executable definitions.
Colorless Potion now samples the supported, combat-generatable single-player
Colorless pool, including all 15 epoch-gated cards for the required fully
unlocked profile. Orobic Acid still uses Silent candidates only; its
other-character pools are deferred with cross-character card mechanics.
Native acquisition timing remains open.

Glory Ancient source offers and their current gates are tracked in the
[Glory Ancient ledger](reference-builds/v0.111.0-glory-ancients.md). Signet
Ring is registered as an Ancient relic and grants 888 gold through the shared
newly-acquired-relic trigger. Looming Fruit adds 31 current and maximum HP
through the shared acquisition path. Diamond Diadem grants 20 opening Block
and Blur; coverage follows its Nonupeipe reward and checks turn gating and
melted copies. Sai grants 7 Block at each player-side turn start. Spiked Gauntlets
adds one energy and raises Power-card cost by one. These effects are covered
through reward resolution and combat turns. Crossbow generates a free Attack
at each player-side turn start through the shared generated-card pipeline.
Its source-owned pool order and RNG draws remain unverified. This is a
dependency slice only. Iron Club draws on every fourth card played and stores
its counter in persistent relic state across combats; a combat test verifies
the trigger cadence, saved counter and snapshot restoration. Tanx's offer
pool and event branches are integrated through a named Ancient fixture; the
configured route now also includes Tanx in normal assignment, with its native
assignment RNG still pending.
Throwing Axe repeats the first card played each combat; direct combat coverage
checks the replay and confirms later cards play once.
Meat Cleaver adds Cook to rest sites when at least two removable deck cards
remain. Cooking removes the selected pair and adds 5 current and maximum HP;
tests cover eligibility and the complete rest-site action.
Tanx's Whistle adds an Ancient attack to the deck. Whistle costs 2, deals 33
damage (44 upgraded), exhausts and stuns a surviving target for one turn while
preserving its committed follow-up move. Acquisition, combat, upgrade,
snapshot and stun-resume cases are covered. Tri Boomerang now starts an
acquisition choice for three eligible Attack instances and applies Instinct's
2x powered-attack damage effect. Acquisition eligibility, snapshot restore,
and combat damage are covered. Claws now offers an optional, snapshot-safe
choice of up to six deck transformations into Maul. Maul preserves one upgrade
and enchantments that its card accepts, attacks twice for 5/6 damage per hit,
and each play raises every Maul's damage by 2/3 for the combat. Selection,
cancel-without-transforming, upgrade/enchantment transfer and combat growth
are covered. War Hammer upgrades up to four distinct upgradable deck cards
after Elite victories using run.niche when native RNG ownership is enabled;
native stream selection and snapshot cursor tests are covered. All ten Tanx
relic effects now have executable dependencies. Tanx's conditional offer pool,
native event shuffle, persisted RNG cursor and relic-choice branches are
executable through a named Glory Ancient opening fixture. The configured route
can assign Tanx at act entry; Tanx remains a forced Ancient opening rather than
a weighted map event.
Nonupeipe's conditional offer shuffle and all ten relic branches now execute
through a named Glory Ancient opening fixture; the configured route now
includes it in run-start assignment. Vakuu's three source-owned offer pools and native event shuffle
now run through a named Ancient opening fixture. Lord's Parasol free-purchase
flow reuses existing card, relic and potion acquisition paths, persists its
potion-replacement and forced-removal choices, and is covered by a full shop
entry test. The configured route now includes Vakuu in run-start assignment;
native assignment RNG remains open.
Nonupeipe's Jewelry Box branch now follows its persistent acquisition through
the Innate, Exhaust Apotheosis card; playing Apotheosis upgrades all other
upgradable combat cards. Brilliant Scarf's fifth manual card play now costs
zero energy, using the turn-scoped manual-play counter. Blessed Antler adds
one energy and inserts three Dazed at random draw-pile positions before the
first hand draw; executable coverage checks its Nonupeipe offer and opening
trigger. Glitter applies Glam to eligible cards in normal and extra
combat reward groups; its saved option enchantments survive restore and card
acquisition. Event-owned reward surfaces and native hook order remain gated.
Beautiful Bracelet's four-card Swift acquisition now uses run.niche and its
two-card draw enchant triggers on the first play per combat. Coverage checks
the conditional Nonupeipe offer and four-of-five acquisition. Fiddle adds two
cards to opening and subsequent hand draws and prevents extra draws during its
owner's turn; coverage checks both draw boundaries and off-turn behavior.
Blood-Soaked Rose adds one energy and the Eternal Enthralled curse, which
blocks other manual card plays while it remains in hand; coverage checks its
[Vakuu reward and acquisition trigger](reference-builds/v0.111.0-mechanics-gap-073.md).
Whispering Earring adds one energy and pays to auto-play the first playable
hand cards in order on turn one, up to the source cap of 13; it reuses the
resumable card-play pipeline; coverage checks costs, effects, turn gating and
melted copies. Sere Talon loses 9 Max HP and adds three Wish cards; Wish exhausts
after selecting a draw-pile card into hand, and its upgrade grants Retain.
Preserved Fog removes up to three chosen cards before adding Folly, and Music
Box copies the first Attack each player turn as Ethereal while preserving the
played card's instance data. Their acquisition and turn-scoped behavior have
direct tests. Jeweled Mask moves a preferred non-Innate Power from draw pile
to hand before turn-one draw and makes it free through end of turn; its native
combat path consumes `run.combat_card_selection`. Distinguished Cape adds its
two source-eligible modifier curses and three Apparitions through relic
acquisition, consuming `run.niche` when native RNG ownership is active.
Vakuu's offer event and Lord's Parasol shop flow are implemented through the
named Ancient fixture and normal configured assignment. Fur Coat marks
up to eight current-act Monster/Elite rooms and reduces initial and summoned
enemies there to 1 HP while persisting its map selection and content RNG
snapshot. Delicate Frond now
generates and procures potions before combat until the slots fill, using
`run.combat_potion_generation`; Sozu blocks procurement after one generated
potion. Full native potion-pool and cursor parity remain unverified.

## Delivered

- Imported the existing `prototype/full-run-silent` implementation: all 20
  ordinary Underdocks encounters, ten regional event models, all 53 eligible
  colorless cards, and committed public enemy intents. Event models do not
  establish exhaustive branch or native parity coverage.
- Compiled an identifier-only inventory from both pinned reference caches:
  1,555 eligible entries and 58 multiplayer-only exclusions. All 90 encounters
  include ten special/event encounters outside ordinary act pools. Cards have
  base/upgrade variant IDs; events have known page/option/Ancient IDs. Dynamic
  event branch completeness is explicitly false and requires source auditing.
  Punch Off's two-Construct, Dense Vegetation's four-Wriggler, Hive Lantern
  Key's Mysterious Knight, and all three Battleworn training event fights are
  registered at zero ordinary-map weight and connected to their event choices;
  coverage reports their native event encounter IDs as integrated without
  treating them as map-pool fights.
- Added reproducible coverage by native ID with separate registration,
  implementation, integration and validation dimensions. Metadata registration
  never establishes implementation. Claims derive from typed executable
  registries; all native validation remains unverified. Reports retain gaps.
- Added fork-owned character, A0–A10, profile, route, ruleset, fingerprint and
  fidelity configuration. All five starting definitions and persistent resource
  types are present. Execution initializes Silent only with a fully unlocked
  profile. Native A0 decks omit the legacy Restlessness training addition.
- Added a standalone 15-stream native RNG codec: xxHash64, historical seed
  compatibility, SplitMix64, xoshiro256**, native bounded sampling, float
  rounding, Gaussian sampling and shuffle. Independent vectors and fork/restore
  tests pass. **The engine still uses six prototype streams**; context ownership,
  call order and outcome pool order remain to be integrated.
- Added pinned v111 context-specific RNG constructors for act maps,
  encounter formations, monsters, events and player-owned content, using
  audited run-seed/hash/slot/floor/model-ID formulas. Five independent
  initial-state vectors and fork/restore tests cover the new boundaries.
  The constructors are standalone: native context-stream ownership and
  gameplay call-site integration remain open. See
  [the context RNG audit](reference-builds/v0.111.0-native-context-rng-factories.md).
- Migrated the opt-in Hive/Glory map fixture to the pinned
  independent `act_2_map`/`act_3_map` streams. The native map entry point
  now applies v111 xoshiro bounded draws, Gaussian and Fisher-Yates
  without advancing the persistent run/player or legacy RNG streams,
  and exports the final context snapshot for auditing. The prior
  `Generate(RngBundle, ...)` adapter remains prototype-compatible.
  Fork/restore, multi-seed geometry and stream-isolation tests cover
  the new boundary. Source-shaped map path/pruning/placement call-order
  parity remains unverified; native Hive/Glory event and reward bags stay gated.
  See [later-act map geometry](reference-builds/v0.111.0-hive-glory-map-geometry.md).
- Added the opt-in v111 *encounter-formation ownership* path:
  `NativeEncounterFormationFactoryV111` consumes a native per-encounter
  RNG seeded by native model entry and total floor, returns its advanced
  cursor alongside source-shaped formation, and initializes untouched
  per-creature RNGs from act index, map coordinates and combat IDs.
  `PrototypeNativeLaterActFixture.GenerateEncounterFormation` exercises
  pinned Hive and Glory entries, including distinct Bowlbug worker
  choices. The legacy `ResolveEnemySpecs(RngBundle)` adapter remains
  unchanged, and live `StartCombat` is not yet migrated. Native monster
  HP/AI, encounter-specific generation call order, and full-game
  cross-boundary parity remain open. See
  [the encounter formation audit](reference-builds/v0.111.0-native-encounter-formation-rng.md).
- Added **opt-in native combat RNG ownership**: `CombatState.NativeRng`
  holds the complete 12 run + 3 player streams, the post-formation
  encounter RNG, and separately seeded monster-local streams, all
  deep-forked/serializable/validated. Initial monster HP is sampled
  from `run.niche` in the pinned source's unique-HP order; native-owned
  intent commits now use `run.monster_ai` rather than the shared
  prototype combat RNG. Fast named Hive/Glory combat-entry snapshots
  expose HP, formation, AI intents and all cursor counters without a
  full run or deck. The default `StartCombat` remains legacy;
  source-specific hooks, cross-combat native run-stream history and
  end-to-end RNG parity are still incomplete. See
  [native combat HP/AI RNG](reference-builds/v0.111.0-native-combat-hp-ai-rng.md).
- Added **long-lived native RNG ownership on RunState** as a separate
  15-stream v111 `NativeRunRng` bundle. Configured v111 runs seed it on
  creation and preserve it through run initialization, room decisions,
  fork/JSON snapshots, and replay. Explicit `BeginEncounter`/
  `ReplaceCombat`/`ReleaseCombat` APIs now transfer persistent run
  stream cursors to/from the combat owner; native HP sampling resumes
  from the current run cursor instead of resetting the seed. Engine-step
  reconciliation retains advanced cursors when combat exits.
  `SampleNextInt` provides an atomic, opt-in audit draw in and out of
  combat. Run/combat cursor agreement is enforced by state invariants.
  The prototype engine remains semantically unchanged: full native
  live combat and cross-room RNG call-order parity are still open.
  See [native run RNG persistence](reference-builds/v0.111.0-native-run-rng-persistence.md).
- Added an **explicit opt-in experimental native Act 1 live combat
  entry path** for six source-audited fixed weak encounters (three
  Overgrowth and three Underdocks). These entries use independent
  native encounter RNG ownership, source-pinned native names and map
  coordinates, persistent `run.niche` initial HP draws, and
  `run.monster_ai` intent commitments. Unsupported dynamic weak
  formations such as Slimes/Corpse Slugs and all normal/elite/boss
  encounters reject rather than silently falling back. Default
  configured runs and legacy prototype training remain unchanged.
  Encounter bag selection, map geometry, deck shuffling and many
  remaining combat effects still use prototype RNG and require
  native call-order audits. See
  [the live weak combat entry boundary](reference-builds/v0.111.0-live-weak-combat-entry-rng.md).
- Extended the explicit native live-combat weak-entry path from six
  fixed encounters to **all eight Act 1 weak encounters**. Slimes Weak
  now consumes the exact three encounter-local draws (including the
  singleton pick), and Corpse Slugs Weak consumes one encounter-local
  draw for distinct first-move rotation. Their native encounter stream
  cursors and starter offsets persist through combat and snapshots;
  no duplicate prototype opening draw occurs. Cross-region inputs
  and unsupported normal/elite/boss/event formations still fail
  rather than silently approximate. Default training RNG unchanged.
  See [native Act 1 weak entry](reference-builds/v0.111.0-live-weak-combat-entry-rng.md).
- Expanded the opt-in native combat entry to **22 source-audited
  Act 1 normal encounter formations** (12 Overgrowth, 10 Underdocks),
  in addition to all eight weak encounters. Fixed layouts, native
  slot/order differences, Flyconid/Ruby Raider/Slimes/Strangler random
  selections and encounter-local Corpse Slug/Two-Tailed Rat starter
  rotations now follow pinned `GenerateMonsters()` source logic.
  Initial HP continues from persistent `run.niche`, intent commitment
  uses `run.monster_ai`, and all native RNG cursors survive snapshots
  and turns. Other gameplay and encounter selection still use
  prototype logic; elite/boss/event native paths remain unsupported.
  See [Act 1 normal RNG audit](reference-builds/v0.111.0-native-act1-normal-encounter-rng.md).
- Extended the experimental v111 native combat RNG to **append-only
  monster creation** in both enemy-action summons and death-triggered
  summons. Source run seed / act index / map coordinates persist in the
  encounter formation; every new lifetime CombatId receives a native
  monster-local stream, and new HP comes from advancing `run.niche`
  rather than the legacy prototype combat RNG. Existing/dead monster
  RNG contexts and snapshots remain immutable. Unverified native
  transformations and randomized summon pools are strictly gated;
  summoning strategy and spawn-specific gameplay hooks remain
  prototype-shaped. See [native creature creation RNG](reference-builds/v0.111.0-native-creature-creation-rng.md).
- Source-audited two native Act 1 summon-specific hooks:
  Living Fog now occupies the **first** unoccupied bomb slot, not
  the prototype's last; Gremlin Merc's SurprisePower death **precreates**
  Fat Gremlin before adding Sneaky, producing native CombatId order
  1/3/2 in final turn order, with Fat and Sneaky niche HP rolls in
  source creation order and correct excluded attached-max-HP set.
  Native creature contexts and slots are permuted atomically without
  reseeding. General native transformation remains explicitly gated;
  `ToughEgg.Hatch` is an in-place source-state change rather than
  a species replacement. See [summon hook audit](reference-builds/v0.111.0-native-summon-hooks.md).
- Implemented the first tightly gated **native in-place monster
  transformation**: ToughEgg.Hatch in the isolated Hive
  OvicopterNormal combat. The source keeps the original ToughEgg
  MonsterModel, CombatId and monster RNG while replacing its move
  state with Nibble and rolling new HP directly from `run.niche`
  (19–22 at A0, 20–23 at A8+). The existing prototype Hatchling
  move ID is an explicitly marked proxy; native encounter specs and
  creature-local RNG identities remain unchanged. Non-Minion powers
  are removed, without clearing Block or creating a new monster.
  Tests cover 192 seeds, ascension breakpoints, snapshots and
  invalid in-place proxy mutations. Other native transformations,
  early/spawn hatching and random summon pools remain gated.
  See [native Tough Egg hatch](reference-builds/v0.111.0-native-tough-egg-hatch.md).
- Extended the strictly opt-in live native Act 1 combat entry to **all
  six elites and six bosses** (three each per region and room tier),
  completing the ordinary/elite/boss **42** initial encounter entries
  across Overgrowth and Underdocks (8 weak, 22 normal, 6 elite, 6 boss).
  Source `GenerateMonsters()` shows all twelve elite/boss formations
  are fixed and consume no encounter-local draws. The Kin's creation
  order is two followers (slot1 starts on Dance, slot2 on Quick),
  then the priest (leaderSlot), unlike the legacy prototype model
  order; its conditional AI receives source-equivalent transient
  slot aliases without changing native serialized slots. The entry
  is guarded by room type, pinned map coordinate, region and
  persistent RNG ownership. Combat still uses prototype move/effect,
  reward and encounter-bag logic; actual native elite/boss
  progression, special hooks and reference trace parity remain open.
  See [native elite/boss entry](reference-builds/v0.111.0-native-act1-elite-boss-entry.md).
- Implemented a native **Phrog Parasite elite death-phase** slice:
  source `AfterAddedToRoom` gives four Infested stacks, and its
  `AfterDeath` creates four Wrigglers in source slot order,
  preserving native lifetime IDs and persistent `run.niche` HP
  draws. Each newly created Wriggler carries a real committed
  `SPAWNED_MOVE` / StunIntent with no effect before its native
  odd-slot Bite/even-slot Wriggle opening and alternating cycle,
  not the legacy prototype's invisible skipped turn. Tests cover
  independent source-oracle HP rolls over 144 deaths, full
  map-entry/player-card death and 2-turn replay, source identity
  and explicit unsupported enemy-turn-start poison-death timing.
  Other native elite/boss hooks remain provisional.
  See [Phrog native death phase](reference-builds/v0.111.0-native-phrog-parasite-death-phase.md).
- Source-audited native **Ceremonial Beast PlowPower** and
  **Terror Eel ShriekPower** damage-only threshold interrupts.
  Both `AfterDamageReceived` hooks require positive unblocked
  damage and HP at/below the threshold; raw poison/LoseHp cannot
  stun the boss, even when HP crosses the threshold. Fully
  blocked attacks cannot trigger, and an eventual unblocked hit
  triggers even if HP was already below threshold. The opt-in
  mode now separates direct HP-loss from damage callbacks,
  preserving native Stun intents, power cleanup and RNG isolation.
  Default prototype behavior is intentionally unchanged.
  See [native damage-triggered thresholds](reference-builds/v0.111.0-native-damage-only-boss-thresholds.md).
- Hardened experimental native combat RNG snapshot validation against
  **encounter/monster stream owner swaps**: native encounter and creature
  stream IDs are now checked against entry/floor and native CombatId,
  act and map provenance, with source-compatible optional fields for
  older isolated snapshots. Attachment validates before publishing the
  sidecar. Focused cross-act/JSON/fork and corruption tests establish
  snapshot ownership, not exact game RNG call-order parity.
  See [native snapshot identity](reference-builds/v0.111.0-native-combat-snapshot-identity.md).
- Added exact snapshots, ordered deterministic batches, a Python binding and
  canonical replay. Replay checks legal actions, state and RNG, and reports
  the first divergent JSON path. Synthetic tests do not establish native parity.
- Implemented all ten Trash Heap cards, including Clash, Distraction, Dual Wield,
  Entrench, Hello World, Rebound, Rip and Tear and Stack, plus Hand Drill with
  base/upgrade, identity, choice, generation timing, targeting and Block
  tests. Underdocks shares native-shaped Act 1 shops, rewards, treasures and
  unknown-room resolution. Recovered stolen gold has its own reward group.
  Neow continuations preserve pre-map history. Trash Heap preserves duplicate
  relic rolls as separate instances; duplicate Dream Catchers offer independently
  selectable rest rewards using the shared rarity lifecycle, and fresh Maw Banks
  retain independent purchase state. Fresnel Lens exposes Nimble +2 on
  reward/shop card offer previews using the same acquisition hook, including
  Colorless Fisticuffs' attack-result Block; regressions compare previews with
  acquired card state and its combat Block result.
- Corrected v111 Endless Conveyor continuation ordering: the next dish is
  rolled after Jelly Liver deck transformation or Suspicious Condiment potion
  reward resolution. The visible dish ID/number are now exported to consumers;
  a pending roll remains fork- and snapshot-safe. Punch Off also consumes its
  pinned event-entry dynamic-gold roll (91–98), even when that value is unused
  by its options. The event RNG still uses the prototype codec, so native
  stream parity remains unverified.
- Exposed only already-visible v111 event economics and fixed item rewards in
  optional AI event-choice projections: independent chest rolls, Statue/Conveyor
  outcomes, cost/HP trades, escalation in Abyssal Baths and source-known
  curses, potions and relics. Internal Punch Off RNG rolls stay private.
  Suspended card/potion prompts hide the parent page's option metadata.
- Updated Sword of Stone and Fishing Rod victory callbacks to advance **each**
  owned relic instance separately. Two rods can upgrade two eligible cards
  after their third ordinary combat, while each Sword has its own five-elite
  transformation counter. Fishing Rod still uses prototype event RNG instead
  of the native Niche stream.
- Extended structural validation to the Underdocks multi-page event state
  machine, source-range persisted Treasury/Statue/Punch Off rolls, deferred
  Conveyor selections and native potion-offer slots. This narrows invalid
  states admitted by synthetic snapshots and fixes valid special-event states
  rejected by legacy single-page assumptions.
- Source-audited lethal event actions: pinned solo Overgrowth/Underdocks
  choices are legal even when their HP loss is fatal (native options warn but
  do not lock). Damage-first paths now stop immediately on defeat without
  yielding deferred rewards; Sunken Statue's source-ordered gold-before-damage
  path still credits its gold. Legacy prototype-only event safety gating stays
  unchanged. Regression tests cover Maze, Statue and delayed Whispering Hollow.
- Extended source-backed public event option projections across Overgrowth:
  Dense Vegetation's rolled gold and rest heal, Byrdonis Nest fixed Egg/HP,
  Morphic Grove's entire current gold sacrifice, Sapphire Seed heal,
  Unrest Site's current healing and fixed curse/max-HP sacrifice, Wellspring's
  curse, Luminous Choir's card, and Wood Carvings' fixed transformations.
  All projections derive from visible state and consume no random draws.
- Fixed three source-pinned direct event relic pickups: both variants of
  Sunken Statue grant a fresh Sword of Stone even when already owned, and
  Drowning Beacon grants a second Fresnel Lens. Each retains independent
  persistent state. Nimble is nonstackable in the pinned source, so two
  Fresnel Lenses enchant new eligible cards with Nimble +2 **once**, not
  +4. Random rewards, shops and relic grab bags retain their single-copy
  eligibility restrictions.
- Acquisition-only run callbacks now execute for the **newly acquired relic
  instance**, not all owned copies of the same ID. Verified with duplicate
  Old Coin and Mango; continuous RestSiteHealed triggers still execute for
  every owned Stone Humidifier. This follows native RelicModel.AfterObtained.
- Event UpgradeRandomCard and UpgradeAllCards now use the typed
  upgradability predicate (maximum upgrade level and card state) instead of
  assuming any upgrade-level-zero card is eligible. Added Doors of Light
  and Dark regression with a non-upgradable Injury in the deck.
  Pinned source metadata now explicitly marks Injury, Greed (both ID
  variants), Guilty, Dowsing, Infection and Slimed as max-level zero.
  Native StableShuffle RNG call-order for Doors remains unverified.
- Started the **native later-act content** track without allowing prototype
  substitutions into configured runs. Pinned `Hive.cs` (20) and `Glory.cs`
  (18) ordinary encounter ID catalogues are recorded. A first executable
  slice adds Hive's Bugslayer event and its event-only Exterminate/Squash
  cards, Hive Bowlbug Egg/Nectar worker models, and Glory Devoted Sculptor/
  Turret Operator models. The native Glory Devoted Sculptor weak encounter
  is also available as an explicit test fixture. Act 2/3 random selection
  is **disabled** until complete regional encounter/event policies exist;
  the typed entries must not be mistaken for full playable later acts.
  Source audit and test boundaries:
  [Hive/Glory first slice](reference-builds/v0.111.0-hive-glory-first-slice.md).
- Implemented **all four source-backed Hive weak encounter formations**
  in the typed combat registry: Bowlbugs Weak (Rock plus Egg/Nectar
  roll), Exoskeletons Weak (three distinct opening slots), Tunneler Weak
  (one Burrowed Tunneler), and Thieving Hopper Weak (single thief). Added Rock's
  Imbalanced fully-blocked-attack → committed Dizzy turn → recovery,
  Silk's independent Weak/Thrash sequence, Exoskeleton's slot-based
  move state machine and non-repeating AI, and Hard to Kill's 9-HP
  loss cap per hit. Tunneler's Burrowed now preserves enemy Block
  through enemy-side turns, and a fully broken shield interrupts the
  committed Below intent with Dizzy, then restores the Bite cycle.
  These are explicitly testable and fork-safe, **not yet sampled in
  actual Hive maps**; legacy selector weights remain zero. The
  Thieving Hopper now steals eligible **physical persistent deck cards**
  from combat draw/discard, carrying full upgrade/instance state in Swipe
  until the thief dies and automatically restores its original card,
  or escapes and permanently removes it. Flutter halves powered attacks
  only and stuns on the fifth unblocked hit. The special return is exposed
  as typed reward metadata without giving a duplicate persistent card.
  All four fights remain **gated from real Hive route generation**;
  their legacy weights are zero. Native CombatCardGeneration RNG and exact
  reward UI timing remain unverified; Imbued Skills use the source's lowest
  theft priority class.
  Source details are recorded in the Hive/Glory first-slice audit note.
- Added **four gated Hive normal encounters** with source-backed
  formations and combat moves: Bowlbugs Normal (Rock plus two different
  workers), Exoskeletons Normal (four slots with a stochastic fourth
  opener), Chompers Normal (paired Artifact-2 Chompers with opposite
  starting moves and Dazed creation), and Mytes Normal (paired Mytes
  with distinct initial move states, Toxic-to-hand and Suck Strength).
  Enemy AI Conditional states now permit transitions into Random
  states while preserving precommitted public intents. Added the
  playable one-cost, exhausting Toxic Status (5 damage when left in
  hand at turn end) and bounded enemy-generated hand-card creation.
  All four normal encounters remain **Weight=0 and unavailable in
  real Hive maps** until region generation is implemented.
  Hand-overflow discard behavior and native combat RNG parity are
  marked as provisional rather than silently called faithful.
- Added **two more gated Hive normal encounters**: Spiny Toad
  (Protruding Spikes → Spike Explosion → Tongue Lash, including
  native Thorns damage retaliation and removal) and Slumbering
  Beetle (Rock/Silk/Beetle formation, three-turn Slumber countdown,
  Plating, three unblocked-damage callbacks to wake and Roll Out
  Strength growth). Slumber now decrements from unblocked damage
  callbacks regardless of attack source; raw HP loss such as poison does
  not advance it. Tests cover blocked hits, all three counters and poison.
  Added source-backed enemy power presence/absence AI branches so
  sleep and wake actions are committed before observations and
  persist through forks. Native Hive normal encounters now have
  six guarded entries; native Act 2 map and selection remain disabled.
- Implemented **two further gated Hive normal encounters**:
  Hunter Killer with its source-weighted/nonrepeating Bite/Puncture branch
  and Tender's reversible card-completion Strength/Dexterity penalties;
  Louse Progenitor with fixed Web/Curl/Pounce cycle, Frail and
  Curl Up's first powered-card-hit completion Block trigger (including
  fully blocked hits). Both preserve exact source-pinned A8/A9 numbers,
  occupy independent enemy instances and retain Weight=0 pending
  native Hive region generation.
- Implemented **two more gated Hive normal encounters**: Ovicopter
  (three egg summons with reserved native slots; Tough Egg to Hatchling
  transformations, living-teammate conditional Lay/Paste) and The Obscura
  (Parafright illusion summon, nonrepeating random attacks, Wail buffing
  all living enemies, minion death/revival). Secondary minions have
  persistent instance IDs and no longer block combat victory after
  their primary is defeated. Spawn, hatch and revive transitions have
  focused deterministic, Ascension, fork and edge-case coverage.
  Prototype summon RNG and exceptional visual/slot ordering remain
  explicitly unverified against live game traces. Both Weight=0.
- Three **gated Hive elite first-slice registrations**: Entomancer,
  Infested Prism and the three-part Decimillipede. Source-scaled
  HP, move damage/repetitions, formation slots and starting powers
  are included, with deterministic Entomancer/Prism move cycles.
  PersonalHive now adds Dazed per powered hit with a conditional stack
  cap at 3; VitalSpark taints Skill cards, inflicts Tainted on play,
  adds flat incoming attack damage, and expires after the enemy turn.
  Decimillipede now coordinates its rotational opening moves from a
  single encounter-local roll, assigns distinct even segment HP, and
  performs an untargetable dead turn followed by reattachment for 25 HP
  while another segment remains alive. Dynamic owner-death fatal is
  gated on surviving siblings. Remaining mismatch: death/reattach
  visuals, niche RNG streams, and lack of live-game trace evidence.
  All three Weight=0; native fidelity remains unverified.
- Three **source-pinned Hive boss combat entries** registered at
  `Weight=0`: Kaiser Crab (independently targetable Crusher and Rocket,
  5-move cycles, Surrounded directional damage, once-per-arm Crab Rage
  +6 Strength/+99 Block on the other arm's death), Knowledge Demon
  (attack/heal state machine, finite three-curse schedule, and
  player-owned two-card curse choices with source-defined debuff powers),
  and The Insatiable (Liquify Sandpit countdown, six Frantic Escapes
  split between randomly inserted draw/discard, five-move loop, and
  Frantic Escape increasing Sandpit and its own cost). Source HP,
  Ascension damage breaks and boss formations have synthetic tests.
  Knowledge Demon's mandatory curse selection is a typed, resumable
  enemy-origin prompt with source-defined option sets and debuff effects.
  All three remain gated from generic pools; regional route selection,
  combat RNG parity and live-game validation remain open.
- Two **gated Glory normal encounters** are now source-backed isolated
  combat models: Frog Knight (Plating, Tongue Lash/Frail, Strike,
  Queen buff, one conditional Beetle Charge) and Slimed Berserker
  (Vomit ten Slimed, four-hit Pummeling, Weak/Strength Hug, Smother).
  Enemy AI has fork-owned rolled maximum HP and committed half-HP
  predicates. A0/A8/A9, cycle, identity and fork tests are included.
  Both remain forced-test-only at Weight=0; no native Glory map,
  encounter bag, RNG stream or real-game replay evidence is claimed.
  See [Glory first normal slice](reference-builds/v0.111.0-glory-normal-first-slice.md).
- Added **gated Glory Scrolls of Biting weak and normal formations**:
  one shared encounter roll rotates three distinct opener states
  (Chomp, Chew, More Teeth); a fourth normal scroll always opens at
  More Teeth. Source-backed Scroll of Biting HP/A8/A9, move graph,
  weighted random Chew branch and its Paper Cuts power are modeled.
  Paper Cuts reduces persistent player maximum HP by two after each
  *unblocked powered attack hit*, including individual multi-hits;
  absorbed hits leave max HP unchanged. The max-HP clamp follows
  the pinned LoseMaxHp command. Encounters remain Weight=0,
  synthetic/test-only. See [Glory scroll slice](reference-builds/v0.111.0-glory-scrolls-first-slice.md).
- Completed the **source-backed ordinary weak encounter catalogue**:
  all 15 weak encounter IDs across Overgrowth (4), Underdocks (4),
  Hive (4), and Glory (3) have typed executable registrations.
  Glory's previously missing Turret Operator Weak now pairs a
  55/65-HP Living Shield (Shield Slam while accompanied; permanent
  Smash/+3 Strength when alone) with the existing Turret Operator.
  Its Rampart 25 grants Block to living Turret Operators at each
  player-side start, including the initial turn; each living Shield
  independently contributes, and killing the owner stops future grants.
  Added A0/A8/A9, formation, side-turn timing, kill-order and
  deterministic fork tests. Later-act entries remain Weight=0
  forced-combat fixtures, with native maps, RNG ownership, and real
  replay evidence outstanding. Details:
  [Glory weak closure](reference-builds/v0.111.0-glory-weak-closure.md).
- Added source-pinned **Tunneler Normal** (Hive) and
  **Construct Menagerie Normal** (Glory) encounter formations,
  reusing existing typed Chomper/Tunneler and Punch/Cubex enemies.
  The Chomper is explicitly assigned its source-true screaming
  initial phase; Menagerie has independently targetable Punch
  and two Cubex instances. Construct Menagerie is in the opt-in Glory queue;
  Tunneler Normal remains a forced fixture because it is absent from Hive's
  native encounter roster. Both retain Weight=0 in the generic selector.
  [Normal formation audit](reference-builds/v0.111.0-normal-formations.md).
- Added source-backed **Owl Magistrate Normal** with
  A0/A8/A9 attack and HP deltas, six-hit Peck, four-move loop,
  Judicial Flight's Soar (halves incoming powered attack damage),
  and Verdict's Vulnerable and Soar removal. It is `Weight=0`,
  like the other test-only Glory models. The Soar modifier is
  typed and reusable for future incoming-attack rules.
- Added gated **Globe Head Normal** with native 3-move loop,
  A0/A8/A9 HP and damage, Frail and Strength, plus a source-owned
  Galvanic affliction hook: Power cards present at combat entry
  or newly created in combat become Galvanized, and playing one
  takes 6/8 damage from the living source power. Damage observes
  player Block and owner death; native damage-modifier parity
  remains unverified.
- Added gated **Axebots Normal** as a three-stage, two-respawn
  Stock encounter: the first Axebot opens with Hammer Uppercut,
  each replacement has 10 more maximum HP, loses one Stock,
  and opens with Boot Up (Block plus respawn-scaled Strength).
  Each stage has separate committed intents and A0/A8/A9 HP,
  attack and debuff values. The typed death-summon chain makes
  Stock prevent premature victory. Native animation timing,
  replacement RNG and live replay remain unverified.
- Added gated **The Lost and Forgotten Normal**: two living,
  independently targetable paired enemies that alternate
  Strength/Dexterity theft with source-scaled Eye Lasers/Dread
  attacks. Possess Strength/Speed now tracks the actual player
  stat decrease that survives debuff prevention and restores it
  on the corresponding owner's death, only once. Dread includes
  the Forgotten's current Dexterity before attack modifiers.
  Source numbers, Ascension breaks, state forks and restoration
  are regression-tested; live trace parity is still outstanding.
- Added gated **Fabricator Normal** with four source-backed
  minion types: Zapbot (High Voltage +2 Strength per enemy side),
  Stabbot (attack/Frail), Guardbot (15 Block to each living
  Fabricator), and Noisebot (one Dazed into discard and another
  randomly into draw). Fabricator chooses between defensive+aggressive
  summon and Strike+aggressive summon until four living allies,
  then uses Disintegrate; bots occupy native five-slot formation
  positions. Bot pools exclude the last spawned monster type,
  including dead minions, and use prototype combat RNG rather
  than native MonsterAi; this and native summon animation timing
  remain explicitly unverified. All encounters remain Weight=0.
  The ordinary 41-encounter normal roster now has executable
  registrations, but later-act pool integration is still gated.
- Added two **gated Glory elite combat fixtures**, Soul Nexus and
  Mecha Knight. Soul Nexus begins with Soul Burn, then commits uniformly
  sampled nonrepeating attacks (including four-hit Maelstrom) and
  Vulnerable/Weak Drain Life. Mecha Knight starts with Artifact 3,
  cycles Charge → Flamethrower → Windup → Heavy Cleave, and creates
  four independent, unplayable Burn instances; each held Burn deals
  2 unpowered end-of-turn damage. A0/A8/A9 data and deterministic
  fork, intent, generation and hand-effect regressions are included.
  All encounters remain Weight=0 forced-test-only; native Glory
  selection, MonsterAi RNG ownership and live-game parity are
  unverified. Knights Elite remains outstanding. Evidence:
  [Glory elite slice](reference-builds/v0.111.0-glory-elites-soul-mecha.md).
- Added source-pinned **Knights Elite** and all three **Glory bosses**
  (Queen, Test Subject, Aeonglass) as typed Weight=0 forced-combat
  fixtures, completing the native 3/3 elite and 3/3 boss encounter
  registrations alongside Soul Nexus and Mecha Knight. Knights
  preserves three named bodies, weighted/conditional moves and
  Hex/Dampen powers. Queen models Amalgam-death interrupt and Bound
  cards; Test Subject models two same-instance resurrections,
  Enrage, Wounds and Nemesis; Aeonglass models six-card Withering
  Presence and ever-increasing Wither fake upgrades.
  Per-instance boss phase and status counters survive snapshots
  and forks. A0/A8/A9, formation, ownership, death, hand,
  generation and conditional-intent regressions were added.
  All new encounters remain unintegrated and unverified against
  native traces, with specific parity gaps documented in
  [the source audit](reference-builds/v0.111.0-glory-elites-bosses.md).
  Glory map/encounter selection, elites/boss rewards, native RNG,
  rare power interactions and act terminal parity remain open.
- Live-game Act 1 fidelity capture is currently unavailable to the user;
  this is an external evidence blocker, not a reason to stop content work.
  No current patch establishes new real-game fidelity measurements.
- Added CI pin/inventory/coverage baseline checks and real-server Python
  snapshot/batch/compatibility checks. Added configured decision-boundary
  benchmarks for forks, allocations, stepping, batches, hashing, snapshots and
  retained branch memory; whole-run benchmarks explicitly use the prototype.


- Added the pinned ten-entry Hive event catalogue and an executable,
  deterministic **Infested Automaton** forced-event fixture. The `Study`
  branch draws an implemented character Power; `Touch Core` draws an
  implemented character card whose canonical cost is fixed zero. Both add
  a new persistent card instance, preserve fork/hash determinism and reject
  invalid choices. The native CardFactory uses unlocked character pools,
  noncombat rarity odds, and card-generation RNG: these remain **provisional**
  here because only the supported Silent reward pool and the prototype event
  RNG stream are used. Both Hive event registrations remain Weight=0, with
  map/event integration and live oracle replay outstanding.

- Added two source-backed Hive event fixtures: **Lost Wisp** and **Zen Weaver**.
  Lost Wisp offers its entry-rolled 45–75 Gold or a newly obtained
  Lost Wisp relic plus the unupgradable Decay Curse. Decay inflicts 2
  blockable turn-end damage; Lost Wisp fires 8 unpowered damage on all
  enemies for each played Power card. Zen Weaver requires 125 Gold to
  enter; its fixed 50/125/250-Gold options give two independent
  Enlightenment cards or remove one/two selected cards. Removal costs
  are deferred until after the card-selection continuation.
  Enlightenment limits the current hand's costs to one, preserving any
  lower cost; base expires when played or at turn end, while upgraded
  cards retain the reduced cost until combat end. The added events
  remain forced-event Weight=0 fixtures; native Hive maps, event
  randomness and live-oracle parity are still pending.

- Added source-backed Act 2 Hive forced-event fixtures for **Colossal Flower**
  and **Spirit Grafter**. Flower supports all three digging stages:
  35/75/135 Gold or HP losses of 5/6/7, culminating in Pollinous Core.
  The flower eligibility check requires at least 19 HP. The relic keeps
  a serializable cross-combat hand-draw counter, granting +2 extra
  cards on every fourth hand draw including the opening draw.
  Spirit Grafter offers 25 healing plus Metamorphosis, or a selected
  card upgrade followed by 10 HP loss; the rejection option remains
  legal with no upgradable deck card. Metamorphosis generates 3 (5)
  random eligible Silent Attacks with replacement, inserts each into
  the combat draw pile at a random index, and makes each free for that
  combat. Native unlock pools and exact CardFactory RNG are still
  approximated; real Act 2 event routing and oracle validation remain
  gated.

- Added a source-pinned **Amalgamator** Act 2 Hive forced event fixture.
  Eligibility requires at least two removable Basic Strikes and two
  removable Basic Defends. Both choices filter the actual deck-card
  selection to the matching tag and Basic rarity, removing two
  persistent cards before adding one Ultimate Strike (14/20 damage)
  or Ultimate Defend (11/15 Block). These resulting cards are not
  normal reward-pool cards. Event remains Weight=0 and native selection
  and oracle parity are unverified.

- Added source-backed Hive **Field of Man-Sized Holes** forced event
  (Weight=0), with eligibility requiring an enchantable persistent
  card. Resist selects up to two removable cards, removes them, and
  adds the Normality curse afterward. Enter Your Hole selects one
  enchantable card to gain the persistent Perfect Fit enchantment.
  Normality is unplayable and prohibits further manual card plays
  when held after three plays in a turn (including autoplay). Perfect Fit moves its card
  to the next-drawn position on non-initial draw-pile reshuffles,
  leaving the opening shuffle unchanged. No unimplemented native
  routing or oracle validation is claimed.

- Added gated Hive **The Lantern Key** event: Return grants 100 Gold;
  Keep reveals a forced single-enemy Mysterious Knight fight inheriting
  Flail Knight's moves, Strength 6 and Plating 6. Plating now reuses
  native-shaped initial Block, enemy-end Block and later-side decay.
  Combat victory offers an optional Quest-rarity Lantern Key through
  a resumed event reward screen, with take/skip/leave semantics.
  Glory Unknown-room redirection and War Historian Repy payoffs are
  deliberately pending. Source-backed forced-fixture registration
  is distinct from native Hive pool integration and live oracle parity.
  See [Lantern Key audit](reference-builds/v0.111.0-hive-lantern-key.md).

- Implemented **gated Glory Lantern Key → War Historian Repy**:
  a carried Key forces a Glory Unknown or Event room into the
  quest continuation without enabling unrelated later-act maps.
  The initial choice consumes one Key; in solo runs with further
  Keys the opposite option is offered after rewards, then removes
  all remaining Keys. Cage sets the persistent FreedRepy flag and
  obtains the event-only History Course relic. Chest presents two
  potions and two distinct relics together; each offer can be taken
  or skipped independently, with pre-rolled values and resolution
  state preserved through snapshots. Prototype pool selection and
  native live trace parity remain explicitly unverified.
  History Course now auto-plays the last non-dupe Attack completed on
  the previous turn at the start of turn 2+, with normal targeting,
  zero energy cost, a fork-owned source snapshot and dupe exclusion.
  Melted copies are inactive. Coverage executes the War Historian cage
  reward path and a turn-two Silent Attack replay.
  Exact native CardPlaysFinished history parity remains unverified.
  [Glory quest audit](reference-builds/v0.111.0-glory-repy-quest.md).

- Added **source-shaped native-structure Hive/Glory map geometry** as a
  deterministic opt-in fixture, without enabling incomplete regional
  encounters/events. Hive's map has 14 ordinary rows and boss at 15;
  Glory's has 13 ordinary rows and boss at 14. Both use a fixed first
  combat row, middle treasure row, preboss rest row, 7-column path
  layout and act-specific rest/unknown quotas. New validators check
  topology, floor restrictions, room types and cursor consistency.
  Full-sized Glory unknown rooms holding Lantern Key redirect to
  War Historian Repy. Other later-act room execution is explicitly
  unsupported to prevent prototype fallbacks. Native map stream,
  pruning/repair and generation parity remain open; see
  [later-act map audit](reference-builds/v0.111.0-hive-glory-map-geometry.md).

- Implemented a gated **Glory Hungry for Mushrooms** source slice:
  Big Mushroom grants 20 Max HP and reduces the first combat hand
  draw by two; Fragrant Mushroom deals 15 immediate unpowered,
  unblockable damage before upgrading two distinct upgradable deck
  cards (none on fatal pickup). The event and its two event-only
  relics run on an explicit full-Glory-map fixture, without enabling
  the remaining random regional event bag. Exact native Niche RNG,
  arbitrary relic acquisition hooks and game replay remain partial.
  [Source audit](reference-builds/v0.111.0-glory-hungry-mushrooms.md).

- Added two more **gated source-backed Glory events**, Reflections and
  Round Tea Party, plus Bad Luck (13 unblockable hand-end damage) and
  Royal Poison (4 unblockable HP on the opening turn of each combat).
  Reflections samples two distinct upgraded cards to downgrade, then
  recomputes upgradability and upgrades up to four distinct cards; its
  alternate route duplicates the original deck only, preserving typed
  card payloads before appending one Bad Luck. Round Tea Party's
  fight opens a distinct follow-up page: damage 11 first, then pull
  one relic from the persistent reward bag, or die with no reward.
  Its peaceful option obtains Royal Poison and heals to full.
  The full-map fixtures and fork/phase/callback tests do **not** enable
  incomplete Glory event bags or imply native RNG parity.
  [Source audit](reference-builds/v0.111.0-glory-reflections-tea-party.md).

- Added gated **Glory Grave of the Forgotten**: source-correct
  Confront eligibility, cursed Decay addition before choosing one
  existing Exhaust card for the persistent Souls Power enchantment
  (removing local Exhaust), and Accept acquisition of Forgotten Soul.
  Reuses the existing Hive Decay curse definition and existing
  Forgotten Soul `CardExhausted` randomized unpowered 1-damage hook,
  rather than duplicating source models. Corrected Forgotten Soul's
  erroneous ordinary reward-pool membership: native rarity is Event.
  Executable coverage follows the Act 3 Accept reward and dispatches the
  damage trigger, including the melted-relic case.
  Souls Power's effect applies to both manual and automatic card play.
  Detailed branch, persistence and combat tests remain emulator-only,
  not native RNG/trace parity evidence.
  [Source audit](reference-builds/v0.111.0-glory-grave-of-forgotten.md).

- Added a gated **Glory Trial** fixture with source-shaped Accept/Reject
  and three randomized court cases. All six verdict outcomes now resolve
  their native-shaped curse, gold/healing, distinct relic pickup,
  two-card upgrade/transform prompts or two optional three-card rewards.
  A separate page models the reject-page abandon confirmation without
  silently terminating on the initial click. Trial's Regret, Shame and
  Doubt curses are playable as unplayable hand-end effects: Regret
  deals hand-size unblockable damage; Shame/Doubt apply Frail/Weak
  with their first-duration tick skipped. The scenario RNG and
  reward pools are provisional, and the regional random event bag
  and native replay parity remain unsupported.
  [Source audit](reference-builds/v0.111.0-glory-trial.md).

- Added gated **Glory Battleworn Dummy**, including all three timed
  event fights: inert 75/150/300 HP targets with an enemy-side
  3-turn countdown and explicit timeout escape. Winning gives one
  optional potion, two distinct random deck upgrades, or an optional
  relic from the persistent front grab bag; timing out yields no
  reward. Event combat completion is counted as one original
  event room. The encounter models remain outside random regional
  selection; native RNG, rewards and trace parity are unresolved.
  [Source audit](reference-builds/v0.111.0-glory-battleworn-dummy.md).

- Added gated **Glory Tinker Time**: source-shaped, two-of-three
  selectable card types followed by two-of-three riders from the
  chosen type. The single Mad Science card keeps its type/rider in
  persistent per-instance JSON, and all nine rider effects have
  typed combat execution, including Chaos free-card generation,
  Curious discounts, Improvement postcombat upgrades and existing
  Strangle completion hooks. Tests exercise selection, instance
  identity, invalid choices, all nine effects and deterministic
  forks. Exact native card-generation, event RNG and trigger parity
  remain unverified; region random event routing stays disabled.
  [Source audit](reference-builds/v0.111.0-glory-tinker-mad-science.md).

## Experimental first three-act Silent route slice (opt-in)

`v111-experimental-silent-three-act-route-v1` now permits configured Silent
runs to continue after an Act 1 boss reward onto the native-shaped Hive and
Glory maps, retaining HP/deck/relics/potions, native run RNG streams, legacy
prototype RNG, history, and the run's versioned configuration. The ordinary
v111 fidelity and original AI training modes retain their previous hard gates.

Room entry admits a *restricted source-backed combat roster* in each act:
Hive Tunneler/Exoskeleton weak, Spiny Toad/Mytes normal, Entomancer elite,
Kaiser Crab boss; Glory Devoted Sculptor/Turret Operator weak,
Frog Knight/Slimed Berserker normal, Soul Nexus elite, Queen boss.
The first Unknown in each later act selects one implemented native event
(Infested Automaton or Hungry for Mushrooms), with subsequent Unknowns
explicitly restricted to combat/shop. Native later-act maps, native encounter
identities, full boss/floor progression and existing typed rewards/shops/rest
paths are used; map generation uses the pinned act-local RNG.

**Fidelity warning:** encounter bag, Unknown room distribution, event history,
combat shuffle/AI RNG, later-act rewards/economy and boss mechanics still use
bounded prototype approximations. The slice is intended to enable whole-route
integration, *not* a full native-compatible Silent run, a complete later-act
catalogue, or live oracle parity. Additional unsupported content stays gated;
do not report experimental sample coverage as native pool completeness.

See [the route slice note](reference-builds/v0.111.0-experimental-silent-three-act-route.md)
and `ExperimentalSilentThreeActRouteV111Tests`.

## Broader regional integration (experimental v111 three-act mode)

The runnable configured Silent route now has explicit, fork-owned encounter
bags covering all **20 Hive** and **18 Glory** native weak/normal/elite/boss
registrations, rather than the initial two-per-tier smoke slice.
The pinned native act classes specify **two weak ordinary combats** in both
later acts, followed by normal encounters. Regional bosses are preselected;
normal and elite pools use without-replacement selection and refill.

The route also samples the **9 currently implemented Hive** and **all 7 Glory**
regional event registrations under explicit eligibility and event-history
filters, with no silent substitution of prototype events. Hive's remaining
Colorful Philosophers is cross-character-only and remains gated pending its
other-character card pools. Underdocks already registers all 20 encounters
and 10 regional events; tests now lock those catalogues to their supported
registrations.

Configured later-act Unknown rooms now use the same persistent,
per-act monster/shop/treasure/event pity state as the Overgrowth
native-shaped profile; later-act rewards, shops and treasure use the
shared rarity, potion-pity and relic-grab-bag machinery instead of
the historical prototype pools. This still requires exact native stream
ownership, event/encounter sampling weights and oracle validation.
Glory Battleworn's independently selectable custom event rewards now
initialize proper per-group resolution state.

The larger full-route CI smoke also uncovered and fixed two cross-content
invariants: enemy-owned card afflictions (Galvanized/Tainted) must not be
validated as player-power afflictions, and relics obtained directly from
events must be removed from remaining native relic queues. Neither change
claims real-game source parity for these systems.

The experimental route now represents Glory's Ascension 10+ Double Boss rule:
a distinct second boss is persisted in the preselected encounter history, the
map has a mandatory second boss node, and the first boss reward continues to
that node. A synthetic A10 route completes both boss combats and reaches
victory. Boss-selection draw ownership/order and live reward parity remain
open; see the [double-boss source audit](reference-builds/v0.111.0-glory-double-boss.md).

Eight route integration cases now traverse Act 1 from both Overgrowth and
Underdocks through their generated maps, encounters, events, rewards and boss
boundaries before continuing through Hive and Glory. They cover three A0 seeds
per starting region and one A10 seed per region, including Glory's double boss.
Synthetic combat finishers keep this a progression/persistence check; it does
not establish unassisted victory rates, exhaustive event branches or native RNG
parity.

Underdocks' complete ten-entry normal encounter bag now refills to support the
native twelve-normal history, and its three-entry elite bag refills for longer
elite sequences. Selection avoids an immediate repeat when alternatives remain;
native pre-generated queue order, tag filtering and RNG parity are still open.

**This is expanded implementation access, not completion certification.**
Knowledge Demon's compulsory curse selection now suspends and resumes the
actual enemy turn without auto-selection. Additional later-act
combat/Ancient/reward interactions still need completion, multi-boss
all-seed generated-run sweeps, and native differential verification.
See [regional integration audit](reference-builds/v0.111.0-regional-integration-bags.md).

## Coverage interpretation

Run `dotnet src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll v111-coverage`.
`--missing` lists eligible implementation/integration/validation gaps;
`--check` exits 1 while release coverage is incomplete. That is expected at
this milestone, rather than a regression failure.

The reviewed report is [v111-baseline.json](../data/coverage/v111-baseline.json).
`implemented` for a card means its typed mechanics flag is true; `integrated`
for ordinary cards/encounters means actual supported pool membership. Event
and generated acquisition paths require separate evidence: Silent/shared
potions must be in both the pinned full-unlock order and run pool, while the
Orobas-only Archaic Tooth and Touch paths must execute their actual starter
card/relic transformation. These paths remain native-unverified. Other
categories remain partial until completeness evidence is expressed; zero
counts do not mean no potion/relic/event behavior.

Scope excludes 37 cards, one relic, one rest option and 19 powers whose audited
content-owner reference graph reaches only excluded content. Re-audit that
inference if reflection/dynamic acquisition is added. No proprietary text,
assets, assemblies or source are bundled. Top-level identifier mapping is
complete; exhaustive variant and event branch enumeration is not.

Reproduce with local pinned caches:

```sh
python tools/build_v111_inventory.py /path/to/pinned/eng /path/to/pinned/source --check
python tools/check_v111_coverage.py
```

After reviewing intentional changes, regenerate the inventory and run
`python tools/check_v111_coverage.py --update`; commit the reviewed baseline
diff. Native parity claims require a real reference fixture ledger.

## Remaining gates

1. Exhaustive event branches, Ancient options, acquisition paths, profiles and
   evidence-ledger coverage for every variant.
2. Remaining Act 1 mechanics and native validation; exhaustive event outcomes
   and reward modifiers remain unaudited.
3. Native Hive/Glory encounters, maps, events, Ancients, transfers and terminal
   rules. Configured transitions reject unrelated prototype acts.
4. Ironclad, Defect, Regent and Necrobinder execution/content; all remaining
   shared/generated cards, modifications, potions and relics.
5. Native RNG ownership/context generators, capture normalization and complete
   cross-character/act/difficulty replay. The codec alone does not provide parity.
6. At least 1,000 pinned complete/terminal real-game runs without unexplained
   divergence, plus rare-content fixtures and performance budgets.

The workspace has neither the pinned game installation nor a real-game trace
corpus. Source-backed implementation and synthetic validation can proceed,
but those native release gates cannot be certified from the available files.

## Knowledge Demon enemy-side choice (v111 opt-in route)

The mandatory Curse of Knowledge is now executable across all three
source-scripted choices. The choice is explicit, forkable, serializable,
and resumed at the original committed enemy action; Disintegration,
Mind Rot, Sloth and Waste Away have source-shaped immediate debuff
semantics. No choice is silently selected and choice-only status
cards do not enter normal rewards. See
[the source audit](reference-builds/v0.111.0-knowledge-demon-player-choices.md).
RNG/call-order parity with the real game remains unverified.

## Silent relic: Twisted Funnel

Added the pinned Silent uncommon **Twisted Funnel** to the ordinary relic
pool. It applies 4 Poison to every living enemy at combat start, matching the
source's first-player-turn application at the mechanics level. The combat
event ordering and native trigger timing remain for the later fidelity pass.
Source: [TwistedFunnel.cs](https://github.com/Flaaax/STS2-source/blob/706ef1c9fa2219220065849fd5265328607ece20/src/Core/Models/Relics/TwistedFunnel.cs).
Added the Silent shop relic **Ninja Scroll** to the shop rarity pool; before
the opening hand draw on turn one, it creates three Shivs in hand. Source:
[NinjaScroll.cs](https://github.com/Flaaax/STS2-source/blob/706ef1c9fa2219220065849fd5265328607ece20/src/Core/Models/Relics/NinjaScroll.cs).
Added the Silent rare relic **Helical Dart** to the rare relic pool. Each
played Shiv grants 1 temporary Dexterity for the current player turn, using
the card tag in the shared relic trigger pipeline. Source:
[HelicalDart.cs](https://github.com/Flaaax/STS2-source/blob/706ef1c9fa2219220065849fd5265328607ece20/src/Core/Models/Relics/HelicalDart.cs)
and [HelicalDartPower.cs](https://github.com/Flaaax/STS2-source/blob/706ef1c9fa2219220065849fd5265328607ece20/src/Core/Models/Powers/HelicalDartPower.cs).
Added **Snecko Skull** to the common relic pool. Each positive Poison
application the player gives to an enemy gains one additional stack, through
the shared enemy-status application path. Source:
[SneckoSkull.cs](https://github.com/Flaaax/STS2-source/blob/706ef1c9fa2219220065849fd5265328607ece20/src/Core/Models/Relics/SneckoSkull.cs).
Added the Silent rare relic **Paper Krane**. Weak enemy attacks against the
player use a 60% damage multiplier (the base Weak multiplier is 75%); Weak
does not reduce enemy non-attack damage. Source:
[PaperKrane.cs](https://github.com/Flaaax/STS2-source/blob/706ef1c9fa2219220065849fd5265328607ece20/src/Core/Models/Relics/PaperKrane.cs).
Tingsha and Tough Bandages now require a player-side discard, matching their
native owner/current-side guard. End-of-turn hand cleanup now dispatches
discard hooks before player hand-cleanup powers expire. Tingsha deals 3
unpowered damage to a random living enemy; Tough Bandages grants 3 Block. Both
remain in their native Uncommon/Rare reward pools. Sources:
[Tingsha.cs](https://github.com/Flaaax/STS2-source/blob/706ef1c9fa2219220065849fd5265328607ece20/src/Core/Models/Relics/Tingsha.cs),
[ToughBandages.cs](https://github.com/Flaaax/STS2-source/blob/706ef1c9fa2219220065849fd5265328607ece20/src/Core/Models/Relics/ToughBandages.cs).
Pen Nib is now an Uncommon shared relic. Its saved counter wraps every ten
owner-played Attacks, and the tenth Attack deals double powered damage. See
[mechanics gap 109](reference-builds/v0.111.0-mechanics-gap-109.md).
Gnarled Hammer is now a Shop relic. It offers up to three eligible Attacks
for Sharp +3 at acquisition, with optional completion. See
[mechanics gap 110](reference-builds/v0.111.0-mechanics-gap-110.md).
Kusarigama is now an Uncommon shared relic. Every third owner-played Attack
deals 6 unpowered damage to a random living enemy, then the counter resets
next turn. See
[mechanics gap 111](reference-builds/v0.111.0-mechanics-gap-111.md).
Miniature Cannon is now an Uncommon shared relic. It adds 3 damage to powered
Attacks from upgraded cards. See
[mechanics gap 112](reference-builds/v0.111.0-mechanics-gap-112.md).
Belt Buckle is now a Shop relic, granting 2 Dexterity when its owner has no
potions at combat start or after using the last one. The emulator has no
mid-combat potion procurement or discard action. See
[mechanics gap 113](reference-builds/v0.111.0-mechanics-gap-113.md).
Golden Pearl's Neow pickup now has executable coverage for its +150 Gold
acquisition callback. See
[mechanics gap 114](reference-builds/v0.111.0-mechanics-gap-114.md).
Cursed Pearl's Neow pickup now has executable coverage for adding Greed and
granting 333 Gold. See
[mechanics gap 115](reference-builds/v0.111.0-mechanics-gap-115.md).
Neow's Torment's Neow pickup now has executable coverage for adding Neow's
Fury to the deck. See
[mechanics gap 116](reference-builds/v0.111.0-mechanics-gap-116.md).
Precise Scissors' Neow pickup now has executable coverage for removing one
selected card from the deck. See
[mechanics gap 117](reference-builds/v0.111.0-mechanics-gap-117.md).
Arcane Scroll's Neow pickup now has executable coverage for adding one Rare
card from the supported Silent reward pool. Native card-pool ordering and RNG
parity remain unverified. See
[mechanics gap 118](reference-builds/v0.111.0-mechanics-gap-118.md).
Book of Five Rings is now included in the Common relic pool and heals 20 after
each fifth card added to the deck, with its saved counter preserved between
grants. The native Glory relic drawer applies its source cutoff at floor 7,
the Act 3 treasure chest. See
[mechanics gap 119](reference-builds/v0.111.0-mechanics-gap-119.md).
Bowler Hat is now included in the Uncommon relic pool, is excluded from shops,
and multiplies positive owner gold gains by 1.25. The native Glory relic
drawer applies its source cutoff at floor 7. See
[mechanics gap 120](reference-builds/v0.111.0-mechanics-gap-120.md).
Dragon Fruit is now a Shop relic and grants 1 Max HP, including current HP,
after each positive owner gold gain. The native Glory relic drawer applies
its source cutoff at floor 7. See
[mechanics gap 121](reference-builds/v0.111.0-mechanics-gap-121.md).
Screaming Flagon is now a Shop relic and deals 20 unpowered damage to all
enemies before the owner's turn ends when their hand is empty. See
[mechanics gap 122](reference-builds/v0.111.0-mechanics-gap-122.md).
Sparkling Rouge is now an Uncommon relic and grants its owner 1 Strength and
1 Dexterity at the start of turn three. See
[mechanics gap 123](reference-builds/v0.111.0-mechanics-gap-123.md).
Permafrost is now an Uncommon relic and grants 7 Block after the first Power
card played in each combat. See
[mechanics gap 124](reference-builds/v0.111.0-mechanics-gap-124.md).
Parrying Shield is now an Uncommon relic and deals 6 damage to one random
enemy at turn end when its owner has at least 10 Block. See
[mechanics gap 125](reference-builds/v0.111.0-mechanics-gap-125.md).
Vexing Puzzlebox is now a Rare relic and adds one random Silent card to its
owner's first-turn hand, free for that turn. See
[mechanics gap 126](reference-builds/v0.111.0-mechanics-gap-126.md).
Pendulum is now a Common relic and draws one extra card every third hand draw,
keeping its counter between combats. See
[mechanics gap 127](reference-builds/v0.111.0-mechanics-gap-127.md).
Razor Tooth is now a Rare relic and upgrades each owner-played Attack or Skill,
updating both its combat instance and persistent deck card. See
[mechanics gap 128](reference-builds/v0.111.0-mechanics-gap-128.md).
Ripple Basin is now an Uncommon relic and grants 4 Block at the end of its
owner's side turn when they played no Attacks that turn. See
[mechanics gap 129](reference-builds/v0.111.0-mechanics-gap-129.md).
Intimidating Helmet is now a Rare relic and grants 4 Block before its owner
plays a card that spends at least 2 Energy. See
[mechanics gap 130](reference-builds/v0.111.0-mechanics-gap-130.md).
Kifuda is now a Shop relic and requires its owner to enchant three deck cards
with Adroit, which grants 3 Block each time an enchanted card is played. See
[mechanics gap 131](reference-builds/v0.111.0-mechanics-gap-131.md).
Punch Dagger is now a Shop relic that enchants one Attack with Momentum. Each
play adds 5 damage to the next powered attack from that card for the rest of
combat. See [mechanics gap 132](reference-builds/v0.111.0-mechanics-gap-132.md).
Perfect Fit is now executable in Hive's Field of Man-Sized Holes event and
moves its enchanted card to the top after a noninitial draw-pile shuffle. See
[mechanics gap 133](reference-builds/v0.111.0-mechanics-gap-133.md).
Dolly's Mirror is now a Shop relic that copies one non-Quest deck card while
preserving its upgrade, enchantment, and persistent state. See
[mechanics gap 134](reference-builds/v0.111.0-mechanics-gap-134.md).
Swift now has executable coverage through Glory's Beautiful Bracelet: the
first play draws two cards, and later plays in that combat do not draw again.
See [mechanics gap 135](reference-builds/v0.111.0-mechanics-gap-135.md).
Planisphere now heals 5 HP on the run's first Unknown map point and stops
appearing at the Act 3 treasure chest. See
[mechanics gap 136](reference-builds/v0.111.0-mechanics-gap-136.md).
Shiv now has executable integration coverage as a generated token: Blade Dance
creates three or four Shivs, while base and upgraded Storm of Steel create
matching Shiv variants after discarding the rest of the hand. Shiv stays out
of ordinary reward pools. See
[mechanics gap 137](reference-builds/v0.111.0-mechanics-gap-137.md).
