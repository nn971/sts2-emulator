Warning: truncated output (original token count: 147611)
Total output lines: 2988

# Unified stochastic architecture — implementation roadmap

**Recorded:** 2026-10-16. **Status:** proposed implementation milestones, **not completed work**. **Authority:** [Unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md).

**S0 source-site expression resolution increment (2026-10-23):** The direct
source inventory follows type-scoped `const string` aliases while expanding
composed registry properties, resolving the existing Tablet of Truth and
Trash Heap session sites to authoritative registry entries without duplicate
IDs. It also resolves bare type constants in typed requests and finite values
from the guarded pool-shuffle site switch. The generated direct-call census
reports 185 registered sites for 185 migration rows, with no registry
mismatches or unresolved references; 104 of 111 typed requests resolve to a
registry site, while seven forwarded runtime expressions remain dynamic.
The inventory schema is v43; static source analysis only, with no tests or
builds run.

**S2/S4 Gambler's Brew selection increment (2026-10-23):** Gambler's Brew
now uses the existing sequential card-selection schema and semantic draw
owner. The admission checks the source potion-use completion event, hand to
discard operation, selected-prefix count, and absent outer continuation;
the source draw count equals the number discarded. No event or snapshot
schema was added. Static source review and `git diff --check` only; no tests
or builds were run.

**S2/S4 Colorless Potion source-action increment (2026-10-23):** The
persistent semantic pilot now accepts Colorless Potion from any matching
combat slot. Its source guard checks the three-card generated-Colorless
operation, `PotionUsed` completion, and empty nested continuation, then reuses
the existing full-pool shuffle and optional ChoicePool selection owners.
Fixture setup remains available. Native acquisition, pool fidelity, and
cursor parity remain open. Static source review and `git diff --check` only;
no tests or builds were run.

**S2/S4 Anointed source-card increment (2026-10-23):** The persistent
semantic pilot now admits Anointed's Rare draw-pile shuffle by its physical
source-card identity and exact amount-ten operation, `CardPlayed` completion,
and saved card-play frame. The existing full-pool shuffle owner and stable
card-instance support remain unchanged. Fixture setup is optional for
supplying Anointed and a Rare pile; native acquisition, pool completeness, and
cursor parity remain open. Static source review and `git diff --check` only;
no tests or builds were run.

**S2/S4 Seeker Strike source-card increment (2026-10-23):** The persistent
semantic pilot now admits Seeker Strike's draw-pile shortlist through the
physical source-card identity and exact one-of-three `ChooseCards` operation.
It reuses the existing full-pool shuffle and player-selection events. Fixture
setup remains optional. Draw-pile removal support split by known anchors
remains fail-closed; native acquisition and cursor parity remain open. Static
source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Alchemize source-card increment (2026-10-23):** The persistent
semantic pilot now admits Alchemize's potion-generation action by its physical
source-card identity and exact amount-one `AcquireRandomCombatPotion`
operation, `CardPlayed` completion, and saved manual card-play frame. It
reuses the existing potion-generation event and resolver. Fixture setup
remains optional; native acquisition, pool completeness, and cursor parity
remain open. Static source review and `git diff --check` only; no tests or
builds were run.

**S2/S4 Discovery source-card increment (2026-10-23):** The persistent
semantic pilot now admits Discovery's optional three-card generated offer by
its physical source-card identity, exact generated-card operation, and saved
`CardPlayed` continuation. It reuses the existing full-pool shuffle and
ChoicePool selection owners. Fixture setup remains optional; native
acquisition, generated-pool completeness, and cursor parity remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Splash source-card increment (2026-10-23):** The persistent semantic
pilot now admits Splash's optional three-Attack offer by its physical source
card identity, exact generated-Attack operation, and upgrade state. It reuses
the existing full-pool shuffle and ChoicePool selection owners. Fixture setup
remains optional; generated-pool completeness and cursor parity remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Jackpot source-card increment (2026-10-23):** The persistent semantic
pilot now admits Jackpot's three random zero-cost card additions from its
physical source-card identity. It reuses the existing generated-card event and
validates each `CardPlayed` continuation, source upgrade level, and remaining
generation suffix. Fixture setup remains optional; generated-pool completeness
and cursor parity remain open. Static source review and `git diff --check`
only; no tests or builds were run.

**S2/S4 Volley target-action increment (2026-10-23):** The persistent semantic
pilot now admits Volley by physical source-card identity. Each paid X hit uses
the existing random-target event and exact remaining damage-operation suffix.
Native target-pool and cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S2/S4 Jack of All Trades source-card increment (2026-10-23):** The
persistent semantic pilot admits its distinct Colorless-card generation from
the physical source-card identity and exact amount-one-or-upgraded operation.
It reuses the existing full-pool shuffle owner. Fixture setup remains optional;
pool completeness and cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S2/S4 Colorless selection-card increment (2026-10-23):** Secret Technique,
Secret Weapon, Purity, and Scrawl now enter the existing semantic selection
owner from their physical source cards. Supported draw-pile moves retain the
known or contiguous-pool checks. Thinking Ahead resumes its draw and top-place
selection through the existing draw and selection owners. No event or snapshot
schema changed. Static source review and `git diff --check` only; no tests or
builds were run.

**S2/S4 Neow's Abundance choice increment (2026-10-23):** The persistent
semantic combat pilot admits Abundance's generated-Power choice through the
existing full-pool shuffle and ChoicePool selection owners. The shuffle guard
matches the exact source card instance and required upgraded-Power operation;
the hidden draw pile remains unchanged and no event or snapshot schema was
added. Native acquisition and cursor parity remain open. Static source review
and `git diff --check` only; no tests or builds were run.

**S2/S4 source-generated potion increment (2026-10-23):** Orobic Acid now
enters the persistent semantic pilot by its source potion identity without
requiring a seeded-fixture marker. Its three ordered Attack, Skill, and Power
generated-card model events reuse the existing source pool and replay owners;
the hidden draw pile remains unchanged. Colorless Potion retains its prior
fixture-backed shuffle and optional ChoicePool pick. No event or snapshot
schema was added. Static source review and `git diff --check` only; no tests
or builds were run.

**S2/S4 generated-card potion increment (2026-10-23):** Attack, Skill, and
Power Potions now enter the persistent semantic pilot through the existing
selection-potion path. Their source full-pool shuffle, generated-card model
choice, and ChoicePool selection reuse current event and replay owners; the
hidden draw pile remains unchanged. No event or snapshot schema was added.
Other potion effects remain open. Static source review and `git diff --check`
only; no tests or builds were run.

**S2/S4 semantic source card-selection increment (2026-10-23):** The
persistent semantic combat pilot now admits Neow's Fury's optional
discard-pile-to-hand card choice through the existing semantic card-selection
event and replay owner. The action preserves the provider-owned hidden draw
pile; no choice or snapshot schema was added. Other card effects remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 semantic card-effect draw increment (2026-10-23):** The persistent
semantic combat pilot now admits Restlessness, Slimed, and Calculated Gamble
through its existing semantic draw owner. Restlessness keeps its source
hand-empty enqueue condition; Calculated Gamble keeps its queued draw count
while the preceding discard operation runs. No chance-event or snapshot schema
was added. Other card-effect and opening draws remain open. Static source
review and `git diff --check` only; no tests or builds were run.

**S2/S4 semantic draw-potion action increment (2026-10-23):** The persistent
semantic combat pilot now admits modeled direct-draw actions for Glowwater,
Clarity, Cure All, Snecko Oil, and Swift. Each suspended card identity reuses
the existing provider-owned semantic draw event and nested effect continuation;
Snecko Oil's later hand-cost randomization remains with the existing cost
owner. Unsupported potion definitions still fail closed, and the extension
adds no chance-event or snapshot schema. Static source review and
`git diff --check` only; no tests or builds were run.

**S6 combat card-pool shuffle delegation increment (2026-10-23):** The
run-owned `SemanticOtherChanceStateV111` now resolves saved full-pool swaps
for generated cards through `run.combat_card_generation`, and Anointed Rare
card and random-shortlist swaps through `run.combat_card_selection`. Each
result resumes the same permutation continuation without a Nature history
choice. The existing per-swap Chance Laws remain exact only over their
declared bounds; source pool, stream attribution, and cursor parity remain
open. Static source review and `git diff --check` only; no tests or builds
were run.

**S6 random replay/autoplay selection delegation increment (2026-10-23):**
The run-owned provider now resolves saved random replay-card and autoplay
card-instance selections through the registered
`run.combat_card_selection` `choose_index` boundary. The existing saved
shortlist records the selected identity and resumes without adding a Nature
history choice. Eligibility and stream/cursor parity remain open. Static
source review and `git diff --check` only; no tests or builds were run.

**S6 semantic draw-insertion delegation increment (2026-10-23):** Saved
generated-card, after-shuffle relic, and Personal Hive insertion events that
carry their Native `run.shuffle` stream owner now route through the registered
bounded-index boundary and resume the original insertion frame without a
Nature history choice. The event's source owner and current pile support
remain authoritative for applying the chosen position. Native-game stream
attribution, pile completeness, and cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S6 hand-return shuffle delegation increment (2026-10-23):** Bottled
Potential's saved hand-return swaps now delegate through the registered Native
`run.shuffle` boundary when their event carries that stream owner. The event
preserves the source operation's owner prefix and full pool fingerprint, then
resumes the existing shuffle and draw continuation without a Nature history
choice. Native-game stream attribution, pool completeness, and cursor parity
remain open. Static source review and `git diff --check` only; no tests or
builds were run.

**S6 saved enemy-action Native delegation increment (2026-10-23):** The
semantic end-turn frame now delegates any suspended enemy-action request whose
exact site/stream/operation pair is registered, covering enemy draw/discard
insertions on `run.shuffle` and supported summoned-creature or isolated Tough
Egg HP requests on `run.niche`. The existing saved action replay applies the
selected outcome without a Nature history choice. Native route, pool and
cursor parity remain open; unsupported summon pools and nested effects remain
fail-closed. Static source review and `git diff --check` only; no tests or
builds were run.

**S6 Native initial-HP per-index law increment (2026-10-23):** The
`combat.initial_unique_hp` Chance Law query now binds a `creature:<CombatId>`
owner, ordered HP support, and source ordinal to `run.niche`, returning exact
Native `NextInt` grid masses over support indices. The caller declares the
eligible HP support and order; pool construction, seed frequencies, and
end-to-end stream/cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S6 Native Tough Egg per-index law increment (2026-10-23):** The direct
`combat.tough_egg.hatchling_hp` query now binds a canonical creature owner,
Ascension, and source ordinal zero, deriving the pinned 19–22 or 20–23 HP support
before applying the exact `run.niche` `NextInt` grid law. Event eligibility
and the saved in-place hatch boundary remain with the caller. Static source
review and `git diff --check` only; no tests or builds were run.

**D2 Native initial-HP result audit increment (2026-10-23):** The source-route
Native-policy entry audit now pairs each setup `run.niche` HP ticket with its
combat identity, ordered HP support, and selected HP. New encounter-formation
schema V2 requires one ordered outcome per saved formation context and checks
each against the mixed Native trace and source-route combat origin; V1 and
unmarked snapshots keep their previous rules. This records emulator output,
not installed-game parity or pool eligibility. Static source review and
`git diff --check` only; no tests or builds were run.

**D2 Native encounter-formation result audit increment (2026-10-23):** The
source-route Native-policy baseline now retains the existing
`NativeEncounterFormationAuditV111`, pairing the selected formation with its
typed encounter-index decisions. The schema checks every choice against the
mixed Native trace, and source-route validation checks the formation against
the combat origin. Older unmarked audits remain readable. Source-pool
eligibility and native result/cursor parity remain open. Static source review
and `git diff --check` only; no tests or builds were run.

**D2 Native pool-shuffle outcome audit increment (2026-10-23):** Native combat
card-pool Fisher–Yates swaps now annotate the ordered card support immediately
before each ticket by reusing `SemanticPoolShuffleEventV111`. The Native-policy
audit links every annotation to its exact provider request and selected index,
checks the descending permutation chain, and marks new snapshots as requiring
complete coverage for registered pool-shuffle sites. Source-pool eligibility,
Native stream attribution, and cursor parity remain open. Static source review
and `git diff --check` only; no tests or builds were run.

**D2 Native generated-card model outcome audit increment (2026-10-23):**
Native generated-card model requests now retain the selected model and ordered
source pool through `SemanticGeneratedCardEventV111`, paired with the exact
Native request, cursor span, source operation and source card identity. New
Native-policy audits require one typed result for each supported generated-card
selection request. Pool completeness, stream attribution and cursor parity
remain open. Static source review and `git diff --check` only; no tests or
builds were run.

**D2 Native card-transform outcome audit increment (2026-10-23):** Native
transform replacement tickets now retain the original model, ordered
replacement pool, selected model, source instance and exact Native cursor span
through `SemanticCardTransformEventV111`. New Native-policy audits require one
typed outcome for each transform replacement request. Transform pool
completeness, stream attribution and cursor parity remain open. Static source
review and `git diff --check` only; no tests or builds were run.

**D2 Native random-target outcome audit increment (2026-10-23):** The shared
Native random-target resolver now reuses `SemanticTargetEventV111` and
`SemanticTargetDecisionV111` to retain the ordered enemy-instance support and
selected identity beside the exact provider decision. Newly marked Native-
policy audits require one result for every registered random-target request;
older unmarked snapshots remain readable. Source eligibility, stream
attribution, and result/cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**D2 Native combat-potion outcome audit increment (2026-10-23):** The
in-combat random-potion model resolver now reuses
`SemanticPotionGenerationEventV111` and `SemanticPotionGenerationDecisionV111`
to retain its ordered potion pool and selected model beside the exact Native
decision. Newly marked Native-policy audits require complete coverage for
this combat site across setup and player actions. Pre-combat PotionFactory
requests use separate sites. Potion-pool eligibility, stream attribution,
and result/cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**D2 Jeweled Mask Native card-identity audit increment (2026-10-23):** The
existing card-instance outcome audit now also covers Jeweled Mask's Native
random Power draw. Its ordered eligible card identities and selected instance
are paired with the exact provider decision, and the Native-policy card
identity schema requires complete coverage for the added site. Mechanics
regression, pool completeness, stream attribution, and cursor parity remain
open. Static source review and `git diff --check` only; no tests or builds
were run.

**S0 stochastic SiteId ownership consolidation (2026-10-23):** The registry
now owns the identifiers for random hand-cost selection and resolution,
draw-cost choices, potion generation, card transforms, and the semantic
shortlist, pool-shuffle, and enemy-choice families. Semantic choice owners,
registry coverage and control checks, and gameplay request callsites all
reference those constants instead of maintaining duplicate literals.
Static source review and `git diff --check` only; no tests or builds were run.

**S0 enemy-theft site review increment (2026-10-23):** The scoped manual
inventory now traces persistent-card support, theft-priority ordering, the
Nature semantic-deck removal guard, enemy-action replay, and atomic zone/deck
commit for `combat.enemy.theft_card.select`. Its stable SiteId now comes from
the shared registry at request, continuation, and capability boundaries.
Native stream attribution, source-pool fidelity, legacy law, and cursor parity
remain open. Static source review and `git diff --check` only; no tests or
builds were run.

**S0 Endless Conveyor manual site review increment (2026-10-23):** The scoped
inventory now traces the dish ticket, Observe/Spicy Snappy upgrade, Fried Eel
Colorless draw, and Suspicious Condiment potion boundary through their existing
Chance Laws and atomic session. It records deferred next-dish chronology and
the legacy fallback limits. The inventory's census references now match v41.
Static source review and `git diff --check` only; no tests or builds were run.

**S0 Stone of All Time manual site review increment (2026-10-23):** The
occupied-slot entry law and push/lift ticket continuation are now traced
together in the scoped inventory. It records the intervening push deck choice,
the no-effect ticket mapping, the lethal early exit, and the configured,
retained-legacy, and Native-regional fallback boundaries. Native cursor parity
and legacy modulo probability remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S0 Future of Potions manual site review increment (2026-10-23):** The
ordered potion-slot card-type prefix now has a scoped inventory trace through
its Native law, saved event outcomes, and existing atomic Native event-entry
session. The shared event-context contract now points to that session instead
of implying none exists; its Native-regional legacy weights remain
`UNVERIFIED`. Static source review and `git diff --check` only; no tests or
builds were run.

**S0 generated-gold manual site review increment (2026-10-23):** The two
event-local gold sites now trace supported Native index and unit-float
branches, the shared event-entry replay, and the legacy index fallback. Their
runtime callsites use the existing registry-owned SiteIds. Native cursor
parity, event-pool completeness, and legacy equal-index weights remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S0 Trial, Tablet of Truth, and Trash Heap manual review increment
(2026-10-23):** Four event-choice site IDs now have scoped source traces for
their ordered case, eligible-card, and fixed reward supports, draw-free
branches, Nature/player projections, and existing atomic sessions. Their
runtime callers and session registration use registry-owned SiteIds. Native
cursor parity and source pool limits remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S0 Neow offer manual site review increment (2026-10-23):** The five initial
offer-generation sites now trace the cursed choice, conditional relic pairs,
single-player filtering, and ordered positive-pool shuffle through the
existing Native generator and atomic ReferenceNature session. Native unlock
filtering, source call order, and cursor parity remain open. Static source
review and `git diff --check` only; no tests or builds were run.

**S0 Reflections manual site review increment (2026-10-23):** The two
Touch a Mirror card-pick sites now trace the ordered downgrade and recomputed
upgrade supports through the existing atomic ReferenceNature continuation.
Source filtering, fallback weighting, and Native cursor parity remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S0 Punch Off manual site review increment (2026-10-23):** The fight-entry
and post-victory reward sites now trace their CombatId-owned offsets, ordered
relic/potion prefix, session boundaries, and audience projections. The pinned
source audit now matches the current Chance Law provenance: bounded-index
transforms are source verified on declared supports, while Native context and
reward stream attribution, pool fidelity, and cursor parity remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S0 Native event shuffle manual review increment (2026-10-23):** Six sites
now trace Fake Merchant stock, Fragrant Mushroom upgrades, Battleworn setting
2 upgrades, both Tinker offer stages, and Doors of Light through the existing
conditional shuffle session. The Fragrant Mushroom source note now reflects
its Native event-context path and current legacy fallback. Source pools,
event-call parity, and seeded Native cursor parity remain open. Static source
review and `git diff --check` only; no tests or builds were run.

**S0 summon-pool SiteId ownership increment (2026-10-23):** The existing
legacy summon continuation and query registry now use the shared
`combat.enemy.summon_pool.select` constant. Its Native pool path continues to
fail closed pending source RNG assignment evidence. Static source review and
`git diff --check` only; no tests or builds were run.

