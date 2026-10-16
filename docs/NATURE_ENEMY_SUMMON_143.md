# Nature #143 — controlled legacy enemy summons

The saved enemy-action replay can suspend at the modeled legacy
`combat.enemy.summon_pool.select` site. It preserves the current source-shaped
pool, including the Fabricator last-spawn exclusion and fallback to the full
pool when all entries are excluded. Replaying the choice resumes the enemy
action and creates the selected summon through the existing combat kernel.

If the selected summoned enemy has a variable HP range, the same replay frame
then exposes `combat.initial_unique_hp` as a second `enemy_action_chance` event.
Earlier summon choices are injected again from the checkpoint before the HP
choice is reached. The source action commits the selected HP and continues the
enemy stage once all decisions are resolved.

This continuation covers the existing legacy combat model. Native summon-pool
selection remains fail-closed until its source RNG assignment is audited.
Probability status is `UNVERIFIED`; the new nested controlled mechanics path
has build evidence only.
