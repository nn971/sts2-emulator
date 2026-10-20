# Project instructions: agent training and native-game fidelity

## Mandatory separation of acceptance tracks

**Native/original-game RNG fidelity checks must never be a prerequisite for agent training.** This policy applies to all Nature, player, PPO, and other training plans, experiments, roadmaps, PR reviews, and status reports in `sts2-ai` and `sts2-emulator`.

- **Training gate (mechanical/instrumentation):** The exact pinned emulator builds; the chosen *restricted* environment can execute genuine terminal combat through the ordinary legal-action and fair-observation interface; outcomes are legal under the emulator's declared Chance Law; unsupported stochastic sites fail closed; hidden state does not leak; both training and reference policies use consistent supported mechanics; and snapshots/transcripts replay deterministically. Require statistically appropriate evaluation for claims of learning effectiveness. These are *emulator-internal* and experimental-validity requirements.
- **Explicitly NOT training gates:** Installed-game/native RNG or gameplay parity, original-game RNG stream/cursor/seed/hook-order reproduction, Fidelity Inspector captures or audits (emulator issue #122), source-law calibration/N5, and completion of broad S3/S4 or all-Act/cross-provider equivalence beyond the selected training subset. Pursue these on **independent tracks**, even while training proceeds.
- **Meaning of `UNVERIFIED`:** A probability-provenance label saying that the declared emulator Chance Law has not been calibrated against the original game. It is **not a blocker or failure status for training**. `ReferenceNature` may sample the declared law; Nature may train against it, with the label retained and results framed as emulator-relative.
- **Claims:** Internal legal-support, declared-weight consistency and replay can be tested without native fidelity. Original-game-unbiased probability/importance-weighting or transfer claims require their own later source-law evidence; describe these claims as unestablished until then.
- **Reporting rule:** Whenever describing blockers, distinguish **(A) actual chosen-subset mechanics, interface, replay or statistical issues** from **(B) independent native-fidelity/calibration work**. Never put (B) on the critical path for starting or continuing (A). If only (B) remains, proceed with the declared-law training experiment.

This is a standing cross-repository decision. Revisit it only if the user explicitly changes the training goal to require original-game fidelity.

**Related work:** [emulator Nature issue #143](https://github.com/nn971/sts2-emulator/issues/143), [AI Nature PR #82](https://github.com/nn971/sts2-ai/pull/82), [emulator native Fidelity Inspector issue #122](https://github.com/nn971/sts2-emulator/issues/122).
