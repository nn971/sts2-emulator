# Nature #143 — controlled legacy enemy summons

## S0 stochastic site contract

This continuation covers `combat.enemy.summon_pool.select`,
`combat.initial_unique_hp`, and `combat.tough_egg.hatchling_hp` when those
requests occur in the supported legacy enemy-action frame. The summon pool is
ordered after the source Fabricator last-spawn exclusion, with a full-pool
fallback if every entry is excluded. HP support is constrained by each
encounter's declared range and the already-selected unique HP values. Native
initial HP and Tough Egg ownership are documented in the [native HP audit](reference-builds/v0.111.0-native-combat-hp-ai-rng.md) and [Tough Egg audit](reference-builds/v0.111.0-native-tough-egg-hatch.md).

The read-only Native per-index laws are available through `chance_law_query`.
`combat.initial_unique_hp` takes `owner_id="creature:<CombatId>"`, an
ordered `ordered_hp_support`, and the source ordinal. Its outcomes are
support indices, with the request fingerprint binding the HP values and their
order. `combat.tough_egg.hatchling_hp` takes the same creature owner, source
ordinal zero, and Ascension; it derives 19–22 HP below Ascension 8 and 20–23
HP at Ascension 8 or higher. Both queries return exact `run.niche` `NextInt`
masses without reading a saved run seed or cursor. They cover only the
declared or derived index support: unique-HP pool reconstruction, event
eligibility, and end-to-end cursor parity remain with separate evidence.

The player observation reveals the committed summon and current enemy HP, not
the eligible summon list or unselected HP values. The saved enemy-action
continuation replays prior decisions and resumes the mechanics owner once the
pending choice is resolved. Native summon-pool selection remains fail-closed
until its source RNG assignment is audited; unsupported nested mechanics do
not publish partial support. The unique-HP and Tough Egg Chance Laws use
exact Native `NextInt` grid masses and are `SOURCE_VERIFIED` for the bounded
index transform on their declared HP supports. The summon-pool law, legacy
modulo probabilities, Native stream attribution, and cursor parity remain
`UNVERIFIED`.

The saved enemy-action replay can suspend at the modeled legacy
`combat.enemy.summon_pool.select` site. It preserves the current source-shaped
pool, including the Fabricator last-spawn exclusion and fallback to the full
pool when all entries are excluded. Replaying the choice resumes the enemy
action and creates the selected summon through the existing combat kernel.

If the selected summoned enemy has a variable HP range, the same replay frame
then exposes `combat.initial_unique_hp` as a second `enemy_action_chance` event.
The event's `candidate_labels` identify summon models and pool slots, or the
bounded HP values. JSONL callers can resolve those outcomes by `candidate_label`
while `selected_index` remains compatible. Earlier summon choices are injected
again from the checkpoint before the HP choice is reached. The source action
commits the selected HP and continues the enemy stage once all decisions are
resolved.

The frame also resumes a legacy Tough Egg hatch that rolls variable Hatchling
HP through `combat.tough_egg.hatchling_hp`. Native hatching remains routed
through its separately audited run-level HP provider.

When the semantic end-turn frame suspends on an exact registered Native
enemy-action request, the run-owned `SemanticOtherChanceStateV111` consumes
that request and replays the same frame with its selected index. This covers
supported summoned-creature HP and the isolated Native Ovicopter Tough Egg
HP site on `run.niche`; it does not extend Native summon-pool support or the
source/cursor parity claim.

The summon-pool continuation covers the existing legacy combat model. Native
summon-pool selection remains fail-closed until its source RNG assignment is audited.
The Native HP transform is source-verified on its declared support; legacy
modulo probabilities remain `UNVERIFIED`. The new nested controlled mechanics
path has build evidence only.
