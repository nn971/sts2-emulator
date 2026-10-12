# v0.111 combat fidelity: next native capture gate

**Target:** broad, reusable single-player combat evidence for Silent against the
pinned v0.111 game build. **Exact RNG cursor/stream parity is deferred.**
The target is independent, observable state transitions, actual action source
callbacks, hook/effect execution ordering, and semantic player choices. A
collection of consistent checkpoints is evidence *for the observed mechanics*;
all-combat fidelity requires coverage across mechanic families.

## Current evidence and code boundaries

- A genuine Silent STRIKE_SILENT → SLUDGE_SPINNER pre-state was imported into
  real `PrototypeGameEngine.Step` and matched **47/47** observed fields; see
  PR #127/#128. The corresponding v111 emulator event journal is in PR #129.
- PR #121 Inspector records strict `0.2` decisions, selected actions,
  CombatHistory projections and successor checkpoints. Some traces end early
  on a nested Survivor choice or an action that predates the frame readiness
  poll.
- PR #130 introduces **a separate all-executor action sidecar**. It records
  every observed `ActionExecutor.BeforeActionExecuted` /
  `AfterActionExecuted`, including actions outside a human legal menu,
  records `BeforePausedForPlayerChoice`, and preserves ordered
  `CombatHistory.Changed` entries. It keeps recording even after the strict
  trace stops; frame polling adds deduplicated state snapshots that can
  show UI-driven changes between executor callbacks.
- The sidecar records actual callback order and full currently accessible
  canonical combat *visible* state. The source's deeper effect hook invocation
  order is a **separate, unfinished observation layer**. A history projection
  must never be substituted for an effect-hook invocation timestamp.

## Two independently useful files per observed encounter

| File | Contract | On incomplete choice |
| --- | --- | --- |
| `passive-*.jsonl` | Strict player decision / action / checkpoint; validates hash/event cursor | Terminates fail-closed |
| `passive-*.actions.jsonl` | All executor action start/end, nested-choice pause, history entries and changed-frame states | Keeps recording while combat continues |

The sidecar is deliberately **not** schema `0.2`; its JSONL header declares
`native-action-envelope-v1`. Events use monotone `ordinal` and executor
`action_id` / `parent_action_id`. A `history_baseline` records entries
present before attaching the observer; these earlier entries are not falsely
attributed to newly observed callbacks. `observation_failure`,
`history_reset`, orphaned or unfinished actions and write queue failures
must produce an incomplete status.

`frame_state` records only changed canonical snapshots, with an increasing
frame index. This is especially valuable for identifying a Survivor discard
selection after the nested-choice pause. Since polling occurs at frame
boundaries, it can miss intra-frame changes and is **not** evidence of hook
invocation order.

## Supported observations and remaining uncertainty

| Mechanic | Native capture from next run | Mechanical parity gate |
| --- | --- | --- |
| Card plays / action identities | Executor action envelopes, optional canonical card ID and target | Run genuine engine step from an independently verified pre-state |
| Energy, HP, block, buffs and debuffs | Per-action and changed-frame canonical snapshots, history projections | Field-wise comparison plus effect operation/source audit |
| Card draw, discard, exhaust, deck order | All four card zones with stable instance IDs at snapshots | Compare card movement and source trigger order; separately audit hidden draw order |
| Auto-play / automatic and enemy actions | Every observed executor callback, including unknown native types | Map callbacks to emulator operation/effect semantics; verify enemy move history |
| Nested player choices | Pause signal, enclosing action identity and changed snapshots | **Missing:** source-backed choice identity, legal options and accepted selection |
| Relics and potions | Effects may be visible via resulting state/history | **Missing:** complete inventory, mutable relic/potion state and hook callbacks |
| Combat entry, start-of-turn, end-of-turn, combat exit | Partial first-frame baseline, later executor/history observations | **Missing:** entry-time hooks before observer installation |
| Damage and death / summons | Creature identity/HP changes and observed history events | Compare blocking, death, new instance IDs, reactive effects, delayed triggers |
| Random choice sites | Actual outcomes and resulting state preserved | Compare conditional laws or inject observed outcomes; exact RNG cursor matching deferred |

### Definition of gate status

- **Capture integrity:** action IDs and ordinals complete; the sidecar auditor
  returns `CALLBACK_TIMELINE_CONSISTENT`. A strict trace may independently be
  `INCOMPLETE_CAPTURE` yet retain usable sidecar evidence.