**S0 Native request-wrapper callsite closure increment (2026-10-23):** The
direct-call census now resolves the three generic request constructors in
`NativeRandomChoiceV111` through their operation-matched Native player-choice
and direct helper callsites. The generated inventory maps 104 of 104 typed
request constructors to registered sites, 39 of 39 helper callsites to
migration rows, and keeps the external-caller boundary explicit. Registry,
contract-link, ambient-entropy census checks and `git diff --check` passed;
no tests or builds were run.

**D3/D4 event-reward action continuation increment (2026-10-23):** The
existing controlled event-reward frame now saves a legal Driftwood
`reroll_reward_card` action before its first request and replays it through
the `reward.card_rarity.ticket` and `reward.card_model.index_draw` sites. The
same frame retains the selected prefix across additional reroll requests,
snapshots, and ReferenceNature session replay; other stochastic event-reward
actions still fail closed. Static source review and `git diff --check` only;
no tests or builds were run.

**D3/D4 combat-reward relic action continuation increment (2026-10-23):** The
existing controlled run-action frame now also saves legal legacy
`take_reward_relic` and `take_reward_extra_relic` actions from Combat, Elite,
or Boss rewards when a registered relic-acquisition hook suspends. It replays
the exact relic action and ordered request prefix through the existing
acquisition-site registry; configured Native combat-reward relic cases remain
with their specialized session.
Static source review and `git diff --check` only; no tests or builds were run.

**S6 Native extra-relic acquisition action increment (2026-10-23):** The
existing Native event-relic session now recognizes both `take_reward_relic`
and `take_reward_extra_relic` when they select one of its four supported
combat-reward acquisition hooks. For a secondary group, it resolves the
first pending group through the legal index-zero primary action or addresses
an extra group directly, then replays the same registered request prefix.
Other relic hooks, source-pool fidelity, and Native cursor parity remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**D3/D4 rest-heal reward continuation increment (2026-10-23):** The shared
controlled run-action frame now saves a legal legacy `rest_heal` that reaches
relic-generated card offers. It captures the registered `reward`-stream
rarity/model requests, including the legacy requests used by Native-shaped
Dream Catcher generation when `NativeRunRng` is absent, then replays the saved
action and commits the Reward with the ReferenceNature cursor. Configured
Native reward generation and unrelated Rest actions remain outside this
boundary. Static source review and `git diff --check` only; no tests or builds
were run.

**S0 Neow request-source inventory increment (2026-10-23):** The Neow
decision-plan builder now references its owning Chance Law session's finite
site list explicitly, and the regenerated direct-call census resolves both
guarded Neow request constructors. The census maps 99 of 104 typed requests;
five generic or dynamic request sites remain for manual source tracing.
Static review and `git diff --check` only; no tests or builds were run.

**S0 forwarded reward-query inventory increment (2026-10-23):** The direct
callsite census now follows the finite Chance Law site-array guard for the
legacy Punch Off reward request and resolves its two registered reward sites.
The generated inventory maps 100 of 104 typed requests; four generic or
forwarded request sites remain unresolved. Static review and
`git diff --check` only; no tests or builds were run.

**S0 boss-law comparison source trace increment (2026-10-23):** The direct
callsite census now follows the run-start comparison's Overgrowth and
Underdocks boss-law branches through their shared fail-closed builder. It
resolves both registered site IDs and their shared `choose_index` operation.
The generated inventory maps 101 of 104 typed requests; the three generic
Native choice wrappers remain dynamic. Static review and `git diff --check`
only; no tests or builds were run.

**S0 Native player-choice caller inventory increment (2026-10-23):** The
existing helper-call census now traces 23 concrete `NativePlayerRngV111` and
direct `NativeRandomChoiceV111` callers to their stable site IDs. All 39
known helper callsites, including shuffle helpers, map to registry and
migration-matrix rows. The three generic request constructors remain
parameterized in the typed-construction census. Static review and
`git diff --check` only; no tests or builds were run.

**S6 Native-regional Event-entry fallback increment (2026-10-23):** The
existing atomic Event-node entry session now consumes supported saved legacy
`event` requests for generated gold, Stone of All Time's potion slot, and
Future of Potions card types. It preserves each typed request ordinal and
uses equal-weight `UNVERIFIED` laws; configured Native laws and providers are
unchanged. Event selection, event-pool completeness, seed frequencies, and
Native cursor parity remain open. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 Native combat-generation stream attribution increment (2026-10-22):**
Supported generated-model, random draw-Attack, and transform choices now label
their typed semantic events with `run.combat_card_generation`, while random
combat-potion choices use `run.combat_potion_generation`, when Native combat
run streams are present. These match ordinary Native execution. Prototype-only
choices retain `prototype.combat`; prior prototype-labeled decisions remain
valid when replaying Native-backed histories, and pilot restore republishes the
saved typed decision so its original transcript is preserved. Probability
status and Native cursor parity remain unverified. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 turn-start selection inventory correction (2026-10-22):** The current
continuation already admits source-driven player-turn discard counts and
Tools of the Trade's per-stack hand selection through semantic card selection
and end-turn chronology replay. The unified architecture now records these
alongside Entropy and Toasty Mittens, while keeping unhandled turn-start
selection prompts unsupported. Static source review and `git diff --check`
only; no tests or builds were run.

**S3 later-act encounter-bag shape guard increment (2026-10-22):** The
run-world stochastic owner now checks active and prepared later-act bag act
IDs, cursor-dependent weak/normal queue lengths, pool membership, boss
identities, and second-boss shape on construction and replacement. Ascension,
profile unlock, route ordering, and shared-assignment checks remain at the
run boundary and validating commit transitions. Static source review and
`git diff --check` only; no tests or builds were run.

**S5 native-policy fair-view capability increment (2026-10-22):** The shared
`fair_observe` envelope now advertises a `semantic_native_policy` view kind.
It accepts only baseline-enabled semantic sessions at player boundaries and
uses the same `PrototypeAiEnvironment` projection as other fair views. The
Python `SemanticCombat` selects the mode-specific view automatically; the
existing `semantic_combat` view remains compatible. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 Act 1 encounter-pool projection guard increment (2026-10-22):** The
run-world stochastic owner now validates Act 1 pool structure, region,
remaining queues, cursor, and boss identity during construction and every
replacement, including snapshot and flattened projection writes. The full
world-region correspondence check remains at run validation. Static source
review and `git diff --check` only; no tests or builds were run.

**S3 three-act encounter handoff ownership increment (2026-10-22):** Act
transitions now publish the next active bag, remaining future bags, and Act 1
pool clear through `RunWorldStochasticStateV111.CommitActTransition`. The
owner validates the target act's queues and shared Darv assignment across
active and prepared bags before committing. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 prepared later-act encounter publication increment (2026-10-22):**
Completed interactive and configured run-start preparation now commits
future-act bags through
`RunWorldStochasticStateV111.CommitPreparedLaterActEncounterBags`, validating
the complete ordered act set, route eligibility, Ascension, and revealed
Ancient support before publishing. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 later-act encounter-pool commit ownership increment (2026-10-22):**
Experimental Hive/Glory combat entry now publishes the selected encounter bag
through `RunWorldStochasticStateV111.CommitLaterActEncounterBag`, validating
the bag against the configured route, act, Ascension, and revealed Ancient
support before publication. Static source review and `git diff --check` only;
no tests or builds were run.

**S3 Act 1 encounter-pool commit ownership increment (2026-10-22):** Live
Overgrowth and Underdocks encounter draws now publish weak, normal, and elite
pool updates through `RunWorldStochasticStateV111.CommitActOneEncounterPool`,
which validates each candidate against the current Act 1 region and cursor
before publishing. Static source review and `git diff --check` only; no tests
or builds were run.

**S3 relic-bag projection write guard increment (2026-10-22):** The
`RunWorldStochasticStateV111.RelicBag` owner property validates every non-null
bag's rarity partitions, identity pool, and order mode on construction and
replacement, including writes through the flattened world projection.
Player-ownership consistency remains a complete run-boundary check. Static
source review and `git diff --check` only; no tests or builds were run.

**S3 relic-bag publication ownership increment (2026-10-22):** Relic
acquisition pruning and bag pulls now commit through
`RunWorldStochasticStateV111.CommitRelicBag`, which validates the resulting
rarity partitions and remaining identities before publishing the provider
state. Full ownership checks remain at the run boundary, allowing mechanics
to finish an acquisition before stale bag entries are pruned. Static source
review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature projection write guard increment (2026-10-22):** The
flattened `RunState.ReferenceNatureState` setter now routes non-null cursor
writes through the monotonic run-owner transition. Explicit null clearing also
uses a named owner operation. Static source review and `git diff --check` only;
no tests or builds were run.

**S3 ReferenceNature run-cursor commit guard increment (2026-10-22):**
`ReferenceNatureRunCursorV111.Commit` now delegates publication to
`RunStochasticStateV111.CommitReferenceNatureCursor`, which rejects a seed
replacement, cursor rewind, or same-count branch change against the run's
committed cursor. Static source review and `git diff --check` only; no tests or
builds were run.

**S3 ReferenceNature map-audit restore ownership increment (2026-10-22):**
Legacy run-action snapshot restoration now imports missing map-generation
audits by replaying each entry through
`RunStochasticStateV111.AppendReferenceNatureMapGenerationAudit`, preserving
the owner validation and fork boundary. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 controlled run suspension publication increment (2026-10-22):**
`RunStochasticStateV111.SuspendControlledRunActionChoice` and
`SuspendControlledEventRewardChoice` now validate each continuation against
its exact partial run and publish it through the run stochastic owner.
Start-run, map-node, Shovel, event-reward, and replay-resuspension paths use
these transitions; post-step checkpoint rebasing shares them. Static source
review and `git diff --check` only; no tests or builds were run.

**S3 semantic pilot run-provider publication increment (2026-10-22):**
`SemanticPilotCombatEngineV111.PublishRunContinuation` now publishes the
run-scoped `SemanticOtherChanceStateV111` and reward cursor audit through
`RunStochasticStateV111.PublishSemanticPilotContinuation`, preserving the
mechanics result and validating the cost provider before the pilot transition
is committed. The pilot `OtherChance` and `RewardAudit` members remain
compatibility projections. Static source review and `git diff --check` only;
no tests or builds were run.

**S3 ReferenceNature run-initialization publication increment (2026-10-22):**
`RunStochasticStateV111.CommitReferenceNatureRunInitialization` now publishes
the run's Nature cursor, completed initialization transcript, and pending
marker together. It validates that a completed transcript agrees with the
committed cursor. Configured run creation and ordinary, Overgrowth, and
experimental ReferenceNature run-start completion use this owner transition.
Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled run-continuation checkpoint publication increment (2026-10-22):**
`RunStochasticStateV111.RebasePendingContinuationCheckpoints` now republishes
event-reward and run-action partial checkpoints after step mechanics and Native
provider reconciliation. It validates each rebased continuation against the
complete run owner, and `PrototypeGameEngine.FinalizeStep` delegates this
transition to the stochastic owner. Snapshot fields remain unchanged. Static
source review and `git diff --check` only; no tests or builds were run.

**S3 run-world encounter and completed-room history ownership increment (2026-10-22):**
`RunWorldMechanicsStateV111` now appends encounter IDs and completed-room
records through owner methods. Ordinary combat entry, controlled-playable
fixture entry, and room completion use those transitions. Static source review
and `git diff --check` only; no tests or builds were run.

**S3 run-world event history append ownership increment (2026-10-22):**
`RunWorldMechanicsStateV111.AppendEventId` owns event-history appends, and
ordinary event entry, Native Ancient fixtures, and source-backed event
fixtures now use it. Flat snapshot projection remains unchanged. Static source
review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature run audit history append ownership increment (2026-10-22):**
`RunStochasticStateV111` now validates and appends completed map-generation
audits and Ancient-offer session snapshots. Run-action completion and legacy
Ancient replay use those owner methods instead of assembling history arrays.
Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature run-action cursor append ownership increment (2026-10-22):**
The run-action stochastic owner now advances its Nature cursor and appends the
matching decision as one transition. Ordinary actions, Native combat entry,
map generation, Ancient offers, and later-act preparation use that transition.
Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled committed-intent append ownership increment (2026-10-22):**
`ControlledIntentStateV111.AppendResolvedDecision` checks the chosen ticket
against the exact pending request and ordinal, then appends it while clearing
the request. Live intent resolution and source-reachability replay share the
same transition. Static source review and `git diff --check` only; no tests or
builds were run.

**S3 controlled initial HP choice transition ownership increment (2026-10-22):**
`ControlledInitialHpStateV111.ApplyChoice` now validates a selected ticket and
publishes the enemy HP, enemy cursor, transcript, and next request together.
Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled recycle swap transition ownership increment (2026-10-22):**
`ControlledRecycleShuffleV111.AdvanceSwap` now validates the selected ticket
against the saved request and advances its permutation, cursor, transcript, and
next request together. Live resolution and replay validation share the same
transition. Static source review and `git diff --check` only; no tests or
builds were run.

**S3 controlled intent ticket append ownership increment (2026-10-22):**
`ControlledTurnIntentStochasticStateV111` now validates a selected outcome
against its pending request and transcript ordinal, then appends the decision
and clears that request together. Live resolution and prefix validation use the
same transition. Static source review and `git diff --check` only; no tests or
builds were run.

**S3 scripted run completed-action publication increment (2026-10-22):**
`ScriptedRunStochasticStateV111` now validates completed action records and
appends them while clearing the active action. `ScriptedRunSessionV111`
publishes that provider transition with the resulting run through its
mechanics and stochastic envelope. Static source review and `git diff --check`
only; no tests or builds were run.

**S3 controlled run history append ownership increment (2026-10-22):**
`RunStochasticStateV111` now validates and appends completed controlled
event-choice and run-action traces. It also owns replacement of the latest
event-choice trace when its deferred reward completes, and publishes a
completed run-action trace together with clearing its continuation. Static
source review and `git diff --check` only; no tests or builds were run.

**S3 controlled playable history append ownership increment (2026-10-22):**
`ControlledPlayableCombatStochasticStateV111` now appends each live controlled
decision to its mixed transcript and typed projection in one validated owner
transition, and advances the fixture shuffle cursor with its decision and next
request. `ControlledPlayableCombatMechanicsStateV111` now publishes the player
action and resulting run together. Static source review and `git diff --check`
only; no tests or builds were run.

**S3 semantic pilot mechanics history append increment (2026-10-22):**
`SemanticPilotMechanicsStateV111` now validates, forks, and appends each
typed continuation history entry. Pilot publication delegates the history
transition to that mechanics owner. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 turn-intent ticket append ownership increment (2026-10-22):** The
semantic end-turn resolver now verifies that one selected intent ticket
extends the saved prefix, then publishes it through
`SemanticEndTurnStochasticStateV111.AppendIntentDecision`. The stochastic
owner validates the controlled provider and site before copying the request
support. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 semantic end-turn chronology append ownership increment (2026-10-22):**
`SemanticEndTurnStochasticStateV111` now appends enemy-action, target, and
nested-choice decisions with their boundary references as one transition. It
reconstructs legacy enemy/target chronology before appending and carries the
completed boundary history into the next intent frame. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 semantic intent transcript append ownership increment (2026-10-22):**
`SemanticEndTurnStochasticStateV111` now validates and appends semantic intent
annotations. The resolver publishes the annotation through the stochastic
owner after checking that the primitive intent ticket advanced once. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 common automatic-pipeline resume boundary classifier increment (2026-10-22):**
`ClassifyAutomaticPipelineResumeBoundary` now classifies reward/terminal,
controlled and semantic choices, player decisions, `AdvanceTurn`,
`DrawPlayerHand`, and other suspended cursors in one engine owner. Semantic
end-turn publication and controlled-playable staging use the shared result;
unrecognized unresolved choice payloads remain suspended instead of falling
through to a stage. Their transcript and provider policies remain local.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 tagged semantic pre-draw resume handoff increment (2026-10-22):** The
ordinary pipeline resumer now recognizes a cleared, tagged semantic
`BeforeHandDraw` continuation and resumes that stage itself. Pool-shuffle
adapters no longer pass a provider-specific resume flag; transcript and draw
progress remain with the semantic facade. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 post-intent boundary publication increment (2026-10-22):** Semantic
end-turn now sends the resumed post-intent checkpoint through the same
automatic-pipeline boundary publisher as later hand-draw resumes. Reward and
terminal results, controlled choices, and supported semantic choices reached
before `DrawPlayerHand` remain with their existing owners; the facade advances
into hand draw only on the classified clean draw boundary. The initial
end-turn action likewise advances only from a classified `AdvanceTurn` result.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 clean hand-draw boundary admission increment (2026-10-22):** The shared
resume classifier now checks the composed continuation's readiness before
reporting `AdvanceTurn` or `DrawPlayerHand`. Semantic hand-draw entry and its
engine owner, plus semantic end-turn origin validation, use that classification
instead of separate lists of active progress fields. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic hand-draw progress projection increment (2026-10-22):** The
automatic-pipeline stochastic owner now projects both active semantic draw
counts and the zero-count `BeforeHandDraw` suspension to the semantic draw
facade. That facade no longer interprets the tagged pre-draw mode itself.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 semantic draw mechanics projection ownership increment (2026-10-22):**
`SemanticCombatDrawMechanicsStateV111` now owns Run, requested/remaining count,
and automatic-pipeline progress projection transitions. The semantic draw
record keeps its flattened properties as delegates, and end-turn projection
queries the mechanics owner directly. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic draw transcript append ownership increment (2026-10-22):**
`SemanticCombatDrawStochasticStateV111` now owns validated appends for random
pile-card, insertion, cost, Stratagem, and draw-hook decisions. Resolution
adapters publish these typed history transitions through the stochastic owner.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 automatic-pipeline cursor clear owner increment (2026-10-22):** Runtime
cursor consumption and cursor-free checkpoint projection now use
`PrototypeGameEngine.ClearAutomaticPipelineContinuation`. End-turn setup,
resume, extra turns, intent checkpoints, and enemy-action/initial-intent
validation no longer write ad hoc null cursor projections. Snapshot fields and
resume behavior remain unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic hand-draw receipt publication owner increment (2026-10-22):**
`AttachCompletedSemanticHandDrawReceipt` now publishes the completed semantic
draw receipt through the automatic-pipeline owner. Both ordinary hand-draw
completion and the already-advanced semantic resume path use it, so the
receipt is validated against the candidate combat at one boundary. Snapshot
fields remain unchanged. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 automatic-pipeline continuation publication owner increment (2026-10-22):**
`AttachValidatedAutomaticPipelineContinuation` now validates and publishes
non-null pipeline cursors through one runtime boundary. Opening-stage,
hand-draw, turn-intent, and enemy-action writers use it, so cursor and payload
validation happen against the candidate combat before publication. Snapshot
fields remain unchanged. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 automatic-pipeline cursor publication validation increment (2026-10-22):**
`SetAutomaticPipelineContinuation` now validates the composed cursor and its
live combat suspension payload before publishing the new continuation.
Malformed indices and post-dispatch markers therefore fail at the shared
writer as well as during snapshot validation. Snapshot fields are unchanged.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 active hand-draw cursor validation increment (2026-10-22):** The
automatic-pipeline stochastic owner now requires every active hand-draw
progress mode to sit at `DrawPlayerHand` and rejects overlap with enemy-action
or turn-intent frames. This covers ordinary controlled draw and draw-cost
progress in addition to the more specialized semantic and hook checks.
Snapshot shape and valid resume behavior are unchanged. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 automatic-pipeline resume alias cleanup (2026-10-22):** Removed the
unused internal `ResumeAutomaticPipelineAfterChoiceResolution` forwarding
method after confirming all repository call sites use
`ResumeAutomaticPipelineContinuation`. The shared resume owner remains the
single entry point. Static source review and `git diff --check` only; no tests
or builds were run.

