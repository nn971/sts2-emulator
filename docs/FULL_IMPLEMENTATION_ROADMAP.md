# Complete single-player STS2 emulator roadmap

Recorded 2026-10-10 for `nn971/sts2-emulator`. Baseline: emulator revision `3e897c8`.

The goal is a fully implemented, deterministic, headless emulator covering
complete runs through **Overgrowth or Underdocks → Hive → Glory**, every
single-player character, encounter, event, card, potion and relic.
Multiplayer-only content and mechanics are excluded from implementation,
validation and coverage denominators. Shared content follows single-player rules.

This is the active implementation plan. Earlier prototype milestones remain
historical evidence, not the definition of completion. Search, training,
experiment management, strategy and presentation remain outside this repository.

## Version and evidence

- Game version: **v0.111.0**; game commit: **41cef1ea**.
- Binary fingerprint: `3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e`.
- [Build manifest](../data/reference_builds/v0.111.0-41cef1ea.json).
- Structured corpus: `ptrlrd/spire-codex@b59c6f96043051afee8683237d04f943309ced35`;
  per-file hashes: `tools/reference_source/spire_codex_v0.111.0_git_blobs.tsv`.
- Audited source snapshot: `Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20`.

Use pinned structured data for inventory, audited executable semantics for
behavior, and focused reference probes for unresolved timing, RNG and decision
boundaries. Parser metadata is not an executable specification. External data,
assemblies and decompiled source remain local and ignored under the
[data policy](LEGAL_AND_DATA_POLICY.md).

## Existing foundation

| Area | Baseline | Remaining |
| --- | --- | --- |
| Engine | Whole-run state, independent forks, hashes, resumable choices, ordered combat hooks, maps, rewards, shops, events, transitions | General configuration and remaining semantics/parity |
| Silent | All 86 single-player entries in the 91-card native pool marked implemented; five multiplayer cards excluded | Variant-aware semantic and integration validation |
| Colorless | 65-card pool inventoried; first six implemented | Remaining eligible shared cards |
| Overgrowth | All 22 encounters marked implemented for models, formations and pools | Remaining events/run rules, progression conditions and reference validation |
| Underdocks | 4/4 weak encounters, 2/10 normals, no implemented elites/bosses | Eight normals, three elites, three bosses, regional events and complete integration |
| Hive / Glory | Prototype later-act flow and some reusable enemy mechanics | Native acts and all their content |
| Other characters | Reusable kernel | Ironclad, Defect, Regent, Necrobinder |
| RNG | Six prototype streams; native inventory and oracle state-evolution audit | Native integration, ownership and call-order parity |
| Interface | Versioned prototype JSONL API, semantic actions, capability IDs | Complete coverage report, save/load, bindings, compatibility |

These are implementation claims, not exact native parity. The archive inventories
595 cards, 298 relics, 64 potions, 265 powers, 115 monsters and 66 events **before
single-player filtering**; derive the actual eligible denominator rather than
treating these totals as implementation targets. Existing Act 1 bookkeeping
excludes event-only combats; the final inventory must include them.

## Delivery rules

Extend the current engine. Content breadth is the main implementation track;
native fidelity and measured performance are continuous tracks. Intermediate
playable slices can have explicit fidelity gaps; final completion requires all
release gates below.

For each batch: record pin/provenance and dependencies, implement typed behavior,
integrate correct pools/acquisition paths, add focused semantic tests, run relevant
regressions and update coverage. Probe the native game for unresolved runtime
facts. Missing native content must not silently become a prototype substitute.

Track four independent dimensions per item: **catalogued**, **implemented**,
**integrated**, and **validated**. Registered metadata is not executable coverage.
Preserve explicit prototype compatibility through versioned adapters.

## 1. Completion contract and executable inventory

- [ ] Derive all eligible single-player content from the pinned corpus.
- [ ] Extend capabilities with implementation/integration/evidence status and
      a reproducible missing-content report.
- [ ] Inventory all characters, act pools, special/event encounters, event
      branches, Ancient choices, cards/upgrades/generated cards, potions, relics,
      powers, orbs, enchantments, afflictions and rest options.
- [ ] Exclude multiplayer-only behavior from denominators and generation.
- [ ] Enforce v111 build identity in native-mode state, snapshots and traces;
      distinguish legacy prototype identity and semantics.