- **Observed behavior fidelity:** for every covered case, the emulator executes
  an independent action from an audited native pre-state and the supported
  post-state fields agree.
- **Hook ordering fidelity:** native source callbacks and emulator source
  dispatch/operation events are observed independently and normalized by
  audited semantics, including nested continuations.
- **Coverage breadth:** enumerate and demonstrate every combat mechanic family
  in a test corpus. A single combat or a single run samples only some of them.
- **Full combat fidelity:** requires all applicable mechanics above plus hidden
  continuation and decision handling. Currently **NOT_TESTED**.

A random-dependent step may yield different outcomes. Mark the first
uncontrolled chance site and compare deterministic *preceding* effects.
Subsequent steps require either the same externally supplied outcome or a
verified comparison of the probability rule conditioned on player-visible
information. Random outcome equality by coincidence is weak evidence.

## User-local next native session

Use a **new isolated worktree** based on PR #130's branch and rebuild the
read-only Inspector against the pinned, locally installed game:

```fish
cd ~/projects/sts2-inspector-test
git fetch origin feature/v111-native-all-action-envelopes
git worktree add ~/projects/sts2-fidelity-next-capture \
    origin/feature/v111-native-all-action-envelopes
cd ~/projects/sts2-fidelity-next-capture
# Supply the actual installed game root as the argument.
scripts/build-native-inspector.fish "$GAME_ROOT"
```

Game environment still requires `STS2_FIDELITY_CAPTURE=1`,
`STS2_FIDELITY_EXPECT_SHA256` set to the local `sts2.dll` SHA-256,
and optionally `STS2_FIDELITY_TRACE_DIR`. Restart the game after installing
the updated mod. Capture at least one entire combat, deliberately exercising
a Strike/Defend, an enemy turn and Survivor's nested discard prompt. If
possible use block, debuff, draw and a potion or relic. Keep **both** files.

```fish
set trace /mnt/d/SteamLibrary/sts2-inspector-traces
for sidecar in $trace/*.actions.jsonl
    python tools/native_action_envelope_audit.py "$sidecar" \
        --out (string replace -r '\\.jsonl$' '.audit.json' "$sidecar")
end
```

For an aggregate report over all recorded combats, including mechanic families
that remain **unobserved**, run:

```fish
python tools/native_action_envelope_batch.py "$trace" \\
    --out ~/projects/sts2-inspector-stage/reports/next-combat-corpus.json
```

A corpus is a set of observations, so the report deliberately retains
`native_game_parity=NOT_TESTED`. `OBSERVED_UNVERIFIED_MECHANICS` means a
matching state change or callback was encountered, not that its implementation
or hook ordering has been independently verified.

Run `python tools/native_trace_audit.py` separately on matching
`passive-*.jsonl` files; expect that strict decisions might fail closed
without discarding the all-action sidecar. Preserve raw JSONL files and
game logs, including any `.incomplete` markers.

## Implementation roadmap after this capture

1. **Pinned-game native validation:** compile/load PR #130 against the actual
   installed v0.111 DLL, prove hook attachment and unchanged human combat
   behavior. CI's native-free build cannot validate native API signatures.
2. **Source hook probes:** add explicit, read-only hooks for card play stages,
   damage/block, power apply/expire, draw/discard/exhaust, relic/potion
   triggers, enemy action scheduling and combat entry/win/lose. Stamp causal
   parent and native source location. Compare to PR #129's engine journal.
3. **Semantic nested choices:** identify the pause's choice type, legal option
   instance IDs and actual selected option at the source callback; represent
   every selected choice as a distinct stable decision.
4. **Richer continuation snapshots:** support live relic/potion state, relevant
   card enchantments/cost modifiers, statuses/powers with ordered stacks,
   enemy move history, pending effect queue and source-generated instances.
5. **Batch engine checker:** replay supported action envelopes on the actual
   emulator without feeding native POST data into Step. Report the first
   mismatching event/field and classify unsupported vs chance-dependent
   continuation; maintain a corpus with a coverage matrix per mechanic.
6. **Chance outcomes:** log native outcome/site and conditioning context,
   replay by injecting that outcome where feasible, or compare its conditional
   distribution across controlled samples. Exact stream seeds are optional.

No changes to Nature training or any Nature policy belong to this work.
