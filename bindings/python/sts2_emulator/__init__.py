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

    def query_chance_law(self, site_id: str, *, ordinal: int = 0,
                         prior_indices=None, card_type=None, rarity=None,
                         excluded_potion_ids=None, card_id=None,
                         potion_id=None, relic_id=None,
                         colorless_offer=False, sale_reprice=False,
                         owner_id=None, offer_ordinal=None,
                         relic_candidate_ids=None, excluded_relic_ids=None):
        """Read an advertised pre-outcome Chance Law without changing state."""
        if not self.session.supports_chance_law_query(site_id):
            raise EmulatorError(
                f"No direct Chance Law query is advertised for site {site_id!r}")
        if type(ordinal) is not int or ordinal < 0:
            raise ValueError("ordinal must be a nonnegative integer")
        payload = {"state_handle": self.handle,
                   "site_id": site_id, "ordinal": ordinal}
        if card_type is not None:
            if not isinstance(card_type, str) or not card_type:
                raise ValueError("card_type must be a nonempty string")
            payload["card_type"] = card_type
        if rarity is not None:
            if not isinstance(rarity, str) or not rarity:
                raise ValueError("rarity must be a nonempty string")
            payload["rarity"] = rarity
        for field, value in (("card_id", card_id),
                             ("potion_id", potion_id),
                             ("relic_id", relic_id)):
            if value is not None:
                if not isinstance(value, str) or not value:
                    raise ValueError(f"{field} must be a nonempty string")
                payload[field] = value
        if type(colorless_offer) is not bool or type(sale_reprice) is not bool:
            raise ValueError("colorless_offer and sale_reprice must be booleans")
        if colorless_offer:
            payload["colorless_offer"] = True
        if sale_reprice:
            payload["sale_reprice"] = True
        if owner_id is not None:
            if not isinstance(owner_id, str) or not owner_id:
                raise ValueError("owner_id must be a nonempty string")
            payload["owner_id"] = owner_id
        if offer_ordinal is not None:
            if type(offer_ordinal) is not int or offer_ordinal < 0:
                raise ValueError(
                    "offer_ordinal must be a nonnegative integer")
            payload["offer_ordinal"] = offer_ordinal
        for field, value in (("relic_candidate_ids", relic_candidate_ids),
                             ("excluded_relic_ids", excluded_relic_ids)):
            if value is not None:
                if not isinstance(value, (list, tuple)) or any(
                        not isinstance(relic_id, str) or not relic_id
                        for relic_id in value):
                    raise ValueError(
                        f"{field} must be a sequence of nonempty strings")
                payload[field] = list(value)
        if excluded_potion_ids is not None:
            if not isinstance(excluded_potion_ids, (list, tuple)) or any(
                    not isinstance(potion_id, str) or not potion_id
                    for potion_id in excluded_potion_ids):
                raise ValueError(
                    "excluded_potion_ids must be a sequence of nonempty strings")
            payload["excluded_potion_ids"] = list(excluded_potion_ids)
        if prior_indices is not None:
            if not isinstance(prior_indices, (list, tuple)) or any(
                    type(index) is not int or index < 0
                    for index in prior_indices):
                raise ValueError(
                    "prior_indices must be a sequence of nonnegative integers")
            payload["prior_indices"] = list(prior_indices)
        result = self.session.call(
            "chance_law_query", **payload)
        return result["law"]

    def save_snapshot(self):
        """Return hidden exact state; this is not a fair-policy observation."""
        return self.session.call("save_snapshot", state_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.call("release_many", state_handles=[self.handle])["released"]

    def begin_controlled_event_choice(self, choice_id: str):
        """Start a saved controlled frame for one supported event option."""
        if self.session.capabilities.get("controlledEventChoiceSchemaId") != (
                "sts2-v111-controlled-event-choice-v1"):
            raise EmulatorError("Emulator does not expose controlled event choices")
        result = self.session.call(
            "controlled_event_begin", state_handle=self.handle,
            choice_id=choice_id)
        return ControlledEventChoice.from_wire(self.session, result)

    def resume_controlled_event_choice(self):
        """Inspect a pending event or event-reward boundary from a snapshot."""
        if self.session.capabilities.get("controlledEventChoiceSchemaId") != (
                "sts2-v111-controlled-event-choice-v1"):
            raise EmulatorError("Emulator does not expose controlled event choices")
        result = self.session.call(
            "controlled_event_inspect", state_handle=self.handle)
        return ControlledEventChoice.from_wire(self.session, result)

    def advance_controlled_event_reward(self, action_id: str):
        """Apply one legal event-reward action under controlled chance."""
        if self.session.capabilities.get("controlledEventChoiceSchemaId") != (
                "sts2-v111-controlled-event-choice-v1"):
            raise EmulatorError("Emulator does not expose controlled event choices")
        result = self.session.call(
            "controlled_event_advance", state_handle=self.handle,
            action_id=action_id)
        return ControlledEventChoice.from_wire(self.session, result)

    def begin_controlled_shovel_dig(self):
        """Start a saved Nature boundary for legacy Shovel relic selection."""
        actions = [action for action in self.actions()
                   if action["kind"] == "rest_dig"]
        if len(actions) != 1:
            raise EmulatorError("State does not expose one legal Shovel dig action")
        return self.begin_controlled_run_action(actions[0]["actionId"])

    def begin_controlled_run_start(self):
        """Start a controlled boundary for the legacy run-start encounter pick."""
        actions = [action for action in self.actions()
                   if action["kind"] == "start_run"]
        if len(actions) != 1:
            raise EmulatorError("State is not at an unsuspended run start")
        return self.begin_controlled_run_action(actions[0]["actionId"])

    def begin_controlled_run_action(self, action_id: str):
        """Start a saved Nature boundary for a supported whole-run action."""
        if self.session.capabilities.get("controlledRunActionSchemaId") != (
                "sts2-v111-controlled-run-action-v1"):
            raise EmulatorError("Emulator does not expose controlled run actions")
        result = self.session.call(
            "controlled_run_action_begin", state_handle=self.handle,
            action_id=action_id)
        return ControlledRunActionChoice.from_wire(self.session, result)

    def resume_controlled_run_action(self):
        """Inspect a pending controlled run action restored from a snapshot."""
        if self.session.capabilities.get("controlledRunActionSchemaId") != (
                "sts2-v111-controlled-run-action-v1"):
            raise EmulatorError("Emulator does not expose controlled run actions")
        result = self.session.call(
            "controlled_run_action_inspect", state_handle=self.handle)
        return ControlledRunActionChoice.from_wire(self.session, result)

    def begin_reference_nature_run_action(self, nature_seed: int | None = None, *,
                                          action_id: str | None = None):
        """Begin or continue atomic ReferenceNature sampling for a run action.

        Pass a seed for the first session on a run. Omit it to continue the
        cursor already saved in the run state.
        """
        if self.session.capabilities.get(
                "referenceNatureRunActionSessionSchemaId") != (
                "sts2-v111-reference-nature-run-action-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature run-action sessions")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        if action_id is not None:
            payload["action_id"] = action_id
        result = self.session.call("reference_nature_run_action_begin", **payload)
        return ReferenceNatureRunActionSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_event_choice(self, choice_id: str,
                                            nature_seed: int | None = None):
        """Begin or continue a supported controlled event choice.

        Pass a seed for the first ReferenceNature session on a run. Omit it
        to continue the cursor saved in the run state.
        """
        if self.session.capabilities.get(
                "referenceNatureEventChoiceSessionSchemaId") != (
                "sts2-v111-reference-nature-event-choice-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature event-choice sessions")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "choice_id": choice_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_event_choice_begin", **payload)
        return ReferenceNatureEventChoiceSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_ancient_offer(self, site_id: str,
                                             nature_seed: int | None = None):
        """Begin or continue atomic Nature sampling for a supported Ancient offer."""
        if self.session.capabilities.get(
                "referenceNatureAncientOfferSessionSchemaId") != (
                "sts2-v111-reference-nature-ancient-offer-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Ancient-offer sessions")
        if not self.session.supports_reference_nature_site(
                site_id, session="ancient_offer"):
            raise EmulatorError(
                f"No ReferenceNature Ancient-offer session is advertised for {site_id!r}")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "site_id": site_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_ancient_offer_begin", **payload)
        return ReferenceNatureAncientOfferSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_merchant(self, action_id: str,
                                        nature_seed: int | None = None):
        """Begin atomic ReferenceNature generation for a configured Shop node."""
        if self.session.capabilities.get(
                "referenceNatureMerchantSessionSchemaId") != (
                "sts2-v111-reference-nature-merchant-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature merchant sessions")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be a nonempty legal Shop-node action ID")
        if not self.session.supports_reference_nature_site(
                "shop.merchant.sale_slot.index_draw", session="merchant"):
            raise EmulatorError(
                "No ReferenceNature merchant session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call("reference_nature_merchant_begin", **payload)
        return ReferenceNatureMerchantSession.from_wire(
            self.session, result["result"])


@dataclass(frozen=True)
class ControlledEventChoice:
    """A whole-run event choice suspended at an explicit controlled outcome."""
    session: "Session"
    handle: str
    fingerprint: str
    boundary: str
    pending: dict | None
    transcript: list | None
    actions: list

    @classmethod
    def from_wire(cls, session, payload):
        return cls(
            session, payload["stateHandle"], payload["fingerprint"],
            payload["boundary"], payload["pending"],
            payload["transcript"], payload["actions"])

    @property
    def state(self):
        return State(self.session, self.handle)

    def resolve(self, selected_index: int):
        result = self.session.call(
            "controlled_event_resolve", state_handle=self.handle,
            selected_index=selected_index)
        return ControlledEventChoice.from_wire(self.session, result)

    def enumerate_chance(self, max_outcomes: int = 32):
        """Resolve each outcome of one small pending event chance boundary."""
        if self.session.capabilities.get(
                "controlledEventChoiceEnumerationSchemaId") != (
                "sts2-v111-controlled-event-enumeration-v1"):
            raise EmulatorError(
                "Emulator lacks controlled event-choice enumeration")
        capability_limit = self.session.capabilities.get(
            "controlledEventChoiceEnumerationMaximumOutcomes", 256)
        if (not isinstance(max_outcomes, int) or isinstance(max_outcomes, bool)
                or max_outcomes < 1 or max_outcomes > capability_limit):
            raise ValueError(
                f"max_outcomes must be between 1 and {capability_limit}")
        result = self.session.call(
            "controlled_event_enumerate", state_handle=self.handle,
            fingerprint=self.fingerprint, maximum_outcomes=max_outcomes)
        return ControlledEventChoiceEnumeration.from_wire(
            self.session, result)

    def save_snapshot(self):
        """Return a hidden exact snapshot, including its pending event frame."""
        return self.session.call("save_snapshot", state_handle=self.handle)["snapshot"]

    def release(self):
        return self.state.release()


@dataclass(frozen=True)
class ControlledRunActionChoice:
    """A whole-run action suspended at an explicit controlled outcome."""
    session: "Session"
    handle: str
    fingerprint: str
    boundary: str
    pending: dict | None
    transcript: dict | list | None
    actions: list

    @classmethod
    def from_wire(cls, session, payload):
        return cls(
            session, payload["stateHandle"], payload["fingerprint"],
            payload["boundary"], payload["pending"],
            payload["transcript"], payload["actions"])

    @property
    def state(self):
        return State(self.session, self.handle)

    def resolve(self, selected_index: int):
        result = self.session.call(
            "controlled_run_action_resolve", state_handle=self.handle,
            selected_index=selected_index)
        return ControlledRunActionChoice.from_wire(self.session, result)

    def save_snapshot(self):
        """Return a hidden exact snapshot, including the pending run-action frame."""
        return self.session.call("save_snapshot", state_handle=self.handle)["snapshot"]

    def release(self):
        return self.state.release()


@dataclass(frozen=True)
class ReferenceNatureRunActionSession:
    """Atomic run-action mechanics and ReferenceNature sampler state."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    action_id: str
    run_phase: str
    boundary: str
    pending: dict | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-run-action-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature run-action session schema")
        return cls(
            session, payload["referenceNatureRunActionHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["actionId"],
            payload["runPhase"], payload["boundary"], payload["pending"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        """Ordinary playable state, available after this Nature action commits."""
        if self.state_handle is None:
            raise EmulatorError(
                "ReferenceNature run-action has not committed its pending choice")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_run_action_status",
            reference_nature_run_action_handle=self.handle)
        return ReferenceNatureRunActionSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample and resolve one pending chance through run mechanics."""
        result = self.session.call(
            "reference_nature_run_action_resolve_next",
            reference_nature_run_action_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureRunActionSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_run_action_fork",
            reference_nature_run_action_handle=self.handle)
        return ReferenceNatureRunActionSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_run_action_save",
            reference_nature_run_action_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_run_action_many([self])


@dataclass(frozen=True)
class ReferenceNatureEventChoiceSession:
    """Atomic event-choice mechanics, player reward actions and sampler state."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str
    choice_id: str
    run_phase: str
    boundary: str
    pending: dict | None
    actions: list
    decision_transcript: list
    player_action_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-event-choice-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature event-choice session schema")
        return cls(
            session, payload["referenceNatureEventChoiceHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["eventId"], payload["choiceId"], payload["runPhase"],
            payload["boundary"], payload["pending"], payload["actions"],
            payload["decisionTranscript"], payload["playerActionTranscript"],
            payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError(
                "ReferenceNature event choice has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_event_choice_status",
            reference_nature_event_choice_handle=self.handle)
        return ReferenceNatureEventChoiceSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one pending event outcome through the saved event resolver."""
        result = self.session.call(
            "reference_nature_event_choice_resolve_next",
            reference_nature_event_choice_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureEventChoiceSession.from_wire(
            self.session, result["result"])

    def advance(self, action_id: str):
        """Apply one legal reward action while retaining the sampler cursor."""
        result = self.session.call(
            "reference_nature_event_choice_advance",
            reference_nature_event_choice_handle=self.handle,
            fingerprint=self.fingerprint, action_id=action_id)
        return ReferenceNatureEventChoiceSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_event_choice_fork",
            reference_nature_event_choice_handle=self.handle)
        return ReferenceNatureEventChoiceSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_event_choice_save",
            reference_nature_event_choice_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_event_choice_many([self])


@dataclass(frozen=True)
class ReferenceNatureAncientOfferSession:
    """Atomic three-choice Ancient offer generation and sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    site_id: str
    event_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    offered_choice_ids: list | None
    event_random_provider: str | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-ancient-offer-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Ancient-offer session schema")
        return cls(
            session, payload["referenceNatureAncientOfferHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["siteId"], payload["eventId"], payload["runPhase"],
            payload["boundary"], payload["nextOrdinal"],
            payload["pending"], payload["offeredChoiceIds"],
            payload["eventRandomProvider"], payload["decisionTranscript"],
            payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError(
                "ReferenceNature Ancient offers have not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_ancient_offer_status",
            reference_nature_ancient_offer_handle=self.handle)
        return ReferenceNatureAncientOfferSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one pending offer and publish the run after all three."""
        result = self.session.call(
            "reference_nature_ancient_offer_resolve_next",
            reference_nature_ancient_offer_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureAncientOfferSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_ancient_offer_fork",
            reference_nature_ancient_offer_handle=self.handle)
        return ReferenceNatureAncientOfferSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_ancient_offer_save",
            reference_nature_ancient_offer_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_ancient_offer_many([self])


@dataclass(frozen=True)
class ReferenceNatureMerchantSession:
    """Atomic native Shop entry, mixed Chance Law replay and Nature cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    action_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    shop: dict | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-merchant-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature merchant session schema")
        return cls(
            session, payload["referenceNatureMerchantHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["actionId"], payload["runPhase"],
            payload["boundary"], payload["nextOrdinal"],
            payload["pending"], payload["shop"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError(
                "ReferenceNature merchant generation has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_merchant_status",
            reference_nature_merchant_handle=self.handle)
        return ReferenceNatureMerchantSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one pending Chance Law and replay the shop from its origin."""
        result = self.session.call(
            "reference_nature_merchant_resolve_next",
            reference_nature_merchant_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureMerchantSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_merchant_fork",
            reference_nature_merchant_handle=self.handle)
        return ReferenceNatureMerchantSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_merchant_save",
            reference_nature_merchant_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_merchant_many([self])


@dataclass(frozen=True)
class ControlledEventChoiceOutcome:
    selected_index: int
    candidate_label: str
    child: ControlledEventChoice


@dataclass(frozen=True)
class ControlledEventChoiceEnumeration:
    event_id: str
    choice_id: str
    outcomes: tuple[ControlledEventChoiceOutcome, ...]

    @classmethod
    def from_wire(cls, session, payload):
        outcomes = tuple(
            ControlledEventChoiceOutcome(
                item["outcomeId"], item["candidateLabel"],
                ControlledEventChoice.from_wire(session, item["result"]))
            for item in payload["outcomes"])
        return cls(payload["eventId"], payload["choiceId"], outcomes)


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
    exact order; unresolved intent, draw, and declared deck-reveal events
    reach Nature. The fair
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
        if self.boundary not in ("player", "player_choice"):
            raise EmulatorError("Expected a semantic player decision")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_step", semantic_handle=self.handle,
                fingerprint=self.fingerprint, action_id=action_id))

    def resolve_card_selection(self, action_id: str):
        """Resolve a source select_cards prompt by one listed legal action."""
        if not self.pending or self.pending.get("kind") != "player_card_selection":
            raise EmulatorError("Expected a player_card_selection boundary")
        if self.session.capabilities.get(
                "semanticCardSelectionChoiceSchemaId") != (
                    "sts2-v111-semantic-card-selection-choice-v1"):
            raise EmulatorError("Emulator lacks semantic card-selection choices")
        if action_id not in {
                candidate["actionId"]
                for candidate in self.pending.get("actions", [])}:
            raise ValueError("Action is outside the pending card-selection support")
        return self.step(action_id)

    def resolve(self, selected_index: int):
        if not self.pending or self.pending["kind"] not in (
                "enemy_intent", "draw_card", "random_pile_card"):
            raise EmulatorError("Expected a semantic Nature event")
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

    def resolve_deck_reveal(self, card_instance_id: int):
        """Resolve one pending peek or scry by physical card identity."""
        if not self.pending or self.pending.get("kind") != "deck_reveal":
            raise EmulatorError("Expected a semantic deck_reveal event")
        if self.session.capabilities.get(
                "semanticDeckRevealChoiceSchemaId") != (
                    "sts2-v111-semantic-deck-reveal-decision-v1"):
            raise EmulatorError(
                "Emulator does not expose semantic deck-reveal choices")
        candidate_ids = [
            candidate["cardInstanceId"]
            for candidate in self.pending.get("candidates", [])]
        if card_instance_id not in candidate_ids:
            raise ValueError(
                "Card instance is outside the pending deck-reveal support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_deck_reveal",
                semantic_handle=self.handle,
                fingerprint=self.fingerprint,
                event_id=self.pending["eventId"],
                card_instance_id=card_instance_id))

    def resolve_random_pile_card(self, card_instance_id: int):
        """Resolve a suspended random pile selection by physical card identity."""
        if not self.pending or self.pending.get("kind") != "random_pile_card":
            raise EmulatorError("Expected a random_pile_card Nature event")
        if self.session.capabilities.get(
                "semanticRandomPileCardChoiceSchemaId") != (
                    "sts2-v111-semantic-random-pile-card-v1"):
            raise EmulatorError(
                "Emulator does not expose semantic random pile-card choices")
        candidate_ids = [
            candidate["cardInstanceId"]
            for candidate in self.pending.get("candidates", [])]
        if card_instance_id not in candidate_ids:
            raise ValueError(
                "Card instance is outside the pending random pile-card support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_random_pile_card",
                semantic_handle=self.handle,
                fingerprint=self.fingerprint,
                event_id=self.pending["eventId"],
                card_instance_id=card_instance_id))

    def resolve_random_target(self, enemy_instance_id: int):
        """Resolve a suspended random-enemy effect by enemy identity."""
        if not self.pending or self.pending["kind"] != "random_target":
            raise EmulatorError("Expected a random_target Nature event")
        if self.session.capabilities.get("semanticTargetChoiceSchemaId") != (
                "sts2-v111-semantic-target-choice-v1"):
            raise EmulatorError("Emulator does not expose semantic target identities")
        candidate_ids = [
            candidate["enemyInstanceId"]
            for candidate in self.pending.get("candidates", [])]
        if enemy_instance_id not in candidate_ids:
            raise ValueError("Enemy instance is outside the pending target support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_random_target", semantic_handle=self.handle,
                fingerprint=self.fingerprint, event_id=self.pending["eventId"],
                enemy_instance_id=enemy_instance_id))

    def resolve_generated_card(self, card_model_id: str):
        """Resolve one generated card from its ordered source pool."""
        if not self.pending or self.pending["kind"] != "generated_card":
            raise EmulatorError("Expected a generated_card Nature event")
        if self.session.capabilities.get(
                "semanticGeneratedCardChoiceSchemaId") != (
                    "sts2-v111-semantic-generation-choice-v1"):
            raise EmulatorError(
                "Emulator does not expose semantic generated-card identities")
        candidate_ids = [
            candidate["cardModelId"]
            for candidate in self.pending.get("candidates", [])]
        if card_model_id not in candidate_ids:
            raise ValueError("Card model is outside the pending generation support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_generated_card", semantic_handle=self.handle,
                fingerprint=self.fingerprint, event_id=self.pending["eventId"],
                card_model_id=card_model_id))

    def resolve_potion_generation(self, potion_model_id: str):
        """Resolve one random combat potion by its ordered source model ID."""
        if not self.pending or self.pending.get("kind") != "potion_generation":
            raise EmulatorError("Expected a potion_generation Nature event")
        if self.session.capabilities.get(
                "semanticPotionGenerationChoiceSchemaId") != (
                    "sts2-v111-semantic-potion-generation-v1"):
            raise EmulatorError(
                "Emulator does not expose semantic potion-generation choices")
        candidate_ids = [
            candidate["potionModelId"]
            for candidate in self.pending.get("candidates", [])]
        if potion_model_id not in candidate_ids:
            raise ValueError(
                "Potion model is outside the pending generation support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_potion_generation",
                semantic_handle=self.handle,
                fingerprint=self.fingerprint,
                event_id=self.pending["eventId"],
                potion_model_id=potion_model_id))

    def resolve_card_transform(self, card_model_id: str):
        """Resolve one combat-card replacement from its source pool."""
        if not self.pending or self.pending.get("kind") != "card_transform":
            raise EmulatorError("Expected a card_transform Nature event")
        if self.session.capabilities.get(
                "semanticCardTransformChoiceSchemaId") != (
                    "sts2-v111-semantic-card-transform-v1"):
            raise EmulatorError(
                "Emulator does not expose semantic card-transform choices")
        candidate_ids = [
            candidate["cardModelId"]
            for candidate in self.pending.get("candidates", [])]
        if card_model_id not in candidate_ids:
            raise ValueError("Card model is outside the pending transform support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_card_transform",
                semantic_handle=self.handle,
                fingerprint=self.fingerprint,
                event_id=self.pending["eventId"],
                card_model_id=card_model_id))

    def resolve_cost(self, cost: int):
        """Resolve an opt-in temporary card cost by its legal value."""
        if not self.pending or self.pending.get("kind") not in (
                "drawn_card_cost", "random_hand_card_cost"):
            raise EmulatorError("Expected a semantic card-cost Nature event")
        if self.session.capabilities.get("semanticCostChoiceSchemaId") != (
                "sts2-v111-semantic-cost-choice-v1"):
            raise EmulatorError("Emulator does not expose semantic cost choices")
        candidate_costs = [
            candidate["cost"] for candidate in self.pending.get("candidates", [])]
        if cost not in candidate_costs:
            raise ValueError("Cost value is outside the pending event support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_cost", semantic_handle=self.handle,
                fingerprint=self.fingerprint, event_id=self.pending["eventId"],
                cost=cost))

    def resolve_hand_cost_selection(self, card_instance_id: int):
        """Select the source-eligible hand card for a fixed cost effect."""
        if not self.pending or self.pending.get(
                "kind") != "random_hand_card_selection":
            raise EmulatorError(
                "Expected a random_hand_card_selection Nature event")
        if self.session.capabilities.get(
                "semanticHandCostSelectionChoiceSchemaId") != (
                    "sts2-v111-semantic-hand-cost-selection-v1"):
            raise EmulatorError(
                "Emulator lacks semantic hand-cost selection choices")
        candidates = [candidate["cardInstanceId"]
                      for candidate in self.pending.get("candidates", [])]
        if card_instance_id not in candidates:
            raise ValueError(
                "Card instance is outside the pending hand-cost support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_hand_cost_selection",
                semantic_handle=self.handle,
                fingerprint=self.fingerprint,
                event_id=self.pending["eventId"],
                card_instance_id=card_instance_id))

    def resolve_card_shortlist(self, card_instance_id: int):
        """Select one eligible card for a source random-pile shortlist."""
        if not self.pending or self.pending.get(
                "kind") != "random_pile_selection_shortlist":
            raise EmulatorError(
                "Expected a random_pile_selection_shortlist Nature event")
        if self.session.capabilities.get(
                "semanticCardShortlistChoiceSchemaId") != (
                    "sts2-v111-semantic-card-shortlist-v1"):
            raise EmulatorError(
                "Emulator lacks semantic card-shortlist choices")
        candidates = [candidate["cardInstanceId"]
                      for candidate in self.pending.get("candidates", [])]
        if card_instance_id not in candidates:
            raise ValueError(
                "Card instance is outside the pending shortlist support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_card_shortlist",
                semantic_handle=self.handle,
                fingerprint=self.fingerprint,
                event_id=self.pending["eventId"],
                card_instance_id=card_instance_id))

    def resolve_pool_shuffle(self, swap_index: int):
        """Choose one bounded swap for a full-pool shuffle continuation."""
        if not self.pending or self.pending.get("kind") not in (
                "generated_card_pool_shuffle", "hand_return_shuffle"):
            raise EmulatorError("Expected a full-pool shuffle event")
        if self.session.capabilities.get(
                "semanticPoolShuffleChoiceSchemaId") != (
                    "sts2-v111-semantic-pool-shuffle-v1"):
            raise EmulatorError("Emulator lacks semantic pool-shuffle choices")
        candidates = [
            candidate["swapIndex"]
            for candidate in self.pending.get("candidates", [])]
        if swap_index not in candidates:
            raise ValueError("Swap index is outside the pending event support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_pool_shuffle", semantic_handle=self.handle,
                fingerprint=self.fingerprint, event_id=self.pending["eventId"],
                swap_index=swap_index))

    def resolve_draw_insertion(self, position: int):
        """Insert the pending generated card at a legal draw-pile position."""
        if not self.pending or self.pending.get(
                "kind") != "generated_draw_pile_insertion":
            raise EmulatorError(
                "Expected a generated_draw_pile_insertion event")
        if self.session.capabilities.get(
                "semanticDrawInsertionChoiceSchemaId") != (
                    "sts2-v111-semantic-draw-insertion-v1"):
            raise EmulatorError("Emulator lacks semantic draw-insertion choices")
        positions = [candidate["position"]
                     for candidate in self.pending.get("candidates", [])]
        if position not in positions:
            raise ValueError("Position is outside the pending event support")
        return SemanticCombat.from_wire(
            self.session, self.session.call(
                "semantic_resolve_draw_insertion",
                semantic_handle=self.handle, fingerprint=self.fingerprint,
                event_id=self.pending["eventId"], position=position))

    def enumerate_chance(self, max_outcomes: int = 32) -> "SemanticEnumeration":
        """Fork every legal outcome of one small pending Nature event."""
        if self.session.capabilities.get("semanticEnumerationSchemaId") != (
                "sts2-v111-semantic-enumeration-v1"):
            raise EmulatorError("Emulator lacks semantic chance enumeration")
        capability_limit = self.session.capabilities.get(
            "semanticEnumerationMaximumOutcomes", 256)
        if (not isinstance(max_outcomes, int) or isinstance(max_outcomes, bool)
                or max_outcomes < 1 or max_outcomes > capability_limit):
            raise ValueError(
                f"max_outcomes must be between 1 and {capability_limit}")
        result = self.session.call(
            "semantic_enumerate", semantic_handle=self.handle,
            fingerprint=self.fingerprint, maximum_outcomes=max_outcomes)
        return SemanticEnumeration.from_wire(self.session, result)

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


