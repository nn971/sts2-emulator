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
