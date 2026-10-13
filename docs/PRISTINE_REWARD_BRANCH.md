# Pristine first-reward stream proposal

**Experimental only.** This operation is available exclusively on JSONL
handles that originated from `reset_factorized_hypothetical`, whose six
SplitMix64 RNG streams were independently and uniformly initialized.

In the pinned prototype, at the first combat-to-reward boundary a
`reward` stream whose `CallCount == 0` has not participated in
earlier actions. Consequently, under this **specified alternate game
prior**, its initial 64-bit state is still independent of all public
history and other five stream states. It can be proposed afresh without
modifying the conditional distribution of the other five streams.

`PrototypePristineRewardProposal.Branch` takes a pre-transition
hypothetical combat state, an actually legal public action, and an
**independently generated** 64-bit hexadecimal reward state. It:

1. Requires the explicitly independent initial-stream synthetic run.
2. Requires the pre-transition reward RNG cursor to have zero calls.
3. Forks the entire game state; changes only the initial reward stream.
4. Executes the real engine action, including its reward RNG draws.
5. Requires the resulting phase to be `Reward` (otherwise refuses).

The JSONL operation `propose_pristine_reward` exposes this guarded
step only on independent hypothetical handles. It returns a new child
handle but **never** the private RNG state or any real-run oracle
handle. A research-side conditioner must check the *full public
observation and legal-action menu* before retaining a candidate.

## Correct use with a joint empirical particle posterior

Suppose `N` complete hidden states are equally weighted *empirical*
particles conditioned on a public pre-reward history. For each
particle, sample `K` **independent uniform initial 64-bit reward
states** from the independent stream prior and branch the genuine
reward-generating action. Accept a child precisely when its entire
public frame and complete legal menu match the observed reward.
Uniformly weighting **all accepted children**, not one child per
parent, targets the conditional distribution of the `N × K` finite
joint empirical proposal cohort. This naturally reweights parents
in proportion to their estimated reward-evidence likelihood.

Importantly, simply finding one matching reward state per parent
and giving parents equal weights would **not** be correct if the
likelihood of that public reward differs between parents.

For finite `K`, this remains a **random finite-cohort
approximation**, not exact sampling from the full six-stream game
prior. All previous stream states and cursors are preserved. Do not
claim fresh reward resampling is valid if `reward.CallCount > 0`,
in other reward phases, on normally seeded runs, or at earlier
observations affected by the reward RNG.

This operation intentionally has no automatic fallback to local
rekeying in illegal contexts. Long-run statistical validity and
native STS2 reward RNG laws remain separate verification tasks.