@dataclass(frozen=True)
class SemanticBranch:
    outcome_id: str
    child: SemanticCombat


@dataclass(frozen=True)
class SemanticEnumeration:
    event_kind: str
    event_id: str
    outcomes: tuple[SemanticBranch, ...]

    @classmethod
    def from_wire(cls, session, payload):
        branches = tuple(
            SemanticBranch(item["outcomeId"],
                           SemanticCombat.from_wire(session, item))
            for item in payload["outcomes"])
        return cls(payload["eventKind"], payload["eventId"], branches)


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
            self.stochastic_session_coverage = self.capabilities.get(
                "stochasticSessionCoverage")
            self.reference_nature_state_snapshot_schema_id = (
                self.capabilities.get("referenceNatureStateSnapshotSchemaId"))
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

    def supports_reference_nature_site(self, site_id: str, *,
                                       session: str = "any") -> bool:
        """Check site coverage for a ReferenceNature continuation schema."""
        fields = {
            "semantic": "semanticReferenceNatureSiteIds",
            "run_action": "referenceNatureRunActionSiteIds",
            "event_choice": "referenceNatureEventChoiceSiteIds",
            "ancient_offer": "referenceNatureAncientOfferSiteIds",
            "merchant": "referenceNatureMerchantSiteIds",
        }
        if session != "any" and session not in fields:
            raise ValueError(
                "session must be semantic, run_action, event_choice, ancient_offer, merchant, or any")
        coverage = self.stochastic_session_coverage
        if not isinstance(coverage, dict) or coverage.get("schemaId") != (
                "sts2-v111-stochastic-session-coverage-v1"):
            return False
        if session == "any":
            return any(site_id in coverage.get(field, [])
                       for field in fields.values())
        return site_id in coverage.get(fields[session], [])

    def supports_chance_law_query(self, site_id: str) -> bool:
        """Check the separate, versioned read-only Chance Law query coverage."""
        coverage = self.capabilities.get(
            "stochasticChanceLawQueryCoverage") or {}
        return (coverage.get("schemaId") ==
                "sts2-v111-stochastic-chance-law-query-coverage-v1"
                and coverage.get("queryOperation") == "chance_law_query"
                and site_id in coverage.get("siteIds", []))

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
            fixture_random_target_card_id: str | None = None,
            fixture_generation_potion_id: str | None = None,
            fixture_selection_card_id: str | None = None,
            fixture_nature_draw_costs: bool = False,
            fixture_pool_generation_potion_id: str | None = None,
            fixture_generated_draw_insertion_card_id: str | None = None,
            fixture_random_free_attack_draw_insertion: bool = False,
            fixture_randomize_hand_costs_max_exclusive: int | None = None,
            fixture_set_random_hand_card_cost: int | None = None,
            fixture_potion_generation_card_id: str | None = None,
            replay_opening_draw_hooks: bool = False,
            native_policy_baseline: bool = False,
            source_route_entry: bool = False,
            route_region: str = "Overgrowth",
            route_neow_choice_id: str | None = None,
            route_map_node_id: str | None = None):
        """Begin a two-site pilot without external opening chance tickets.

        By default setup uses the isolated seeded fixture. Set
        source_route_entry through begin_seeded_semantic_route() to run the
        configured Act 1 route and real native weak-combat entry. The
        optional random-target fixture adds Ricochet to the opening hand for
        semantic target-boundary integration checks; the generation fixture
        adds Orobic Acid to expose three ordered generated-card pools.
        The selection fixture can replace the first card with Survivor for
        an ordinary player choice or Seeker Strike for a random pile shortlist.
        The pool-generation fixture adds a Colorless Potion for typed
        full-pool shuffle choices followed by an ordinary card selection.
        The draw-insertion fixture begins at a source-authentic generated
        card insertion boundary for the requested card model.
        The random-Attack insertion fixture exposes both source model choice
        and insertion position stages.
        The hand-cost fixture exposes one cost event per eligible card.
        The fixed hand-cost fixture exposes the eligible card-selection event.
        The potion-generation card fixture places Alchemize in the opening
        hand to expose its ordered random-potion model pool.
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
            fixture_random_target_card_id=fixture_random_target_card_id,
            fixture_generation_potion_id=fixture_generation_potion_id,
            fixture_selection_card_id=fixture_selection_card_id,
            fixture_nature_draw_costs=fixture_nature_draw_costs,
            fixture_pool_generation_potion_id=
                fixture_pool_generation_potion_id,
            fixture_generated_draw_insertion_card_id=
                fixture_generated_draw_insertion_card_id,
            fixture_random_free_attack_draw_insertion=
                fixture_random_free_attack_draw_insertion,
            fixture_randomize_hand_costs_max_exclusive=
                fixture_randomize_hand_costs_max_exclusive,
            fixture_set_random_hand_card_cost=
                fixture_set_random_hand_card_cost,
            fixture_potion_generation_card_id=
                fixture_potion_generation_card_id,
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

    def load_reference_nature_run_action_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureRunActionSessionSchemaId") != (
                "sts2-v111-reference-nature-run-action-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature run-action sessions")
        result = self.call("reference_nature_run_action_load", snapshot=snapshot)
        return ReferenceNatureRunActionSession.from_wire(
            self, result["result"])

    def release_reference_nature_run_action_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature run-action handle belongs to another session")
        return self.call(
            "reference_nature_run_action_release_many",
            reference_nature_run_action_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_event_choice_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureEventChoiceSessionSchemaId") != (
                "sts2-v111-reference-nature-event-choice-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature event-choice sessions")
        result = self.call("reference_nature_event_choice_load",
                           snapshot=snapshot)
        return ReferenceNatureEventChoiceSession.from_wire(
            self, result["result"])

    def release_reference_nature_event_choice_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature event-choice handle belongs to another session")
        return self.call(
            "reference_nature_event_choice_release_many",
            reference_nature_event_choice_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_ancient_offer_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureAncientOfferSessionSchemaId") != (
                "sts2-v111-reference-nature-ancient-offer-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Ancient-offer sessions")
        result = self.call("reference_nature_ancient_offer_load",
                           snapshot=snapshot)
        return ReferenceNatureAncientOfferSession.from_wire(
            self, result["result"])

    def release_reference_nature_ancient_offer_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Ancient-offer handle belongs to another session")
        return self.call(
            "reference_nature_ancient_offer_release_many",
            reference_nature_ancient_offer_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_merchant_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureMerchantSessionSchemaId") != (
                "sts2-v111-reference-nature-merchant-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature merchant sessions")
        result = self.call("reference_nature_merchant_load",
                           snapshot=snapshot)
        return ReferenceNatureMerchantSession.from_wire(
            self, result["result"])

    def release_reference_nature_merchant_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature merchant handle belongs to another session")
        return self.call(
            "reference_nature_merchant_release_many",
            reference_nature_merchant_handles=[
                item.handle for item in sessions])["released"]

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