**S4 semantic pilot opening-cursor projection increment (2026-10-22):**
`PendingInitialIntents` now reads the source-route intent frame through
`PrototypeAutomaticPipelineMechanicsStateV111.IsAtOpeningBoundary`, checking
the named opening stage and configured pipeline-end cursor together.
Deserialization and the flattened field remain unchanged. Static source
review and `git diff --check` only; no tests or builds were run.

**S4 post-dispatch suspension payload validation increment (2026-10-22):**
The mechanics owner now rejects a saved post-dispatch cursor unless its
dispatch event still has a pending player or Knowledge Demon choice. Both
the cursor/event match and the pending suspension payload are checked against
the live combat state. Snapshot shape remains unchanged. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 post-dispatch cursor validation increment (2026-10-22):** The automatic
pipeline mechanics owner now requires a saved post-dispatch event marker to
match the dispatch immediately before its resume cursor. Unknown event kinds
and mismatched cursor/event pairs fail validation; valid continuation shape
and snapshot fields remain unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 configured Native Colorless pool increment (2026-10-22):** Run profiles
and combat snapshots carry the fully-unlocked bit and optional exact
Colorless epoch unlock IDs, with an explicit list overriding the full-profile
default. Ordinary Native generation and resumable Nature
replay share the source-ordered implemented solo pool filtered by unlocked
epochs; unknown partial-profile data fails closed, while synthetic generation
retains its conservative pool. The operation uses
`run.combat_card_generation`; source attribution and cursor parity remain
unverified. Static source review and `git diff --check` only; no tests or
builds were run.

**S6 Native merchant Colorless unlock-pool increment (2026-10-22):** Ordinary
Native Shop generation, the read-only merchant model law, and atomic
ReferenceNature replay now resolve their ordered Colorless support through
the configured run unlock profile. Unknown partial-profile unlock data fails
closed. Static source review and `git diff --check` only; no tests or builds
were run.

**S6 Lead Paperweight epoch-filtered reward-pool increment (2026-10-22):**
Lead Paperweight's ordinary Neow reward and its atomic ReferenceNature pool
reconstruction now filter the ordered implemented Colorless reward pool by
the configured epoch unlocks. The existing reward RNG treatment and native
pool fidelity remain open. Static source review and `git diff --check` only;
no tests or builds were run.

**S6 Colorless transform unlock-pool increment (2026-10-22):** Ordinary
combat transforms, saved transform choices, and Mayhem transform validation
now resolve Colorless replacement support through the configured combat
profile. Profile-filtered support stays consistent through choice suspension
and replay validation. Native pool assignment and cursor parity remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S6 Fried Eel Colorless epoch-pool increment (2026-10-22):** Endless
Conveyor's ordinary Fried Eel draw, Chance Law query, and atomic
ReferenceNature replay now share the configured epoch-filtered implemented
solo Colorless pool. Fully unlocked profiles retain the current supported
pool order; unknown partial-profile data fails closed. Source pool fidelity,
event-stream assignment, and seeded cursor parity remain open. Static source
review and `git diff --check` only; no tests or builds were run.

**S6 merchant snapshot Colorless unlock validation increment (2026-10-22):**
Run-state validation now checks each configured Colorless Shop offer against
the same epoch-filtered rarity pool used by merchant generation and laws.
Fully unlocked profiles retain the previous accepted offers; locked or
unknown-profile offers fail closed. Unconfigured legacy states retain their
catalog-wide compatibility validation. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 automatic-pipeline validated resume-index increment (2026-10-22):**
The pipeline mechanics owner now validates ordinary resume-index projection
and suspended step-index matching. The shared engine and stochastic owner use
those operations for runtime resume and saved enemy-action validation instead
of directly interpreting `NextStepIndex`; post-dispatch context and the
serialized cursor shape remain unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 ordinary automatic-pipeline cursor ownership increment (2026-10-22):**
The mechanics owner now exposes `IsValidOrdinaryCursor` for well-formed
ordinary cursors, including the completed final-step boundary. Hand-draw,
generation, intent, target, and post-dispatch validators use its step and
boundary predicates instead of repeating opening-stage or post-dispatch
checks. Cursor serialization and resume behavior remain unchanged. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 opening Imbued autoplay target increment (2026-10-22):** The shared
automatic-pipeline target boundary now validates the saved `LateRelicAutoPlay`
cursor against the exact remaining Imbued driver queue in persistent-card
order. Target choices must name its head; nested choices must retain the
current autoplay frame and exact queue suffix. This reuses the existing
semantic target and nested-choice continuations. Static source review and
`git diff --check` only; no tests or builds were run.

**S1 exact deck-prefix JSONL query increment (2026-10-22):** The bounded
Nature-only prefix distribution is now available through the opt-in
`semantic_deck_prefix_distribution` JSONL operation. It accepts a run handle
whose combat has an idle Nature-owned deck, a prefix length, and an optional
full-order cap, and returns
the versioned exact integer completion counts already provided by the core
enumerator. Its optional `audit_sequential` input composes `QueryDraw` supports
through the audit limit negotiated from `hello` (currently 64 positions) and
reports an exact match against full-order enumeration. The audited operation
returns the distribution it already enumerated, avoiding a second full-order
pass. The capability stays out of the fair player view. The distribution
remains `UNVERIFIED`; no tests or builds were run.

**S1 exact deck-prefix distribution increment (2026-10-22):**
`SemanticDeckEngineV111.EnumeratePrefixDistribution` groups the existing
bounded full-order completions by ordered physical-card prefix and returns
integer completion counts with defensive state and outcome fingerprints. This
provides a sampler-independent joint distribution for source-independent
ReferenceNature comparisons under the existing uniform-pool model.
`AuditPrefixDistributionAgainstSequentialChanceLaws` also composes each
successive physical-card `QueryDraw` support and checks exact prefix counts
against full-order enumeration. The uniform-pool law remains `UNVERIFIED`, and
no native calibration or distribution-suite acceptance is claimed. Static
source review and `git diff --check` only; no tests or builds were run.

**S3 automatic-pipeline mechanics owner read increment (2026-10-22):**
Pipeline execution, hand-draw boundaries, end-turn validation, and the
stochastic owner's cursor checks now read step index, opening stage, and
post-dispatch event through the mechanics owner. Flat continuation fields
remain snapshot projections. Static source review and `git diff --check` only;
no tests or builds were run.

**S3 automatic-pipeline stochastic owner read increment (2026-10-22):**
Automatic pipeline execution, validation, semantic-pilot projection, and
controlled-playable checks now read intent, enemy-action, and hand-draw
progress through the continuation's stochastic owner. Intent and enemy-action
frames are published through that owner, and intent resumption clears its
frame there. Flat members remain snapshot projections. Static source review
and `git diff --check` only; no tests or builds were run.

**S3 scripted run session owner read increment (2026-10-22):** Scripted action
decision prefixes and continuations, plus whole-run completed actions and active
action, now read through their stochastic owners for resolve, replay, validation,
and save. Existing flat snapshots and replay order are unchanged. Static source
review and `git diff --check` only; no tests or builds were run.

**S3 Native event stochastic session owner read increment (2026-10-22):**
Native event-relic logic now reads its sampler cursor, provider prefix, pending
request, and labels through its controlled-action stochastic owner; Native
event-gold logic reads cursor and decision state through its stochastic owner.
Flat snapshots, boundary filters, and request order are unchanged. Static
source review and `git diff --check` only; no tests or builds were run.

**S3 controlled-action stochastic session owner read increment (2026-10-22):**
Merchant, Treasure, combat-reward, and Native Shovel session logic now reads
the Nature cursors, provider prefixes, pending requests, and candidate labels
through `ReferenceNatureControlledActionStochasticStateV111`. Snapshots and
writes remain flat-compatible; request support and replay order are unchanged.
Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native event-shuffle transcript owner read increment (2026-10-22):**
Native event-shuffle support construction, sampling, replay, validation, and
snapshot restoration now read the ordered Nature decision prefix through
`ReferenceNatureStochasticStateV111`. Combat-end index replay and its request
checks are unchanged; snapshot fields remain flat. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 map and later-act preparation transcript owner read increment (2026-10-22):**
Map-generation audit/session and later-act preparation session validation and
replay now read their Nature decision histories through the shared stochastic
cursor owner. Existing audit and snapshot fields, generator prefixes, and
request ordering are unchanged. Static source review and `git diff --check`
only; no tests or builds were run.

**S3 setup and event Nature transcript owner read increment (2026-10-22):**
Neow-offer, Native event-relic, and configured Native event-gold sessions now
read decision prefixes through their stochastic owners during query,
continuation, validation, save, and replay. Flat snapshots and serialized
request order are unchanged. Static source review and `git diff --check` only;
no tests or builds were run.

**S3 controlled-action Nature transcript owner read increment (2026-10-22):**
Merchant, Treasure, combat-reward, and Native Shovel sessions now read their
law-decision histories through the typed stochastic owner during resolution
and validation, including source-order checks. Flat snapshots, transcript
ordering, and writes remain unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 multi-choice ReferenceNature transcript owner read increment (2026-10-22):**
Glory Reflections, Infested Automaton, Endless Conveyor, and Punch Off
fight/reward sessions now read their Nature decision prefixes through
`ReferenceNatureStochasticStateV111` during support construction, continuation,
validation, save, and replay. Snapshot fields and ordered decisions are
unchanged. Static source review and `git diff --check` only; no tests or builds
were run.

**S3 one-choice ReferenceNature transcript owner read increment (2026-10-22):**
Trial, Tablet of Truth, and Trash Heap session completion, validation, save,
and restore now read their Nature decisions through
`ReferenceNatureStochasticStateV111`. Snapshot fields and decision order are
unchanged. Static source review and `git diff --check` only; no tests or builds
were run.

**S3 controlled turn-intent transcript owner read increment (2026-10-22):**
Intent-boundary validation, support construction, and typed replay now read
pending requests and ticket histories through
`ControlledTurnIntentStochasticStateV111`. Existing flattened snapshot fields,
writes, and outcome order are unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 semantic draw transcript owner read increment (2026-10-22):** Draw-cost
request ordinals and random-pile-card, insertion, cost, Stratagem, and draw-hook
replay/publication now read typed histories through
`SemanticCombatDrawStochasticStateV111`. Existing flat snapshot projections,
write paths, and continuation order are unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 continuation transcript owner read increment (2026-10-22):** Semantic
end-turn validation, replay, and publication now read intent, enemy-action,
target, boundary, and nested-choice histories through
`SemanticEndTurnStochasticStateV111`; semantic-pilot and controlled-playable
replay and validation read typed histories through their stochastic owners.
The controlled-playable composite recycle transcript remains a facade
projection. Existing flattened snapshot projections, write paths, schemas, and
transcript order are unchanged. Static source review and `git diff --check`
only; no tests or builds were run.

**S5 Nature-choice session-kind registry increment (2026-10-22):** The advertised `SemanticNatureChoiceViewSessionKinds` and JSONL supported-kind classification now use one registry. JSONL keeps its per-session typed result dispatch; a registry kind with no matching dispatch now fails as an implementation mismatch rather than being reported as unsupported. No wire schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Native-policy replay action-resolution increment (2026-10-22):** Provider replay capture now passes each already-resolved legal `GameAction` through the validated Native-policy pilot step. Capture and execution therefore use the same action resolution while retaining the existing provider audit and action history. No snapshot or wire schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S3 run-world stochastic owner read increment (2026-10-22):** Runtime encounter-pool and relic-bag reads now go through `RunWorldState.Stochastic` across route entry, combat selection, Chance Law validation, replay, snapshots, observations, and invariants. Flattened `RunWorldState` properties remain for snapshot compatibility. Stream, bag, and encounter behavior are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 run audit and transcript owner read increment (2026-10-22):** Provider replay capture, ReferenceNature validation, snapshot validation, and semantic-pilot projections now read run initialization transcripts, map/Ancient audit histories, pending markers, and semantic audits through `RunStochasticStateV111`. Serialized fields and replay checks remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic deck owner read increment (2026-10-22):** Semantic draw, selection, cost, pool-shuffle, replay projection, and combat-entry paths now read the Nature deck through `CombatStochasticStateV111`. The flattened `CombatState.SemanticDeck` projection remains for snapshot compatibility. Deck transcripts, mechanics, and outcomes are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 committed event-gold owner read increment (2026-10-22):** Native event gold outcomes now flow through `EventStochasticStateV111.RandomOutcomes` in source-boundary validation, event resolution, and Nature observations. Flattened event snapshot fields and result values are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 event potion-slot owner read increment (2026-10-22):** Stone of All Time entry validation, event-choice/session guards, and Native event replay now read the committed potion slot through `EventStochasticStateV111.RandomOutcomes`. Flattened event fields, selected slots, and serialized snapshots are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 committed Conveyor outcome owner read increment (2026-10-22):** Endless Conveyor dish identity, previous dish, count, and deferred-roll status now flow through `EventStochasticStateV111.RandomOutcomes` in event mechanics, validation, Native event replay, Nature sessions, and observations. Flattened event fields and serialized snapshots are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 committed event continuation outcome owner read increment (2026-10-22):** Deferred card IDs, Tinker type, and Repy chest potion/relic offers now flow through `EventStochasticStateV111.RandomOutcomes` in event resolution, validation, and Native event replay. Flattened event fields and serialized snapshots are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Future of Potions offer owner read increment (2026-10-22):** Opening-offer validation, trade resolution, and legal-action generation now read saved potion offers through `EventStochasticStateV111.RandomOutcomes`. Flattened event fields and serialized snapshots are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 committed event outcome owner publication increment (2026-10-22):** Stone of All Time now clears its selected potion slot by publishing a new `EventCommittedRandomStateV111` through the event stochastic owner. Snapshot fields and potion resolution are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled event/run-action continuation read increment (2026-10-22):** Controlled event-choice admission, validation, enumeration, scripted run-action admission, run-action validation, and provider replay capture now read pending continuations and committed histories through `RunStochasticStateV111` and `EventStochasticStateV111`. Snapshot projections and replay outcomes are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 JSONL controlled-continuation owner read increment (2026-10-22):** JSONL session results, event/run-action observations, and pending-choice inspection now read suspended continuations and committed histories through the run and event stochastic owners. Serialized JSON fields and session handles remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 room replay continuation owner read increment (2026-10-22):** Event-choice, event-reward, and run-action room execution now reads pending continuations and committed histories through the stochastic owners when constructing legal actions, resuming replay, and appending completed traces. Snapshot projections and action outcomes are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Chance Law cursor owner read increment (2026-10-22):** ReferenceNature session initialization, commit validation, terminal-combat checks, and controlled run-action/event-choice replay now read the saved run cursor through `RunStochasticStateV111`. Cursor equality and commit behavior are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature cursor projection completion increment (2026-10-22):** Remaining Native map/event-result validation, run initialization, exact snapshot schema checks, and run-stochastic audit validation now read the cursor through `RunStochasticStateV111`. Snapshot schemas and serialized cursor values are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat Native RNG projection completion increment (2026-10-22):** Remaining draw-insertion, War Hammer, and Punch Off combat paths now read Native combat RNG through `CombatStochasticStateV111`, including null-presence checks used during replay validation. Stream selection, snapshots, and mechanics outcomes are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Chance Law controlled-continuation owner read increment (2026-10-22):** Native merchant/Treasure law guards, controlled request queries, and ReferenceNature run-action/event-choice session validation now read pending controller frames and committed traces through the run and event stochastic owners. Law supports, fingerprints, and session schemas are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 engine pending-choice owner read increment (2026-10-22):** Legal-action generation, guarded step routing, continuation resume, and fair player-action projection now read event, reward, and run-action suspension state through their stochastic owners. Action IDs, payloads, and pending-choice behavior are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 specialized-session owner read increment (2026-10-22):** Merchant, Treasure, Shovel, combat reward, Native event-choice/reward, and scripted run-session boundary guards now read pending run/event continuations through their stochastic owners; Native event-shuffle cursor snapshots read combat RNG through its owner. Admission rules, laws, and saved transcript shapes are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S5 configured ReferenceNature run-start replay-capture increment (2026-10-22):** Run-action capture composes the saved configured boss trace with the validated Act 1 Overgrowth map audit and, when present, the later-act preparation trace reconstructed by replaying its existing generator from the saved prototype RNG, configuration, and Nature decisions. The exact typed request prefix must cover the complete initialization transcript. This accepts complete configured Underdocks starts too and adds no snapshot field. Static source review and `git diff --check` only; no tests or builds were run.

**S5 experimental ReferenceNature act-transition replay-capture increment (2026-10-22):** Completed configured experimental `continue_act` sessions can now build a provider replay capture without an appended controlled action trace. Capture reconstructs map requests from the newly committed map audit and assigned Ancient requests from the optional saved Ancient-offer audit, while requiring both audit histories to preserve their origin prefixes and their ordered Nature outcomes to equal the run-action transcript. Legacy Native Ancient completion remains unsupported because that provider trace is absent. No snapshot fields changed. Static source review and `git diff --check` only; no tests or builds were run.

**S5 ReferenceNature no-action setup replay-capture increment (2026-10-22):** Neow-offer and Ancient-offer sessions now implement one run-setup capture interface. `provider_replay_compare` validates the completed session and rebuilds its exact ordered typed prefix from the selected outcomes before recording the origin-to-result interval with no player actions. JSONL advertises and accepts both existing setup handles; no session snapshot schema changed. This is replay diagnostic evidence, not source-hook or probability calibration. Static source review and `git diff --check` only; no tests or builds were run.

**S5 ReferenceNature run-action replay-capture expansion (2026-10-22):** The existing run-action capture now accepts any completed ReferenceNature action whose appended controlled trace identifies the saved action and covers every saved Nature decision. It preserves existing action-history prefixes, supports mixed typed traces, and retains the no-trace path only for completed actions with no Nature decisions. Sessions whose specialized continuation publishes an incomplete action trace remain unsupported. Static source review and `git diff --check` only; no tests or builds were run.

