# ReferenceNature Tablet of Truth session

`ReferenceNatureTabletOfTruthSessionV111` handles one pending Tablet of Truth
`decipher` draw on pages 0 through 3. It conditions on the current player
state and the ordered physical deck instances that remain unupgraded, are
playable, and are not Curse, Status, or Quest cards. A Chance Law assigns equal
integer weight to each eligible card instance. The selected index is replayed
through the ordinary `decipher` event action with the scripted index provider;
the page's HP cost, card upgrade, and page advance therefore use the existing
mechanics resolver. The resulting run and ReferenceNature cursor commit
together.

The session covers one page at a time. Continue from its committed `State` to
take another player action and, if the next page offers another eligible
`decipher`, begin a new session. Page 4 upgrades every eligible card without
an index draw. A lethal page and an empty eligible-card pool also have no draw
and do not produce a Chance Law. The query counts exact Native `NextInt`
preimages on the uniform 53-bit `NextDouble` grid and is
`SOURCE_VERIFIED` under that input model. Source filtering, seed frequencies,
and Native cursor parity remain separate claims.

The scripted action preserves the Native event stream cursor. The completed
EventState records `scripted-v111`; the event-state invariant allows this
marker on progressed Tablet pages because the player may continue with
ordinary Native choices after a Nature-resolved page. Native source filtering,
seeded cursor parity, and probability calibration remain open.

## S0 projection, resolver, provenance, and unsupported boundary

The `event.tablet_of_truth.card.index_draw` support is the ordered set of
eligible physical deck instances on the current page. Card eligibility,
current page, and the selected index prefix are private generation state. The
Nature view receives the current eligible-card law; ordinary player
observations retain their existing event projection and reveal the committed
card upgrade and page advance through normal mechanics.

The session replays the ordinary `decipher` action from its saved page state,
applying the HP cost and card upgrade once, then commits the page and
ReferenceNature cursor together. The declared Native bounded-index transform
is `SOURCE_VERIFIED` on its current support; installed-game source filtering,
seed frequencies, and cursor parity remain open.

Only pages 0 through 3 with nonempty eligible support suspend for a Nature
choice. Page 4, lethal pages, and empty support are draw-free; other event
operations remain outside this session. No native reference mod or privileged
Fidelity Inspector projection is included.

## JSONL operations

| Operation | Inputs | Result |
| --- | --- | --- |
| `chance_law_query` | pending event `state_handle`, site ID, page `ordinal` | Uniform eligible physical-card law |
| `reference_nature_tablet_of_truth_begin` | event `state_handle`, optional `nature_seed` | One-page session before the draw |
| `reference_nature_tablet_of_truth_status` | session handle | Current law or committed page result |
| `reference_nature_tablet_of_truth_resolve_next` | handle and current `fingerprint` | Child with card upgrade, page advance, and sampler cursor committed |
| `reference_nature_tablet_of_truth_fork` | session handle | Independent child session |
| `reference_nature_tablet_of_truth_save` / `reference_nature_tablet_of_truth_load` | handle / snapshot | Replay-validated session snapshot and restored handle |
| `reference_nature_tablet_of_truth_release_many` | `reference_nature_tablet_of_truth_handles` | Number of released sessions |

The stable site is `event.tablet_of_truth.card.index_draw`. Query and session
coverage are advertised separately by the capability manifest. The session
does not change the player-facing observation contract. The read-only law
derives the event stream label without reading the run seed or saved cursor;
the replay uses the event's saved cursor only to preserve its starting state.

## Python binding

```python
session = state.begin_reference_nature_tablet_of_truth(nature_seed=123)
session = session.resolve_next()
page_run = session.state
```

Omit `nature_seed` to continue a ReferenceNature cursor already committed to
the run. The session also supports `status()`, `fork()`, `save_snapshot()`,
`Session.load_reference_nature_tablet_of_truth_snapshot(snapshot)`, and bulk
release. A direct law query uses
`state.query_chance_law("event.tablet_of_truth.card.index_draw", ordinal=page)`.

This continuation covers only the eligible card draw on the first four pages.
It does not calibrate the probability law or establish installed-game Native
cursor parity and does not depend on the native reference mod or Fidelity
Inspector.