- [ ] Detect changed inventories and stale coverage claims in CI.

**Gate:** every remaining eligible item is identifiable; missing behavior,
unreachable content and missing validation are distinguishable.

## 2. Configurable characters and acts

- [ ] Represent character, Ascension, profile/unlocks, ruleset and act identity
      explicitly in run configuration and persistent state.
- [ ] Define character starting HP/deck/relics/resources/pools and unique state.
- [ ] Remove Silent assumptions from initialization, rules lookup and reporting.
- [ ] Generalize act transitions for Overgrowth, Underdocks, Hive and Glory.
- [ ] Keep resources, pools, counters, history and continuations fork-safe;
      avoid mutable global run configuration.
- [ ] Maintain versioned consumer compatibility.

**Gate:** initialization, serialization, forks and observations support multiple
characters and all four act identities, with explicit unsupported behavior.

## 3. Complete both Act 1 variants

- [ ] Close Overgrowth event, map, progression and validation gaps.
- [ ] Add remaining Underdocks normals: Cultists, Fossil Stalker, Gremlin Merc,
      Haunted Ship, Living Fog, Seapunk Normal, Sewer Clam, Two-Tailed Rats.
- [ ] Add elites: Phantasmal Gardeners, Skulking Colony, Terror Eel.
- [ ] Add bosses: Waterfall Giant, Soul Fysh, Lagavulin Matriarch.
- [ ] Complete all ten regional Underdocks events and every eligible branch.
- [ ] Complete formations/slots, move policies, summons, ally-death hooks,
      phases and Ascension breakpoints.
- [ ] Complete native encounter bags/history/adjacency/eligibility and pool
      exhaustion/replenishment rules; remove prototype event substitution.

**Gate:** Silent completes either Act 1 using correct regional content; every
eligible regional entry is integrated, reachable and tested.

## 4. Native Hive and Glory

- [ ] Audit the documented 20 Hive and 18 Glory encounters against v111.
- [ ] Implement every weak, normal, elite and boss encounter plus all additional
      special/event combats found by the global inventory.
- [ ] Implement map/room rules, encounter selection/history and difficulty variants.
- [ ] Implement regional events, act-entry Ancients, rewards and boss conditions.
- [ ] Replace native-mode prototype later-act fixtures; complete persistent-state
      transfer, transitions and terminal outcomes.
- [ ] Integrate one full route early, then close every remaining encounter/branch.

**Gate:** Silent completes both `Overgrowth → Hive → Glory` and
`Underdocks → Hive → Glory`; all eligible act content is integrated. Successful
random runs alone do not prove exhaustive coverage.

## 5. Shared cards and card modifications

- [ ] Complete remaining eligible colorless cards and common/basic, curse,
      status, event, quest and generated-card families.
- [ ] Implement every eligible upgrade and modified card-instance variant.
- [ ] Complete enchantment/affliction acquisition, persistence, copying,
      temporary mutation, suppression, removal and cleanup.
- [ ] Complete transformation and random-generation pools/exclusions.
- [ ] Extend resumable draw/damage/discard/exhaust/death loops where content
      introduces choices inside those loops.
- [ ] Audit Silent mechanics, especially replay/autoplay, choices and identity.

**Gate:** every eligible shared card/modification executes correctly, has proper
acquisition/generation paths and receives variant-aware validation.

## 6. All five characters

Retain Silent as the integration reference. Recommended order:
**Ironclad → Defect → Regent → Necrobinder**.

| Character | Foundations |
| --- | --- |
| Ironclad | HP-loss/self-damage hooks, Exhaust, Strength, persistent scaling |
| Defect | Orb slots, FIFO channel/evoke, Lightning/Frost/Dark/Glass/Plasma, Focus, targeting |
| Regent | Stars alongside energy, Forge, Sovereign Blade, generated Minions |
| Necrobinder | Osty as a secondary creature, Summon, damage routing, Souls, Doom |

- [ ] Complete Ironclad configuration and eligible content.
- [ ] Complete Defect configuration and eligible content.
- [ ] Complete Regent configuration and eligible content.
- [ ] Complete Necrobinder configuration and eligible content.
- [ ] Cover starting state, cards/upgrades/generated cards, character relics,
      potions and event/reward interactions for every character.
- [ ] Verify cross-character acquisition wherever single-player rules permit it.