**S5 ReferenceNature event-choice replay-capture increment (2026-10-22):** `provider_replay_compare` now accepts completed controlled event-choice sessions. Capture construction restores and replays the mixed player-action/Nature sequence, verifies the appended typed choice trace against the Nature outcomes and prior history, then materializes its requests through the shared boundary journal. JSONL advertises and accepts the existing event-choice session handle; no second capture owner or snapshot schema was added. Static source review and `git diff --check` only; no tests or builds were run.

**S5 ReferenceNature controlled-action replay-capture increment (2026-10-22):** Merchant, Treasure, combat-reward, and Native Shovel sessions now implement one replay-validated action-session interface. `provider_replay_compare` uses one capture factory to validate each session's action and typed stochastic prefix, then records those requests through the shared boundary journal. JSONL accepts each existing completed session handle; mechanics and provider session ownership remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S5 ReferenceNature event-action replay-capture increment (2026-10-22):** Native event-gold/relic, Infested Automaton, Glory Reflections, Trial, Trash Heap, Tablet of Truth, Punch Off fight/reward, Native event-shuffle, and Endless Conveyor now implement the existing controlled-action capture interface alongside Merchant, Treasure, combat-reward, and Native Shovel. Each completed session supplies its exact action and typed provider prefix; sessions that own only semantic decisions reconstruct typed values from replay-validated Chance Laws, requests, and ordered outcome supports. `provider_replay_compare` and JSONL reuse the existing capture factory and accept these session handles; no new capture owner or session snapshot field was added. Static source review and `git diff --check` only; no tests or builds were run.

**S5 Native event-shuffle replay-capture increment (2026-10-22):** The existing Native event-shuffle session now implements the same controlled-action interface. Its completed prefix reconstructs each exact request and Chance Law in order, including combat-end and legacy Repy branches, then routes the selected indices through the shared capture journal. JSONL accepts the existing session handle; no session schema or stochastic owner changed. Static source review and `git diff --check` only; no tests or builds were run.

**S5 Endless Conveyor replay-capture increment (2026-10-22):** The existing multi-boundary Endless Conveyor session now implements the controlled-action interface. Capture reconstructs the completed index and unit-float prefix from its validated ordered Chance Laws, preserving Native mixed requests and the legacy event/reward request bindings. JSONL accepts the existing session handle and reuses the shared capture factory. Static source review and `git diff --check` only; no tests or builds were run.

**S6 legacy act-transition map continuation increment (2026-10-22):** Unconfigured prototype `continue_act` now suspends its registered `map.legacy.*` requests through the existing controlled run-action continuation. The saved frame binds the ActTransition boundary and map stream, replays the action prefix, and publishes the completed map and RNG advancement with the same action trace; `ReferenceNatureRunActionSessionV111` reuses that continuation and commits its cursor after completion. Native map generation, source geometry and call-order fidelity, and legacy equal-index law calibration remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S6 legacy act-transition entry-point increment (2026-10-22):** `ControlledRunActionEngineV111.BeginLegacyActTransition` exposes the existing `continue_act` continuation with the same explicit unsuspended legacy-boundary checks as run-start and Shovel entry points. It delegates to the existing generic run-action owner. Static source review and `git diff --check` only; no tests or builds were run.

**S5 ReferenceNature run-action replay-capture increment (2026-10-22):** Provider replay comparison can now build a validated capture from an unconfigured prototype `continue_act` session when its appended controlled action trace covers every sampled Chance Law decision. Capture validation replays the session, preserves its prior action-history prefix, and records the saved typed decisions through the existing boundary journal. JSONL accepts a run-action session handle for the existing comparison operation. This remains provider/mechanics diagnostic evidence, not Native source-hook or probability evidence. Static source review and `git diff --check` only; no tests or builds were run.

**S4 scripted replay state-envelope increment (2026-10-21):** Scripted action sessions now compose their action mechanics and mixed typed stochastic prefix through `SimulationStateV111`; whole-run sessions compose the shared origin/current checkpoint with completed-action history and the active action. The suspended action's partial mechanics continue to be reconstructed from its origin and decisions. Flat snapshot members and schemas remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 automatic-pipeline cursor ownership increment (2026-10-22):** `PrototypeAutomaticPipelineMechanicsStateV111` now owns “immediately after step,” “immediately after dispatch,” and validated step advancement. Semantic hand-draw and Mayhem event-continuation validation use these operations instead of repeating `NextStepIndex` arithmetic, keeping pipeline cursor interpretation with its mechanics owner. Snapshot fields and draw behavior remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 named RNG bundle access increment (2026-10-22):** `RngBundle` now resolves streams by stable ID and replaces them only when the replacement keeps that ID. Prototype and Native RNG advancement plus the stream-reset and speculative-replacement paths use named access instead of retaining array positions. The serialized stream arrays and RNG behavior remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 semantic/Native-policy action-boundary increment (2026-10-22):** The semantic pilot, Native-policy baseline, and Native-policy replay capture now use `PrototypeAiEnvironment`'s existing legal-action resolver. The two execution routes pass its resolved `GameAction` through the shared environment step entry; each retains its provider-specific scope, stochastic trace, and result-boundary checks. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Native-policy decision-boundary increment (2026-10-22):** The Native-policy post-action validator now uses `PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary`, sharing the semantic pilot and continuation facades' active combat/player-turn/pending-choice classification. Native RNG ownership and exact semantic-deck synchronization remain provider-specific checks. Static review and `git diff --check` only; no tests or builds were run.

**S3 remaining runtime named-stream lookup increment (2026-10-22):** Native event-shuffle cursor audits and the Underdocks legacy combat-cursor comparison now resolve streams by ID through `RngBundle`, including optional lookups that preserve their existing missing-stream checks. Runtime ID-specific stream searches no longer depend on array enumeration; full bundle enumeration remains limited to validation and aggregate cursor snapshots. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native run-RNG lifecycle publication increment (2026-10-22):** `NativeRunRngV111` now publishes enable, sample, combat handoff, release, and post-step reconciliation updates through `RunState.Stochastic` and `RunStochasticStateV111.WithNativeRunRng`. The existing `NativeRunRng` snapshot property remains a compatibility projection, while the active provider owner now writes directly through its stochastic envelope. Static source review and `git diff --check` only; no tests or builds were run.

**S3 run-provider lifecycle publication increment (2026-10-22):** Native run RNG writes throughout combat entry, action/reward resolution, room hooks, run initialization, and semantic Native-policy setup now publish through `RunStochasticStateV111.WithNativeRunRng`; the remaining configured ReferenceNature initialization and restore writes use `WithReferenceNature`. Runtime run-state provider writes no longer use the flattened provider projections; snapshot members and provider values remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat provider publication increment (2026-10-22):** Native combat cursor advancement, formation and creature RNG updates, combat-entry setup, controlled-intent baseline creation, and Native-policy initialization now publish through `CombatStochasticStateV111.WithNativeRng`; Native-policy baseline deck removal uses `WithSemanticDeck`. The flattened combat provider members remain compatibility projections, and stream contents and snapshot shape are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 event stochastic publication increment (2026-10-22):** Event RNG streams and provider modes, suspended controlled choices, and committed event outcomes now publish through `EventStochasticStateV111` owners across event resolution, continuation, Nature replay, and Native event providers. The flattened event members remain compatibility projections, with saved outcomes and snapshot shape unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 prototype RNG lifecycle publication increment (2026-10-22):** Factorized run creation, later-act preparation, Underdocks and Overgrowth setup, and act-transition map generation now publish `PrototypeRng` through `RunState.Stochastic` and `RunStochasticStateV111.WithPrototypeRng`. The flattened `Rng` snapshot property remains a compatibility projection; named stream contents and serialized shape are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature cursor publication increment (2026-10-22):** Run-start, map-generation, run-action, event-choice, combat-terminal restore, and cursor commit paths now publish the committed Nature cursor through `RunStochasticStateV111` or the shared `ReferenceNatureRunCursorV111.Commit` owner. The flattened `ReferenceNatureState` member remains the snapshot projection; session-specific transcript and mechanics updates stay with their existing owners. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic-pilot provider projection increment (2026-10-22):** The pilot's `OtherChance` and `RewardAudit` compatibility setters now update `RunState.Stochastic` through the existing `RunStochasticStateV111` owners. The pilot continues to expose its established flattened properties while provider lifecycle writes stay under the run envelope. Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled run-action publication increment (2026-10-22):** Start-run suspension, post-step continuation reconstruction, controlled run-action resolution, and completed run-action history now publish through `RunState.Stochastic` and the existing `RunStochasticStateV111` helpers. Partial-state replay semantics and flattened choice/history snapshot members remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled event-choice publication increment (2026-10-22):** Suspended event-reward frames and controlled event-choice histories now publish through the run stochastic owner in the event continuation and post-step reconstruction paths. Nested partial-state snapshots keep the same replay boundary and flattened snapshot members. Static source review and `git diff --check` only; no tests or builds were run.

**S3 named RNG result publication increment (2026-10-22):** Native event-context commits, configured event offer audits, combat formation state, map audit snapshots, and the private energy-cost provider now read their resulting stream by its owner ID from `RngBundle`. Runtime bundle consumers no longer publish a stream by assuming it occupies slot zero; serialized stream arrays remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 named speculative RNG copy-back increment (2026-10-22):** Potion model generation now copies speculative stream advances back into the original `RngBundle` by stream ID, not by parallel array position. The same streams advance with unchanged provider calls and cursor values. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic target identity continuation increment (2026-10-22):** Semantic and controlled-playable target choices now pass the selected enemy instance ID into the shared engine continuation. The source-ordered index remains in the typed provider decision and is derived from the live support only where source mechanics require it, such as Sly autoplay's source-order audit. Broader outcome-identity migration and primitive-script versioning remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 generated-card identity continuation increment (2026-10-22):** Ordinary semantic generated-card choices now pass the selected card model ID into the engine continuation, which verifies membership in the live ordered source pool before applying it. Controlled-playable replay translates its retained index decision to that ID at the adapter boundary. Draw-insertion model choices retain their specialized index route for now; broader outcome-identity migration remains open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 draw-insertion model identity continuation increment (2026-10-22):** Semantic generated-card choices at the draw-insertion model stage now pass the selected card model ID into the engine, which validates it against the live source pool before advancing to position selection. Bounded draw-pile insertion positions remain index-valued, and the low-level controlled index script route remains available for compatibility. Other indexed continuation families remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 end-turn intent identity continuation increment (2026-10-22):** `SemanticEndTurnV111` now exposes source-filtered weighted intent choices by stable intent ID and stores each semantic decision beside its compatibility ticket. Restore revalidates the event support and ticket mapping before replaying the ID; older snapshots with only index tickets remain supported. Other indexed continuation families remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 potion and transform identity continuation increment (2026-10-22):** Semantic potion-generation and card-transform choices now pass the selected model ID to their ordinary engine continuations, which validate it against the saved source support before applying it. Controlled-playable replay translates its compatibility index ticket at the adapter boundary. Other indexed continuation families and primitive-script versioning remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 card-instance identity continuation increment (2026-10-22):** Semantic card-shortlist and automatic random-pile-card continuations now apply the selected card instance ID directly after validating it against the live support. Their decision records still retain the ordered index for replay compatibility. Other indexed continuation families and primitive-script versioning remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic draw identity commit increment (2026-10-22):** Ordinary semantic draw, controlled card-effect draw, and peek/scry reveal commits now pass the selected physical card instance ID through `SemanticDrawPileAdapterV111` into the deck owner. The deck owner derives the ordered transcript index from the saved support; serialized event and transcript fields remain unchanged. Other indexed continuations and primitive-script versioning remain open. Static review and `git diff --check` only; no tests or builds were run.

**S3 random hand-cost identity continuation increment (2026-10-22):** Semantic hand-cost selection now passes the selected card instance ID through the shared random-cost continuation. The engine validates it against the saved eligible hand support and applies the chosen card's cost; compatibility index decisions remain available for the controlled-playable adapter. Other indexed continuations and primitive-script versioning remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S4 ReferenceNature law-to-request ordinal binding increment (2026-10-22):** Both `ReferenceNatureChanceLawDecisionV111` entry points now share one selection validator for the law ID, selected outcome, request site, owner, and ordinal. The ordered-index path retains its support-order checks, and primitive-specific transforms retain their existing validation. No transcript or snapshot fields changed. Static review and `git diff --check` only; no tests or builds were run.

**S4 provider replay comparison capture increment (2026-10-22):** `SimulationProviderReplayCaptureV111` groups copied origin/result runs, the player-action sequence, and its ordered typed-boundary trace through the existing `SimulationStateV111` envelope. The comparison engine accepts two validated captures and reports action, origin, boundary, and final-mechanics agreement. `FromScriptedRunSession` and `FromScriptedRunActionSession` replay their completed actions and materialize saved six-primitive decisions through the existing random-boundary journal, binding those capture fields to validated session histories. Generic captures still depend on caller-provided interval completeness; neither path claims source-hook parity or probability fidelity. No gameplay snapshot or provider state changed. Static review and `git diff --check` only; no tests or builds were run.

**S5 provider replay comparison bridge increment (2026-10-22):** JSONL now exposes `provider_replay_compare` and constructs current capture envelopes either from a completed scripted-run or scripted-action handle or from exact origin/result snapshots, player actions, and typed boundary arrays. `hello` advertises the capture/result schemas, operation, input fields, and provider-private diagnostic scope. The fair player observation and existing snapshot schemas are unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S5 Native policy replay capture increment (2026-10-22):** `FromSemanticNativePolicySession` accepts only a seeded Native-policy session whose saved history passes full replay. It reconstructs the first player boundary, verifies the audited setup/committed-intent trace prefix, replays the player actions to recover their exact mechanics actions, and captures only the following mixed provider trace. JSONL accepts `<side>_semantic_native_policy_handle`; setup and initial-intent records stay outside the captured action interval. This is still replay comparison evidence, not Native fidelity or law evidence. Static review and `git diff --check` only; no tests or builds were run.

**S5 replay-capture provenance increment (2026-10-22):** Provider replay capture v2 marks captures as caller-supplied and unverified, scripted whole-run replay validated, scripted action replay validated, or semantic Native-policy replay validated. Comparison v2 reports both sides' provenance alongside action, boundary, and mechanics agreement, so the diagnostic exposes how each interval was assembled without upgrading caller assertions into completeness proof. `hello` advertises the new schemas from their versioned owners. Static review and `git diff --check` only; no tests or builds were run.

**S4 automatic-pipeline boundary query increment (2026-10-22):** The mechanics cursor now also owns valid-index, remaining-step, opening-stage-at-end, and passed-dispatch queries. Controlled-target admission keeps its gameplay policy in `PrototypeGameEngine` while delegating cursor interpretation; opening intent, Mayhem, semantic hand-draw, and end-turn checks use the shared cursor queries. Mechanics validation uses the same opening and index queries. Snapshot fields and resume behavior remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 semantic BeforeHandDraw resume completion increment (2026-10-22):** Resolving a semantic `BeforeHandDraw` choice now returns the completed `DrawPlayerHand` stage to `ResumeAutomaticPipelineContinuation` at its advanced cursor. The shared handoff runs remaining stages and terminal reward publication, or leaves the next supported suspension to its owning adapter. This closes the direct-return path that previously stopped after hand draw. Static review and `git diff --check` only; no tests or builds were run.

**S4 AdvanceTurn intent resume unification increment (2026-10-22):** After committing next-turn intent choices, `ResumeControlledTurnAfterIntents` now attaches the advanced mechanics cursor with the consumed intent frame cleared, then delegates to `ResumeAutomaticPipelineContinuation`. Its `stopBeforeSemanticHandDraw` option now flows through the same handoff, so the end-turn facade retains its draw-stage checkpoint while normal Native/controlled turns continue through the shared cursor owner. Static review and `git diff --check` only; no tests or builds were run.

**S4 semantic end-turn resume-boundary publication increment (2026-10-22):** Resumed enemy-action, random-target, and completed hand-draw paths now share one facade owner for terminal outcomes and supported pending pipeline choices. Each path retains its transcript and source-specific boundary policy, while classification and Run publication stay in one place. Snapshot fields and choice chronology remain unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S4 semantic pilot draw-insertion publication increment (2026-10-21):** Non-end-turn draw insertion now publishes through `ResumeAndPublishNestedChoice`, like the other pilot nested choices. The typed resume and pilot history/run publication share their existing owners, while the insertion's paired-zone mechanics commit and post-insertion support checks remain specialized. End-turn insertion continues through its saved hand-draw facade. Static review and `git diff --check` only; no tests or builds were run.

**S4 semantic BeforeHandDraw pipeline handoff increment (2026-10-21):** The common automatic-pipeline resumer now owns an explicit guarded entry for a resolved semantic `BeforeHandDraw` boundary. Hello World's already-admitted pool-shuffle continuation uses that entry to resume the saved stage; the draw facade retains its counter-returning path. Other callers keep the default behavior for active semantic draw progress, and the source-trigger admission remains unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S4 ReferenceNature run-action checkpoint envelope increment (2026-10-21):** The existing `ReferenceNatureRunActionSessionV111` now composes its source run, selected action, current run, Nature cursor/history, and typed Native combat-entry prefix through `SimulationStateV111` and the shared `PrototypeMechanicsContinuationStateV111`. Its flattened session fields and snapshot schema remain unchanged; nested map, later-act preparation, and Ancient-offer sessions keep their existing owners. Static review and `git diff --check` only; no tests or builds were run.

**S4 semantic ReferenceNature session envelope increment (2026-10-21):** `ReferenceNatureSessionV111` now composes its `SemanticPilotCombatV111` mechanics checkpoint and independent sampler cursor through `SimulationStateV111`, with a focused cursor owner. Existing `Combat` and `NatureState` snapshot members remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 ReferenceNature controlled-action envelope consolidation (2026-10-21):** Configured merchant and combat-reward sessions now reuse the same run-action mechanics checkpoint owner and a shared generic stochastic-prefix owner for cursor, typed provider decisions, law decisions, pending request, and candidate labels. Their typed law validation and boundary replay stay with each session; flattened snapshot fields and schemas remain unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S4 Native event-relic session envelope consolidation (2026-10-21):** `ReferenceNatureNativeEventRelicSessionV111` now reuses the run-action mechanics checkpoint owner and generic controlled-action stochastic-prefix owner. Its typed event/relic law replay, relic-bag checks, cursor validation, and action-specific transcript-length bounds stay with the session; flattened snapshot fields and schema remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Native Treasure and Shovel session envelope consolidation (2026-10-21):** `ReferenceNatureTreasureSessionV111` and `ReferenceNatureNativeShovelSessionV111` now use the shared run-action mechanics checkpoint and controlled-action stochastic-prefix owners. Their relic laws, bag checks, and atomic cursor/replay validation remain session-specific; flattened snapshots and schemas remain unchanged. Static source review and `git diff --check` only; no tests or builds were r…97611 tokens truncated… increment (2026-10-20):** `ControlledInitialHpStateV111` now copies its ordered pending support and typed decision transcript, including nested request supports, on construction, replacement, projection, and fork. Existing snapshot member names and the positional constructor/deconstruction shape remain unchanged. Tampering fixtures now publish a replacement request explicitly; the indexed-write census and `git diff --check` passed. No tests or builds were run.


