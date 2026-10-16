"""Thin, version-negotiated JSONL binding. Canonical mechanics stay in C#."""
from dataclasses import dataclass
import json
import subprocess
import threading


class EmulatorError(RuntimeError):
    pass


@dataclass(frozen=True)
class State:
    session: "Session"
    handle: str

    def actions(self):
        return self.session.call("legal_actions", state_handle=self.handle)["actions"]

    def observe(self):
        result = self.session.call("observe", state_handle=self.handle,
                                   policy_id="prototype-fair-v0")
        return json.loads(result["payloadJson"])

    def step(self, action_id):
        result = self.session.call("step", state_handle=self.handle, action_id=action_id)
        return State(self.session, result["child"])

    def fork(self):
        return State(self.session, self.session.call("fork", state_handle=self.handle)["child"])

    def exact_hash(self):
        return self.session.call("exact_hash", state_handle=self.handle)["exactHash"]

    def save_snapshot(self):
        """Return hidden exact state; this is not a fair-policy observation."""
        return self.session.call("save_snapshot", state_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.call("release_many", state_handles=[self.handle])["released"]


@dataclass(frozen=True)
class NatureBoundary:
    """Isolated source-shaped Nature choice, NOT a playable game State."""
    session: "Session"
    handle: str
    kind: str
    fingerprint: str
    pending: dict | None
    complete: bool

    @classmethod
    def from_wire(cls, session, payload):
        return cls(session, payload["natureHandle"], payload["kind"],
                   payload["fingerprint"], payload["pending"], payload["complete"])

    def resolve(self, selected_index: int):
        """Create a child by supplying one legal chance index."""
        result = self.session.call("nature_resolve", nature_handle=self.handle,
                                   fingerprint=self.fingerprint,
                                   selected_index=selected_index)
        return NatureBoundary.from_wire(self.session, result)

    def fork(self):
        result = self.session.call("nature_fork", nature_handle=self.handle)
        return NatureBoundary.from_wire(self.session, result)

    def advance(self):
        """Advance a *completed HP* boundary to committed enemy intents."""
        result = self.session.call("nature_advance", nature_handle=self.handle)
        return NatureBoundary.from_wire(self.session, result)

    def inspect(self):
        """Return source-site chance support and deterministic replay metadata.

        This is Nature-facing information, never a fair player observation
        or a playable combat handle. Reference probabilities are unverified.
        """
        if self.session.capabilities.get("natureEventMetadataSchemaId") != (
                "sts2-v111-nature-event-metadata-v1"):
            raise EmulatorError("Emulator does not support Nature event metadata")
        return self.session.call("nature_inspect", nature_handle=self.handle)

    def save_snapshot(self):
        """Return an opaque Nature snapshot; distinct from RunState snapshot."""
        return self.session.call("nature_save", nature_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_nature_many([self])





@dataclass(frozen=True)
class ControlledCombat:
    """Opt-in playable combat fixture, with source-controlled opening shuffle.

    Only deterministic player actions are currently executable. Unmigrated
    chance sites fail explicitly; this is not full Nature gameplay support.
    """
    session: "Session"
    handle: str
    fingerprint: str

    @classmethod
    def from_wire(cls, session, payload):
        return cls(session, payload["controlledHandle"], payload["fingerprint"])

    def trace(self):
        return self.session.call("controlled_trace", controlled_handle=self.handle)

    def resolve(self, selected_index: int):
        return ControlledCombat.from_wire(self.session,
            self.session.call("controlled_resolve", controlled_handle=self.handle,
                              fingerprint=self.fingerprint,
                              selected_index=selected_index))

    def observe(self):
        result = self.session.call("controlled_observe",
                                   controlled_handle=self.handle,
                                   policy_id="prototype-fair-v0")
        return json.loads(result["payloadJson"])

    def actions(self):
        return self.session.call("controlled_legal_actions",
                                 controlled_handle=self.handle)["actions"]

    def step(self, action_id: str):
        return ControlledCombat.from_wire(self.session,
            self.session.call("controlled_step",
                              controlled_handle=self.handle, action_id=action_id))

    def fork(self):
        return ControlledCombat.from_wire(self.session,
            self.session.call("controlled_fork", controlled_handle=self.handle))

    def save_snapshot(self):
        """Privileged hidden snapshot; NEVER a fair player observation."""
        return self.session.call("controlled_save",
                                 controlled_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_controlled_many([self])


@dataclass(frozen=True)
class SemanticCombat:
    """Versioned two-site Nature session.

    This first P0 bridge is seeded from an already completed source
    controlled opening. Source-committed opening cards keep their hidden
    exact order; only fresh intent/draw events reach Nature. The fair
    policy observation is returned separately from Nature event support.
    """
    session: "Session"
    handle: str
    fingerprint: str
    boundary: str
    pending: dict | None
    terminal: bool
    outcome: str | None
    exit_hp: int | None

    @classmethod
    def from_wire(cls, session, payload):
        data = payload["result"]
        return cls(
            session, data["semanticHandle"], data["fingerprint"],
            data["boundary"], data["pending"], data["terminal"],
            data["outcome"], data["exitHp"])

    def status(self):
        return self.session.call(
            "semantic_status", semantic_handle=self.handle)["result"]

    def observe(self):
        """Fair player-only observation: never a privileged Nature event."""
        result = self.session.call(
            "semantic_observe", semantic_handle=self.handle,
            policy_id="prototype-fair-v0")
        return json.loads(result["payloadJson"])

    def actions(self):
        result = self.session.call(
            "semantic_observe", semantic_handle=self.handle,
            policy_id="prototype-fair-v0")
        return result["actions"]

    def step(self, action_id: str):
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_step", semantic_handle=self.handle,
                fingerprint=self.fingerprint, action_id=action_id))

    def resolve(self, selected_index: int):
        if not self.pending or self.pending["kind"] not in (
                "enemy_intent", "draw_card"):
            raise EmulatorError("Expected a two-site semantic Nature event")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve", semantic_handle=self.handle,
                fingerprint=self.fingerprint, selected_index=selected_index))

    def resolve_intent(self, intent_id: str):
        """Choose a filtered, weighted semantic enemy move by stable ID.

        The declared raw weights are source-shaped and remain UNVERIFIED
        as calibrated reference probabilities.
        """
        if not self.pending or self.pending["kind"] != "enemy_intent":
            raise EmulatorError("Expected an enemy_intent Nature event")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_intent", semantic_handle=self.handle,
                fingerprint=self.fingerprint, intent_id=intent_id))

    def resolve_draw_card(self, card_instance_id: int):
        """Resolve a deferred draw by event and physical instance identity."""
        if not self.pending or self.pending["kind"] != "draw_card":
            raise EmulatorError("Expected a draw_card Nature event")
        if self.session.capabilities.get("semanticDrawChoiceSchemaId") != (
                "sts2-v111-semantic-deck-v1"):
            raise EmulatorError("Emulator does not expose semantic draw identities")
        candidate_ids = [
            candidate["cardInstanceId"]
            for candidate in self.pending.get("candidates", [])]
        if card_instance_id not in candidate_ids:
            raise ValueError("Card instance is outside the pending draw support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_draw_card", semantic_handle=self.handle,
                fingerprint=self.fingerprint, event_id=self.pending["eventId"],
                card_instance_id=card_instance_id))

    def fork(self):
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_fork", semantic_handle=self.handle))

    def save_snapshot(self):
        """Privileged exact snapshot, never valid as a fair observation."""
        return self.session.call(
            "semantic_save", semantic_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_semantic_many([self])


class Session:
    def __init__(self, command, *, cwd=None):
        if isinstance(command, str):
            raise TypeError("command must be an argument sequence, not a shell command")
        self._process = subprocess.Popen(list(command), cwd=cwd, stdin=subprocess.PIPE,
                                         stdout=subprocess.PIPE, text=True, encoding="utf-8", bufsize=1)
        self._lock = threading.RLock()
        self._next_request = 0
        self._closed = False
        try:
            self.capabilities = self.call("hello")
            if self.capabilities.get("wireSchemaId") != "prototype-ai-jsonl-v0":
                raise EmulatorError("Unsupported emulator wire schema")
            if self.capabilities.get("snapshotSchemaId") != "sts2-exact-snapshot-v1":
                raise EmulatorError("Emulator does not support exact snapshot v1")
            if self.capabilities.get("batchSchemaId") != "sts2-deterministic-batch-v1":
                raise EmulatorError("Emulator does not support deterministic batch v1")
        except BaseException:
            self._process.terminate()
            self._process.wait(timeout=5)
            self._close_pipes()
            raise

    def call(self, operation, **payload):
        with self._lock:
            if self._closed:
                raise EmulatorError("Emulator session is closed")
            request_id = str(self._next_request)
            self._next_request += 1
            request = {**payload, "op": operation, "request_id": request_id}
            try:
                self._process.stdin.write(json.dumps(request, ensure_ascii=True) + "\n")
                self._process.stdin.flush()
                line = self._process.stdout.readline()
            except (BrokenPipeError, OSError) as error:
                raise EmulatorError("Emulator process disconnected") from error
            if not line:
                raise EmulatorError(f"Emulator process exited ({self._process.poll()})")
            result = json.loads(line)
            if result.get("requestId") != request_id:
                raise EmulatorError("Emulator response request ID does not match")
            if not result.get("ok"):
                raise EmulatorError(f"{result.get('errorType')}: {result.get('error')}")
            return result

    def reset(self, seed, *, mode="v111", character="silent", first_act="Overgrowth",
              ascension=0, fidelity_id=None):
        if mode not in {"v111", "prototype"}:
            raise ValueError("mode must be v111 or prototype")
        if mode == "prototype" and (character != "silent" or first_act != "Overgrowth"
                                    or fidelity_id is not None):
            raise ValueError("Legacy prototype reset supports Silent/Overgrowth without v111 fidelity overrides")
        payload = {"seed": seed, "ascension": ascension}
        if mode == "v111":
            payload.update(character=character, first_act=first_act)
            if fidelity_id is not None:
                payload["fidelity_id"] = fidelity_id
        result = self.call("reset_v111" if mode == "v111" else "reset", **payload)
        return State(self, result["stateHandle"])

    def load_snapshot(self, snapshot):
        return State(self, self.call("load_snapshot", snapshot=snapshot)["stateHandle"])

    def step_batch(self, steps, *, parallelism=1):
        payload = []
        for state, action_id in steps:
            if state.session is not self:
                raise ValueError("Batch state belongs to a different session")
            payload.append({"state_handle": state.handle, "action_id": action_id})
        result = self.call("step_batch", steps=payload, parallelism=parallelism)
        return [State(self, item["child"]) for item in result["children"]]

    def _require_nature_capability(self):
        if self.capabilities.get("natureBoundarySchemaId") != (
                "sts2-v111-nature-isolated-boundary-v1"):
            raise EmulatorError("Emulator does not expose the opt-in Nature boundary protocol")

    def begin_nature(self, seed: str, *, kind: str,
                     act: int, encounter_id: str, native_entry: str,
                     total_floor: int, map_column: int = 3,
                     map_row: int = 5, ascension: int = 0):
        """Create an isolated test fixture; no full-run chance stepping."""
        self._require_nature_capability()
        if kind not in ("initial_hp", "intents"):
            raise ValueError("Supported Nature kinds: initial_hp, intents")
        result = self.call("nature_begin", kind=kind, seed=seed, act=act,
                           encounter_id=encounter_id, native_entry=native_entry,
                           total_floor=total_floor, map_column=map_column,
                           map_row=map_row, ascension=ascension)
        return NatureBoundary.from_wire(self, result)

    def begin_controlled_combat(self, completed_intents: NatureBoundary,
                                *, seed: str):
        """Join a completed controlled HP→intent prefix with a playable fixture."""
        self._require_nature_capability()
        if self.capabilities.get("controlledChanceSchemaId") != (
                "sts2-v111-controlled-playable-fixture-v1"):
            raise EmulatorError("Emulator lacks playable Nature fixture capability")
        if completed_intents.session is not self or not completed_intents.complete:
            raise ValueError("Expected completed Nature intent from this session")
        if completed_intents.kind != "intents":
            raise ValueError("Controlled combat begins after intent commitments")
        response = self.call("controlled_begin",
                             nature_handle=completed_intents.handle, seed=seed)
        return ControlledCombat.from_wire(self, response)

    def begin_semantic_combat(self, completed_opening: ControlledCombat):
        """P0 interoperability: the opening fixture must be fully committed.

        The initial legacy shuffle is treated as an already committed
        source order; it cannot be selected as a Nature draw event.
        """
        if self.capabilities.get("semanticChanceSchemaId") != (
                "sts2-v111-semantic-playable-p0-v1"):
            raise EmulatorError("Emulator does not expose semantic gameplay")
        if completed_opening.session is not self:
            raise ValueError("Opening handle belongs to another session")
        return SemanticCombat.from_wire(self, self.call(
            "semantic_begin", controlled_handle=completed_opening.handle))

    def begin_seeded_semantic_combat(
            self, seed: str, *, act: int, encounter_id: str,
            native_entry: str, total_floor: int, ascension: int = 0,
            first_monster_combat_id: int = 1,
            map_column: int = 3, map_row: int = 5,
            other_chance_provider_id: str | None = None,
            fixture_draw_cost_relic_id: str | None = None,
            replay_opening_draw_hooks: bool = False,
            native_policy_baseline: bool = False,
            source_route_entry: bool = False,
            route_region: str = "Overgrowth",
            route_neow_choice_id: str | None = None,
            route_map_node_id: str | None = None):
        """Begin a two-site pilot without external opening chance tickets.

        By default setup uses the isolated seeded fixture. Set
        source_route_entry through begin_seeded_semantic_route() to run the
        configured Act 1 route and real native weak-combat entry.
        """
        if self.capabilities.get("semanticSeededOpeningProvider") != (
                "engine-owned-seeded-source-shaped-unverified-v1"):
            raise EmulatorError("Emulator lacks seeded semantic entry")
        if native_policy_baseline and self.capabilities.get(
                "semanticNativePolicyComparatorId") != (
                    "engine-owned-native-v111-comparator-unverified-v1"):
            raise EmulatorError("Emulator lacks the native-policy comparator")
        if source_route_entry and self.capabilities.get(
                "semanticSeededRouteEntryProviderId") != (
                    "engine-owned-v111-native-weak-route-entry-unverified-v1"):
            raise EmulatorError("Emulator lacks source-route semantic entry")
        if other_chance_provider_id is not None and (
                other_chance_provider_id not in
                self.capabilities.get("semanticOtherChanceProviders", [])):
            raise EmulatorError("Unsupported engine-owned chance provider")
        return SemanticCombat.from_wire(self, self.call(
            "semantic_begin_seeded", seed=seed, act=act,
            encounter_id=encounter_id, native_entry=native_entry,
            total_floor=total_floor, ascension=ascension,
            first_monster_combat_id=first_monster_combat_id,
            map_column=map_column, map_row=map_row,
            other_chance_provider_id=other_chance_provider_id,
            fixture_draw_cost_relic_id=fixture_draw_cost_relic_id,
            replay_opening_draw_hooks=replay_opening_draw_hooks,
            native_policy_baseline=native_policy_baseline,
            source_route_entry=source_route_entry,
            route_region=route_region,
            route_neow_choice_id=route_neow_choice_id,
            route_map_node_id=route_map_node_id))

    def begin_seeded_semantic_route(
            self, seed: str, *, region: str = "Overgrowth",
            ascension: int = 0, native_policy_baseline: bool = False,
            neow_choice_id: str | None = None,
            first_map_node_id: str | None = None):
        """Replay a source-shaped Act 1 run into its first native weak combat."""
        if self.capabilities.get("semanticSeededRouteEntryProviderId") != (
                "engine-owned-v111-native-weak-route-entry-unverified-v1"):
            raise EmulatorError("Emulator lacks source-route semantic entry")
        return self.begin_seeded_semantic_combat(
            seed, act=1, encounter_id="@source-route",
            native_entry="@source-route", total_floor=1,
            ascension=ascension, native_policy_baseline=native_policy_baseline,
            source_route_entry=True, route_region=region,
            route_neow_choice_id=neow_choice_id,
            route_map_node_id=first_map_node_id)

    def load_semantic_snapshot(self, snapshot: str):
        if self.capabilities.get("semanticChanceSchemaId") != (
                "sts2-v111-semantic-playable-p0-v1"):
            raise EmulatorError("Emulator does not expose semantic gameplay")
        return SemanticCombat.from_wire(self, self.call(
            "semantic_load", snapshot=snapshot))

    def release_semantic_many(self, states):
        states = list(states)
        if any(s.session is not self for s in states):
            raise ValueError("Semantic handle belongs to another session")
        return self.call("semantic_release_many",
                         semantic_handles=[s.handle for s in states])["released"]

    def load_controlled_snapshot(self, snapshot: str):
        return ControlledCombat.from_wire(self,
            self.call("controlled_load", snapshot=snapshot))

    def release_controlled_many(self, states):
        states = list(states)
        if any(x.session is not self for x in states):
            raise ValueError("Controlled handle belongs to another session")
        return self.call("controlled_release_many",
                         controlled_handles=[x.handle for x in states])["released"]

    def load_nature_snapshot(self, snapshot: str, *, kind: str):
        self._require_nature_capability()
        result = self.call("nature_load", kind=kind, snapshot=snapshot)
        return NatureBoundary.from_wire(self, result)

    def release_nature_many(self, boundaries):
        self._require_nature_capability()
        states = list(boundaries)
        if any(state.session is not self for state in states):
            raise ValueError("Nature handle belongs to another session")
        return self.call("nature_release_many",
                         nature_handles=[state.handle for state in states])["released"]

    def coverage(self):
        return self.call("v111_coverage")["coverage"]

    def _close_pipes(self):
        self._closed = True
        for pipe in [self._process.stdin, self._process.stdout]:
            if pipe:
                pipe.close()

    def close(self):
        with self._lock:
            if self._closed:
                return
            try:
                if self._process.poll() is None:
                    self.call("close")
                    self._process.wait(timeout=5)
            finally:
                if self._process.poll() is None:
                    self._process.terminate()
                    try:
                        self._process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        self._process.kill()
                        self._process.wait()
                self._close_pipes()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
