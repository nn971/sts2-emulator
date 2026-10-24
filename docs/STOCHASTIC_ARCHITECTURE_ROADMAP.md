# Unified stochastic architecture — implementation roadmap

**Recorded:** 2026-10-16. **Status:** proposed implementation milestones, **not completed work**. **Authority:** [Unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md).

**S4 nested draw-hook child classification consolidation (2026-10-25):**
Automatic draw-hook replay, semantic draw-frame validation, and pending child
event discovery now share one classification against the saved pre-draw or
active hand-draw cursor, then dispatch boundary validation or event discovery
by child kind. Unknown tagged choices remain fail-closed. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 automatic random pile-card handoff consolidation (2026-10-25):** After
Jeweled Mask's automatic-hook card choice resolves, both active and pre-draw
continuations now use the common engine-owned nested-choice handoff for cursor
classification and count validation. Card support and hook continuation checks
remain with the random pile-card owner. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic draw adapter handoff consolidation (2026-10-25):** Automatic
hand-draw results from semantic draw segments, generated-card, Stratagem
selection, controlled and semantic draw insertions, and nested effect draws
now use the common
engine-owned nested-choice handoff for cursor classification and request-count
validation. Their typed transcript and child-event checks stay in their existing
owners. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic target-result handoff consolidation (2026-10-25):** After a
random-target child resolves, the combat-draw adapter now routes the resulting
active hand-draw cursor through the common engine-owned nested-choice handoff.
Target transcript validation and chained-target handling remain with the
target/draw owners; the shared handoff checks request progress and publishes
the classified pipeline boundary. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 semantic draw-hook handoff consolidation (2026-10-25):** The semantic
draw-hook adapter now delegates active, pre-draw, and completed cursor
publication to the shared engine-owned hand-draw handoff. Child event
validation remains in the draw-hook owner; the adapter validates its source
counts and publishes the returned cursor through the common path. Static source
review and `git diff --check` only; no tests or builds were run.

**S4 semantic nested hand-draw handoff ownership increment (2026-10-25):**
Nested semantic draw and drawn-cost adapters now delegate active, pre-draw,
and completed cursor publication to one engine-owned handoff. It validates the
source request and permits a changed request count only when the source cursor
was pre-draw and the resumed cursor entered the actual hand-draw stage. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 nested pre-draw semantic draw/cost increment (2026-10-25):** A selected
`BeforeHandDraw` action may now suspend on its semantic effect-draw ticket or
drawn-card cost. The existing semantic draw frame and version 8 chronology
retain and replay the child before resuming the saved pre-draw stage. The
automatic hand-draw request may transition from the pre-draw zero-count frame
to its live count only at that cursor; active hand-draw counts remain checked.
No choice or snapshot schema changed. Other pre-draw nested choice kinds
remain fail-closed. Static source review and `git diff --check` only; no tests
or builds were run.

**S4 nested random draw-attack model continuation increment (2026-10-25):**
Active `CardDrawn` and pre-draw hook continuations may now suspend first on
the random free-attack model choice and then on the existing insertion-position
choice. The semantic draw frame owns the generated-card transcript and its
cross-type chronology; chronology version 2 adds this choice kind, while
version 1 snapshots without generated-card decisions remain replayable.
End-turn forwards the pending model event to the draw frame, and pilot history
records the external choice once. Static source review and
`git diff --check` only; no tests or builds were run. Random-pool fidelity and
Native stream parity remain open pending reference-mod evidence.

**S4 nested hand-draw generation continuation increment (2026-10-25):**
Generated-card model choices opened by active `CardDrawn` or pre-draw hook
actions now stay with the semantic draw frame. It suspends the automatic draw
cursor while the existing generation owner applies one selected model, then
resumes the same draw or pre-draw stage. Repeated generation choices append in
order to the generated-card transcript and chronology. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 BeforeHandDraw fixed insertion child increment (2026-10-25):** A selected
`BeforeHandDraw` action may now suspend on its source card's fixed draw-pile
insertion ticket. Admission retains the semantic pre-draw cursor, and the
existing insertion owner resumes into the ordinary hand draw. No choice or
snapshot schema changed. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 active-hand-draw nested draw-ticket increment (2026-10-25):**
A selected action from an active `CardDrawn` hook can now suspend on its
source card's semantic effect-draw, drawn-cost, or fixed draw-insertion
ticket. Admission binds the child to the live source card and active
requested/remaining hand-draw cursor. The semantic combat-draw and end-turn
owners reuse the existing draw, cost, insertion, transcript, and replay paths.
No choice or snapshot schema changed.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 end-turn active semantic draw routing increment (2026-10-25):** The
selected-index resolver now routes a pending semantic card event owned by an
active `SemanticCombatDrawV111` hand-draw frame through that frame's existing
resolver and completion handoff. The end-turn facade does not take ownership
of the draw transcript. No event or snapshot schema changed. Static source
review and `git diff --check` only; no tests or builds were run.

**S4 active-hand-draw random pile-card continuation increment (2026-10-25):**
A selected active `CardDrawn` hook may now suspend on the existing Jeweled Mask
random power pile-card event. Hook replay retains its exact hand-draw counts;
the existing pile-card owner validates and commits the physical move, then
resumes the same draw. The end-turn selected-index resolver routes this child
through the active draw frame. The pre-draw hook relay now publishes its
existing random pile-card child before resuming the pipeline. Restore uses the
existing typed transcript and chronology. Other post-selection nested choices
remain fail-closed. No event or snapshot schema changed. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 chained active-hand-draw target continuation increment (2026-10-25):**
Resolving a supported `CardDrawn` random target may suspend the saved source
continuation on another random target. The semantic draw owner retags that
child with the existing active-hand-draw marker, appends it to the target
transcript and chronology, and preserves the same requested/remaining cursor.
Restore replays each target in order before resuming the hand draw. Other child
choice kinds remain fail-closed. No event or snapshot schema changed. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 draw-hook resume boundary validation increment (2026-10-25):** The shared
draw-hook resume now validates the classified automatic-pipeline boundary
against the returned run on every branch, including standalone draws and
saved pre-draw progress. Resuming the active automatic hand draw also checks
that its requested count is unchanged and returns the already-classified
progress value. Choice transcripts and snapshot fields are unchanged. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 repeated-damage defeat-hook continuation increment (2026-10-25):** The
same resumable `EnemyDamaged` and `EnemyDefeated` boundary now covers
`DamageAllEnemiesRepeatPerKill` (including Echoing Slash). Single and repeated
damage now share one post-hit event dispatcher and death follow-up. The
repeated continuation retains the current round's target snapshot, next target
index, and pending extra kill rounds, so resolving a hook neither repeats
earlier hits nor skips later targets. Restore checks tie a defeated target to
the saved cursor, and the mechanics projection includes that progress under
schema v19. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 nested pre-draw choice continuation increment (2026-10-25):** A legal
`BeforeHandDraw` hook action can now hand off to an already-supported random
target choice while retaining the pre-draw cursor. The existing target owner
resolves that child before the ordinary pipeline starts the hand draw. No
continuation schema changed; other nested choices and choices opened after a
hand-draw card hook remain unsupported. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic hand-draw pile-card boundary classification increment (2026-10-25):**
The shared automatic-pipeline classifier now reports the typed random
pile-card event as a semantic choice only when its live event and the saved
`BeforeHandDraw` or active hand-draw cursor validate. Other cursor positions
retain their existing classification. No snapshot field changed. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 enemy-defeat hook continuation increment (2026-10-25):** A modeled
`EnemyDamaged` or `EnemyDefeated` subscriber can now suspend the current
`DamageEnemy` operation through the existing outer choice continuation. The
saved frame resumes remaining damage hooks, death summons, fatal rewards, and
the operation suffix in source order; pending enemy-defeat event dispatches
also use the resumable dispatcher. Combat restore validates the saved
follow-up, and the mechanics comparison retains it under schema v18. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 DrawCards continuation-owner increment (2026-10-25):**
`ResolveOperations` now delegates effect `DrawCards` and
`ReturnHandToDrawAndShuffle` boundaries to the shared draw-continuation owner.
Hand-return shuffle now stops before executing its saved suffix when an
after-shuffle insertion suspends, preserving Bottled Potential's draw order.
The complete `ResolveOperations` choice-site review found that all other
suspending branches already return their saved suffix; the kernel now enforces
that boundary after each operation and fails immediately if a pending choice
would otherwise continue inline.
Ordinary and resumed cost draws also share `DispatchCardDrawnAfterCost`;
opening Nature Stratagem draws use the semantic result dispatcher; opening and
automatic source-stage hand draws build the same Stratagem-plus-draw suffix
through one operation constructor. Saved fields and transcript schemas are
unchanged.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 Mayhem opening Sly-target continuation increment (2026-10-24):** The
`AfterAutoPrePlayPolicy` target boundary now admits nested Sly autoplay only
when the discarded target card is live, Sly, and enemy-targeting, and the
saved continuation retains an active autoplay card-play series whose source
has left the play pile to resolve its effects. Nested ordinary random-effect
targets use the existing effect-target continuation when they retain that
active series. This covers nested Mayhem opening effects without changing
direct autoplay-target validation or snapshot schemas. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 autoplay Tingsha discard-target continuation increment (2026-10-25):**
Tingsha's automatic discard target now uses the existing target event when the
saved CardDiscarded dispatch is its exact live relic trigger, the DamageEnemy
operation matches the source effect, and an active autoplay card-play series
is present at a supported automatic-pipeline boundary. Mayhem opening also
admits nested ordinary random-effect targets from its active autoplay series.
No event or snapshot schema changed. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 sequential-discard Tingsha continuation increment (2026-10-25):**
Gambler's Brew and opening Gambling Chip now admit each Tingsha target when
the saved CardDiscarded event is its live relic trigger, its remaining discard
queue still names live discard-pile cards, and the parent contains the matching
selected-count draw. Gambling Chip also requires its exact opening cursor and
relic subscriber; Gambler's Brew requires its modeled potion selection and
`PotionUsed` completion event. Queued Sly identities are checked against the
live discard-pile cards, and Gambling Chip admits the resulting Sly target only
at that same opening cursor and subscriber. Nested random-effect targets in
those Sly plays also require the active autoplay series and the exact opening
subscriber in their saved continuation chain. No event or snapshot schema
changed. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 Tools of the Trade Tingsha continuation increment (2026-10-25):**
Tools of the Trade turn-start discards now admit Tingsha's target only when
the saved parent resumes the exact live `PlayerTurnStarted` power subscriber
at its post-dispatch cursor. The outstanding discard and Sly queues are
validated against live card instances and bounded by the power's stack count;
the existing target and event schemas are reused. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 Imbued nested-choice source-frame validation increment (2026-10-24):**
Nested choices during the `LateRelicAutoPlay` sequence now identify the active
Imbued source from its saved autoplay series after the card has left the play
pile, then match the continuation's exact remaining Imbued driver queue.
Direct target validation still requires the live queue head. This admits
effects inside the final Imbued card when no queued cards remain, without
changing snapshot schemas. Static source review and `git diff --check` only;
no tests or builds were run.

**S4 Sly target source validation increment (2026-10-24):** Shared live target
support now requires a `sly_auto_play` card to be present in discard, carry
the current Sly keyword (including keyword overrides), and have an enemy
target definition. Combat restore, controlled-playable validation, target
selection, and the opening target admission reuse this source check. No event
or snapshot schema changed. Static source review and `git diff --check` only;
no tests or builds were run.

**S4 controlled target continuation identity increment (2026-10-24):** Shared
live target validation now requires the pending choice's source-card identity
and destination to match its saved outer continuation. Effect-target choices
must also retain a non-targeted first operation in the action-target mode
produced by suspension. This rejects malformed target checkpoints before
selection while preserving all target payload and snapshot schemas. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 opening Mayhem target continuation increment (2026-10-24):** The shared
opening target boundary now admits Mayhem's turn-one top-autoplay target at
`AfterAutoPrePlayPolicy`, validating the Power-sourced `AutoPlayCombatCard`
operation, selected card identity, and live enemy support. Target replay
resumes the staged autoplay through the existing owners. No event or snapshot
schema changed. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 automatic Nature hand-draw after-shuffle insertion increment (2026-10-24):**
The existing draw-insertion admission now accepts an after-shuffle relic
insertion in an automatic Nature hand draw when its saved semantic draw frame
matches the active `DrawPlayerHand` remaining count. This admits Biiig Hug's
Soot insertion after a recycle and resumes the saved draw through the existing
insertion owner. No event or snapshot schema changed. Static source review and
`git diff --check` only; no tests or builds were run.

**S0 deferred deck-reveal source census (2026-10-24):** A targeted Core
source search found the existing semantic deck kernel, Chance Law query, and
typed pilot resolver for peek/scry, but no modeled card, potion, power, or
relic operation that requests a reveal. The migration row now records that
source integration needs a source-defined operation; the typed kernel remains
available for that continuation. This emulator census does not establish
whether the installed game has additional reveal callers. No code or schema
changed. Static source review and `git diff --check` only; no tests or builds
were run.

**S2/S4 generated-choice subscriber validation merge (2026-10-24):** The
turn-one Toolbox automatic `BeforeHandDraw` shuffle and ChoicePool-selection
owners now reuse the exact generated-choice subscriber validator already used
by Toolbox and Choices Paradox opening continuations. Opening and automatic
offers also share validation of generated physical-card attributes and source
pool membership. The shared source check still leaves opening-stage and
automatic draw-cursor validation at their respective continuation boundaries.
No event or snapshot schema changed. Static source review and
`git diff --check` only; no tests or builds were run.

**S2/S4 Toolbox pre-draw generation continuation increment (2026-10-24):**
Toolbox's turn-one `BeforeHandDraw` generated offer now reuses the existing
full-pool shuffle and ChoicePool selection owners. Both boundaries are admitted
only for the exact Toolbox relic trigger and saved draw-stage cursor; selection
resumes the same hand-draw stage after its event dispatcher completes. The
pre-draw generated selection is accepted by the existing draw and pipeline
owners, and is not projected as a draw-hook player action. No event or snapshot
schema changed. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 single classified resume-boundary owner increment (2026-10-24):** The
automatic-pipeline resume result now owns both boundary classification and
validation of classifications carried by draw-progress or semantic-choice
results. A supplied boundary that disagrees with its returned run fails closed;
all adapters use the same conversion path. Snapshot and transcript formats are
unchanged. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 semantic draw-hook resume boundary increment (2026-10-24):** Draw-hook
replay now exposes a classified automatic-pipeline result while retaining its
run-only facade. Semantic automatic-choice replay passes the classification
through the nested-choice adapter to boundary publication. The separate
draw-resume adapter keeps it on its transient progress result without adding a
serialized field. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 semantic Stratagem resume boundary increment (2026-10-24):** The
Stratagem card-selection adapter retains its typed selection boundary on
transient draw progress. If selection immediately resumes more hand-draw
work, it carries the typed downstream boundary or classifies the resulting
run. Serialized draw state remains unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic draw-segment boundary increment (2026-10-24):** Semantic draw
segment replay now retains the draw engine's typed progress result instead of
reconstructing it from a run and remaining count. Missing transient boundary
metadata is classified from the resulting run; replay identity validation and
serialized state remain unchanged. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 semantic effect-draw cost boundary increment (2026-10-24):** Standalone
semantic effect-draw cost replay now uses the cost owner's classified resume
result and carries its boundary on transient draw progress. The existing
run-only facade, saved draw frame, and serialized state remain unchanged.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 semantic random pile-card boundary increment (2026-10-24):** Automatic
random pile-card replay now completes missing transient boundary metadata
from the downstream draw result's run, while preserving a typed boundary when
the hand-draw owner provides one. Its event and serialized draw state remain
unchanged. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 shared draw-progress boundary owner increment (2026-10-24):** The
provider-neutral draw-progress result now owns the rule that preserves an
existing pipeline classification or derives one from its resulting run.
Semantic draw, draw-hook, Stratagem and random pile-card adapters share it;
serialized state is unchanged. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 semantic-choice boundary conversion owner increment (2026-10-24):** The
semantic choice resume result now owns conversion to the automatic-pipeline
resume type used by end-turn publication, preserving a supplied classification
or deriving it from the returned run. Choice identity and serialized state are
unchanged. Static source review and `git diff --check` only; no tests or builds
were run.

**S2/S4 Tingsha discard-target continuation increment (2026-10-24):** Tingsha
now reuses the semantic random-target event for manual Storm of Steel, Shadow
Step, Survivor, Hidden Daggers, Concentrate, Dagger Throw, Acrobatics,
Prepared, and Calculated Gamble discards. Admission checks the exact relic
subscriber, discarded-card event, physical card-play frame and source-card
destination, plus the queued Shiv-creation, power, Energy, draw, or
selection-completion suffix; the saved discard queue resumes in order. End-turn
hand-discard targets continue through the existing automatic-pipeline target
transcript; other player-card Tingsha origins and Native target-pool, stream,
and cursor parity remain open. No event or snapshot schema changed. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 modeled automatic-trigger census increment (2026-10-24):** The unified
architecture now records the current repository's stochastic opening,
hand-draw-entry, turn-start, and pre-play continuation census, including each
supported typed choice owner and the deterministic `BeforeHandDraw`
hand-creation hooks. The census found no
additional modeled source requiring an unimplemented continuation in those
trigger families; installed-game hook completeness, trigger order, and stream
attribution still require the Native reference mod or Fidelity Inspector.
This closes the repository inventory only. Static source review and
`git diff --check` only; no tests or builds were run.

**S1 compact Native bounded-index Chance Law increment (2026-10-24):**
Chance Law v3 now represents large canonical Native `NextInt(bound)` laws by
their exact 53-bit input grid and source multiply-and-truncate transform.
ReferenceNature samples the grid and maps it to the raw index without an
outcome array; the descriptor carries the full grid mass and remains
`SOURCE_VERIFIED` for that transform. Existing explicit laws and compact
legacy-uniform v2 laws retain their identities. The v4 capability manifest
advertises all three schemas, and the Python binding validates the Native
compact form. Seed derivation and cursor parity remain separate. Static source
review, `git diff --check`, Python syntax compilation, and Chance Law boundary
guards only; no tests or builds were run.

**S1 compact Chance Law range increment (2026-10-24):** Chance Law v2 now
represents large unverified uniform bounded-index supports by a fingerprinted
range descriptor. ReferenceNature samples the descriptor directly, and the
map request dispatcher routes the legacy `map.legacy.convergence.order_key`
bound (`Int32.MaxValue`) through it without allocating per-index outcomes.
Explicit v1 laws and their identities are unchanged. The stochastic
capability manifest v4 advertises both schemas, and the Python binding
validates compact laws while retaining v3 manifest support. The equal-index
map model, seed distribution, map source parity, and cursor parity remain
`UNVERIFIED`. Static source review, `git diff --check`, Python syntax
compilation, and Chance Law boundary guards only; no tests or builds were run.

**S1 fail-closed explicit Chance Law support limit (2026-10-24):** Generic
legacy and Native bounded-index laws now reject supports above 65,536 before
building per-outcome arrays. This prevents the registered legacy map
`map.legacy.convergence.order_key` request (bound `Int32.MaxValue`) from
allocating an unbounded law; its typed request remains replayable, while
ReferenceNature map resolution needs a compact law over map-order outcomes.
Static source review and `git diff --check` only; no tests or builds were run.

**S1 controlled legacy map edge-ticket semantics increment (2026-10-24):**
The controlled `continue_act` Chance Law now expands the registered 100-ticket
edge-extension request into distinct raw ticket outcomes annotated with the
prototype's `< 35` edge-extension branch. Replay still uses the original
bounded ticket index. Ticket weighting and map-generation order remain
`UNVERIFIED`; the `Int32.MaxValue` convergence-key law remains unsupported
until compact map-order outcomes are represented. Static source review and
Chance Law boundary guards only; no tests or builds were run.

**S1 controlled Rest Heal rarity-ticket semantics increment (2026-10-24):**
Legacy Rest Heal run-action queries now expand the source-shaped rarity ticket
law into distinct raw ticket-position outcomes annotated with the rarity bucket
each position reaches. The continuation verifies bounded support and numeric
labels, while action replay still receives the unchanged raw ticket index.
Pool fidelity and ticket weighting remain `UNVERIFIED`. Static source review,
`git diff --check`, and Chance Law boundary guards only; no tests or builds
were run.

**S3 combat-reward ticket helper ownership follow-up (2026-10-24):** The
legacy card-rarity ticket weight helper now lives with its sole combat-reward
query caller. Shared cross-family rarity outcome and grid helpers remain in
core. Query policy and weights are unchanged. Static source preservation and
`git diff --check` only; no tests or builds were run.

**S3 shared Chance Law Native helper ownership follow-up (2026-10-24):**
Exact Native unit-float-to-integer grid mapping and Native boundary-law
construction now live in `ChanceLawV111.cs`, alongside shared grid and law
helpers. Callers and query behavior are unchanged. Static source preservation
and `git diff --check` only; no tests or builds were run.

**S3 Ancient-offer policy ownership follow-up (2026-10-24):** The public
`EventOfferConditioningPolicyId` value now lives with the Ancient-offer laws in
`ChanceLawAncientOfferV111.cs`; its value and public API are unchanged. Static
source review and `git diff --check` only; no tests or builds were run.

**S1 controlled relic-acquisition identity outcomes increment (2026-10-24):**
The run-action Chance Law now routes supported legacy random Rare-card and
potion acquisition requests, plus event and run empty-slot potion fills,
through their existing source-specific semantic laws. It verifies each law
against the saved request owner, ordinal, ordered indices and candidate
labels, then binds the returned event ID to the exact saved action boundary.
Native `player.rewards` requests keep their Native index law. Source pool
fidelity and equal-index weighting remain `UNVERIFIED`. Static source review
and Chance Law boundary guards only; no tests or builds were run.

**S1 controlled legacy Treasure identity outcomes increment (2026-10-24):**
The generic legacy Treasure map-entry continuation now reuses the existing
`relic:<id>` Chance Law after validating its Act, floor, owned-relic pool,
request owner, and ordered index support. Large supports may omit the saved
inline numeric labels; the source pool and owner still determine the law.
Replay continues by selected index. Source-pool fidelity and equal-index
weighting remain `UNVERIFIED`. Static source review and Chance Law boundary
guards only; no tests or builds were run.

**S1 Rest Heal reward-card identity query increment (2026-10-24):** Legacy
Rest Heal card-model requests now reuse the source-specific `card:<model>`
law. The query reconstructs the external reward-group owner, selected rarity
and prior card identities from the saved request and decision prefix, then
checks the resulting support against the pending request. The rarity ticket
continues to expose raw ticket positions for source-compatible resolution.
Pool fidelity and equal-index weighting remain `UNVERIFIED`. Static source
review and Chance Law boundary guards only; no tests or builds were run.

**S3 controlled-choice Chance Law ownership increment (2026-10-24):** The
controlled run-action law and boundary fingerprint now live in
`ChanceLawRunActionV111.cs`; controlled event-choice dispatch, Stone of All Time
ticket law, and boundary description now live with Native event-choice laws.
Shared random-index law construction remains in core. Public APIs and boundary
policies are unchanged. Static source preservation and `git diff --check` only;
no tests or builds were run.

**S3 remaining legacy ticket and relic-selection law ownership follow-up
(2026-10-24):** Legacy card rarity and upgrade-ticket queries now live with
combat-reward laws; the caller-bound unowned-relic index query now lives with
relic-acquisition laws. Cross-family relic grab-bag rarity and generic request
routing remain shared in core. Query APIs, supports, and policies are unchanged.
Static source preservation and `git diff --check` only; no tests or builds
were run.

**S3 semantic-combat Chance Law ownership increment (2026-10-24):**
Intent and end-turn; Native draw/recycle shuffles and Personal Hive insertion;
and target, generated-card, cost, pile-card, transform, potion-generation,
hand-cost, pool-shuffle, draw-insertion, shortlist, and deck-reveal laws now live in
`ChanceLawSemanticCombatV111.cs` with their policy IDs. Shared Native grid and
law-construction helpers remain in core. Public APIs and supports are unchanged.
Static source preservation, signature inventory, and `git diff --check` only;
no tests or builds were run.

**S3 combat-end and relic-acquisition law ownership increment
(2026-10-24):** Fishing Rod, Improvement, and Pael's Tooth combat-end laws
now live with the existing Native event-shuffle query owner; Pael's Legion
skin acquisition now lives with relic-acquisition laws. Public and internal
query APIs are unchanged. Static source preservation, the Chance Law signature
inventory, and `git diff --check` passed; no tests or builds were run.

**S3 Native combat-entry Chance Law ownership increment (2026-10-24):**
Encounter-formation, unique-monster-HP, and Tough Egg hatch-HP queries, plus
canonical creature-owner validation, now live in
`ChanceLawCombatEntryV111.cs`. The generic Native bounded-index router and
shared grid construction remain in core. Public query APIs, supports, and
schemas are unchanged. Static source review and `git diff --check` only; no
tests or builds were run.

**S3 legacy Shop Chance Law ownership increment (2026-10-24):** The five
legacy Shop rarity/model and price queries, plus the retained legacy merchant
request-index law, now live with Native merchant laws in
`ChanceLawMerchantV111.cs`. Shared reward-card-pool and request construction
remain in core. Public APIs, supports, and schemas are unchanged. Static source
review and `git diff --check` only; no tests or builds were run.

**S3 potion-generation Chance Law ownership increment (2026-10-24):**
Generic legacy potion rarity/model laws and Native PotionFactory rarity/model
laws, including the factory policy IDs, now live in
`ChanceLawPotionGenerationV111.cs`. Shared threshold-grid and categorical
helpers remain in core. Public query APIs, supports, and schemas are unchanged.
Static source review and `git diff --check` only; no tests or builds were run.

**S3 Native combat-reward law ownership follow-up (2026-10-24):** Native
combat gold, potion odds, card rarity and upgrade, card identity, boss relic
offers, and Lava Rock relic offers now join the reward owner. Cross-family
relic-bag rarity and queue laws remain shared in core. Query APIs, supports,
and schemas are unchanged. Static source review and `git diff --check` only;
no tests or builds were run.

**S3 combat-reward Chance Law ownership increment (2026-10-24):** The
legacy combat-reward card rarity/model, potion drop/model/odds, and Elite relic
laws now live with the existing Native combat-reward relic-identity law in
`ChanceLawCombatRewardV111.cs`. Shared pool and categorical-law helpers
remain in core. Public query APIs and supports are unchanged. Static source
review and `git diff --check` only; no tests or builds were run.

**S1 Chance Law duplicate-signature guard increment (2026-10-24):** The
v59 static inventory records identical masked method signatures across
`ChanceLawEngineV111` partials and fails the outcome-blind check if any are
present. This catches duplicate declarations while preserving legal overloads
with distinct source signatures; it does not resolve call overloads. Static
inventory and `git diff --check` passed; no tests or builds were run.

**S3 legacy Event effect model ownership follow-up (2026-10-24):** The
generic random-potion and Rare-card Event effect laws now join the existing
modifier-curse, potion-slot-fill, and custom reward potion-option queries in
`ChanceLawEventRewardV111.cs`. Shared pool and categorical-law helpers remain
in core. Static source review and `git diff --check` only; no tests or builds
were run.

**S3 legacy Event-reward law ownership increment (2026-10-24):** The
legacy modifier-curse, event potion-slot fill, and custom reward potion-option
laws now live in `ChanceLawEventRewardV111.cs`. Their query methods and source
conditions are unchanged; shared categorical construction remains in core.
Static source review and `git diff --check` only; no tests or builds were run.

**S3 merchant and Treasure helper ownership increment (2026-10-24):**
The legacy Shop card-type validator now lives with merchant Chance Laws, and
the legacy Treasure relic query now lives with the existing Native Treasure
query owner. Shared reward-pool and categorical-law helpers remain in core.
Public query APIs, supports, and schemas are unchanged. Static source review
and `git diff --check` only; no tests or builds were run.

**S3 Ancient-offer Chance Law ownership increment (2026-10-24):** The
Ancient offer queries, source-boundary checks, ordered shuffle-pool builders,
Darv request builders, and family policy IDs now live in
`ChanceLawAncientOfferV111.cs`, separate from the existing ReferenceNature
session owner. Shared native bounded-index grid and law-construction helpers
remain in core. Public query APIs, supports, and schemas are unchanged. Static
source review and `git diff --check` only; no tests or builds were run.

**S3 persistent-card acquisition law ownership increment (2026-10-24):**
The legacy source-model-excluding card transformation law now lives in
`ChanceLawRelicAcquisitionV111.cs` with the other relic-acquisition queries.
Shared reward-card-pool validation remains in core for its reward and merchant
callers. The moved query is unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 relic-acquisition Chance Law ownership increment (2026-10-24):**
Legacy random Rare-card and potion acquisition laws, legacy Run potion-fill
laws, and the Native player-model and Entropic Brew fill laws now live in
`ChanceLawRelicAcquisitionV111.cs`. Shared index-law construction remains in
core. Public query APIs, supports, and schemas are unchanged. Static source
review and `git diff --check` only; no tests or builds were run.

**S3 map-generation Chance Law ownership increment (2026-10-24):** Native
map Gaussian and request laws, map-path and room-assignment laws, run-start
encounter-pool laws, and experimental three-act assignment laws now live in
`ChanceLawMapGenerationV111.cs`. Shared bounded-grid, event hashing, and law
construction helpers remain in the core partial. Public query APIs, supports,
and schemas are unchanged. Static source review and `git diff --check` only;
no tests or builds were run.

**S1 transitive Chance Law outcome-blind guard increment (2026-10-24):** The
source inventory now follows method-name calls from all Chance Law query
entrypoints through the static methods declared on `ChanceLawEngineV111`
partials, checking direct and helper-mediated paths for provider sampling,
cursor reads, boundary journals, replay capture, and engine steps. It indexes
the query owner’s methods and conservatively includes overloads by name; calls
into other types, delegates, and reflection remain outside its evidence. The
136-query static inventory passed with no forbidden references. No tests or
builds were run.

**S3 merchant, Treasure, and combat-reward Chance Law ownership increment
(2026-10-24):** Native merchant and Alchemical Coffer laws, Native Treasure
laws, and the combat-reward hidden relic-identity law now live in their
owner-named partial source files. Family policy IDs and query-specific
validation helpers move with their query owners; shared law construction and
float-grid mapping remain in the Chance Law core. Public query APIs, supports,
and schemas are unchanged. Static source review and `git diff --check` only;
no tests or builds were run.

**S3 Native Gaussian request independence increment (2026-10-24):** Native
Gaussian-integer sampling and decision validation now obtain site and exact
source-shape support from `StochasticSiteRegistryV111`; Chance Law retains a
delegating query facade. Native provider execution no longer depends on Chance
Law query coverage. Static source review and `git diff --check` only; no tests
or builds were run.

**S3 Native/Chance Law dependency guard increment (2026-10-24):** The source
inventory now reports direct Chance Law symbol references in Native RNG
provider/adapter files and native mod C# sources, and
`--check-no-native-chance-law-dependency` fails if any are present. The scan is
a direct-reference regression guard and does not prove the absence of indirect
dependencies. The inventory found zero such references. The source inventory
and `git diff --check` passed; no tests or builds were run.

**S3 Chance Law/ReferenceNature source ownership increment (2026-10-24):**
ReferenceNature sampling, cursor/session state, and the run-action,
Ancient-offer, and controlled event-choice session types now live in
owner-named source files instead of the Chance Law core file. Public type
names, behavior, and snapshot schemas are unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S3 Chance Law query source ownership increment (2026-10-24):** Eight existing
`ChanceLawEngineV111` partial implementations for Native events, event
shuffles, Neow, Punch Off, Tablet of Truth, Trash Heap, and Trial now live in
`ChanceLaw*V111.cs` files rather than ReferenceNature session sources. The
ReferenceNature session files retain their existing session and replay owners;
their SiteId lists now live on those session types and the registry resolves
them directly. Chance Law retains delegating list projections for compatibility.
Public query methods, behavior, and schemas are unchanged. The full source
inventory passed; no tests or builds were run.

**S4 choice-resolution continuation owner increment (2026-10-24):** The
existing `CreateEffectContinuation`, `ResumeChoiceResolutionContinuation`,
and outer/parent frame composition helpers now live in
`PrototypeChoiceResolutionContinuationV111`. Effect execution supplies its
source context and remaining queue through that owner; nested-choice replay
and state formats are unchanged. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 draw-continuation helper ownership increment (2026-10-24):** The
provider-neutral draw continuation owner now also prepends the saved draw
operation to an outer choice frame and accumulates distinct deferred drawn
card identities. These draw-specific helpers were removed from
`PrototypeCombat`; generic choice-parent composition remains with its existing
owner. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 draw-resume operation construction owner increment (2026-10-24):**
`PrototypeDrawCardsContinuationV111` now constructs the queued resume operation
directly from the saved draw frame. The scalar factory was removed from
`PrototypeCombat`, leaving frame projection, validation, and defensive payload
publication with the existing continuation owner. No snapshot or transcript
format changed. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 controlled-choice payload discriminator increment (2026-10-24):** The
flattened `PendingCombatChoiceState` now owns the mapping from controlled
choice IDs to their exclusive typed payloads, including the sole supported
draw-cost/semantic-draw pair, and enforces exclusivity of its four Stratagem
and CardDrawn-hook mode flags. Combat-state and semantic draw-cost validation
reuse this invariant, removing repeated payload-exclusion, draw-frame shape,
and mode-overlap checks without changing the snapshot schema. Static source
review and `git diff --check` only; no tests or builds were run.

**S4 guarded run-action and event-choice scope increment (2026-10-24):**
Controlled run-action replay and entry, event-choice and reward continuations,
and their shared `Step` adapters now use
`ControlledChanceGuardV111.ExecuteGuarded`. Exact request filtering,
continuation validation, and provider-specific replay stay with their existing
owners. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 guarded semantic combat-draw scope increment (2026-10-24):** Semantic
combat-draw entry, automatic hand-draw entry, and resumed automatic hand draws
now use `ControlledChanceGuardV111.ExecuteGuarded` for the shared synchronous
guard boundary. Draw progress and source-boundary policy remain in their
existing owners. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 controlled random-target validation owner increment (2026-10-24):** The
semantic target owner now validates ordered live enemy support and Sly/autoplay
source identity for semantic boundaries, combat-state validation,
controlled-playable validation, and target resume. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 canonical typed-request identity increment (2026-10-24):**
`ControlledStochasticChanceReplayV111` now owns complete serialized request
matching for every typed request and the mixed request envelope. Its previous
per-type field comparators and direct request-hash comparisons were removed
from controlled and scripted replay, Native outcome annotations, semantic draw
insertion and end-turn replay, and intent/recycle cursors. The executor's
`RequestsMatch` facade also delegates to the canonical matcher. Request identity
now has one implementation across typed replay, scripted replay, and saved
source-boundary validation. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 candidate-label equality owner increment (2026-10-24):** Ordered,
null-aware candidate-label equality now sits beside canonical request identity
in `ControlledStochasticChanceReplayV111`; the executor retains a facade and
label-to-index translation. Duplicate string-array comparisons were removed
from the Native run-action transcript, Shovel and event-relic bag replay,
controlled event/run-action validation, and the source run-start boundary.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 shared guarded transition runner increment (2026-10-24):**
`ControlledChanceGuardV111.ExecuteGuarded` now owns optional Native-provider
and prototype-reward scopes as well as the base controlled guard. Semantic
pilot actions and Native-policy transitions, end-turn pipeline resumes,
controlled-playable continuations, and ordinary semantic player actions use
the same runner. Their validation, chronology, and provider journals remain
with the existing owners. No snapshot or transcript format changed. Broader
resume consolidation and cross-provider mechanical equivalence remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 guarded hand-draw completion owner increment (2026-10-24):** A clean
guarded `DrawPlayerHand` source result now completes through the existing
`CompleteAutomaticHandDrawStage` owner. This shares after-hand power cleanup
and cursor advancement with controlled and semantic continuations; ordinary
unguarded draws retain their existing pipeline progression. Pending boundaries
still route to their provider adapter. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 source-route opening continuation increment (2026-10-24):** Semantic
initial-intent completion now transfers the already-committed physical draw pile
into the Nature deck before replaying opening stages. It publishes the clean
player boundary or an exact supported opening-pipeline choice, allowing
turn-one Crossbow generation to use its existing generated-card owner without
rerandomizing the opening shuffle or hand. Unsupported opening suspensions
remain fail-closed. Static source review and `git diff --check` only; no tests
or builds were run.

**S4 Crossbow opening generation continuation increment (2026-10-24):**
Crossbow's existing generated-card continuation now admits its exact turn-one
opening `BeforeHandDraw` cursor as well as the later-turn `DrawPlayerHand`
cursor. The saved opening stage resumes through the existing opening-combat
pipeline after the card is selected; exact trigger, operation, and supported
remainder checks remain in force. Static source review and `git diff --check`
only; no tests or builds were run.

**S0 runtime entropy scan scope increment (2026-10-24):** Census v53 keeps
adapter callsite tracing Core-scoped and extends the direct ad hoc entropy
check to runtime C# sources under `src/` and `native/`, excluding tests. The
gate finds zero direct `System.Random` or `RandomNumberGenerator` expressions
in those trees. This does not classify Native reference-mod RNG hooks or prove
the absence of indirect/aliased entropy. The regenerated matrix, contract-link,
and no-ambient-entropy inventory gate passed; no tests or builds were run.

**S0 typed entropy alias scan increment (2026-10-24):** Census v54 also scans
runtime C# code for `System.Random` declarations and type aliases, using a
comment/literal mask to avoid matching prose. The expanded gate still finds
zero ambient entropy findings. It remains a source-level check and cannot
prove the absence of runtime-injected or otherwise indirect entropy. Site-
matrix, contract-link, and no-ambient-entropy inventory checks passed; no tests
or builds were run.

**S4 controlled hand-draw remainder query increment (2026-10-24):** Runtime
controlled recycle and draw-cost continuation checks now pass the mechanics
cursor to the pipeline stochastic owner, which validates that the saved
controlled mode is active at `DrawPlayerHand` before returning its remaining
count. The no-cursor query remains for the flattened snapshot projection.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 controlled draw-progress owner increment (2026-10-24):** Controlled-
playable validation and turn-resume classification, controlled insertion
validation, controlled hand-draw hook and recycle resumption, and post-choice
recycle dispatch now obtain active automatic hand-draw modes through the
pipeline stochastic owner's cursor-validated query. The draw completion guard
also uses its semantic pre-draw stage query. These consumers no longer read the
raw saved progress tag for controlled draw continuation decisions. The saved
payload and snapshot representation are unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 shared enemy-action label resolver increment (2026-10-24):**
ControlledChanceContinuationExecutorV111 now owns candidate-label validation
and index translation. SemanticEndTurnIndexEventV111 and the new
controlled-playable enemy-action label entry point use the same helper against
the saved ordered support. The numeric decision transcript and snapshot shape
are unchanged. Static source review and git diff --check only; no tests or
builds were run.

**S4 controlled-playable identity resolver increment (2026-10-24):**
ControlledPlayableCombatEngineV111 now accepts generated-card, potion,
transform, and target outcomes by stable model or enemy instance ID. Each
identity must match exactly one candidate in the live pending source support;
the resolver then uses the existing indexed continuation and transcript path.
Its weighted turn-intent boundary exposes the existing semantic event and
accepts an intent ID mapped to the first legal ticket in that candidate's
weight interval. No session or snapshot schema changed. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 controlled-playable replay identity increment (2026-10-24):** The
existing chronological live transcript now optionally stores ordered outcome
labels for identity-bearing supports. Restore compares them with the live
support at every replay boundary, catching same-sized support reorders that
request metadata does not encode. Numeric-only choices retain null entries;
snapshots without the optional field retain the legacy index-only path. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 shared request and candidate comparison increment (2026-10-24):** The
existing `ControlledChanceContinuationExecutorV111` now owns canonical typed
request matching and null-aware ordered candidate-label comparison, alongside
label-to-index validation. Configured ReferenceNature merchant, Treasure,
combat-reward, Shovel, and event-relic sessions plus controlled-playable replay
reuse those rules; their duplicate facade helpers were removed. Replay
behavior and snapshot shapes are unchanged. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 shared index-request comparison increment (2026-10-24):** The existing
executor now also compares nullable `RandomIndexRequestV111` values through
the canonical request projection. Controlled initial-HP and intent replay plus
ReferenceNature map-generation and later-act-preparation validation reuse it;
their duplicate request comparators were removed. Every request field and
ordered candidate remains part of the comparison. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 hand-draw progress presence owner increment (2026-10-24):** The pipeline
stochastic owner now exposes `HasSavedHandDrawProgress` for clean-boundary
presence checks. Semantic and controlled draw completion, automatic pipeline
opening and turn-start guards, generation admission, Mayhem guards, and combat
boundary validation use that query; mode decisions use the validated stage
queries. The mechanics diagnostic projection remains the only runtime reader
outside the owner that projects the tagged payload. No snapshot schema changed.
Static source review and `git diff --check` only; no tests or builds were run.

**S0 Native combat RNG entry-point census increment (2026-10-24):** The v55
inventory now traces Native initial-HP attachment, Tough Egg hatch, summoned
enemy creation, and monster-local sampling callsites through their fixed or
forwarded SiteIds. All seven Core callsites resolve to registered migration
rows, and the existing matrix check rejects unmapped Native combat-entry
helpers. This extends the source locator only; it does not establish native
source completeness or fidelity. Static inventory generation and
`git diff --check` only; no tests or builds were run.

**S0 Native random-choice callsite census increment (2026-10-24):** The v52
inventory now includes the two direct `NativeRandomChoiceV111` calls in the
Native relic-grab-bag source path, resolving both to existing matrix rows.
Calls inside `NativePlayerRngV111` are represented by that higher-level
adapter's separate callsite census. The matrix check rejects unmapped direct
Native random-choice calls outside the wrapper. External callers, indirect
entropy sources, Native source completeness, and the broader S0 evidence audit
remain open. Static inventory generation and `git diff --check` only; no tests
or builds were run.

**S0 legacy Fisher–Yates adapter census increment (2026-10-24):** The v51
direct-call inventory now includes all 14 direct
`LegacyFisherYatesV111` calls in Core and resolves each to an existing matrix
row. Site values follow direct expressions, finite helper caller closures, or
the Native event-context wrapper's validated shuffle list. The matrix check
rejects unmapped legacy shuffle calls. External callers, indirect entropy
sources, Native source completeness, and the broader S0 evidence audit remain
open. Static inventory generation and `git diff --check` only; no tests or
builds were run.

**S0 Native event-context adapter census increment (2026-10-24):** The v50
direct-call inventory now includes all 21 direct
`NativeEventContextRngV111` calls in Core and resolves each index,
bounded-integer, unit-float, and shuffle call to an existing matrix row. The
matrix check rejects unmapped event-context calls. External callers, indirect
entropy sources, Native source completeness, and the broader S0 evidence
audit remain open. Static inventory generation and `git diff --check` only;
no tests or builds were run.

**S0 legacy random-choice adapter census increment (2026-10-24):** The v49
direct-call inventory now includes all 56 direct `LegacyRandomChoiceV111`
calls in Core and resolves all 56 to existing matrix rows. Its source locator
traces finite method arguments through Core callers and maps the four Gaussian
`.component` subrequests to their parent rows; the matrix check rejects
unmapped calls. External callers, indirect entropy sources, Native source
completeness, and the broader S0 evidence audit remain open. Static inventory
generation and `git diff --check` only; no tests or builds were run.

**S4 automatic hand-draw suspension ownership increment (2026-10-24):** The
existing automatic hand-draw continuation owner now decides when saved draw
progress or a pre-draw choice keeps the pipeline at `DrawPlayerHand`. The
generic pipeline loop no longer inspects hand-draw progress tags or the
pre-draw random-pile boundary. Provider guards and serialized continuation
state are unchanged. Static source review and `git diff --check` only; no tests
or builds were run.

**S0 Native player-RNG callsite census increment (2026-10-24):** The v48
direct-call inventory now includes all 20 direct `NativePlayerRngV111` calls
in Core, resolving each to an existing migration-matrix site row. The
matrix-check option rejects unmapped Native player-RNG calls. This extends the
adapter coverage alongside the v47 inventory of 13 high-level `MapRandom`
calls in the modeled Overgrowth, Hive, and Glory generators. Indirect Core
delegates, other external callers, Native source completeness, and the broader
S0 evidence audit remain open. Static source inventory generation and
`git diff --check` only; no tests or builds were run.

**S0 modeled map RNG callsite census increment (2026-10-24):** The v47
direct-call inventory includes all 13 high-level `MapRandom` calls in the
modeled Overgrowth, Hive, and Glory generators, resolving each to its existing
migration-matrix site row. The matrix-check option rejects unmapped calls.
This closes the census gap for these Core source adapters only; indirect Core
delegates, other external callers, Native source completeness, and the broader
S0 evidence audit remain open. Static source inventory generation and
`git diff --check` only; no tests or builds were run.

**S4 automatic hand-draw stage preparation owner increment (2026-10-24):**
The automatic hand-draw stochastic owner now exposes cursor-validated queries
for saved semantic and controlled `BeforeHandDraw` progress. The existing
continuation owner uses them to validate deferred semantic entry, clear the
resumed hook marker, choose the source-result context, and dispatch resumed
controlled or semantic pre-draw hooks and controlled draw-hook choices. The
automatic-step switch and pipeline handoff no longer read those saved tags.
No snapshot schema changed. Static source review and `git diff --check` only;
no tests or builds were run.

**S4 automatic draw source-result owner increment (2026-10-24):** The source
`DrawPlayerHand` stage now sends every `DrawCards` result through
`ResumeAutomaticHandDrawSourceResult`. The shared owner handles provider
suspension routing, ordinary Stratagem completion, deferred stage completion,
and post-draw power timing. A typed context distinguishes ordinary work,
deferred semantic completion, and a resumed semantic pre-draw hook; the
pipeline step accepts only the first two and derives the third from its saved
progress marker. Deferred semantic work checks for its active provider before
drawing and again when publishing the result. A pending deferred result that
escapes provider routing now fails closed. The pipeline switch no longer
interprets result boundaries.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 suspended run/event mechanics comparison increment (2026-10-24):** The
provider-independent mechanics projection now includes pending run-action,
post-reward event-action, and event-effect continuation progress from their
existing mechanics owners, retaining action identity, effect cursor, and
queued consequences while excluding random requests and decision payloads.
Mechanics-comparison and provider-replay-comparison schema IDs advanced to v17
and v3. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 mechanics comparison input validation increment (2026-10-24):** The
provider-independent mechanics projection now validates the complete
`RunState` before stripping provider state. Raw-state comparisons and
mechanics fingerprints therefore reject invalid owner correspondence instead
of potentially treating matching malformed snapshots as equivalent. Static
source review and `git diff --check` only; no tests or builds were run.

**S0/S4 combat card-action admission census (2026-10-24):** Cross-checked
current source pool/model arrays against direct semantic player-action gates
and specialized semantic-pilot continuations. All 53 currently implemented
Colorless cards and all 88 implemented solo Silent pool cards have an action
route; five Silent pool entries remain explicitly multiplayer-only and without
implemented mechanics. This records model/admission coverage only, not Native
acquisition completeness or RNG parity. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 deterministic combat-potion admission increment (2026-10-24):** Blood
Potion and Fruit Juice now enter the semantic combat pilot through its existing
deterministic potion gate. Their modeled heal and Max HP run effects preserve
the ordinary combat result boundary and do not add a chance owner or snapshot
field. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 combat Entropic Brew continuation increment (2026-10-24):** The
ReferenceNature run-action session admits Entropic Brew in combat when its
matching combat potion is available and no combat choice is pending. Legacy
draws reuse the ordered `reward`-stream pool; Native run ownership uses the
`player.rewards` law, and an active combat sidecar receives the same advanced
run streams before remaining potion operations. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 shared draw-resume progress increment (2026-10-24):** Ordinary semantic
draw segments and automatic hand-draw stages now use the existing provider-
neutral `PrototypeDrawCardsResumeResultV111` progress owner, carrying run and
remaining count with optional requested count for hand-draw stage publication.
The duplicate transient result record was removed without changing saved
frame fields or transcript owners. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 automatic draw-count progress increment (2026-10-24):** Semantic
automatic hand-draw completion and partial draw resumes now use the shared
`GetCardsDrawnSince` mechanics query to validate and interpret combat card-count
deltas. This keeps requested and remaining progress tied to the same saved
checkpoint. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 tagged automatic hand-draw progress query increment (2026-10-24):** The
pipeline stochastic owner now exposes one stage-validated query returning the
active count-bearing progress tag and counts. Semantic and remaining-count
queries project from that value, and automatic cost validation reads the tag
and saved count together. The zero-count `BeforeHandDraw` projection also
requires the saved cursor to remain at that stage. Provider resume policies
and transcripts remain with their existing adapters. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic automatic hand-draw result ownership increment (2026-10-24):**
`ResumeSemanticAutomaticHandDrawStep` is now the single interpreter for active
draw, pending `BeforeHandDraw`, and completed zero or positive draw outcomes.
`BeginSemanticAutomaticHandDraw` returns its validated result without
rechecking zero-count boundaries. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 automatic hand-draw stage validation ownership increment (2026-10-24):**
The pipeline stochastic validator now checks `DrawPlayerHand` alignment once
for every active progress tag. Repeated per-mode stage checks were removed;
provider and pending-choice validation stays with each tagged mode. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 semantic hand-draw projection increment (2026-10-24):** The initial
semantic draw facade publishes requested and remaining counts directly from
the shared stage result, and frame validation identifies zero-count
`BeforeHandDraw` progress through the pipeline owner projection. An explicit
stage-and-payload query identifies that tagged state for combat and draw-hook
validation, semantic pre-draw resume paths, and the Mayhem cursor guard,
avoiding inference from `0/0` counts.
The semantic draw facade no longer reinterprets a pending random-pile choice.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 automatic hand-draw router context increment (2026-10-24):** The shared
boundary router now takes an explicit source, saved-controlled, or
saved-semantic context. It requires a semantic draw frame only for the saved
semantic route, removing the ambiguous saved-stage boolean/provider inference.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 draw-resume progress validation increment (2026-10-24):** The shared
transient result now rejects null runs and negative requested/remaining counts
in its constructor, covering every direct provider-neutral draw-resume result.
Its explicit validation still catches a default struct value before
publication; the established values and deconstruction shape are preserved.
No saved schema changed. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 controlled event-choice candidate replay increment (2026-10-24):**
Generic event-effect continuations now pass each saved decision's ordered
candidate labels into replay and compare them with the live support before
consuming the saved index. A support with the same size but changed identity or
order now fails closed; run-action and other replay owners are unchanged. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 controlled run-action candidate replay increment (2026-10-24):** Run-action
restoration and pending-frame source validation now supply the saved
candidate-label prefix to the same replay owner, which verifies each live
ordered support before consuming the saved index. This covers existing map,
encounter-entry, Shop, reward, rest, and act-transition frames without a second
replay mechanism or snapshot change. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 post-choice Event Entropic Brew continuation audit (2026-10-24):**
The controlled event-reward continuation already saves the Entropic Brew action
with its pending empty-slot draw, resumes the ordinary potion action after each
selected model, and retains the event-reward decision prefix. The architecture
support note now records this existing path and its boundary. Static source
review and `git diff --check` only; no tests or builds were run.

**S4 Bottled Potential continuation inventory increment (2026-10-24):**
Recorded the existing semantic path for its full-pool hand-return shuffle,
after-shuffle relic insertions, and resumed five-card draw under their current
owners. No new owner or schema was added. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 automatic enemy pile-insertion identity commit increment (2026-10-24):**
Enemy draw/discard insertion now resolves the saved generated-card and physical
gap label against the current pile, then commits through
`SemanticDrawPileAdapterV111`. The ordered index remains in the provider
transcript, and label-free snapshots keep their index path. Static source review
and `git diff --check` only; no tests or builds were run.

**S3 automatic enemy-theft identity commit increment (2026-10-24):** The
controlled chance replay owner now exposes the candidate label belonging to
the exact consumed index request. Enemy theft uses its saved physical
card-instance identity to apply the selected card against the live eligible
support; the ordered index remains in the provider transcript, and older
label-free snapshots continue through their index projection. Static source
review and `git diff --check` only; no tests or builds were run.

**S3 automatic summon-pool identity commit increment (2026-10-24):** The
legacy enemy summon resolver now applies the selected enemy model from its
saved `enemy:<id>:slot:<index>` candidate label after checking that the label's
slot still contains that model in the live filtered pool. The index remains in
the provider transcript; label-free snapshots keep their index path. Native
summon-pool selection remains outside this controlled continuation. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 draw-result and deferred-effect ownership increment (2026-10-24):** The
ordinary effect resolver now captures resumable draw operation suffixes and
their source context through one owner. `PublishDrawMechanicsState` now owns
coupled player/combat publication across draw results, active pipeline
progress, and hand-draw completion; `PublishDrawCardsResult` delegates to it.
Legacy and semantic effect draws share deferred card-ID accumulation and
keyword application. Standalone hook-choice frames reuse the semantic draw
frame factory. Provider-specific boundary interpretation and transcript
policies remain with their existing adapters, so full resume consolidation
and cross-provider equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 modeled Ancient card-action increment (2026-10-24):** Apparition,
Apotheosis, Maul, Whistle, the source-playable Enthralled curse, and Pael's
Horn's Relax now enter through the existing semantic player-action boundary.
Their modeled effects use ordinary source combat resolution under the
controlled-chance guard; no chance owner or snapshot schema changed. Wish keeps
its existing card-selection continuation, while unplayable Ancient curses
remain excluded. Static source review and `git diff --check` only; no tests or
builds were run.

**S2/S4 Mad Science source-action increment (2026-10-24):** The semantic
pilot now admits modeled Mad Science Attack, Skill, and Power riders. Wisdom
retains its semantic draw continuation; Skill/Chaos reuses the generated-card
model event after its Block effect, with admission bound to the saved Tinker
card and manual `CardPlayed` continuation. No event or snapshot schema changed.
Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Trash Heap combat-card increment (2026-10-24):** Distraction now
enters through its physical Exhausting card action and reuses the existing
generated-Skill model event. Rip and Tear reuses the existing random-target
event for both hits, with its physical card and remaining operation suffix
checked against the manual `CardPlayed` frame. No event or snapshot schema
changed. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 Metamorphosis source-action increment (2026-10-24):** The persistent
semantic pilot now admits the physical Metamorphosis card and validates its
manual Exhausting card-play frame, exact 3 (5 upgraded) generated-Attack
operation, and remaining-count suffix. Its model and draw-pile position choices
reuse the existing typed events and implemented Silent Attack pool; no event or
snapshot schema changed. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 Ricochet random-target action increment (2026-10-24):** Ricochet now
enters the persistent semantic pilot by its physical source-card identity. Its
four base or five upgraded random-target hits reuse the existing typed target
event and replay continuation; no event or snapshot schema changed. Static
source review and `git diff --check` only; no tests or builds were run.

**S0 v46 census refresh (2026-10-24):** Regenerated the direct-call census
against current Core sources and synchronized the ownership inventory. Static
checks report 111/111 typed request constructions resolved to registered sites,
185/185 registered sites mapped, 36/36 known helper callsites mapped, and
review-pointer matches for all five facets on all 185 rows. Site-matrix,
contract-link, and ambient-entropy checks pass; these results do not establish
source callsite completeness. Static inventory and `git diff --check` only; no
tests or builds were run.

**S4 automatic enemy-action semantic outcomes increment (2026-10-23):** The
existing controlled enemy-action frame now persists optional ordered
candidate labels with its index prefix. Card theft labels physical card
instances, summon-pool labels retain model identity and pool slot, and other
enemy-action sites label their bounded values. The semantic end-turn event
fingerprints those labels and `semantic_resolve_enemy_action` accepts either
`candidate_label` or the compatibility `selected_index`. Full source replay
validates the label support; snapshots without labels retain index replay.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 enemy pile-insertion identity follow-up (2026-10-23):** The shared
`ResolveRandomPilePosition` source now labels enemy draw/discard insertion
outcomes by the generated card model and the physical pile gap: empty pile,
before a card, between two card instances, or after a card. These labels flow
through the existing enemy-action replay and end-turn transcript; index
resolution remains compatible. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 controlled-playable combat boundary follow-up (2026-10-23):** The
controlled-playable facade now delegates its combat player boundary to
`PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary`, while retaining its
existing reward and terminal boundaries. This prevents a player action from
being exposed when a pending combat choice or automatic-pipeline cursor is not
represented by the facade's Nature transcript. Static source review and
`git diff --check` only; no tests or builds were run.

**S0 Native request and combat-card helper closure increment (2026-10-23):**
The direct-call census now resolves the Native generic bounded-index request
through its finite operation registry after the Native provider guard, and
traces `ResolveRandomCombatCardIndex` through all three registered Core
callers. All 111 typed request constructions now resolve to registry sites;
the inventory reports 185/185 registered sites mapped with no unresolved
references and 36/36 known helper callsites mapped. Class-qualified constant
resolution now indexes source types once per scan, reducing inventory
generation from over a minute to under ten seconds. Census schema v46; static
source analysis and `git diff --check` only, with no tests or builds run.

**S2/S4 repeated target and source-selection action increment
(2026-10-23):** Bouncing Flask now enters the persistent semantic pilot by
physical source identity and reuses the saved random-target continuation for
each Poison application. Hidden Daggers uses the existing card-selection event
for its hand discard and resumes the queued Shiv creation. Both paths preserve
the common action replay and add no event or snapshot schema. Native target
pool and stream/cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 deterministic Silent action increment (2026-10-23):** Echoing Slash and
Outbreak now use the ordinary semantic player-action boundary. Shadow Step
uses the existing source card-selection event for its hand discard and queued
power application. No new event or snapshot schema was introduced. Static
source review and `git diff --check` only; no tests or builds were run.

**S2/S4 source-card autoplay continuation increment (2026-10-23):** Knife
Trap now enters the persistent semantic pilot from its physical source-card
identity. Its ordered Exhaust-pile Shiv queue reuses the existing autoplay
target continuation when a prior Shiv defeats the chosen enemy. Native target
pool, stream, and cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 source card-selection increment (2026-10-23):** Concentrate now uses the
existing card-selection event for its three-card hand discard and resumes the
queued energy gain through the same replay boundary. No event or snapshot
schema changed. Static source review and `git diff --check` only; no tests or
builds were run.

**S0 validated event-site domain increment (2026-10-23):** The typed-request
inventory now follows three existing semantic boundaries: target-event site
validation through the target-kind resolver, the shortlist's finite supported
site predicate, and the pool-shuffle site's fail-closed stream resolver. This
maps all five forwarded `SemanticOtherChanceV111` event fields to their
registered site rows. The census reports 185 registered sites for 185
migration rows, no registry mismatches or unresolved references, and 109 of
111 typed requests mapped; two generic forwarded helper parameters remain
dynamic. Inventory schema v45; static source analysis only, with no tests or
builds run.

**S0 fail-closed member-site resolution increment (2026-10-23):** Typed
requests that forward a saved event's `SiteId` now resolve when the containing
resolver rejects every value except a directly compared registered site. This
maps the hand-cost target and Jeweled Mask random-pile-card boundaries without
inferring values from unrelated event state. The generated direct-call census
reports 185 registered sites for 185 migration rows, no registry mismatches or
unresolved references, and 106 of 111 typed requests mapped to registry sites;
five forwarded expressions remain dynamic. The inventory schema is v44;
static source analysis only, with no tests or builds run.

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

**S4 Native Treasure and Shovel session envelope consolidation (2026-10-21):** `ReferenceNatureTreasureSessionV111` and `ReferenceNatureNativeShovelSessionV111` now use the shared run-action mechanics checkpoint and controlled-action stochastic-prefix owners. Their relic laws, bag checks, and atomic cursor/replay validation remain session-specific; flattened snapshots and schemas remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 shared ReferenceNature cursor/transcript owner (2026-10-21):** Run-action and controlled-action stochastic owners now reuse `ReferenceNatureStochasticStateV111<TDecision>` for the initial/current sampler cursor and defensive typed transcript. Native event-gold, Glory Reflections, Neow-offer, Tablet of Truth, Trial, Trash Heap, Infested Automaton, Endless Conveyor, Punch Off fight/reward, and Native event-shuffle sessions compose that same owner with `PrototypeMechanicsContinuationStateV111` directly under a run-session continuation kind. Their flattened snapshots and schemas remain unchanged; law replay and run-cursor checks stay with each session. Static source review and `git diff --check` only; no tests or builds were run.

**S4 generator continuation cursor-owner increment (2026-10-21):** `ReferenceNatureMapGenerationSessionV111` and `ReferenceNatureLaterActPreparationSessionV111` now use the same generic cursor/transcript owner. Their generator requests, provider decisions, Chance Laws, maps, and encounter bags remain in their existing owners; flattened snapshot fields are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 generator continuation simulation-envelope increment (2026-10-21):** Map generation and later-act preparation now compose source-specific generator mechanics with the stochastic cursor, typed prefix, and pending request/law state through `SimulationStateV111`. This moves existing generator inputs, outputs, and stochastic fields under domain owners while preserving the flattened snapshot contracts. Static source review and `git diff --check` only; no tests or builds were run.

**S4 run-initialization audit cursor-owner increment (2026-10-21):** The completed map-generation audit and `ReferenceNatureRunInitializationTranscriptV111` now reuse `ReferenceNatureStochasticStateV111<TDecision>` for initial/final cursors and typed decisions. Their Chance Laws, map/prefix audits, and preparation metadata remain alongside the shared owner; serialized properties are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S4 remaining ReferenceNature cursor-owner increment (2026-10-21):** The Ancient-offer session, later-act assignment and encounter-preparation results, Underdocks and Overgrowth run results, and native encounter-formation result now reuse `ReferenceNatureStochasticStateV111<TDecision>` for their cursor pair and typed transcript. Their source-specific mechanics, generator outputs, Chance Laws, map audit, and Native formation audit retain their existing owners; snapshot records remain flat. Static source review and `git diff --check` only; no tests or builds were run.

**S4 remaining ReferenceNature mechanics/stochastic envelope increment (2026-10-21):** Ancient-offer and controlled event-choice sessions now compose `PrototypeMechanicsContinuationStateV111` with their stochastic owner through `SimulationStateV111`. Controlled event-choice stores its cursors and mixed player-action/Nature steps in one owner and projects the Nature-only transcript from those steps. Flattened serialized fields and snapshot schemas remain unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S7 PotionFactory model-law stream alignment (2026-10-21):** `QueryNativePotionFactoryModelIndex` now accepts the same registered Native streams as its rarity law: `run.combat_potion_generation`, `player.rewards`, and `player.shops`. It still validates the declared rarity and remaining source-ordered pool, and does not claim cursor or prefix validation. Static review and `git diff --check` only; no tests or builds were run.

**S4 shared combat potion model-pool increment (2026-10-21):** Ordinary `AcquireRandomCombatPotion` sampling, controlled suspension, and replay validation now use `CombatPotionModelPool`. This keeps the current ordered potion support under one source-pool owner without changing provider requests or continuation behavior. Native stream attribution, pool completeness, and seeded parity remain open. Static review and `git diff --check` only; no tests or builds were run.

**S6 configured combat-reward relic acquisition continuation increment (2026-10-21):** The existing `ReferenceNatureNativeEventRelicSessionV111` now accepts a legal configured Native `take_reward_relic` action for Beautiful Bracelet, Distinguished Cape, Pael's Legion, or Sand Castle. It captures the selected relic's already-registered acquisition request prefix, validates each request against the saved action, exact SiteId/stream allowlist, support labels, and prior choices, then commits the relic effect and Nature cursor together. `run.niche` bounded-index transforms are `SOURCE_VERIFIED`; retained `event` and `reward` requests remain `UNVERIFIED`. Other reward relic acquisitions remain outside this boundary. Static review and `git diff --check` only; no tests or builds were run.

**S6 Native-regional Treasure and Shovel fallback continuation increment (2026-10-21):** The generic `ReferenceNatureRunActionSessionV111` now handles Native-regional Treasure entry and Shovel Dig when `NativeRunRng` is absent, controlling the retained `relic_grab_bag.rarity.ticket` request and its conditional sorted-support `treasure.relic_grab_bag.model.index_draw`. Nature replay canonicalizes or creates the relic bag, validates the selected rarity and exact identity owner/support, and commits the Treasure room or Shovel reward action with the cursor. Retained index weights remain `UNVERIFIED`; Native queue/cursor parity and source-pool completeness remain open. Static review and `git diff --check` only; no tests or builds were run.

**S6 Neow Arcane Scroll and Phial Holster acquisition continuation increment (2026-10-21):** The existing Native event relic session now captures Arcane Scroll's Rare-card model request and Phial Holster's two potion model requests when acquired through Neow. It validates the source-owned `player.rewards` support and pool owner, rebuilds Phial Holster's first-open-slot sequence from the saved pickup and prior choices, and commits the relic effects with the Nature cursor after replay. Native bounded-index transforms are `SOURCE_VERIFIED` on their declared supports; pool fidelity and Native cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S6 Neow Lost Coffer reward-generation continuation increment (2026-10-21):** The existing Native event relic session now captures Lost Coffer's source-ordered legacy reward card-rarity tickets and card-model indices, followed by its optional potion-model index. It derives the maximum card-pick count and potion-prevention condition from the saved Neow action, validates each pending request and label prefix by replay, and commits the nested reward and Nature cursor together. Equal-index reward laws and Native pool/cursor fidelity remain `UNVERIFIED`; no tests or builds were run.

**S6 Neow Scroll Boxes card-bundle continuation increment (2026-10-21):** The existing Native event relic session now captures Scroll Boxes' separate full descending Common and Uncommon shuffles through `event.reward.card_bundle.shuffle_swap_index`, in source order, with the exact Neow owners and reconstructed `NativeSilentRewardCardPool` rarity filters. It commits the ordinary card-bundle reward with the Nature cursor after both prefixes replay. Legacy `reward` equal-index laws remain `UNVERIFIED`; Native pool fidelity and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S6 Neow Hefty Tablet and Lead Paperweight offer-shuffle continuation increment (2026-10-21):** The existing Native event relic session now captures the `run.random_pool_distinct.shuffle_swap_index` prefixes for Neow's Hefty Tablet Rare-card and Lead Paperweight Colorless-card rewards, in addition to Neow's Bones relic offers. Each descending swap is checked against its relic-specific reconstructed ordered pool and owner, and the nested Reward commits with the Nature cursor. The legacy `reward` equal-index laws remain `UNVERIFIED`; Native source-pool fidelity and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S6 Punch Off unowned relic-offer continuation increment (2026-10-21):** The existing `ReferenceNatureNativeEventRelicSessionV111` now admits the exact configured Native Punch Off page-zero `nab` action and captures its single `unowned_relic_selection` request. It reconstructs the ordered relic pool after the Injury is added, validates the owner and support, and commits the pending relic reward with the Nature cursor. The bounded-index transform is `SOURCE_VERIFIED`; source-pool fidelity, event/source parity, and Native cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S6 Neow's Bones relic-offer shuffle continuation increment (2026-10-21):** The existing `ReferenceNatureNativeEventRelicSessionV111` now captures the full `run.random_pool_distinct.shuffle_swap_index` prefix produced when Neow's Bones is acquired from the opening offer or a Neow relic reward. Each descending swap is validated against the reconstructed ordered eligible pool and owner, and the ordinary offer result commits with the Nature cursor. The draw uses legacy `reward`; its equal-index law remains `UNVERIFIED`, with Native source, pool completeness, and cursor parity open. Static source review and `git diff --check` only; no tests or builds were run.

**S6 Neow Small Capsule relic-pickup continuation increment (2026-10-21):** The existing `ReferenceNatureNativeEventRelicSessionV111` now captures the configured Native `unowned_relic_selection` request made when Small Capsule is selected from the Neow opening offers or a Neow relic reward. Its exact bounded-index law is conditioned on the acquired-relic-adjusted ordered pool, and the event/reward result commits with the Nature cursor after replay. Other Neow pickup effects, source-pool completeness, and Native cursor parity remain open. Static review and `git diff --check` only; no tests or builds were run.

**S6 Toy Box Shop-purchase relic-pull continuation increment (2026-10-21):** The existing `ReferenceNatureMerchantSessionV111` now captures Toy Box's five configured Native relic-bag rarity and identity pulls during its legal Shop purchase. Each rarity law uses exact rounded Native float-threshold masses; each identity choice uses the captured sorted residual bag support with equal `UNVERIFIED` weights. The ordinary purchase replay validates the full request and label prefix, and session validation requires the exact five-pair order plus the completed five-offer reward before committing with the Nature cursor. Native queue order and cursor remain private and unchanged; other later bag-pull boundaries remain open. `git diff --check` passed; no tests or builds were run.

**S3 shared Nature relic-pull transition increment (2026-10-21):** `PrototypeNativeRelicGrabBag.PlanNaturePull` now derives sorted identity support only from a `NatureUnordered` bag, and `PullNatureSelected` revalidates support before removing the selected identity. Controlled draws and event-relic, Shovel, and combat-reward prefix replay now use these shared operations, keeping Native queue-front pulls unchanged. Static call-site review and `git diff --check` only; no tests or builds were run.

**S6 configured experimental three-act start continuation increment (2026-10-21):** `ReferenceNatureRunActionSessionV111` now composes the configured boss choice, Overgrowth map session when applicable, and existing Ancient-assignment/later-act bag generators. Each resolve advances one existing generator choice; completed Act 1 snapshots retain the preparation RNG origin and replay the full prefix against persisted bags. A pending initialization marker keeps partial runs unavailable until all bags commit with the transcript and Nature cursor. Equal-index later-act laws remain `UNVERIFIED`; Native `UpFront` ordering, source fidelity and cursor parity remain open. `git diff --check` passed; no tests or builds were run.

**S6 Native-shaped event potion-choice increment (2026-10-21):** The existing controlled event-choice continuation now admits `potion.uniform_model.index_draw`, `potion.rarity_roll.ticket`, and `potion.model_within_rarity.index_draw`. This composes Wellspring's `bottle` and Whispering Hollow's two-potion `gold` choices through the existing replay path, preserving each request's ordered support and chronology. Equal-index law weights, pool fidelity and Native cursor parity remain `UNVERIFIED`; no new session was introduced, and no tests or builds were run.

**S6 experimental later-act transition continuation increment (2026-10-21):** `ReferenceNatureRunActionSessionV111` composes the existing Hive/Glory map-generation session and, when the prepared bag assigns an Ancient, its existing Ancient-offer session with configured experimental `continue_act`. The run stays at the act-transition boundary until the map and offers finish; the engine then applies ordinary post-step hooks once. Saved action sessions replay both prefixes against their existing generators, and completed runs retain the map audit plus a deep-copied Ancient-offer session snapshot that RunStochasticState replays on restore. Restore upgrades older completed map-only snapshots that lack that audit and replays pre-composition Native Ancient openings from their original transcript. Map geometry, request call order, Gaussian finite-grid masses and Native cursor parity remain open. `git diff --check` passed; no tests or builds were run.

**S6 completed later-act map audit ownership increment (2026-10-21):** `RunStochasticStateV111` now retains completed Hive/Glory ReferenceNature map audits produced by act-transition sessions. Run snapshots deep-copy and replay each audit against the existing generator, validate act/seed/Ascension order, and require the run cursor to follow the final map cursor. Native transitions without a ReferenceNature map session keep the audit history absent. Map geometry and cursor parity remain open; `git diff --check` passed, with no tests or builds run.

**S6 configured Overgrowth ReferenceNature start continuation increment (2026-10-21):** `ReferenceNatureRunActionSessionV111` now suspends on the configured Overgrowth boss draw, then composes `ReferenceNatureMapGenerationSessionV111` for the source-shaped Act 1 map. Each resolve advances one boss or typed map request; the run and full initialization audit commit only after map completion. Saved-session replay reconstructs the nested map prefix through the existing generator. At this checkpoint, other startup calls retain their providers and experimental later-act preparation remains outside this continuation. Boss and Gaussian laws remain `UNVERIFIED`; Native pool/map fidelity and cursor parity remain open. `git diff --check` passed; no tests or builds were run.

**S6 configured Underdocks ReferenceNature start continuation increment (2026-10-21):** The existing `ReferenceNatureRunActionSessionV111` now suspends on the configured Underdocks boss index during `start_run`. A scoped run-start marker sends replay through the existing non-Nature factory so the session controls exactly that request while other startup draws keep their current providers; the hidden Overgrowth boss draw in the shared starter remains outside the filter. Completion commits the selected boss, Nature cursor, controlled run-action trace, and one-choice run-initialization transcript together. At this checkpoint, configured Overgrowth map/start and experimental later-act preparation are not yet resumable. Equal-index boss probability remains `UNVERIFIED`; `git diff --check` passed, with no tests or builds run.

**S6 Native encounter-formation ReferenceNature provider increment (2026-10-21):** `NativeEncounterFormationFactoryV111.GenerateWithReferenceNature` now injects a ReferenceNature index provider through the existing source-shaped factory and caller source resolver. It returns the formation, initial/final Nature cursor, Chance Laws, and replayable decision transcript as one validated result; validation rederives each Native formation law from the recorded encounter entry, floor, support, and ordinal. The counterfactual path leaves the Native encounter cursor at its initial position and is not a saved combat-entry continuation. The exact Native bounded-index transform is `SOURCE_VERIFIED` only for declared support; candidate support/order, preceding source calls, source completeness, seed derivation/frequencies, and cursor parity remain open. `git diff --check` passed; no tests or builds were run.

**S6 configured Native formation ReferenceNature continuation increment (2026-10-21):** The existing `ReferenceNatureRunActionSessionV111` now suspends on `combat.encounter.formation.index_draw` during configured experimental Native Act 1 Combat, Elite, or Boss entry. It replays the ordinary map-node action with each selected formation index and commits the resulting combat and Nature cursor together; other combat-entry requests keep their existing providers. The existing controlled run-action envelope owns the pending request and history, with numeric labels for source resolvers that declare only an index bound. Native encounter context cursors remain unadvanced by Nature choices. `git diff --check` passed; no tests or builds were run.

**S6 configured Native encounter-pool ReferenceNature continuation increment (2026-10-21):** The same controlled run-action envelope now captures the supported Overgrowth and Underdocks weak, normal, and elite encounter-pool index requests before Native formation. Each decision replays the map-node action against its ordered support and commits with the final combat and Nature cursor; unrelated combat-entry requests keep their providers. The counterfactual selections do not advance the `combat` provider cursor, and their equal-index laws remain `UNVERIFIED` with source-pool fidelity open. `git diff --check` passed; no tests or builds were run.

**S6 configured Overgrowth ReferenceNature map increment (2026-10-21):** The configured Overgrowth boss factory now continues the run-owned Nature cursor through `ReferenceNatureMapGenerationSessionV111` and installs the generated source-shaped map. `Begin` accepts the existing cursor, and the completed run-start transcript retains a compact typed request prefix plus the map fingerprint; snapshot validation reconstructs the map through the existing generator without duplicating the map in the audit. Bounded map laws retain `SOURCE_VERIFIED` transform status for declared supports, while Gaussian quota masses and Native geometry/cursor fidelity remain open. Neow offers still use their Native event context. `git diff --check` passed; no tests or builds were run.

**S6 Overgrowth boss run-creation ReferenceNature increment (2026-10-21):** `PrototypeNativeOvergrowthRunFactory.CreateWithReferenceNature` scopes the existing `start_run` action to sample its ordered boss-pool index through `QueryOvergrowthBossEncounter`, while delegating the legacy prototype map-generation requests to their established provider. It returns the configured run, initial/final cursor, Chance Law, and decision as a validated result; `V111RunFactory.Initialize` uses this path whenever an Overgrowth run already owns a Nature cursor. The legacy combat cursor remains at its clean initial position. The equal-index law remains `UNVERIFIED`, and the synchronous start path does not persist a Chance Law transcript or establish source-pool fidelity. `git diff --check` passed; no tests or builds were run.

**S6 configured run-initialization transcript persistence increment (2026-10-21):** Configured runs with a ReferenceNature cursor now retain an optional run-stochastic transcript containing the initial/final cursor, ordered Chance Laws, and decisions from Act 1 boss selection and, on the experimental three-act route, Ancient assignment and prepared encounter bags. `RunStochasticStateV111` owns and validates the transcript by replaying the declared laws; the per-factory validators continue to bind selected outcomes to their mechanics boundaries. Existing snapshots without the field remain readable, and snapshots without a Nature cursor omit it. This does not make `start_run` resumable or upgrade the `UNVERIFIED` equal-index laws. `git diff --check` passed; no tests or builds were run.

**S6 Underdocks boss run-creation ReferenceNature increment (2026-10-21):** `PrototypeNativeUnderdocksRunFactory.CreateWithReferenceNature` reuses the existing initializer and provider scope to sample its one ordered boss-pool index from `QueryUnderdocksBossEncounter`, then returns the initialized run, initial/final cursor, Chance Law, and decision as one validated result. Configured run initialization uses that path when the incoming run already owns a Nature cursor; ordinary initialization remains unchanged. `ReferenceNatureDecisionV111` now accepts the existing stable `encounter:<id>` outcome identity. The legacy `combat` cursor remains at its clean post-reset position. The pool's equal-index law remains `UNVERIFIED`, and this does not add a resumable run-start session or source-pool evidence. `git diff --check` passed; no tests or builds were run.

**S5 configured Underdocks Nature cursor entry increment (2026-10-21):** `V111RunFactory.CreateWithReferenceNature` seeds the run-owned sampler before initialization, and JSONL `reset_v111` accepts optional `nature_seed` and reports the cursor schema. A configured Underdocks `start_run` now reaches the existing one-choice Nature factory without callers having to mutate a snapshot; ordinary reset behavior remains unchanged when the field is omitted. This does not add a resumable startup session or change the boss law's `UNVERIFIED` status. `git diff --check` passed; no tests or builds were run.

**S6 Endless Conveyor Suspicious Condiment increment (2026-10-21):** The existing ReferenceNature session now samples the `potion.uniform_model.index_draw` request when a legal Suspicious Condiment Grab queues its potion. It replays the ordinary action, verifies the selected potion and pending reward boundary, preserves the legacy `reward` stream and Native event cursor, then commits the Nature cursor. Taking or skipping the potion and resolving the deferred next-dish ticket remain a separate action/session. The full-pool model weights remain `UNVERIFIED`; `git diff --check` passed, and no tests or builds were run.

**Tracking:** [S1 Chance Law and ReferenceNature issue #174](https://github.com/nn971/sts2-emulator/issues/174); [Nature pilot #143](https://github.com/nn971/sts2-emulator/issues/143); [native inspector #122](https://github.com/nn971/sts2-emulator/issues/122). The existing site ledger and this architecture's ownership crosswalk are joined in the [site and state inventory](STOCHASTIC_SITE_OWNERSHIP_INVENTORY.md); that file deliberately does not duplicate per-site rows.

**S3 combat payload ownership increment (2026-10-21):** `PrototypeCombatCardSnapshot` now owns its opaque JSON state and keyword-override array, and `PrototypeKnowledgeDemonChoiceState` owns its ordered card offers, on construction, replacement, and projection. Their existing fork methods delegate to those owners without changing the positional or JSON surface. `git diff --check` passed; no tests or builds were run.

**S3 combat-counter history ownership increment (2026-10-21):** `PrototypeCombatCounters` now copies its played-card tag and identity histories on construction, replacement, and projection; its fork delegates those array copies to the value owner while preserving independent card snapshots. Positional constructor, deconstruction, and JSON members remain unchanged. `git diff --check` passed; no tests or builds were run.

**S3 queued draw-operation ownership increment (2026-10-21):** `PrototypeQueuedOperation` now copies its deferred drawn-card identity prefix on construction, replacement, and projection, and exposes a `Fork()` owner. Choice-resolution, Personal Hive, and pending combat-choice continuation forks use that owner instead of repeating array-copy logic. Positional constructor, deconstruction, and JSON shape remain unchanged. `git diff --check` passed; no tests or builds were run.

**S3 controlled trace value ownership increment (2026-10-21):** Controlled event-choice and run-action traces now own their typed decisions, nested request support, and candidate-label arrays on construction, replacement, and projection; their parent stochastic history still deep-forks them. Positional constructors, deconstruction, and JSON member names remain intact. `git diff --check` passed; no tests or builds were run.

**S3 controlled event-enumeration result ownership increment (2026-10-21):** Controlled event enumeration results now copy the ordered outcome array and records on construction, replacement, and projection; explicit result forks also fork each child run. Positional constructors, deconstruction, and JSON members remain unchanged. `git diff --check` passed; no tests or builds were run.

**S4 pilot replay validation ownership increment (2026-10-21):** Pilot replay now sends expected draw, random-pile, and cost decisions through the typed resume owners; source-event validation occurs before mechanics application there, while the pilot retains branch index checks and post-replay transcript comparison for exact outcome identity. Duplicate facade prechecks were removed without changing replay history shapes. Static review and `git diff --check` passed; no tests or builds were run.

**S4 end-turn typed validation ownership increment (2026-10-21):** End-turn nested-choice preflight now checks only the facade's live boundary; choice-envelope, source-event, and selected-payload validation runs in `SemanticChoiceResumeV111` before mechanics replay. End-turn chronology and serialized decision shapes are unchanged. The three static inventory gates and `git diff --check` passed; no tests or builds were run.

**S4 pilot cost validation ownership increment (2026-10-21):** Live pilot cost selection now passes its typed decision directly to the shared publication/resume path after choice construction; the common choice owner performs source-event validation before mechanics replay. Cost choice and pilot history shapes are unchanged. Static review and `git diff --check` passed; no tests or builds were run.

**S3 legacy recycle shuffle provider increment (2026-10-21):** The ordinary legacy recycle fallback now uses `LegacyFisherYatesV111` instead of calling `PrototypeRng.Shuffle` directly. `LegacyFisherYatesV111` and the Native path share the same descending swap core, while each wrapper validates its own site contract and provider. The modulo-based legacy stream sequence is preserved and every swap now enters the typed legacy index provider. A source census leaves only `LegacyPrototypeRandomIndexProviderV111` calling `PrototypeRng.NextInt`; `git diff --check` passed, with no tests or builds run.

**S3 RNG bundle identity validation increment (2026-10-21):** `RngBundle` now rejects null collections, null or unnamed streams, and duplicate stream IDs when constructed or replaced, and `ReplaceStreamAt` enforces the same invariant. The empty bundle remains supported. Static callsite review found no current bundle construction that relies on duplicate IDs; `git diff --check` passed, with no tests or builds run.

**S2 legacy Fisher-Yates site contract increment (2026-10-21):** `LegacyFisherYatesV111` now accepts only the finite registry of Native Fisher-Yates boundaries plus legacy-only recycle, reward-bundle, distinct-pool, shop-offer, and Sand Castle shuffle sites. Legacy typed requests at those sites require `bounded_integer`; the wider legacy choice registry remains available to non-shuffle operations. All current wrapper callers were traced to the allowlist; `git diff --check` passed, with no tests or builds run.

**S3 provider-aware card-move increment (2026-10-21):** `SemanticDrawPileAdapterV111.ApplyCardZoneMove` now owns source/destination zone movement and selects the matching provider-aware transition for DrawPile insertion, removal, or unchanged draw membership. `CombatCardZonesV111.MoveCards` accepts a destination index and preserves the requested identity order; DrawPile positions are bottom-first. Existing autoplay and card-play moves now use this common commit path. `git diff --check` passed; no tests or builds were run.

**S0 shared shuffle-core site resolution increment (2026-10-21):** Census v38 resolves the typed index request inside `NativeFisherYatesV111.ShuffleInPlaceCore` through the union of the Native and legacy finite shuffle registries, but only while it finds both guarded wrapper call paths and confirms that they pass the same `siteId` to the core. All 101 typed request constructions now resolve to registered site IDs; forwarded values elsewhere remain review items. The regenerated census and `git diff --check` pass; no tests or builds were run.

**S4 provider-neutral typed resume increment (2026-10-21):** `SemanticChoiceResumeV111` now exposes typed overloads for draw and cost, insertion, generated card and potion, transform, shortlist, pool shuffle, hand-cost selection, card selection, draw hook, and Knowledge Demon decisions. The end-turn transcript switch delegates to the same overloads; standalone semantic draw and Stratagem facades call them directly instead of constructing `SemanticEndTurnNestedChoiceV111`. End-turn and pilot transcript records retain their established shape. Choice validation, checkpoint forking, and decision-copy behavior remain at the shared owner. `git diff --check` passed; no tests or builds were run.

**S4 shared live and replay choice dispatch increment (2026-10-21):** Live end-turn nested choices and live pilot card selections now enter the same typed resume owner used by restore. End-turn chronology and pilot publication remain with their respective facades, while source-event/action validation and mechanics replay stay at the shared choice owner. Transcript and snapshot shapes are unchanged. `git diff --check` passed; no tests or builds were run.

**S4 semantic deck-reveal resume increment (2026-10-21):** Pilot peek/scry resolution now routes its typed decision through `SemanticChoiceResumeV111`, which validates and copies both the decision and run checkpoint before the existing provider-aware deck and physical-pile commit. Pilot history and serialized decision shape are unchanged. `git diff --check` passed; no tests or builds were run.

**S3 mechanics comparison result ownership increment (2026-10-21):** `SimulationMechanicsComparisonV111` now copies its difference array on construction, replacement, and read while retaining its positional constructor and JSON member. Callers cannot mutate a returned diagnostic result through an array alias. `git diff --check` passed; no tests or builds were run.

**S4 typed card-selection replay increment (2026-10-21):** `SemanticCardSelectionEngineV111.Resume` now validates a recorded decision against a newly described source event and executes that action through the mechanics kernel. `SemanticChoiceResumeV111` dispatches typed card-selection decisions here instead of rebuilding a selection through `Step(actionId)`; `Step` remains a select-then-resume entry point. Restore and pilot/end-turn history shapes stay unchanged. `git diff --check` passed; no tests or builds were run.

**S4 shared semantic draw-cost replay increment (2026-10-21):** The standalone semantic draw facade now sends both ordinary drawn-card cost and nested effect-draw cost resumption through `SemanticChoiceResumeV111`. The shared owner keeps the existing bounded cost validation, engine-owned reward policy, controlled guard, and remaining-count calculation; the draw facade retains its cost transcript and progress publication. No event or snapshot fields changed. `git diff --check` passed; no tests or builds were run.

**S4 semantic automatic-draw resume increment (2026-10-21):** Draw-hook action, Stratagem selection, selected draw-pile identity, and after-shuffle insertion continuations now re-enter automatic semantic hand draws through `ResumeSemanticAutomaticHandDraw`. The shared helper owns the controlled guard and dispatches through the existing semantic draw kernel, preserving the pipeline's saved requested/remaining counts and transcript facades. Static review and `git diff --check` only; no tests or builds were run.

**S4 typed Knowledge Demon replay increment (2026-10-21):** `SemanticKnowledgeDemonChoiceEngineV111.Resume` now applies an owned typed decision after re-deriving the saved offer and checking its current legal action payload. The shared typed dispatcher calls this path directly, avoiding a second live-selection pass during replay. The legacy event-plus-action resolver composes selection with the typed resume, preserving its API and result. Event/history shapes are unchanged; `git diff --check` passed, with no tests or builds run.

**S4 semantic random-pile resume consolidation (2026-10-21):** The draw facade now routes Jeweled Mask's typed random-pile-card decision through `SemanticChoiceResumeV111`, which validates and forks both the checkpoint and decision before invoking the existing automatic-hook mechanics resume. The specialized owner retains its exact opening hook, subscriber, candidate, and pipeline checks. Draw transcript shape and replay order are unchanged. Static review and `git diff --check` passed; no tests or builds were run.

**S4 shared target-resume ownership correction (2026-10-21):** `SemanticChoiceResumeV111.ResumeTarget` now owns the target decision fork alongside the mechanics checkpoint fork. End-turn and pilot callers pass their selected value directly and retain separate forks only for their transcripts/history, removing caller-side duplicate copies. Target validation, transcript chronology, and serialized shapes are unchanged. Static call-site review and `git diff --check` passed; no tests or builds were run.

**S4 ordinary semantic draw resume consolidation (2026-10-21):** The top-level semantic draw facade now routes a selected physical card through `SemanticChoiceResumeV111.ResumeDrawSegment`. The shared owner validates the pending draw event and provider/zone boundary, forks the checkpoint and decision, commits the provider event with physical identity, resumes the existing DrawCards kernel, and verifies the recorded identity. Nested card-effect draws remain on their separate effect-draw resume because they carry a saved operation continuation. Event and transcript shapes are unchanged. Static review and `git diff --check` passed; no tests or builds were run.

**S4 draw-hook action resume consolidation (2026-10-21):** Standalone and automatic semantic draw-hook continuations now select the legal action, then use `SemanticChoiceResumeV111` for the typed decision replay. `SemanticDrawHookChoiceEngineV111.Resume` owns live-event/action validation and ordinary mechanics replay; the draw owner retains the follow-up hand-draw or pipeline continuation and exact nested-choice guard. Draw and Stratagem facades pass the immutable run checkpoint directly so the shared owner controls the checkpoint fork. Transcript schemas and action identity are unchanged. Static review and `git diff --check` passed; no tests or builds were run.

**S3 multi-zone identity transition owner increment (2026-10-21):** The existing `SemanticDrawPileAdapterV111.ApplyZoneTransition` routes identity-preserving moves across the six combat zones through `ApplyZoneIdentityDelta`; explicit additions, removals, and non-draw replacements use the typed identity-delta/replacement operations, while draw-pile changes retain their provider-aware methods. This records the current common multi-zone API and supersedes older roadmap entries that still list that API as open. `git diff --check` passed; no tests or builds were run.

**S4 semantic combat-draw replay cursor increment (2026-10-21):** `SemanticCombatDrawReplayCursorV111` now owns consumption across the draw frame’s typed deck, cost, pile-card, insertion, Stratagem, and draw-hook transcripts. The replayed mechanics state still determines the next pending boundary, while the cursor owns per-transcript progress and complete-consumption checks. Snapshot members and chronology remain unchanged. `git diff --check` passed; no tests or builds were run.

**S4 semantic end-turn replay chronology increment (2026-10-21):** Restore now replays nested automatic choices through one chronological nested-choice cursor and the shared resume path. It uses the engine’s common supported-choice predicate to detect nested suspensions and the stochastic owner’s validated per-kind boundary indexes to select enemy-action and target decisions, leaving the replay loop responsible for matching live pending boundaries and reproducing outcomes. This removes duplicate per-kind cursor accounting from source replay while preserving serialized transcripts. `git diff --check` passed; no tests or builds were run.

**S3 common action-payload ownership increment (2026-10-21):** `GameAction` now clones its JSON payload on construction and `with` replacement, so action values remain valid after the caller disposes the source `JsonDocument`. The serialized member names and action JSON shape remain unchanged; `git diff --check` passed. No tests or builds were run.

**S3 scripted replay history ownership increment (2026-10-21):** Scripted action records now clone their JSON payload and own typed decisions; whole-run sessions and serialized snapshots copy completed-action arrays; per-action sessions and snapshots own their action payload and typed decision prefix; scripted outcomes own candidate labels. Construction, replacement, and projections preserve the existing positional and JSON surfaces while preventing mutable aliases through replay history. Validation, save/restore, and fork use the owned values. Static indexed-write inspection found no writes through the returned history arrays; `git diff --check` passed. No tests or builds were run.

**S3 static stochastic support ownership increment (2026-10-21):** `PrototypeContent` now keeps starting deck/relic templates and shared Native/legacy card, potion, and relic pools in private backing arrays; `ExperimentalSilentThreeActRouteV111` does the same for Hive/Glory encounter, boss-discovery, and event pools; and Darv, Trash Heap, and Pael's Legion keep their supported relic/card/skin lists private. Existing array member types and access patterns are preserved as copy-returning properties, preventing callers from changing shared candidate support or source ordering. Static indexed-write inspection found no direct writes through these projections; `git diff --check` passed. No tests or builds were run.

**S3 shared simulation-envelope completeness increment (2026-10-21):** `SimulationStateV111<TMechanics, TStochastic>` now rejects null mechanics or provider owners during construction and `with` replacement. This centralizes the complete-envelope invariant across run, world, combat, event, and specialized continuation compositions; the former combat-only owner-null guard was removed. Existing property, constructor, deconstruction, and snapshot surfaces remain unchanged. `git diff --check` passed; no tests or builds were run.

**S5 semantic enumeration result ownership increment (2026-10-21):** Enumeration results now copy their ordered outcome arrays and outcome records on construction, replacement, and projection; explicit result forks also fork child pilots. Child-session projections retain their own defensive owners. Outcome member names and JSON shape remain unchanged. `git diff --check` passed; no tests or builds were run.

**S3 semantic pilot history discriminant increment (2026-10-21):** `SemanticPilotContinuationV111` now validates that each history kind has exactly one matching typed decision or inline player/index value, then validates that payload before fork or replay. The pilot frame validates every saved history entry, preventing malformed or cross-kind payloads from being silently copied or interpreted during restore. Existing history members and JSON names remain unchanged. `git diff --check` passed; no tests or builds were run.

**S3 combat event-dispatch value ownership increment (2026-10-21):** `PrototypeEventSubscriberState` now owns its effect array and power-card payload across construction, replacement, projection, and fork. `PrototypeEventDispatchContinuationState` likewise owns its current subscriber and remaining queue, closing array aliasing through an in-flight combat-event continuation. Positional constructors, deconstruction, and JSON members remain unchanged. `git diff --check` passed; no tests or builds were run.

**S4 semantic pilot nested action handoff increment (2026-10-21):** Nested autoplay `CardDrawn` actions and Knowledge Demon choices now select typed decisions in the pilot and enter `ResumeAndPublishNestedChoice`, sharing mechanics resume, end-turn chronology, pilot history, run publication, and pending-frame transition. Hand-draw hook choices keep their draw-facade transcript path. `git diff --check` passed; no tests or builds were run.

**S4 end-turn nested replay validation ownership increment (2026-10-21):** End-turn restore keeps its per-kind boundary order, pending-choice, and transcript-index guards, then routes each typed nested outcome through the shared continuation. That continuation validates the typed decision against its pending source event before mechanics replay for restored, live, and pilot choices, removing duplicate prechecks from restore and preventing replay from preceding source validation. Cross-provider mechanical equivalence remains open. `git diff --check` passed; no tests or builds were run.

**S3 controlled stochastic continuation value ownership increment (2026-10-21):** Mixed controlled continuations now deep-copy typed decision prefixes and pending candidate labels on construction and replacement, with defensive array projections. The public suspended-chance exception also copies candidate labels at construction and return. Existing record members and JSON names are retained. `git diff --check` passed; no tests or builds were run.

**S3 semantic choice event value ownership increment (2026-10-21):** Target, generated-card, potion-generation, cost, card-transform, hand-cost-selection, card-shortlist, pool-shuffle, draw-insertion, weighted-intent, random-pile-card, draw-hook, and Knowledge Demon events now own their candidate support on construction and replacement and return defensive projections. Decision forks use each event's copy owner. Event schemas, constructors, deconstruction, and JSON names remain unchanged. `git diff --check` passed; no tests or builds were run.

**S3 semantic card-selection value ownership increment (2026-10-21):** Card-selection candidate, event, decision, and completed-frame values now copy their candidate and selected-identity arrays on construction and replacement and return defensive projections. Positional constructors, deconstruction, `with` replacement, JSON member names, and event schema IDs remain unchanged. `git diff --check` passed; no tests or builds were run.

**S3 combat simulation publication guard increment (2026-10-21):** The public `CombatState.Simulation` initializer now rejects draw-pile changes when a semantic deck exists before or after replacement, closing the envelope-level bypass around the compatibility setter guard. The draw-pile adapter publishes the paired mechanics/provider transition through `CommitSemanticDrawPileTransition` after validating candidate membership and Native order correspondence. Serialized snapshot shape and legacy properties are unchanged. `git diff --check` passed; no tests or builds were run.

**S6 merchant purchase stream validation increment (2026-10-21):** The Native merchant session now shares the controlled run-action purchase stream allowlist when validating registered typed index requests. The source audit traced `StepShopCore` card and relic purchases through card-added/relic-acquired hooks, including Bracelet/Cape/Legion/Sand Castle indices, random-card/potion run effects, potion-slot fill, and Alchemical Coffer's separate PotionFactory transcript. A request with a registered SiteId but an unsupported stream now fails closed in the Nature session as it already did in the controlled continuation. The audit found no currently reachable purchase request missing from the typed-law path; generic noncanonical-stream weights remain `UNVERIFIED`. `git diff --check` passed; no tests or builds were run.

**S3 pilot audit value ownership increment (2026-10-21):** `RandomBoundaryEventV111.Fork` and `RandomBoundaryTranscriptEntryV111.Fork` centralize copies of request support and serialized payloads. `SemanticNativePolicyAuditV111` now owns defensive copies of typed Native decisions and mixed trace entries, while `SemanticSourceRouteAuditV111` owns action IDs and native entry decisions. Their `Fork` methods centralize audit copies, and the pilot stochastic owner delegates to those value types instead of reproducing nested array copies. Public record members and JSON names are retained. `git diff --check` passed; no tests or builds were run.

**S4 semantic pilot nested-choice publication increment (2026-10-21):** The semantic pilot now uses one `ResumeAndPublishNestedChoice` path for card selection, generated card/potion, transform, cost, shortlist, pool shuffle, hand-cost selection, nested effect draw/cost outcomes, and non-end-turn draw insertion. It dispatches through `SemanticChoiceResumeV111`, continues the saved end-turn frame when present, and publishes the new run, history entry, and pending-frame state together. Draw insertion retains its specialized paired-zone commit and validation because it publishes a new physical card; end-turn insertion also retains its hand-draw facade. Full common automatic-pipeline resume and cross-provider mechanical equivalence remain open. `git diff --check` passed; no tests or builds were run.

**S3 continuation mechanics ownership increment (2026-10-21):** Controlled event-choice mechanics now copy queued potion IDs and deep-fork queued deck-choice records. Controlled playable mechanics own copies of the origin intent checkpoint and player-action history. Semantic pilot mechanics own their opening fixture and deeply fork typed history entries through a shared continuation fork method. Existing flattened snapshot and JSON members remain unchanged. `git diff --check` passed; no tests or builds were run.

**S4 typed continuation SiteId increment (2026-10-21):** The shared nested-choice resume path now carries the SiteId from the recorded draw or draw-insertion event into end-turn boundary handling. Random insertion sites no longer inherit the after-shuffle relic SiteId. `git diff --check` passed; no tests or builds were run.

**S3 ReferenceNature session transcript ownership increment (2026-10-20):** Run-action and Ancient-offer session decision transcripts, plus controlled event-choice session steps, now copy on construction/replacement and return defensive projections. Their save snapshots apply the same ownership rule, including deep copies of event player-action payloads. `ControlledStochasticDecisionV111` also owns nested candidate labels so provider journals cannot share writable support metadata. Positional record/deconstruction and JSON members remain unchanged. `git diff --check` passed; no tests or builds were run.

**S3 ReferenceNature session ownership increment (2026-10-20):** All ReferenceNature session families now copy decision arrays on construction, replacement, projection, and save-snapshot boundaries. Configured merchant, Treasure, Native event-relic, Native Shovel, and combat-reward sessions also own typed provider prefixes, pending requests, and candidate labels; map-generation sessions own typed decisions, requests, and map outputs; Native event-shuffle sessions own planned candidate identities. Shared `ReferenceNatureSessionValueCopiesV111` handles common array, provider-decision, and typed-request copies. Session schemas and positional record shapes remain unchanged. `git diff --check` passed; no tests or builds were run.

**S3 capability and law-result ownership increment (2026-10-20):** Session coverage, Chance Law query coverage, stochastic capability manifests, and Chance Law results now copy their array inputs on construction and replacement and return defensive projections for advertised SiteIds, operations, statuses, and outcomes. Existing positional constructors, deconstruction, JSON members, and `with` updates remain available. Validated support cannot be changed through shared arrays after the boundary check. `git diff --check` passed; no tests or builds were run.

**S3 engine-owned cost-provider encapsulation increment (2026-10-20):** `SemanticOtherChanceStateV111` now copies its named RNG bundle and typed decision transcript on construction, replacement, projection, and fork. Cost resolution mutates a detached working bundle and publishes the updated bundle with its decision, preserving the call cursor and flattened RunState snapshot fields. Older full-bundle snapshots remain readable and compact on their next resolution. The broader named RNG-bundle and cross-provider envelope migrations remain open. `git diff --check` passed; no tests or builds were run.

**S3 RNG bundle and stream ownership increment (2026-10-20):** `RngBundle` and `RngStreamState` now copy their owned arrays on construction and replacement and return defensive copies from `Streams` and `StateBytes`. Engine cursor advancement and stream substitution publish through `ReplaceStreamAt`, which copies the collection before replacing one entry. Constructor, deconstruction, `with`, and JSON property shapes remain intact. The stream replacement census found and migrated all direct Core and fixture writes; cross-provider envelope migration remains open. `git diff --check` passed; no tests or builds were run.

**S3 player mechanics array ownership increment (2026-10-20):** `PlayerState` now owns its deck, relic, potion-slot, and optional character-resource values; `CharacterResourceState` owns its orb array. Construction and replacement copy the arrays, and property projections return defensive copies while preserving positional constructor/deconstruction and JSON member shapes. The source and fixture scan found no direct in-place player-array writes; fork-local changes must publish a replacement value. `git diff --check` passed; no tests or builds were run.

**S3 run-configuration array ownership increment (2026-10-20):** `RunConfiguration`, `SimulationProfile`, and `V111CharacterDefinition` now copy their act, unlock/reveal/history, starting-deck, and starting-relic arrays on construction and replacement and return defensive copies. Constructor/deconstruction signatures and JSON members remain stable. The only direct Core/test mutations were in the fork fixture, which now publishes changes through record replacement. `git diff --check` passed; no tests or builds were run.

**S3 map graph array ownership increment (2026-10-20):** `MapState` now copies its node collection and entry-node list, while `MapNodeState` copies outgoing edges. Their projections are defensive, their positional constructor/deconstruction and JSON shapes remain intact, and legal-node resolution reads the owned graph directly. The Core/test write scan found no direct indexed mutation through these projections. `git diff --check` passed; no tests or builds were run.

**S3 run-world mechanics history ownership increment (2026-10-20):** `RunWorldMechanicsStateV111` now copies encounter, event, and completed-room history arrays on construction, replacement, and projection. Its positional constructor/deconstruction shape remains intact, and the flattened `RunWorldState` fields continue to delegate through the owner. The Core/test write scan found no direct indexed mutations. `git diff --check` passed; no tests or builds were run.

**S3 shop-offer array ownership increment (2026-10-20):** `ShopState` now copies card and additional potion/relic offer arrays on construction and replacement and returns defensive copies. Its positional constructor/deconstruction, flattened offer members, and JSON shape remain stable; existing purchase code already publishes updated offers through record replacement. The Core/test write scan found no direct indexed mutations. `git diff --check` passed; no tests or builds were run.

**S3 reward-offer array ownership increment (2026-10-20):** `RewardState` now copies its option arrays, nested bundles, group-resolution and upgrade flags, enchantments, generator metadata, and returned stolen-card records on construction, replacement, projection, and fork. Reward group transitions continue to mutate working flag copies and commit through record replacement. Positional constructor/deconstruction and JSON member names, including null-omitted members, remain intact. The Core/test write scan found no direct indexed mutations. `git diff --check` passed; no tests or builds were run.

**S4 nested BeforeHandDraw random pile-card continuation increment (2026-10-20):** Resolving a supported automatic `BeforeHandDraw` action may now suspend on Jeweled Mask's random power draw-pile choice. It validates and exposes the existing semantic random pile-card event without relabeling it as another action choice; the automatic hand-draw owner keeps and replays both outcomes in order through its existing transcript. Other nested hook choices and Native trigger-frequency/mechanics parity remain open. `git diff --check` passed; no tests or builds were run.

**S6 registered merchant-purchase index continuation increment (2026-10-20):** `ReferenceNatureMerchantSessionV111` now resolves every remaining `RunActionShopPurchaseSiteIds` index request through the shared typed-index law. One support validator checks the registered index request and its distinct ordered labels both when validating a pending session and when constructing its law. The event fingerprint includes the provider-independent mechanics boundary, legal purchase action, exact request, ordered candidate labels, and prior decision prefix. Native canonical streams retain exact bounded-transform masses; noncanonical streams remain `UNVERIFIED`. Unregistered purchase hooks still fail closed, and source pool, trigger, stream, and cursor parity remain open. `git diff --check` passed; no tests or builds were run.

**S3 combat-zone writer audit increment (2026-10-20):** A Core C# assignment census found no current gameplay replacement that bypasses `SemanticDrawPileAdapterV111`; the transform path builds all-zone replacements and commits them through that adapter. The mechanics comparison now normalizes draw-pile order through `Mechanics.CardZones`. Existing flattened `init` accessors remain because compatibility and fixture callers still use them; subsequent setter and envelope increments guard changed draw-pile writes, while defensive zone projections prevent in-place array mutation. Current gameplay writers are audited; cross-provider mechanics equivalence remains open. No tests or builds were run.

**S3 Nature draw-pile setter guard increment (2026-10-20):** The flattened `CombatState.DrawPile` initializer now rejects a changed pile while a semantic deck is attached, directing those writes through `SemanticDrawPileAdapterV111`; same-sequence compatibility assignments remain valid. This closes the replacement-setter bypass without changing the snapshot/API shape. The later combat-zone ownership increment below also closes in-place mutation through returned zone arrays. Cross-provider mechanics equivalence remains open. No tests or builds were run.

**S3 combat-zone array encapsulation increment (2026-10-20):** `CombatCardZonesV111` now copies all six zone arrays at construction and replacement, returns defensive copies from its public array projections, and deep-copies the private arrays on `Fork`. Flattened snapshot fields and JSON property names stay unchanged; mutation through a returned array or a caller-owned constructor/setter array can no longer change stored zone membership. Zone changes still publish through the existing provider-aware adapter. No tests or builds were run.

**S3 mechanics-envelope draw-pile guard increment (2026-10-20):** The `CombatState.Mechanics` and public `Simulation` initializers reject changed draw-pile contents whenever an old or new semantic deck is attached; flattened zone projections route through the same checks. `SemanticDrawPileAdapterV111` publishes provider constraints and mechanics zones through `CommitSemanticDrawPileTransition`, which validates candidate correspondence before its paired update. Direct envelope replacement can no longer bypass the draw-pile guard. Cross-provider mechanics equivalence remains open. No tests or builds were run.

**S4 Entropy turn-start transform continuation increment (2026-10-20):** Semantic end-turn now admits Entropy's exact `PlayerTurnStarted` hand-selection subscriber when every offered card has a supported transform pool. The existing card-selection owner records the chosen physical cards, and the existing card-transform owner records each replacement in sequence; restore replays both through the saved event dispatch before continuing the turn. Other trigger prompts and Native trigger-frequency/mechanics parity remain open. `git diff --check` passed; no tests or builds were run.

**S4 Toasty Mittens turn-start choice continuation increment (2026-10-20):** Semantic card selection now admits the exact Toasty Mittens opening and turn-two-or-later subscriber frames. It validates the one-card hand-to-exhaust selection, saved relic trigger identity, cursor, and queued Strength effect before resuming dispatch; the action itself consumes no RNG. Other turn-start prompts and Native trigger-frequency/mechanics parity remain open. `git diff --check` passed; no tests or builds were run.

**S4 Choices Paradox opening generated-offer continuation increment (2026-10-20):** Its exact turn-one `OpeningPlayerTurnStarted` subscriber now resumes through the same full-pool semantic shuffle and ChoicePool card-selection owners. The choice uses the character-card pool and card-generation stream, creates five free, retained physical offers, and keeps the `BoneTeaAfterTurnStart` cursor until the required pick is completed. Admission validates the relic trigger, generated operation, source pool, and offer attributes. Source-law calibration, native cursor parity, and unrelated opening choices remain open. `git diff --check` passed; no tests or builds were run.

**S4 Toolbox opening generated-offer continuation increment (2026-10-20):** The exact turn-one `BeforeHandDraw` Toolbox subscriber now resumes through the existing full-pool semantic shuffle, then the existing ChoicePool card-selection event, while retaining the opening `DrawHand` cursor. The guard shares the source pool builder, checks Toolbox's ordered trigger and generation flags, and validates the three offered temporary card instances before exposing the pick. The generated offer remains under the existing source-provenance limits; unrelated opening choices remain open. `git diff --check` passed; no tests or builds were run.

**S4 common automatic-pipeline choice classifier increment (2026-10-20):** Controlled enemy actions, random targets, source action prompts, and typed semantic choices now share a boundary-presence predicate. End-turn and card-selection validation use it where either choice family may suspend; source-specific cursor and payload validation remains with each choice owner. No tests or builds were run.

**S4 Tools of the Trade turn-start selection increment (2026-10-20):** The semantic card-selection path now recognizes the exact Tools of the Trade `PlayerTurnStarted` subscriber frame before post-dispatch policy runs. Selection resolution resumes the subscriber list, then the saved pipeline policy, so any following turn-start prompt remains in order. Native trigger-frequency and mechanics parity remain open. No tests or builds were run.

**S4 automatic Knowledge Demon action-choice continuation increment (2026-10-20):** Curse of Knowledge now has a typed event over its saved enemy, turn, curse index, ordered card offer, and source legal-action payloads. Semantic end-turn records the selected action in nested chronology and full restore replays it before resuming the same enemy-action pipeline; the semantic pilot exposes and records the action. This source action consumes no RNG, so it has no Chance Law; Native trigger-frequency and mechanics parity remain open. `git diff --check` passed; no tests or builds were run.

**S4 PlayerTurnStarted selection continuation increment (2026-10-20):** End-turn now recognizes the source `DiscardAfterPlayerTurnStart` selection only at the cursor immediately after the `PlayerTurnStarted` dispatch, with its live hand, discard destination and exact forced-selection count validated. It uses the existing typed card-selection event, pilot action route, end-turn chronology and full restore replay. This player action consumes no RNG; source trigger-frequency and Native mechanics parity remain open. `git diff --check` passed; no tests or builds were run.

**S4 automatic-pipeline model-choice continuation increment (2026-10-20):** Autoplay may now suspend at source-validated generated-card or combat-potion model choices when the saved continuation identifies an autoplayed card at a supported Mayhem or post-draw cursor. The semantic end-turn owner records both typed outcomes in the same boundary chronology as nested draws, costs, insertions and targets, then replays them on restore; the pilot routes resolution through the existing generation choice owners. Other automatic-pipeline choice families and cross-provider mechanical equivalence remain open. `git diff --check` passed; no tests or builds were run.

**S4 automatic-pipeline card-transform continuation increment (2026-10-20):** Source-validated combat-card transforms reached from an autoplayed card now use the shared Mayhem/post-draw autoplay cursor predicate and the chronological semantic end-turn transcript. A multi-card transform records each current source instance and replacement pool separately; restore validates and replays each outcome before resuming the original selection. Other automatic-pipeline choice families and cross-provider mechanical equivalence remain open. `git diff --check` passed; no tests or builds were run.

**S4 automatic-pipeline card-shortlist continuation increment (2026-10-20):** Source-validated random auto-play and replay-card shortlists reached from an autoplayed card now use the shared Mayhem/post-draw autoplay cursor predicate and semantic end-turn chronology. Each without-replacement pick retains the current physical-card support and prior selected prefix; full restore validates the ordered outcome before resuming the original shortlist or autoplay operation. Other automatic-pipeline choice families and cross-provider mechanical equivalence remain open. `git diff --check` passed; no tests or builds were run.

**S4 automatic-pipeline pool-shuffle continuation increment (2026-10-20):** Supported source-pool shuffles reached from autoplay now record each bounded swap in semantic end-turn chronology and validate the saved permutation on full restore before resuming the ordinary shuffle operation. The automatic path excludes shortlist shuffles that hand off to a separate player card selection. Other automatic-pipeline choice families and cross-provider mechanical equivalence remain open. `git diff --check` passed; no tests or builds were run.

**S4 draw-insertion event construction increment (2026-10-20):** `SemanticDrawInsertionChoiceV111` now constructs pending insertion requests and event envelopes for the semantic draw and pilot facades. Each caller still supplies its own owner prefix, ordinal, and pile-identity policy, while shared boundary validation and support construction live with the insertion choice owner. The permitted RNG inventory and `git diff --check` passed; no tests or builds were run.

**S3 mixed-transcript payload validation increment (2026-10-20):** `RandomBoundaryTranscriptEntryV111.Validate` now deserializes the operation-specific payload and delegates request, result, SiteId, and Native cursor validation to its typed decision. Index entries also verify the recorded selected value against the chosen index. Journal serialization, both scripted providers (including resumable run-action sessions), ordered comparison, and Native-policy audit share this payload codec, removing their independent serializer settings and repeated per-family validation. The permitted RNG inventory and `git diff --check` passed; no tests or builds were run.

**S5 session capability closure increment (2026-10-20):** The Nature-choice SiteId union now includes every site listed by session coverage, including Native event-entry/relic/Shovel, Glory Reflections, Punch Off, and Ancient-offer continuations. `StochasticSessionCoverageV111.Validate` rejects a coverage record that advertises any session SiteId absent from the negotiable Nature-choice set. The permitted RNG inventory and `git diff --check` passed; no tests or builds were run.

**S4 shared automatic-pipeline choice boundary increment (2026-10-20):** `PrototypeGameEngine.HasPendingControlledAutomaticPipelineChoice` now classifies pending enemy-action and nested random-target choices for semantic end-turn entry/resume and controlled-playable staging. Both facades use the same boundary check before starting next-turn intent resolution; a pending target can no longer be skipped by the controlled-playable stage transition. The permitted RNG inventory and `git diff --check` passed; no tests or builds were run.

**S2 typed stochastic request registration increment (2026-10-20):** All six typed request envelopes now reject SiteIds absent from their corresponding family registry before provider resolution. Unit-float and Gaussian-integer use their existing Native site sets; ranged-float, ranged-double, and Gaussian-double have explicit empty site sets and remain unsupported until source sites are audited. This closes the unregistered-site path for scripted and controlled adapters as well as Native adapters; the Native control guard continues to enforce exact SiteId/stream/operation triples. The permitted RNG inventory and `git diff --check` passed; no tests or builds were run.

**S3 typed-decision cursor provenance increment (2026-10-20):** One shared cursor-span validator now requires complete, codec-valid before/after snapshots on Native decisions and rejects Native cursor claims from other provider modes across all six typed families. The Native-policy mixed transcript delegates cursor validation to those typed decisions, retaining only its profile-specific Native-mode check. The permitted RNG inventory and `git diff --check` passed; no tests or builds were run.

**S2 concrete legacy-provider rejection (2026-10-20):** `LegacyPrototypeRandomIndexProviderV111` now rejects requests while Native control is active unless they match the exact engine-owned prototype reward allowlist. This closes a bypass where the provider-active scope skipped the controlled guard and the coarse `prototype_rng.reward` primitive could accept any reward-stream SiteId. Pael's Tooth's existing saved-card return remains explicitly allowlisted with its legacy provider provenance; other prototype requests fail closed. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S2 combat-end Native boundary coverage (2026-10-20):** Static tracing found that an elite win can run War Hammer's Native Fisher–Yates upgrade shuffle on `run.niche`, and every third ordinary win can request Fishing Rod's Native card index on the same stream. The control allowlist now admits only those exact SiteId/operation pairs; no other `run.niche` site is enabled. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S2 Native combat-action boundary coverage (2026-10-20):** Native in-place Tough Egg hatching requests hatchling HP from `run.niche` with `combat.tough_egg.hatchling_hp` and `choose_index`; Native death summons use `combat.initial_unique_hp` on the same stream through `AppendSummonedEnemy`. The control allowlist admits only those exact request triples in addition to the previously audited combat-end sites. The wider Native Hive route and pre-hatched spawn branch remain outside the Tough Egg continuation. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S2 Native site-to-stream guard increment (2026-10-20):** The controlled Native boundary now validates each request's SiteId, stream, and operation together. Combat streams are limited to their source-mapped intent, shuffle/insertion, cost, target, card selection/generation, and potion sites. A guarded winning action also admits the registered `player.rewards` reward-generation sites and the elite relic bag's `run.treasure_room_relics` shuffle. Registered sites paired with a different Native stream fail closed. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S2 unified Native fallback increment (2026-10-20):** `NativeRandomBoundaryProviderV111` composes the six existing Native typed adapters behind `IRandomBoundaryProviderV111`, and `StepNativePolicyBaseline` installs it for every typed boundary. Existing explicit providers retain precedence; otherwise all six request kinds resolve through the engine-owned fallback, where the typed authorization rejects unregistered sites or streams. Ranged-float, ranged-double, and Gaussian-double remain unsupported under the control guard until their gameplay sites are audited and registered. This makes provider routing explicit but does not establish full reachable-site coverage or common mechanics continuation. No tests or builds were run.

**S2 native control-boundary increment (2026-10-20):** Each Native typed adapter now opens a scoped authorization around the exact request site, stream, and operation. During the guarded native-policy control action, the RNG primitive rejects draws outside that active boundary; the request must also match a finite native site/operation registry and one of the control arm's owned streams. Ranged-float, ranged-double, and Gaussian-double fallbacks currently have no registered native gameplay sites and fail closed under the control guard until source sites are audited. This narrows unknown-fallback behavior but does not establish full reachable-site coverage or common mechanics continuation. No tests or builds were run.

**S1 deferred-draw identity binding increment (2026-10-20):** `ReferenceNatureV111.ResolvePending` now verifies that the sampled law outcome's physical card-instance identity matches the same indexed identity in the pending mechanics draw before calling the draw resolver. A stale or misaligned law therefore fails closed instead of committing a different card than the one sampled. Independent constrained-deck and continuation acceptance remains open; no tests or builds were run.

**S1 constrained-deck enumeration invariant increment (2026-10-20):** `SemanticDeckEngineV111.EnumeratePossibleOrders` now checks its generated count against the exact factorial completion count, rejects duplicate physical orders, and validates every result against fixed known positions and each shared pool's remaining identity set before returning the enumeration. This makes the bounded exact-model primitive fail closed on omissions or malformed completions; the independent distribution suite and source-law calibration remain open. No tests or builds were run.

**S0 finite pool-shuffle SiteId trace increment (2026-10-20):** Census v36 scopes an unqualified `SiteIdForOperation` switch to its containing type, resolving the semantic pool-shuffle request's finite mapping instead of leaving it dynamic because another type has a same-named helper. The 99 typed constructors now have 98 registry/conditional resolutions, zero dynamic or missing identifiers, and one intentional Native diagnostic request outside the gameplay matrix. This remains source-expression evidence; the legacy caller-forwarded boundary and row-by-row S0 review remain open. The permitted census and `git diff --check` passed; no tests or builds were run.

**S0 Fisher–Yates wrapper guard trace increment (2026-10-20):** Census v37 follows the low-level `NativeFisherYatesV111.ShuffleInPlace` parameter through its fail-closed `ValidateSiteId` call and resolves the 28 supported IDs from the existing `StochasticSiteRegistryV111.NativeFisherYatesSiteIds` property. All 24 direct Native Fisher–Yates expressions now map to migration rows with no dynamic, unmapped, or missing expressions. This recognizes the shared primitive's declared support; caller reachability and the S0 row-by-row review remain open. The permitted census and `git diff --check` passed; no tests or builds were run.

**S0 deferred-draw source review update (2026-10-20):** The existing `combat.draw_pile.draw_card` review now records that ReferenceNature validates the sampled physical identity against the mechanics event and that bounded full-order enumeration checks exact count, uniqueness, fixed positions, and shared-pool membership. This confirms implementation invariants only; the equal shared-pool probability model remains `UNVERIFIED`, peek/scry gameplay callsites remain absent, and the broader row-by-row review remains open. No tests or builds were run.

**S0 generated-choice and enemy-insertion ownership review (2026-10-20):** The site inventory now manually traces generated-card selection and enemy draw/discard insertion through event support, source-boundary revalidation, ordinary continuation replay, and provider-aware mechanics publication. Source-pool fidelity, legacy probability models, and provider/cursor regression evidence remain open as recorded per row. No tests or builds were run.

**S0 random hand-cost ownership review (2026-10-20):** The inventory now distinguishes the single-card identity choice from per-card bounded cost draws, documents their separate ordered supports and Native streams, and traces `NextCardIndex` through saved continuation replay and draw-pile/provider validation. Legacy law, Native-to-source stream attribution, source-pool fidelity, and cursor evidence remain open. No tests or builds were run.

**S0 legacy index operation-kind trace increment (2026-10-20):** `LegacyRandomChoiceV111` now constructs explicit `choose_index` and `bounded_integer` requests at their respective entry paths; the shared resolver retains ordinal, guard, and provider handling and rejects mismatched support shapes. The permitted census now resolves all explicit request operation-kind expressions; registry/site ownership and broader row-level review remain open. `git diff --check` passed; no tests or builds were run.

**S0/S2 provider-aware index-contract increment (2026-10-20):** `RandomIndexRequestV111.Validate` now owns only common request shape and registered-SiteId checks; each provider validates its own operation contract. The legacy provider enforces registered stream, operation, and support bounds, while the Native provider checks Native site support, defaults unmapped Native index requests to `choose_index`, and recognizes the audited bounded/Fisher–Yates sites plus stream-specific contracts. Native event shuffle plans validate generated swap requests and reject combat-end sites whose typed requests are captured from the source boundary. The permitted RNG census and `git diff --check` passed; no tests or builds were run.

**S2 typed index SiteId validation increment (2026-10-20):** `RandomIndexRequestV111.Validate` now rejects SiteIds outside the union of the existing legacy random-choice and Native index registries before provider resolution. Provider-specific stream and mode checks remain with their existing adapters. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**Implementation scope:** `sts2-emulator`, STS2 v0.111.0, single-player. The first vertical slice is two-site Silent combat. Keep the four-Act Silent gameplay and active native inspector developments on separate tracks. Existing versioned `controlled_*`, `nature_*`, `semantic_*` and ordinary AI protocols remain supported; no historic snapshot/probability reinterpretation.

**S4 Whispering Earring opening autoplay continuation increment (2026-10-20):** The shared automatic-target boundary now admits the exact first-turn late-relic autoplay continuation saved at the following `Complete` cursor. Admission requires the live unmelted relic, the `AutoPlayPlayableCardsFromHand` driver frame, and either an active autoplay card-play series or the exact queued pre-play target operation. Existing semantic owners resume target, draw, cost, insertion, generation, transform, shortlist, and card-selection outcomes before opening completes. Other opening-stage choices and Native stream/cursor fidelity remain open. No tests or builds were run.

**S0 automatic random-target route review update (2026-10-20):** The scoped inventory now traces `combat.autoplay.random_enemy` through the shared target owner for Mayhem, late player-turn hooks, and the exact first-turn Whispering Earring `LateRelicAutoPlay` continuation. Earring validation binds the saved `Complete` cursor to its live relic and autoplay driver, including the queued pre-play card case. Other automatic contexts, Native source target-pool fidelity, stream attribution, and cursor parity remain open. No tests or builds were run.

**S0 diagnostic monster-local request registration increment (2026-10-20):** `StochasticSiteRegistryV111` now lists the diagnostic monster-local bounded-index site explicitly and derives its operation validation from the same registry-owned set. The migration matrix identifies it as diagnostic-only, with no game-source probability claim and no Nature/session capability, and links directly to the request entry and registry code for review. The v37 census now resolves all 101 typed request constructions and maps all 185 registered site IDs to matrix rows; all 16 known SiteId forwarders map. Its S0 locator finds row text or linked-document context for all five review facets on all 185 rows; these are review pointers, not proof of complete source evidence. The source-census registry, local-link, and ambient-entropy checks pass; no tests or builds were run.

**S0 Punch Off monster-local request review update (2026-10-20):** The Construct HP row now links the event callsite, the CombatId-selected Native local-stream entry, and the atomic ReferenceNature fight session. The emulator path uses bound 8, ordinal 0, and an event/CombatId owner per construct; replay verifies both ordered requests and preserves the event cursor. The bounded-transform law is source-verified under the stated uniform Native grid model, while seed/context parity remains open. No tests or builds were run.

**S4 mechanics comparison random-pile-card cursor increment (2026-10-20):** The v16 provider-independent projection now preserves the operation and occurrence ordinal for a pending Jewelled Mask random draw-pile-card choice while omitting candidate identities and its support fingerprint. Stripping stochastic payload can no longer erase this mechanics suspension boundary from cross-provider comparisons. No tests or builds were run.

## Priorities and milestones

| Stage | Priority | Deliverables | Acceptance gate |
| --- | --- | --- | --- |
| **S0 — Contract and inventory** | P0 | Canonical ownership model; document source-vs-model probability status; map all current RNG sites, private-state owners and overlapping code paths | Maintainers can identify for each site: owner, operation, private constraint, public visibility, resolver, continuation, provenance and unsupported handling |
| **S1 — Chance Law + ReferenceNature** | **P0 / next** | Pure query for `enemy_intent` weighted eligible support and unresolved `draw_card` conditional probabilities; typed provenance and conditioning ID; seeded simple `ReferenceNature` selector | Independent exact small-deck enumeration; intent eligibility/weights; distribution normalization; outcome-blindness; sample-vs-declared-law checks; deterministic fork/replay; zero original-game requirements |
| **S2 — Complete engine-owned RNG control arm** | **P0 / #143** | Auto-resolving emulator policy for the same two semantic sites; all other reachable stochastic sites delegated to explicit engine-owned provider; seedable/forkable state and full traces | Real supported Silent fights work under both ReferenceNature/control and learned/scripted Nature with the **same mechanical continuation**; no unknown RNG fallback; terminal replay; 1 vs 15 workers |
| **S3 — Unified provider-state envelope** | P1 | Mechanical/hidden stochastic state ownership contract; one authoritative deck-zone membership and private-order representation; stable shuffle epoch/commit/reveal/consume semantics; atomic snapshot/fork | Forced top/bottom insertion, middle insert, peek/scry, recycled draws, multiple shared hidden pools; no duplicate shuffle sampling, divergent zone copies, or accidental information leak |
| **S4 — Common mechanics continuation** | P1 | Migrate specialized controlled end-turn/draw/pilot staging to the ordinary engine's resumable chance suspension; preserve old adapters as facades | Native, ReferenceNature, scripted and learned Nature produce identical **mechanical** results for identical semantic outcomes, including nested hooks and terminal rewards; legacy tests remain green |
| **S5 — Shared observations and external contracts** | P1 | Versioned public player view, Nature-only chance view and privileged inspector view; stable semantic IDs and replay fingerprints; bridge capability negotiation | No private RNG/order leak to player; consistent actions/observations across emulator modes; native spectator declares gaps; schema compatibility and malformed/stale choice rejection |
| **S6 — Broaden stochastic coverage** | P2 | Select-without-replacement offers, rewards, shops, formations, generated cards, targeting, draw costs and later map operations | Every supported stochastic site declares its sampling law, provider ownership, source timing and supported hidden constraints; expanded source-independent tests |
| **S7 — Source-law calibration (N5)** | Parallel, independent | Audited source conditional laws, `p` provenance, original-game importance-correction eligibility | Explicit independent mathematical/source evidence for each law; no `UNVERIFIED` `p` used as original-game-unbiased likelihood |
| **F — Native Fidelity Inspector** | **Parallel / #122** | Source capture, hook/event/state diff, named RNG stream/cursor audits, exact source-based replay | Independently validated installed-game traces; no requirement for S1–S2 structural acceptance |

**S4 semantic end-turn boundary handoff increment (2026-10-19):** Enemy-action and random-target resolutions now share one semantic end-turn continuation handoff. It classifies terminal outcomes, subsequent automatic-pipeline chance suspensions, and the `AdvanceTurn` checkpoint in one place, carrying both typed transcripts and their chronological boundary transcript into intent staging. This also preserves earlier target decisions when a later enemy-action choice reaches `AdvanceTurn`. Choice resolution and transcript replay remain at their adapters; broader resume consolidation and cross-provider mechanical equivalence remain open. No tests or builds were run.

**S4 semantic end-turn hand-draw continuation increment (2026-10-19):** `SemanticEndTurnEngineV111.ContinueHandDraw` now owns publication of newly begun, resumed and replayed `SemanticCombatDrawV111` frames, keeping the current run and saved draw facade together and applying the end-turn handoff when the frame reaches its boundary. Index selection, hook actions, Stratagem actions and transcript replay remain with their existing adapters. `git diff --check` and the authorized RNG inventory check passed; no tests or builds were run.

**S3 semantic deck choice commit unification increment (2026-10-19):** `SemanticDeckEventV111.Validate` now owns the common draw/reveal event-envelope rules, and `SemanticDeckEngineV111.Inspect` validates constructed events before law, observation or continuation consumers receive them. `SemanticDrawPileAdapterV111.ResolveNatureDeckEvent` revalidates and commits both deferred draws and peek/scry reveals with their physical pile state. The choice owners retain operation-specific selection and continuation checks. `git diff --check` and the authorized RNG inventory check passed; no tests or builds were run.

**S4 semantic end-turn completion publication increment (2026-10-19):** `SemanticEndTurnEngineV111.CompleteAtBoundary` now publishes the final run and completion flag for terminal automatic choices and completed hand draws. Terminal choices clear active intent/draw frames; completed draw frames retain their replay projection. The existing shared phase and player-boundary predicates continue to validate the respective results. `git diff --check` and the authorized RNG inventory check passed; no tests or builds were run.

**S4 semantic draw-progress publication increment (2026-10-19):** `SemanticCombatDrawEngineV111.PublishDrawProgress` now owns the run, requested-count and remaining-count updates across resolved card, insertion, random pile-card, cost, nested draw, hook and Stratagem outcomes. Resolution adapters retain their typed transcripts and boundary validation. `git diff --check` and the authorized RNG inventory check passed; no tests or builds were run.

**S4 semantic pilot run-continuation projection increment (2026-10-19):** `SemanticPilotCombatEngineV111.PublishRunContinuation` now publishes player-action and resolved end-turn outcomes through one transition for run state, the pending frame, reward audit, optional pilot history and `OtherChance` state. Draw-hook, target, Nature-choice and engine-owned draw-cost resumes reuse it while retaining their own transcript and support checks. `git diff --check` and the authorized RNG inventory check passed; no tests or builds were run.

**S4 semantic pilot choice-publication increment (2026-10-19):** Semantic card selection, target, generated card, potion generation, transform, cost, shortlist, pool shuffle, draw insertion, and effect draw outcomes now use `PublishRunContinuation` for run, reward audit, and history publication. Unrelated outcomes preserve the saved pending end-turn frame; end-turn outcomes publish a replacement or clear a completed frame. Native-policy baseline publication uses the same owner while keeping its provider audit at the adapter. Each adapter retains its own support, replay, and boundary checks. `git diff --check` and the authorized RNG inventory check passed; no tests or builds were run.

**S4 semantic pilot opening-continuation publication increment (2026-10-19):** Opening draw results, initial-intent outcomes and deferred deck-reveal choices now also use `PublishRunContinuation`. The ordinary automatic-pipeline cursor remains the owner of pending intent mechanics, and the reveal adapter retains its typed decision. `git diff --check` and the authorized RNG inventory check passed; no tests or builds were run.

**S4 common combat player-boundary increment (2026-10-19):** `PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary` now owns the run-level clean combat decision predicate used by semantic player-action, pilot, and end-turn facades, plus player-action result validation, end-turn source replay, and initial-intent completion. End-turn completion adds its separate idle-Nature-deck requirement after the shared predicate. This also rejects pending Knowledge Demon choices on paths that previously checked only the player turn and automatic-pipeline cursor. No tests or builds were run.

**S4 draw-pile correspondence increment (2026-10-19):** saved after-shuffle continuations, automatic hand-draw entry and end-turn source validation now delegate Nature deck-to-zone checks to `SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary`. Native policy and top-autoplay boundaries, plus resumed card selection, use `ValidateSynchronized`; candidate card-selection decks use the adapter's shared correspondence validator before attachment. Caller-specific continuation errors and additional idle-boundary rules remain with their owners. Only `git diff --check` and the authorized RNG inventory check were run; no tests or builds were run.

**S4 combat completion phase increment (2026-10-19):** `PrototypeGameEngine.IsAtCombatRewardOrTerminalBoundary` now owns the shared reward-or-terminal phase classification used when semantic draw, cost, generated-card, transform, target, player-action, pilot, and end-turn continuations finish. Choice-specific suspended boundaries and reward-specific bookkeeping remain in their existing adapters. No tests or builds were run.

**S4 combat action result boundary increment (2026-10-20):** `PrototypeGameEngine.IsAtCombatActionResultBoundary` now classifies a continued combat, initialized reward, or closed defeat. Semantic pilot card actions and the Native policy baseline use the shared predicate; supported Distilled Chaos action replay also accepts a closed defeat. Terminal outcomes must have no live combat or reward, so their provider-owned combat continuations cannot remain attached. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 nested combat outcome publication increment (2026-10-20):** Semantic draw, target, generated-card, and draw-cost resumes now use the same validated combat-action result boundary. Draw insertion checks its inserted card only while combat remains active, applies the exact engine-owned reward fallback to the resumed suffix, and clears a saved end-turn frame when the mechanics reaches reward or closed defeat. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 closed combat-result boundary increment (2026-10-20):** `IsAtCombatRewardOrTerminalBoundary` now accepts only an initialized reward or a closed defeat with no live combat or reward. Existing transform and end-turn continuations therefore reject malformed completed outcomes through the shared predicate. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.


**S2 explicit-provider source-audit increment (2026-10-18):** The direct-call census now reports typed and target-typed System.Random construction, shared-instance use, and RandomNumberGenerator calls in Core; --check-no-ambient-entropy fails when those APIs appear. The current source scan finds zero such expressions. The scan cannot establish the absence of indirect or aliased entropy sources, which still require call-site tracing; no tests were added or run.

**S0 typed-request census increment (2026-10-18):** The direct-call inventory now lists explicit constructions of all six typed stochastic request families and resolves positional or named SiteId arguments against the existing registry and migration matrix where possible. Dynamic and aliased request arguments remain review items; no site definitions were duplicated and no tests were added or run.

**S0 controlled-playable registry increment (2026-10-18):** The controlled-playable supported-site capability now comes from StochasticSiteRegistryV111 instead of a second JSONL list. Its fixture-only opening-shuffle request has an explicit migration row that distinguishes the synthetic fixture from game-source Native sampling.

**S0 request-census argument-order increment (2026-10-19):** Typed request inventory now searches every named constructor argument for `SiteId` before falling back to the first positional argument. This preserves static registry/matrix resolution when callers name constructor fields out of positional order; dynamic request forwarding remains for manual trace.

**S0 typed SiteId forwarding classification (2026-10-19):** Census v15 distinguishes single static IDs, finite alternatives, forwarded/local identifiers and forwarded members. Its 100 typed-request constructions resolve 47 single IDs and 7 finite alternatives; the remaining 46 are 42 identifier expressions and 4 member expressions, with no computed SiteId expression. This narrows the manual trace to caller/member ownership without treating a forwarded value as resolved; all 46 remain review items.

**S0 legacy provider allowlist trace (2026-10-19):** `StochasticSiteRegistryV111.NatureChoiceSiteCandidates` now supplies both the unique Nature-choice view and the non-query portion of the legacy random-choice provider guard. The census follows that shared registry list and the existing Chance Law query list; four internal legacy Gaussian `.component` requests map to their parent Gaussian migration rows. It now resolves 100/101 typed request constructions to registry sites, with the remaining request intentionally diagnostic-only and outside the gameplay matrix. Registry coverage remains 184/184 with no unresolved references. The allowed inventory command and `git diff --check` passed; no tests or builds were run.

**S0 fail-closed SiteId guard tracing (2026-10-19):** Census v16 resolves an identifier when its containing method explicitly rejects unsupported IDs and supplies finite alternatives. Seven typed-request constructions now resolve through such checks, including Native acquisition models, merchant index laws, enemy theft, Trash Heap, and the controlled intent continuation's next-turn/committed-intent sites. The source-validated native shuffle replay now requests its fixed registered shuffle SiteId directly. The census reports 62/100 request constructors mapped or conditionally mapped; 38 forwarded identifier/member expressions still need caller tracing. Guard matches are review evidence, not proof that the complete source operation is modeled.

**S0 method-local SiteId constant trace (2026-10-19):** Census v17 resolves constant declarations and aliases in the containing C# member, avoiding same-name local constants in unrelated methods. This maps the unowned-relic query's site alias and the Native encounter card-model site without following caller-controlled values. The inventory maps or conditionally maps 63/100 typed request constructions; 37 identifier/member forwards remain for source trace.

**S0 finite local SiteId assignment trace (2026-10-19):** Census v18 also follows a single unreassigned local `SiteId` initializer when its literal, ternary, or switch-expression results all resolve to registered or migration-ledger IDs. It maps three finite local assignments, including the target-choice, Glory Reflections, and semantic pool-shuffle requests. The census now maps or conditionally maps 66/100 constructors; 34 identifier/member forwards remain. This static trace does not infer hidden support, operation constraints, or source parity.

**S0 source-owned index dictionary trace (2026-10-19):** Census v19 follows a fail-closed `TryGetValue(siteId, ...)` to the keys of its source-owned dictionary initializer. This maps both random-index constructors in the legacy caller-bound helper from the existing 32-site allowlist, with no second site list; 68/100 constructors are now mapped or conditional and 32 forwarding rows remain.

**S0 Native bounded-index helper guard (2026-10-19):** The private Native bounded-index request constructor now rejects any SiteId absent from its existing source-owned Native-law dictionary. Census v20 resolves the constructor to that dictionary's ten keys; 69/100 constructors are mapped or conditional, with 31 unresolved identifier/member forwards. Source-law provenance remains per row and unchanged.

**S0 ReferenceNature query SiteId guards (2026-10-19):** Ancient offer replay and shuffle law builders, Native Event entry and event-gold Chance Law builders, and the Neow offer decision builder now reject unsupported request SiteIds explicitly; the Neow and shuffle paths reuse their existing session site lists. Census v21 follows guarded SiteId membership through a source-owned array property and maps eight request constructions; 77/100 are now mapped or conditional, while generic wrapper and caller-forwarded identifiers remain for manual trace. This records request construction scope, not source completeness or probability provenance.

**S0 Native event shuffle plan guard (2026-10-19):** A saved shuffle plan now checks its `SiteId` against the eleven supported plan sites before constructing a bounded request. The session capability list composes those plan sites with Repy's separate potion offer site. Census v22 follows this member guard to the eleven plan sites; 78/100 request constructions are mapped or conditional, while 22 forwarded/member IDs remain unresolved. This protects the request boundary and does not establish source callsite completeness or cursor parity.

**S0 continuation request-list trace (2026-10-19):** The Endless Conveyor session validates a forwarded law `SiteId` against its three-site continuation list before constructing a replay request. The legacy Punch Off reward query's existing fail-closed check now qualifies its existing two-site query list for the source locator. Census v23 follows both guards to their source-owned arrays; 80/100 request constructions are mapped or conditional, with 20 forwarded/member IDs still requiring caller trace. This checks request compatibility without establishing source coverage or law provenance.

**S0 controlled insertion request ownership increment (2026-10-19):** `StochasticSiteRegistryV111` now owns the three allowed controlled draw-insertion request sites and the after-shuffle relic ID constant; mechanics validation, semantic insertion laws, and controlled snapshot projection reuse those identifiers. The projection rejects a forwarded `RequestSiteId` outside that list. Census v24 follows the guard to the registry array and maps 81/100 constructors, leaving 19 generic or caller-forwarded expressions for manual trace.

**S0 Native map-adapter SiteId guards (2026-10-19):** Overgrowth's private RNG adapter now accepts only its path-start index and two Gaussian count sites; the later-act adapter accepts only its path-start/Glory rest-count indices and Hive/unknown-count Gaussians. Census v25 maps their four forwarded request constructors through fail-closed finite guards; 85/100 are now mapped or conditional, with 15 generic or caller-forwarded requests remaining. The direct Native Fisher–Yates wrapper remains generic and is still traced through its caller sites separately.

**S0 map Fisher–Yates caller trace (2026-10-19):** Both map adapters now guard the path-delta and room-assignment shuffle sites by their existing constants. Census v26 follows fail-closed caller guards for the Overgrowth and later-act Native Fisher–Yates expressions instead of leaving those two helper calls dynamic; the underlying primitive remains generic. The 100 typed request count is unchanged at 85 mapped or conditional, with 15 identifier/member forwards still open.

**S0 remaining typed-request boundary pass (2026-10-19):** Private combat intent, random-target, generated-card, pile-position, and card-pool shuffle helpers now reject SiteIds outside finite `StochasticSiteRegistryV111` lists. Semantic draw-insertion request construction shares the controlled insertion list, and Native Neow's local index builder accepts only its four source-owned choice sites. Census v26 now resolves 91/100 typed constructors to registered or conditional sites. Manual trace classifies the remaining eight forwarded constructors as generic `NativeEventContextRngV111`, `NativeFisherYatesV111`, `NativeRandomChoiceV111`, and legacy provider adapters; their semantic SiteId is caller-owned. The ninth residual is the diagnostic-only monster-local request site, deliberately outside the gameplay migration matrix. These classifications do not finish the row-level S0 acceptance audit. `git diff --check` and the existing RNG inventory audit passed; no tests or builds were run.

**S0 site-local contract locator pass (2026-10-19):** Census v16 counts only row text or explicitly linked heading sections; generic document-wide keyword hits and broken local anchors are excluded. The current census finds all five facets for 184/184 migration rows, with zero missing matches in every facet and no broken local contract links. Existing family contracts now have row-level links for event, map, reward, merchant, run-action, combat draw, and relic-operation sites. These matches are review pointers rather than proof of contract completeness or source accuracy; manual source review remains authoritative and S0 inventory acceptance remains open.

**S0 semantic choice-path review increment (2026-10-19):** Scoped implementation reviews now trace random hand-cost selection/costs, selected-card transforms, generated draw-pile insertion and card shortlists from their saved support through the ordinary mechanics resume. The inventory records their live-support checks, draw-pile invariants, provider/zone commit paths and remaining Native/source-law limits. This advances the row-level S0 audit without claiming source parity. `git diff --check` and the authorized RNG inventory check passed; no tests or builds were run.

**Implementation checkpoint (2026-10-17):** Typed, read-only queries and an independent seeded `ReferenceNatureV111` sampler cover the existing filtered intent event, pending unresolved semantic deck draw, semantic random-target boundaries for effect, Sly autoplay, and autoplay sites in both the pilot and semantic end-turn continuations, bounded random-index events inside the controlled automatic enemy-action pipeline, generated-card model selection at its two supported semantic sites, random hand/drawn-card costs and random hand-card target selection exposed by semantic continuations, the random Power instance selection at the Jewelled Mask site, selected-card transform replacements, generated-potion model selection, each bounded swap in the two supported semantic pool shuffles, bounded generated-card draw-pile insertion positions, and sequential picks from the remaining support at three card-shortlist sites. Target, enemy-action index, generated-card, bounded-cost, random-pile-card, hand-cost-selection, transform, potion-generation, pool-shuffle, draw-insertion, and card-shortlist laws use equal integer weights over their saved support; pool-shuffle and shortlist queries condition on prior choices, and all added source probabilities remain `UNVERIFIED`. The semantic end-turn route now suspends, resolves, saves and replays automatic-pipeline target and enemy-action decisions in chronological boundary order. Pending peek/scry deck reveals now have a typed law, stable-identity semantic resolver, replay decision, and ReferenceNature session path; the game callsites that initiate those reveals remain unintegrated. `ReferenceNatureSessionV111` keeps the sampler cursor and semantic combat continuation together across fork/save/restore; `ResolveNext` applies the sampled outcome through the existing intent, target, enemy-action, generated-card, transform, cost, hand-card, pile-card, potion, pool-shuffle, shortlist, insertion, draw, or deck-reveal resolver. Generic typed random-index laws now accompany pending controlled event-effect, post-reward modifier-curse, and supported run-action requests. Their equal weights describe request indices, retain the saved request and boundary fingerprint, and remain `UNVERIFIED`; at this checkpoint, event-effect/reward sessions still used the controlled external resolver rather than an atomic ReferenceNature session, which the increment below supersedes. A new `ReferenceNatureRunActionSessionV111` samples supported run-start, legacy Event-node entry, legacy Shop-node generation, legacy Treasure-node relic selection, and Shovel continuations, committing each sample and ordinary run-action resolution together across fork/save/restore. Shop-node support covers only the seven `shop.*` random-index sites used by the current non-native shop generator; Treasure-node support covers its single legacy relic-pool index. Native configured Shop entry is handled by ReferenceNatureMerchantSessionV111, and configured Treasure entry by ReferenceNatureTreasureSessionV111; merchant and Treasure laws remain UNVERIFIED. All laws remain `UNVERIFIED`. Focused acceptance independently enumerates all 24 hidden orders of a four-card physical pool across forced top, middle and bottom cards, checks conditional draw probabilities and complete path probabilities, verifies query immutability and intent support invalidation, and checks a 24,000-sample weighted smoke plus fork/save/restore replay. Query-only Chance Law coverage initially included the Orobas, Pael and Tezcatara Ancient offer sites, with stable choice identities, support and conditioning fingerprints; Pael's repeated ordered entries become integer outcome weights. The later S6 query and atomic-session increments added the three Glory Ancient shuffle sites, with each swap law conditioned on its prior-index prefix, and exposed all six offers through forkable, replayable ReferenceNature sessions. The full S1 gate remains open for duplicate-model fixtures, every constrained-deck operation in the exact enumerator, the complete mechanical and observation invariants, and broader stale-event/mode coverage. S0 inventory acceptance is also open because some existing site rows lack explicit constraint, audience, continuation and fail-closed links. The v5 census now reports per-row text/link matches for those fields plus provenance and checks local contract targets; its matches are review pointers, not acceptance evidence. The encounter selection and formation rows now link a shared stochastic contract; the six Ancient offer sites also link their pinned contracts. The locator's updated match count is recorded below.

**Increment since checkpoint:** `ReferenceNatureControlledEventChoiceSessionV111` now samples supported saved event-effect and post-reward modifier-curse requests while keeping legal player reward actions and Stone of All Time push's deck-card choice in the same ordered transcript. The legacy `controlled_event_*` operations remain the external-resolution facade; the new session shares the existing mechanics resolver and sampler-state type. `StochasticSiteRegistryV111` supplies the `hello` site lists and controlled replay allowlists from one source; a separate versioned `stochasticSessionCoverage` response reports support by ReferenceNature continuation schema. The committed ReferenceNature cursor now travels on `RunState`, survives forks and ordinary snapshots, and is inherited by a later semantic, run-action, or event-choice session when the caller omits `nature_seed`. Pending sessions keep the run's previous committed cursor until the full controlled continuation completes, preserving its continuation-state invariants. Ordinary snapshots without a committed Nature cursor retain `sts2-exact-snapshot-v1`; those with one use `sts2-exact-snapshot-v2-reference-nature-state`, unless a Nature-owned relic identity set requires `sts2-exact-snapshot-v3-nature-relic-bag-order`. These capabilities are advertised separately in `hello`. Older completed session snapshots are upgraded during replay. The Python binding exposes cursor continuation and per-schema site coverage. This fixes the former omission of the Fake Merchant relic shuffle from the controlled-event site report without changing the existing player-observation schema.

**S4 selected-index replay increment (2026-10-18):** `ControlledChanceContinuationExecutorV111.ResolveSelectedIndex` now wraps boundary-owned logical index tickets in a one-outcome typed continuation and consumes them at their declared ordinal under an exact request filter. `ResolveTypedSelectedIndex` records a boundary-selected ticket and replays the full saved prefix through the same executor. Semantic draw costs, semantic end-turn automatic enemy-action selection, controlled-playable opening shuffle, draw and random-card costs, pool shuffle, insertion, enemy-action, generated-card, potion-generation, card-transform, target and recycle swap indices, controlled initial-enemy HP, legacy committed-intent choices, and next-turn intent ticket resolution use the shared adapter before their mechanics resume. Their transcript modes and flattened snapshot fields remain unchanged. Other semantic draw boundaries remain adapter-owned; the common resume and cross-provider mechanical-equivalence gates remain open.

**S4 semantic nested-choice resume increment (2026-10-20):** `SemanticNestedChoiceResumeV111` now applies the recorded typed nested choice through one mechanics dispatcher shared by semantic end-turn restore and semantic pilot resolution for effect draws and costs, generated cards and potions, transforms, card shortlists, pool shuffles, hand-cost selection, and draw insertion. Card selection, automatic draw-hook choice, and Knowledge Demon choice replay also use the same dispatcher. End-turn and pilot facades retain their own fingerprints, chronology, and action histories; target, draw-hook pipeline entry, automatic hand-draw segments, and provider-equivalence work still have separate owners. `git diff --check` passed; tests and builds were not run.

**S4 live nested-choice resume increment (2026-10-20):** Semantic end-turn now builds typed decisions for automatic-pipeline `CardDrawn` and Knowledge Demon actions, then applies them through `SemanticNestedChoiceResumeV111` before committing the existing nested chronology. `SemanticDrawHookChoiceEngineV111.Select` and `SemanticKnowledgeDemonChoiceEngineV111.Select` split support/action validation from mechanics execution so live resolution and full restore share one resume dispatcher. Standalone and automatic hand-draw hook progress remain with `SemanticCombatDrawEngineV111`. `git diff --check` passed; tests and builds were not run.

**S4 live card-selection resume increment (2026-10-20):** `SemanticPilotCombatEngineV111.StepCardSelection` now validates the pending typed event and selected action, then applies it through `SemanticNestedChoiceResumeV111`, the same mechanics path used by end-turn replay. Pilot action history and end-turn chronology remain with their existing facade owners. `git diff --check` passed; tests and builds were not run.

**S4 live end-turn nested-choice resume increment (2026-10-20):** Live end-turn selection and the chronological restore replay now share `ResumeNestedSemanticChoice` for nested effect draws and costs, insertion, generated-card and potion choices, transforms, shortlists, pool shuffles, hand-cost selection, card selection, draw-hook actions, and Knowledge Demon choices. The dispatcher applies mechanics, then the helper commits the typed choice and source SiteId through the end-turn chronology owner. Random targets, initial/next-turn intents, and standalone hand-draw choices retain their specialized owners. `git diff --check` passed; tests and builds were not run.

**S4 semantic target resume consolidation (2026-10-18):** `SemanticTargetChoiceV111` now owns stable-identity mapping, the guarded ordinary target resume, and the draw-pile/semantic-deck preservation invariant used by both semantic pilot and semantic end-turn continuations. Each facade still validates its own stale fingerprint and retains its own transcript and boundary progression; broader semantic resume unification and cross-provider mechanical equivalence remain open.

**S4 automatic hand-draw segment resume increment (2026-10-18):** `PrototypeGameEngine.ResumeAutomaticHandDrawCards` now validates the active tagged remaining count, clears that progress discriminant, and re-enters the ordinary `DrawCards` kernel for resumed legacy and semantic automatic segments. Controlled draw-hook, recycle-Stratagem, draw-cost, insertion, semantic hook-choice, and semantic post-cost continuations use this common mechanics path. Adapters retain their boundary-specific suspension profiles, choice histories, and result interpretation; full resume consolidation and cross-provider mechanical equivalence remain open. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 automatic draw-progress interpretation increment (2026-10-18):** `PrototypeAutomaticPipelineStochasticStateV111.TryGetAutomaticHandDrawRemainder` now defines the tagged progress modes that can re-enter a saved automatic draw segment, including their stage and opening-cursor constraints. The semantic-specific progress query builds on the same interpretation. `ResumeAutomaticHandDrawCards` delegates cursor/count interpretation to that existing stochastic owner before clearing progress and invoking `DrawCards`. Provider policies and tagged result interpretation remain at the adapters; broader resume consolidation and cross-provider equivalence remain open.

**S4 automatic draw-cost resume increment (2026-10-18):** Legacy controlled and semantic automatic hand-draw cost continuations now share `ResumeAutomaticHandDrawAfterCost` for advancing the selected cost, retaining a pending cost stage, forming a `CardDrawn` hook boundary, and re-entering the saved draw count. Each adapter supplies its existing suspension policy and continues the tagged result through its own frame. Nested effect-draw costs retain their distinct continuation; broader resume consolidation and cross-provider equivalence remain open.

**S4 automatic draw-cost cursor validation increment (2026-10-23):** `ResumeAutomaticHandDrawAfterCost` now owns validation of the active `DrawPlayerHand` cursor, provider progress kind, saved remaining count, and cost-card identity before applying legacy or semantic automatic cost outcomes. It returns the validated semantic requested count with its tagged result, leaving provider-specific transcript identity, hook policy, and continuation replay with the adapters. `git diff --check` passed; no tests or builds were run.

**S4 semantic hand-draw completion receipt increment (2026-10-23):** The semantic automatic-draw adapter now passes its saved requested count into the shared `CompleteAutomaticHandDrawStage` owner, which commits the receipt with stage cleanup, cursor advance, and post-stage timing. The facade derives a receipt only for source-committed completion without a semantic draw request. Static source review and `git diff --check` only; no tests or builds were run.

**S4 automatic draw-hook checkpoint increment (2026-10-18):** Controlled and semantic automatic hand draws now publish a suspended `CardDrawn` hook choice through `SuspendAutomaticHandDrawAtCardDrawnChoice`. It validates the common pipeline boundary, applies the semantic provider marker when needed, and commits the typed cursor progress with the latest mechanics state. The adapters retain distinct choice transcripts and resume behavior; broader resume consolidation and cross-provider equivalence remain open. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 automatic Stratagem checkpoint increment (2026-10-18):** Legacy controlled and semantic automatic draws now commit their tagged progress through `CommitAutomaticHandDrawStratagemSelection` after the ordinary draw-pile selection prompt has been formed. The owner checks legal selection support and the DrawPlayerHand cursor, then publishes provider-appropriate progress with the current player/combat state. Prompt construction and selection transcripts remain with their adapters; broader resume consolidation and cross-provider equivalence remain open. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 Stratagem selection-validation ownership increment (2026-10-18):** `ExpectedStratagemSelectionCount` is now the shared rule for Stratagem operation construction and eligibility at initial/recycled draws, after-shuffle continuations, automatic checkpoint commits, and semantic choice validation. Controlled recycle snapshots and both semantic Stratagem choice forms require exact minimum/maximum selection bounds and positive current support; boundary owners and validators no longer reimplement the power, draw-pile, and hand-capacity formula. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 maximum hand-capacity ownership increment (2026-10-18):** `PrototypeRuleset.MaxHandSize` now supplies the hand ceiling; `HasHandCapacity` and `RemainingHandCapacity` own the shared capacity checks used by ordinary draws, generated-card placement, random/selected card movement, and Stratagem selection support. The initial draw target remains `HandSize`, distinct from the maximum capacity. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**ReferenceNature cursor increment (2026-10-17):** `ReferenceNatureRunCursorV111` now owns run-scoped sampler initialization, cursor/transcript validation, and cursor commit publication for semantic-combat, run-action, and event-choice sessions. `ReferenceNatureV111.SampleValidated` checks every sampler result against the queried law identity and ordered support, and all semantic and run-scoped session paths use it. Mechanics resolution and boundary transcripts remain with their existing continuation owners; this consolidates provider lifecycle rules without changing snapshot or wire schemas.

**S4 shared replay-executor increment (2026-10-17):** `ControlledChanceContinuationExecutorV111` now owns replay-scope lifetime, exact saved-prefix consumption, and capture of the next unresolved typed request across run-action, event-effect, event-reward, event-choice entry, and event-reward advance mechanics. Existing boundary adapters still choose checkpoints, enforce their site allowlists, and persist the versioned continuation frames. Semantic end-turn/draw/pilot resume loops and full cross-provider mechanical equivalence remain open.

**S4 automatic enemy-action replay increment (2026-10-17):** automatic enemy-action replay now uses the same executor for replay-scope lifetime, typed prefix consumption, and suspension capture. Its automatic-pipeline adapter retains the pre-step checkpoint, pipeline index, enemy-action site policy and serialized frame. Semantic end-turn/draw/pilot resume loops and full cross-provider mechanical equivalence remain open.

**S4 enemy-action mechanics-envelope increment (2026-10-17):** `PrototypeControlledEnemyActionContinuationV111.Simulation` now pairs the common `PrototypeMechanicsContinuationStateV111` checkpoint with the shared controlled chance payload. The serialized `Checkpoint`, `StepIndex`, `Decisions`, and `PendingRequest` fields remain projections; the automatic-pipeline validator continues to check the saved stage against its combat boundary. The semantic draw facade and full cross-provider mechanical equivalence remain open.

**S4 enemy-action common-handoff increment (2026-10-22):** `ResumeControlledEnemyAction` now re-enters through `ResumeAutomaticPipelineContinuation` with a cursor-only `ResolveEnemyActions` boundary. The common owner validates cursor/checkpoint correspondence and forwards the typed replay prefix to the ordinary pipeline runner. Request capture and site validation remain with the enemy-action continuation; broader resume consolidation and cross-provider mechanical equivalence remain open. No tests or builds were run.

**S4 automatic pipeline start handoff increment (2026-10-22):** Ordinary end-turn and extra-player-turn starts now publish their existing pipeline index through `SetAutomaticPipelineContinuation` and resume through the common owner. Extra turns retain the `ResetPlayerBlock` entry point and continue to skip `AdvanceTurn`. Other specialized pipeline boundaries and cross-provider mechanical equivalence remain open. No tests or builds were run.

**S4 controlled pre-draw completion handoff increment (2026-10-22):** After a controlled `BeforeHandDraw` choice resumes its saved `DrawPlayerHand` stage, the resulting state now returns through `ResumeAutomaticPipelineContinuation` for clean cursor continuation and terminal reward settlement. The provider marker still prevents replaying the completed hook. Other specialized pipeline boundaries and cross-provider mechanical equivalence remain open. No tests or builds were run.

**S4 opening combat common-handoff increment (2026-10-22):** Initial combat entry now installs a `PlayerSideStart` opening cursor and enters `ResumeAutomaticPipelineContinuation`; opening-stage suspensions use the same cursor writer. This consolidates initial execution with restored opening choices while preserving stage order. Other specialized pipeline boundaries and cross-provider mechanical equivalence remain open. No tests or builds were run.

**S4 opening reward publication increment (2026-10-22):** `ResumeOpeningCombat` now reports a clean completed opening boundary when all enemies are defeated and leaves Reward publication to `ResumeAutomaticPipelineContinuation`. Opening and ordinary resumes therefore use the same terminal reward owner. Other pipeline mechanics and cross-provider mechanical equivalence remain open. No tests or builds were run.

**S4 common cursor validation increment (2026-10-22):** `PrototypeAutomaticPipelineContinuationState.IsReadyForAutomaticPipelineResume` now validates the saved mechanics cursor and provider payload, then identifies unresolved deck, hand-draw, enemy-action, and intent boundaries. The common resumer uses it before clearing a clean handoff. The opening mechanics validator admits the transient `PlayerSideStart` entry cursor; completed opening execution leaves cursor clearing to the common owner. Specialized pending choices retain their existing adapter validation. No tests or builds were run.

**S4 end-turn draw-checkpoint increment (2026-10-17):** `SemanticEndTurnMechanicsStateV111` now stores draw origin/count/stage metadata and typed transcripts while live `SemanticEndTurnV111.HandDraw` projects its run from the shared end-turn mechanics checkpoint. Completed snapshots retain the prior draw-boundary run in the nested `HandDraw.Run` projection for replay while the common checkpoint stores the final run. This removes the second in-memory current-run owner from the live draw facade; common draw resolution and cross-provider equivalence remain open.

**S4 semantic pilot current-run projection increment (2026-10-17):** `SemanticPilotMechanicsStateV111` now owns the single current run and a runless pending end-turn mechanics projection; `SemanticPilotStochasticStateV111` owns the end-turn and automatic-draw transcripts. The flattened `PendingEndTurn` facade rebuilds the nested continuation from those owners, preserving its snapshot shape while removing the duplicate current run. Draw origin/count/stage metadata remain mechanics state, and typed draw decisions now live with the end-turn stochastic owner. Common resume flow and cross-provider mechanical equivalence remain open.

**S3 controlled recycle transcript ownership increment (2026-10-17):** `ControlledRecycleShuffleV111` is now the sole owner of decisions in an active recycle segment. `ControlledPlayableCombatStochasticStateV111` retains only completed recycle decisions; its flattened `RecycleTranscript` projection combines that prefix with the active stage. This removes duplicate live transcript storage while preserving the snapshot field and cumulative replay history.

**S4 automatic-pipeline stage interpretation increment (2026-10-17):** `PrototypeAutomaticPipelineMechanicsStateV111.CurrentStepKind` is now the shared interpretation of an ordinary pipeline cursor. Semantic end-turn, controlled-playable, legacy hand-draw, and draw-cost continuations use the common stage projection instead of independently indexing the pipeline definition. Boundary adapters still own their provider choices and resume policy; the complete shared resume flow and cross-provider mechanical-equivalence gate remain open.

**Latest continuation increment:** the semantic deck reveal kernel's existing peek/scry support now flows through typed semantic result, external resolver, replay transcript and `ReferenceNatureSessionV111`. This adds the existing `combat.draw_pile.reveal_card` site to session coverage. No source gameplay callsite currently initiates a peek/scry request, so this is resolver infrastructure and does not claim broader card-hook support.

**S4 replay-prefix increment (2026-10-17):** controlled event/run-action replay, automatic enemy-action replay, and next-turn intent replay now use one `ControlledRandomIndexReplayPrefixV111` for saved-decision cloning, cursor advancement, typed request matching, and unused-prefix rejection. Event/run-action ordinal normalization and candidate labels remain owned by their boundary adapter; combat requests retain source eligibility and ordinals. This removes duplicated prefix mechanics while preserving the existing continuation frames and does not complete the common-mechanics-continuation acceptance gate.

**S4 controlled provider-scope increment (2026-10-17):** index-only event/run-action and enemy-action resume now install their replay providers through `StochasticProviderScopeV111` for the full mechanics callback, including legacy choice call sites. These resumes fail closed on unit-float requests, while mixed typed replay continues through `ResumeStochastic`; the shared provider lifecycle is in place, but boundary-specific frames and complete common resume flow remain.

**S4 automatic hand-draw step-resume increment (2026-10-17):** pre-draw action-choice and random-pile-hook continuations now use one ordinary-engine helper to resume `DrawPlayerHand`, retain the shared requested/remaining cursor when suspended, and clear it at the completed pipeline boundary. The source checkpoints and semantic transcripts remain with their existing facades; complete semantic resume unification and cross-provider equivalence remain open.

**S4 semantic hand-draw entry increment (2026-10-18):** initial automatic semantic hand draw and pre-draw action-choice resumption now share the same ordinary-engine outcome interpreter. It handles random-pile/draw-hook suspension and source-ordered deterministic completion alongside semantic continuation, so the facade no longer owns a second completion path or advances the DrawPlayerHand cursor separately. Typed choice transcripts and cross-provider equivalence remain open.

**S4 semantic draw-result continuation increment (2026-10-18):** initial and post-cost semantic draws now share one engine-owned `DrawCards` result handler for shuffle insertions, nested draw hooks, Stratagem selection, and pending automatic draw progress. The adapters keep their typed decision history and boundary validation; nested semantic resumes and cross-provider mechanical equivalence remain open.

**S4 terminal end-turn handoff increment (2026-10-17):** after semantic hand draw, end-turn continuation now accepts the ordinary pipeline's actual Reward or Terminal result as well as a next-player boundary. A completed terminal/reward frame retains its at-boundary draw projection and transcript for source replay; pending draw state is still rejected.

**S4 shared automatic-pipeline handoff increment (2026-10-17):** ordinary controlled-choice continuations and semantic end-turn post-draw completion now resume through `PrototypeGameEngine.ResumeAutomaticPipelineContinuation`. It owns clearing the saved cursor, reopening the remaining ordinary stages, and settling the terminal combat reward boundary; specialized facades continue to validate their own final outcomes.

**S4 automatic draw-cursor ownership increment (2026-10-17):** while `DrawPlayerHand` is active, `SemanticCombatDrawV111` reads requested/remaining counts from `PrototypeAutomaticPipelineStochasticStateV111`; end-turn and pilot draw projections no longer store a second live copy. Flattened draw counts remain available for snapshots and completed-stage replay, where the ordinary pipeline has cleared its active cursor.

**S4 automatic draw-cursor interpretation increment (2026-10-17):** `PrototypeAutomaticPipelineStochasticStateV111.TryGetSemanticHandDrawProgress` now owns the active-cursor predicate and count projection. The ordinary-engine guard, pipeline validator, and semantic draw facade share that interpretation.

**S3 event-mechanics increment (2026-10-17):** `EventState.Mechanics` now owns event/choice identity, offered choices, pending deck/potion/reward choices, queued acquisitions, deferred losses, and event-page progress through `EventMechanicsStateV111`. The existing flattened fields project this owner and remain the snapshot representation. Forking an event now clones this mechanics envelope together with its stochastic provider envelope. Controlled event continuations still save their partial run and effect cursor separately, so the complete mechanics/stochastic envelope and common continuation remain open.

**S4 persisted chance-payload increment (2026-10-17):** controlled event, run-action, and automatic enemy-action continuations now share `ControlledChanceContinuationStateV111` for their saved decision prefix, pending typed request, and optional candidate-label transcript. Each boundary keeps its own mechanics checkpoint and validation rules; existing flattened continuation fields still project the shared payload. The ordinary-engine migration and full cross-provider mechanical-equivalence gate remain open.

**S4 common mechanics-checkpoint increment (2026-10-17):** semantic pilot, end-turn, draw, controlled playable, run-action, event-effect, and event-reward mechanics owners now use `PrototypeMechanicsContinuationStateV111` for their current run checkpoint; semantic frames also carry and validate the origin run. Existing snapshot properties remain flattened projections, while stage cursors and outcome transcripts stay with their boundary owners. This consolidates the checkpoint representation; moving their separate resume loops into the ordinary engine and proving cross-provider mechanical equivalence remain open.

**S4 AdvanceTurn continuation increment (2026-10-17):** `ControlledTurnIntentsV111` now resides in `PrototypeAutomaticPipelineContinuationState.Stochastic` at the suspended AdvanceTurn cursor for both `SemanticEndTurnV111` and `ControlledPlayableCombatV111`. Their existing `Intents` / `TurnIntents` snapshot properties project and restore the same pipeline frame. The automatic-pipeline owner validates it against the saved combat checkpoint and forks it with the engine continuation; the controlled-playable facade now delegates full run validation to `RunState.Validate`. This moves both semantic and scripted turn-intent staging into the ordinary pipeline; semantic draw staging and the full cross-provider mechanical-equivalence gate remain open.

**S4 opening-intent continuation increment (2026-10-17):** source-route initial intent staging now uses the same automatic-pipeline stochastic owner at an explicit `InitialEnemyIntents` opening cursor. `SemanticPilotCombatV111.PendingInitialIntents` remains the serialized compatibility projection; the pilot restores it into the pipeline frame, whose validator checks the source route's baseline combat and replays the saved intent prefix. The semantic pilot now delegates run validation to `RunState.Validate`. This removes the separate initial-intent provider owner but leaves the semantic draw adapter staging and full cross-provider mechanical-equivalence gate open.

**S4 semantic draw cursor-validation increment (2026-10-17):** `PrototypeAutomaticPipelineStochasticStateV111.Validate` now checks the automatic semantic hand-draw counters and the `BeforeHandDraw` pending marker against the DrawPlayerHand cursor and live Nature-deck checkpoint. The `SemanticCombatDrawV111` boundary frame retains its typed transcript and compatibility fields. The remaining S4 work is broader common resume flow and cross-provider mechanical equivalence.

**S3 pilot continuation ownership increment (2026-10-17):** `SemanticPilotStochasticStateV111` owns the pending `SemanticEndTurnV111` frame along with provider provenance. At that earlier migration point, intent frames were also held by the pilot; the later S4 AdvanceTurn and opening-intent increments moved those frames to `PrototypeAutomaticPipelineContinuationState.Stochastic`. `SemanticPilotCombatV111` preserves its existing serialized fields as projections, and its stochastic-owner fork deep-forks the end-turn frame. This groups the playable pilot's pending provider continuation atomically but does not yet merge all adapters into one engine continuation.

**S3 end-turn transcript ownership increment (2026-10-17):** `SemanticEndTurnStochasticStateV111` now owns and validates the intent, automatic enemy-action, target, and interleaving transcripts for the specialized semantic end-turn facade. Existing transcript fields remain flattened projections, while one owner fork deep-copies their typed requests and stable-identity decisions. The ordinary engine still owns mechanical execution through its automatic-pipeline continuation; unification of all semantic facades remains S4 work.

**S3 semantic draw transcript ownership increment (2026-10-17):** `SemanticCombatDrawStochasticStateV111` now owns the semantic draw frame's random pile-card, draw-insertion, Stratagem-selection, draw-hook and energy-cost transcripts. Existing properties remain flattened projections, and the owner deep-forks and validates typed decisions and provenance. Draw mechanics checkpoints and pending stages remain on the specialized frame, so common continuation remains open.

**S3 turn-intent provider ownership increment (2026-10-17):** `ControlledTurnIntentStochasticStateV111` owns the pending typed random-index request and decision prefix in `ControlledTurnIntentsV111`. The old fields remain flattened projections, the stochastic owner deep-forks and validates the provider payload, and `ControlledTurnIntentsV111.Validate` owns pure replay checking against saved enemy eligibility and mechanics.

**S3 automatic-pipeline provider validation increment (2026-10-17):** `PrototypeAutomaticPipelineStochasticStateV111` now validates the controlled recycle-Stratagem stage and nested enemy-action checkpoint against its automatic-pipeline mechanics boundary. `CombatStochasticStateV111.Validate` invokes this check, removing those provider-specific branches from the general combat invariant routine while preserving the mechanics-state checks there.

**S3 run-world bag ownership validation increment (2026-10-17):** `RunWorldStochasticStateV111` now validates its Act 1 encounter queues and routes persistent relic queue validation through `PrototypeNativeRelicGrabBag.Validate`, which checks queue membership, duplicate/owned relic exclusion and rarity partitions. Run-level stochastic validation invokes the world owner so these checks remain part of the full snapshot invariant path.

**S3 later-act bag ownership validation increment (2026-10-17):** Active and run-start prepared later-act encounter queues, their act-specific pool membership and Ascension-sensitive second boss selection now validate through `RunWorldStochasticStateV111`. The owner also checks that the revealed-Darv pool choice is eligible and that the shared Darv Ancient is assigned at most once across the active and prepared acts. The existing flattened snapshot fields and route gate are unchanged.

**S3 event realization validation increment (2026-10-17):** `EventStochasticStateV111` now validates its suspended controlled-choice consistency, the event-local cached Future of Potions offer set, committed native gold rolls, the Stone of All Time selected potion slot, Tinker Time's committed type selection, Endless Conveyor dish progression, War Historian Repy's preselected chest rewards and deferred event card outcomes. The broad event invariant retains its prior validation points, including the terminal-page early return, while mechanics and provider realization checks stay attached to their existing owners.

**S3 run continuation validation increment (2026-10-17):** `RunStochasticStateV111` now validates its controlled-choice and run-action histories, plus the saved run-action and event-reward continuations at their existing invariant boundaries. The flattened snapshot projection and continuation validation semantics are unchanged.

**S3 native event-context validation increment (2026-10-17):** The run stochastic owner now dispatches validation of each supported native event-context stream binding at the existing run invariant boundary. `NativeEventContextRngV111` remains the stream validator; this removes its event registry calls from the general invariant body without changing the native stream rules.

**S3 event simulation envelope increment (2026-10-17):** `EventState.Simulation` now composes its existing mechanics and stochastic owners in one generic `SimulationStateV111` envelope. The prior `Mechanics` and `Stochastic` properties are projections onto that single composite value, and snapshot fields remain flattened.

**S3 active event validation boundary increment (2026-10-17):** Active event invariant checks now live on `EventState.Validate`, called from the existing Event and Repy reward boundaries. The event owner delegates provider-owned outcome validation to `EventStochasticStateV111`; map history, phase routing and other run-level checks remain on the run invariant path.

**S3 automatic pipeline simulation envelope increment (2026-10-17):** `PrototypeAutomaticPipelineContinuationState.Simulation` composes its step/opening mechanics cursor and `PrototypeAutomaticPipelineStochasticStateV111` resume values. Existing flattened properties remain the snapshot wire representation.

**S3 turn-intent simulation envelope increment (2026-10-17):** `ControlledTurnIntentsV111.Simulation` now composes its baseline/current combat checkpoints, encounter cursor and request site with the existing intent transcript/pending request owner. Legacy fields remain flattened in snapshots.

**S3 semantic end-turn simulation envelope increment (2026-10-17):** `SemanticEndTurnV111.Simulation` now composes its run, intent, and hand-draw mechanics checkpoint with `SemanticEndTurnStochasticStateV111` transcripts and boundary chronology. The existing continuation properties remain flattened in snapshots.

**S3 playable-session simulation envelope increment (2026-10-17):** `ControlledPlayableCombatV111.Simulation` now composes its fixture/run/action history with `ControlledPlayableCombatStochasticStateV111` cursors and outcomes. The existing snapshot fields remain flattened.

**S3 semantic draw simulation envelope increment (2026-10-17):** `SemanticCombatDrawV111.Simulation` now composes the run, requested/remaining counts and draw mode with `SemanticCombatDrawStochasticStateV111` outcome transcripts. Existing snapshot properties remain flattened.

**S3 semantic pilot simulation envelope increment (2026-10-17):** `SemanticPilotCombatV111.Simulation` now composes its opening/run/action mechanics with `SemanticPilotStochasticStateV111` provider provenance and suspended continuations. Existing snapshot properties remain flattened.

**S3 run simulation envelope increment (2026-10-17):** `RunState.Simulation` now composes `RunMechanicsStateV111` with `RunStochasticStateV111`. Legacy run and provider fields still serialize in their established flattened form.

**S3 run-world simulation envelope increment (2026-10-17):** `RunWorldState.Simulation` now composes persistent world mechanics with encounter/relic bag realizations in `RunWorldStochasticStateV111`. Existing world snapshot fields remain flattened.

**S3 combat simulation envelope increment (2026-10-17):** `CombatState.Simulation` now composes `CombatMechanicsStateV111` with native RNG and semantic deck state in `CombatStochasticStateV111`. Existing combat snapshot fields remain flattened.

**S3 run-action continuation envelope increment (2026-10-17):** `PrototypeControlledRunActionContinuationV111.Simulation` now composes the partial run/action mechanics with `ControlledChanceContinuationStateV111` saved outcomes and pending request. Existing continuation fields remain flattened.

**S3 event-effect continuation envelope increment (2026-10-17):** `PrototypeControlledEventChoiceContinuationV111.Simulation` now composes the partial run, event effect cursor and queued consequences with the shared controlled chance prefix. Existing continuation fields remain flattened.

**S3 event-reward continuation envelope increment (2026-10-17):** `PrototypeControlledEventRewardChoiceContinuationV111.Simulation` now composes its partial run/event boundary and decision prefix with the shared controlled chance payload. Existing continuation fields remain flattened.

**S3 combat invariant ownership increment (2026-10-17):** The active combat mechanical invariant path now lives on `CombatState.Validate(PlayerState)`, and the existing run validation boundary delegates to it. `CombatState.Validate` calls the stochastic owner's provider validation at the same point.

**S3 run/world invariant ownership increment (2026-10-17):** Run and world mechanics checks now live on `RunState.Validate`; `PrototypeStateInvariants.Validate` remains a compatibility entry point and delegates to the run owner, which invokes the combat and event owners.

**S3 semantic end-turn validation ownership increment (2026-10-17):** `SemanticEndTurnV111.Validate` now checks the specialized continuation's staged mechanics and transcript relationship, then delegates typed history checks to `SemanticEndTurnStochasticStateV111`. The existing engine validator delegates to the frame owner.

**S3 semantic draw validation ownership increment (2026-10-17):** `SemanticCombatDrawV111.Validate` now owns the draw-stage and physical/provider deck consistency checks, while `SemanticCombatDrawStochasticStateV111` validates typed draw history. The old engine validator delegates to the frame owner.

**S3 controlled playable validation ownership increment (2026-10-17):** `ControlledPlayableCombatV111.Validate` now owns its pending-stage consistency checks and delegates nested intent/recycle checks to their frame owners. Existing engine save/replay paths call the frame validator.

**S3 semantic pilot validation ownership increment (2026-10-17):** `SemanticPilotCombatV111.Validate` now owns playable session boundary, nested continuation, and provider provenance checks. Engine validation and save/replay paths delegate to the pilot frame.

**S3 automatic-pipeline mechanics validation increment (2026-10-17):** `PrototypeAutomaticPipelineContinuationState.Validate(CombatState)` now owns its opening and step cursor rules. Combat validation delegates that check, while the stochastic owner retains resume-prefix validation.

**S6 Atomic Ancient-offer session increment (2026-10-17):** `ReferenceNatureAncientOfferSessionV111` now samples direct choices for Orobas, Pael and Tezcatara and conditioned Fisher-Yates swaps for Nonupeipe, Tanx and Vakuu in one atomic ReferenceNature continuation. It holds intermediate outcomes and the sampler cursor within the session, then replays the completed transcript through the existing event-specific provider and publishes the run and cursor together. Fork, save, restore, JSONL capability/session coverage and the Python binding use the versioned session schema `sts2-v111-reference-nature-ancient-offer-session-v1`. The native event cursor remains unchanged under `scripted-v111`; probability provenance remains `UNVERIFIED`, and no native reference mod or Fidelity Inspector work is included.

## S1 precise design

Implement a **read-only, outcome-blind** Chance Law query in `Sts2Emulator.Core` with:
- Semantic operation and call-site/owner/occurrence identifiers.
- Current legal **physical** outcome identities (distinct card instances) or stable intent IDs, eligibility and raw weights.
- Conditioning-policy ID and hidden **constraint** fingerprint; no selected native realization or RNG seed/cursor.
- Normalized/compact reference distribution, law version and `UNVERIFIED`/independently verified provenance.
- Immutable query result: the law cannot mutate RNG streams, card positions, game state or Nature's constraints.

`ReferenceNature` consumes that result, draws from it using an independent seeded test PRNG, returns one legal outcome and updates the Nature provider constraints through the existing selection/continuation mechanism. A known already-committed card produces **no Nature choice**. No native stream parity is required.

### Required independent tests

1. Weighted enemy intents after phase, previous-move, anti-repeat and formation eligibility filtering; candidate order changes must preserve stable identities and properly invalidate old support fingerprints.
2. Exhaustive **3–4 physical-card permutation** enumeration, including duplicate card models with distinct instances, known top/bottom positions, interleaved known and unknown slots, multiple draws, forced middle insertions, peek/scry and recycle/Perfect Fit restrictions.
3. Conditional joint probabilities after sequential decisions; compare exact small-state probabilities to independently enumerated reference permutations (not only Monte Carlo counts from ReferenceNature).
4. Proof-by-execution checks: Chance Law query must not change the state hash or depend on an already sampled realization when queried under the **same declared information state**; resolving a known card must never re-randomize it.
5. Invariant-based mechanical continuation: each chosen card triggers the real `DrawCards` / `CardDrawn` path once, with nested costs and interactions remaining engine-owned or explicitly unsupported.
6. Independent fork/save/load/replay, stale-event rejection, mode/provenance versions, state ownership and observation information-boundary tests.
7. Monte Carlo distribution smoke over many independently seeded episodes as a secondary check; a handful of rounds is **not** an evaluation of learned performance or general distribution accuracy.

## S2: training comparator and honest provenance

For #143 use two clear emulator policies:
- **ReferenceNature / declared-law control:** samples `p` at the semantic Nature sites without source cursor parity and delegates other chance to the engine's ordinary provider. This is the primary clear structural baseline.
- **Learned Nature:** selects `enemy_intent` and `draw_card` through `sts2-ai`; other stochastic operations go through the **identical** engine-owned provider.

Optionally check a **native-style commit-once control** (exact hidden deck realization using source-native RNG algorithms) against the ReferenceNature's specified conditional law. This is an additional distributional cross-check, not a requirement that counterfactual draws match native seed/cursor. Do not call an uncalibrated `p` the original-game conditional law.

Run matched source-state/seed sets; frozen player policy; fair observations/legal actions; independent full combat terminal outcomes; process-isolated 15-worker replay; report variance and multi-run comparisons. Keep toy/structural smoke distinct from performance evidence. `sts2-ai` owns optimizer, neural policy and experiments.

## S3–S5: migration without breaking the running project

- `SemanticDrawPileAdapterV111` now owns draw-pile membership/order correspondence checks and the shared semantic insert, remove, replace, shuffle, and physical-mutation boundary. Runtime draw-pile writes route through this adapter, which publishes the mechanics membership and provider state atomically. `CombatState.DrawPile` remains the mechanics membership source. `SemanticDeckStateV111` and its nested pool, pending, transcript, event, and order records now protect provider-owned arrays with constructor/replacement copies and defensive projections. `RunSnapshot` writes the composed `RunState.Simulation` envelope in v4 and continues to load the flattened v1–v3 formats; nested domain continuations retain their existing versioned or compatibility snapshot representations.
- `RunStochasticStateV111` owns and validates `SemanticOtherChanceStateV111`; `SemanticPilotCombatV111.OtherChance` remains its existing compatibility projection, and pilot transitions publish updates through `PublishRunContinuation`. The run snapshot wire migration is complete for this owner through v4; nested domain snapshot projections remain in place. `RunStochasticStateV111` copies incoming prototype and Native run RNG bundles on assignment and returns defensive public projections. All audited Core transitions that advance those bundles now work on local projections and publish through the owner helpers; direct mutable views remain inside owner implementation code. `RngBundle` provides named `GetStream`, `TryGetStream`, and identity-checked `ReplaceStream` access while retaining the serialized stream array. Keep the single physical membership source and reject mismatches before publishing a state.
- Combat entry and persistent combat-end relic callbacks fork and publish their local prototype and Native run streams through `RunStochasticStateV111`. The Core callsite audit now finds no run-owner mutable RNG view outside `RunStochasticStateV111` itself.
- `CombatStochasticStateV111` copies incoming `NativeRng` sidecars on assignment and returns a forked public projection. Combat consumers now read that defensive projection and publish updated owners through `WithNativeRng`; no `NativeRngMutable` access remains outside the owner type.
- Make the choice/continuation path consume semantic outcome identity, with a separate low-level native primitive journal. Preserve and version primitive-index scripts for compatibility.
- For each moved mechanic, add a native/scripted/ReferenceNature equivalence fixture and a legacy compatibility fixture **before** changing the default routing.
- Merge/rebase native inspector spectator/stream PRs independently on their current approved base. Carry forward source-specific hooks without binding them to Chance Law.
- Avoid touching the active Silent content branch while structural foundations are developed and tested on the isolated RNG integration branch.

## Deliverables, status discipline and ownership

- **S0:** ownership documents and a cross-linked site/state inventory are present. The checked locator finds row or linked-contract text for all five facets on 185/185 registered rows; every registered site maps to a matrix row, all rows state a law status, and the v60 direct census lists 113 typed request constructions (113 registry-resolved, zero dynamic or missing, and no static IDs absent from the registry), 13 source-shaped map adapter callsites, 20 direct Native player-RNG adapter callsites, 56 direct legacy random-choice adapter callsites, 21 direct Native event-context adapter callsites, 14 direct legacy Fisher–Yates adapter callsites, and two direct Native random-choice adapter callsites outside the NativePlayer wrapper. Indirect source completeness, accuracy of evidence links, Native source completeness, and the broader row-level acceptance review remain open.
- **S1:** the Chance Law core is in `ChanceLawV111.cs`; ReferenceNature sampling, cursor/session state, and specialized run-action, Ancient-offer, and event-choice sessions live in their owner-named source files. The first independent exact small-state and seeded replay suite passes, while the complete acceptance matrix remains open.
- **S2:** the emulator has an opt-in seeded native-policy comparator with private stream state, fail-closed provider guards, and a chronological mixed trace across all six random-boundary primitives; its existing index decisions remain a validated compatibility projection. The Nature action route now delegates drawn-card costs, registered random hand-cost target/value boundaries, registered random-enemy targets, registered combat card and potion model picks and card-transform replacements, Jeweled Mask's random draw-pile card pick, and saved enemy-theft selections through its run-owned Native stream bundle. It also has atomic semantic-combat, supported run-action, event-choice, Ancient-offer, Neow-offer, Trash Heap, configured native merchant, and configured native Treasure ReferenceNature JSONL sessions. The generic run-action session also controls Native-regional fallback Treasure and Shovel rarity/identity requests when `NativeRunRng` is absent. The real Act 1 weak-combat source route now reaches its ordinary terminal reward through a narrowly allowlisted engine-owned reward stream. The learned-vs-reference A/B trainer and 15-worker report live in `sts2-ai`. Remaining ordinary stochastic-site coverage and equivalence through one shared mechanics continuation remain open.
- **S3–S4:** every runtime draw-pile mutation now goes through the adapter, which validates and returns mechanics membership and semantic provider order together. Native and controlled Nature turn-intent paths share both the pure eligibility selector and the same mechanics outcome-commit function. The semantic end-turn continuation now suspends, resolves, saves and replays automatic-pipeline target and enemy-action decisions with a chronological boundary transcript; `SemanticEndTurnV111.Validate` owns end-turn stage and transcript consistency checks. `RunState.Simulation` composes `RunMechanicsStateV111` with `RunStochasticStateV111`, and `RunState.Validate` owns run/world mechanics invariants while delegating provider checks; `RunWorldState.Simulation` composes persistent world mechanics with `RunWorldStochasticStateV111` bag realizations and validates configured encounter queues plus relic ownership/rarity partitions; `CombatState.Simulation` composes `CombatMechanicsStateV111` with native RNG and semantic deck state, and `CombatState.Validate` owns combat invariants with stochastic validation delegated to the combat owner; `PrototypeControlledRunActionContinuationV111.Simulation`, `PrototypeControlledEventChoiceContinuationV111.Simulation`, and `PrototypeControlledEventRewardChoiceContinuationV111.Simulation` compose suspended boundary mechanics with the shared provider prefix; `PrototypeAutomaticPipelineContinuationState.Simulation` composes controlled draw/resume decisions with the automatic-pipeline cursor, whose mechanics owner validates cursor/opening boundaries and exposes safe `IsAtStep` queries for validators/adapters; controlled choice adapters now call `PrototypeGameEngine.ResumeAutomaticPipelineContinuation` directly for the saved opening or ordinary cursor handoff and terminal reward boundary; `ControlledPlayableCombatV111.Simulation` composes the fixture mechanics with its provider cursors and transcripts; `SemanticPilotCombatV111.Simulation` composes the playable mechanics with provider provenance and continuations; `ControlledTurnIntentsV111.Simulation` composes intent mechanics with its stochastic request prefix; `SemanticEndTurnV111.Simulation` composes the end-turn mechanics checkpoint with its transcripts; `SemanticCombatDrawV111.Simulation` composes the draw checkpoint/counts/stage with its transcripts; and `EventState.Simulation` composes event mechanics and stochastic state, including `EventCommittedRandomStateV111` outcomes/draw-relevant values (event gold, selected card/potion/dish/Tinker outcomes, and cached Repy/Future-of-Potions offers), while validating controlled choice, cached Future of Potions offers and native event gold rolls. Nested domain members retain their legacy compatibility projections, while exact run snapshots now serialize the composed run envelope in v4 and load flattened v1–v3 snapshots. Unification of semantic and native-policy session routes remains migration work.
- **S5:** the player schema remains unchanged; versioned stochastic capabilities negotiate supported Chance Law sites and ReferenceNature operations. `stochasticSessionCoverage` reports each continuation's supported SiteIds by schema, and validation requires all advertised session SiteIds to be present in the Nature-choice capability set; separate Chance Law query coverage includes native merchant offers/prices and compact plus granular native Treasure laws. Semantic Nature pending payloads expose typed laws for intent/draw and the implemented target, automatic enemy-action index, generated-model, cost, transform, potion, random pile-card, pool-shuffle, draw-insertion, and shortlist events. Controlled event-effect/reward and run-action pending payloads expose generic index laws with request and boundary fingerprints; atomic run-action and event-choice sessions have separate schemas and return to ordinary run handles when committed. The existing JSONL exercise reached an intent law and a 10-card deferred draw law while confirming the fair player view omitted Chance Law data; this coverage addition has not been exercised. Broader cross-mode observations and capability coverage remain open.
- **S6:** ReferenceNature now handles live random-enemy target laws for effect, Sly autoplay, and autoplay sites in both the pilot and semantic end-turn continuations; bounded index laws for supported random enemy-action requests; uniform generated-card model laws at the two existing semantic generation sites; bounded laws for three semantic card-cost sites; a random hand-cost target selection; the random Power instance selection at the Jewelled Mask site; selected-card transform replacements; the current generated-potion model site; bounded Fisher–Yates steps for generated-card pools, rare draw-pile selection, and controlled discard recycle; bounded positions at two semantic draw-insertion sites; sequential conditional choices at three shortlist sites; supported run-action sampling; and supported controlled event-effect/reward sampling with intervening player actions, including Stone push's deck-card choice. Native merchant Chance Laws describe discrete sale-slot, card-rarity/model, colorless-card, potion-rarity/model, relic-rarity, hidden relic identity, no-effect upgrade, and rounded card/potion/relic price outcomes. A dedicated atomic ReferenceNature session consumes those laws during configured Shop entry. A compact native Treasure law integrates the rarity roll and hidden queue permutation, while ReferenceNatureTreasureSessionV111 samples the rarity and conditional identity during configured Treasure entry. The generic run-action session also covers the Native-regional no-`NativeRunRng` Treasure and Shovel fallbacks' legacy rarity tickets and conditional identity draws. The shared native event shuffle session covers Repy's chest relic shuffle and two conditioned potion-index picks, while its player-stream assignment remains a hypothesis. Generic request-index and compact native Treasure identity-mixture laws remain `UNVERIFIED`; exact merchant and granular Treasure rarity, supported merchant-price, and Endless Conveyor ticket grid masses are `SOURCE_VERIFIED`. Repy bounded-index masses are `SOURCE_VERIFIED` on the configured potion support while `player.rewards` attribution remains a hypothesis. Beyond the Neow and Trash Heap sessions and Repy's chest-offer continuation, other non-Ancient offer and reward paths, unregistered Native merchant purchase effects and merchant cursor parity, native Treasure cursor parity and post-purchase draws, formations, other generated-card and cost operations, and later map operations still need law descriptors and/or supported selection paths. Legacy Shop-node generation is covered only at its seven current `shop.*` random-index sites; ordinary legacy Treasure-node generation is covered at its relic-pool index, with the Native-regional fallback as described above. Existing event/reward continuation support remains limited to its allowlisted sites; native merchant entry and registered purchase-index requests now use the serialized merchant Nature session, while Treasure post-purchase draws and source cursor parity remain open.
- **S7:** probability evidence/calibration; separately gated from #143.
- **F:** [inspector issue #122](https://github.com/nn971/sts2-emulator/issues/122) and [native plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md).

**Success criterion for the unified architecture:** one mechanical execution path under Native, ReferenceNature, scripted replay and learned Nature; provider-private state and provenance are unambiguous; declared-law ReferenceNature passes independent conditional distribution tests; complete states replay/fork identically; player-visible observations remain fair. Source-exact RNG parity remains its own inspector acceptance gate.

**S1 constrained-deck enumeration increment (2026-10-17):** `SemanticDeckEngineV111.EnumeratePossibleOrders` exactly enumerates every bounded full deck order compatible with a Nature-owned state, assigning each remaining pool's distinct physical instances across all of its referenced positions, including ranges split by known insertions. The result includes a conditioning fingerprint, exact completion count and stable outcome fingerprint; the equal-weight model remains `UNVERIFIED`, and the API rejects committed Native order. This gives source-independent consumers an exact small-state enumeration primitive without replacing the required independent permutation/probability acceptance checks. Gameplay callsites for peek/scry remain unintegrated.

**S0 matrix evidence locator increment (2026-10-17):** `tools/rng_callsite_inventory.py` parses the existing migration rows and reports row-level text or linked-document matches for private constraints, audience projections, resolver/continuation, probability provenance, and unsupported handling. Its optional local-link check catches missing contract files; the heuristic does not certify a facet or duplicate the site inventory. Encounter selection/formation and their pinned subsystem audits now share one contract. Six Ancient offer rows, eleven native merchant law rows, two native Treasure law rows, and selected semantic draw/enemy sites link their existing contracts, with explicit audience projection and unsupported boundaries. The locator finds all five facets for 98 of 182 rows; S0 remains open for the rest of the matrix.

**S6 Orobas law-query increment (2026-10-17):** `ChanceLawEngineV111.QueryOrobasOffer` returns the pre-event equal-weight law over the exact ordered, player-eligible choice IDs shared with `NativeOrobasEventV111` offer generation. The JSONL `chance_law_query` operation is advertised through a separate versioned query-coverage object and is read-only; its boundary and law include support and conditioning fingerprints, while probability provenance remains `UNVERIFIED`. This was the query-only increment; the later S6 atomic-offer increment adds a resumable session and reconstructs support from the origin run.

**S6 Ancient offer-law query increment (2026-10-17):** `ChanceLawEngineV111.QueryAncientOffer` dispatches all six registered Ancient offer sites and shares their opening-boundary validation and candidate builders with event generation. Hive offer pools return stable choice identities; repeated Pael entries aggregate into integer weights. The Glory shuffle sites return one bounded Fisher-Yates swap law per ordinal and require the preceding swap indices so the conditioning fingerprint binds the current ordered pools. The opt-in JSONL query coverage and atomic ReferenceNature session cover the same six sites. Session completion replays the original event generator and publishes the run with the committed Nature cursor; EventState compatibility fields remain unchanged. Probability provenance remains `UNVERIFIED`.

**S6 native merchant law-query increment (2026-10-17):** Read-only `chance_law_query` now covers the configured native merchant's sale slot, card rarity/model, Colorless card, potion rarity/model, relic rarity, and rounded card/potion/relic price sites. Candidate laws reuse the merchant's ordered model pools and condition on the current rarity offset, offer ordinal, and prior potion exclusions; threshold outcomes retain nominal source partitions while remaining `UNVERIFIED`. Offer and rounded-price queries require an available native Shop node at the pre-entry map boundary and leave run state unchanged. `ReferenceNatureRunActionSessionV111` still covers only legacy Shop generation; the new `ReferenceNatureMerchantSessionV111` owns configured native Shop entry, while exact native float-edge calibration, cursor parity, purchases, and later Treasure draws remain open.

**S6 native treasure law-query increment (2026-10-17):** Read-only `chance_law_query` covers the configured native treasure-room relic draw. Its compact law combines nominal 50/33/17 rarity weights with conditional equal support over hidden remaining queue permutations. The granular rarity and identity laws use the same eligibility and fallback helpers as native mechanics and fingerprint sorted remaining identities without exposing queue order. All laws remain `UNVERIFIED`.

**S6 native merchant provider-scope increment (2026-10-17):** Configured `StartShop` now uses `StochasticProviderScopeV111` to route ambient Native player-stream calls, native Fisher–Yates calls, and PotionFactory's explicitly passed providers through one replaceable index/unit-float provider pair. The scope tracks logical per-stream ordinals even when a replacement provider leaves Native RNG untouched. The local Native providers remain the default, while an enclosing controller scope can replace them. `ReferenceNatureMerchantSessionV111` now saves a mixed typed prefix, samples the existing merchant laws, replays actual Shop entry, and commits the shop with the run cursor; purchase continuation remains open.

**S4 mixed stochastic replay increment (2026-10-17):** `ControlledStochasticChanceReplayV111` and the versioned `ControlledStochasticContinuationStateV111` preserve chronological request order across random-index and unit-float decisions, with typed pending-request identity, scoped candidate-label matching and controlled-provenance validation. `ResumeStochastic` owns the paired provider scope and prefix replay lifetime. Existing index-only continuations remain unchanged. The native merchant session uses this primitive for its typed prefix, with a merchant law dispatcher and atomic shop/cursor commit. Hidden queue order is removed from its Nature origin projection; bag construction and purchases remain outside this session.

**S6 merchant law-to-source mapping increment (2026-10-17):** `ReferenceNatureChanceLawDecisionV111` maps one-to-one merchant index laws to ordered source indices and maps card/potion/relic rarity plus rounded card/potion/relic price outcomes to representative floats. Price representatives are searched against the same source conversion functions used by ordinary generation; rarity representatives are checked by the source threshold resolvers. This preserves the sampled mechanic outcome but does not calibrate `UNVERIFIED` weights or float-edge masses. The merchant session consumes the mapper, including a degenerate no-effect representative for the mechanically inert upgrade roll; Native cursor parity remains open.

**S6 legacy Underdocks combat-entry continuation increment (2026-10-18):** `ReferenceNatureRunActionSessionV111` now also suspends and replays eligible legacy Act 1 Underdocks Combat/Elite map-node entry. It uses the existing session cursor and shared formation, HP, intent, opening-shuffle, and pre-combat potion sites, with three region-specific encounter-pool sites registered and validated against the Underdocks route. The JSONL capability advertises separate Overgrowth and Underdocks groups. Configured Native runs and later acts remain excluded; generic index laws remain `UNVERIFIED`. The Release solution build succeeds with no warnings or errors; no tests were run.

**S6 shared relic pull-plan increment (2026-10-17):** `PrototypeNativeRelicGrabBag.PlanPull` chooses the active fallback tier and returns its eligible identities for Native mechanics and Treasure laws. Native `Pull` still resolves the first source-ordered identity, while `PullSelected` revalidates the current plan and removes a selected eligible identity without disturbing other queue entries. The merchant and Treasure Nature sessions use this mechanics-owned apply path for relic identities. Their canonicalized origins prevent Native queue order from entering Nature state; Native bag construction and Treasure draws after purchases remain open.

**S6 merchant relic identity increment (2026-10-17):** Mixed controlled replay now reaches the Nature-only `shop.merchant.relic_model.index_draw` site during configured `StartShop`, with a sorted labeled support and a saved same-shop exclusion prefix. `QueryNativeMerchantRelicSelection` verifies that support against the source fallback plan after prior selected identities are removed, and the typed outcome is applied through `PullSelected`; ordinary Native generation retains its queue-order draw. JSONL Chance Law coverage and the migration matrix include the new query site. The serialized merchant session now owns Shop entry and cursor publication; bag construction, later Treasure queue continuation and purchases remain open.

**S6 native merchant ReferenceNature-session increment (2026-10-17):** `ReferenceNatureMerchantSessionV111` begins from a legal configured Shop-node action, replays the real `StartShop` mechanics with mixed typed index/unit-float decisions, conditions each Chance Law on the pre-entry state and prior session outcomes, and commits the generated Shop with the ReferenceNature cursor atomically. It exposes fork/save/restore/status/resolve/release operations through JSONL and Python, and session coverage lists its sites separately. The Nature origin canonicalizes the existing relic queues and tags them as unordered so Native hidden queue order is not retained or later consumed as a queue front/back. Native bag construction remains required; bag creation, purchases, Native cursor parity and exact float calibration remain open. All merchant laws remain `UNVERIFIED`.

**S6 native Treasure ReferenceNature-session increment (2026-10-17):** `ReferenceNatureTreasureSessionV111` begins from a legal, non-skipped configured Treasure-node action and replays the real map action with mixed typed rarity-float and relic-identity decisions. The identity law conditions on the sampled rarity and shared fallback plan; the selected identity is applied by `PullSelected`. The session commits the resulting Reward or completed room with the ReferenceNature cursor atomically and exposes fork/save/restore/status/resolve/release through JSONL and Python. Coverage lists the rarity and Nature-only identity sites by session schema. Its origin canonicalizes and tags the existing relic sets, or constructs tagged sorted eligible rarity sets when the source run has no bag. Native execution retains its original rarity draw and queue-front resolution; an ordinary queue pull against a Nature-owned set now fails closed. Cursor parity, native shuffle replay, other bag-backed sites, Treasure draws after purchases, and probability calibration remain open. All Treasure laws remain `UNVERIFIED`.

**S6 Nature Treasure pool-opening increment (2026-10-17):** The Treasure Chance Law query and atomic session no longer require a Native-created relic bag. `PrototypeNativeRelicGrabBag.CreateNatureUnordered` uses the shared eligible-pool builder to seed canonical rarity sets directly from the source catalog and current ownership, then the existing conditional rarity/identity laws drive Treasure pulls. This avoids importing Native's shuffled queue order and models a pull as a draw from the hidden permutation. Nature projections use an optional order-mode discriminator and the versioned `sts2-exact-snapshot-v3-nature-relic-bag-order`; default Native snapshots retain their shape. Queue-front/back pulls reject Nature-owned sets unless a controlled Chance Law replay selects an identity. Native queue-shuffle calls, generalized residual-set continuations across other bag-backed sites, post-purchase Treasure draws and native cursor parity remain open. Laws stay `UNVERIFIED`.

**S0 map and combat-reward inventory increment (2026-10-17):** A shared source-shaped map contract now covers all five Overgrowth and six Hive/Glory map rows, including stream ownership, path and placement supports, player/Nature/inspector boundaries, synchronous generation, and the unavailable Nature path. The pinned combat reward-order contract now links eight `reward.*` and four `card_rarity.*` rows to the source chronology, private constraints, public reward projection, synchronous resolver, `UNVERIFIED` provenance, and unsupported ReferenceNature boundary. The generated locator finds all five evidence facets for 121/182 rows; this is a review pointer, so the S0 acceptance audit remains open.

**S0 reward-operation contract increment (2026-10-17):** The Native combat reward-order source document now also states the S0 boundary for combat reward generation and links 12 reward/card-rarity rows from the migration matrix. It binds those rows to the existing player reward-selection projection, persistent reward provider timing, private pool/pity/threshold constraints, synchronous population order, `UNVERIFIED` probability provenance, and missing atomic Nature continuation. The generated locator remains heuristic; current coverage is 121/182 rows.

**S0 legacy merchant-boundary increment (2026-10-17):** The existing Native merchant contract now explicitly covers the four linked legacy price/no-effect ticket rows, distinguishing their discrete price approximations and mechanically inert upgrade roll from the configured Native merchant's unit-float session. It records that these legacy operations have no matching ReferenceNature merchant continuation and must remain fail-closed if requested. The generated locator finds all five facets for 125/182 rows; merchant laws remain `UNVERIFIED` and S0 acceptance remains open.

**S0 Neow offer boundary increment (2026-10-17):** The pinned Neow generation audit now records the five offer sites' private eligibility/exclusion state, player projection, one-shot provider transcript, synchronous run-start resolver, unverified law status, and lack of a persisted ReferenceNature continuation. The migration rows link that existing source contract; no new site ledger was introduced. The census now finds all five facets for 130/182 rows, while source-filter completeness, call-order parity, and Nature session coverage remain open.

**S0 event-operation boundary increment (2026-10-17):** The existing Act 1 event-page and Trash Heap source audits now document the audience, resolver, provenance, and unsupported Nature boundaries for `event.tablet_of_truth.card.index_draw` and `event.trash_heap.relic_reward.index_draw`. Their migration rows link those operation-specific audits; both laws remain `UNVERIFIED`. The census finds all five facets for 132/182 rows, and S0 remains open.

**S0 event-context ownership increment (2026-10-17):** A shared contract now traces 17 event-local rows through `NativeEventContextRngV111` and `EventState`: context creation, persisted cursor updates, the legacy event-stream path, private candidate constraints, player projection, synchronous event resolution, and the unsupported Nature boundary. Row-specific supports and event stages stay in the existing matrix. The locator reaches 149/182 rows; it remains heuristic, and no laws are calibrated by this documentation.

**S0 site-boundary increment (2026-10-17):** Existing Punch Off, Pael relic, and War Hammer source audits now document six additional matrix rows' private supports, player-visible outcomes, synchronous resolver boundaries, `UNVERIFIED` law status, and unsupported Nature handling. The matrix links each row to its contract; reward-stream attribution for Punch Off remains a hypothesis, and Pael's Legion seed derivation and War Hammer source card ordering remain unverified. The census finds all five evidence facets for 155/182 rows; the locator is heuristic, and S0 acceptance remains open.

**S0 relic and assignment boundary increment (2026-10-17):** Existing source audits now document and link ten additional Fishing Rod, Sand Castle, Glory relic, Repy chest, and later-act Ancient assignment rows. They separate player outcomes from hidden supports, distinguish synchronous source-owned resolvers from the missing Nature continuations, and preserve unresolved stream-attribution claims. The census finds all five evidence facets for 165/182 rows; its keyword/link results remain review pointers and S0 acceptance remains open.

**S0 semantic combat and potion boundary increment (2026-10-17):** Existing semantic deck, insertion, full-pool shuffle, Anointed, automatic hand-draw, transform, and potion-generation contracts now link the remaining combat/potion rows. The Glory Improvement and Unknown-room operations also have explicit boundaries; the legacy Unknown-room integer ticket remains an approximation of the pinned source float path. The census now locates all five facets for 182/182 rows, but these keyword/link matches still need manual source review and do not close S0 acceptance. Every reference law remains `UNVERIFIED`.

**S4 provider-scope increment (2026-10-17):** Legacy typed index choices and Native event-context index/unit-float choices now route through `StochasticProviderScopeV111`, retaining their current provider as the default and using scope-maintained stream ordinals when a replacement provider does not advance saved RNG state. The shared scope now covers these adapters as well as Native player/explicit-provider calls. Scope installation across all ordinary engine actions, semantic draw/end-turn/pilot resume unification, and cross-provider mechanical equivalence remain open.

**S4 Native control-scope increment (2026-10-17):** Native-policy combat player actions now install the shared provider scope with Native index/unit-float providers while preserving explicit provider selection. Legacy reward delegation remains explicit. This aligns scoped provider lifecycle and ordinal tracking across the Native control arm; full ordinary-action coverage and mechanical equivalence remain open.

**S4 ordinary-step provider boundary increment (2026-10-17):** Every `PrototypeGameEngine.Step` now opens the shared provider scope in pass-through mode. Explicit legacy and Native adapters retain their providers, while an enclosing controller scope can replace provider selection across the complete action. Scope-owned logical ordinals are exposed only under an overriding provider, preserving ordinary request identities. Cross-provider mechanical equivalence and semantic continuation unification remain open.

**S4 semantic draw-progress ownership increment (2026-10-17):** Remaining draw-count interpretation, traversal of nested standalone draw continuations, and the expected number of cards in a Stratagem draw selection now live on `PrototypeGameEngine`, alongside the automatic hand-draw cursor and ordinary DrawCards mechanics. `SemanticCombatDrawV111` delegates these mechanics queries while retaining its typed transcripts and compatibility frame. Shared resume loops and full cross-provider mechanical equivalence remain open.

**S4 common hand-draw completion increment (2026-10-17):** Controlled and semantic `DrawPlayerHand` continuations now use `PrototypeGameEngine.CompleteAutomaticHandDrawStage` for the shared after-hand power cleanup and cursor advance, then resume the remaining authentic stages through `ResumeAutomaticPipelineContinuation`. Boundary-specific choice selection and transcripts remain in their adapters; broader resume unification and cross-provider mechanical equivalence remain open.

**S4 tagged automatic draw-progress increment (2026-10-17):** The automatic-pipeline stochastic owner now stores controlled draw, draw-cost, semantic pre-draw, semantic draw, and recycle-Stratagem progress in one discriminated value. Existing flattened continuation fields remain compatibility projections and reject conflicting active modes when loaded. Full resume-flow consolidation and cross-provider mechanical equivalence remain open.

**S5 separate Nature-choice view increment (2026-10-17):** JSONL now exposes pending Chance Law boundaries through `nature_choice_observe` across semantic and ReferenceNature combat, run-action, event-choice, Ancient-offer, Neow-offer, Trash Heap, Tablet of Truth, Trial, native event shuffle, Glory Reflections, merchant, Treasure, and controlled event/run-action sessions, using the versioned `SemanticNatureChoiceViewV111` envelope. `semantic_nature_observe` remains an alias for semantic combat. The capability manifest advertises both operations, the shared schema, and supported session kinds; old status `pending` objects remain compatibility projections. The view binds event and law support fingerprints plus conditioning/provenance metadata while leaving `semantic_observe` as the fair player view. Broader cross-mode observation and bridge-negotiation acceptance remain open.

**S6 Neow offer continuation increment (2026-10-17):** The five existing Neow choice and shuffle sites now have conditional Chance Laws and an atomic ReferenceNature session. It samples the current modeled offer graph, replays the full prefix through the existing Neow generator with the scripted provider, then publishes the run and Nature cursor together. The CLI advertises query/session coverage and exposes the pending Nature view; the Python binding supports fork, save/restore, resolve and state access. Unlock filtering, source-law calibration, Native cursor parity and the broader S6 acceptance gate remain open.

**S6 Trash Heap reward continuation increment (2026-10-17):** The fixed-pool card reward and nonlethal relic reward now have Chance Laws and a one-draw ReferenceNature session. The selected `grab` or `dive` action replays through the ordinary event mechanics with the scripted provider, and the run plus sampler cursor commit together while the Native event cursor stays unchanged. The saved EventState retains a `scripted-v111` marker and validates the committed reward against its source pool. Lethal dives remain draw-free. Native source filtering, cursor parity and law calibration remain open.


**S6 Tablet of Truth page continuation increment (2026-10-17):** The first four eligible-card draws now have conditional Chance Laws and one-page ReferenceNatureTabletOfTruthSessionV111 continuations. Each sampled physical card is applied by replaying the ordinary decipher action, with the page result and sampler cursor committed together while the Native event cursor stays unchanged. Page 4, lethal decipher, and empty eligible-card pools remain draw-free. The CLI and Python bindings expose query, session, snapshot, and Nature-choice coverage. Native source filtering, seeded cursor parity, law calibration, and the broader S6 gate remain open.


**S6 Trial case continuation increment (2026-10-17):** Glory Trial acceptance now has a three-outcome Chance Law and an atomic ReferenceNature session. The session replays the ordinary acceptance action, commits the case page and sampler cursor together, and preserves the TRIAL Native event cursor with a validated scripted provider marker. Reject and later case decisions remain ordinary player actions. Case-law calibration, source reward fidelity, seeded Native parity, and the broader S6 gate remain open.

**S6 native event shuffle continuation increment (2026-10-17):** Doors of Light and Dark and Tinker Time now use one conditional Fisher–Yates Chance Law and ReferenceNature session. The Tinker type shuffle can begin on its initial page; its rider shuffle is admitted only after the preceding scripted Nature type offer and is replayed through the ordinary type action. Pending runs remain private until each complete shuffle commits with its sampler cursor, preserving and validating the Native event cursor. Empty and singleton pools are draw-free. Python, JSONL capability/session operations, Nature-choice observation, migration coverage and the direct-call census include all three shuffle sites. Other event shuffles, Native seeded-result parity and law calibration remain open; all weights remain `UNVERIFIED`.

**S6 Fragrant Mushroom shuffle continuation increment (2026-10-17):** The existing native event shuffle session now also covers Hungry for Mushrooms' Fragrant Mushroom choice. It conditions each card-instance swap on the preceding prefix and replays the ordinary event choice after the complete shuffle; sessions reject the choice when its 15 HP cost is fatal and skip pools of zero or one. The Native event cursor remains at entry, and Python, JSONL capability, Nature-choice observation, migration coverage and the census include the site. Other event shuffles, Native seeded-result parity, probability calibration and the broader S6 gate remain open; the law remains `UNVERIFIED`.

**S6 Battleworn upgrade shuffle continuation increment (2026-10-17):** The existing native event shuffle session now accepts a legal combat action that wins Battleworn Dummy setting 2 immediately. It conditions each eligible card-instance swap on the preceding prefix, replays the killing action with other combat index requests routed through their existing stream providers, and commits the victory, upgrades, and sampler cursor together while preserving the Native event cursor. Multi-action combat continuations remain outside this boundary. JSONL, Python, capability coverage, Nature-choice observation, migration coverage, and the direct-call census include the site; the law remains `UNVERIFIED`.

**S6 Fake Merchant stock shuffle continuation increment (2026-10-17):** The existing native event shuffle session now accepts a legal configured Event-node action when ordinary weighted event selection resolves to Fake Merchant. It keeps that event-selection request on its existing provider, conditions each stock-relic Fisher–Yates swap on the preceding prefix, and scripts only the six-of-nine stock shuffle during replay. The event mechanics and sampler cursor commit together while preserving the Native event cursor. A map action that resolves to another event remains outside this conditional boundary; native seeded-result parity and source-law calibration remain open, and the law is `UNVERIFIED`.

**S6 Repy chest-offer continuation increment (2026-10-17):** The shared native event shuffle session now admits War Historian Repy's page-zero `unlock_chest` action in configured native regional mode. It captures the ordered unowned relic pool and pre-action `player.rewards` cursor, samples the relic swaps followed by both potion indices, and replays the complete typed prefix through the ordinary event action while preserving the player-stream entry cursor. Each potion law includes the full relic-swap prefix and any earlier potion pick; the event RNG context remains absent. Repy's source stream attribution is still a hypothesis; native parity, source-law calibration, and full S6 acceptance remain open, and the laws are `UNVERIFIED`.

**S6 Endless Conveyor initial-dish law query increment (2026-10-17):** The initial `event.endless_conveyor.dish_weighted_ticket` now has an outcome-blind Chance Law query at configured Event-node entry. The probe captures the typed unit-float request and pre-draw eligibility on a fork; the law exposes the source-shaped weighted dish support only when ordinary event selection enters Endless Conveyor. Later dish rolls and an atomic unit-float ReferenceNature continuation remain open; float calibration and native parity remain unverified.

**S6 Endless Conveyor initial-dish continuation increment (2026-10-17):** `ReferenceNatureEndlessConveyorSessionV111` now resolves the initial weighted ticket when ordinary event selection on a legal Event-node action enters Endless Conveyor. It samples the read-only law, scripts only the typed dish unit-float request during ordinary action replay, and commits the dish with the sampler cursor while preserving the Native event cursor. JSONL, Python, capability and Nature-choice coverage, migration status and the direct-call census include this boundary. Later dish rolls remain open and the law remains `UNVERIFIED`.

**S6 Endless Conveyor continuation-ticket increment (2026-10-17):** The same Chance Law and one-ticket session now cover a legal active-event action when its ordinary continuation reaches exactly one later weighted dish roll, including a Jelly Liver or Suspicious Condiment resolution that resumes the deferred roll. The query conditions on the captured pre-ticket state and Native cursor; replay keeps any earlier Native random effects ordinary, scripts only the ticket, then commits the dish and Nature cursor atomically. The forced fifth Seapunk Salad remains draw-free, and a session never spans multiple tickets. Laws remain `UNVERIFIED`.

**S6 Endless Conveyor Observe upgrade increment (2026-10-17):** The existing Endless Conveyor Chance Law and session now cover the legal first-dish Observe action. Its law records ordered eligible physical card instances and the persisted Native event cursor; replay scripts the one upgrade index through the ordinary action and verifies the selected card changed by one level. Empty upgrade support skips the draw. The session lifecycle and API are reused; the law remains `UNVERIFIED` and Native seeded-result parity remains open.

**S6 Endless Conveyor mixed Grab-prefix increment (2026-10-17):** The same session now covers Spicy Snappy's eligible-card upgrade and Fried Eel's supported Colorless-card draw before their next weighted dish ticket. Each action samples an ordered index-plus-unit-float prefix, then replays the complete ordinary Grab with both typed requests scripted and commits the card effect, dish, and Nature cursor together while preserving the Native event cursor. Empty Spicy upgrade support skips its index draw. The Colorless pool is restricted to implemented solo-safe cards; pool fidelity, native seeded-result parity, and probability calibration remain open, with laws `UNVERIFIED`.

**S6 Reflections conditional card-pick laws (2026-10-17):** Read-only Chance Laws now cover each distinct downgrade and upgrade card pick made by `touch_mirror`. Queries replay the supplied index prefix against a projected deck, recompute upgrade eligibility after downgrades, and expose the remaining physical card identities without incorporating a selected outcome. The laws remain `UNVERIFIED`.

**S6 Reflections atomic choice continuation (2026-10-17):** `ReferenceNatureGloryReflectionsSessionV111` now samples the ordered downgrade and upgrade picks as one session, recomputing the deck and upgrade support after each prior choice. It replays the ordinary `touch_mirror` action once all draws are resolved, then commits the changed run and Nature cursor together while preserving the Native event cursor. JSONL and Python expose status, resolve, fork, save/restore, release, capability coverage and Nature-choice observation. Draw-free Reflections states remain outside the session. Native seeded-result parity, source-law calibration and broader S6 equivalence remain open; the laws stay `UNVERIFIED`.

**S6 Infested Automaton card-offer continuation (2026-10-18):** `ReferenceNatureInfestedAutomatonSessionV111` now samples the card index for either `study` or `touch_core`, conditions the law on that choice and its eligible supported Silent pool, and replays the ordinary event action before committing the run and Nature cursor. JSONL and Python expose begin, status, resolve, fork, save/restore, release, capability coverage, and Nature-choice observation; direct Chance Law queries accept the selected choice as `action_id`. The native event cursor is preserved, while pool fidelity, source probability calibration, and seeded result/cursor parity remain open; the law is `UNVERIFIED`.

**S6 Punch Off combat-reward continuation (2026-10-18):** `ReferenceNaturePunchOffRewardSessionV111` now samples the relic and potion choices as one ordered prefix and replays a legal combat action only when it wins immediately and captures exactly those two `player.rewards` requests. Replay scripts only the reward requests, retains the pre-action reward and event cursors, and commits the reward state with the Nature cursor. The `player.rewards` stream attribution is still a hypothesis; reward pool fidelity, source probability calibration, and Native seeded parity remain open, with laws `UNVERIFIED`.

**S0 Punch Off reward-operation boundary update (2026-10-18):** The Punch Off source audit now records the restricted immediate-win ReferenceNature reward boundary: one legal action must produce exactly the ordered relic and potion requests, replay scripts only those requests, and both Native cursors remain at entry. Construct HP offsets have a separate fight-entry session. Prototype reward pool support, `player.rewards` attribution, source probability calibration, and seeded parity remain unverified; requests outside either admitted boundary fail closed.

**S4 monster-local provider-scope increment (2026-10-18):** `NativeCombatRngEntryV111.AdvanceMonsterLocal` now resolves through `StochasticProviderScopeV111`, retaining its Native provider in ordinary execution while allowing a boundary replay to replace the explicit monster-local provider. This closes the scope bypass at Punch Off setup; cross-provider equivalence and seeded monster-context parity remain open.

**S6 Punch Off fight-entry continuation (2026-10-18):** `ReferenceNaturePunchOffFightSessionV111` samples both construct HP offsets in CombatId call order, replays the ordinary Fight action while scripting only those two monster-local requests, verifies the opening moves and unchanged monster/event cursors, then commits combat and the Nature cursor atomically. JSONL and Python expose the direct query, session lifecycle, snapshot, release, Nature-choice view, and capability coverage. The bounded-index law and Native seed parity remain unverified; the later reward continuation stays a separate immediate-win boundary.

**S6 War Hammer elite-upgrade shuffle continuation (2026-10-17):** The shared Native shuffle Chance Law and session now cover one active War Hammer when a legal combat action immediately wins an ordinary Elite fight. A boundary probe captures the emulator's ordered eligible-card instances and the actual pre-shuffle `run.niche` cursor; replay scripts only those Fisher–Yates requests and commits the victory with the ReferenceNature cursor. Admission rejects multiple hammers, draw-free pools, delayed wins, and actions that make later `run.niche` draws after the shuffle. The source ModelId ordering remains unverified and the emulator's CardId-plus-instance-ID proxy remains its modeled order. Python and JSONL site coverage include the continuation. The law remains `UNVERIFIED`; seeded parity and broader S6/S4 gates remain open.

**S6 Sand Castle acquisition shuffle continuation (2026-10-17):** Orobas Sand Castle acquisition now reuses the saved controlled event-choice continuation for each conditional Fisher–Yates swap, labeling the current support with physical card-instance IDs and replaying the remaining upgrades before the event choice commits. Empty and singleton pools remain draw-free. The migration matrix and event-choice contract include the site; source stream ownership, Native seeded parity, and equal-index probability calibration remain open, and the law stays `UNVERIFIED`.

**S6 Beautiful Bracelet acquisition shuffle continuation (2026-10-17):** Event-gained Beautiful Bracelet now reuses the same controlled event-choice continuation for its descending deck shuffle, including Native `run.niche` requests under the replaceable provider scope. Each conditional support carries its current physical card-instance IDs; completed swaps replay through the ordinary acquisition hook and do not advance the Native cursor. Empty and singleton pools remain draw-free. The migration matrix and event-choice contract include the site; seeded parity and law calibration remain open, and the law stays `UNVERIFIED`.

**S6 Distinguished Cape curse-pick continuation (2026-10-17):** Event-gained Distinguished Cape now routes both without-replacement curse picks through the replaceable index-provider scope and the existing controlled event-effect frame. The first Nature result removes its curse from the ordered support before the second law is queried; both picks replay through the ordinary acquisition hook without advancing Native `run.niche`. The event-choice contract and migration matrix include the site; the law remains `UNVERIFIED` and native replay parity remains open.

**S6 Fur Coat map-mark continuation (2026-10-17):** Event-gained Fur Coat now reuses the same generic controlled event-effect continuation for its current-act Combat/Elite node shuffle. Each conditional swap carries the current ordered node IDs; the selected marks commit with the event choice while the separate relic-owned Native content `Rng` remains absent in Nature mode. The event-choice contract and migration matrix include the site. The law and native source parity remain `UNVERIFIED`.


**S6 Darv offer continuation (2026-10-17):** The existing Ancient-offer Chance Law and atomic ReferenceNature session now cover Darv's binary Dusty Tome/three-relic offer variant followed by the complete eligible-relic Fisher–Yates shuffle. The session binds each shuffle law to the selected variant and prior swap prefix, replays the full transcript through `NativeDarvEventV111`, and commits offers with the Nature cursor atomically. Both sites are registered for direct query; only the variant root starts the combined session. Source probabilities, full pool/call-order fidelity, and seeded native result/cursor parity remain open, and the laws stay `UNVERIFIED`.


**S4 semantic end-turn resumer merge (2026-10-17):** `SemanticEndTurnV111` now resumes the post-hand portion through the same `PrototypeGameEngine.ResumeAutomaticPipelineContinuation` used by controlled choices and draw completion. The redundant semantic-only engine wrapper is removed; the end-turn facade retains its next-player and terminal-boundary checks. Nested semantic draw resolution and cross-provider mechanical equivalence remain open.

**S7 Punch Off bounded-index source verification increment (2026-10-18):** Chance Laws now carry per-law `UNVERIFIED` or `SOURCE_VERIFIED` status, and the capability envelope advertises both while retaining its conservative legacy status field. The Punch Off `NextInt(8)` law is source-verified because each of the eight outcomes receives exactly `2^50` points on the 53-bit `NextDouble` grid under the declared uniform-grid input model. This does not prove run-seed frequencies, monster-context derivation, or seeded parity; other laws remain `UNVERIFIED` pending their own evidence.

**S7 Native shuffle bounded-index verification increment (2026-10-18):** The shared Native event-shuffle session applies the same source proof per request: power-of-two bounds divide the 53-bit `NextDouble` grid evenly and carry `SOURCE_VERIFIED`; other bounds keep their equal-weight modeled law `UNVERIFIED`. The status does not establish stream attribution, seed frequencies, or seeded parity.

**S7 Ancient-offer bounded-index verification increment (2026-10-18):** The Nonupeipe, Tanx and Vakuu offer-shuffle laws, Darv's relic-shuffle laws, and Darv's binary variant now use the same per-law bounded-index source proof. Power-of-two bounds are `SOURCE_VERIFIED`; other swap bounds and the direct Orobas, Pael and Tezcatara offer laws remain `UNVERIFIED`.

**S7 exact bounded-index masses increment (2026-10-18):** Native Punch Off, event-shuffle, and Ancient-offer Chance Laws now report each outcome's exact integer preimage count on the 53-bit `NextDouble` grid, including binary64 multiplication rounding before truncation. These laws are `SOURCE_VERIFIED` for every supported bound under the uniform-grid input model; stream frequencies, source context derivation and seeded parity remain separately open.

**S7 event and merchant bounded-index increment (2026-10-18):** Exact Native `NextInt` grid masses now cover Reflections' conditional downgrade/upgrade picks, Infested Automaton's supported card offer, Endless Conveyor's upgrade and Colorless-card indices, Repy's configured potion indices, and the merchant sale-slot, card-model, Colorless-model, and potion-model indices. `SOURCE_VERIFIED` applies to the bounded-index transform on each declared support; restricted event and Colorless pools, merchant pool completeness, nonmerchant potion-factory contexts, stream attribution, seed frequencies, and seeded cursor parity remain open. Repy's `player.rewards` attribution and Punch Off reward stream hypotheses remain unverified.

**S7 direct Ancient-offer bounded-index increment (2026-10-18):** Orobas, Pael and Tezcatara direct offer laws now use exact Native `NextInt` grid masses on their declared ordered pools. Pael duplicate choices aggregate the masses of their source indices. `SOURCE_VERIFIED` applies to the transform; Orobas's omitted source alternatives, Native stream assignment, full pool fidelity, seed frequencies and cursor parity remain open.

**S7 Neow bounded-index increment (2026-10-18):** The five Neow initial-offer laws now use exact Native `NextInt` preimage masses for their direct choices and conditioned positive-pool shuffle indices. `SOURCE_VERIFIED` applies to the transform on the declared support; the default pool's completeness, Native unlock filtering and call order, stream attribution, seed frequencies, and cursor parity remain open.

**S7 Native unit-float threshold increment (2026-10-18):** Merchant card, potion, and relic rarity laws, plus the granular Treasure rarity law, now count exact outcomes after Native `NextFloat()` rounds the 53-bit `NextDouble` grid to binary32 and source threshold comparisons are applied. The degenerate merchant upgrade law is source-verified as `no_effect`. The compact Treasure identity mixture, merchant rounded-price laws, other potion/card rarity contexts, seed frequencies, stream attribution, and cursor parity remain open.

**S7 Native merchant rounded-price increment (2026-10-18):** Card, potion, and relic price laws now count the exact 53-bit `NextDouble` preimages after unit-float rounding, source modifier arithmetic, `MathF.Round` midpoint-to-even behavior, and the card sale-reprice mapping where applicable. `SOURCE_VERIFIED` applies to the declared item and price branch. Merchant purchase behavior, item-pool fidelity, seed frequencies, stream attribution, and Native cursor parity remain open.

**S7 Endless Conveyor weighted-ticket increment (2026-10-18):** Initial and continuation dish laws now count exact 53-bit `NextDouble` preimages after unit-float rounding and the source's float multiplication plus sequential ticket subtraction. `SOURCE_VERIFIED` applies to the declared weighted support; source pool fidelity, Native event stream attribution, seed frequencies, and cursor parity remain open. The legacy integer-ticket path is unchanged.

**S7 Native event gold unit-float increment (2026-10-18):** Lost Wisp and Jungle Maze Adventure now expose conditional read-only gold laws for their three pinned `NextFloat` formulas. Exact 53-bit `NextDouble` preimages include binary32 rounding, source float arithmetic, and integer truncation, so those mappings are `SOURCE_VERIFIED` under the uniform-grid input model. No atomic event-entry session was added; the legacy integer fallback, event-pool completeness, stream attribution, seed frequencies, and cursor parity remain open.

**S7 Native event gold bounded-index increment (2026-10-18):** Conditional read-only laws now count exact Native `NextInt` preimages for the pinned Dense Vegetation, Whispering Hollow, Sunken Statue, Luminous Choir, Sunken Treasury, Punch Off, and This or That gold formulas. Sunken Treasury's primary and secondary requests remain separate ordinals, and Luminous Choir's reversed mapping is preserved. `SOURCE_VERIFIED` applies to each declared Native index transform; legacy fallbacks, remaining source alternatives, event-pool completeness, stream attribution, seed frequencies, and cursor parity remain open.

**S7 event gold conditioning correction (2026-10-18):** Gold-law queries now require `event_id` as an explicit branch condition and construct the expected typed request directly. They no longer step a Native fork to discover the selected event, keeping the hidden event-selection realization out of the Chance Law inputs. These remain conditional gold-transform laws; event selection, complete event-entry continuation, legacy fallbacks, and Native parity remain separate work.

**S7 Stone of All Time ticket increment (2026-10-18):** The `push_ticket` and `lift_ticket` sites have read-only conditional laws at their legal active-event choices. Native tickets use ordinal 1 after the entry potion-slot draw. The pinned resolver discards each bounded result, so all source indices map to one `no_effect` outcome and that mapping is `SOURCE_VERIFIED`. The existing atomic controlled event-choice continuation covers Native and retained legacy routes: lift suspends before potion/Max HP mutation; push records its player deck-card choice before suspending at the ticket. Lethal push HP loss ends the run before the ticket. Controlled outcomes advance neither provider cursor; seeded stream parity remains open.

**S7 Stone legacy pre-choice law increment (2026-10-18):** The direct one-outcome ticket law now accepts a configured Native or retained legacy event boundary. Native requests retain ordinal 1; legacy requests use the current `event` stream call count as the next typed request ordinal. The read-only query validates route identity and the legal action without advancing either provider. The collapsed `no_effect` mapping remains `SOURCE_VERIFIED`; seeded stream parity remains open.

**S7 Stone of All Time entry-slot increment (2026-10-18):** Its initial potion-slot index has a read-only law conditioned on a legal Event-node action and an explicit Stone event branch. Native uses exact `NextInt` masses at ordinal 0 and is `SOURCE_VERIFIED`. The retained legacy route validates the next `event` call ordinal after event selection and exposes equal occupied-slot weights as `UNVERIFIED`. The event-selection result, complete Event-node law, and Native cursor parity remain separate.

**S7 Future of Potions increment (2026-10-18):** Each source-ordered potion-slot card-type draw now has a read-only conditional law with exact Native `NextInt` masses over the rarity-derived card-type support. Queries require the legal Event-node action and explicit event branch and bind the preceding request prefix. Event selection, ordinary Native offer resolution, legacy fallback and cursor parity remain outside this law.

**S7 Trial law and information-boundary increment (2026-10-18):** The Trial Chance Law now uses exact Native `NextInt(3)` preimage weights and is `SOURCE_VERIFIED` under the uniform-grid input model. Law construction derives only the event stream label and no longer reads the Native cursor; the atomic replay session retains its separate untouched-cursor admission check.

**S7 Trash Heap law and information-boundary increment (2026-10-18):** The existing card and nonlethal relic reward laws now count exact Native bounded-index preimages on their declared source-ordered pools and are `SOURCE_VERIFIED` under the uniform-grid input model. Chance Law planning no longer reads the saved event cursor; atomic-session admission keeps its separate cursor check for replay. Pool completeness, event selection, stream attribution, and Native parity remain open.

**S7 Tablet of Truth law and information-boundary increment (2026-10-18):** Its one-page eligible-card laws now count exact Native bounded-index preimages and are `SOURCE_VERIFIED` on the declared ordered physical-card support. Law construction uses neither run seed nor saved cursor; each atomic page session preserves the Native cursor captured at that page boundary. Native source filtering and cursor parity remain open.

**S4 Distilled Chaos top-autoplay continuation increment (2026-10-18):** Manual Distilled Chaos use in the semantic pilot now suspends at an unresolved top card through the existing semantic draw event, saves staged physical identities and the queued operation suffix, and resumes through ordinary `AutoPlayCombatCard` mechanics. The seeded fixture can provide Distilled Chaos explicitly. The continuation rejects a required discard recycle and Mayhem's automatic-pipeline route, which still need a composed continuation. The draw law remains `UNVERIFIED`; the Release CLI build succeeds, and no tests were run.

**S4 Mayhem top-autoplay pipeline continuation increment (2026-10-18):** Nature semantic draws from Mayhem now preserve the `AutoPrePlay` automatic-pipeline cursor and resume through the History Course post-dispatch policy before the next opening or turn stage. The opening combat pipeline now represents that policy as its own saved stage, so a suspended Mayhem draw cannot skip it. Required discard recycle remains unsupported until shuffle and after-shuffle effects compose with this autoplay continuation. The draw law remains `UNVERIFIED`; the Release CLI build succeeds, and no tests were run.

**S4 semantic top-autoplay recycle increment (2026-10-18):** Distilled Chaos and Mayhem now recycle an exhausted Nature draw pile into a deferred shuffle epoch, then resolve each staged autoplay identity through the same Nature top-card event. The physical draw pile removes the identity selected by Nature instead of assuming its stored array order is the hidden top order. After-shuffle relics that insert generated cards use the existing controlled insertion continuation before the saved autoplay operation resumes. The draw law remains `UNVERIFIED`; the Release CLI build succeeds, and no tests were run.

**S4 nested Mayhem autoplay draw increment (2026-10-18):** Semantic draw and drawn-card cost suspensions reached inside a Mayhem-autoplayed card now admit only a saved autoplay card-play frame with the exact `AutoPrePlay` pipeline cursor. Their source-ordered draw hooks and card-play continuation resume before the automatic pipeline advances. Nested Nature recycle and after-shuffle insertion reuse the existing `DrawCards` continuation; the legacy recycle ticket without a semantic deck remains fail-closed. Other unrelated controlled sites remain separately gated. The draw law remains `UNVERIFIED`; the Release CLI build succeeds, and no tests were run.

**S6 deterministic Shop-action session increment (2026-10-18):** `ReferenceNatureRunActionSessionV111` now also wraps legal card purchases, potion purchases, potion replacement, and card removal when the ordinary action completes without a random request. The action is replayed through `PrototypeGameEngine.Step`, committed with the existing ReferenceNature run cursor, and has an empty chance transcript because no Nature outcome was sampled. If an action reaches an unhandled stochastic hook, the controlled boundary fails closed. Relic purchases and their stochastic acquisition effects, Native cursor parity, and post-purchase Treasure draws remain open.

**S6 index-only Shop-purchase continuation increment (2026-10-18):** The run-action envelope now preserves the full legal Shop action and fingerprints it in pending-law boundaries and completed traces. It can suspend and replay registered index-only effects during card, potion, replacement, removal, and relic purchases, including Beautiful Bracelet and Distinguished Cape shuffles, Sand Castle upgrades, random card/potion acquisition, potion-slot fills, and card-transform replacement. Pending request identity and candidate labels are re-derived by replay from the saved pre-action run. The session coverage and JSONL hello response list these sites separately. Their generic request-index laws remain `UNVERIFIED`; controlled outcomes do not advance game RNG. Unit-float acquisition hooks and unregistered purchase effects remain fail-closed; Native cursor parity and post-purchase Treasure draws remain open.

**S6 Alchemical Coffer purchase continuation increment (2026-10-18):** `ReferenceNatureMerchantSessionV111` now also accepts the current legal purchase of an Alchemical Coffer from a configured Native Shop. It reuses the session's mixed typed provider for the four PotionFactory rarity-float and model-index pairs, conditioning each model pool on the sampled rarity and previously selected potions, then commits the ordinary purchase result with the Nature cursor. Exact rounded-float threshold masses and bounded-index grid masses are `SOURCE_VERIFIED` on their declared input model and support. Other stochastic purchase hooks, Native cursor parity, and later Treasure draws remain open; no tests were run.

**S6 Native Shop-purchase player-index continuation increment (2026-10-19):** The existing Native merchant session now accepts legal configured Shop purchase actions and controls registered `player.rewards` bounded-index draws for random Rare-card/potion acquisition and potion-slot filling. Replay validates each typed request and ordered candidate support against the purchase action before committing mechanics and the Nature cursor together. The Native bounded-index transform is `SOURCE_VERIFIED`; pool fidelity, hook completeness, stream attribution, and Native cursor parity remain open. No tests or builds were run.

**S6 Native Calling Bell event-reward continuation increment (2026-10-19):** The existing controlled event-choice session now handles Native Darv's Calling Bell acquisition, whose reward pulls one relic from each forced Common, Uncommon, and Rare bag tier. The event action replays those ordered Native `player.rewards` model requests through the existing saved event-choice prefix, after projecting the committed bag to canonical Nature sets. The bounded-index transform is `SOURCE_VERIFIED`; hidden-queue identity, pool completeness, and Native cursor parity remain open. No tests or builds were run.

**S6 Native event gold continuation increment (2026-10-18):** `ReferenceNatureNativeEventGoldSessionV111` now consumes the existing exact unit-float laws while replaying legal configured Event-node entry for Lost Wisp and Jungle Maze Adventure. It controls one Lost Wisp gold draw or Jungle Maze's primary/secondary draws, keeps event selection and other requests on their existing providers, and commits the completed entry with the Nature cursor. CLI, Python, capability coverage, Nature-choice view and migration records expose the continuation. Shared Nature run projection now canonicalizes existing relic bags for merchant, Treasure, and event-gold replays; the event-gold session preserves an absent bag. Event-pool completeness, legacy fallback, stream attribution, Native seed parity and cursor parity remain open; no tests were run.

**S6 Native event gold index-session expansion (2026-10-18):** The same `ReferenceNatureNativeEventGoldSessionV111` now also consumes the exact bounded-index mappings for Dense Vegetation, Whispering Hollow, regional Sunken Statues, Luminous Choir, Sunken Treasury, Punch Off, and This or That. Its filtered mixed replay passes the earlier event-selection ticket through the existing provider and suspends at either the matching Native gold index or float request. Sunken Treasury and Jungle Maze preserve both ordered stage draws. Event context validation accepts the scripted gold-entry marker only when the event branch, result ranges, page, and untouched Native event cursor match. The same CLI/Python lifecycle and session schema serve both site IDs; pool/source alternatives and Native parity remain open; no tests were run.

**S6 Native Tezcatara Toy Box event relic continuation (2026-10-19):** `ReferenceNatureNativeEventRelicSessionV111` now replays a legal configured Native Tezcatara choice that grants Toy Box. The session captures and resolves its five rarity floats and five relic identity indices as one typed transcript, validates each identity support against the canonical Nature relic bag and prior pulls, and commits the Reward with the sampler cursor after all ten requests finish. JSONL, Python, session coverage, capability metadata, and the migration docs expose the boundary. The Native rarity and bounded-index transforms reuse existing source-shaped laws; source-pool completeness, hidden queue identity probabilities, event branch parity, and Native cursor parity remain open. No tests or builds were run.

**S6 Native Potion-event entry continuation increment (2026-10-18):** The existing atomic Native event-entry replay now consumes Stone of All Time's occupied-potion-slot law and The Future of Potions' per-potion card-type laws, in slot order, without adding a duplicate session. It preserves event selection and unrelated requests on their current providers, commits the event-entry result with the Nature cursor, and validates the scripted result against the unchanged Native event stream. Capability coverage and the migration matrix include both sites. Legacy route behavior, event-pool fidelity and Native parity remain open; the Release CLI build and Python syntax check pass, and no tests were run.

**S6 Native Potion-event continuation composition increment (2026-10-18):** Scripted entry markers now remain valid through the supported Stone of All Time and Future of Potions choices, and through the declared completion choices for generated-gold events. Stone push/lift tickets explicitly retain their Native source ordinal 1 after the Nature-controlled entry slot draw, so the existing atomic event-choice continuation can consume the ticket. An ordinary Native event-context draw clears the scripted marker after advancing the stream. The Release CLI build passes with no warnings or errors; no tests were run.

**S6 legacy Overgrowth combat-entry continuation increment (2026-10-18):** The existing `ReferenceNatureRunActionSessionV111` now suspends and replays legal legacy Act 1 Overgrowth Combat/Elite map-node actions through ordinary room entry. Its filtered controlled provider covers registered `combat`-stream Overgrowth encounter selection, formation, initial unique-enemy HP, initial committed intents, and opening draw-pile shuffle requests, plus the two legacy `event`-stream PotionFactory indices used by pre-combat relic hooks. Other requests retain their existing providers and unknown combat-stream sites fail closed. The site group is exposed through the existing run-action capability and registry, and the same session commits all choices with its Nature cursor. Configured Native runs, Underdocks, later acts, source-law calibration and Native cursor parity remain open. The Release solution build succeeds with no warnings or errors; no tests were run.

**S3 shared top-card resolution increment (2026-10-18):** `SemanticDrawPileAdapterV111.ResolveTopCard` now owns semantic provider/physical membership validation, Native top-order validation, Nature reveal suspension, and consumption of a known top identity for both ordinary `DrawCards` and `AutoPlayTopDrawCards`. Their distinct hook order and saved continuations remain with the callers. Broader draw-adapter staging, common multi-zone transitions, and cross-provider equivalence remain open; the Release build is the implementation check, with no tests run.

**S3 Nature recycle commit increment (2026-10-18):** `SemanticDrawPileAdapterV111.CommitNatureRecycle` now validates a staged exhausted-deck checkpoint and commits the new Nature shuffle epoch with the discard-to-draw-pile zone transfer. Ordinary draws, top-card autoplay, and saved controlled recycle use this operation, so the Nature constraint update and physical membership publication have one common boundary. Shuffle hooks and continuation frames remain engine-owned; the unconfigured legacy provider and multi-zone transition API remain separate open boundaries.

**S3 Native semantic recycle commit increment (2026-10-18):** `PrototypeGameEngine.CommitNativeSemanticRecycle` now shares the Native stream Fisher–Yates draw, Perfect Fit post-shuffle ordering, `RecycleNative` epoch update, and physical draw/discard commit between ordinary draws and top-card autoplay. Each caller still runs its own after-shuffle hooks and continuation policy. The unconfigured legacy provider envelope and common multi-zone transition API remain open.

**S3 legacy recycle commit increment (2026-10-18):** `PrototypeGameEngine.CommitLegacyDrawPileRecycle` now shares the unconfigured `PrototypeRng` shuffle, Perfect Fit post-hook, and physical draw/discard commit between ordinary `DrawCards` and `AutoPlayTopDrawCards`. The existing controlled-mode suspension gate and subsequent after-shuffle effects retain their prior position. The common multi-zone transition API and the legacy provider's broader RNG-control envelope remain open.

**S3 combat zone ownership increment (2026-10-18):** `CombatMechanicsStateV111.CardZones` now owns all six physical combat card-zone memberships through `CombatCardZonesV111`; the established `CombatState` fields remain flattened projections. Forking clones the shared zone owner, global and pending-choice validation enumerate it, and generic `SetZone` plus source-zone lookup use it. Draw-pile changes continue through the provider-aware adapter. A common transition API for multi-zone mechanics updates remains open.

**S3 common zone transition increment (2026-10-18):** `SemanticDrawPileAdapterV111.ApplyZoneTransition` now commits a complete `CombatCardZonesV111` owner with provider-aware DrawPile synchronization, global identity uniqueness validation, and provider correspondence validation. Generic zone writes, common card moves, multi-zone card-selection resolution, and Nature, Native, and legacy recycle commits publish through this boundary. Remaining card-hook outcomes still update compatibility projections directly. The Release solution build succeeds with no warnings or errors; no tests were run.

**S3 engine zone-write migration increment (2026-10-18):** All engine card-zone mutations now publish through the common zone transition boundary. This covers turn-end hand cleanup, explicit hand exhaust/discard, hand-return shuffles, Rebound retrieval, generated card placement, controlled pile choices and draw insertion, ordinary and semantic hand draws, opening-hand setup, and top autoplay staging. Snapshot-facing `CombatState` zone fields remain projections. Draw-pile provider operations for other hidden-order changes and cross-provider mechanical equivalence remain open; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S3 typed draw identity transition increment (2026-10-18):** `ApplyDrawPileInsertion` validates a source-visible physical index and resulting membership before synchronizing the provider insertion; `ApplyDrawPileRemoval` checks the exact remaining order, synchronizes removal from the active provider, and commits destination-zone changes with the owner. Enemy and relic insertions, controlled post-shuffle insertion, pile-to-hand effects, Rebound retrieval, random draw-card autoplay, and saved card-shortlist autoplay now use these typed boundaries. Replacement and shuffle commits and cross-provider mechanical equivalence remain open. The Release solution build succeeds with no warnings or errors; no tests were run.

**S3 typed draw identity replacement increment (2026-10-18):** `ApplyDrawPileReplacement` now validates the replaced identity at its physical slot, preserves the surrounding order, substitutes the identity in the active semantic provider, and commits the full zone owner. Random card transforms use it when the transformed card is in DrawPile; transforms from other zones use the common zone transition. Shuffle commits and cross-provider mechanical equivalence remain open. The Release solution build succeeds with no warnings or errors; no tests were run.

**S3 typed draw-pile shuffle increment (2026-10-18):** `ApplyDrawPileShuffle` now validates the exact set of existing draw-pile identities plus incoming identities, and publishes the shuffled order, provider state, and affected zones together. Nature, Native, and legacy recycle commits, controlled recycle continuation, and hand-return shuffles use this boundary. The Release solution build succeeds with no warnings or errors; no tests were run.

**S4 nested legacy recycle continuation increment (2026-10-18):** A guarded card-effect draw without a semantic deck now saves the remaining `DrawCards` operation and outer effect/card-play continuation while each discard-pile Fisher–Yates swap resolves through the existing pool-shuffle event. Completing the shuffle commits through `ApplyDrawPileShuffle`, applies after-shuffle insertions through the existing continuation, and resumes the original draw before Mayhem advances its `AutoPrePlay` pipeline. A one-card recycle commits directly without a stochastic ticket. Canonical Native `run.shuffle` swaps have exact `NextInt` grid masses and `SOURCE_VERIFIED` bounded transforms; legacy `prototype.combat` requests remain `UNVERIFIED`. The Release solution build succeeds with no warnings or errors, and no tests were run.

**S3 typed multi-card top placement increment (2026-10-18):** `ApplyDrawPileInsertionSequence` now commits a source-ordered set of known card identities at one physical index, validates all source and destination zones, and inserts the corresponding provider slots in the same order. Card selection that moves cards to DrawPile now uses this boundary, and its known top placements remain marked publicly known; DrawPile card selection and opening-hand movement use typed removals. The Release solution build succeeds with no warnings or errors; no tests were run.

**S3 generic top-placement guard increment (2026-10-18):** the generic `SetZone` helper now routes known top insertions through `ApplyDrawPileInsertionSequence`, including atomic removal from any previous zone, and rejects DrawPile reorder or removal requests that lack an explicit typed operation. The semantic provider therefore receives the source-known placement and the mechanics owner together. The Release solution build succeeds with no warnings or errors; no tests were run.

**S3 run-level control-provider ownership increment (2026-10-18):** `RunStochasticStateV111` now owns the seeded comparator's engine-owned drawn-card-cost provider alongside the named run streams. `SemanticPilotCombatV111.OtherChance` remains the existing snapshot projection, while `SemanticPilotStochasticStateV111` no longer stores a duplicate provider copy. Run forks and invariant validation include the provider. This changes no serialized fields; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S3 run-level reward-audit ownership increment (2026-10-18):** terminal reward RNG cursor validation now lives on `RunStochasticStateV111` beside the reward stream it audits. The pilot's established `RewardAudit` field projects that owner for snapshot compatibility, and the pilot stochastic envelope no longer duplicates the audit. The Release solution build succeeds with no warnings or errors; no tests were run.

**S3 explicit draw-pile operation guard increment (2026-10-18):** generic `ApplyZoneTransition` now rejects DrawPile changes. Fixture initialization, resolved physical-order commits, known insertions/removals/replacements, and full shuffles use named adapter operations; runtime call sites were migrated and audited for unchanged-pile generic commits. The compatibility `ApplyPhysicalMutation` API delegates to the explicit same-identity order commit. The Release solution build succeeds with no warnings or errors; no tests were run.

**S4 provider-independent mechanics projection increment (2026-10-18):** `SimulationMechanicsProjectionEngineV111` composes the existing run, world, combat, and event mechanics owners into a canonical projection and fingerprint, omitting run seeds, provider cursors, hidden deck order, and unresolved chance payloads. It provides a reusable structural comparison aid after matching semantic outcomes; it does not establish cross-provider equivalence by itself. The Release solution build succeeds with no warnings or errors; no tests were run.


**S4 common automatic-pipeline handoff increment (2026-10-18):** controlled draw-cost, generation, potion, insertion, pool-shuffle, shortlist, random-cost, and Knowledge Demon adapters now enter the ordinary engine through `ResumeAutomaticPipelineContinuation` directly. The old internal choice-named method remains a compatibility alias, while the engine owns one cursor-resume and terminal-reward path. The Release solution build succeeds with no warnings or errors; no tests were run.

**S1 Chance Law information-boundary increment (2026-10-18):** Stone of All Time's Native and legacy event laws no longer inspect saved stream call counts to derive or validate request ordinals; callers provide the declared source ordinal, and replay checks the typed request in its saved chronological boundary. Infested Automaton and Glory Reflections plans validate only the event stream identity, not the saved cursor. The common automatic-pipeline resume owns terminal reward settlement after controlled autoplay draws; nested semantic draw choices and card-effect draw suffixes return through their owning continuations before the pipeline can settle rewards. Full Chance Law input isolation and S4 resume consolidation remain open; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S4 automatic-pipeline clean-boundary guard (2026-10-18):** The shared resume now advances only after ordinary and Knowledge Demon choices are clear, the semantic deck has no unresolved reveal, and the pipeline has no active hand-draw, enemy-action, or intent-provider payload. Its terminal reward handoff applies the same clean-boundary guard and waits for the automatic cursor to be consumed. This keeps incomplete controlled continuations from advancing or settling combat; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S4 mechanics comparison diagnostics (2026-10-18):** The provider-independent mechanics projection now reports a bounded list of JSON-pointer differences with both fingerprints, so cross-provider replay divergence identifies the affected fields. It compares mechanics only and does not establish native fidelity or stochastic-choice equivalence; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S5 Nature-choice provider-metadata boundary (2026-10-18):** `SemanticNatureChoiceViewV111` now publishes schema v2 and recursively removes provider stream, seed, cursor, RNG-state, and call-count fields from pending-event metadata, including nested semantic intent events. Law support, site/owner occurrence, conditioning, and boundary fingerprints remain available; legacy pending-status payloads stay unchanged. The Python binding negotiates v2 from `hello`; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S6 legacy Unknown-room continuation increment (2026-10-18):** The existing ReferenceNature run-action session now admits eligible Unknown-node entries on unconfigured legacy regional runs with no `NativeRunRng`. It exposes the existing 1000-way bounded ticket through the generic unverified request-index law and replays the ordinary map action, including forced-Event ticket consumption and odds updates. Configured Native run streams and source-float/cursor parity remain outside this continuation; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S0 semantic-recycle ledger alignment (2026-10-18):** Added the distinct `combat.draw.recycle.shuffle_swap_index` semantic effect-recycle site to the existing migration matrix with its pool, source timing, Nature continuation, and unverified law status. The direct-call census now finds migration rows for all 139 registered stable sites and heuristic locators for the five acceptance facets on all 183 rows; these matches remain review pointers, so manual S0 acceptance is still open.


**S5/S6 semantic hand-return registry alignment increment (2026-10-18):** The typed pool-shuffle event now accepts the existing Bottled Potential `ReturnHandToDrawAndShuffle` continuation and validates it against `combat.hand_return.draw_pile.shuffle_swap_index`. The stable site is included in semantic Nature capabilities so clients can discover the already-implemented saved replay path. The census maps all 140 registered site IDs to the existing migration rows; the Release solution build succeeds with no warnings or errors, and no tests were run. Native stream attribution, cursor parity, and law calibration remain `UNVERIFIED`.


**S7 Fisher–Yates bounded-law increment (2026-10-18):** `ChanceLawEngineV111.QueryPoolShuffle` now uses the exact Native `NextInt` 53-bit grid masses for each saved descending swap bound and reports `SOURCE_VERIFIED` for that transform. This covers generated-card, rare-card, hand-return, and semantic effect-recycle swap sites on their declared supports; it does not establish source pool completeness, stream assignment, seed frequencies, or cursor parity. The Nature-choice view takes provenance from the Chance Law and omits an older conservative event-level status when it conflicts, preserving stored transcript fields. The Release solution build succeeds with no warnings or errors, and no tests were run.


**S1 seed-free Chance Law stream identity increment (2026-10-18):** Infested Automaton and Glory Reflections card-pick plans, plus Darv offer and shuffle requests, now use `NativeRngV111Contexts.EventStreamId` instead of constructing a seeded stream just to read its fixed ID. Their Chance Law builders no longer inspect `RunSeed`; event legality, provider mode, support, and action conditions remain checked. This removes seed state from these laws while preserving the typed request identity. The Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 target and generated-card bounded-law increment (2026-10-18):** Random-target and generated-card Chance Laws now count exact Native `NextInt` preimages on their declared ordered supports and report `SOURCE_VERIFIED` for the bounded-index transform. This covers all three random-enemy sites and both generated-card model-selection sites; source-pool completeness, provider parity, stream attribution, seed frequencies, and cursor parity remain separate. Stored event compatibility statuses are retained, while the Chance Law owns the calibrated provenance. The Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 bounded combat-cost law increment (2026-10-18):** The random hand-card cost and both drawn-card cost Chance Laws now count exact Native `NextInt` 53-bit preimages over each event's contiguous `0..bound-1` support and report `SOURCE_VERIFIED` for the transform. Stream attribution, seed frequencies, and cursor parity remain separate. The Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 random hand-card selection law increment (2026-10-18):** The fixed-cost hand-card selection Chance Law now uses exact Native `NextInt` masses on its ordered physical-instance support and reports `SOURCE_VERIFIED` for the index transform. Eligibility, stream attribution, seed frequencies, and cursor parity remain separate. The Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 random draw-pile Power law increment (2026-10-18):** Jeweled Mask's `random_pile_card` Chance Law now counts exact Native `NextInt` preimages on the declared ordered physical-card support and reports `SOURCE_VERIFIED` for the bounded-index transform. Native pool eligibility, stream attribution, seed frequencies, cursor parity, and mechanics evidence for the saved hook continuation remain open. The Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 combat model-pool law increment (2026-10-18):** Combat card-transform replacement and generated-potion Chance Laws now count exact Native `NextInt` preimages on their declared ordered model supports and report `SOURCE_VERIFIED` for the index transform. Full source-pool eligibility, stream attribution, seed frequencies, and cursor parity remain separate; both continuations still need mechanics evidence. The Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 card-shortlist index-law increment (2026-10-18):** Random auto-play and random replay card choices now use exact Native `NextInt` masses per ordered remaining support and report `SOURCE_VERIFIED` for the bounded-index transform. The draw-pile `random_shortlist` site remains `UNVERIFIED` because its Native choice is produced by a full-pool shuffle and needs the corresponding conditional permutation law. Source-pool completeness, stream attribution, seed frequencies, and cursor parity remain separate; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 enemy HP bounded-law increment (2026-10-18):** The saved enemy-action Chance Law now uses exact Native `NextInt` masses for unique enemy HP selection and Tough Egg hatchling HP, each on its declared bounded support. Native stream attribution, seed frequencies, cursor parity, and retained legacy modulo probabilities remain separate. The summon-pool law remains `UNVERIFIED`; the Release solution build succeeds with no warnings or errors, and no tests were run.


**S7 weighted intent-ticket law increment (2026-10-18):** Initial and next-turn intent Chance Laws now map exact Native `NextInt` 53-bit preimages through each source-validated raw-ticket interval, reporting `SOURCE_VERIFIED` on the declared weighted support. Source eligibility completeness, seed frequencies, Native cursor parity, and player-information likelihood remain separate. The Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 generic Native index-request law increment (2026-10-18):** The shared request-index Chance Law now recognizes stream IDs through the existing `NativeRngV111` and `NativeRngV111Contexts` helpers, then reports exact `NextInt(request.SupportCount)` 53-bit grid masses on that declared support. Legacy and unknown stream IDs keep the equal-index `UNVERIFIED` model. This calibrates the bounded transform only; stream attribution, source-pool completeness, seed frequencies, and cursor parity remain separate. The Release solution build succeeds with no warnings or errors; no tests were run.

**S7 automatic enemy-action index-law increment (2026-10-18):** Saved enemy-action Chance Laws now use the same recognized Native stream rule for any typed index request, including the Thieving Hopper's ordered card-instance choice; legacy or unknown streams retain equal `UNVERIFIED` weights. Native stream attribution, source eligibility and cursor parity remain separate. The Release solution build succeeds with no warnings or errors; no tests were run.

**S1 Chance Law normalization guard (2026-10-18):** `ChanceLawV111.Validate` now requires a `SOURCE_VERIFIED` law to cover the complete Native 2^53 input grid, except for a canonical single-outcome law weighted 1. This centralizes normalization for bounded-index, unit-float, weighted-ticket and mapped-outcome laws while retaining deterministic no-effect projections. The Release solution build succeeds with no warnings or errors; no tests were run.

**S0 census conditional-law status increment (2026-10-18):** The direct-call inventory v8 now counts migration rows explicitly prefixed `Conditional:` separately from fully `UNVERIFIED` and calibrated rows. The generated census reports 104 unverified, 15 conditional and 64 calibrated rows across 183 ledger rows, with all 140 registered site IDs mapped. Conditional rows retain their mixed route-specific claims; manual per-site acceptance remains open.

**S7 Native random-shortlist swap-law increment (2026-10-18):** Nature-controlled `ChooseCards` random shortlists now reuse the existing source-ordered pool-shuffle continuation for every descending Fisher–Yates swap, then publish the ordinary player selection over the shuffled prefix. Canonical `run.combat_card_selection` swaps receive exact Native `NextInt` grid masses and `SOURCE_VERIFIED` transform status; legacy and compatibility shortlist queries remain `UNVERIFIED`. Pool eligibility, stream attribution, seed frequencies, cursor parity, and cross-provider acceptance remain separate; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 Native draw-insertion law increment (2026-10-18):** Supported generated-card, after-shuffle relic, and Personal Hive insertion events now bind `run.shuffle` when a Native combat stream owner is present. `QueryDrawInsertion` reports exact Native `NextInt` 53-bit masses and `SOURCE_VERIFIED` for those declared positions; legacy `prototype.combat` requests retain the equal-position `UNVERIFIED` model. Pool eligibility, stream attribution, seed frequencies, cursor parity, and fixture acquisition remain separate; the Release solution build succeeds with no warnings or errors, and no tests were run.

**S7 automatic enemy insertion law increment (2026-10-18):** The existing suspended enemy-action index event applies the same request-conditioned Native bounded-index law to enemy-generated draw/discard insertions on canonical `run.shuffle`; the migration matrix now records those Native routes as conditional and keeps legacy `combat` routes `UNVERIFIED`. Enemy pool eligibility, source attribution, cursor parity, and mechanics acceptance remain separate.

**S7 live recycle law metadata increment (2026-10-18):** `NatureBoundaryMetadataV111` now attaches `QueryRandomIndex` law IDs and statuses to pending controlled requests, and records the derived law status in replay transcript entries. Native `run.shuffle` recycle swaps surface the exact bounded-index law as `SOURCE_VERIFIED`; unknown/legacy streams stay `UNVERIFIED`. The build and census checks pass; no tests were run.

**S4 shared draw-insertion event increment (2026-10-18):** Semantic pilot and semantic draw/end-turn adapters now create insertion events through one typed factory. The draw adapter preserves `run.shuffle` on Native-backed after-shuffle relic insertions instead of labeling them as legacy `prototype.combat`, so both entry points query the same Native bounded-index law. Broader recycle contexts remain fail-closed at their existing boundaries; the Release build succeeds with no warnings or errors, and no tests were run.

**S3 paired top-card resolution increment (2026-10-18):** `SemanticDrawPileAdapterV111.ResolveTopCard` now returns the consumed provider state together with the corresponding remaining physical draw-pile identities. It validates the resulting provider/membership correspondence for Native and Nature modes, while pending Nature reveals preserve physical membership. Ordinary draws and top autoplay use this shared result rather than removing the identity independently; their caller-specific hooks and continuations remain separate. The Release solution build succeeds with no warnings or errors, the direct-call inventory check passes (140 registered sites mapped; 97 unverified, 22 conditional and 64 calibrated), and no tests were run.

**S4 typed draw-result boundary increment (2026-10-18):** Every engine consumer of `PrototypeDrawCardsResult` now uses its shared `BoundaryKind` classification. The result rejects combinations with multiple pending continuation kinds, preventing automatic hand draw, opening draw, semantic draw, and nested card-effect draw adapters from silently choosing different suspension precedence. Adapters retain their boundary-specific continuation and hook ownership. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 tagged draw-result increment (2026-10-18):** Replaced `PrototypeDrawCardsResult`'s independent suspension flags with one `PrototypeDrawCardsBoundaryV111` value. Private factories bind each boundary kind to its legal remaining-count and optional insertion payload, so a result cannot carry two competing continuation states. The engine adapters dispatch on that shared discriminant while preserving their saved-frame and hook policies. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared draw-cost mechanics increment (2026-10-18):** Semantic hand draws and nested effect draws now use one engine helper to validate the selected cost bound, apply the card-cost change, advance the Relic/Slither sequence, and dispatch `CardDrawn` once. The two continuation facades retain their own nested-choice attachment and automatic-pipeline handoff rules. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared after-shuffle draw suspension increment (2026-10-18):** Ordinary semantic hand draws and nested effect draws now route `AfterShuffleInsertions` through `SuspendDrawCardsAfterShuffleInsertion`. It combines previously drawn identities with deferred keyword state, carries the remaining draw count and Stratagem condition, and publishes the pending insertion suffix through the common continuation adapter. Only the automatic-hand-draw caller supplies and updates the pipeline cursor. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared Stratagem draw suspension increment (2026-10-18):** Semantic hand draws and nested effect draws now route the `ShuffleSelection` boundary through `SuspendDrawCardsForStratagemSelection`. The helper validates eligibility, carries the remaining count and deferred identities, and resumes the controlled choice while retaining each caller's continuation frame; only automatic hand draw supplies pipeline progress. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared CardDrawn hook continuation increment (2026-10-18):** Standalone semantic draws and nested effect draws now use `SuspendDrawCardsAtCardDrawnChoice` to build and attach the draw-resume operation. Effect draws retain their deferred keyword identities and outer continuation; standalone choices retain their marker with a single resume operation. Automatic hand draw continues to use its pipeline-owned hook marker. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 semantic draw-frame advancement increment (2026-10-18):** `AdvanceControlledSemanticDrawFrame` now owns remaining-count and deferred-identity advancement across after-shuffle insertion, Stratagem selection, nested `CardDrawn` suspension, and nested-effect completion. Callers retain only boundary-specific progress such as the Stratagem condition and continuation attachment. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared DrawCards result interpreter increment (2026-10-18):** Semantic draw-session results and nested effect-draw results now dispatch through `ResolveControlledSemanticDrawResult`, including boundary-specific suspension, effect continuation attachment, deferred keyword application, and completion. The ordinary automatic pipeline also sends its semantic after-shuffle, hook-choice, and Stratagem results through that interpreter; initial semantic draw/cost cursor creation stays with the pipeline. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared initial semantic hand-draw checkpoint increment (2026-10-18):** The ordinary `DrawPlayerHand` stage now sends pending semantic draw and draw-cost results through `ResolveControlledSemanticDrawResult`. `SaveAutomaticSemanticHandDrawBoundary` owns the requested/remaining pipeline cursor for that first suspension, removing the stage's separate cursor-writing branch. Legacy controlled cost and recycle progress retain their provider-specific policy. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared initial controlled hand-draw checkpoint increment (2026-10-18):** Initial legacy controlled draw-cost and recycle boundaries now seed the existing DrawPlayerHand continuation and use `ContinueControlledHandDraw` for cursor state and boundary interpretation. This removes a second initial-stage implementation of those legacy progress transitions while preserving Native semantic-deck and unguarded Stratagem paths. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared automatic draw-cost outcome increment (2026-10-18):** Legacy controlled automatic hand draws now reuse `ResolveDrawCostAndDispatchCardDrawn` for bounds, temporary cost application, Relic-to-Slither progression, and the single `CardDrawn` dispatch. The legacy adapter retains its existing rejection of nested hook choices and resumes the remaining hand draw through its pipeline continuation. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 shared hand-draw post-stage mechanics increment (2026-10-18):** Initial source-stage and suspended automatic hand draws now apply remove-after-draw and decrement-after-draw power timing through `ApplyAutomaticHandDrawPostStagePowerTiming`. Their pipeline cursor transitions remain at their respective boundaries. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 semantic automatic hand-draw completion merge (2026-10-18):** A clean semantic automatic hand-draw source step now defers its post-stage cleanup and cursor advance to `CompleteAutomaticHandDrawStage`, the same owner used after controlled suspensions. The compatibility adapter receives its requested-count receipt and clears that transient projection before publishing the completed frame. Suspended semantic boundaries still retain their existing continuation. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S3 explicit combat-zone identity delta increment (2026-10-18):** `ApplyZoneTransition` now requires all physical card identities to remain present exactly once while forbidding DrawPile edits. Intentional non-draw additions/removals use `ApplyZoneIdentityDelta`; non-draw transforms use `ApplyZoneIdentityReplacement`, which validates that the instance stays in the same zone and ordered slot. Generic `SetZone` declares the identity delta it publishes, and generated-choice cleanup and enemy theft use explicit removal transitions. Typed draw-pile insertion sequences validate their resulting zone identities against the inserted identities and any declared removals, including generated-choice cleanup that moves selected cards to DrawPile. This closes the silent-add/drop path in the existing zone transition adapters; card-record publication and broader typed draw-pile delta checks remain separate work. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S3 provider-committed zone identity validation increment (2026-10-18):** `ApplyCommittedZoneTransition` now requires the complete physical identity set to be preserved. Typed draw-pile removal checks all six zones and accepts a separately declared set only for cards intentionally removed from combat, as in Hive Swipe theft; other removals must move the identities into their destination zones. Draw-pile replacement validates the exact old-to-new identity delta, and insertion sequences commit only after their declared identity checks. Fixture initialization remains the explicit boundary for seeding a zone owner. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 automatic hand-draw checkpoint writer increment (2026-10-18):** `PrototypeAutomaticHandDrawProgressV111` now validates and constructs each supported suspension mode, while `WithHandDrawProgress` replaces the progress discriminant in one pipeline-state update. Legacy controlled recycle/cost boundaries and semantic draw/pre-draw-hook boundaries use that writer; the completed semantic requested-count receipt remains a transient compatibility projection. This consolidates persisted cursor writes without merging provider-specific resume policies. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes, and no tests were run.

**S4 source-committed hand-draw completion increment (2026-10-18):** Semantic completion from an already-committed opening order now enters `CompleteAutomaticHandDrawStage`, which owns post-draw power timing and pipeline advancement for controlled and semantic draws. This removes a direct cursor advance that bypassed the shared completion mechanics. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes (140 registered sites mapped, 183/183 S0 locator matches); no tests were run.

**S3 paired top-card provider/zone commit increment (2026-10-18):** `CommitResolvedTopCards` replays a saved sequence of known or pending top-card resolutions from its synchronized source checkpoint, validates the resulting physical draw pile, and commits provider state with the destination-zone change. Ordinary draws and top autoplay now use this adapter path. Top autoplay also publishes staged play identities before recycling so the recycle commit preserves every physical identity; legacy autoplay uses the same staged-zone checkpoint without a semantic deck. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes (140 registered sites mapped, 183/183 S0 locator matches); no tests were run.

**S3 draw-hook zone refresh increment (2026-10-18):** After each ordinary `CardDrawn` hook, `DrawCards` now validates the committed provider/pile correspondence and reloads its local draw/discard working lists from the authoritative combat zone owner before continuing. This keeps deterministic hook zone changes in the same draw iteration and prevents stale local pile snapshots from overriding the hook result. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes (140 registered sites mapped, 183/183 S0 locator matches); no tests were run.

**S3 authoritative recycle checkpoint increment (2026-10-18):** Nature, Native semantic, and legacy recycle operations now derive the exhausted draw pile, discard identities, and provider deck directly from `CombatState`. Their callers no longer pass parallel pile snapshots or provider state that could overwrite the committed zone owner. This makes recycle shuffle, provider epoch, and zone transfer one source-boundary transition. The Release solution build succeeds with no warnings or errors; the direct-call inventory check passes (140 registered sites mapped, 183/183 S0 locator matches); no tests were run.

**S3 hand-return shuffle paired-commit increment (2026-10-18):** `SemanticDrawPileAdapterV111.CommitHandReturnShuffle` validates that returned identities match the committed hand, applies the resolved shuffle order to the Native or Nature semantic deck, and publishes the new draw pile and empty hand together. Ordinary combat effects and the controlled pool-shuffle continuation share this operation; their duplicate provider-update and zone-commit steps are removed. The caller retains shuffle resolution and after-shuffle effects.

**S3 recycle provider/zone commit increment (2026-10-18):** `SemanticDrawPileAdapterV111.CommitNativeRecycle` and `CommitLegacyRecycle` derive recycled identities from the committed discard zone, validate the resolved order, and publish the destination draw/discard zones with the provider epoch where applicable. Ordinary draw, top-autoplay and controlled recycle callers retain shuffle sampling and source hooks but no longer assemble separate discard-to-draw transitions. Nature recycle was already committed through the adapter.

**S3 semantic resolution commit increment (2026-10-18):** Specialized pending semantic draws and generated draw insertion now publish their resolved provider deck through `SemanticDrawPileAdapterV111.ApplyCommittedPhysicalMutation`, which validates the deck against the current physical draw pile before returning the combat state. This removes three direct runtime provider assignments outside the adapter; provider initialization and fixture setup remain separate.

**S3 semantic deck initialization increment (2026-10-18):** `SemanticDrawPileAdapterV111.InitializeSemanticDrawPile` now attaches initial Nature and Native provider states only after validating draw-pile membership and, for Native, exact top order. Semantic draw, pilot, source-route and Native policy setup use the adapter instead of publishing an unchecked provider assignment. Forking an existing provider state continues through the existing combat fork operation.

**S6 merchant missing-bag Nature projection increment (2026-10-18):** The configured merchant ReferenceNature session now initializes canonical eligible rarity sets when its source snapshot has no persistent relic bag, matching the existing Treasure projection. Existing Native bag queues are still canonicalized without retaining their order. Native bag construction remains outside the session, and eligible-pool completeness remains unverified.

**S7 Fishing Rod index-law increment (2026-10-18):** The persistent combat-end upgrade now routes its Native index request through the shared provider scope, and the direct Chance Law query exposes exact `run.niche` `NextInt` masses for a caller-declared support size. Trigger timing, eligible-card support, owner calibration, and an atomic saved victory continuation remain open; the Release solution build succeeds with no warnings or errors, the direct-call inventory passes, and no tests were run.

**S6 Fishing Rod victory replay increment (2026-10-18):** The existing `ReferenceNatureNativeEventShuffleSessionV111` now admits a configured ordinary-combat action only when it wins immediately and triggers at least one Fishing Rod draw. It captures every `run.niche` request, dynamically recaptures later supports after earlier upgrades, replays the ordinary victory action with scripted choices, and commits the mechanics with the Nature cursor while preserving the Native cursor. Draw-free victories, event combats, legacy runs, extra unexplained `run.niche` calls, trigger/support completeness, source-owner calibration, and Native cursor parity remain open.

**S6 Improvement victory replay increment (2026-10-18):** The same saved victory session now admits a legal combat action that immediately wins with Mad Science Improvement picks. It scripts each legacy `combat` index, recomputes the without-replacement support after each selection, and commits the ordinary upgrade hook with the Nature cursor while preserving the legacy stream cursor. Its request-index law remains `UNVERIFIED`; source-native assignment, independent pool fidelity, wins with additional unexplained `combat` requests, and broader S4 equivalence remain open. The Release solution build succeeds with no warnings or errors; no tests were run.

**S6 Pael's Tooth victory replay increment (2026-10-18):** The existing saved victory session now admits a legal combat action that immediately wins and returns one card from each non-melted Tooth with a nonempty queue. It scripts `reward` indices in relic-slot order, validates queue ownership and stream ordinals, rejects unrelated `reward` requests before or between Tooth returns, and leaves later reward generation on its legacy provider. Each generic request-index law stays `UNVERIFIED`; Native stream ownership, source probability, and Native cursor parity remain open. The Release solution build succeeds with no warnings or errors; no tests were run.

**S6 Pael's Legion acquisition continuation increment (2026-10-18):** The existing ReferenceNature run-action and event-choice sessions now admit Pael's four-skin acquisition index on a legal Shop purchase or supported event reward. The direct and pending index laws remain `UNVERIFIED`; the emulator's relic-owned seed derivation is explicitly retained as an open source boundary. The Release solution build succeeds with no warnings or errors; no tests were run.

**S6 fixed Act 1 boss law-query increment (2026-10-18):** Read-only `chance_law_query` now exposes the Overgrowth and Underdocks source-ordered boss identities as equal-weight outcomes with fixed-pool conditioning fingerprints. The laws remain `UNVERIFIED`; Overgrowth keeps its existing legacy run-start continuation, configured Underdocks run creation remains source-owned, and no legacy seed or Native cursor calibration is claimed. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 145/145 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 Glory second-boss law-query increment (2026-10-18):** Read-only `chance_law_query` now returns the two source-ordered alternatives for Glory Ascension 10+ when the caller declares the already-selected first boss. The conditioning fingerprint also includes Ascension; the equal-index law remains `UNVERIFIED`, and run initialization remains source-owned. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 146/146 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 Hive/Glory initial boss law-query increment (2026-10-18):** Read-only `chance_law_query` now returns the source-ordered raw boss-pool index law for act 2 or 3. The query records the index draw before the first-undiscovered-boss preference and remains `UNVERIFIED`; encounter preparation remains source-owned. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 147/147 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 later-act encounter and Ancient law-query increment (2026-10-18):** Read-only `chance_law_query` now exposes the Hive/Glory raw boss-pool draw, Glory's conditional second-boss draw, the Hive Darv partition, Glory's conditionally bound-one partition draw, and each act's source-ordered local Ancient pool. Later-act boss queries accept caller-declared nonnegative `combat` stream ordinals after encounter-queue preparation; Darv and Ancient queries accept caller-declared `event` stream ordinals. The queries do not validate the complete run-start prefix or add an atomic initialization continuation; all laws remain `UNVERIFIED`. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 150/150 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 Glory map rest-count law increment (2026-10-18):** Read-only `chance_law_query` now returns exact Native `NextInt(2)` grid masses for the Glory map's rest quotas 5 and 6 at site-local ordinal zero. The law is `SOURCE_VERIFIED` under the uniform 53-bit input-grid model; map context derivation, complete generation order, seed frequencies, and cursor parity remain open. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 151/151 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 Hive/Glory raw path-start law increment (2026-10-18):** Read-only `chance_law_query` now returns the exact Native `NextInt(7)` grid masses for each raw Hive/Glory path-start draw, conditioned on act and caller-declared site-local ordinal. The law is `SOURCE_VERIFIED`; it describes the source column draw before the second-path retry and does not validate that prefix or add map continuation. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 152/152 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 Overgrowth raw path-start law increment (2026-10-18):** The read-only query now covers Act 1 Overgrowth's raw Native `NextInt(7)` path-start calls. It requires the source owner ID, including which path or path-two retry made the call, plus its site-local ordinal. The law is `SOURCE_VERIFIED` for the bounded transform and does not claim the accepted retry result or complete map replay. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 153/153 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 map path-delta swap-law increment (2026-10-18):** Read-only Native laws now expose individual Fisher–Yates indices for Act 1 Overgrowth and Hive/Glory path delta shuffles. The three-entry shuffle's first swap has bound 3 and its second has bound 2; Act 1 requests bind the source path/floor owner, while later-act requests bind the act and per-site ordinal. The laws are `SOURCE_VERIFIED` for each bounded transform and do not claim the completed permutation or map. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 155/155 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 map room-assignment swap-law increment (2026-10-18):** Read-only Native laws now expose exact per-swap bounded-index masses for Overgrowth and Hive/Glory room placement. Callers declare the current support size and site ordinal; Act 1 additionally binds the source room/attempt/pool owner, while later acts bind the act. This verifies the bounded transform without validating the current map-derived support or adding a map continuation. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 157/157 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 combat-gold law-query increment (2026-10-18):** Read-only `chance_law_query` now returns source-ordered inclusive gold-amount outcomes using exact Native bounded-index grid masses. The query conditions on room, Ascension, and the source gold proportion for normal Combat, reuses `PrototypeNativeCombatGoldReward.Bounds`, and rejects zero-proportion branches where no draw occurs. Reward-prefix validation, stream assignment, seed frequencies, and cursor parity remain open. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 158/158 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 combat potion-odds law-query increment (2026-10-18):** Read-only `chance_law_query` now counts exact source-rounded Native `NextFloat()` grid inputs below the configured combat potion threshold. The caller declares the persistent pity value, Elite bonus, non-forced branch, and Rewards-stream ordinal; White Beast Statue forced rewards are rejected because they bypass the draw. Reward-prefix validation, stream attribution, seed frequencies, and cursor parity remain open. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 159/159 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 card-rarity source-law increment (2026-10-18):** Read-only `chance_law_query` now accepts the source, current rarity offset, Ascension, owner, and stream ordinal for `card_rarity.roll.unit_float`. It reports the raw rolled rarity before available-pool fallback and counts exact Native `NextFloat()` grid masses; it does not validate the encounter reward prefix, source pool, stream assignment, or cursor parity.

**S6 combat card-upgrade law-query increment (2026-10-18):** Read-only `chance_law_query` now exposes the raw comparison flag for supported Native Silent reward cards, Act, and Ascension. It counts exact source-rounded `NextFloat() <= threshold` grid masses, including the single zero-valued grid input at a zero threshold. Card mutation, reward-prefix validation, stream assignment, and cursor parity remain open. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 160/160 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 PotionFactory source-law increment (2026-10-18):** The PotionFactory rarity query now also accepts a typed request on `run.combat_potion_generation`, `player.rewards`, or `player.shops`, binding the caller-declared owner and ordinal while counting the exact inclusive Native float thresholds. Merchant and Alchemical Coffer laws share the same threshold-weight helper. This adds a cursor-blind law without claiming source-prefix or stream parity. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 160/160 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 relic-bag rarity source-law increment (2026-10-18):** The raw `relic_grab_bag.rarity.unit_float` query now accepts a caller-declared `player.rewards` request for reward, Treasure, Shovel, event, and merchant contexts. It counts exact Native float-grid masses at the strict 0.50 and 0.83 thresholds without inspecting relic queue order or validating the earlier call prefix. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 160/160 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 encounter card-model bounded-index law increment (2026-10-18):** Read-only `chance_law_query` now reports exact Native bounded-index masses for `card_rarity.encounter_model.index_draw` over a caller-declared support size, owner, and `player.rewards` ordinal. The support's source-derived card identities, reward prefix, stream attribution, and cursor parity remain unvalidated. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 161/161 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 relic-bag queue-shuffle index law increment (2026-10-18):** Read-only `chance_law_query` now counts exact Native `NextInt(bound)` masses for each caller-declared `relic_grab_bag.queue_shuffle.swap_index` on `run.treasure_room_relics`, binding the rarity-queue owner and local swap ordinal. It verifies one bounded transform and leaves eligibility, preceding swaps, full queue identity, and cursor parity open. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 161/161 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 reward relic-offer shuffle law increment (2026-10-18):** Boss relic rewards and Lava Rock extra-relic offers now expose their per-swap Native `NextInt` laws on the persistent `player.rewards` stream, using a shared bounded-index request helper. Callers declare each current swap bound, owner, and ordinal. The laws do not validate relic-pool eligibility, the reward prefix, legacy draws, or cursor parity. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 163/163 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 draw-pile shuffle transform law increment (2026-10-18):** Read-only `chance_law_query` now counts exact Native `run.shuffle` bounded-index masses for one caller-declared `combat.draw_pile.shuffle.swap` bound, owner, and ordinal. It verifies the primitive transform without exposing the pile order or adding a combat continuation; call-prefix, legacy-route, and cursor parity remain open. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 163/163 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 Native bounded-index query dispatch consolidation (2026-10-18):** The four Native per-swap queries for relic-bag queue construction, boss relic offers, Lava Rock offers, and ordinary draw-pile shuffles now share one allowlisted site-to-stream policy and one CLI request path. Site-specific law wrappers remain available to core callers, while the shared path owns positive-bound/owner validation and the common Chance Law response envelope. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 163/163 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 Native recycle-shuffle index law increment (2026-10-18):** The same read-only bounded-index query now covers `combat.draw_pile_recycle.swap_index` on Native `run.shuffle`, using the explicit `player:0` owner from the saved recycle request. It reports exact per-bound Native masses without checking the discard order, earlier draw prefix, or cursor. Legacy recycle requests and seeded parity remain unverified. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 164/164 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S6 Native insertion-index law family (2026-10-18):** The shared Native `run.shuffle` bounded-index query now covers ordinary generated-card insertion, after-shuffle relic insertion, enemy draw/discard insertion, and Personal Hive Dazed insertion. Callers declare each source-specific owner, current pile-position count, and request ordinal. The laws cover the exact bounded transform only; stream calibration, preceding operation prefixes, legacy request laws, and cursor parity remain open. The Release solution build passed with 0 warnings and errors; the direct-call inventory maps 165/165 registered sites with no missing or duplicate matrix rows, and the S0 locator matches all five facets for 183/183 rows. No tests were run.

**S0 bidirectional registry-ledger census (2026-10-18):** `tools/rng_callsite_inventory.py` now reports both registry IDs missing migration rows and migration rows absent from the active registry, with an informational law-status breakdown for matrix-only rows. It keeps legacy and conditional rows visible for disposition without treating them as registry failures. The current census maps 165/165 registered sites, reports 18 matrix-only rows, no unresolved IDs or duplicate rows, and all five heuristic facets for 183/183 rows; manual review remains required. No tests were run.

**S0 composed-registry locator correction (2026-10-18):** The inventory resolver now follows expression-bodied registry properties that clone private site arrays or expose dictionary keys, and resolves qualified constants across partial-class files. The regenerated v10 census maps 183/183 registered sites to migration rows, with zero registry gaps, matrix-only rows, unresolved references, or duplicate IDs; all five heuristic facets match for 183/183 rows. This is a corrected locator count, not independent source-completeness evidence, and every row still requires manual review. No tests were run.

**S4 suspended-intent checkpoint correction (2026-10-18):** The automatic-pipeline validator now compares its suspended combat with `ControlledTurnIntents.Combat`, which includes source-ordered intents already committed before the pending ticket. `ControlledTurnIntents.Validate` independently replays that prefix from `BaselineCombat`. Fixed seeded semantic-combat entry also writes its explicit encounter ID into the active run's encounter history. Release build passed; no tests were added or run.

**S3 legacy known-draw adapter increment (2026-10-18):** Legacy ordinary draws and top-card autoplay staging now commit known DrawPile removals through `ApplyDrawPileRemoval`, including destination hand/play zones. This closes the remaining runtime callers that changed DrawPile through the generic committed-zone method; other generic committed-zone call sites either initialize semantic opening state or replay a typed semantic transition. Release solution build succeeded with 0 warnings and errors; `git diff --check` and source review passed. No tests were added or run.

**S3 provider-neutral top-card commit increment (2026-10-18):** `ResolveTopCard` and `CommitTopCardResolutions` now cover Native, Nature, and legacy draws. Ordinary draws and top autoplay share the same top identity resolution and replay-checked multi-zone commit; the legacy path no longer selects or removes the top card independently in `PrototypeCombat`. Release solution build succeeded with 0 warnings and errors; source inventory confirms no remaining direct `draw.RemoveAt` top-draw path. No tests were added or run.

**S4 mixed primitive provider-scope increment (2026-10-18):** `IRandomBoundaryProviderV111` composes the six typed random primitive providers, and `StochasticProviderScopeV111.EnterBoundary` installs one mixed provider across the ordinary call graph. The Native Act 1 Overgrowth and later-act map generators now route their explicit index and Gaussian-integer requests through that scope, so a captured mixed transcript can replay their mechanics without bypassing provider selection. Filtered continuation scopes remain limited to their versioned index/unit-float request types. Release solution build succeeded with 0 warnings and errors; source review found no remaining direct Native map provider calls at those sites. No tests were added or run.

**S4 explicit-provider adapter coverage increment (2026-10-18):** Source-side explicit index requests across combat helpers, Native initial/hatch/spawn HP, legacy and Native encounter formation, Native event offers, and semantic pilot setup now resolve through `StochasticProviderScopeV111`, preserving each local provider by default while allowing an enclosing boundary provider to replace it. Combat adapters deriving ordinals from Native cursor state now use the scope's logical ordinal under counterfactual replay, so a non-advancing scripted provider does not repeat request identities. Replay validators retain direct Native calls. Cross-provider equivalence and full mixed-primitive continuation serialization remain open. Release solution build and `git diff --check` passed; no tests were added or run.

**S4 nested legacy draw-hook continuation increment (2026-10-18):** Controlled card-effect draws without a semantic deck now let a `CardDrawn` hook's pending player choice suspend as the existing `NestedCardDrawnChoice` result. The ordinary outer choice continuation retains the remaining draw count, already drawn identities, and the effect suffix, then resumes through the existing `DrawCards` path after selection. This closes the legacy effect-draw hook-choice rejection without adding a parallel continuation frame; automatic hand-draw and cross-provider equivalence remain open. Release build and `git diff --check` passed; no tests were added or run.

**S4 automatic legacy hand-draw hook-choice increment (2026-10-18):** Controlled legacy `DrawPlayerHand` now saves nested `CardDrawn` hook choices as a tagged pipeline progress mode with the exact remaining draw count. Resolving the hook and any nested choices resumes `DrawCards` before the stage applies post-draw power timing or advances the pipeline. Initial, recycled, Stratagem, after-shuffle-insertion, and draw-cost resume paths retain hook suspension. Native reference-mod fidelity and cross-provider mechanical equivalence remain open. Release build and `git diff --check` passed; no tests were added or run.

**S4 legacy BeforeHandDraw choice-resume increment (2026-10-18):** Controlled legacy turns can now suspend when `BeforeHandDraw` opens a player choice. A dedicated pipeline progress mode resumes at `DrawPlayerHand` after the saved event continuation settles, so the event does not replay and the automatic hand draw still occurs. Semantic mode retains its existing pre-draw marker. Native reference-mod fidelity and cross-provider mechanical equivalence remain open. Release build and `git diff --check` passed; no tests were added or run.

**S4 nested semantic draw-hook resume increment (2026-10-18):** After an after-shuffle insertion choice, resumed nested semantic effect draws now keep `CardDrawn` hook-choice suspension enabled. Any next hook boundary is returned through the tagged draw result and attached to the existing outer effect continuation. Release solution build passed with 0 warnings and errors; no tests were run.

**S4 all-primitive mixed replay increment (2026-10-18):** The shared typed replay provider and `ResumeStochastic` executor now cover index, unit-float, ranged-float, ranged-double, Gaussian-integer, and Gaussian-double operations. A versioned v2 continuation envelope can persist all six request/outcome types while v1 remains limited to index and unit-float fields. Request filters now apply to every primitive. Existing specialized sessions still need migration to persist v2 continuations and to declare Chance Laws for their supported sites. Core Release build passed with 0 warnings and errors; no tests were run.

**S4 versioned mixed-continuation executor increment (2026-10-18):** The shared executor now captures a versioned pending-request envelope and resolves a selected typed outcome only after exact request and candidate-label validation. The existing `ResumeStochastic` shape remains a compatibility wrapper for specialized adapters; new continuation owners can use the v1 or v2 envelope directly. Specialized session persistence migration and Chance Law coverage remain open. Release solution build passed with 0 warnings and errors; `git diff --check` passed; no tests were run.

**S4 merchant and Treasure continuation migration increment (2026-10-18):** The configured Native merchant and Treasure ReferenceNature sessions now reconstruct their existing pending v1 frame as `ControlledStochasticContinuationStateV111` and advance each sampled outcome through `ResolveStochastic`. Their established snapshot fields and session schema remain unchanged; the common resolver now validates request identity, support labels, and saved-prefix replay before publishing the next request or completed run. Release solution build passed with 0 warnings and errors; `git diff --check` passed; no tests were run.

**S4 continuation-owner consolidation increment (2026-10-18):** `ControlledChanceContinuationStateV111`, the existing index-only owner used by controlled event, run-action, and enemy-action frames, is now a compatibility projection over `ControlledStochasticContinuationStateV111`. The common typed envelope owns the provider prefix and pending request, while existing index and candidate-label properties and flattened snapshot schemas remain available. The solution Release build passed with 0 warnings and errors; `git diff --check` passed; no tests were run.

**S4 Native event-gold continuation migration increment (2026-10-18):** Configured Native event-entry ReferenceNature resolution now advances its saved v1 prefix through `ResolveStochastic`, validating the exact request and retaining the event provider-mode completion marker. Its existing law transcript and snapshot schema remain unchanged. The Release solution build passed with 0 warnings and errors; `git diff --check` passed; no tests were run.

**S6 legacy merchant request-law increment (2026-10-18):** Read-only Chance Law queries now expose the retained legacy Native-merchant card, potion, and relic price-index requests and the 10,000-way no-effect upgrade ticket. The source-shaped supports are validated, while equal request-index weights remain `UNVERIFIED`; the legacy fallback still has no atomic ReferenceNature continuation and remains fail-closed in sessions. Release solution build passed with 0 warnings and errors; `git diff --check` passed; no tests were run.

**S4 direct typed-result migration increment (2026-10-18):** Merchant, Treasure, and configured Native event-gold ReferenceNature replay helpers now use `CaptureStochastic` with their v1 schema and consume `ControlledStochasticContinuationResultV111` directly. The obsolete compatibility wrapper that converted typed continuations into pending exceptions was removed; session snapshot fields and boundary policies remain unchanged. Release solution build passed with 0 warnings and errors; `git diff --check` passed; no tests were run.

**S4 Native-regional legacy merchant continuation increment (2026-10-21):** `ReferenceNatureMerchantSessionV111` now accepts a legal Native-regional Shop entry when `NativeRunRng` is absent and replays the registered legacy sale-slot, card, Colorless-card, weighted-potion, relic-rarity/identity, price, and no-effect upgrade requests as one atomic Shop setup. Nature relic identity uses the canonical unordered bag support; fallback request-index weights remain `UNVERIFIED`. Unsupported stochastic Shop hooks remain fail-closed. Static source review and `git diff --check` only; no tests or builds were run.

**S6 legacy rarity-ticket law increment (2026-10-18):** Read-only Chance Law queries now expose categorical outcomes for the retained legacy card-rarity 10,000-ticket draw and potion/relic 100-ticket draws. Card weights use the declared source, offset, and Ascension thresholds; potion and relic laws bind their legacy stream/owner context, including the relic merchant suffix. All remain `UNVERIFIED` against Native RNG behavior and add no ReferenceNature continuation. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy card-upgrade ticket law increment (2026-10-18):** Read-only Chance Law now exposes the retained `card_rarity.upgrade_roll.ticket` pass/fail outcome weights for a declared card, Act, Ascension, and Rewards ordinal. It preserves the integer ticket's strict comparison, including a deterministic no-upgrade result at threshold zero; weights remain `UNVERIFIED` against Native RNG behavior and no card mutation or ReferenceNature continuation is added. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy reward-card rarity law increment (2026-10-18):** Read-only Chance Law now returns the rarity-category weights for `reward.card_rarity.ticket` after removing tiers absent from the remaining prototype reward pool. Callers declare Act, reward-group owner, Rewards ordinal, and all prior selected card IDs; the fingerprinted request owner matches reward generation. Pool fidelity and equal ticket-index weighting remain `UNVERIFIED`; the existing saved event-effect continuation continues to consume typed ticket indices. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy combat-potion drop law increment (2026-10-18):** Read-only Chance Law now exposes offered/not-offered outcomes for the legacy two-way `reward.random_potion.drop.ticket`. The caller declares act, floor, Combat/Elite/Boss room, potion-acquisition eligibility, and Rewards ordinal; no-ticket branches are rejected. Equal ticket weights remain `UNVERIFIED`, and combat reward generation remains source-owned. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy reward-potion model law increment (2026-10-18):** Read-only Chance Law now returns the fixed ordered prototype potion pool for `reward.random_potion.model`, conditioned on declared act, floor, Combat/Elite/Boss room, available potion capacity, successful prior drop, and Rewards ordinal. It binds the same pool fingerprint into the request owner; pool completeness and equal-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy potion-pity law increment (2026-10-18):** Read-only Chance Law now exposes offered/not-offered weights for `reward.potion_odds.ticket` from the declared current pity value and Elite bonus over the 1,000-way integer ticket. Forced rewards are rejected because they consume no ticket and do not update pity; legacy weights remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy Uncommon-potion model law increment (2026-10-18):** Read-only Chance Law now exposes the ordered implemented Uncommon-potion pool at `potion.uncommon_model.index_draw`, conditioned on caller-declared event/reward stream, owner prefix, and ordinal. The request binds the same pool fingerprint used by the legacy owner; source-pool completeness and equal-index weighting remain `UNVERIFIED`, and the existing event-choice continuation is unchanged. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy potion-by-rarity model law increment (2026-10-18):** Read-only Chance Law now returns the remaining source-ordered candidates for `potion.model_within_rarity.index_draw`, conditioned on rarity, excluded potion IDs, stream, owner prefix, and ordinal. Its owner suffix fingerprints the same remaining pool used by legacy generation; candidate completeness and equal-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy uniform-potion model law increment (2026-10-18):** Read-only Chance Law now exposes the full ordered potion pool at `potion.uniform_model.index_draw` for a caller-declared event/reward stream, owner prefix, and ordinal. The request owner includes the exact pool fingerprint; source-pool completeness and equal-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy event-potion model law increment (2026-10-18):** Read-only Chance Law now exposes the ordered prototype pool at the generic legacy `event.reward.random_potion.model` site, conditioned on event ID, selected choice ID, and event-stream ordinal. Wellspring and Whispering Hollow's Native-shaped branches remain separate; pool fidelity and equal-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy event Rare-card law increment (2026-10-18):** Read-only Chance Law now exposes the ordered prototype Rare-card reward pool for `event.reward.random_rare_card.model`, conditioned on event ID, selected choice ID, and Rewards ordinal. It remains `UNVERIFIED` because the emulator pool is not the Native character pool; the existing event-effect continuation is unchanged. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy event potion-option law increment (2026-10-18):** Read-only Chance Law now returns the ordered prototype potion pool for `event.reward.offer_potion.model.index_draw`, conditioned on a declared custom reward owner and available potion capacity. The query binds the legacy owner suffix and rejects the no-capacity branch where the source skips the draw; pool fidelity and equal-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy Elite relic model law increment (2026-10-18):** Read-only Chance Law now returns the fixed ordered prototype relic pool for `reward.elite_relic.model`, conditioned on declared Act, floor, and Rewards ordinal. Configured Native runs use the relic grab bag; source-pool fidelity and parity remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy reward card-model law increment (2026-10-18):** Read-only Chance Law now returns the remaining source-ordered prototype card identities for `reward.card_model.index_draw`, conditioned on Act, reward-group owner, selected rarity, prior card IDs, and Rewards ordinal. The owner fingerprint matches the legacy generator; pool fidelity and equal-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy modifier-curse model law increment (2026-10-18):** Read-only Chance Law now returns the implemented source-ordered modifier-curse pool for `event.reward.random_modifier_curse.index_draw`, conditioned on event ID and Rewards ordinal. The incomplete source reward audit and Native parity remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy reward card-transform law increment (2026-10-18):** Read-only Chance Law now returns the remaining ordered prototype reward pool for `reward.card_transform.replacement.index_draw`, conditioned on card instance, original model, and Rewards ordinal. It reconstructs the same owner fingerprint as persistent-deck transformation; source-pool fidelity and equal-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy event potion-fill law increment (2026-10-18):** Read-only Chance Law now returns the ordered prototype potion pool for `event.potion.fill_empty_slots.model`, conditioned on slot index, available potion capacity, empty-slot status, and Event ordinal. The query rejects branches where the source skips the draw; pool fidelity and equal-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy Shop card-law increment (2026-10-18):** Read-only Chance Law now returns filtered Act rarity weights at `shop.card_rarity.ticket` and remaining source-ordered card identities at `shop.card_model.index_draw`, conditioned on card type, prior offers within that type group, selected rarity, and Shop ordinal. Shared pool validation keeps both owners aligned with the legacy generator; pool fidelity and equal request-index weighting remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy Shop price-law increment (2026-10-18):** Read-only Chance Law now returns the rarity-specific card price outcomes and inclusive 40–60 potion and 100–140 relic price outcomes for legacy Shop requests. Price ranges are shared with generation and owners bind the card or declared Act/floor/offer/model tuple; equal request-index weights remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy bounded-index query increment (2026-10-18):** Read-only Chance Law now covers allowlisted legacy combat, event, map, relic, reward, and Shop index/shuffle sites through the shared request law. Each query requires the source stream/operation from the registry plus a caller-declared owner, ordinal, and current support; fixed map ticket/key supports are enforced. Pool construction, queue history, prior shuffle prefix, and legacy equal-index weights remain open. Release solution build and `git diff --check` passed; no tests were run.

**S6 legacy Treasure relic-model law increment (2026-10-18):** Read-only Chance Law now returns the ordered prototype relic pool after removing caller-declared owned relics for `treasure.relic_model.index_draw`. The query binds Act, floor, and the remaining-pool fingerprint to the Rewards owner and rejects the exhausted-pool branch, which consumes no index draw. Configured Native relic-grab-bag selection stays on its separate route; source-pool fidelity and equal-index weights remain `UNVERIFIED`. Release solution build and `git diff --check` passed; no tests were run.

**S6 relic-acquisition model-law increment (2026-10-18):** Read-only Chance Law now returns the legacy prototype Rare-card and potion pools for the two random relic-acquisition model sites, deriving the same owners as the existing providers. Potion requests require the caller-declared first open slot. An explicit `player.rewards` route exposes exact Native bounded-index masses for a caller-declared owner and support, while source pool, stream attribution, cursor parity, and legacy equal-index weights remain open. Release solution build and `git diff --check` passed; no tests were run.

**S6 Run potion-slot-fill law increment (2026-10-18):** Read-only Chance Law now exposes the legacy ordered potion pool for each caller-declared empty slot at `run.potion.fill_empty_slots.model`, conditioned on available potion capacity and rejecting occupied-slot or no-capacity branches. It binds the source-shaped slot owner and pool fingerprint. An explicit `player.rewards` route exposes exact Native bounded-index masses for the declared support; pool completeness, trigger order, stream attribution, and legacy equal-index weights remain open. Core and CLI Release builds and `git diff --check` passed; no tests were run.

**S6 Pael's Tooth index-law increment (2026-10-18):** Read-only Chance Law now includes `relic.paels_tooth.card_return.index_draw` in the shared legacy bounded-index query registry with its fixed `reward` stream and index operation. Callers declare the current nonempty saved-card support and source-shaped owner used for each relic slot; empty queues remain draw-free. The existing combat-end continuation still scripts each triggered request. Saved-queue derivation and equal-index weights remain `UNVERIFIED`. Core and CLI Release builds and `git diff --check` passed; no tests were run.

**S6 unowned-relic selection law increment (2026-10-18):** Read-only Chance Law now accepts the current owner and eligible support for `relic.unowned_random_selection.index_draw` on legacy `event` or `reward` streams and the hypothesized Native `player.rewards` stream. Native bounds use exact source `NextInt` masses; pool filtering, legacy equal-index weights, stream attribution, and cursor parity remain open. Core and CLI Release builds and `git diff --check` passed; no tests were run.

**S7 Punch Off reward-law increment (2026-10-18):** The configured relic and potion reward laws now preserve their ordered identity labels while mapping exact source `NextInt` grid masses onto the declared `player.rewards` supports; the potion law remains conditioned on the preceding relic index. A direct legacy `reward` route accepts the current caller-declared support and owner for either site. The bounded transform is `SOURCE_VERIFIED`; Native stream attribution, source pool fidelity, legacy equal-index weights, and seed/cursor parity remain open. Core and CLI Release builds and `git diff --check` passed; no tests were run.

**S6 Unknown-room ticket-law increment (2026-10-18):** The legacy `event.unknown_room.odds.ticket` is now directly queryable through the shared bounded-index law with a fixed 1,000-ticket support and `event` stream. The query exposes ticket indices without asserting their room-outcome mapping or Native float fidelity; equal ticket weights and source-float parity remain `UNVERIFIED`. Core and CLI Release builds and `git diff --check` passed; no tests were run.

**S6 legacy event and summon index-law increment (2026-10-18):** The shared caller-bound law now includes `event.selection.weighted_ticket` and `combat.enemy.summon_pool.select`, fixing their `event` and `combat` streams and bounded-index operations. Callers declare the current ticket/support count and source-shaped owner; event weights, eligibility, summon-pool completeness, and legacy equal-index weights remain `UNVERIFIED`. Pael's Legion continues through its existing dedicated query, which fixes the four-skin support and validates the isolated relic-instance owner. Core and CLI Release builds and `git diff --check` passed; no tests were run.

**S6 Mad Science Improvement direct-law increment (2026-10-18):** The shared caller-bound law now covers `combat.glory_tinker.improvement_card.index_draw` when the caller explicitly supplies `stream_id: combat`, an owner, and current support. Omitting `stream_id` preserves the existing state-bound query and its victory-action request capture. Pool completeness, source-native stream assignment, and equal-index weights remain `UNVERIFIED`. Core and CLI Release builds and `git diff --check` passed; no tests were run.

**S6 Native map Gaussian-law increment (2026-10-18):** Read-only `gaussian_int` laws now expose the rounded and truncated normal model for the Overgrowth rest/unknown quotas and Hive/Glory rest/unknown quotas. Queries enforce the source-shaped map context stream, owner, and ordinal zero; the shared law-to-provider adapter maps selected integer outcomes to the existing controlled Gaussian-integer decision. The model is a continuous-normal approximation; finite Native-grid masses, call-order parity, and the legacy Box–Muller component path remain `UNVERIFIED`.

**S6 atomic map-generation continuation increment (2026-10-18):** `ReferenceNatureMapGenerationSessionV111` now replays the existing source-shaped Overgrowth, Hive, and Glory generators under a complete mixed typed provider prefix. It resolves each path-start, delta-shuffle, room-assignment, Glory rest-quota, and Gaussian request through its Chance Law and exposes the map only after generator replay, transcript, and Nature cursor validate together. JSONL supports begin/status/resolve/fork/save/load/release, and capability plus Nature-choice coverage includes the session. This is opt-in; ordinary Native map generation remains source-owned. Map geometry/source fidelity, Gaussian finite-grid masses, source call order, legacy Box–Muller parity, and Native cursor parity remain open. Core and CLI Release builds and `git diff --check` passed; no tests were run.

**S4 serializable scripted run-action controller increment (2026-10-18):** `ScriptedRunActionSessionV111` drives one current legal action through `PrototypeGameEngine.Step` under the shared six-primitive executor. It accepts ordered boundary transcript outcomes, validates each exact pending request and optional candidate labels, and persists the origin, action, typed prefix, pending v2 continuation, and completed run. JSONL exposes begin/status/resolve/fork/save/load/release and capability discovery. Scripted probability remains `UNVERIFIED`; Native cursor advancement and cross-provider equivalence remain open. Core and CLI Release builds passed with 0 warnings and errors; no tests were added or run.

**S4 whole-run scripted orchestration increment (2026-10-18):** `ScriptedRunSessionV111` now chains the existing per-action scripted controller across ordinary legal player actions. It keeps the run at the last committed action boundary during a pending typed random request, validates the active action against that boundary, and replays completed action records from the origin when validating or restoring. JSONL and the standard-library Python binding expose begin/status/action-begin/resolve/fork/save/load/release. Scripted probability remains `UNVERIFIED` and no Native RNG cursor is advanced or validated; cross-provider equivalence remains open. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 shared automatic hand-draw checkpoint commit increment (2026-10-18):** Legacy and semantic automatic hand-draw adapters now publish their tagged progress together with the current player and combat state through `CommitAutomaticHandDrawProgress`. The owner validates the source `DrawPlayerHand` cursor, initializes it for first suspension, and preserves the established flattened snapshot projections. Provider-specific choice histories and resume policies remain with their adapters; complete resume consolidation and cross-provider equivalence remain open. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 typed DrawCards suspension-policy increment (2026-10-18):** `DrawCards` now accepts one `PrototypeDrawCardsSuspensionPolicyV111` value instead of five independent booleans. Named profiles preserve the previous source automatic-hand-draw, controlled legacy continuation, semantic draw/effect, and mixed-provider nested-effect boundary coverage. The tagged result still owns the actual pending boundary, and provider-specific resume behavior remains in its adapters. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 generic typed continuation executor increment (2026-10-18):** The shared `CaptureTyped`/`ResolveTyped` executor now accepts immutable mechanics checkpoint and result types beyond `RunState`, while the existing RunState entry points remain compatibility wrappers. `ReferenceNatureMapGenerationSessionV111` now captures and resolves its mixed six-primitive map prefix through that common owner, retaining its existing session snapshot fields and full generator replay validation. Map source fidelity and cursor parity remain separate. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 Infested Automaton continuation migration increment (2026-10-18):** The one-draw Infested Automaton ReferenceNature session now replays its selected card-pool index through the shared `CaptureStochastic` executor with an exact request filter. The session keeps its existing saved decision transcript and independently checks that the Native event cursor remains unchanged before publishing the Nature cursor. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 Native event continuation migration increment (2026-10-18):** Tablet of Truth and Trial now replay their one-page typed index choices through the shared continuation executor. `ReferenceNatureNativeEventIndexReplayV111` owns exact request filtering, complete selected-prefix consumption, Native event cursor preservation, and the scripted provider marker for these event sessions. `FromOrderedIndexSelection` validates semantic card/case labels against the ordered source support before mapping a sampled outcome to its provider index; this also fixes Infested Automaton's nonnumeric card-label mapping. Existing snapshot and transcript schemas are unchanged. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 multi-pick Native event replay migration increment (2026-10-18):** Glory Reflections now replays its complete ordered card-index prefix through the shared continuation executor, retaining exact request consumption and Native event cursor checks. Endless Conveyor's single-index card-choice path uses the same filtered executor while preserving its selected-card mechanics audit; its mixed index/ticket path keeps its specialized pre-draw capture. Existing session snapshot fields are unchanged. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 Trash Heap replay migration increment (2026-10-18):** The one-draw Trash Heap session now maps its selected card or relic identity against the ordered Chance Law support and replays the ordinary choice through the shared filtered executor. The common event replay helper enforces complete request consumption and the unchanged Native event cursor while retaining the established event marker and snapshot schema. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 Neow generation replay migration increment (2026-10-18):** The Neow ReferenceNature session now routes the complete offer prefix through `CaptureTyped` while invoking the existing run-start and Neow offer generators. Its site allowlist retains boundary ownership; generator audit decisions are compared against the saved source requests, the Native event stream is checked, and the established scripted event marker is preserved. Existing session snapshot fields are unchanged. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 Punch Off continuation migration increment (2026-10-18):** Punch Off fight-entry and reward sessions now replay their selected index prefixes through the shared stochastic continuation executor. A boundary-owned codec-aware fallback preserves nonselected index draws, while CombatId request identity, reward order, and the existing run, reward-stream, and event-stream cursor audits remain enforced. Journal entries now identify the shared controlled provider mode. Session snapshot fields are unchanged. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 Native event-shuffle continuation migration increment (2026-10-18):** Standard event-shuffle, War Hammer, Fake Merchant, Battleworn, and Repy session replays now consume their selected index prefixes through the shared executor, retaining candidate identity probes, cursor/request audits, and codec-aware fallbacks. Repy's later native potion requests stay outside the selected prefix and retain their existing stream audit. Combat-end upgrades still use specialized providers to discover dynamic Native/legacy tails; exact selected-prefix replay is tracked in the increment below. Existing action plans and snapshot fields are unchanged. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 Endless Conveyor typed replay migration increment (2026-10-18):** Its one-ticket and mixed index-plus-ticket replays now consume typed index/unit-float decisions through the shared executor. The session retains its Native pre-draw probe, exact ticket request, selected dish/card checks, and unchanged event-cursor audit; unmatched provider calls retain codec-aware Native/legacy fallbacks. Existing session snapshot fields are unchanged. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 combat-end Native shuffle prefix migration increment (2026-10-18):** Fishing Rod, Mad Science Improvement, and Pael's Tooth exact selected index prefixes now replay through the shared typed continuation executor. Existing request validation, victory/reward checks, and Native or legacy stream cursor audits remain in place. The shared executor now provides a filtered index fallback for Native/legacy tail capture, discovering dynamically reached later requests before Nature commits each choice. Existing session snapshots are unchanged. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 Repy Native tail replay increment (2026-10-18):** War Historian Repy's later potion-index requests now run through the shared executor's filtered Native index fallback after the saved relic and first two potion decisions are consumed. Existing source request order, reward-stream cursor, and native-tail count audits remain unchanged; unrelated requests still use the codec-aware fallback. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 Ancient-offer continuation migration increment (2026-10-18):** Ancient offer and shuffle replay now routes the full typed index prefix through the common `CaptureStochastic` executor. A scoped adapter bridges the event generators’ explicit provider parameters into that shared replay while retaining their `scripted-v111` marker, exact request identity checks, complete-prefix consumption, audit-journal isolation, and existing session snapshots. Core and serial CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S2 native-policy mixed-trace increment (2026-10-18):** Native-policy audit state now retains the filtered chronological native-v111 transcript across index, unit-float, ranged-float, ranged-double, Gaussian-integer, and Gaussian-double boundaries from seeded setup, source-route initial intent, and subsequent player actions. Typed payloads preserve request/result and native before/after cursor data; the legacy index decision list remains a validated projection. `SemanticPilotStochasticStateV111` owns audit validation and snapshot forks deep-clone trace payloads; the fair player observation remains unchanged. Older snapshots without a mixed trace retain compatibility. Probability and parity status remain `UNVERIFIED`. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S5 capability-schema ownership increment (2026-10-18):** `StochasticCapabilitiesV111.Create` now sources every ReferenceNature session schema field from the validated `StochasticSessionCoverageV111` produced by `StochasticSiteRegistryV111`. The capability response retains its existing fields and operation list, while schema negotiation can no longer drift from session coverage through independently repeated constants. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 next-turn intent replay increment (2026-10-18):** `ControlledTurnIntentEngineV111` now reconstructs the pure selector pass from `BaselineCombat` inside `ControlledChanceContinuationExecutorV111`. The shared replay provider scope validates and consumes the complete typed index prefix; the existing intent adapter still captures the next pending request and returns the same flattened continuation state. Cross-provider mechanical equivalence and the remaining semantic draw/pilot resume-loop migrations remain open. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 next-turn intent resolution increment (2026-10-18):** Resolving a selected intent ticket now passes the saved pending request, prior outcomes, and selected index through `ResolveTyped`. The shared executor checks the selected outcome against that exact request, then rebuilds the intent pass and captures its next pending ticket; the facade keeps the established transcript and request fields. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed. No tests were added or run.

**S5 atomic capability and coverage response increment (2026-10-18):** The JSONL `hello` response now constructs `StochasticSessionCoverageV111` once and passes that validated object into capability-manifest creation before publishing both fields. This makes the session schema IDs negotiated in the manifest the same values emitted in coverage for that response. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed. No tests were added or run.

**S4 automatic enemy-action typed replay increment (2026-10-18):** The automatic pipeline now replays its saved enemy-action index prefix through `ControlledChanceContinuationExecutorV111.CaptureTyped`, filtered to the established enemy-action site allowlist. The executor's pending typed request is projected into the existing pipeline continuation, and source callsites now use the shared provider scope; the duplicate private enemy-action replay cursor and provider were removed. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed. No tests were added or run.

**S4 shared semantic draw-card commit increment (2026-10-18):** `SemanticDrawChoiceV111` now validates the selected draw-card identity against the live request and commits provider deck state with the matching physical draw-pile update. Standalone semantic draw sessions and nested card-effect draws use the same owner, while retaining their own post-commit draw and effect continuations. Core and CLI Release builds passed with 0 warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 shared semantic draw-cost continuation increment (2026-10-18):** `ContinueSemanticDrawCost` now owns post-cost hook suspension, resumed `DrawCards` setup, and tagged result interpretation for both automatic/standalone semantic draws and nested effect draws. Boundary adapters retain validation, their saved frame, and whether completion re-enters the automatic pipeline. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic draw-hook resume increment (2026-10-18):** `PrototypeGameEngine.ResumeSemanticDrawHookChoice` now interprets the resolved hook action for standalone and automatic semantic draws, including resuming `BeforeHandDraw`, preserving a suspended automatic cursor, and continuing remaining draws. The draw facade retains the saved typed decision transcript. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic Stratagem resume increment (2026-10-18):** `PrototypeGameEngine.ResumeSemanticStratagemSelection` now validates and replays the selected source action against the saved Stratagem event, then continues the automatic or standalone semantic draw through shared engine draw progress. The facade retains the typed decision transcript. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic draw-insertion resume increment (2026-10-18):** `SemanticDrawInsertionChoiceV111` now owns ordered position selection and guarded resumption of the ordinary insertion mechanics for semantic draw and pilot facades. Their insertion-stage invariants and transcript ownership remain local. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic insertion commit ownership increment (2026-10-18):** `ResumeControlledDrawInsertion` already commits the selected physical index and provider position together through `ApplyDrawPileInsertion`. `SemanticPilotCombatV111` now treats that engine result as authoritative and keeps only boundary validation and decision history, removing its second provider-state publication from the source checkpoint. This prevents the facade write from replacing provider progress made by the resumed continuation. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 random hand-cost continuation consolidation increment (2026-10-18):** `SemanticRandomHandCostChoiceV111` now owns guarded resumption and the shared source-boundary, phase, and hidden draw-pile checks for random hand-cost target selection and cost selection. The semantic pilot keeps event validation and its typed history while using one engine continuation contract for both stages. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 generated-card continuation ownership increment (2026-10-18):** `SemanticGenerationChoiceV111` now validates ordered card-model selections and resumes both direct generation and random draw-Attack insertion through the ordinary guarded engine continuation. It owns source-boundary, resulting-phase, and hidden draw-pile checks; the semantic pilot retains event freshness and decision/reward history. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 potion-generation continuation ownership increment (2026-10-18):** `SemanticPotionGenerationChoiceV111` now owns typed event selection, source-pool revalidation, guarded engine resumption, supported-phase validation, and the hidden draw-pile invariant. The semantic pilot retains only boundary freshness, decision history, and session publication. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 card-transform continuation ownership increment (2026-10-18):** `SemanticCardTransformChoiceV111` now owns typed replacement selection, verifies the live source card and eligible replacement pool, and resumes the ordinary controlled transform mechanics. Combat and terminal phase checks stay with this choice owner, while the pilot retains event freshness and decision/reward history. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 card-shortlist continuation ownership increment (2026-10-18):** `SemanticCardShortlistChoiceV111` now validates stable card-instance selection, rechecks the live ordered support, and resumes selection, replay-card, or random-autoplay mechanics through one guarded path. It owns the result-boundary and hidden-deck checks, including the autoplay terminal branch; the pilot retains decision history and reward auditing. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 hand-cost selection continuation ownership increment (2026-10-18):** `SemanticHandCostSelectionChoiceV111` now owns stable card-instance selection, live hand-support and applied-cost validation, and the guarded ordinary random-cost resume. The pilot retains boundary freshness and decision history. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic deck-reveal choice ownership increment (2026-10-18):** `SemanticDeckRevealChoiceV111` now owns stable peek/scry selection, live-request revalidation, and atomic provider-deck/physical-pile commit through the shared draw-pile adapter. The pilot retains boundary freshness and decision history. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 pool-shuffle continuation ownership increment (2026-10-18):** `SemanticPoolShuffleChoiceV111` now owns bounded swap selection, verifies the live source pool and operation, and resumes the ordinary shuffle continuation. It also owns subsequent choice-boundary validation and the operation-specific hidden-deck checks; the pilot retains event freshness and decision history. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic draw identity-selection increment (2026-10-18):** `SemanticDrawChoiceV111` now owns stable card-instance selection and stale event-ID rejection alongside its existing ordered-index path and provider/physical-pile commit. The semantic pilot delegates selection to the shared owner before continuing its draw frame. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic cost-selection increment (2026-10-18):** `SemanticCostChoiceV111` now owns stale event-ID rejection, ordered cost support lookup, and typed decision construction for the pilot's cost resolver. Draw and random-hand continuations continue to own their distinct mechanics resumption. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic intent selection increment (2026-10-18):** `SemanticIntentChoiceV111` now maps stable intent IDs to their source ticket and deep-forks the weighted decision event; the pilot uses this owner before resuming its existing intent frame. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 random pile-card identity-selection increment (2026-10-18):** `SemanticRandomPileCardEngineV111` now owns stale event-ID validation and stable card-instance lookup for the Jewelled Mask random pile-card event. The pilot delegates selection and passes the typed result into its existing semantic draw continuation. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 shared effect-draw decision construction increment (2026-10-18):** The pilot's nested semantic effect-draw and effect-cost branches now construct their typed decisions through `SemanticDrawChoiceV111` and `SemanticCostChoiceV111`, matching the owners used by the standalone draw and stable-cost APIs. Mechanics resume and boundary-specific transcript publication remain in their existing engine and pilot continuations. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic decision validation ownership increment (2026-10-18):** `SemanticCostChoiceV111`, `SemanticDrawChoiceV111`, `SemanticIntentChoiceV111`, and `SemanticRandomPileCardEngineV111` now own exact pending-event and selected-outcome validation. The semantic pilot reconstructs its current boundary event, delegates typed decision checks to each choice owner, and no longer carries duplicate private validators. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic choice-boundary ownership increment (2026-10-18):** Source reachability and hidden-deck checks for target, generated-card, potion-generation, card-transform, draw-insertion, pool-shuffle, hand-cost, and card-shortlist continuations now live with their matching typed choice owners. The pilot's semantic-combat boundary is a dispatcher for those owners; nested effect-draw checks remain at their existing facade boundary. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 effect-draw boundary ownership increment (2026-10-18):** Nature-backed card-effect draw and draw-cost source-boundary validation now lives in `SemanticDrawChoiceV111` and `SemanticCostChoiceV111`. The pilot's dispatcher routes both pending effect stages to those owners, eliminating the remaining draw-choice-specific boundary checks from the pilot. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 shared semantic hand-draw replay increment (2026-10-18):** `SemanticEndTurnEngineV111.Restore` now delegates its nested hand-draw transcript replay to `SemanticCombatDrawEngineV111.Replay`, the same reducer used by standalone draw restoration. The end-turn owner retains the source-origin check and final automatic-pipeline handoff. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 shared Nature draw-pile boundary increment (2026-10-18):** `SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary` now owns provider validation and physical membership checks for idle or pending Nature draw-pile boundaries. Player-action, source card-selection, target, generation, potion, transform, insertion, shuffle, shortlist, hand-cost, semantic draw, and draw-cost owners delegate the repeated deck-to-zone invariant while keeping their own source support and pending-stage rules. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 shared semantic effect-draw continuation increment (2026-10-18):** `SemanticDrawChoiceV111` now owns the ordinary guarded engine resume for nested card-effect draws under an explicit caller policy for engine-owned prototype rewards, the supported post-resume boundary checks, and validation that the provider transcript records the resumed event and selected candidate index. Standalone draw and pilot direct/end-turn paths use this shared continuation and result check; their subsequent hooks and outer pipeline policies remain with their existing frames. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 shared nested effect-cost continuation increment (2026-10-18):** `SemanticCostChoiceV111` now owns the guarded resume and result-boundary validation for costs inside nested semantic card-effect draws. The standalone draw adapter and semantic pilot share that path while retaining their typed versus replay-index transcripts and their draw-count or outer-pipeline policies. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 nested effect-cost event ownership increment (2026-10-18):** `SemanticCostChoiceV111` now describes a pending nested effect-draw cost from its live mechanics boundary, including the current card identity, cost stage, bound, support fingerprint, and event identity. The pilot supplies its boundary fingerprint and transcript ordinal; the separate end-turn and random-hand cost origins keep their existing adapters. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S3 committed-opening deck transfer increment (2026-10-18):** `SemanticDrawPileAdapterV111.InitializeFromCommittedOpening` now owns the physical pile's bottom-first to semantic deck's top-first order mapping when an already-resolved opening order enters Nature mode. Semantic playable entry and both completed initial-intent paths use it, preserving the committed order without rerandomization. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S3 shared Nature recycle mechanics increment (2026-10-18):** `PrototypeGameEngine.CommitNatureDrawPileRecycle` now derives the Perfect Fit top identities once from the live discard zone and commits the requested physical recycle order through `SemanticDrawPileAdapterV111`. Ordinary draws, top-draw autoplay, and controlled recycle share this mechanics/provider boundary while retaining their caller-specific shuffle and suspension policies. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 semantic opening-intent completion increment (2026-10-18):** Source-route intent frames that are already complete at initialization and those completed by a Nature choice now share `CompleteNatureInitialCombatEntry`. It resumes the ordinary opening pipeline, validates the post-opening player boundary, and initializes the semantic deck from the already-committed physical order through the shared adapter. Native committed-intent entry keeps its existing provider path. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 shared post-resume choice validation increment (2026-10-18):** Nested semantic effect-draw and effect-cost continuations now use the semantic pilot's existing choice dispatcher to validate their resulting combat boundary. Their individual owners retain their selected-outcome transcript checks and reward/terminal phase policy. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S3 unchanged draw-pile invariant increment (2026-10-18):** `SemanticDrawPileAdapterV111.RequireUnchangedDrawPile` now owns the paired physical-order and provider-state check used after target, generated-card, potion-generation, random-hand-cost, and non-autoplay shortlist continuations. The owners retain their supported-phase rules, error context, and random-autoplay exception. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S5 Nature observation privacy increment (2026-10-18):** `SemanticNatureChoiceViewV111` now removes forbidden provider and current selected-result fields at the top-level event envelope as well as recursively, and drops named or structurally recognized raw run snapshots before constructing Nature metadata. View validation rejects any forbidden metadata that remains. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 mechanics replay diagnostic increment (2026-10-18):** JSONL now exposes `mechanics_compare`, accepting a validated run snapshot or semantic-pilot handle for each side, and returning provider-independent mechanics fingerprints and bounded JSON-pointer differences from `SimulationMechanicsProjectionEngineV111`. `hello` advertises the versioned comparison schema and accepted inputs. The diagnostic requires matched semantic outcomes and does not claim identical stochastic choices or Native fidelity. Core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 mechanics comparison continuation increment (2026-10-18):** The v3 mechanics projection retains typed automatic hand-draw progress, pending semantic draw progress, and pending draw-cost mechanics (card identity, remaining count, and cost stage). Candidate supports, random cost bounds, and provider decisions remain omitted, including when the draw frame is nested under an after-shuffle insertion. The comparison schema ID is versioned to v3; core and CLI Release builds and `git diff --check` passed; no tests were added or run.

**S4 mechanics comparison insertion-cursor increment (2026-10-18):** The v4 mechanics projection retains the provider-independent cursor for pending draw-pile insertion: operation, stage, selected card model, ordered after-shuffle relic progress, and Personal Hive damage continuation. It continues to omit candidate support, eligible draw-pile order, and request identity. Cross-provider mechanics equivalence remains open. Core and CLI Release builds passed with zero warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 mechanics comparison pool-shuffle increment (2026-10-18):** The v5 projection adds the pending full-pool shuffle operation and its completed-swap and selection-count progress while omitting the eligible support and partial permutation. This keeps saved mechanics progress comparable without copying provider outcomes into the mechanics view. Cross-provider mechanics equivalence remains open. Core and CLI Release builds passed with zero warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 mechanics comparison choice-cursor increment (2026-10-18):** The v6 projection retains the active mechanics operation and progress for pending random target, generated-card, potion-generation, card-transform, random-cost, and card-shortlist choices. It omits their random supports and selected outcomes. The comparison schema is versioned to v6; cross-provider mechanics equivalence remains open. Core and CLI Release builds passed with zero warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 public deck-knowledge comparison increment (2026-10-19):** The v7 provider-independent mechanics projection now includes the public draw-pile view after a completed peek or scry, so equal mechanics fingerprints cannot hide different revealed card identities. Hidden pool membership/order and unresolved reveal support remain excluded. The JSONL capability continues to advertise the versioned comparison schema. The Release solution build passed with zero warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 hidden draw-order projection increment (2026-10-19):** The v8 mechanics projection canonicalizes physical draw-pile membership before fingerprinting, removing Native's committed hidden order while retaining the card identities still in the zone and the separate public peek/scry view. The Release solution build passed with zero warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 pending deck-operation projection increment (2026-10-19):** The v9 mechanics projection now retains the purpose and top offset of a pending semantic deck request. This keeps a suspended draw/peek/scry operation visible as mechanics progress without including its candidate identities, pool membership, or selected outcome. The Release solution build passed with zero warnings and errors; `git diff --check` passed; no tests were added or run.

**S4 resumed semantic hand-draw completion increment (2026-10-19):** A completed semantic hand draw resumed after its `BeforeHandDraw` hook now returns through `CompleteAutomaticHandDrawStage`, sharing post-draw power timing, pipeline advancement, and the requested-count receipt with other controlled and semantic completion paths. The same owner now validates and clears the deferred completion cursor, removing the remaining duplicate boundary check and cursor write; provider-specific draw suspension and transcript rules remain with their existing adapters. No tests or builds were run.

**S0 S1-boundary manual review increment (2026-10-19):** The ownership inventory now records scoped implementation reviews for committed/next-turn enemy intents and deferred physical-card draws. The reviews traced source-shaped intent support capture, bounded ticket mapping, shared outcome commit, known-versus-unresolved deck positions, atomic deck/zone publication, and the ordinary draw/cost/hook sequence. Native fidelity, full source eligibility, original-game conditional probabilities, and the remaining migration rows stay open; no tests were added or run.

**S0 S4/S6 boundary manual review increment (2026-10-19):** The ownership inventory also records scoped reviews for random enemy targets and drawn-card energy costs. The target review follows live ordered formation revalidation into the ordinary controlled target continuation; the cost review follows card/stage/bound validation through cost application, one `CardDrawn` dispatch, and resumed `DrawCards`. Bounded-transform evidence does not establish source-pool completeness or Native stream/cursor parity; other rows remain to review. No tests were added or run.

**S0 encounter formation and weighted ticket review (2026-10-19):** Manually traced `combat.encounter.formation.index_draw` through the ordinary typed resolver and opt-in Act 1 Native formation wrappers, and `combat.encounter.weighted_selection.ticket` through the current eligibility filter, summed ticket bound, and ordered weight subtraction. The inventory records verified emulator behavior and separates it from source pool, resolver-call-order, conditional-law, stream, and cursor claims that remain unverified. The wider S0 row review remains open; no tests or builds were run.

**S0 enemy HP boundary review (2026-10-19):** The ownership inventory now distinguishes legacy `combat` HP requests from Native `run.niche` initial/summon HP and the separate in-place Tough Egg hatch. It records unique-HP support reconstruction, the isolated Native hatch scope, and remaining source-order, legacy-law, and continuation limits. The wider S0 review remains open; no tests or builds were run.

**S0 summon-pool continuation review (2026-10-19):** The same source pass now records the legacy Fabricator last-spawn exclusion, full-pool fallback, and saved enemy-action replay for `combat.enemy.summon_pool.select`. Native pooled summons remain explicitly fail-closed because their source RNG owner and continuation are not integrated; the legacy law is `UNVERIFIED`. No tests or builds were run.

**S0 Act 1 encounter-pool review (2026-10-19):** The ownership inventory now traces Overgrowth's run-start boss and weak/normal/elite bags plus the Underdocks boss initialization and weak/normal/elite selectors. It records the current refill, exclusion, fallback and fail-closed behavior, and distinguishes these legacy-provider paths from Native pre-generated queues and RNG parity. A stale Underdocks audit paragraph that contradicted the ten-entry normal bag refill is corrected. Encounter probabilities and Native ordering remain unverified; no tests or builds were run.

**S0 later-act encounter queue review (2026-10-19):** The ownership inventory now traces run-start preparation and later room-entry consumption for Hive and Glory weak, normal, elite, first-boss and conditional second-boss sites. It records the audited tag/refill rules, first-undiscovered-boss override, hidden bag ownership and no-draw queue consumption. The current route still uses bounded legacy `combat` requests instead of the Native shared `run.Rng.UpFront` `GrabBag` sequence; caller-supported laws do not validate a complete initialized history. Source-law calibration and full Native prefix/cursor parity remain open; no tests or builds were run.

**S0 weighted event-selection review (2026-10-19):** The ownership inventory now traces `event.selection.weighted_ticket` from route-specific eligibility and history filters through ordered weighted-ticket resolution, event-history commit, and post-selection event-context creation. It distinguishes the legacy Event-node continuation from configured event RNG state and records provisional later-act unit weights plus the unaudited Native selection pool/stream. The law remains a caller-bounded ticket-index model; no tests or builds were run.

**S0 map-generation continuation review (2026-10-19):** The ownership inventory now traces the five Overgrowth and six Hive/Glory map rows through their Native/legacy RNG adapters and the opt-in `ReferenceNatureMapGenerationSessionV111`. The map contract distinguishes Nature’s pending-law view and atomic typed-prefix replay from the ordinary source-owned Native generator, and records that replay validates emulator retries/supports rather than Native geometry or call order. Gaussian finite-grid masses, Native seed/cursor parity, and legacy prototype map rows remain open; no tests or builds were run.

**S0 combat reward-generation review (2026-10-19):** The ownership inventory now traces the combat-reward Native/legacy provider split through combat victory and reward construction, including potion pity, gold, PotionFactory choices, card rarity/model/upgrade progression, and relic offers. It records that the provider-backed branch also serves the opted-in Hive/Glory and active Golden Path profiles, whose broader room execution remains experimental. The pinned reward audits were corrected to reflect those callsites; no complete ReferenceNature reward-generation continuation, source-prefix validation, or Native cursor parity is claimed. No tests or builds were run.

**S6 configured combat reward continuation increment (2026-10-19):** `ReferenceNatureCombatRewardSessionV111` now replays a legal configured Native Combat, Elite, or Boss victory through the registered pity, gold, potion, card, relic, boss-offer, and Lava Rock request prefix, publishing the completed Reward with the Nature cursor only after replay validation. JSONL, Python, capability coverage, and the Nature choice view expose the forkable/savable session. Legacy reward generation, unregistered requests, Native relic-bag queue construction, source-pool completeness, stream attribution, seeded Native parity, and cursor parity remain open. No tests or builds were run.

**S3 canonical relic-projection validation increment (2026-10-19):** The shared Native ReferenceNature run projection now validates serialized origins and replay runs for Merchant, Treasure, combat-reward, Native event-gold, and Calling Bell event-choice sessions. Present relic bags must remain `NatureUnordered` and pass rarity, ownership, uniqueness, and canonical-sort validation; event-gold entry continues to allow a missing bag. This prevents restored Nature sessions from retaining Native queue order. No tests or builds were run.

**S6 configured Native event relic continuation expansion (2026-10-19):** `ReferenceNatureNativeEventRelicSessionV111` now handles supported configured Native event choices with direct `GainRandomRelic` effects as well as Toy Box's five Wax draws. It derives the expected transcript length from the chosen event effects, validates each ordered rarity/index pair against the canonical remaining bag, and publishes the event result and Nature cursor atomically. Choices with another fixed relic effect or a non-relic stochastic request fail closed. Source-pool fidelity, hidden queue identity probabilities, mixed stochastic event effects, and Native cursor parity remain open. No tests or builds were run.

**S6 Native Neow relic-pickup continuation increment (2026-10-19):** The existing Native event relic session now accepts a legal Neow's Bones relic pickup when it grants Large Capsule. It captures the two post-acquisition relic rarity/index pairs in source order and commits the acquired relic, its follow-up effects, and the Nature cursor atomically. Other Neow pickup effects and non-relic stochastic hooks remain fail-closed. Source-pool completeness, hidden queue identity, and Native cursor parity remain open. No tests or builds were run.

**S6 Battleworn setting-three relic reward continuation increment (2026-10-19):** The Native event relic session now accepts a legal action that immediately wins Battleworn Dummy setting three and reaches its relic reward draw. Unsupported earlier stochastic requests remain fail-closed, and an empty eligible relic bag records its rarity request without requiring a model-index request. Python exposes `State.begin_reference_nature_native_battleworn_relic_reward`; the Native event relic contract, unified architecture, migration matrix, and ownership inventory record this boundary. Source-pool fidelity, hidden queue identity probabilities, and Native cursor parity remain open. No tests or builds were run.

**S6 additional Native relic reward paths (2026-10-19):** The same continuation now captures Round Tea Party's `continue_fight` draw, Trial's two `merchant_guilty` draws, and Pael's Wing's relic draw when a legal card-reward sacrifice advances its counter to an even number. Python exposes an action-ID entry point for Pael's Wing; event branches use the existing event-choice method. Other stochastic sites during these actions remain fail-closed. Source-pool fidelity, hidden queue identity probabilities, and Native cursor parity remain open. No tests or builds were run.

**S0 direct RNG and relic-draw audit increment (2026-10-19):** `rng_callsite_inventory.py --check-site-matrix --check-s0-contract-links --check-no-ambient-entropy` exits successfully. Its source census reports 35 direct RNG API expressions, no direct ad hoc entropy API expressions, and 100 typed request constructions: 54 resolve to static registry IDs, 46 require dynamic or forwarded-site tracing, and none of the statically resolved IDs are unregistered. A manual trace of all 13 `PrototypeNativeRelicGrabBag.Draw` callsites now maps each to an existing continuation or a deliberately restricted boundary; Native event relic, Neow, Battleworn, Round Tea Party, Trial, and Pael's Wing draws share the typed atomic relic continuation. The census and callsite trace do not establish installed-game coverage, source pool fidelity, or Native cursor parity. No tests or builds were run.

**S0 Native event-context SiteId guard (2026-10-19):** `StochasticSiteRegistryV111` now derives operation-specific lists accepted by `NativeEventContextRngV111` from existing event query/session lists and the six existing event-shuffle constants. Index, bounded-integer, unit-float, and shuffle helpers reject IDs outside their own lists before reading or advancing the saved event stream. The 21 statically enumerated direct event-context callsite IDs are included. Census v27 now resolves the three typed event-context request constructors and the shuffle wrapper through those finite lists: 94/100 typed constructors map to registered or conditional sites, five generic request adapters remain forwarded, and the diagnostic-only request remains outside the matrix. This closes the helper's forwarded-SiteId boundary; it does not establish source pool fidelity, event call-sequence completeness, or Native cursor parity. No tests or builds were run.

**S0 Native Fisher–Yates SiteId guard (2026-10-19):** The shared `NativeFisherYatesV111` primitive now validates every request SiteId against `StochasticSiteRegistryV111.NativeFisherYatesSiteIds` before the descending swap loop. That set reuses the 28 current Native shuffle callsite IDs from the combat, event, map, relic, and reward owners; the direct census resolves the primitive's forwarded constructor through the same finite list. This closes arbitrary SiteId forwarding at the shared primitive while leaving candidate-pool ownership with each caller. No tests or builds were run.

**S0 Native random-choice SiteId guard (2026-10-19):** `NativeRandomChoiceV111` now rejects request SiteIds outside the existing `ChanceLawQuerySiteIds` registry before dispatching index, bounded-integer, or unit-float requests. The registry lookup reuses its indexed set, while operation/support/stream ownership remain caller contracts. Census v29 resolves the three helper constructors through that registry: 98/100 typed request constructors map to registered or conditional sites, one generic legacy provider adapter remains caller-forwarded, and the diagnostic-only request remains outside the gameplay matrix. This closes arbitrary SiteId forwarding at the Native random-choice convenience boundary; it does not complete source-callsite review or validate site-operation pairings. No tests or builds were run.

**S6 Native encounter-formation per-index law increment (2026-10-19):** The read-only Chance Law query now accepts a declared Native model entry, floor, support count, and ordinal for `combat.encounter.formation.index_draw`. It reconstructs the encounter-local context stream identity and reuses the exact Native `NextInt` grid law, while preserving the legacy caller-bound query when Native fields are absent. The law covers one bounded transform only; formation candidate pools, source resolver order, preceding `GenerateMonsters` calls, seed derivation, source completeness, and Native cursor parity remain open. No tests or builds were run.

**S0 legacy random-choice SiteId guard (2026-10-19):** `LegacyRandomChoiceV111` now limits SiteIds to the registered Chance Law or Nature-choice sets, with explicit allowances for the four legacy Gaussian-component aliases used by Overgrowth and Hive/Glory map samplers. The ownership inventory records that helper boundary alongside the existing Native event-context, Fisher–Yates, and random-choice guards. The permitted inventory audit exits successfully with 101 typed requests, 99 registry-resolved IDs, one forwarded/dynamic ID, and the diagnostic-only unregistered SiteId; `git diff --check` passes. No tests or builds were run.

**S0 concrete legacy provider SiteId guard (2026-10-19):** `LegacyPrototypeRandomIndexProviderV111` now checks each typed request against the same finite legacy random-choice set before locating or advancing its stream. Direct provider callers can no longer bypass the facade's SiteId boundary; the six-stream modulo sampler and stream ownership checks are unchanged. The static census still marks the facade's forwarded identifier for manual tracing and keeps the Native diagnostic request out of the gameplay registry; the finite provider check closes the direct-dispatch path. No tests or builds were run.

**S0 Native random-index provider SiteId guard (2026-10-19):** `NativeRandomIndexProviderV111` now rejects direct typed requests outside the registered Chance Law/Nature-choice sites, the supported Native shuffle set, and the explicit diagnostic-only monster-local index request used for combat inspection. Validation runs before stream lookup and cursor advancement; `NativeRandomChoiceV111` keeps its narrower Chance Law-only convenience contract. No source sampling or cursor-parity claims change. No tests or builds were run.

**S0 Native unit-float provider SiteId guard (2026-10-19):** `NativeRandomUnitFloatProviderV111` now validates direct unit-float requests against the Chance Law registry and the Native event-context unit-float list before stream lookup or cursor advancement. Event-context unit-float draws keep their operation-specific boundary, and the convenience helper remains restricted to Chance Law sites. No source sampling or cursor-parity claims change. No tests or builds were run.

**S0 Native Gaussian-integer provider SiteId guard (2026-10-19):** `NativeRandomGaussianIntProviderV111` now accepts only the four Native map-quota Gaussian sites recognized by `ChanceLawEngineV111.IsNativeMapGaussianIntQuerySite`, before stream lookup or cursor advancement. The guard reuses the source-specific query list and does not broaden the random-index provider's allowed operations. No source sampling or cursor-parity claims change. No tests or builds were run.

**S0 Native request-family pairing increment (2026-10-19):** The Native random-index allowlist now excludes registered unit-float and map Gaussian-integer sites, while `NativeRandomChoiceV111` checks its method family against the corresponding registry-backed request set. Index/bounded-index and unit-float methods can no longer construct a typed request under a known SiteId from another request family. Source mechanics still own ordered support, stream and occurrence; this does not yet distinguish `choose_index` from `bounded_integer` at an individual SiteId. No tests or builds were run.

**S0 bounded-integer helper pairing increment (2026-10-19):** `NativeRandomChoiceV111.ChooseBoundedInteger` now accepts only the registered combat-gold site used by its production caller; `ChooseIndexRange` excludes that bounded-integer-only site. Other bounded requests continue through their existing typed source boundaries. No tests or builds were run.

**S3 named prototype-stream validation increment (2026-10-19):** `RunStochasticStateV111.Validate` now checks the legacy/prototype RNG bundle against its exact six stream names and codec/state shape. The uninitialized `RunStart` may remain empty; factorized starts still require the complete six streams. Native stream validation remains with `NativeRunRngV111`. No tests or builds were run.

**S3 shared Native stream-inventory validation increment (2026-10-19):** The engine-owned drawn-card-cost provider validates the incoming Native run bundle through `NativeRunRngV111.ValidateBundle`, then retains only `run.combat_energy_costs`. It accepts older snapshots that stored all fifteen Native streams and compacts them when the next cost resolves. No tests or builds were run.

**S4 semantic end-turn handoff validation increment (2026-10-19):** After the shared automatic-pipeline resume reaches the next combat player boundary, `SemanticEndTurnV111` now validates the idle Nature deck and its physical draw-pile membership through `SemanticDrawPileAdapterV111`. This closes the gap between validating the suspended draw stage and validating the resumed result; terminal reward handling is unchanged. Only `git diff --check` was run; no tests or builds were run.

**S4 deterministic zero-card hand-draw increment (2026-10-19):** A semantic `DrawPlayerHand` stage that completes with a zero request or no physical cards now advances through `CompleteAutomaticHandDrawStage` and can finish semantic end-turn without inventing a Nature ticket. The shared owner omits zero-count receipts from active progress and normalizes older flattened zero receipts, and the draw continuation validates the completed boundary while unresolved pre-draw choices remain pending. `git diff --check` passed; no tests or builds were run.

**S4 resumed hand-draw cursor commit increment (2026-10-19):** Resuming `BeforeHandDraw` and re-entering `DrawCards` now clear the active tagged progress through `CommitAutomaticHandDrawProgress`, which publishes the latest player and combat mechanics together after validating the shared draw-stage checkpoint. These paths no longer write the pipeline stochastic cursor directly. `git diff --check` passed; no tests or builds were run.

**S3 completed hand-draw receipt separation increment (2026-10-19):** The pipeline stochastic owner now stores and publishes the completed semantic requested-count receipt separately from tagged active hand-draw progress. The established flattened snapshot member projects both states; loading validates that a receipt belongs to the post-DrawPlayerHand cursor with an idle Nature deck, and normalizes legacy zero receipts to absence. No snapshot wire fields changed. `git diff --check` passed; no tests or builds were run.

**S3 typed completed hand-draw receipt transition (2026-10-19):** `CompleteAutomaticHandDrawStage` now publishes the completed requested-count receipt through an explicit provider-state transition after clearing active progress. The owner rejects attempts to publish a receipt while active hand-draw progress remains, and snapshot validation requires the idle Nature deck boundary. The flattened snapshot member and wire shape are unchanged. `git diff --check` passed; no tests or builds were run.

**S0 direct-call census anchor refresh (2026-10-19):** Regenerated `RNG_DIRECT_CALLSITE_CENSUS.md` with the permitted matrix, contract-link, and ambient-entropy checks after source-line movement. The source census still passes; this refresh only updates line anchors and tables, without changing stochastic request or site coverage.

**S0 native recycle shuffle site owner (2026-10-19):** Native semantic recycle now passes the existing `ControlledRecycleShuffleEngineV111.SiteId` to the source Native Fisher–Yates helper; the referenced `StochasticSiteRegistryV111.CombatDrawPileRecycleShuffleSiteId` member had no declaration. The census resolves this call to the registered `combat.draw_pile_recycle.swap_index` row: 23 shuffle expressions now map, with only the generic site-forwarding wrapper unresolved. The allowed census command and `git diff --check` passed; no tests or builds were run.

**S4 provider-independent hand-draw comparison increment (2026-10-19):** The v10 mechanics comparison now projects only active automatic hand-draw remaining-count progress. It drops the provider tag and semantic requested-count receipt from the mechanics fingerprint while leaving the saved provider state unchanged. `hello` continues to advertise the comparison schema ID, now v10. `git diff --check` passed; no tests or builds were run.

**S3 shared Nature deck-membership validation increment (2026-10-19):** Automatic top-draw autoplay and semantic effect-draw suspension, cost attachment, and resume now validate Nature-to-physical draw-pile membership through `SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary`, reusing its idle/pending mode instead of repeating set comparison logic. Existing pending-operation and provider-mode checks remain at each boundary. The permitted RNG census check passed and its source anchors were refreshed; `git diff --check` passed. No tests or builds were run.

**S3 completed hand-draw receipt on advanced cursor increment (2026-10-19):** Semantic automatic hand-draw resume now clears active progress and republishes the completed requested-count receipt through the pipeline stochastic owner when it receives a cursor already advanced past `DrawPlayerHand`. This matches the receipt transition used by `CompleteAutomaticHandDrawStage` and preserves the existing flattened snapshot shape. `git diff --check` passed; no tests or builds were run.

**S0 combat shuffle/recycle source-path review (2026-10-19):** Added scoped manual ownership rows for combat-entry shuffle, Native/legacy draw-pile recycle, and legacy effect-draw recycle to the existing site inventory. The rows trace current continuation owners and keep pool/order, source-prefix, and Native-parity gaps explicit; they do not close S0 acceptance. The permitted callsite census and `git diff --check` passed; no tests or builds were run.

**S4 shared pool-shuffle request owner increment (2026-10-20):** `SemanticPoolShuffleChoiceV111` now derives typed requests and operation-specific site IDs for semantic-pilot events and controlled-playable pending requests. Event validation and resume revalidation share that request construction path, removing duplicate site, owner, stream, and bound logic from the facades. `git diff --check` and the permitted direct-call census passed; no tests or builds were run.

**S4 shared draw-insertion request construction increment (2026-10-20):** The controlled-playable draw-position projection now uses `SemanticDrawInsertionEventV111.CreateRequest`, sharing its finite-site allowlist and typed bounded-request construction with semantic draw-insertion events. Source-timed owner and ordinal selection remain at the caller, and the separate generated-card model site remains distinct. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 shared semantic target-site identity increment (2026-10-20):** `SemanticTargetChoiceV111` now maps target kinds to the registered semantic site IDs, and both the pilot event builder and controlled-playable request projection use that mapping. Event validation checks the same mapping, so an unsupported target kind no longer falls through to the ordinary effect site. Owner-ID formats remain facade-specific for snapshot compatibility. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 generated-card event construction ownership increment (2026-10-20):** `SemanticGenerationChoiceV111` now constructs and validates generated-card event fingerprints, support fingerprints, and event IDs. The semantic pilot supplies the live source pool, operation-specific site and owner, and chronological ordinal, then delegates event construction to the choice owner. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 target-event construction ownership increment (2026-10-20):** `SemanticTargetChoiceV111` now builds the target event, support fingerprint, and event ID from the facade's boundary fingerprint, target kind, owner, ordinal, and live eligible identities. Both pilot and end-turn facades continue through the same builder; site mapping and event validation remain in the target owner. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 potion-generation event construction ownership increment (2026-10-20):** `SemanticPotionGenerationChoiceV111` now owns its stable site ID and constructs/validates the semantic event, support fingerprint, and event ID. The pilot delegates event creation to that owner, and the controlled-playable request projection uses the same site constant. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 card-transform event construction ownership increment (2026-10-20):** `SemanticCardTransformChoiceV111` now owns the transform event schema/site identity and constructs the event owner, support fingerprint, and event ID from the live source card and replacement pool. The pilot delegates construction to the choice owner, and ControlledPlayable uses the shared site ID. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 cost-event envelope ownership increment (2026-10-20):** `SemanticCostChoiceV111` now constructs and validates cost events from caller-selected bounded requests, including support fingerprints, event IDs, defensive request copies, and optional target-card identity. Both the pilot and nested effect-draw cost descriptor use that constructor. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 hand-cost event construction ownership increment (2026-10-20):** `SemanticHandCostSelectionChoiceV111` now owns hand-cost event schema/site identity, owner ID, support fingerprint, event ID, and defensive candidate copies. The pilot supplies the live random-cost operation and corresponding card models, then delegates event construction to the choice owner. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 shortlist event construction ownership increment (2026-10-20):** `SemanticCardShortlistChoiceV111` now owns shortlist site/operation mapping, source-boundary inspection, candidate labeling, support fingerprint, and event ID. The pilot supplies its boundary fingerprint and chronological ordinal, then delegates event construction to the existing shortlist owner. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 pool-shuffle event construction ownership increment (2026-10-20):** `SemanticPoolShuffleChoiceV111` now owns the source-boundary description, typed request, site mapping, event support fingerprint, and event ID for full-pool shuffle events. The pilot retains fixture and boundary-conflict policy, then delegates the source event to the pool-shuffle owner. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 weighted-intent event construction ownership increment (2026-10-20):** `SemanticIntentChoiceV111` now owns the weighted event schema, support fingerprint, event ID, and defensive support copy. Pilot validation, selection, and pending-event projection all use the shared constructor; source request and eligibility generation remain in the controlled-turn owner. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 generated-choice request ownership increment (2026-10-20):** `SemanticGenerationChoiceV111`, `SemanticPotionGenerationChoiceV111`, and `SemanticCardTransformChoiceV111` now build and validate their typed index requests. `ControlledPlayableCombatV111` delegates request construction to those owners while retaining its existing facade-specific owner IDs and ordinals; semantic event construction uses the same site, stream, and ordinal projection. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S0 request operation-kind locator increment (2026-10-20):** RNG census v32 records the `RandomIndexRequestV111` `OperationKind` expression, the default `choose_index` when omitted, and statically visible alternatives alongside each request constructor's SiteId. Its legacy caller-bound query rows now resolve `site.OperationKind` through the site-keyed law dictionary, while one generic legacy provider adapter remains caller-selected. This is review evidence, not runtime site-operation validation or full S0 acceptance. The permitted census and `git diff --check` passed; no tests or builds were run.

**S0 shared caller-bound operation metadata increment (2026-10-20):** `StochasticSiteRegistryV111` now owns the 32-site legacy caller-bound query metadata used by both Chance Law construction and `RandomIndexRequestV111.Validate`. A request for one of those sites must use its registered `choose_index` or `bounded_integer` kind. Census v33 traces constructor operation kinds through the same registry dictionary; generic caller-forwarded values outside this declared subset remain unresolved, and the broader S0 audit stays open. The permitted census and `git diff --check` passed; no tests or builds were run.

**S0 Native bounded-operation validation increment (2026-10-20):** `RandomIndexRequestV111.Validate` now also derives `bounded_integer` from the registry's existing Native bounded-index, Native event-context bounded-index, and Fisher–Yates site sets. Legacy callers and Native providers therefore use the same declared operation boundary without introducing another site list. Census v34 records the expanded validation scope; generic forwarded values outside the declared sets and the row-level S0 audit remain open. No tests or builds were run.

**S3 Tea of Discourtesy insertion adapter increment (2026-10-20):** Combat is now constructed before Tea of Discourtesy adds its two temporary Dazed cards. Each card and its physical draw-pile insertion are committed through `SemanticDrawPileAdapterV111`, with the existing request site, stream, owner, bounds, and insertion order retained. The permitted callsite census and `git diff --check` passed; no tests or builds were run.

**S3 atomic new-card insertion increment (2026-10-20):** `SemanticDrawPileAdapterV111.ApplyNewDrawPileInsertion` now commits a newly created `CombatCardInstance`, its draw-pile membership, `NextCardInstanceId`, and provider constraint together. Enemy draw effects, Personal Hive, generated draw cards, controlled insertion, and Tea of Discourtesy now use this owner; the shared insertion boundary rejects unregistered physical identities. Existing-card moves retain the identity-preserving insertion API. The permitted callsite census and `git diff --check` passed; no tests or builds were run.

**S3 atomic combat-zone identity increment (2026-10-20):** `ApplyNewCardZoneInsertion` now commits new card records and next IDs with Hand, DiscardPile, PlayPile, and ChoicePool membership. Generated cards across ordinary combat, controlled pool-shuffle, and History Course paths use it. Draw-pile and non-draw zone adapters reject unregistered card identities; card transforms replace zone identity, old/new card records, and draw provider constraint together. Explicit combat-card deletions from source selections and Hive Swipe now commit with their zone removals. The permitted callsite census and `git diff --check` passed; no tests or builds were run.

**S3 atomic transform-provenance increment (2026-10-20):** Combat-card replacement now commits `TransformedPersistentCardIds` through the same zone-identity adapter transition as the replacement zone membership, old/new card records, next ID, and draw-provider constraint. The empty provenance projection after temporary-card transforms remains compatible. No tests or builds were run.

**S3 committed zone-registry validation increment (2026-10-20):** The shared zone commit now rejects duplicate combat-card records, any resulting physical zone identity without a registered card record, and a stale `NextCardInstanceId` before publishing mechanics and provider state. Suspended choice sources may remain registered outside zones and continue through their owning continuation validation. No tests or builds were run.

**S0 finite semantic SiteId mapping increment (2026-10-20):** `SemanticPoolShuffleChoiceV111.SiteIdForOperation` now maps all eight supported source operations explicitly and returns no SiteId for unknown operations; request construction fails closed on that result. The v35 direct-call census resolves the target SiteId helper into its three registered target sites; it still marks the pool-shuffle helper as dynamic, now documented with its finite switch and rejection path in the ownership inventory. The permitted census and `git diff --check` passed; no tests or builds were run.

**S4 shared next-turn intent-frame consumption increment (2026-10-20):** `ResumeControlledTurnAfterIntents` now reads and validates the completed `ControlledTurnIntentsV111` frame from the automatic pipeline owner, then resumes from that frame's combat checkpoint. Semantic end-turn and controlled-playable facades no longer unwrap, validate, and pass a duplicate combat checkpoint; semantic end-turn keeps its stop-before-hand-draw continuation policy. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 mechanics comparison intent-cursor increment (2026-10-20):** The provider-independent mechanics projection now retains the next enemy cursor for suspended initial and next-turn intent passes, while omitting their provider requests and transcript. The comparison schema advances to v11. This makes partially committed intent mechanics progress visible in replay comparisons without exposing the stochastic ticket payload. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 mechanics comparison enemy-action progress increment (2026-10-20):** The mechanics projection now retains a normalized operation kind and completed-choice count for a suspended enemy-action pipeline, using the existing replay owner’s finite SiteId mapping rather than adding another site list. It omits request SiteId, owner, support and selected outcomes while preserving where the mechanics continuation stopped. The comparison schema advances to v12. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 mechanics comparison semantic-draw normalization increment (2026-10-20):** Pending semantic draw progress is now projected through a provider-independent mechanics record containing remaining count, source boundary, event depth, deferred keyword mechanics and Stratagem stage. Standalone/automatic semantic-provider segment flags are omitted, matching the comparison contract. The schema advances to v13. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 recursive mechanics-continuation normalization increment (2026-10-20):** The v14 mechanics projection now normalizes provider policy recursively through pending-choice operation queues, nested choice parents, and saved event subscribers. It removes Native generated-choice RNG policy and semantic draw/Stratagem routing tags while retaining operation mechanics, hook effects, and continuation order. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 shared semantic hand-draw source boundary increment (2026-10-20):** `SemanticDrawPileAdapterV111.ValidateSemanticHandDrawSourceDeck` and `IsSupportedSemanticHandDrawSourceDeck` now own admission for an idle Nature deck or the providerless empty-draw/nonempty-discard recycle case, shared by end-turn validation and automatic draw entry. After next-turn intents, semantic end-turn now recognizes a reward or terminal result before requiring the hand-draw checkpoint and clears the active intent frame through `CompleteAtBoundary`. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 completed hand-draw automatic-target continuation increment (2026-10-20):** `SemanticTargetChoiceV111.ValidateBoundary` now admits the player-turn automatic cursor from `PlayerTurnStarted` through late relic autoplay, including the final pipeline cursor. Semantic end-turn exposes supported controlled target choices there, retains the completed draw frame through chained targets and terminal outcomes, uses the target owner's draw-pile/provider check for continuing combat results, and replays the draw and pipeline prefix on restore before accepting the saved state. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 automatic target-cursor ownership increment (2026-10-20):** `PrototypeGameEngine.IsAtSupportedAutomaticPipelineTargetBoundary` now owns the valid cursor range for both pre-AdvanceTurn target suspensions and post-draw player-turn hooks/late autoplay. `SemanticTargetChoiceV111.ValidateBoundary` delegates automatic timing to that engine predicate instead of deriving pipeline cursor semantics locally. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 automatic nested draw/cost chronology increment (2026-10-20):** Late player-turn autoplay may suspend on semantic effect draw or draw cost after a controlled target. The mechanics owner admits only an autoplay card-play frame at the supported post-draw pipeline cursor; the end-turn owner stores typed nested outcomes in chronology with target decisions and replays them in order on restore. Existing Mayhem admission remains supported. Other automatic-pipeline choice families remain open. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 automatic nested draw-insertion chronology increment (2026-10-20):** Semantic effect draws resumed from autoplay can suspend on a controlled after-shuffle relic insertion. The end-turn continuation now owns its typed insertion decision in the same chronological transcript as nested draws, costs, and targets, and full restore replays each insertion before resuming the draw suffix. The pilot routes end-turn insertion events through the owning hand-draw or nested end-turn continuation. The permitted direct-call census and `git diff --check` passed; no tests or builds were run.

**S4 autoplay hand-cost chronology increment (2026-10-20):** Source-validated autoplay continuations can now suspend on a random cost for each eligible hand card or a fixed-cost random hand-card target. Semantic end-turn records both outcomes in its typed nested-choice chronology and replays them in order; the pilot routes each pending event through that owner. The permitted `git diff --check` passed; no tests or builds were run.

**S4 autoplay source card-selection chronology increment (2026-10-20):** A source `select_cards` prompt under a saved autoplay card is now admitted only at supported Mayhem or post-draw cursors. The semantic card-selection owner validates action support and hidden-pile changes; semantic end-turn stores the typed action in boundary order and replays it before continuing the pipeline. The permitted `git diff --check` passed; no tests or builds were run.

**S4 autoplay draw-hook action chronology increment (2026-10-20):** A source `CardDrawn` hook action inside an autoplayed card's semantic effect draw is now admitted only at supported Mayhem or late player-turn cursors. A new draw-hook event schema records this automatic-pipeline context; semantic end-turn adds the typed action to boundary chronology and full restore replays it before continuing the pipeline. Existing standalone and automatic hand-draw hook schemas and event identities remain compatible. The permitted `git diff --check` passed; no tests or builds were run.

**S0 physical-draw ownership review update (2026-10-20):** The existing `combat.draw_pile.draw_card` scoped review now traces source `CardDrawn` hook actions reached inside autoplayed semantic effect draws through the Mayhem/late player-turn boundary, typed v3 event, end-turn chronology, and full replay. The deferred draw law remains `UNVERIFIED`, peek/scry gameplay callsites are still absent, and the row-level S0 audit remains open. `git diff --check` passed; no tests or builds were run.

**S0 Jeweled Mask source review update (2026-10-20):** The scoped review now traces the ordered Power-pile support, non-Innate preference, exact `BeforeHandDraw` semantic continuation, physical-identity event, and full automatic-draw replay. Native pool/stream fidelity, source seed frequencies, cursor parity, and mechanics-tested evidence remain open; Nature probability remains `UNVERIFIED`. `git diff --check` passed; no tests or builds were run.

**S0 autoplay shortlist ownership review update (2026-10-20):** The existing random-shortlist site review now records that semantic end-turn stores each autoplay shortlist outcome in source order and replays it before continuing the saved pipeline. Native pool/stream fidelity and source eligibility review remain open; full-pool shortlist sampling remains `UNVERIFIED`. `git diff --check` passed; no tests or builds were run.

**S4 Gambling Chip opening-selection increment (2026-10-20):** Nature mode now admits the exact sequential optional discard prompt from Gambling Chip's opening `OpeningPlayerTurnStarted` subscriber at the saved `BoneTeaAfterTurnStart` cursor. Semantic card-selection schema v2 fingerprints the accumulated selected-card prefix; ordinary v1 event identity is unchanged. Completing the prompt preserves discard-event ordering and the generated draw count through semantic draw/cost continuations, admitted only while the saved event frame still identifies Gambling Chip. The mechanics comparison advances to v15 to retain this selected draw count. Other opening choices and nested hook prompts remain unsupported. `git diff --check` passed; no tests or builds were run.

**S4 Blessed Antler opening insertion increment (2026-10-20):** The existing typed draw-insertion continuation now admits Blessed Antler's exact turn-one `BeforeHandDraw` subscriber at the saved `DrawHand` cursor. It validates the fixed Dazed insertion, remaining operation count, event frame, and relic trigger; each selected position is recorded through the semantic-pilot insertion history before the opening hand draw resumes. The resume boundary accepts only the same validated automatic insertion or another supported pipeline choice. The semantic insertion schema and continuation owner are unchanged. Static review and `git diff --check` passed; no tests or builds were run.

**S4 Hello World hand-draw generation increment (2026-10-20):** The existing full-pool shuffle choice now admits Hello World's exact turn-two-or-later `BeforeHandDraw` subscriber. It validates the filtered character Common-card pool, live stack-count operation, capped saved turn-start generation snapshot, and automatic hand-draw cursor; Native run-stream snapshots assign the swap request to `run.combat_card_generation`, matching the source call. Each bounded swap continues through the existing nested end-turn transcript before the hook dispatch resumes. Other automatic hand-draw pool shuffles remain unsupported. Static review and `git diff --check` passed; no tests or builds were run.

**S4 Crossbow turn-start generation increment (2026-10-20):** The existing typed generated-card choice now admits Crossbow's exact turn-two-or-later `PlayerSideTurnStarted` trigger at the clean `DrawPlayerHand` cursor. Later subscribers must match their saved relic trigger and use only source-reachable deterministic energy, gold-for-energy, block, or player-power effects; other random or choice continuations fail closed. Semantic end-turn holds the pre-draw choice and starts the semantic hand-draw owner after the selected Attack is committed; restore replays the trigger and selection before continuing. The current generated-card event retains its `prototype.combat` label and `UNVERIFIED` status, so Native `run.combat_card_generation` attribution and cursor parity remain open. Other automatic-pipeline generation continuations remain unsupported. Static review and `git diff --check` passed; no tests or builds were run.

**S0 opening `CombatStarted` stage review (2026-10-20):** The current modeled subscriber set was traced across the core and Native-profile relic definitions. Every effect is fixed block, power, status, healing, or fixed-card creation, so this stage introduces no stochastic choice in the current model. The opening-pipeline comment now points to this scoped review and calls for revisiting it when the subscriber set changes. This does not close the row-by-row S0 audit or establish Native trigger-order parity. No tests or builds were run.


**S3 Nature deck array encapsulation increment (2026-10-20):** `SemanticDeckStateV111` and its nested pool, pending, transcript, event, and order records now copy mutable arrays on construction and replacement and return defensive copies from array properties. Constructor and deconstruction shapes, JSON property names, record `with` updates, and independent `Fork` behavior are preserved. A source scan found no indexed writes through these array projections. `git diff --check` passed; no tests or builds were run.


**S3 semantic draw transcript encapsulation increment (2026-10-20):** `SemanticCombatDrawStochasticStateV111` now copies its typed decision arrays on construction and replacement and returns cloned decisions from its transcript properties. Suspended cost requests and typed choice support can no longer be edited through a shared continuation array. Existing flattened snapshots and `Fork` behavior are preserved. The permitted indexed-write census and `git diff --check` passed; no tests or builds were run.


**S4 end-turn chronology encapsulation increment (2026-10-20):** `SemanticEndTurnStochasticStateV111` now protects intent, enemy-action, target, boundary, and nested-choice arrays with deep copies on construction/replacement and when returned to callers. Existing constructor/deconstruction shapes, JSON property names, flattened facade projections, and chronological `Fork` replay remain in place. A source scan found no indexed writes through these projections; `git diff --check` passed. No tests or builds were run.


**S3 controlled-playable transcript encapsulation increment (2026-10-20):** `ControlledPlayableCombatStochasticStateV111` now deep-copies all typed decision transcripts, the pending index request and its ordered support, and the active recycle continuation on construction, replacement, projection, and fork. Flattened compatibility fields and JSON member names remain stable. The indexed-write census and `git diff --check` passed; no tests or builds were run.


**S2 pilot audit snapshot encapsulation increment (2026-10-20):** `SemanticPilotStochasticStateV111` now deep-copies Native-policy decisions and mixed provider traces, source-route action and decision arrays, and pending end-turn stochastic chronology on construction, replacement, projection, and fork. Native request supports cannot be rewritten through a shared audit array. Constructor/deconstruction shape and serialized field names remain stable. `git diff --check` passed; no tests or builds were run.


**S3 run-world queue encapsulation increment (2026-10-20):** `RunWorldStochasticStateV111` and its Act 1, later-act, and relic bag records now copy persistent queue arrays and prepared bag collections on construction, replacement, projection, and fork. Existing constructor/deconstruction shapes, JSON members, `OrderMode` discrimination, and flattened `RunWorldState` projections remain in place. The permitted indexed-write census and `git diff --check` passed; no tests or builds were run.


**S3 run stochastic history encapsulation increment (2026-10-20):** `RunStochasticStateV111` now deep-copies controlled event-choice and run-action history arrays, including ordered request support, on construction, replacement, projection, and fork. Existing positional constructor/deconstruction shape, JSON names, and flattened `RunState` projections remain stable. The indexed-write census and `git diff --check` passed; no tests or builds were run.


**S3 pending choice ownership increment (2026-10-20):** `PrototypePendingDeckChoiceState` and `PrototypePendingEventDeckChoiceState` now copy candidate card-instance arrays, and `PrototypePendingEventPotionReplacementState` copies candidate slots, on construction and replacement and returns defensive copies. Existing constructor/deconstruction signatures, JSON members, optional defaults, `with` updates, and independent fork behavior remain available. Stale fixtures now publish intentional candidate changes through `with` replacement or verify detached reads. The indexed-write census and `git diff --check` passed; no tests or builds were run.


**S3 event continuation ownership increment (2026-10-20):** `EventMechanicsStateV111` now owns its queued potion IDs, queued deck choices, and offered choice IDs. `EventCommittedRandomStateV111` owns its Repy potion/relic result lists and Future of Potions offers, and `EventStochasticStateV111` forks its suspended controlled event-choice frame on access. Positional constructors/deconstructors, flattened `EventState` members, JSON names, and record `with` updates remain intact. Event fork fixtures now publish queue replacements explicitly. The indexed-write census and `git diff --check` passed; no tests or builds were run.


**S3 combat mechanics projection ownership increment (2026-10-20):** `CombatMechanicsStateV111` now deep-copies enemy, card, power, relic, potion, shield, autoplay, pet, and transformed-card collections on construction, replacement, and projection. It also owns pending choice trees, automatic-pipeline continuations, Knowledge Demon choices, and combat counters through their `Fork` boundaries; `CombatState.Fork` delegates to the mechanics owner. Tampering fixtures now publish changed combat arrays and pending choices through `with` replacement. The indexed-write census and `git diff --check` passed; no tests or builds were run.


**S3 Native encounter-formation ownership increment (2026-10-20):** `NativeEncounterFormationV111` now copies its enemy formation and per-monster RNG contexts, and `NativeEncounterFormationAuditV111` copies typed decisions with their ordered request supports, on construction and replacement and returns defensive projections. Native monster-local cursor advancement publishes the updated context collection through `with` replacement, preserving each context's defensive RNG bytes. Existing constructor/deconstruction and snapshot members remain unchanged. The indexed-write census and `git diff --check` passed; no tests or builds were run.


**S3 controlled initial-HP snapshot ownership increment (2026-10-20):** `ControlledInitialHpStateV111` now copies its ordered pending support and typed decision transcript, including nested request supports, on construction, replacement, projection, and fork. Existing snapshot member names and the positional constructor/deconstruction shape remain unchanged. Tampering fixtures now publish a replacement request explicitly; the indexed-write census and `git diff --check` passed. No tests or builds were run.


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

**S4 deterministic outside-combat potion action increment (2026-10-23):** The existing `ReferenceNatureRunActionSessionV111` admits legal Fruit Juice, Blood Potion, and Neow's Ambergris uses through their exact modeled run effects. Its controlled replay rejects any chance request, and completed actions use the existing snapshot replay validation. In addition to safe pre-choice Event use, the saved event-reward continuation now advances deterministic potion actions while the event reward remains pending and no full-belt replacement is active. Replacement continuations remain event-specific. No chance owner or snapshot schema was added. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Entropic Brew fill continuation increment (2026-10-23):** The same run-action session captures each empty-slot potion draw for legal non-event uses and pre-choice Event uses when no full-belt replacement is pending. Legacy runs use the existing ordered-pool law on `reward`; Native runs use the `player.rewards` bounded-index law, with each slot owner and support checked against the prototype pool. The post-choice Event reward case is handled by the existing controlled event-reward continuation with a saved Entropic Brew action and event-decision prefix; see the 2026-10-24 continuation audit above. Native game pool ordering, trigger order, and source stream attribution remain open pending the reference mod. Static source review and `git diff --check` only; no tests or builds were run.

**S4 deterministic combat-potion victory increment (2026-10-23):** Fire, Explosive, and Explosive Ampoule now use the existing complete combat action-result boundary when their modeled damage ends combat. Potion replay gets the existing narrow engine-owned reward allowance; Distilled Chaos gets it as well because autoplay may win combat. Reward generation remains limited to the registered engine-owned sites. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Colorless semantic effect-draw increment (2026-10-23):** Finesse, Flash of Steel, Impatience, Master of Strategy, and Scrawl now use the existing source card-effect draw admission and provider-owned draw continuation. Impatience retains its no-Attack enqueue condition, Scrawl remains bounded by normal hand capacity, and Flash of Steel preserves damage-before-draw ordering. No event or snapshot schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 deterministic Colorless player-action increment (2026-10-23):** The semantic player-action gate now admits 32 modeled deterministic Colorless card effects, including legal target actions. Rend uses its target-debuff damage count; Bolas and Thrumming Hatchet reuse the existing last-turn rebound hook and semantic draw-pile adapter. Calamity installs its modeled power and resolves later Attack-triggered model picks through the existing generated-card continuation. Nostalgia's attack and skill returns are admitted only for the exact known source-card append under the active power's stack condition; the shared adapter commits it. Ordinary action replay remains guarded and validates provider-owned deck consistency at the completed player boundary. Mayhem, Entropy, and Stratagem reuse their existing downstream owners; other random generation, draw, and selection remain specialized. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Entropic Brew event-reward continuation increment (2026-10-23):** The existing controlled event-choice continuation now captures and resumes post-choice Entropic Brew draws while the event reward is pending. It preserves the action and each selected potion draw in the event session's mixed Nature/player transcript, using one shared request predicate for pool fingerprint, source slot owner, support, and legacy `reward` or Native `player.rewards` stream. Native pool order, trigger order, and source stream attribution remain open pending the reference mod. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Colorless Restlessness draw continuation increment (2026-10-23):** The semantic pilot now admits the implemented Colorless Restlessness card through the existing provider-owned effect-draw continuation. Its `HandEmptyAtEnqueue` condition remains in the source mechanics queue, so the following draw and energy gain retain source order and the shared physical-pile/provider checks. No event or snapshot schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Neow's Ambergris combat-potion increment (2026-10-23):** The semantic pilot now admits Neow's Ambergris when present in a combat potion slot. Its modeled healing and extra-turn flag stay with the ordinary combat action and turn pipeline under the controlled-chance guard; no chance owner or snapshot schema was added. Static source review and `git diff --check` only; no tests or builds were run.

**S4 deterministic event-card action increment (2026-10-23):** Byrd Swoop, Peck, and Toric Toughness now enter through the existing semantic player-action allowlist. The source combat engine resolves their modeled damage or Block effects, while the controlled-chance guard and existing draw-pile boundary checks remain active. Static source review and `git diff --check` only; no tests or builds were run.

**S4 deterministic full-belt event-potion increment (2026-10-23):** The saved ReferenceNature run-action path now admits the exact modeled deterministic outside-combat potion effects while an Event replacement is pending and no controlled event choice is active. The source mechanics action consumes the potion and fills the freed slot with the pending event potion atomically. Entropic Brew and other stochastic replacement paths retain their dedicated continuations. No chance owner or snapshot schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 relic action-draw continuation increment (2026-10-23):** Player card actions can now suspend at Gremlin Horn's `EnemyDefeated` draw, Ink Bottle's third `CardPlayed` draw, Iron Club's fourth `CardPlayed` draw, Unceasing Top's empty-hand `CardPlayed` draw, and Joss Paper's fifth `CardExhausted` draw. Admission verifies the pending source card, exact relic subscriber and trigger, plus the `CardPlayed` source identity where applicable. The suspended first draw requires a nonempty source pile; any later nested draw still passes its own continuation checks. No event or snapshot schema changed. The modeled trigger census found no additional relic `DrawCards` source; Native trigger and cursor parity remain open. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Mad Science draw-card continuation increment (2026-10-23):** The semantic pilot recognizes Mad Science through its existing runtime card definition and admits only the saved Wisdom Skill's `DrawCards` effect to the semantic effect-draw owner. Other Tinker type/rider combinations stay outside this admission. No event or snapshot schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S2/S4 Brightest Flame draw-card continuation increment (2026-10-23):** The semantic pilot now admits the physical Brightest Flame Ancient reward card through the existing effect-draw owner. The continuation preserves the source order of its draw followed by Max HP loss, with no event or snapshot schema change. Static source review and `git diff --check` only; no tests or builds were run.

**S4 modeled direct-draw source census (2026-10-23):** A static scan accounted for every modeled `DrawCards` source across standard and Colorless cards, Brightest Flame, Mad Science's Wisdom rider, direct-draw combat potions, and modeled action-relic triggers. Each source already routes through a semantic effect-draw, automatic hand-draw, autoplay, or selection continuation. No additional modeled direct-draw allowlist entry remains; Native stream/cursor parity and unmodeled game-content triggers remain separate work. `git diff --check` passed; no tests or builds were run.

**S4 semantic draw chronology increment (2026-10-23):** `SemanticCombatDrawV111` now stores versioned ordered references across its semantic deck, cost, random pile-card, draw-insertion, Stratagem, and draw-hook transcripts. Replay checks each chronology entry against the next live mechanics boundary before consuming that typed decision. Legacy snapshots without the chronology version remain readable and gain the field when restored through source replay; decision and event identities are unchanged. `git diff --check` passed; no tests or builds were run.

**S4 shared automatic-pipeline hand-draw handoff increment (2026-10-23):** Semantic end-turn now uses one validated hand-draw entry for both supported choice resumes at `DrawPlayerHand` and the post-intent boundary. The source-specific Crossbow continuation still validates its saved relic and generated-card operation before resolution; the shared classifier now owns the subsequent hand-draw handoff. Static source review and `git diff --check` only; no tests or builds were run.

**S4 semantic selection-potion source census (2026-10-23):** Static review confirms the seven potion IDs admitted by `IsSemanticSelectionPotion` reuse the existing semantic card-selection, full-pool shuffle, and generated-card owners. This includes physical draw-pile selection for Droplet of Precognition, the source temporary-cost effects for Touch of Insanity and Liquid Memories, and Gambler's Brew's sequential selection followed by its draw continuation. No additional chance or snapshot owner is needed for these modeled actions; no tests or builds were run.

**S4 semantic end-turn chronology version increment (2026-10-23):** New end-turn continuations mark their ordered enemy-action, target, and nested-choice boundary transcript as version 1 and require complete coverage even when empty. Snapshots without the marker keep the legacy replay path and are upgraded from replayed chronology on restore; chronology carries across next-turn intent frames. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Foul Potion action increment (2026-10-23):** The modeled Potion Courier Foul Potion now enters the semantic combat pilot through the existing deterministic-potion guard. Its all-enemy damage reuses the ordinary action result and narrow engine-owned reward boundary if it wins combat; no chance owner or snapshot schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S2 scripted mixed-primitive schema increment (2026-10-23):** Scripted action and whole-run session schemas now identify six-primitive transcripts as v2. Restore accepts their prior v1 envelopes and upgrades legacy index/unit-float continuation state to the mixed continuation schema before replay validation; saved decisions and mechanics are unchanged. Static source review and `git diff --check` only; no tests or builds were run.

**S2 controlled run-action transcript version increment (2026-10-23):** Completed controlled run-action traces now carry an additive schema marker for index-only v1 or typed-union v2 payloads. New trace creation sets the matching marker; snapshots predating the marker retain their existing inferred format and validation. Static source review and `git diff --check` only; no tests or builds were run.

**S2 controlled event replay prefix consolidation (2026-10-23):** Controlled event-choice replay now uses the detached prefix cursor from `ControlledStochasticChanceReplayV111` for saved-decision validation and exact request comparison, removing the parallel index-prefix owner without raising the global mixed-replay active signal. Event-specific ordinal normalization and candidate-label capture remain with the event boundary. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Trash Heap semantic card-action increment (2026-10-24):** The six fixed-effect Trash Heap cards now enter through the existing semantic player-action boundary. Dual Wield uses the source card-selection event with checks for its physical source, upgraded copy count, and exact eligible hand candidates. Rebound's known draw-pile return requires the exact physical-card zone transition and expected power-stack consumption, committed by the shared draw-pile adapter. Distraction and Rip and Tear continue through their generated-card and random-target owners. No event or snapshot schema changed. Static source review and `git diff --check` only; no tests or builds were run.

**S4 regional deterministic card-action increment (2026-10-24):** Feeding Frenzy, Enlightenment, Exterminate, Squash, and Luminesce now use the existing semantic player-action boundary. Their modeled power, cost, energy, and damage effects remain under the controlled-chance guard and physical draw-pile checks. No chance owner or snapshot schema was added. Static source review and `git diff --check` only; no tests or builds were run.

**S4 Hive status-card action increment (2026-10-24):** Frantic Escape and Toxic now enter through the same semantic player-action boundary. Frantic Escape replays its source CardPlayed Sandpit and combat-cost hook; Toxic remains a no-effect-on-play exhaust action while its held damage is handled by ordinary turn mechanics. Static source review and `git diff --check` only; no tests or builds were run.

**S4 modeled playable status action increment (2026-10-24):** Beckon now uses the deterministic semantic player-action boundary; its held damage and ordinary card movement remain with source mechanics. A static census separates it, Toxic, and Frantic Escape from the modeled unplayable Burn, Dazed, Wound, Infection, and Wither cards. No chance owner or snapshot schema was added. Static source review and `git diff --check` only; no tests or builds were run.

**S4 tagged hand-draw resume query increment (2026-10-24):** The ordinary
pipeline hand-draw handoff, controlled hook and recycle resumes, semantic
automatic resume, and target-boundary guard now inspect
`PrototypeAutomaticHandDrawProgressV111` through its stochastic owner or
shared remainder queries instead of re-deriving modes from flattened counters.
`ControlledDrawRemaining` remains a compatibility projection, and snapshots
keep their existing shape. Remaining compatibility-projection readers and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 tagged hand-draw consumer migration increment (2026-10-24):** Semantic
hook and draw-insertion validation, controlled-playable staging, automatic
cost and Stratagem resumes, semantic draw-count recovery, and Mayhem cursor
admission now use the tagged progress owner and shared queries. Checks for an
empty hand-draw cursor now rely on `HandDrawProgress: null` plus the separately
owned completed semantic receipt instead of repeating all flattened progress
projections. Snapshot fields and transcript policies are unchanged. Remaining
compatibility-projection consumers and cross-provider mechanical equivalence
remain open. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 semantic hand-draw validation owner increment (2026-10-24):** The semantic
draw frame, semantic mechanics projection, and insertion/Stratagem event
descriptions now compare active automatic draw counts through
`TryGetSemanticHandDrawProgress`; pre-draw validation reads its tagged mode.
The Core scan now finds no runtime reads of flattened hand-draw progress
counters outside their compatibility and serialization owners. Snapshot
members and choice chronology are unchanged. Cross-provider mechanical
equivalence remains open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 next-turn intent entry increment (2026-10-24):** Semantic end-turn and
controlled-playable continuations first shared an engine-owned entry for the
clean ordinary `AdvanceTurn` boundary. The follow-up below extends that owner
to opening committed intents while keeping both site policies distinct.

**S4 shared intent-entry boundary increment (2026-10-24):**
`BeginAutomaticPipelineIntents` now validates both intent entry sites in the
ordinary engine. Semantic source-route opening intents require the
provider-free pre-cursor checkpoint; next-turn intents require the ready
`AdvanceTurn` cursor. Semantic pilot, end-turn, and controlled-playable paths
use that owner while retaining their distinct site and transcript policies.
Snapshot fields are unchanged. Broader resume consolidation and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 classified automatic-resume handoff increment (2026-10-24):**
`ResumeAutomaticPipelineContinuationWithBoundary` now returns the resumed run
with its engine-classified boundary. `ResumeControlledTurnAfterIntents` and
end-turn hand-draw completion consume that pair; semantic end-turn and
controlled-playable facades therefore share the classification with their
resulting run instead of reclassifying it. Controlled-playable indexed-choice
replay also returns the boundary with its result and passes it into turn
staging for draw cost, random cost, pool shuffle, draw insertion, enemy action,
generated-card, potion-generation, transform, and target outcomes. Facades
still select their provider-specific continuation and transcript policies.
Full resume consolidation and cross-provider mechanical equivalence remain
open. Static source review and `git diff --check` only; no tests or builds were
run.

**S4 semantic-choice boundary handoff increment (2026-10-24):** The shared
`SemanticChoiceResumeV111` now returns the classified boundary with semantic
target and automatic-pipeline nested-choice results. End-turn passes that
classification into its boundary publisher alongside the run and typed
decision, while semantic pilot uses the same target resume owner. This avoids
reclassifying those semantic-choice results in the end-turn facade without
moving chronology ownership. Broader resume consolidation and cross-provider
mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 common automatic-resume result increment (2026-10-24):** The engine,
semantic-choice owner, semantic end-turn facade, and controlled-playable
continuations now pass one `PrototypeAutomaticPipelineResumeResultV111`
containing the resumed `RunState` and classified boundary. This removes
parallel pair shapes and prevents end-turn and playable staging from separating
or recomputing the boundary. Snapshot fields, provider transcripts, and
chronology ownership are unchanged. Broader automatic draw-resume consolidation
and cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 enemy-action classified handoff increment (2026-10-24):**
`ResumeControlledEnemyAction` now returns the classified
`PrototypeAutomaticPipelineResumeResultV111` from its existing pipeline
handoff. Semantic end-turn preserves that boundary when publishing its
chronology, and controlled-playable routes the result through the common typed
executor's new value-result entry point instead of classifying its run again.
The executor now returns the compatibility `RandomIndexDecisionV111` beside
that mechanics result, so these transcript owners append the same selected
ticket that exact request replay consumed. Other run-only choice resumes
project their run through the same executor. Snapshot and transcript formats
are unchanged. Broader resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 draw-insertion classified handoff increment (2026-10-24):** The
controlled draw-insertion mechanics owner now returns the existing classified
pipeline result through `ResumeControlledDrawInsertionWithBoundary`. The
controlled-playable adapter passes that result into turn staging, while
`ResumeControlledDrawInsertion` retains its run-only semantic-draw facade.
Automatic pipeline completions no longer lose their classified boundary
inside draw insertion. Snapshot and transcript formats are unchanged. Broader
resume consolidation and cross-provider mechanical equivalence remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 generated-card classified handoff increment (2026-10-24):** The
controlled generated-card mechanics owner now exposes
`ResumeControlledGeneratedCardWithBoundary` for controlled-playable replay.
That adapter carries the existing automatic-pipeline classification through
the selected-index executor into turn staging; semantic-choice consumers keep
the run-only facade. Snapshot and transcript formats are unchanged. Broader
resume consolidation and cross-provider mechanical equivalence remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 potion-generation classified handoff increment (2026-10-24):** The
controlled potion-generation mechanics owner now exposes
`ResumeControlledPotionGenerationWithBoundary` for controlled-playable replay.
It carries the automatic-pipeline result into turn staging; semantic-choice
consumers retain the run-only facade. Snapshot and transcript formats are
unchanged. Broader resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 pool-shuffle classified handoff increment (2026-10-24):** The
controlled pool-shuffle mechanics owner now exposes
`ResumeControlledPoolShuffleWithBoundary` for controlled-playable replay.
It carries the existing automatic-pipeline classification through the
selected-index executor into turn staging; semantic-choice consumers retain
the run-only facade. Snapshot and transcript formats are unchanged. Broader
resume consolidation and cross-provider mechanical equivalence remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 random-target classified handoff increment (2026-10-24):** The
controlled random-target resume now exposes
`ResumeControlledRandomTargetWithBoundary` for controlled-playable replay.
It returns the existing automatic-pipeline classification through the
selected-index executor into turn staging; semantic-choice consumers retain
the run-only facade. Snapshot and transcript formats are unchanged. Broader
resume consolidation and cross-provider mechanical equivalence remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 random-cost classified handoff increment (2026-10-24):** The controlled
random-cost resume now exposes `ResumeControlledRandomCostWithBoundary` for
controlled-playable replay. It carries the existing automatic-pipeline
classification through the selected-index executor into turn staging; other
consumers retain the run-only facade. Snapshot and transcript formats are
unchanged. Broader resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 card-transform classified handoff increment (2026-10-24):** The
controlled card-transform resume now exposes
`ResumeControlledCardTransformWithBoundary` for controlled-playable replay.
The original selection continuation also preserves its pipeline result, so
the selected-index executor stages the engine classification directly;
semantic-choice consumers retain the run-only facade. Snapshot and transcript
formats are unchanged. Broader resume consolidation and cross-provider
mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 draw-cost classified handoff increment (2026-10-24):** Transient
`PrototypeDrawCardsResumeResultV111` progress now carries an optional
automatic-pipeline boundary classification. Controlled draw-cost completion
preserves the engine result through hand-draw progress and exposes
`ResumeControlledDrawCostWithBoundary` to controlled-playable staging;
run-only callers retain the compatibility facade. Snapshot and transcript
formats are unchanged. Broader resume consolidation and cross-provider
mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 controlled-playable executor consolidation (2026-10-24):** All nine
controlled-playable indexed-choice adapters now use the shared executor with
`PrototypeAutomaticPipelineResumeResultV111`; the run-only executor wrapper
has been removed. The executor returns the consumed index decision beside the
classified resume result, and turn staging receives that classification.
Snapshot and transcript formats are unchanged. Broader resume consolidation
and cross-provider mechanical equivalence remain open. Static source review
and `git diff --check` only; no tests or builds were run.

**S4 semantic hand-cost classified handoff increment (2026-10-24):** The
indexed random-cost and physical hand-card cost continuations now return the
engine-classified automatic-pipeline result through
`SemanticChoiceResumeResultV111`. Semantic end-turn receives the original
boundary while existing run-only choice facades remain available. Snapshot and
transcript formats are unchanged. Broader resume consolidation and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic target and hand-cost selection handoff increment (2026-10-24):**
The typed semantic target and hand-cost selection facades now retain the
classified automatic-pipeline result through `SemanticChoiceResumeResultV111`
into end-turn publication. Existing run-only APIs remain projections. Snapshot
and transcript formats are unchanged. Broader resume consolidation and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 Knowledge Demon classified handoff increment (2026-10-24):** Semantic
Knowledge Demon replay now calls the typed engine action continuation and
preserves its automatic-pipeline classification through
`SemanticChoiceResumeResultV111`. The ordinary action-step path retains its
run-only projection. Snapshot and transcript formats are unchanged. Broader
resume consolidation and cross-provider mechanical equivalence remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 semantic card-selection classified handoff increment (2026-10-24):**
Automatic semantic card-selection replay now uses the typed pending-choice
resolver and carries its pipeline classification through
`SemanticChoiceResumeResultV111`. The serialized semantic selection frame is
unchanged. Source validation and transcript ownership remain with the card
selection owner. Broader resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 typed semantic-choice classified handoff increment (2026-10-24):**
Draw-insertion, generated-card, potion-generation, card-transform, and
pool-shuffle choice owners now preserve their automatic-pipeline result through
`SemanticChoiceResumeResultV111` into end-turn publication. Existing run-only
choice APIs remain compatibility projections; source validation and transcript
ownership stay with each choice family. Snapshot and transcript formats are
unchanged. Broader resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 semantic effect-draw classified handoff increment (2026-10-24):**
The controlled semantic effect-draw engine now returns its automatic-pipeline
classification through the semantic draw-choice validator and
`SemanticChoiceResumeResultV111`. Effect-draw hand progress also retains that
classification with the remaining count. Run-only projections and transcript
ownership are unchanged. Snapshot formats are unchanged. Broader resume
consolidation and cross-provider mechanical equivalence remain open. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 semantic effect-draw cost classified handoff increment (2026-10-24):**
Semantic card-effect draw-cost resumes now carry the automatic-pipeline result
through `PrototypeDrawCardsResumeResultV111` and
`SemanticChoiceResumeResultV111`. Remaining-count and reward-policy ownership
are unchanged, and the result is transient. Snapshot and transcript formats
are unchanged. Broader resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 semantic card-shortlist classified handoff increment (2026-10-24):** The
controlled shortlist mechanics owner and semantic-choice facade now preserve
the classified automatic-pipeline result through `SemanticChoiceResumeResultV111`.
End-turn publication receives that result directly, while existing run-only
callers retain their projection. Snapshot and transcript formats are
unchanged. Broader resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 semantic-choice result owner increment (2026-10-24):** Semantic choice
resume overloads that return only a run and source SiteId now use the shared
`SemanticChoiceResumeResultV111` value. Draw resumes that also carry requested
or remaining counts and typed decisions preserve those boundary-specific
results. The value is internal and ephemeral; transcript, chronology, and
snapshot ownership are unchanged. Broader automatic draw-resume consolidation
and cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 semantic draw-resume progress increment (2026-10-24):** The shared
`PrototypeDrawCardsResumeResultV111` now carries the resulting run and
remaining count across semantic draw execution, automatic hand-draw progress,
post-cost continuation, Stratagem selection, and controlled draw-cost replay.
Requested-count ownership and typed transcript chronology remain with their
existing mechanics and stochastic owners; snapshot fields are unchanged.
Broader automatic draw-resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S4 semantic draw-progress result increment (2026-10-24):** The shared
`SemanticDrawProgressResumeResultV111` now carries run, requested count,
and remaining count through automatic hand-draw entry, pre-draw and
random-pile resumes, plus standalone and automatic draw-hook resumes. Draw
and choice results compose this progress with their existing SiteId or typed
decision, while transcript and snapshot
ownership remain unchanged. Broader automatic draw-resume consolidation and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 single automatic-pipeline resume contract increment (2026-10-24):**
`ResumeAutomaticPipelineContinuation` now always returns the existing
`PrototypeAutomaticPipelineResumeResultV111`, including on suspended and
run-only paths. The separate classified wrapper was removed; mechanics-only
adapters project `Run`, while boundary-aware adapters retain the same
classification. Snapshot and transcript ownership are unchanged. Broader draw
resume policy consolidation and cross-provider mechanical equivalence remain
open. Static source review and `git diff --check` only; no tests or builds were
run.

**S4 automatic hand-draw boundary dispatch increment (2026-10-24):** The
`DrawPlayerHand` pipeline step now hands a completed `PrototypeDrawCardsResult`
to `ResumeAutomaticHandDrawBoundary`. That existing mechanics owner routes
semantic and legacy controlled boundaries to their existing adapters, which
retain source-boundary policies and transcript ownership. The pipeline step no
longer interprets those resumable boundary kinds itself. No snapshot or
transcript format changed. Broader draw-resume policy consolidation and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 effect-draw frame advancement increment (2026-10-24):** Initial
`ResolveOperations` effect draws now use
`AdvanceControlledSemanticDrawFrame` to combine deferred physical-card
identities and Stratagem state for semantic-draw, draw-cost, and after-shuffle
boundaries. This shares the same mechanics transition used after resumed
semantic effect draws; operation queues and provider transcripts stay with
their owners. No snapshot format changed. Broader draw-resume policy
consolidation and cross-provider mechanical equivalence remain open. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 automatic draw-cost resume dispatch increment (2026-10-24):** Automatic
legacy and semantic draw-cost continuations now pass through
`ResumeAutomaticHandDrawCost`. It owns the common post-cost hand-draw handoff
and routes the tagged `DrawCards` result to the existing controlled or
semantic adapter while retaining their provider-specific suspension policy,
progress owner, and transcript behavior. Snapshot and transcript formats are
unchanged. Broader draw-resume policy consolidation and cross-provider
mechanical equivalence remain open. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 automatic draw-cost progress result increment (2026-10-24):** The
automatic draw-cost resume's private run/requested-count pair now composes the
provider-neutral `PrototypeDrawCardsResumeResultV111`. Pending cost stages
retain semantic requested-count progress, and resumed draw boundaries validate
and propagate that count through the common hand-draw router. Controlled
progress remains without a semantic requested count. Snapshot and transcript
formats are unchanged. Broader draw-resume policy consolidation and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 provider-neutral draw resume result increment (2026-10-24):** The shared
draw-resume handoff is now named `PrototypeDrawCardsResumeResultV111` and
defined with the other provider-neutral mechanics values. Semantic and legacy
controlled adapters use it for the resumed run and remaining count; choice
transcripts and provider provenance remain with their existing owners. No
snapshot or transcript format changed. Broader draw-resume policy
consolidation and cross-provider mechanical equivalence remain open. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 saved automatic draw boundary routing increment (2026-10-24):**
`ResumeAutomaticHandDrawBoundary` now routes both initial source-step
suspensions and completed resumes of saved automatic draws. The source-step
path leaves ordinary stage completion with the pipeline; the saved-resume
path sends complete and pending results through the existing semantic or
controlled adapter. Both paths return the provider-neutral draw-resume result.
Provider policies, transcript owners, and snapshots are unchanged. Broader
draw-resume policy consolidation and cross-provider mechanical equivalence
remain open. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 controlled automatic draw-resume caller increment (2026-10-24):** Saved
controlled hook, recycle-Stratagem, and draw-insertion continuations now enter
the common automatic boundary router before falling back to their existing
legacy adapter for unsupported result kinds. Provider checkpoint validation,
RNG publication, and transcript ownership are unchanged. Broader draw-resume
policy consolidation and cross-provider mechanical equivalence remain open.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 nested CardDrawn continuation owner increment (2026-10-24):** Initial
effect draws in `ResolveOperations` now share
`AttachDrawCardsAtCardDrawnChoice` with resumed semantic effect draws. The
common mechanics owner advances deferred physical-card identities, prepends
the remaining draw operation to the saved effect suffix, and applies the
standalone hook marker. The operation queue, RNG, and provider transcript
owners remain in their existing boundaries. Broader draw-resume policy
consolidation and cross-provider mechanical equivalence remain open. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 provider-neutral draw continuation owner increment (2026-10-24):**
`AdvanceControlledSemanticDrawFrame` and
`AttachDrawCardsAtCardDrawnChoice` now live in
`PrototypeDrawCardsContinuationV111`, where initial effect resolution and
resumed semantic draws use the same state-only mechanics. Run publication and
provider transcripts remain with their existing owners. No snapshot or
transcript format changed. Broader draw-resume policy consolidation and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 effect-draw progress owner increment (2026-10-24):** Initial effect-draw
recycle, nested hook, semantic, cost, after-shuffle, and Stratagem boundaries
now build their saved frame through `CreateAdvancedEffectDrawFrame`. This
applies the shared remaining-count and deferred-identity transition while
preserving the queued operation fields and continuation suffix. Normal draw
completion also uses that identity prefix for keyword overrides. No snapshot
or transcript format changed. Broader draw-resume policy consolidation and
cross-provider mechanical equivalence remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 draw-frame to operation projection increment (2026-10-24):** Effect
recycle, nested hook, semantic cost, and Stratagem continuations now project a
`PrototypeControlledSemanticDrawV111` through the common
`CreateDrawResumeOperation` factory. This preserves all frame fields,
including `DrawEqualSelectionCount`, while cloning queued payloads at the
existing operation boundary. The value is transient; snapshot schemas are
unchanged. Broader draw-resume policy consolidation and cross-provider
mechanical equivalence remain open. Static source review and `git diff --check`
only; no tests or builds were run.

**S6 Native-regional Toy Box purchase continuation increment (2026-10-24):**
`ReferenceNatureMerchantSessionV111` now also accepts a legal Native-regional
Toy Box purchase when the source run has no Native RNG. It replays the five
legacy relic-rarity tickets and five Nature-only relic-identity selections
against the sorted residual bag, validating each request, rarity owner,
candidate support, and prior pull before committing the purchase and rewards
with the existing Nature cursor. Ticket and identity index weights remain
`UNVERIFIED`; configured Native Toy Box behavior is unchanged. Static source
review and `git diff --check` only; no tests or builds were run.

**S6 Native-regional PotionFactory merchant continuation increment (2026-10-24):**
The existing merchant session now validates all three legacy rarity-ticket and
model-index pairs during Native-regional fallback Shop entry, then accepts a
legal Native-regional Alchemical Coffer purchase with four such pairs. Replay
checks each rarity owner and ticket, source-ordered rarity-conditioned residual
pool, model owner and selection bound. Replay retains the captured stream
ordinals. Fallback model indices use equal weights and legacy stream behavior
remains `UNVERIFIED`; configured Native Coffer draws are unchanged. Static
source review and `git diff --check` only; no tests or builds were run.

**S6 Native-regional registered Shop purchase continuation increment (2026-10-24):**
`ReferenceNatureMerchantSessionV111` now admits every current legal
Native-regional Shop purchase when `NativeRunRng` is absent, routing the
existing legacy merchant sites and registered purchase-index sites through
its same atomic replay. Site allowlists and ordered-support checks remain
shared with the run-action continuation; unregistered stochastic hooks remain
fail-closed. No session or snapshot schema was added. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 guarded semantic-choice resume scope increment (2026-10-24):**
`ControlledChanceGuardV111.ExecuteGuarded` now owns the synchronous guard
scope and optional engine-owned reward allowance shared by typed semantic
draw, cost, target, generation, insertion, card-selection, transform,
shortlist, pool-shuffle, potion-generation, draw-hook, and Knowledge Demon
resumes. Their source-boundary validation, result checks, and transcript
policies remain with their existing choice owners. No snapshot or transcript
format changed. Broader resume consolidation and cross-provider mechanical
equivalence remain open. Static source review and `git diff --check` only; no
tests or builds were run.

**S3 hidden draw-pile comparison owner increment (2026-10-24):**
`SemanticDrawPileAdapterV111.IsUnchangedDrawPile` now owns comparison of
physical draw-pile order and the semantic deck state, and
`RequireUnchangedDrawPile` delegates to it. Semantic player-action validation,
semantic-pilot card and potion continuation checks, and full-pool shuffle use
the shared predicate for their conditional mutation policies. Their source
allowlists and failure handling are unchanged. No snapshot format changed.
Static source review and `git diff --check` only; no tests or builds were run.

**S1 Endless Conveyor outcome-blind query increment (2026-10-24):** Chance Law
builders now consume a declared pre-draw Event state and request ordinal. The
ReferenceNature session projects the same source action with its selected
prefix and suspends at the exact pending typed request; it passes the captured
pre-draw state to the query without resolving that outcome. Upgrade and
Colorless-card laws receive their request ordinals from the pending request
instead of reading the Native event cursor. Inventory schema v57 guards all
136 Chance Law query methods against direct sampling, cursor reads, boundary
journals, replay captures, and engine steps; it reports no violations. Static
source inventory and `git diff --check` only; no tests or builds were run.

**S4 provider-neutral controlled hand-draw result increment (2026-10-24):**
Legacy controlled automatic hand-draw suspension and completion now return
`PrototypeDrawCardsResumeResultV111` directly to the shared boundary router,
matching the semantic adapter. The router no longer wraps a bare `RunState`
with a separately reconstructed remaining count. Suspended counts and completed
zero progress are preserved; provider policies and saved state remain
unchanged. Static source review and `git diff --check` only; no tests or builds
were run.

**S0 forwarded request census increment (2026-10-24):** Census v60 now traces
Neow's forwarded request ID through its fail-closed five-site session list and
the start-run boss request through the Overgrowth/Underdocks Chance Law
branches, including the shared `choose_index` operation. The 113 typed request
constructions now all resolve to registered sites or finite alternatives;
zero remain dynamic or missing, and no static ID is absent from the registry.
The query-law resolver follows owner-named `ChanceLaw*.cs` partials. The direct
census and contract-link checks passed; manual row-by-row and external-source
completeness review remain open. No tests or builds were run.

**S6 pending relic deck-transform action increment (2026-10-24):** The
controlled run-action continuation now captures legal Shop and Reward card
selections when a relic acquisition has queued a random persistent-deck
transform. It fingerprints and replays the exact selection, validates the
source relic and selected card, and checks the ordered replacement pool before
committing the transformed deck and ReferenceNature cursor. Fixed-result
transforms and other deck-choice owners remain on their existing paths; pool
fidelity and equal-index weighting remain `UNVERIFIED`. Static source review
and `git diff --check` only; no tests or builds were run.

**S1 controlled relic-transform identity outcomes increment (2026-10-24):**
The controlled run-action Chance Law now reuses the existing legacy transform
query's ordered `card:<model>` outcomes after checking the pending request and
candidate labels against that law. Its event ID remains bound to the exact
saved action boundary, while replay continues to resolve by the same selected
index. Pool fidelity and equal-index weighting remain `UNVERIFIED`. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 Distilled Chaos nested Tingsha target increment (2026-10-25):** Manual
Distilled Chaos now admits Tingsha's random target when it suspends inside one
of the potion's top-three autoplayed cards. Admission checks the live Tingsha
dispatch, active autoplay series, exact `PotionUsed` root, and remaining
potion-sourced autoplay queue. Static source review and `git diff --check`
only; no tests or builds were run.

**S4 draw-result ownership and Tingsha source census increment (2026-10-25):**
The tagged `DrawCards` boundary/result value, constructors, and mechanical
result validation now live together in `PrototypeDrawCardsBoundaryV111.cs`;
provider adapters retain their existing continuation and transcript policies.
A source census of modeled card definitions accounts for all nine direct
player-card discard origins admitted for Tingsha. A census of modeled potion
effects finds Gambler's Brew and Distilled Chaos as the only potion origins for
discard events, and both use existing continuations. Unmodeled content and
Native target-pool/stream/cursor parity remain open. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 shared automatic hand-draw mechanics ownership increment (2026-10-25):**
Progress publication, stage completion, semantic completion receipts, and
post-draw power timing now live with the provider-neutral automatic hand-draw
resume owner. Legacy controlled and semantic adapters continue to own their
different suspension policies and transcripts. The move also corrected a
missing statement terminator in the shared completion method. Static source
review and `git diff --check` only; no tests or builds were run.

**S4 committed Nature opening-draw entry increment (2026-10-25):** The source
opening hand draw now uses the shared semantic draw profile when its committed
physical pile is represented by a Nature deck, allowing already-known card
identities to pass through the common `DrawCards` kernel without a second
draw request. Its coupled player/combat result now publishes through the
existing `PublishDrawMechanicsState` owner. Unexpected opening draw boundaries
remain fail-closed until an opening-stage continuation owns them. Static source
review and `git diff --check` only; no tests or builds were run.

**S4 draw-result publication ownership increment (2026-10-25):** The existing
`PrototypeDrawCardsBoundaryV111.cs` now owns both tagged-result validation and
the provider-neutral player/combat publication helpers. Automatic, nested,
semantic, controlled, and opening draws therefore publish coupled mechanics
through the same owner while retaining their existing provider policies and
transcripts. Static source review and `git diff --check` only; no tests or
builds were run.

**S4 shared draw-progress rule ownership increment (2026-10-25):** The
provider-neutral draw continuation owner now holds `GetCardsDrawnSince` and
`ExpectedStratagemSelectionCount`, which were previously placed in the
semantic draw adapter despite use by automatic and controlled paths. The
automatic hand-draw resume owner now defines the shared pipeline step index.
All draw boundaries now share those step, count, and capacity rules. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 saved automatic hand-draw kernel ownership increment (2026-10-25):** The
provider-neutral draw continuation owner now validates saved hand-draw count
against the `DrawPlayerHand` cursor, clears progress through the common
publication boundary, and re-enters the draw kernel using the adapter's
suspension policy and local RNG. Controlled and semantic adapters share this
operation before interpreting the tagged result. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 automatic hand-draw progress-position increment (2026-10-25):** Stratagem
and nested `CardDrawn` suspension now share the check that locates active or
pre-draw hand-draw progress and rejects saved progress outside its source
stage. This keeps the same cursor invariant at both suspension boundaries.
Static source review and `git diff --check` only; no tests or builds were run.

**S4 final-card opening hook increment (2026-10-25):** `DrawOpeningHand` now
allows a nested `CardDrawn` choice when the last requested opening card has
already been drawn. The ordinary opening cursor resumes at `AfterHandDraw`
after that choice resolves, so no draw count needs to be serialized. Opening
choices with cards remaining and recycle boundaries remain fail-closed. Static
source review and `git diff --check` only; no tests or builds were run.

**S4 resumable Nature opening draw increment (2026-10-25):** Opening semantic
draw and draw-cost boundaries now save the existing semantic draw frame, and
nested `CardDrawn` choices prepend the remaining hand draw to the existing
choice continuation. The shared automatic-pipeline admission recognizes these
frames only at the opening `AfterHandDraw` cursor. Recycle and after-shuffle
insertion still require a source-supported shuffle continuation. The shared
nonautomatic effect-cost path also now passes a null automatic requested-count
value. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 shared saved draw-progress query ownership increment (2026-10-25):** The
provider-neutral draw continuation owner now resolves semantic remaining-count
progress and locates standalone draw operations in saved choice frames. Semantic
combat-draw, end-turn, pilot, and typed choice-resume paths use the same
mechanics queries; provider transcript and suspension policies are unchanged.
No snapshot schema changed. Static source review and `git diff --check` only;
no tests or builds were run.

**S4 opening after-shuffle insertion continuation increment (2026-10-25):** A
Nature-owned opening hand draw now routes its after-shuffle relic insertion
through the existing typed draw-insertion continuation, preserving the
remaining draw frame and opening pipeline cursor. Admission requires the exact
opening `AfterHandDraw` cursor, a live Nature deck, an empty source continuation,
and the validated after-shuffle relic suffix. Nature recycle itself retains
its existing shuffled-set constraints; no recycle ticket or new snapshot field
was added. Static source review and `git diff --check` only; no tests or builds
were run.

**S4 opening semantic draw dispatcher consolidation (2026-10-25):** Opening
semantic draw, draw-cost, and nonfinal `CardDrawn` hook boundaries now use
`ResolveControlledSemanticDrawResult` with the opening frame, alongside the
after-shuffle insertion path. The final-card hook remains without a saved draw
operation because no remainder exists. Static source review and
`git diff --check` only; no tests or builds were run.

**S4 opening recycled-draw Stratagem increment (2026-10-25):** Nature-owned
opening draws now route the tagged `ShuffleSelection` boundary through the
shared draw dispatcher. The existing semantic card-selection owner admits
only the exact opening `AfterHandDraw` cursor, expected Stratagem pick count,
current draw-pile support, and single saved hand-draw operation. Resolving the
selection resumes that operation before opening advances. Static source review
and `git diff --check` only; no tests or builds were run.