**S3 controlled intent snapshot ownership increment (2026-10-20):** `ControlledIntentStateV111` now copies its intent and prior-HP decision transcripts, including nested ordered supports, and its pending request on construction, replacement, projection, and fork. Its baseline/live combat, formation, and reference-stream values continue through their dedicated owners. Existing positional snapshot members remain unchanged, and tampering fixtures publish changed transcript or combat values through record replacement. The indexed-write census and `git diff --check` passed; no tests or builds were run.


**S3 controlled recycle snapshot ownership increment (2026-10-20):** `ControlledRecycleShuffleV111` now copies its baseline discard order, partial shuffle, typed transcript, and pending support on construction, replacement, projection, and fork. Existing constructor/deconstruction and serialized fields remain unchanged. The tamper fixture now publishes a transcript replacement explicitly; the indexed-write census and `git diff --check` passed. No tests or builds were run.


**S3 typed request support ownership increment (2026-10-20):** `RandomIndexRequestV111` now copies `OrderedCandidates` on construction and replacement and returns a defensive projection. Request validation and support-count projection read the owned backing array, keeping operation validation and JSON/deconstruction members stable without extra array copies in those checks. The indexed-write census and `git diff --check` passed; no tests or builds were run.


**S3 selected-card action payload ownership increment (2026-10-21):** `SelectCardsPayload` now copies its selected physical-card identities on construction and `with` replacement and returns a defensive projection. Its positional constructor, deconstruction shape, and JSON property name remain unchanged. A source scan found no indexed writes through the projected array; `git diff --check` passed. No tests or builds were run.

**S3 Ancient source-support ownership increment (2026-10-21):** `NativeGloryAncientsV111` now keeps its modifier-generated curse IDs and Ancient card/relic catalogs in private backing arrays. Its existing array properties return copies; card effects/tags and relic trigger/effect arrays are copied as well. The Distinguished Cape law and Ancient offer callers therefore cannot mutate the shared support through a returned projection. Static call-site inspection and `git diff --check` passed; no tests or builds were run.

**S3 suspended request label projection increment (2026-10-21):** `ControlledChanceRequiredExceptionV111` now owns candidate labels and returns a defensive copy from its existing property. This completes the exception boundary described in the canonical ownership contract; caller inspection found no indexed writes through the projection. `git diff --check` passed; no tests or builds were run.

**S3 legacy Shop price-support ownership increment (2026-10-21):** `PrototypeLegacyShopPriceV111` now stores potion and relic price ranges in private backing arrays and returns copies through the existing properties. Legacy Shop generation and the corresponding Chance Laws continue to consume the same ordered supports without exposing shared mutable storage. Static call-site inspection and `git diff --check` passed; no tests or builds were run.

**S3 Neow and Overgrowth catalog ownership increment (2026-10-21):** `PrototypeNativeOvergrowthEvents` now keeps Neow's relic pools and regional event IDs private and returns defensive projections. Its Neow relic definitions and Neow/region event definitions are copied through nested trigger, choice, and effect arrays, preventing callers from changing shared offer support or event behavior. Static call-site inspection and `git diff --check` passed; no tests or builds were run.

**S3 shared definition-copy owner increment (2026-10-21):** `PrototypeDefinitionCopiesV111` centralizes deep copies for card, potion, relic, power, enemy, encounter, and event definitions. Ancient, Overgrowth, Hive, and later-act catalogs use this shared value-copy owner instead of maintaining duplicate nested-array clone logic. Static inspection and `git diff --check` passed; no tests or builds were run.

**S3 regional event-pool ownership increment (2026-10-21):** Hive's source-ordered event names and Underdocks' native/supported region event IDs now use private backing arrays with copy-returning properties. Route selection and coverage checks keep the same configured order without exposing shared storage. Static call-site inspection and `git diff --check` passed; no tests or builds were run.

**S3 Hive catalog ownership increment (2026-10-21):** `PrototypeNativeHiveEvents` now stores card, relic, power, enemy, encounter, and event definitions privately and returns deep copies. The shared `PrototypeDefinitionCopiesV111` copier covers nested card effects/tags, trigger effects, enemy move/AI/power arrays, encounter formation pools, and event-choice payloads. The inherited Mysterious Knight enemy is copied before its Hive-specific variant is stored. Static call-site inspection and `git diff --check` passed; no tests or builds were run.

**S3 Ancient and rest-site catalog ownership increment (2026-10-21):** Tezcatara, Pael, Orobas, Silent, and rest-site card/relic catalogs now use private backing arrays with `PrototypeDefinitionCopiesV111` projections. Tezcatara and Pael event definitions also use the shared event copier. Static call-site inspection and `git diff --check` passed; no tests or builds were run.

**S3 later-act source catalog ownership increment (2026-10-21):** `PrototypeNativeLaterActs` now keeps its Hive/Glory encounter-name inventories and card, event, power, enemy, and encounter catalogs private. Its definition arrays use shared deep copies for nested AI, move, event, and formation support. Static call-site inspection and `git diff --check` passed; no tests or builds were run.

**S3 central encounter-support ownership increment (2026-10-21):** `PrototypeContent` now returns copies of the four Overgrowth encounter pools and owns its consolidated encounter catalog separately from source catalog projections. The shared encounter copier isolates nested formations, selection groups, variants, and enemy pools on construction and projection. Static call-site inspection and `git diff --check` passed; no tests or builds were run.

**S3 prototype definition-map ownership increment (2026-10-21):** `PrototypeContent` now stores its card, potion, relic, power, enemy, status, and event definitions in private dictionaries and exposes clone-on-read `IReadOnlyDictionary` views. The wrapper does not expose the backing dictionary through casts, and the shared definition copier clones nested mutable arrays on lookup, value enumeration, and key/value enumeration. Existing lookup and enumeration APIs keep their names and types. Static inspection and `git diff --check` passed; no tests or builds were run.

**S3 prototype ruleset ownership increment (2026-10-21):** `PrototypeContent` now keeps the ruleset private and returns detached ruleset records with cloned map-floor room pools, required-room pools, and automatic-pipeline steps. Mutating a returned map rule or pipeline array can no longer alter global route or turn mechanics. The ruleset property and record shape remain unchanged. Static inspection and `git diff --check` passed; no tests or builds were run.

**S3 Hive/Glory catalog ownership increment (2026-10-21):** Hive bosses and the Glory weak, Scrolls of Biting, normals, and additional-normal catalogs now use private backing arrays with clone-on-read definition projections. The shared copier isolates nested card and power effects, enemy moves/AI, and encounter formations. Existing array property names and types remain unchanged. Static inspection and `git diff --check` passed; no tests or builds were run.

**S3 regional source catalog ownership increment (2026-10-21):** The remaining Hive/Glory normal, elite, boss, summoning, and event catalogs now keep definition arrays private and return shared deep copies. Underdocks event definitions and Trash Heap/Endless Conveyor event-card catalogs follow the same rule. Fake Merchant stock and definitions plus Punch Off's encounter are detached projections. Static catalog scan and `git diff --check` passed; no tests or builds were run.

**S3 colorless and event-definition ownership increment (2026-10-21):** The Underdocks native/supported encounter pools and combat catalogs are now private-backed; `PrototypeColorlessCards` returns cloned ID pools and deep-copied implemented or multiplayer-only card definitions. Derived shop, reward, epoch, and combat-generation supports are private and return copies. Brain Leech, Self-Help Book, Stone of All Time, Future of Potions, Potion Courier, This or That, Tanx, Vakuu, Nonupeipe, Orobas, Darv, and related event/potion definitions now return deep copies. Static property scans and `git diff --check` passed; no tests or builds were run.

**S3 importer and formation metadata ownership increment (2026-10-21):** Fabricator slot names/positions and the Native snapshot importer's required-field lists are now private arrays with copy-returning public properties. Import results receive their own field-list arrays. Static mutation-site inspection and `git diff --check` passed; no tests or builds were run.


**S4 common semantic-choice dispatcher increment (2026-10-21):** `SemanticChoiceResumeV111` now dispatches nested semantic choices from end-turn replay, pilot resolution, and semantic draw continuations, and also owns the target-resume entry used by end-turn and pilot facades. Existing target transcript and boundary chronology remain the serialized owners, with no snapshot member changes. Static review and `git diff --check` only; no tests or builds were run.


**S3 Native offer audit ownership increment (2026-10-21):** Neow and Pael offer audits and Native PotionFactory model-selection audits now defensively own their offered ID, run/RNG snapshot, and typed decision arrays. Existing positional constructors, deconstruction, and JSON members remain intact. Static inspection and `git diff --check` only; no tests or builds were run.


**S3 engine transition-result ownership increment (2026-10-21):** `TransitionResult` now returns a forked `RunState` and defensive diagnostics array, and copies both on `with` replacement. Its positional constructor, deconstruction, and serialized member names remain unchanged. Static inspection and `git diff --check` only; no tests or builds were run.


**S3 public deck-view and relic-pull support ownership increment (2026-10-21):** `SemanticDeckPublicViewV111` and `PrototypeRelicBagPullPlanV111` now copy their array values on construction and replacement and return defensive projections. Their positional constructors, deconstruction, and JSON member names remain stable. Static call-site inspection and `git diff --check` only; no tests or builds were run.


**S3 Native audit and suspended-intent ownership increment (2026-10-21):** The Orobas, Darv, Nonupeipe, Tanx, Vakuu, and Tezcatara offer audits plus Native initial-HP and intent audits now return detached run/combat state and defensive decision arrays. `ControlledTurnIntentStochasticStateV111` owns its decision prefix and pending request. `NativeFisherYatesResultV111` owns its ordered input, pre-hook result, and swap transcript. Existing positional record, deconstruction, and JSON surfaces remain stable. Static call-site review and `git diff --check` only; no tests or builds were run.


**S3 Nature boundary projection ownership increment (2026-10-21):** `NatureBoundaryEventV111`, `NatureReplayChoiceV111`, and `NatureBoundaryViewV111` now own pending candidate support, candidate labels, and transcript arrays, including nested values on replacement and projection. Their positional constructors, deconstruction, and JSON member names remain stable. Static call-site review and `git diff --check` only; no tests or builds were run.

**S6 configured ReferenceNature Ancient assignment increment (2026-10-21):** Configured experimental Silent run initialization now routes its existing Hive/Glory Darv partition and local Ancient assignment calls through the existing conditional Chance Laws when a ReferenceNature cursor is present. It preserves Glory's bound-one draw, validates the ordered law/decision prefix, and saves the assignment results with the advanced Nature cursor. The equal-index laws remain `UNVERIFIED`; the run-start transcript is not persisted and there is no resumable initialization session. Static review and `git diff --check` only; no tests or builds were run.

**S6 configured ReferenceNature later-act bag increment (2026-10-21):** Prepared Hive/Glory bags now route weak, normal, elite, and boss choices, plus Glory's conditional A10 second boss, through their existing equal-index Chance Laws when the run owns a ReferenceNature cursor. Dynamic queue supports and source ordinals are checked by replaying the complete prefix through the existing bag generator; the first-undiscovered-boss preference remains after the raw boss draw. The legacy prototype RNG stays unchanged. Equal-index weights remain `UNVERIFIED`; native UpFront ordering and a persisted run-start transcript/session remain open. Static review and `git diff --check` only; no tests or builds were run.

**S6 retained combat reward continuation increment (2026-10-21):** The existing `ReferenceNatureCombatRewardSessionV111` also captures registered Prototype `reward`-stream index requests when `NativeRunRng` is absent, including potion pity/drop/model, combat gold, Elite relic, reward-card rarity/model, card rarity/upgrade, potion rarity/model, relic rarity/identity, and Boss/Lava Rock offer swaps. It replays the same legal victory action and publishes the completed Reward with its Nature cursor atomically. These use equal-index `UNVERIFIED` laws; other stochastic hooks retain their providers, and source-pool completeness, native parity, and cursor parity remain open. Static review and `git diff --check` only; no tests or builds were run.

**S6 Native-regional Repy continuation increment (2026-10-21):** The existing Native event shuffle session now admits War Historian Repy's `unlock_chest` action when `NativeRunRng` is absent. It captures the legacy `reward` request ordinal across the generic distinct-pool relic shuffle and both potion picks, conditions all choices on the preceding prefix, and replays without advancing the saved legacy call count. Fallback laws use equal-index `UNVERIFIED` weights; configured Native laws retain their exact bounded-index mapping. The `player.rewards` attribution, source pool fidelity, and seeded parity remain open. Static review and `git diff --check` only; no tests or builds were run.

**S6 generated-card pool ownership increment (2026-10-21):** The ordinary combat sampler, controlled Nature suspension, and saved-choice replay now share `GeneratedCardModelPool` for the supported free-character, zero-cost, and typed character-card generation effects. This removes duplicate eligibility filters while preserving ordered pool contents. Full source-pool completeness, provider attribution, and seeded parity remain separate audits. Static call-site review and `git diff --check` only; no tests or builds were run.

**S6 draw-Attack pool ownership increment (2026-10-21):** Ordinary Native sampling, the controlled draw-insertion continuation, and replay validation now share `RandomDrawAttackModelPool` for generated draw-pile Attack eligibility. This removes a second copy of the ordered source filter without changing its candidate order. Full Native unlock-pool eligibility, stream attribution, and seeded parity remain open. Static call-site review and `git diff --check` only; no tests or builds were run.

**S6 full-pool generation ownership increment (2026-10-21):** Ordinary effect execution and Nature's saved full-pool continuation now share `SourcePoolForShuffle` for generated offers and distinct Common-card eligibility, and use the same distinct-Colorless pool helper for the modeled legacy/Nature support. Native distinct-Colorless generation remains fail-closed pending complete unlocked-pool data. Static call-site review and `git diff --check` only; no tests or builds were run.

**S6 mixed Native Act 1 potion-factory continuation increment (2026-10-21):** The existing `ReferenceNatureRunActionSessionV111` now captures the configured Native Act 1 encounter-pool and formation index requests together with pre-combat PotionFactory rarity `NextFloat` and model `NextInt` requests when a relic can fill an open potion slot. The typed prefix binds each rarity/model pair to its selected node and slot, snapshot restore replays and checks the provider decisions, and completed `ControlledRunActionTraceV111` records keep the mixed transcript with their existing index projection. Run-action session coverage includes both Native PotionFactory sites. Bounded transforms and rarity-conditioned support have exact Chance Laws; source-pool completeness, stream attribution, Native cursor parity, and seeded mechanics equivalence remain open. Static review and `git diff --check` only; no tests or builds were run.

**S3 run-provider projection increment (2026-10-22):** `RunState` now exposes JSON-ignored `SemanticOtherChance` and `SemanticRewardAudit` projections over its stochastic owner. The semantic pilot delegates its existing compatibility projections through those run-level owners, so provider state changes remain under `RunState.Simulation` without changing serialized members.

**S3 named RNG access increment (2026-10-22):** `RngBundle.GetStream` and `TryGetStream` provide required and optional lookup by stable stream ID, keeping array positions internal. Native index, unit-float, ranged-float, ranged-double, Gaussian-integer, and Gaussian-double providers now snapshot their stream state through the bundle API. Legacy choice and shuffle ordinals, scoped provider call counts, Native merchant ordinals, shuffle-probe snapshots, Beautiful Bracelet ordinals, encounter-formation codec checks, combat random-boundary ordinals, selective replay-provider selection, fallback event/reward ordinals, combat-end replay cursors, combat-entry/intent/reward audits, Native regional setup, private cost ownership, Pael reward selection, and source shuffle import cursor verification also resolve the named owner through the bundle API. This relies on `RngBundle`'s unique stream-ID invariant and does not change stream state or request ordinals. Static review and `git diff --check` only; no tests or builds were run.

**S3 run-world stochastic publication increment (2026-10-22):** Persistent Act 1 and later-act encounter bags, prepared encounter histories, and relic bags now publish runtime updates through `RunWorldState.Stochastic`. Run setup, transition, encounter selection, validation projections, relic pruning/draws, and ReferenceNature bag projection use the existing `RunWorldStochasticStateV111` owner; flattened fields remain the snapshot and compatibility projections. Static write-site review and `git diff --check` only; no tests or builds were run.

**S3 run-world owner validation increment (2026-10-22):** `RunWorldStochasticStateV111.Validate` now reads and validates its owned encounter and relic bags directly, passing explicit prepared or active queues to the validators. The world is used for mechanical act/region context, so validation no longer reads stochastic values back through flattened compatibility projections. Static review and `git diff --check` only; no tests or builds were run.

**S3 run-provider validation increment (2026-10-22):** `RunStochasticStateV111` now validates its reward audit against its owned prototype RNG, checks pending initialization through the world's stochastic owner, and removes suspended run-action and event-reward continuations through explicit stochastic-owner updates during replay comparison. Event-choice state is read from the event stochastic owner. Static review and `git diff --check` only; no tests or builds were run.

**S3 combat-provider validation increment (2026-10-22):** `CombatStochasticStateV111.Validate` passes its owned Native RNG and semantic deck directly to the existing formation and draw-pile validators. Their compatibility entry points still resolve the same `CombatState.Stochastic` owner for other callers. Static review and `git diff --check` only; no tests or builds were run.

**S3 suspended run-action validation ownership increment (2026-10-22):** `RunStochasticStateV111.Validate` now checks the suspended controlled run-action frame alongside its owned provider snapshot. `RunState.Validate` no longer invokes that provider-frame check separately; the continuation's action-boundary and saved-state digest checks are unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S3 Native run-cursor validation ownership increment (2026-10-22):** `RunStochasticStateV111.Validate` now passes its owned Native run-RNG bundle to the synchronization validator. The validator compares it with the active combat owner directly, while the existing public entry point still resolves the same run stochastic owner for compatibility. Stream inventory and cursor checks are unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S3 Native run-RNG lifecycle ownership increment (2026-10-22):** Native run-RNG enablement, sampling, encounter attach/replace/release, post-step reconciliation, and configured Act 1 combat-entry guards now read active bundles from `RunStochasticStateV111` and `CombatStochasticStateV111` directly. State publication continues through the existing stochastic owners; the legacy snapshot projections remain unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S3 Native combat RNG lifecycle ownership increment (2026-10-22):** `NativeCombatRngEntryV111` now reads the combat stochastic owner directly when attaching, validating, hatching, summoning, reordering, committing intents, and advancing a monster-local stream. Persistent combat-end hooks also read legacy, run, and combat providers through their stochastic owners. State publication continues through `CombatStochasticStateV111` and `RunStochasticStateV111`; compatibility fields remain for snapshots and callers. Static review and `git diff --check` only; no tests or builds were run.

