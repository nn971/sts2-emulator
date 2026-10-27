# H8 M2: Version-owned semantic intent resolution

Status: experimental PR #209, stacked on direct draw PR #207.

## Implemented

A session-owned H8 boundary token (`runtime_id + boundary_version`)
already rejects stale or foreign decisions before mechanics are touched.
The legacy public `SemanticPilotCombatEngineV111.ResolveIntent` nonetheless
revalidated the **entire semantic pilot snapshot** at each intent choice,
then computed the same pilot SHA again for the event identity. This is
unnecessary in H8's trusted single-writer local session.

`ResolveIntentFromH8TrustedBoundary` uses the live
`PendingInitialIntents` or end-turn intent request and the original
weighted `ControlledTurnIntentEngineV111.DescribePendingSemanticIntent`
support; it selects the exact source ticket by semantic intent ID. The
public checked resolver remains untouched. The new path skips the global
pilot `Validate()` walk, then resumes the same typed source intent and
deterministic turn-effect mechanics through the internal prevalidated
resolver. A legacy fingerprint is still computed once to retain exact
serialized intent decision identity during migration.

H8 appends the provider identifier and selected intent ID only after
the complete resulting state has been returned. Failed selections or
stale tokens do not advance the journal or session boundary version.
Provider interchangeability remains future H8 M3 work: the provider
string currently records the chooser, while the existing weighted
source ticket selection is the actual stochastic semantics.

## Required acceptance

- Source-valid Silent Act1 Ascension 10 Mawler seeded combat.
- Same support and selected weighted intent ID as checked v111.
- Exact final serialized semantic pilot state and deterministic replay.
- Rejected invalid intent and foreign boundary tokens leave state and
  journal unchanged.
- Existing H8 direct draw, source-valid A10 sequential 25-decision /
  18-physical-draw test, real Python JSONL fork/replay all pass.

## Remaining

This is a narrow input-validation optimization, **not** the single-owner
native intent/turn execution cursor. A nested legacy end-turn projection
is still necessary; the continuation's output validators still run.
For substantive H8 speed gains, migrate the full
`ChoosePlayerAction -> end turn -> weighted intent -> automatic effects`
segment onto the native effect cursor, and remove output-only legacy
projections once independent H8 checkpoints can audit the journal.
Do not claim end-to-end training speedups from this patch, and leave the
`sts2-ai` revision unchanged until matched production benchmarks.
