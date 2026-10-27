# H8 M2: Direct Player End-Turn Transition

Status: experimental draft PR #208, stacked on native H8 draw PR #207.
This is one **implemented** segment of the larger
[H8 native runtime](H8_NATIVE_COMBAT_RUNTIME.md), not a completed replacement
of every player action or every stochastic provider.

## Structural cost removed

Previously, `H8CombatRuntimeV1.ChoosePlayerAction` invoked the fully checked
`SemanticPilotCombatEngineV111.Step` even for a simple legal end-turn.
That validated and SHA-hashed the entire pilot, forked a `RunState`, observed
the legal-action menu, then `SemanticEndTurnEngineV111` observed the same
menu **again** and made several more full-size defensive forks before
resuming the deterministic source mechanics. A final pilot validation
completed the transition.

Now, an ordinary end-turn executes via
`H8NativePlayerEndTurnEngineV1.TryAdvance`:

1. Verify it is the correct **legal source action** at the version-owned
   H8 player boundary; reject unsupported or stale state. Card plays and
   potions remain on the fully checked v111 fallback.
2. Fork one input `RunState` (the original is still an immutable checkpoint);
   run exactly the ordinary `PrototypeAiEnvironment.Step` under the
   `ControlledChanceGuardV111` and engine-owned reward guard.
3. Assemble a source-valid end-turn/initial-intent/terminal continuation
   **from the already-advanced mechanics result** using
   `SemanticEndTurnEngineV111.ContinueFromH8PlayerEndTurn`.
   The audited legacy API still calls the old fully checked
   `BeginFromPlayerActionOrTerminal`.
4. Keep pre-action source and advanced mechanics result as owned immutable
   references until a future mechanics transition; remove extra enclosing
   frame copies and full-state audits from the H8 branch. Automatic
   pipeline and real draw hooks remain the same engine code.
5. Publish the temporary v111 end-turn projection and compact player
   journal once after successful mechanics. The session version increments
   atomically. Exact replay from source is an **explicit** H8 audit action.

The H8 fast and v111 checked paths share
`BeginAtAdvanceTurnCore` with an explicit `audit` switch. This is a
temporary migration bridge and may be deleted when the H8 effect cursor
and native player-action dispatcher own all state.

## Gates and measured results

The first real Ascension 10 Act 1 Mawler A/B test is
[H8 CI run 40564843681](https://github.com/nn971/sts2-emulator/actions/runs/40564843681).
It passed **14/14 focused C# tests and 1/1 Python JSONL integration**.

Eight matched encounters with identical opening, legal end-turn ID,
initial intent choices, final complete pilot fingerprint and explicit
replay, alternating A/B execution order on the same CPU worker:

| Accepted end_turn operation | Checked v111 | H8 direct | Reduction |
| --- | ---: | ---: | ---: |
| Managed allocations | 7.387 MB | 0.479 MB | 93.5% |
| Elapsed time | 9.064 ms | 0.604 ms | 93.3% |

The tests also reject a foreign runtime/version token before doing
mechanics or appending the journal. A follow-up longer test compares
a matched source-valid encounter across successive real intent, player
and physical draw decisions and verifies the full replay afterward.
The above microbench is **not** the full 128-combat/15-worker CUDA actor
and excludes all model inference, H8 JSONL status and observation costs.

## Next structural steps

- Introduce a **single H8 effect cursor** representing the post-end-turn
  pipeline without retaining a v111 end-turn projection or full origin.
  The current H8 end-turn still produces a v111 output continuation; the
  underlying mechanical RunState remains immutable/record-based.
- Move the entire **player-action** dispatch (supported card/potion,
  selection subchoices) to fast execution with local validations.
- Migrate weighted enemy-intent and remaining controlled chance sites via
  interchangeable typed Native, ReferenceNature, learned Nature and
  scripted provider contracts. No substitution of provider behavior.
- Native/batched actor transport and compact observation arrays are H8 M4.
  Keep `sts2-ai`'s existing pin and report no training-wide gains
  until a 128-combat/15-worker/14.66M-parameter A/B/B/A CUDA rollout,
  identical source and stochastic choices, sampled training replay and
  complete evaluation replay.

Old native Inspector parity and broad historical fixtures remain an
independent track. Novel end-turn/intent/draw mechanics or replay
regressions block adopting the H8 fast path.