**S3 Native event-context provider ownership increment (2026-10-22):** `NativeEventContextRngV111` now reads legacy run RNG, Native run RNG, event context streams, and event provider mode through their stochastic owners for entry, draw, shuffle, and validation. The existing event-state publication and legacy/native routing remain unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S3 combat provider read migration increment (2026-10-22):** `PrototypeCombat` now passes the run-owned prototype RNG and optional Native run RNG from `RunStochasticStateV111` to combat setup, action, draw, automatic pipeline, and potion hooks. Existing provider routing and run/combat cursor publication remain unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S3 run-room provider read migration increment (2026-10-22):** `PrototypeRooms` now passes run-owned prototype and Native RNG bundles through `RunStochasticStateV111` for room entry, map and encounter routing, event generation, shop/treasure/reward continuations, and relic actions. Existing provider routing and cursor publication remain unchanged. Static review and `git diff --check` only; no tests or builds were run.

**S3 controlled and semantic draw provider read migration increment (2026-10-22):** Controlled draw-insertion, generated-card, and random-cost continuations plus semantic combat and automatic hand-draw adapters now pass `RunStochasticStateV111.PrototypeRng` into legacy engine calls. Provider use and continuation mechanics remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native event and reward provider read migration increment (2026-10-22):** The Underdocks remaining-event flows, Glory Trial resolution, and deferred Native event rewards now read the prototype and Native run RNG bundles through `RunStochasticStateV111`. Event-specific provider routing, outcome order, and continuation publication remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 regional Native event provider read migration increment (2026-10-22):** Glory event handlers plus Overgrowth room and Driftwood reward flows now read run-owned prototype and Native RNG bundles through `RunStochasticStateV111`. Event and reward mechanics, provider selection, and saved cursor publication are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled-choice and pipeline provider read migration increment (2026-10-22):** Run-action validation, controlled shortlist/pool-shuffle/draw-cost continuations, event continuations, and the opening combat pipeline now read run-owned provider bundles through `RunStochasticStateV111`. Request validation and replay mechanics are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 remaining run-state provider read migration increment (2026-10-22):** Remaining Native relic, event, combat-entry, map/setup, controlled hand/potion, and ReferenceNature session paths now read prototype and Native run providers through `RunStochasticStateV111`. Existing provider selection, result ordering, and continuation commits remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 run-validator provider read migration increment (2026-10-22):** Chance Law boundary checks, semantic policy guards, Native event replay audits, and controlled draw-cost continuations now read `RunState` providers through `RunStochasticStateV111`. Stream identity, law supports, and saved mechanics checks are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 reward and action source provider read migration increment (2026-10-22):** Neow reward generation, relic acquisition, combat entry, pristine reward proposals, and controlled action-boundary checks now read source and resulting run providers through `RunStochasticStateV111`. Reward selection and action validation remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 replay-validator provider read migration increment (2026-10-22):** Native event, combat-reward, Treasure, Shovel, and act-transition replay validators now read saved origin, projected, and resolved run providers through `RunStochasticStateV111`. Existing replay snapshots, source checks, and request evidence remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 speculative prototype RNG publication increment (2026-10-22):** Conditioned combat-entry and pristine reward proposals now fork and replace their named stream through a local provider bundle, then publish it with `RunStochasticStateV111.WithPrototypeRng`. Their independent-stream proposal law and accepted mechanics state remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat-provider projection read migration increment (2026-10-22):** Combat mechanics, semantic continuation, draw-pile, and validation adapters now read Native RNG and semantic-deck providers through `CombatStochasticStateV111`. Mechanics and provider cursor publication are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat replay provider read migration increment (2026-10-22):** Saved combat checkpoints, card-selection/draw replay, Native policy boundaries, and controlled intent entry now read combat-owned Native RNG and semantic deck through `CombatStochasticStateV111`. Replay comparisons and decision boundaries remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 event-provider projection read migration increment (2026-10-22):** Native event-local stream and provider-mode checks across controlled event choices, ReferenceNature replay sessions, and event handlers now read `EventStochasticStateV111` directly. Event cursor checks, request order, and event mechanics remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S5 JSONL provider-owner projection increment (2026-10-22):** The JSONL session and pending Nature result adapters now read event, run, combat RNG, and semantic deck data through their stochastic owners. Response shapes and advertised session capabilities are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 exact trace provider-owner increment (2026-10-22):** Exact trace replay now compares RunState prototype providers through `RunStochasticStateV111`. Trace serialization, RNG-before/after evidence, and replay result semantics are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic pilot provider-owner read increment (2026-10-22):** Semantic combat validation, action and continuation guards, Native policy replay comparison, snapshot replay, and JSONL capability/observation reporting now read seeded-entry and provider audit data through `SemanticPilotStochasticStateV111`. The flattened pilot members remain compatibility projections; snapshot and response shapes, provider decisions, and combat mechanics are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic pilot run-provider read increment (2026-10-22):** Pilot provenance validation, reward publication, and JSONL session observations now read semantic OtherChance and reward audits through `RunStochasticStateV111`. The flattened pilot properties remain compatibility projections and writes still publish through the existing setters. Response and snapshot shapes, provider decisions, and mechanics are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S0 registry coverage and law-status increment (2026-10-22):** The session capability registry now composes Native combat reward sites through its existing source-owned session-site alias, allowing the inventory to trace the capability to the same session owner without duplicating IDs. The later-act Darv/Hive assignment row now explicitly labels its uncalibrated equal-index law `UNVERIFIED`. Static inventory review and `git diff --check` only; no tests or builds were run.

**S0 row-facet locator completion increment (2026-10-22):** The migration rows for reward-card and potion candidates now state their fair-observation boundary, while later-act Darv/Ancient rows identify their configured route scope and rejected owner/support cases. The checked locator now finds evidence pointers for all five facets on 185/185 registered rows; this closes locator coverage, not manual source-completeness review. Static inventory review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature choice-session cursor read increment (2026-10-22):** Trash Heap, Infested Automaton, Tablet of Truth, and Neow offer sessions now read initial/current Nature cursors through their stochastic owner for sampling, replay validation, and snapshot construction. Flat snapshot members and compatibility projections remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature map and later-act cursor read increment (2026-10-22):** Map-generation audits and sessions plus later-act preparation now read cursor, typed request, transcript, and law state through their stochastic owners for validation, continuation sampling, audit projection, and snapshot construction. Flat snapshot members and compatibility projections remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature Trial and event-choice cursor read increment (2026-10-22):** Trial, Glory Reflections, and Endless Conveyor sessions now read their cursors through the stochastic owner for sampling, replay validation, derived provider decisions, and snapshot construction. Flat snapshot members and compatibility projections remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature Punch Off cursor read increment (2026-10-22):** Punch Off fight and reward sessions now read their cursor through the stochastic owner for conditioned sampling, replay validation, derived provider decisions, and snapshot construction. Flat snapshot members and compatibility projections remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native event-shuffle cursor read increment (2026-10-22):** Native event and combat-end shuffle sessions now read their initial/current cursor through the stochastic owner for provider-prefix derivation, continuation sampling, both replay validators, and snapshot checks. Existing event site/action fields and flat snapshot members remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 common draw-choice resume increment (2026-10-22):** `SemanticChoiceResumeV111` now owns the complete standalone/automatic draw-hook and Stratagem selection resume, including the saved hand-draw cursor handoff. The `PrototypeGameEngine` methods remain delegating facades, and `SemanticCombatDrawV111` uses the shared owner directly while retaining its typed histories. Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature result-audit owner read increment (2026-10-22):** Overgrowth and Underdocks run results, Native encounter-formation results, and nested map audits now read initial/current cursors and Nature transcripts through their stochastic owners during replay validation. Law arrays, provider audit choices, result projections, and serialized fields retain their existing owners and shapes. Static source review and `git diff --check` only; no tests or builds were run.

**S3 later-act result-audit owner read increment (2026-10-22):** Experimental later-act Ancient assignments and encounter-bag preparation now read their initial/current Nature cursors and decision transcripts through the shared stochastic owner during validation and boss-result replay. Chance Laws, prepared bags, and legacy RNG checkpoints retain their existing owners. Static source review and `git diff --check` only; no tests or builds were run.

**S3 run-initialization transcript owner read increment (2026-10-22):** Configured run-initialization replay now reads its cursor and decisions through the stochastic owner, including the cursor boundaries nested around the Act 1 map audit. Chance Laws, typed map-provider decisions, and existing snapshot members retain their existing owners. Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature provider collector owner increment (2026-10-22):** Overgrowth and Underdocks boss providers, Native encounter formation, and both experimental later-act providers now store their current Nature cursor and decision transcript in `ReferenceNatureStochasticStateV111`. Its shared `Advance` operation appends a decision while moving the cursor; provider-specific Chance Law lists, saved-prefix checks, pending-choice behavior, and request validation remain in place. Static source review and `git diff --check` only; no tests or builds were run.

**S3 run-initialization builder owner increment (2026-10-22):** Configured run setup now accumulates Act 1 and later-act Nature initialization segments through `ReferenceNatureStochasticStateV111`, removing the separate decision-list builders. `Append` requires each segment's initial cursor to match the current cursor before joining its decision history; Chance Laws and nested audit data retain their existing owners. Static source review and `git diff --check` only; no tests or builds were run.

**S2 random-boundary journal ownership increment (2026-10-22):** `RandomBoundaryJournalV111` now stores only the globally ordered mixed transcript. Its index, float, and Gaussian event lists are typed projections; small per-operation sequence counters preserve their established numbering. Capture order, event payloads, and Native-policy/fidelity interfaces remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic hand-draw owner read increment (2026-10-22):** Semantic automatic-hand-draw and semantic draw-cost entry, progress, replay, and completion now read tagged progress through `PrototypeAutomaticPipelineContinuationState.Stochastic`; draw-stage and opening-cursor checks read through its mechanics owner. Flat continuation fields remain snapshot projections. Static source review and `git diff --check` only; no tests or builds were run.

**S5 Python bridge negotiation increment (2026-10-22):** The Python session handshake now rejects partial or inconsistent stochastic capability advertisements, checks session schemas and supported site lists against the negotiated manifest, and validates Chance Law query coverage. Direct Chance Law queries and `observe_nature_choice` share envelope validation for Chance Law schema, identity, probability status, total weights, support fingerprints, and law identifiers; direct queries also verify the returned site and ordinal, while the Nature-choice path checks event identity, conditioning fingerprints, and provenance. This strengthens bridge contract acceptance without claiming broader cross-mode observation coverage. Static source review and `git diff --check` only; no tests or builds were run.

**S5 shared fair-observation frame increment (2026-10-22):** Added the opt-in `fair_observe` envelope for ordinary run, controlled-combat, and semantic-combat handles. All three use `PrototypeAiEnvironment` and expose the unchanged public observation schema, its hash, and the matching legal action menu under one versioned frame. Python negotiates and validates that envelope, then uses it for the existing `observe()` and `actions()` methods when available; older handle-specific operations remain available as fallback. Static source review and `git diff --check` only; no tests or builds were run.

**S5 semantic ReferenceNature observation increment (2026-10-22):** Added semantic ReferenceNature handles to the shared `fair_observe` frame and exposed a typed Python session wrapper for begin, status, legal player steps, automatic Nature resolution, fork, snapshot, restore, and release. The player view continues to use `PrototypeAiEnvironment`; the sampler cursor remains behind the separate Nature-only interface. The Python handshake accepts additive advertised view kinds while retaining required baseline views and exact schema negotiation. Static source review and `git diff --check` only; no tests or builds were run.

**S5 semantic ReferenceNature operation negotiation increment (2026-10-22):** The Python handshake now requires the top-level ReferenceNature session schema to match the stochastic manifest and verifies that the manifest advertises every `semantic_reference_*` operation used by the typed wrapper. Partial session contracts fail during startup instead of at the first operation call. Static source review and `git diff --check` only; no tests or builds were run.

**S5 semantic ReferenceNature dual-view binding increment (2026-10-22):** `SemanticReferenceCombat.observe_nature_choice()` now routes the same session handle to the existing negotiated `reference_combat` Nature-only view. The fair observation/action frame remains on `observe()`/`actions()`, keeping player-visible state separate from pending Chance Law support. Static source review and `git diff --check` only; no tests or builds were run.

**S1 controlled-choice Chance Law input audit (2026-10-22):** Source review of `QueryControlledRunAction`, `QueryControlledEventChoice`, and the Stone of All Time ticket law confirms these queries derive identity and conditioning from the saved pending request, candidate support, prior effect prefix, and saved mechanics boundary. The Stone route check reads whether the saved run has a Native provider, but no provider cursor or sampler state. The reviewed query builders contain no call-count, cursor-byte, or sampler-state reads and do not invoke sampling. This closes only the source review for these controlled-choice laws; proof-by-execution and the broader S1 acceptance gate remain open. No tests or builds were run.

**S0 Native request construction decoupled from direct query coverage (2026-10-22):** `NativeRandomChoiceV111` now constructs each typed request and validates its Native operation family through provider request validation. Native index, bounded-integer, and unit-float construction no longer checks the direct Chance Law query allowlist; Native operation registration and query capability coverage remain separate. Static source review and `git diff --check` only; no tests or builds were run.

**S0 Native primitive site registry separation increment (2026-10-22):** The Native index allowlist now composes Native source-operation sites, semantic-choice sites, Native event-context indices, combat intent sites, and bounded-operation sites without using the direct Chance Law query manifest. The Native Gaussian-integer SiteIds now live in `StochasticSiteRegistryV111`, and the Chance Law query API projects that shared list. Row-level boundary review found and registered the Fishing Rod persistent-combat callback, which had previously entered Native validation only through the Chance Law query allowlist. Remaining Native request-site completeness review is open; no tests or builds were run.

**S0 Native site ownership inversion increment (2026-10-22):** Native event gold, Stone of All Time/Future of Potions, event card-pick, Punch Off, and Native event-shuffle SiteIds now live in `StochasticSiteRegistryV111`; Chance Law query arrays and the related session site lists project those shared entries. The registry now reads Neow, Trash Heap, Tablet of Truth, Trial, and Native event-shuffle site lists from their owning sessions instead of Chance Law aliases, and Neow validates against its own session list. Native index validation now checks shape and the Native operation registry directly, while generic request validation retains the legacy/query union; ordinal-scoped resolution selects the provider before applying its registration check. The Chance Law query-site manifest uses a lazy nested registry, so Native registration does not initialize Chance Law coverage. The ownership inventory now describes Native registration separately from direct query coverage. Row-level Native source-site completeness and law calibration remain open; static review and `git diff --check` only, no tests or builds were run.

**S4 Native card-shortlist stream attribution increment (2026-10-23):** Semantic shortlist-choice events for random shortlist, autoplay, and replay-card sites now use `run.combat_card_selection` when the Native combat RNG sidecar is present; prototype-only runs retain `prototype.combat`. Existing event IDs remain stable, and pilot restore preserves saved prototype-labeled decisions while validating them against Native-backed pending events. Native probability and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Native random hand-cost stream attribution increment (2026-10-23):** Semantic hand-card identity choices now use `run.combat_card_selection`, and bounded random energy-cost events use `run.combat_energy_costs` when the Native combat RNG sidecar is present. Prototype-only runs retain `prototype.combat`; event IDs remain stable and pilot restore preserves saved prototype-labeled choices. Native probability and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native run-stream sample publication check increment (2026-10-23):** `NativeRunRngV111.SampleNextInt` now validates the returned state after publishing the advanced run bundle and, during combat, its matching combat-sidecar bundle. This verifies the pair for this explicit sampling API; full fork-and-publish coverage for other Native RNG lifecycle paths remains open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native combat-step RNG reconciliation check increment (2026-10-23):** `NativeRunRngV111.ReconcileAfterStep` now validates the returned run state after reconciling the ordinary engine's post-step run and combat RNG bundles. Native combat exit continues to retain the run owner's post-hook cursors. Other internal mutable RNG transition sites remain to be migrated. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native RNG owner projection increment (2026-10-23):** `NativeRunRngV111` and `NativeCombatRngEntryV111` now read run and combat providers through defensive public projections. Sampling and creature-local cursor updates operate on local forks and publish through the stochastic owners; the broader Core transition paths still use internal mutable views and remain to be migrated. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic intent and replay projection increment (2026-10-23):** Semantic pilot reward audits, later-act ReferenceNature session inputs, run-start replay comparisons, and semantic intent-resolution adapters now use defensive prototype-RNG projections. The Native committed-intent audit passes an empty legacy bundle because its validated combat owner supplies `run.monster_ai`; its result and provider behavior are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 configured run-start RNG publication increment (2026-10-23):** Run configuration now takes its Native run bundle from the defensive owner projection and builds experimental later-act encounter bags against a local prototype RNG copy. It publishes that bundle through `RunStochasticStateV111.WithPrototypeRng` after successful preparation, keeping run-start mechanics and the advanced cursor paired. Static source review and `git diff --check` only; no tests or builds were run.

**S3 provider-inspection projection increment (2026-10-23):** Native route checks and cursor inspections in Chance Law, ReferenceNature sessions, controlled-choice validation, Native event-context adapters, combat-entry guards, semantic-choice event builders, and semantic-pilot provenance/boundary validation now read defensive provider projections. The edits do not advance a provider or alter its routing; broader mutating engine paths remain open for fork-and-publish migration. Static source review and `git diff --check` only; no tests or builds were run.

**S3 out-of-combat potion RNG publication increment (2026-10-23):** Out-of-combat potion effects now receive local copies of the prototype and optional Native run bundles. The engine publishes the resulting provider cursors through `RunStochasticStateV111` in the same returned state as the player effect. Static source review and `git diff --check` only; no tests or builds were run.

