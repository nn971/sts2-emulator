# H7 canonical hash fast path (follow-up to PR #198)

**Status:** isolated optimization branch; production throughput and correctness gate pending.
**Baseline:** `93e780e4f1f6195071719af8aa5ea9fc49633c7b` (PR #198).
**Goal:** remove provably redundant canonical-JSON buffer construction and repeated validation fingerprints without changing the Native / ReferenceNature / controlled Nature interfaces, draw mechanics, source distributions, replay, or stale-state checks.

## Changes

1. `CanonicalJson.Sha256<T>` now runs the **same sorted canonical UTF-8 writer** into one owned `MemoryStream` and hashes its backing span directly. Previously, every hash encoded a canonical UTF-8 buffer, copied it to a byte array, converted it to a managed UTF-16 string, and encoded that string to a second UTF-8 array before hashing. The canonical writer, JSON serializer options, and lower-case SHA-256 hexadecimal format remain unchanged. `CanonicalJson.Serialize<T>` likewise decodes directly from the writer's buffer without calling `ToArray()`.
2. The existing *thread-local, synchronous, read-only* reference-identity fingerprint scope now additionally covers `SemanticPilotCombatV111`, `SemanticEndTurnV111`, and `SemanticCombatDrawV111`, each in a **separate exact-static-type cache**. Subscope disposal, exception cleanup, and optional independent re-hashing on every hit are preserved. No hash is reused across mutable transitions.
3. `ResolveDrawCard` reuses the read-only scope across its input validation, caller fingerprint check, and selected pending-event validation. The scope ends before `Resolve` resumes mechanics.
4. `SemanticResult` reuses read-only hashes across response fingerprint, pending-event inspection, chance-site enumeration, and controller-mode projection. The scope ends before returning the result to the JSONL handler.
5. Added differential tests that compute the old `SHA256(UTF8(CanonicalJson.Serialize(x)))` result for nested JSON, `RunState`, and a full semantic combat session, plus scoped pilot identity-cache tests. The existing stale-fingerprint, snapshot replay, and RunState cache tests remain applicable.

This is one targeted step. It eliminates redundant *buffers* for every canonical hash and some redundant *hash computations* for identical snapshots. Hashing distinct `RunState` values repeatedly inside nested validators remains a larger architectural opportunity; safely removing those checks requires explicit provenance/ownership invariants.

## Correctness conditions

- Exact canonical JSON strings and SHA-256 digests, including Unicode, nesting, keys, and numeric JSON values.
- Same source seed/digest, actor SHA, stochastic support and probability law, player decisions, draw resolutions, and terminal outcomes.
- Stale fingerprint and invalid event/support rejection unchanged.
- Provider-owned hidden deck order and unresolved Nature constraints unchanged.
- All cache hits independently checked with `STS2_H7_VERIFY_RUN_HASH_REUSE=1` in the 16-cell source-realistic draw-diagnostics workflow.
- All scopes limited to synchronous read-only operations; no cross-request or cross-transition memoization.
- Full terminal replay verification in evaluation; sampled terminal replay is controlled by sts2-ai independently.

## CI

On PR creation, these existing workflows cover the change:

- `semantic-rollout-hotpath`: C# build, exact-hash unit tests, stale-state JSONL draws, snapshot restore.
- `h7-draw-stage-diagnostics`: 16 A10 cells, provider traces and independent hash-cache-hit recomputation.
- `h7-runstate-hash-ab`: source-matched 16-cell CPU actor baseline-versus-treatment comparison. Its **baseline has been updated to PR #198**, so it isolates this change rather than reproducing the earlier PR #196-to-#198 gain.

The A/B workflow requires RunState hash allocation to decrease and no increase in the full RunState or pilot hash counts. The profiling stages overlap and must not be added together. Results are not established until CI completes.

## Full 128-combat training-target performance gate

In the existing `~/projects/sts2-ai` environment (fish), build both emulator revisions in isolated temporary directories rather than modifying the active emulator branch. Use `tools/benchmark_h7_rollouts.py` with **128 episodes, 15 workers, shared CUDA actor, max batch 16, delay 3 ms**, seeds `73127/31337/23387`, and the same 14.66M-parameter actor configuration.

Run the existing H7 profile summarizer and comparison, then repeat without `STS2_SEMANTIC_PROFILE_DIR` in an alternating A/B/B/A order to measure actual wall and garbage-collection savings. Benchmark combined 1/8 stratified training terminal replay separately; evaluation retains full replay.

The PR #198 reference full-run results are: 128 A10 combats, 40 wins, 3,418 player decisions, 7,659 boundaries, 4,054 draws, 208,032 full RunState hashes, 218.16 GB draw allocation, and 106.23 seconds profiled rollout time. These are **baseline results**, not measurements of this branch.

**Do not move sts2-ai's pinned emulator submodule before full-scale validation.** Native Inspector parity may inform future fidelity work but must not block agent training.