**Gate per character:** every eligible card is implemented/integrated; full runs
work through both Act 1 variants and later acts at every supported difficulty.

## 7. All relics and potions

Implement dependencies alongside earlier milestones, then close the inventory.

- [ ] Complete relic pickup/removal, counters, hooks and persistence.
- [ ] Complete relic effects on maps, rewards, shops, healing, decks, combat/rests.
- [ ] Complete potion targeting, legal phases, capacity/replacement, generation,
      consumption, automatic activation and effect modification.
- [ ] Complete valid acquisition paths, pool restrictions and interactions.

**Gate:** every eligible item is integrated and validated for meaningful behavior
and interactions; acquisition-only tests do not establish completeness.

## 8. Events, Ancients, economy and progression

- [ ] Complete every page, precondition, choice, cost, random outcome, follow-up,
      card selection and event combat transition.
- [ ] Complete Neow, later Ancients, cross-act and character-specific branches.
- [ ] Complete shop services/prices/inventory, card removal, rarity/pity, potion
      odds, relic bags, rest and treasure behavior.
- [ ] Model unlock/discovery conditions through profile state and provide an
      explicit fully unlocked simulation profile.
- [ ] Reuse canonical deck/inventory/combat operations for event outcomes.
- [ ] Test unavailable options, depleted pools and delayed outcomes directly.

**Gate:** every eligible event branch and run-system rule is implemented,
reachable under its proper conditions and directly testable.

## 9. Exact native RNG and replay parity

Continuous during expansion; mandatory for the final release.

- [ ] Integrate 12 native run streams, three player streams and context-specific
      map/encounter/monster/event/content generators.
- [ ] Match xxHash64 seeds, SplitMix64 initialization, xoshiro256**, bounded
      sampling, shuffle and variable-draw distributions.
- [ ] Match stream ownership/call order and outcome-pool filtering/order/history;
      replacing the PRNG alone is insufficient.
- [ ] Extend reference capture/replay across all characters, acts and difficulties.
- [ ] Compare legal actions, nested choices, decision-boundary state and RNG.
- [ ] Minimize divergences into focused regression fixtures.
- [ ] Validate independent forks and deterministic snapshot restore.

**Gate:** no unexplained release-corpus divergence; rare behavior has targeted
fixtures. Follow the [RNG fidelity matrix](reference-builds/v0.111.0-native-rng-fidelity-matrix.md).

## 10. Stable, practically fast release

- [ ] Finish versioned state/action schemas, save/load and compatibility negotiation.
- [ ] Finish production bindings and deterministic batch APIs.
- [ ] Benchmark whole runs, transitions, choices, forks, serialization and
      memory per branch with representative workloads.
- [ ] Optimize measured bottlenecks while preserving verified replay results.
- [ ] Publish reproducible coverage/fidelity/performance reports.

**Gate:** consumers have stable documented contracts and practical measured
throughput; all semantic/parity regressions remain passing.

## Final release checklist

- [ ] Every eligible v111 single-player entry is implemented and integrated.
- [ ] All five characters complete both Act 1 routes through Hive and Glory.
- [ ] Every encounter, event branch, card variant, potion and relic has evidence.
- [ ] All supported Ascension levels and distinct difficulty breakpoints are covered.
- [ ] Native execution contains no unrelated prototype fallback behavior.
- [ ] Forks/restored snapshots preserve future outcomes and hidden state stays
      separate from fair player observations.
- [ ] At least 1,000 version-pinned complete or terminal real-game runs replay
      without unexplained divergence, supplemented by rare-content fixtures.
- [ ] Legal actions, choices, build identity and continuation-relevant RNG agree.
- [ ] Multiplayer-only mechanics are excluded from coverage and execution.
- [ ] Stable interface and reproducible performance gates pass.

## First implementation batches

1. Coverage/pin audit and honest evidence-aware inventory/reporting.
2. Fork-safe character/act configuration and versioned adapters.
3. Remaining Underdocks normals, followed by elites, bosses and regional events.
4. Native Hive/Glory, shared content and remaining characters.
5. Global content/fidelity closure and release/performance gates.

Future game versions are a separate maintenance task: record a new pin, produce
an inventory/behavior diff, update fixtures and explicitly version behavior.
Do not silently move this completion target beyond v0.111.0.