**S3 event-continuation RNG publication increment (2026-10-23):** Deferred event card creation and the legacy Stone of All Time ticket now advance local prototype RNG bundles, then publish the advanced provider together with the event continuation. Native ticket routing continues through `NativeEventContextRngV111`. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native event-context legacy fallback publication increment (2026-10-23):** Legacy index, bounded-integer, and Fisher-Yates fallbacks in `NativeEventContextRngV111` now mutate a local prototype bundle and publish it with the returned run state. Native event-context cursor routing is unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 event reward and persistent relic RNG publication increment (2026-10-23):** The Future of Potions and Glass Eye reward offers, legacy Stone of All Time lift ticket, and prototype War Hammer victory shuffle now advance local prototype RNG bundles and publish their cursors with the associated reward or deck result. War Hammer's Native path reads defensive run/combat projections and keeps its existing paired Native cursor publication. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Neow relic-pickup RNG publication increment (2026-10-23):** Neow relic-pickup continuations now fork prototype and optional Native run bundles, pass a separate prototype snapshot to deferred reward construction, and publish both advanced providers with the resulting reward/player state. Random relic acquisition, relic hooks, and deferred card grants share the local prototype cursor. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Toy Box acquisition RNG publication increment (2026-10-23):** Toy Box reward generation now passes an explicit prototype RNG bundle to its shared wax-relic reward builder and publishes the local prototype and optional Native run cursors with each resulting reward state. The deferred event-reward caller retains its existing transition-owned bundle while using the same explicit builder input. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Trial event RNG publication increment (2026-10-23):** Trial outcome resolution now uses a local prototype RNG bundle for curse grants, relic acquisition hooks, and reward-card offers, then publishes it with the event/player transition. The optional Native run bundle is likewise read through its defensive projection and published when the outcome advances it. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Glory Reflections RNG publication increment (2026-10-23):** Shatter and Round Tea Party outcomes now resolve relic hooks and card additions against a local prototype RNG bundle, then publish it with the resulting player, world, and event state. The Round Tea Party relic draw also uses a defensive Native run projection and publishes its advanced cursor with the result. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Dense Vegetation and Tea Master RNG publication increment (2026-10-23):** Dense Vegetation's generated-gold and rest-relic paths, plus Tea Master's relic-purchase hooks, now advance a local prototype RNG bundle and publish it with their run-state result. Existing stored Native event-gold outcomes remain authoritative and consume no draw. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Glory and Hive event RNG publication increment (2026-10-23):** Hungry for Mushrooms, Tinker Time, Infested Automaton, and Colossal Flower now pass local prototype RNG bundles into relic hooks or card grants and publish the advanced provider with their event result. Hungry for Mushrooms publishes the relic-hook cursor before its subsequent event shuffle so either provider path continues from the correct state. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Battleworn and Driftwood reward RNG publication increment (2026-10-23):** Battleworn Dummy's potion and relic reward offers and Driftwood's card-group rerolls now use local prototype RNG bundles and publish the resulting cursor with the reward state. Their optional Native run cursors are read through defensive projections and published with relic draws or encounter-card generation. Static source review and `git diff --check` only; no tests or builds were run.

**S3 War Historian Repy RNG publication increment (2026-10-23):** Repy's relic-grant hook and chest-offer generation now use local prototype and optional Native run bundles, publishing the resulting cursors with the event's saved rewards and continuation. Native relic/potion offer routing remains unchanged; prototype fallback draws publish their advanced legacy cursor. Static source review and `git diff --check` only; no tests or builds were run.

**S3 room and Trash Heap reward RNG publication increment (2026-10-23):** Overgrowth treasure-room relic offers, Dream Catcher rest-site card rewards, Fake Merchant relic purchases, and Trash Heap card/relic choices now use local prototype RNG bundles and publish the advanced cursor with the resulting reward or event state. Treasure and rest-site Native run cursors are also published through the run stochastic owner. Static source review and `git diff --check` only; no tests or builds were run.

**S3 deferred event reward RNG publication increment (2026-10-23):** Deferred reward builders now take their prototype RNG bundle explicitly. Neow pickup rewards, event reward offers, Calling Bell, and Punch Off publish the local prototype and optional Native run cursors with their reward continuation; event reward construction no longer accesses the run owner's internal mutable prototype view. Static source review and `git diff --check` only; no tests or builds were run.

**S3 Underdocks remaining-event RNG publication increment (2026-10-23):** Endless Conveyor's dish ticket and dish effects, plus Punch Off combat HP offsets and custom rewards, now advance local prototype RNG bundles and publish them with the event, combat, or reward result. Optional Native run routing is read through defensive projections and publishes with Punch Off rewards. Static source review and `git diff --check` only; no tests or builds were run.

**S3 persistent combat-end RNG publication increment (2026-10-23):** Persistent relic combat-end hooks now read defensive run and combat RNG projections, operate on local prototype and Native run bundles, and publish their cursor updates together with the resulting player, combat, and run state. The existing run/combat Native cursor validation remains in place. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat autoplay and run-factory projection increment (2026-10-23):** History Course and Whispering Earring autoplay now use local prototype RNG bundles and publish them with the resulting combat state. Overgrowth, Underdocks, and later-act fixture factories derive their copied prototype bundle through defensive owner projections, and ReferenceNature initialization audits use defensive projections for their legacy cursor checks. Static source review and `git diff --check` only; no tests or builds were run.

**S3 map-room transition RNG publication increment (2026-10-23):** Unknown map-room resolution now uses a local prototype bundle and publishes its cursor before room entry. The experimental later-act encounter selector reads the defensive projection while updating only its saved encounter bag, because its source path does not consume that legacy stream. Static source review and `git diff --check` only; no tests or builds were run.

**S3 event-entry RNG publication increment (2026-10-23):** Event selection and legacy event gold/potion-slot generation now share a local prototype RNG bundle and publish it before event-entry Native providers resolve their saved outcomes. Native run-provider routing and stored outcome order remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S3 shop inventory RNG publication increment (2026-10-23):** Shop inventory generation, card/potion price rolls, relic and potion offers, and ShopEntered relic hooks now share local prototype and optional Native run bundles. Both advanced providers publish with the completed shop state before the Lords Parasol continuation is applied. Static source review and `git diff --check` only; no tests or builds were run.

**S3 rest-site RNG publication increment (2026-10-23):** Byrdonis Egg hatching and RestSiteHealed relic hooks now use a local prototype bundle, while Shovel offers use local prototype and optional Native run bundles. The resulting cursor publishes with the completed rest action or reward state. Static source review and `git diff --check` only; no tests or builds were run.

**S3 shop action RNG publication increment (2026-10-23):** Shop purchases and pending relic deck-choice resolution now use local prototype and optional Native run bundles and publish them with the returned shop state. Card grants and relic acquisition hooks retain their existing action order and Lords Parasol continuation. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat reward RNG publication increment (2026-10-23):** Combat reward population now uses local prototype and optional Native run bundles across potion-pity, gold, potion, card, relic, Black Star, and boss-relic offers, then publishes both cursor owners with the initialized reward state. Static source review and `git diff --check` only; no tests or builds were run.

**S3 custom reward completion RNG publication increment (2026-10-23):** Custom event reward completion now uses a local prototype bundle for acquired relic cards, follow-up card grants, and random curse grants. It publishes the advanced cursor with the returned event or reward state; suspended controlled curse choices retain their opening cursor until resume. Static source review and `git diff --check` only; no tests or builds were run.

**S3 reward-step RNG publication increment (2026-10-23):** Reward card grants, relic-acquisition hooks, and relic deck-choice follow-ups now use local prototype and optional Native run bundles, then publish their advanced cursors with ordinary reward progress, reward completion, or lethal outcomes. Controlled continuations and dedicated event reward actions keep their existing transition owners. Static source review and `git diff --check` only; no tests or builds were run.

**S3 generic event-choice RNG publication increment (2026-10-23):** Generic event effects and event deck-card choices now use local prototype and optional Native run bundles and publish them with completed effect checkpoints and resulting event progress. Early lethal effects preserve updated world bags and card-instance cursors; controlled chance suspensions save the cursor from the start of the active effect for resumption. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat-entry RNG publication increment (2026-10-23):** Encounter selection, pre-combat potion relics, initial shuffle and HP draws, and first-intent commitment now advance local prototype/Native run bundles and publish through their stochastic owners. Initial intent capture and resume use defensive combat/run projections for Native cursor handoff. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat action and victory RNG publication increment (2026-10-23):** Card play, potion use, automatic death prevention, and combat-win dispatch now use local prototype bundles and publish with their resulting state. Victory dispatch publishes before persistent combat-end relic hooks; Mad Science then advances the defensive projection after those hooks. Static source review and `git diff --check` only; no tests or builds were run.

**S3 automatic combat dispatch RNG publication increment (2026-10-23):** Imbued autoplay operations and the opening player-turn event dispatch now use local prototype bundles and publish them with combat progress, including suspended choice results. Static source review and `git diff --check` only; no tests or builds were run.

**S3 automatic turn-step RNG publication increment (2026-10-23):** Automatic combat steps now use a local prototype bundle across event dispatch, hand effects, enemy statuses/summons/actions, intent commitment, and hand draw. The cursor publishes with completed steps and saved draw continuations; the late autoplay stage refreshes from its dedicated transition. Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled recycle RNG publication increment (2026-10-23):** Controlled hand-draw recycle resumption now uses a local prototype bundle for after-shuffle relic hooks and remaining draws, publishing it before a stratagem or hand-draw continuation resumes. Static source review and `git diff --check` only; no tests or builds were run.

**S3 defensive Native combat RNG read increment (2026-10-23):** Combat intent, enemy formation/summon, targeting, card/potion selection, draw-cost, generation, insertion, and shuffle paths now read the Native combat RNG through its defensive projection and publish updated combat owners through `WithNativeRng`. `PrototypeCombat.cs` no longer accesses `NativeRngMutable`. Static source review and `git diff --check` only; no tests or builds were run.

**S3 combat choice-resume RNG publication increment (2026-10-23):** Pending card selection, controlled target resumption, and controlled semantic card-effect draws now use local prototype bundles and publish them before resuming saved effects or the automatic pipeline. `PrototypeCombat.cs` no longer accesses `PrototypeRngMutable`. Static source review and `git diff --check` only; no tests or builds were run.

**S3 opening combat RNG publication increment (2026-10-23):** Opening combat event dispatch and hand draw now use local prototype bundles and publish them with their stage results, including the optional post-shuffle Stratagem continuation. Static source review and `git diff --check` only; no tests or builds were run.

**S3 room completion, Pael reward, and map transition RNG publication increment (2026-10-23):** Room-completion relic hooks, Pael's reward alternatives, returned saved cards, cloned rest cards, and act-transition map generation now use local prototype bundles and publish the updated cursor with the resulting state. Run start and hypothetical stream replacement now read defensive projections. Static source review and `git diff --check` only; no tests or builds were run.

**S3 controlled combat continuation RNG publication increment (2026-10-23):** Controlled random-cost resumes, potion and generated-card choices, card shortlists, and full-pool shuffle resumes now use local prototype bundles and publish their cursor before automatic-pipeline continuation. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic and automatic draw RNG publication increment (2026-10-23):** Semantic card draws, drawn-card event hooks, controlled draw insertions, and automatic hand-draw resumes now carry a local prototype bundle through nested continuations and publish it at each resulting run-state boundary. Native semantic recycle now reads the defensive combat RNG projection. Static source review and `git diff --check` only; no tests or builds were run.

**S3 composed run-snapshot v4 and RNG projection closeout (2026-10-23):** `RunSnapshot.Save` now serializes `RunState.Simulation` as the v4 mechanics/provider envelope, and `Load` retains flattened v1–v3 compatibility. The JSONL hello capability advertises the current and supported snapshot schemas; the existing integrity fixture follows the new mechanics path. Core transition callsites now use defensive projections, local forks, and owner publication, with mutable RNG accessors confined to their stochastic owner implementations. `git diff --check` passed; no tests or builds were run.

**S5 exact snapshot client negotiation increment (2026-10-23):** The Python bridge now accepts the current snapshot schema advertised by `hello`, validates the supported schema list, and checks saved and loaded run snapshots against the negotiated set while preserving compatibility with earlier v1-only handshakes and their separate v2/v3 identifiers. Added JSON Schema contracts for the missing v2 and current v4 exact-snapshot envelopes. Static source review and `git diff --check` only; no tests or builds were run.

**S3 mutable RNG projection encapsulation closeout (2026-10-23):** The run RNG mutable projections are now private to `RunStochasticStateV111` for its fork and validation paths; the unused combat mutable projection was removed. Core transition code can access only defensive public projections and owner publication APIs. Static source review and `git diff --check` only; no tests or builds were run.

**S4 autoplay Native target handoff clarification (2026-10-23):** Source review confirms that History Course and Whispering Earring autoplay share `ResolveRandomEnemyTarget`. Native-backed combat advances the `run.combat_targets` cursor in the combat sidecar, and ordinary step finalization reconciles the corresponding run bundle; each path separately publishes its local prototype cursor. This records existing provider ownership and does not close Native law or cursor calibration, or the broader cross-provider equivalence gate. Static source review and `git diff --check` only; no tests or builds were run.

**S6 Native Rest Heal reward continuation increment (2026-10-23):** The existing ReferenceNature run-action session now captures configured Native Act 1 Rest Heal rarity floats, encounter-card model indices, and upgrade floats through one typed provider prefix. Rarity laws use the offset derived from preceding raw-rarity outcomes; exact request replay binds the prefix to the legal action and commits the resulting reward with the Nature cursor. The existing serialized combat-entry provider-decision member remains compatible as the shared typed run-action transcript. Static source review and `git diff --check` only; no tests or builds were run.

**S5 legacy Native Ancient transition replay-capture increment (2026-10-23):** Completed legacy Native Ancient transitions now replay the saved Nature map prefix, rerun the assigned Ancient through its existing audited Native opening generator, and require the committed run to equal the saved result before capturing the Native request outcomes and cursor spans after the map decisions. The existing run-action session and capture schemas are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 automatic hand-draw result boundary increment (2026-10-23):** Controlled and semantic automatic-draw adapters now validate `DrawCards` results through `ValidateAutomaticHandDrawResult` before interpreting provider-specific continuations. The shared owner checks the DrawPlayerHand cursor and that the returned complete, cost, recycle, semantic-choice, insertion, Stratagem, or CardDrawn boundary matches its mechanics state; the controlled adapter drops repeated checks. Provider-specific continuation and transcript policies remain with their adapters. Static source review and `git diff --check` only; no tests or builds were run.

**S4 shared DrawCards result validation increment (2026-10-23):** `ValidateDrawCardsResult` now validates every controlled or semantic draw result, including standalone and nested semantic effect draws; automatic segments additionally require the DrawPlayerHand checkpoint. One mechanics boundary validator now guards the tagged result before all of these provider adapters route it. Static source review and `git diff --check` only; no tests or builds were run.

**S4 DrawCards result owner increment (2026-10-23):** The shared `DrawCards` kernel now constructs all tagged outcomes through `CreateDrawCardsResult`, which validates them before any adapter can route the result. Only true automatic pipeline calls require the DrawPlayerHand cursor; effect and standalone draws receive the same boundary-shape checks without acquiring a pipeline owner. Adapter-side duplicate validation was removed. Static source review and `git diff --check` only; no tests or builds were run.

**S0 Ancient-offer manual review increment (2026-10-23):** The scoped inventory now traces Darv's variant-plus-relic shuffle and the Nonupeipe, Tanx, and Vakuu Act 3 offer shuffles through their existing conditional Ancient-offer session. Reconciliation against the 185-ID matrix found three IDs already represented in the scoped rows but omitted from the previous declared total; coverage is now 110/185, with 75 remaining. The Ancient source audit now distinguishes source-verified bounded-index transforms from the still-unverified complete conditional laws, pool fidelity, stream attribution, and cursor parity. Static source review and `git diff --check` only; no tests or builds were run.

**S0 Native merchant manual review increment (2026-10-23):** Eleven Shop site IDs now trace the sale slot, character-card models and inert upgrade roll, card/potion/relic price modifiers, and the Nature-only hidden relic identity through the existing atomic merchant session. The review distinguishes configured `player.shops` Native requests, the retained Native-regional `shop` fallback, and the unverified Nature relic-queue identity law. Scoped manual review coverage is 121/185, with 64 remaining; source pool completeness, Native attribution and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S0 legacy-map manual site review increment (2026-10-23):** The scoped
inventory traces the ten `map.legacy.*` requests through prototype floor
width, adjacent-layer edge construction, room assignment, and the existing
legacy `start_run` / unconfigured `continue_act` continuation. The Native map
generator remains source-owned; legacy equal-index weights, source geometry,
and call-order parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S0 event-reward manual site review increment (2026-10-23):** The five
`event.reward.*` rows now trace legacy event-effect pools, optional potion
offers, both Scroll Boxes shuffles, and the post-reward modifier-curse frame.
The existing controlled event-choice and Native Neow event-relic sessions own
the documented replay boundaries; Wellspring and Whispering Hollow retain
separate potion sites, and Native curse ownership, source pools, stream
attribution, and legacy weights remain open. Scoped manual review coverage is
136/185 stable IDs, with 49 remaining. Static source review and
`git diff --check` only; no tests or builds were run.

**S0 later-act Ancient assignment manual review increment (2026-10-23):**
The three `event.later_act.*` rows now trace the conditional shared-Darv
assignment, the bound-one Glory request, and each act's ordered Ancient pool
through configured experimental run preparation. The pinned source audit now
records the existing saved preparation continuation and its replayed decision
prefix; Native `UpFront` ordering and full source-prefix parity remain open.
Scoped manual review coverage is 139/185 stable IDs, with 46 remaining. Static
source review and `git diff --check` only; no tests or builds were run.

**S0 Act 2 Ancient-offer manual site review increment (2026-10-23):**
The Orobas, Pael, and Tezcatara offer rows now trace their ordered conditional
pools and three-choice replay through the existing atomic Ancient-offer
session. The source ledger now reflects the configured experimental Ancient
assignment and saved preparation work, while Native `UpFront` parity and full
pool fidelity remain open. Scoped manual review coverage is 142/185 stable
IDs, with 43 remaining. Static source review and `git diff --check` only; no
tests or builds were run.

**S0 Repy chest-offer manual site review increment (2026-10-23):** The two
Repy offer rows now trace its source-ordered relic shuffle, independent
with-replacement potion picks, and the existing atomic event-shuffle session
for configured Native and Native-regional fallback routes. The stale pinned
audit note now points to this continuation; source pool fidelity and
`player.rewards` ownership remain open. Scoped manual review coverage is
144/185 stable IDs, with 41 remaining. Static source review and
`git diff --check` only; no tests or builds were run.

**S0 generic event-effect and Infested Automaton review increment (2026-10-23):** The random-upgrade and potion-fill rows now trace their ordered eligible supports through the saved event-effect continuation; potion filling also records the existing legal Shop-purchase capture. Infested Automaton now traces its `study`/`touch_core` conditioned card support through the atomic ReferenceNature choice session, including its Native-regional legacy fallback. The restricted Silent pool, legacy equal-index laws, source-pool completeness, and seeded result/cursor parity remain open. Scoped manual review coverage is 147/185 stable IDs, with 38 remaining. Static source review and `git diff --check` only; no tests or builds were run.

