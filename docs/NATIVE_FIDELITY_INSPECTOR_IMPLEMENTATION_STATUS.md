# Inspector implementation status

**Feature branch:** feature/native-fidelity-inspector-foundation

| Gate | Status | Evidence / blocker |
|---|---|---|
| P0 | PARTIAL | Opt-in source-audited combat hook, hash-pinned loader and packaging script; actual v111 compilation, smoke test and game behavior unverified |
| P1 | PARTIAL | 0.2 records, writer, journal, identity; source-backed partial native combat adapter; nested choices and other phases unsupported |
| P2 | PARTIAL | Strict offline comparator/import coverage and HTML timeline; actual emulator continuation and native traces absent |
| P3 | PARTIAL | Decision controller contracts; native dispatcher and Godot overlay absent; optional loopback transport is implemented but not attached |
| P4 | NOT STARTED | Broader native coverage and regression corpus |
| P5 | DEFERRED | Exact RNG/Nature training is a separate milestone |

Synthetic contract tests establish protocol behavior and are not counted as
native parity validation. Main currently provides generic deterministic engine
contracts rather than a verified native combat snapshot continuation loader.

Next native-local steps: run build-native-inspector.fish using a real installed game; check emitted trace against native behavior; audit semantic combat
boundary/action/effect hooks; attach read-only Silent combat adapter; demonstrate
passive/no-perturbation capture; add actual Strike/Defend fixtures; implement
strict emulator snapshot import and run first differential single-step parity;
then connect controlled input dispatch and the in-game panel.

Coverage labels must remain explicit. INCOMPLETE_CAPTURE, CHANCE_UNALIGNED,
INVALID_SETUP and UNSUPPORTED never count as passes.