**S0 potion-generation and Unknown-room review increment (2026-10-23):** The three legacy potion SiteIds now trace their rarity/model support and existing event-choice, run-action, combat-reward, and merchant capture boundaries; the pinned potion audit was corrected to match those continuations. Unknown-room odds now trace the 1,000-ticket legacy regional map-entry continuation, including the RNG request that still occurs when Event-only odds force the room type. Native float mapping, pool completeness, equal-index weights, Unknown-room ticket probabilities, and Native cursor parity remain open. Scoped manual review coverage is 151/185 stable IDs, with 34 remaining. Static source review and `git diff --check` only; no tests or builds were run.

**S0 relic-acquisition model manual review increment (2026-10-23):** Random Rare-card, random-potion, and potion-slot-fill sites now trace their ordered pool and slot supports through the event-effect, configured merchant, Native event-relic, and legacy run-action/reward pickup boundaries that currently admit them. Other relic acquisition callers remain unsupported; Native `player.rewards` ownership, pool completeness, legacy equal-index weights, and cursor parity remain open. Scoped manual review coverage is 154/185 stable IDs, with 31 remaining. Static source review and `git diff --check` only; no tests or builds were run.

**S0 Ancient relic acquisition-effect review increment (2026-10-23):** Beautiful Bracelet, Distinguished Cape, and Fur Coat now trace their eligible physical-card, shrinking curse, and current-act map-node supports through the existing event-choice and relic-acquisition continuations. Declared Native bounded-index transforms are exact; legacy equal-index laws, complete pools, stream attribution, and cursor parity remain open. Scoped manual review coverage is 157/185 stable IDs, with 28 remaining. Static source review and `git diff --check` only; no tests or builds were run.

**S0 legacy Shop offer review increment (2026-10-23):** The eight card, Colorless, potion, and relic offer/price SiteIds now trace their legacy ordered supports through the Shop-node run-action continuation. The atomic Native merchant entry and its Native-regional fallback use their separate merchant, PotionFactory, and relic-bag requests; the shared Colorless SiteId remains explicitly scoped to its filtered merchant pool. Legacy pool completeness, equal-index weights, source call order, and Native parity remain open. Scoped manual review coverage is 165/185 stable IDs, with 20 remaining. Static source review and `git diff --check` only; no tests or builds were run.

**S0 Treasure relic-pool review increment (2026-10-23):** The legacy Treasure identity site now traces its ordered unowned pool through run-action replay. The configured Native Treasure law and atomic session now trace rarity, fallback tier, and sorted hidden-bag identity; the shared identity SiteId is also linked to its supported Shovel, Calling Bell, Toy Box, combat-reward, and event-relic sessions. Compact hidden-queue identity, retained equal-index laws, source queue completeness, and cursor parity remain open. Scoped manual review coverage is 168/185 stable IDs, with 17 remaining. Static source review and `git diff --check` only; no tests or builds were run.

**S0 combat draw and effect review increment (2026-10-23):** Hand-return shuffle and after-shuffle relic insertion now share their existing continuation review; Anointed's physical Rare-card shuffle, full-pool card generation, random potion selection, semantic deck reveal, and Sly auto-target each trace their current support and resolver. Gameplay peek/scry source callsites remain unintegrated, and potion-pipeline mechanics regression remains pending. Pool completeness, stream attribution, seed frequencies, and cursor parity remain separate. Scoped manual review coverage is 175/185 stable IDs, with 10 remaining. Static source review and `git diff --check` only; no tests or builds were run.

**S0 final stable-site manual review increment (2026-10-23):** Improvement, Fishing Rod, Pael's Tooth, War Hammer, Pael's Legion, Sand Castle, relic-queue shuffling, persistent-card transformation, the controlled-playable fixture opening shuffle, and the diagnostic monster-local sampler now have scoped source/continuation notes. The fixture and diagnostic rows explicitly carry no game-source probability claim. All 185 stable matrix IDs now have scoped manual review pointers; this closes the inventory's review-coverage count, not S0 source-fidelity acceptance. Source-pool completeness, trigger coverage, stream/cursor parity, legacy law calibration, and the Native reference-mod/Fidelity Inspector evidence gate remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 ReferenceNature run-start relic-pool ownership increment (2026-10-23):** `V111RunFactory.Initialize` now creates canonical `NatureUnordered` rarity sets when a ReferenceNature cursor or configured ReferenceNature run-start scope owns the run. It skips constructing the Native `run.treasure_room_relics` queue permutation and canonicalizes any existing rarity support through the same relic-bag helper used by Nature replay; ordinary Native starts retain the existing Native Fisher–Yates queues. Existing Treasure, merchant, reward, event, and Shovel pull continuations consume the unordered sets. Source-pool fidelity, hidden identity probabilities, and Native cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**D2 weighted intent audit metadata increment (2026-10-23):** Opt-in Native committed-intent audits now pair each sampled primitive ticket with the typed weighted event support, selected move branch, and exact ticket interval member. The source selector and Native draw path are unchanged, and the primitive transcript remains available beside the semantic outcome. The event records source-shaped, unverified weights; eligibility completeness, next-turn Native result/cursor regression, and broader semantic outcomes for other provider sites remain open. Static source review and `git diff --check` only; no tests or builds were run.

**D2 next-turn intent audit access increment (2026-10-23):** The isolated public Native intent audit now accepts the registered next-turn intent site as well as the initial-commit site, exposing the same paired primitive and weighted outcome records for callers auditing `AdvanceTurn` intent selection. This does not run the rest of turn advancement or close its Native result/cursor regression. Static source review and `git diff --check` only; no tests or builds were run.

**D2 source-route semantic audit retention increment (2026-10-23):** The Native-policy source-route baseline now stores the audited initial intent outcomes in `SemanticNativePolicyAuditV111` and validates each outcome against its exact primitive request, bound, sampled ticket, and Native cursor span. The owner deep-forks the semantic records with the existing Native index list and mixed provider trace. Later-turn audits and Native parity gates remain separate. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native intent journal annotation increment (2026-10-23):** The existing random-boundary journal now carries weighted intent outcomes as typed annotations paired with their exact provider decisions alongside its unchanged chronological primitive transcript. Native-policy player transitions append these annotations to their provider-private audit state, allowing intent outcomes committed during later turn advancement to survive snapshot and fork validation even when request ordinals repeat across turns. Static source review and `git diff --check` only; no tests or builds were run.

**D2 weighted intent audit completeness increment (2026-10-23):** Native intent commits that request weighted audit metadata now verify that every sampled bounded ticket has exactly one weighted outcome annotation before publishing the commit. The guard makes future selector branches fail closed if they add a draw without exposing source support. Static source review and `git diff --check` only; no tests or builds were run.

**D2 weighted intent restore completeness increment (2026-10-23):** Native-policy audit validation now checks the retained mixed provider trace against its weighted intent annotations and rejects a partial annotated set. Snapshots that predate weighted annotations and contain none remain readable. Static source review and `git diff --check` only; no tests or builds were run.

**D2 weighted intent audit schema increment (2026-10-23):** Newly created Native-policy audits now carry an additive schema marker that enforces restore-time completeness even if all weighted annotations are absent; unmarked older snapshots retain their compatibility path. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native index cursor replay increment (2026-10-23):** `RandomIndexDecisionV111.Validate` now replays one bounded Native `NextInt` from the saved before-cursor and checks the selected ticket and after-cursor. This validates primitive transcript self-consistency without claiming source call-site or seed parity. Static source review and `git diff --check` only; no tests or builds were run.

**D2 mixed Native primitive replay increment (2026-10-23):** Unit-float, ranged-float/double, and Gaussian-int/double Native decision validators now replay their declared source-codec operation from the saved cursor and check both the selected value and resulting cursor. Gaussian replay includes rejected Box–Muller samples. Unregistered Native site families remain fail-closed; transcript self-consistency does not establish source ownership or seed parity. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native physical-card outcome audit increment (2026-10-23):** Native-policy mixed traces now retain the ordered physical card identities selected at registered random-hand-card, autoplay, replay, and enemy-theft index sites, paired with each exact primitive decision and cursor span. A new additive audit schema requires complete annotations for these decisions when restoring newly marked audits; older unmarked snapshots remain readable. This captures semantic outcomes but does not establish source support completeness or Native source parity. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native mixed-trace completeness increment (2026-10-23):** Newly created Native-policy audits now mark the mixed-trace schema, retain a fingerprint of the complete trace, and reject a missing or truncated trace even when no semantic annotation family happens to cover the omitted requests. Restore still accepts older unmarked snapshots through their existing compatibility path. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native hand energy-cost outcome audit increment (2026-10-23):** Native-policy traces now pair each registered bounded hand-card energy-cost draw with a typed outcome carrying the exact eligible physical-card support, selected card instance, sampled cost, and provider decision. The additive schema marker requires complete annotations on restore while older unmarked snapshots remain readable. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native draw-hook energy-cost audit increment (2026-10-23):** The existing typed Native cost audit now also records the selected drawn-card instance, cost stage, bounded outcome, and exact provider ticket for the relic and Slither draw-hook sites. Newly created audits use cost schema V2, whose completeness check covers all three card-cost sites; V1 snapshots retain hand-only completeness. Source stream assignment and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native draw-insertion outcome audit increment (2026-10-23):** The Native-policy journal now reuses the semantic insertion event and decision to retain the ordered draw-pile identities, inserted model, selected position, and exact provider ticket at the three supported draw-pile insertion sites. A new additive schema requires complete annotations for those requests; enemy discard/draw insertion sites remain separate. Native stream ownership and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native enemy-pile insertion audit increment (2026-10-23):** Native enemy effects that insert cards into the player's draw or discard pile now retain the ordered target pile, inserted model, selected gap, source request, and exact bounded ticket. A separate additive schema requires complete annotations for both sites. Source stream ownership and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native pool-shuffle audit expansion (2026-10-23):** Native combat-entry draw-pile shuffles now retain their source-ordered physical card identities beside each exact Fisher–Yates ticket. Native semantic recycle shuffles retain their ordered discard identities, alongside Native startup's four relic rarity-queue shuffles. The shared pool-shuffle owner publishes all four Native families: combat card-pool swaps, entry draw-pile shuffles, semantic recycle, and relic-bag queues. Newly created Native-policy audits use pool-shuffle schema V3 for complete coverage; V2 snapshots retain combat-card and relic-queue completeness, and V1 snapshots retain their former combat-only rule. Pool prefixes, source eligibility, and Native cursor parity remain separate audits. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native reward and persistent shuffle audit expansion (2026-10-23):** The Native pool-shuffle owner now records War Hammer's ordered eligible card instances and the ordered boss/Lava Rock relic offer supports. Fishing Rod's Native combat-end index draw retains the selected physical card identity. New audits use pool-shuffle schema V4 and card-instance schema V2; older schema versions keep their prior completeness subsets. Neow outcomes remain owned by `NativeNeowOfferAuditV111`. Source-pool completeness, Native stream ownership, and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native reward card-model outcome audit (2026-10-23):** The existing generated-card outcome annotation now also retains each Native combat-reward card-model pool and selected offer beside its exact `player.rewards` decision. New Native-policy audits use generated-card schema V2; V1 snapshots keep combat-only completeness. Reward pool completeness, source ownership, and Native cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native combat-reward card-odds audit (2026-10-23):** The Native-policy journal now retains encounter rarity and upgrade results beside their exact `player.rewards` unit-float decisions, including rarity thresholds, the persistent offset update, and the card upgrade threshold. New audits use card-odds schema V1 and require both registered reward float sites; older snapshots remain compatible. Source calibration and Native cursor parity remain separate. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native combat-potion presence audit (2026-10-23):** Native `reward.potion_odds.unit_float` decisions now retain the offered flag, strict threshold, and pity update beside the exact provider ticket. New audits use potion-reward odds schema V1; forced rewards remain draw-free and older snapshots remain compatible. Threshold-law, source-prefix, and Native cursor parity evidence remain separate. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native PotionFactory reward audit (2026-10-23):** Combat-reward PotionFactory picks now retain each ordered same-rarity pool and selected potion beside the exact rarity-float and model-index decisions. New audits use PotionFactory schema V1 and require one paired outcome per `player.rewards` request; older snapshots remain compatible. Native pool completeness and stream/cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**D2 Native combat-gold and relic-reward audit (2026-10-23):** The Native-policy journal now retains raw GoldReward amounts with their exact bounded `player.rewards` decisions, and relic rarity results with the ordered fallback bag support and selected queue identity. New audits use additive V1 schemas and require one annotation per matching reward request; gold is recorded before later modifiers. Pool completeness, law calibration, and Native source/cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic engine-owned Native index provider increment (2026-10-23):** `SemanticOtherChanceStateV111` now retains the complete fifteen-stream Native run bundle with each stream's initial call count and cursor snapshot. Its generic Native index resolver admits only registered engine-owned site/stream/operation triples, preserves each declared source ordinal, and validates the saved transcript's per-stream cursor continuity and final state. The existing drawn-card cost continuation is routed through this resolver; older cost-only provider IDs and snapshots remain readable. Other ordinary action sites and the remaining Native primitive types still need mechanics integration. Static source review and `git diff --check` only; no tests or builds were run.

**S3 semantic engine-owned mixed Native boundary increment (2026-10-23):** The private provider now records a chronological typed transcript for Native index, unit-float, ranged-float/double, and Gaussian-int/double requests when the engine-owned registry admits the exact site/stream/operation pair. Restore validates every typed event, the index projection, continuous per-stream Native cursors, and final stream state. The earlier index-only provider ID and cost-only provider ID remain accepted, and are still advertised for snapshot/client compatibility. The semantic Nature mechanics path currently delegates drawn-card costs; other registered ordinary boundaries still need integration, and unregistered ranged/Gaussian pairs remain fail-closed. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S3 Native hand-cost delegation increment (2026-10-23):** The semantic Nature action continuation now resolves registered random hand-cost target selection through `run.combat_card_selection` and random hand-cost values through `run.combat_energy_costs`, then commits the Native owner and resumes the saved semantic mechanics choice. Engine-owned outcomes are regenerated from the player-action replay and retained in the provider transcript rather than added as Nature decisions. Explicit fixture-controlled cost events and the two Nature-owned sites keep their existing behavior; unsupported site/stream pairs remain fail-closed. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S3 Native random-target delegation increment (2026-10-23):** Supported effect, Sly autoplay, and autoplay random-target continuations now resolve through the run-owned `run.combat_targets` stream and resume their saved combat or end-turn frame. Engine-owned outcomes stay out of the Nature choice history and replay from the player action against the same provider state. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S3 Native generated-card delegation increment (2026-10-23):** Registered combat generated-card model and random draw-Attack model continuations now resolve through `run.combat_card_generation`, then resume their saved generation or draw-insertion frame. The semantic event retains the ordered model pool; engine-owned outcomes are regenerated during action replay and are not added as Nature decisions. Site, stream, and operation checks remain fail-closed, and source-pool/cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S3 Native potion-generation delegation increment (2026-10-23):** Registered combat potion-model continuations now resolve through `run.combat_potion_generation` and resume their saved combat or end-turn frame. The semantic event retains the ordered potion pool, while the provider stores the Native index request and selected position. Engine-owned outcomes are regenerated during action replay and are not added as Nature decisions. Source-pool/cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S3 Native card-transform delegation increment (2026-10-23):** Registered combat transform replacements now resolve through `run.combat_card_generation`, retaining the original card instance, model, and ordered replacement support in the semantic continuation. The provider stores the Native index request and selected position; action replay regenerates the result without adding a Nature decision. Transform-pool/cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S3 Jeweled Mask random pile-card delegation increment (2026-10-23):** The supported `BeforeHandDraw` continuation now resolves its registered random Power instance through `run.combat_card_selection`, then resumes and records the existing semantic draw event. The Native primitive trace stays in the run-owned provider. Mechanics evidence, complete source-pool fidelity, and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S3 saved enemy-theft delegation increment (2026-10-23):** The registered `combat.enemy.theft_card.select` request now maps to `run.combat_card_generation` when the run-owned provider is present, then replays the existing saved enemy-action frame and records the selected identity in end-turn chronology. It does not create a Nature history choice. Native stream attribution, theft-pool fidelity, and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S4 deterministic combat-potion action increment (2026-10-23):** The persistent semantic pilot now admits an exact allowlist of modeled no-target deterministic potion effects under the controlled-chance guard. Existing combat-boundary and hidden draw-pile validation remains authoritative; no chance owner or snapshot schema was added. Targeted, draw, choice, outside-combat, and other potion effects remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S4 targeted deterministic combat-potion action increment (2026-10-23):** Beetle Juice, Powdered Demise, Poison, Weak, and Vulnerable Potions now enter with a legal living-enemy target and reuse their modeled combat effects under the controlled-chance guard. Ordinary combat and hidden draw-pile checks remain in force; potentially lethal targeted damage potions remain outside this increment. Static source review and `git diff --check` only; no tests or builds were run.

**S4 deterministic outside-combat potion action increment (2026-10-23):** The existing `ReferenceNatureRunActionSessionV111` now admits legal non-event Fruit Juice and Blood Potion uses through their exact modeled run effects. Its controlled replay rejects any chance request, and completed deterministic actions use the existing snapshot replay validation. Event continuations remain separate; Entropic Brew remains excluded because its empty-slot refill samples the potion pool. No chance owner or snapshot schema was added. Static source review and `git diff --check` only; no tests or builds were run.

**S4 deterministic combat-potion victory increment (2026-10-23):** Fire, Explosive, and Explosive Ampoule now use the existing complete combat action-result boundary when their modeled damage ends combat. Potion replay gets the existing narrow engine-owned reward allowance; Distilled Chaos gets it as well because autoplay may win combat. Reward generation remains limited to the registered engine-owned sites. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Colorless semantic effect-draw increment (2026-10-23):** Finesse, Flash of Steel, Impatience, Master of Strategy, and Scrawl now use the existing source card-effect draw admission and provider-owned draw continuation. Impatience retains its no-Attack enqueue condition, Scrawl remains bounded by normal hand capacity, and Flash of Steel preserves damage-before-draw ordering. No event or snapshot schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 deterministic Colorless player-action increment (2026-10-23):** The semantic player-action gate now admits 32 modeled deterministic Colorless card effects, including legal target actions. Rend uses its target-debuff damage count; Bolas and Thrumming Hatchet reuse the existing last-turn rebound hook and semantic draw-pile adapter. Calamity installs its modeled power and resolves later Attack-triggered model picks through the existing generated-card continuation. Nostalgia's attack and skill returns are admitted only for the exact known source-card append under the active power's stack condition; the shared adapter commits it. Ordinary action replay remains guarded and validates provider-owned deck consistency at the completed player boundary. Mayhem, Entropy, and Stratagem reuse their existing downstream owners; other random generation, draw, and selection remain specialized. Static source review and `git diff --check` only; no tests or builds were run.
