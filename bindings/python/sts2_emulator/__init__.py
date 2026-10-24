"""Thin, version-negotiated JSONL binding. Canonical mechanics stay in C#."""
from dataclasses import dataclass
import hashlib
import json
import subprocess
import threading


class EmulatorError(RuntimeError):
    pass


def _canonical_protocol_hash(value):
    encoded = json.dumps(value, ensure_ascii=True, sort_keys=True,
                         separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


@dataclass(frozen=True)
class State:
    session: "Session"
    handle: str

    def actions(self):
        if self.session._fair_observation_supported:
            return self.session.observe_fair(
                "run_state", self.handle)["actions"]
        return self.session.call("legal_actions", state_handle=self.handle)["actions"]

    def observe(self):
        if self.session._fair_observation_supported:
            return self.session.observe_fair(
                "run_state", self.handle)["observation"]
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

    def query_deck_prefix_distribution(self, prefix_length: int, *,
                                       maximum_orders: int = 4096,
                                       audit_sequential: bool = False):
        """Return exact Nature deck prefix counts under their UNVERIFIED model.

        Set ``audit_sequential`` to compare the result with composed QueryDraw
        supports. The optional audit is limited to prefixes of at most 64.
        """
        return self.session.query_semantic_deck_prefix_distribution(
            self.handle, prefix_length, maximum_orders=maximum_orders,
            audit_sequential=audit_sequential)

    def query_chance_law(self, site_id: str, *, ordinal: int = 0,
                         prior_indices=None, card_type=None, rarity=None,
                         excluded_potion_ids=None, card_id=None,
                         potion_id=None, relic_id=None,
                         colorless_offer=False, sale_reprice=False,
                         owner_id=None, offer_ordinal=None,
                         relic_candidate_ids=None, excluded_relic_ids=None,
                         action_id=None, prior_variant_index=None,
                         event_id=None, support_count=None,
                         first_boss=None, ascension=None, act=None,
                         include_darv=None, hive_receives_darv=None,
                         room=None, gold_proportion=None,
                         current_thousandths=None, elite=None,
                         forced=None, card_rarity_source=None,
                         current_offset_basis_points=None,
                         stream_id=None, native_encounter_entry=None,
                         total_floor=None, ordered_hp_support=None):
        """Read a pre-outcome law; native event queries may need action_id.

        Reflections card-pick laws use action_id="touch_mirror" and the
        complete prior index prefix.
        Native event gold laws require event_id as an explicit branch
        condition, plus the pending Event-node action_id and stage ordinal.
        Darv relic-shuffle laws accept prior_variant_index to bind the earlier
        offer-variant decision to the query fingerprint.
        Glory second-boss laws require first_boss and ascension.
        Later-act boss-pool laws require act=2 or act=3 and describe the raw
        pool index before the first-undiscovered-boss override.
        Later-act Ancient laws require act and include_darv; Glory Darv
        assignment laws require hive_receives_darv.
        Combat-gold laws require room, ascension and the source gold_proportion;
        Elite and Boss queries use fixed room bounds and need no proportion.
        Combat potion-odds laws require current_thousandths, elite, and forced;
        a forced reward has no odds draw and is rejected.
        Direct card-rarity laws require card_rarity_source, current offset,
        Ascension, and owner_id; outcomes are raw rarities before pool fallback.
        Combat card-upgrade laws require card_id, Act, and Ascension; outcomes
        are the raw inclusive comparison result before card mutation.
        Encounter card-model index laws require owner_id and a positive
        support_count; the source-ordered remaining pool is not validated.
        Relic queue-shuffle laws require owner_id and the current positive
        Fisher-Yates bound; queue eligibility and its prior prefix are open.
        Draw-pile shuffle laws require owner_id and the current positive bound;
        pile ordering and the combat continuation are not validated.
        Native encounter-formation index laws take native_encounter_entry,
        total_floor, support_count, and ordinal. They describe the exact
        bounded transform for one declared index, not the formation pool or
        preceding GenerateMonsters call prefix. The owner and context stream
        are derived from the declared entry and floor.
        Native initial unique-HP laws take owner_id="creature:<CombatId>",
        ordered_hp_support, and ordinal. They bind the declared HP values and
        order to the exact run.niche index law; pool construction remains with
        the caller. Tough Egg hatch HP laws take the same creature owner,
        Ascension, and ordinal; their ordered HP support is derived from the
        source interval.
        PotionFactory and relic rarity laws accept a caller-declared owner_id
        + stream_id pair for cursor-blind source-threshold queries.
        Repy potion laws use ordinal 0 or 1 and prior_indices containing the
        complete relic-shuffle prefix plus any earlier potion index.
        """
        if not self.session.supports_chance_law_query(site_id):
            raise EmulatorError(
                f"No direct Chance Law query is advertised for site {site_id!r}")
        if type(ordinal) is not int or ordinal < 0:
            raise ValueError("ordinal must be a nonnegative integer")
        payload = {"state_handle": self.handle,
                   "site_id": site_id, "ordinal": ordinal}
        if action_id is not None:
            if not isinstance(action_id, str) or not action_id:
                raise ValueError("action_id must be a nonempty string")
            payload["action_id"] = action_id
        if event_id is not None:
            if not isinstance(event_id, str) or not event_id:
                raise ValueError("event_id must be a nonempty string")
            payload["event_id"] = event_id
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
        if support_count is not None:
            if type(support_count) is not int or support_count <= 0:
                raise ValueError("support_count must be a positive integer")
            payload["support_count"] = support_count
        if ordered_hp_support is not None:
            if (not isinstance(ordered_hp_support, (list, tuple)) or
                    not ordered_hp_support or
                    any(type(hp) is not int or hp <= 0
                        or hp > 2_147_483_647
                        for hp in ordered_hp_support) or
                    len(set(ordered_hp_support)) !=
                        len(ordered_hp_support)):
                raise ValueError(
                    "ordered_hp_support must contain distinct positive 32-bit HP integers")
            payload["ordered_hp_support"] = list(ordered_hp_support)
        if site_id == "combat.initial_unique_hp":
            if owner_id is None or not owner_id.startswith("creature:"):
                raise ValueError(
                    "Native initial-HP laws require owner_id=creature:<CombatId>")
            creature_id_text = owner_id[len("creature:"):]
            try:
                creature_id = int(creature_id_text)
            except ValueError as error:
                raise ValueError(
                    "Native initial-HP owner must contain a CombatId") from error
            if (creature_id < 0 or creature_id > 9_223_372_036_854_775_807 or
                    owner_id != f"creature:{creature_id}"):
                raise ValueError(
                    "Native initial-HP owner must contain a canonical CombatId")
            if ordered_hp_support is None:
                raise ValueError(
                    "Native initial-HP laws require ordered_hp_support")
            if support_count is not None:
                raise ValueError(
                    "Native initial-HP laws use ordered_hp_support, not support_count")
        elif site_id == "combat.tough_egg.hatchling_hp":
            if owner_id is None or not owner_id.startswith("creature:"):
                raise ValueError(
                    "Tough Egg hatch HP laws require owner_id=creature:<CombatId>")
            creature_id_text = owner_id[len("creature:"):]
            try:
                creature_id = int(creature_id_text)
            except ValueError as error:
                raise ValueError(
                    "Tough Egg hatch HP owner must contain a CombatId") from error
            if (creature_id < 0 or creature_id > 9_223_372_036_854_775_807 or
                    owner_id != f"creature:{creature_id}"):
                raise ValueError(
                    "Tough Egg hatch HP owner must contain a canonical CombatId")
            if type(ascension) is not int or ascension < 0:
                raise ValueError(
                    "Tough Egg hatch HP laws require nonnegative ascension")
            if ordinal != 0:
                raise ValueError(
                    "Tough Egg hatch HP is the creature's ordinal-zero draw")
            if support_count is not None:
                raise ValueError(
                    "Tough Egg hatch HP support is derived from ascension")
        elif ordered_hp_support is not None:
            raise ValueError(
                "ordered_hp_support applies only to combat.initial_unique_hp")
        if native_encounter_entry is not None:
            if not isinstance(native_encounter_entry, str) or \
                    not native_encounter_entry.strip():
                raise ValueError(
                    "native_encounter_entry must be a nonempty string")
        if total_floor is not None and (
                type(total_floor) is not int or total_floor < 0):
            raise ValueError("total_floor must be a nonnegative integer")
        if site_id == "combat.encounter.formation.index_draw" and (
                native_encounter_entry is not None or total_floor is not None):
            if native_encounter_entry is None or total_floor is None:
                raise ValueError(
                    "Native encounter-formation laws require native_encounter_entry and total_floor together")
            if support_count is None:
                raise ValueError(
                    "Native encounter-formation laws require support_count")
            if owner_id is not None or stream_id is not None:
                raise ValueError(
                    "Native encounter-formation laws derive owner_id and stream_id from native_encounter_entry and total_floor")
            payload["native_encounter_entry"] = native_encounter_entry
            payload["total_floor"] = total_floor
        elif native_encounter_entry is not None or total_floor is not None:
            raise ValueError(
                "native_encounter_entry and total_floor apply only to combat.encounter.formation.index_draw")
        if site_id in (
                "card_rarity.encounter_model.index_draw",
                "relic_grab_bag.queue_shuffle.swap_index",
                "reward.boss_relic_offer.shuffle_swap_index",
                "reward.lava_rock.relic_offer.shuffle_swap_index",
                "combat.draw_pile.shuffle.swap"):
            if owner_id is None:
                raise ValueError(
                    "this Chance Law requires owner_id")
            if support_count is None:
                raise ValueError(
                    "this Chance Law requires support_count")
        if first_boss is not None:
            if not isinstance(first_boss, str) or not first_boss:
                raise ValueError("first_boss must be a nonempty encounter ID")
            payload["first_boss"] = first_boss
        if ascension is not None:
            if type(ascension) is not int or ascension < 0:
                raise ValueError("ascension must be a nonnegative integer")
            payload["ascension"] = ascension
        if act is not None:
            valid_act = type(act) is int and (
                act >= 1 if site_id ==
                "card_rarity.upgrade_roll.unit_float" else act in (2, 3))
            if not valid_act:
                raise ValueError(
                    "act must be a positive integer for card-upgrade laws or 2/3 for later-act boss queries")
            payload["act"] = act
        if include_darv is not None:
            if type(include_darv) is not bool:
                raise ValueError("include_darv must be a boolean")
            payload["include_darv"] = include_darv
        if hive_receives_darv is not None:
            if type(hive_receives_darv) is not bool:
                raise ValueError("hive_receives_darv must be a boolean")
            payload["hive_receives_darv"] = hive_receives_darv
        if room is not None:
            if not isinstance(room, str) or room not in (
                    "Combat", "Elite", "Boss"):
                raise ValueError("room must be Combat, Elite, or Boss")
            if ascension is None:
                raise ValueError("combat gold laws require ascension")
            if room == "Combat" and gold_proportion is None:
                raise ValueError(
                    "normal Combat gold laws require gold_proportion")
            if room != "Combat" and gold_proportion is not None:
                raise ValueError(
                    "gold_proportion is only used for normal Combat")
            payload["room"] = room
        elif gold_proportion is not None:
            raise ValueError("gold_proportion requires a combat gold room")
        if gold_proportion is not None:
            if isinstance(gold_proportion, bool) or not isinstance(
                    gold_proportion, (int, float)):
                raise ValueError("gold_proportion must be a number in [0, 1]")
            proportion = float(gold_proportion)
            if not 0.0 <= proportion <= 1.0:
                raise ValueError("gold_proportion must be a number in [0, 1]")
            payload["gold_proportion"] = proportion
        if site_id == "reward.potion_odds.unit_float":
            if type(current_thousandths) is not int or \
                    current_thousandths % 100 != 0:
                raise ValueError(
                    "current_thousandths must be an integer multiple of 100")
            if type(elite) is not bool or type(forced) is not bool:
                raise ValueError(
                    "elite and forced must be declared booleans")
            if forced:
                raise ValueError(
                    "forced potion rewards bypass the odds draw")
            payload["current_thousandths"] = current_thousandths
            payload["elite"] = elite
            payload["forced"] = forced
        elif any(value is not None for value in (
                current_thousandths, elite, forced)):
            raise ValueError(
                "potion odds parameters apply only to reward.potion_odds.unit_float")
        if site_id == "card_rarity.roll.unit_float" and (
                card_rarity_source is not None or
                current_offset_basis_points is not None):
            if card_rarity_source not in (
                    "RegularEncounter", "EliteEncounter",
                    "BossEncounter", "Shop"):
                raise ValueError(
                    "card_rarity_source must be RegularEncounter, EliteEncounter, BossEncounter, or Shop")
            if type(current_offset_basis_points) is not int or \
                    not -500 <= current_offset_basis_points <= 4000 or \
                    current_offset_basis_points % 50 != 0:
                raise ValueError(
                    "current_offset_basis_points must be a 50-basis-point value in [-500, 4000]")
            if ascension is None:
                raise ValueError("Native card-rarity laws require ascension")
            if owner_id is None:
                raise ValueError("Native card-rarity laws require owner_id")
            payload["card_rarity_source"] = card_rarity_source
            payload["current_offset_basis_points"] = \
                current_offset_basis_points
        elif card_rarity_source is not None or \
                current_offset_basis_points is not None:
            raise ValueError(
                "card-rarity source parameters apply only to card_rarity.roll.unit_float")
        if stream_id is not None:
            if not isinstance(stream_id, str) or not stream_id:
                raise ValueError("stream_id must be a nonempty string")
            if site_id not in (
                    "potion_factory.rarity_roll.unit_float",
                    "relic_grab_bag.rarity.unit_float"):
                raise ValueError(
                    "stream_id applies only to Native PotionFactory or relic rarity laws")
        if site_id == "potion_factory.rarity_roll.unit_float" and (
                owner_id is not None or stream_id is not None):
            if owner_id is None or stream_id is None:
                raise ValueError(
                    "PotionFactory rarity laws require owner_id and stream_id together")
            payload["stream_id"] = stream_id
        if site_id == "relic_grab_bag.rarity.unit_float" and \
                stream_id is not None:
            if owner_id is None:
                raise ValueError(
                    "Native relic rarity laws require owner_id with stream_id")
            payload["stream_id"] = stream_id
        if site_id == "card_rarity.upgrade_roll.unit_float" and (
                card_id is None or act is None or ascension is None):
            raise ValueError(
                "card-upgrade laws require card_id, act, and ascension")
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
        if prior_variant_index is not None:
            if (type(prior_variant_index) is not int or
                    prior_variant_index not in (0, 1)):
                raise ValueError("prior_variant_index must be 0 or 1")
            payload["prior_variant_index"] = prior_variant_index
        result = self.session.call(
            "chance_law_query", **payload)
        law = result.get("law")
        self.session._validate_chance_law(
            law, expected_site_id=site_id, expected_ordinal=ordinal)
        return law

    def save_snapshot(self):
        """Return hidden exact state; this is not a fair-policy observation."""
        snapshot = self.session.call(
            "save_snapshot", state_handle=self.handle)["snapshot"]
        self.session._validate_run_snapshot_schema(snapshot)
        return snapshot

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
        """Inspect a pending event or event-action boundary from a snapshot."""
        if self.session.capabilities.get("controlledEventChoiceSchemaId") != (
                "sts2-v111-controlled-event-choice-v1"):
            raise EmulatorError("Emulator does not expose controlled event choices")
        result = self.session.call(
            "controlled_event_inspect", state_handle=self.handle)
        return ControlledEventChoice.from_wire(self.session, result)

    def advance_controlled_event_action(self, action_id: str):
        """Apply one legal event player action under controlled chance."""
        if self.session.capabilities.get("controlledEventChoiceSchemaId") != (
                "sts2-v111-controlled-event-choice-v1"):
            raise EmulatorError("Emulator does not expose controlled event choices")
        result = self.session.call(
            "controlled_event_advance", state_handle=self.handle,
            action_id=action_id)
        return ControlledEventChoice.from_wire(self.session, result)

    def advance_controlled_event_reward(self, action_id: str):
        """Backward-compatible alias for advance_controlled_event_action()."""
        return self.advance_controlled_event_action(action_id)

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

    def begin_scripted_run(self):
        """Begin whole-run replay with typed outcomes supplied by the caller."""
        if self.session.capabilities.get("scriptedRunSessionSchemaId") != (
                "sts2-v111-scripted-run-session-v1"):
            raise EmulatorError(
                "Emulator does not expose whole-run scripted sessions")
        operations = self.session.capabilities.get(
            "scriptedRunSessionOperations", [])
        if (not isinstance(operations, list) or
                "scripted_run_begin" not in operations):
            raise EmulatorError(
                "Whole-run scripted begin is not advertised")
        result = self.session.call(
            "scripted_run_begin", state_handle=self.handle)
        return ScriptedRunSession.from_wire(self.session, result["result"])

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

    def begin_reference_nature_neow_offer(self,
                                           nature_seed: int):
        """Begin the atomic Neow offer session from a pristine run-start state."""
        if self.session.capabilities.get(
                "referenceNatureNeowOfferSessionSchemaId") != (
                "sts2-v111-reference-nature-neow-offer-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Neow-offer sessions")
        if not self.session.supports_reference_nature_site(
                "event.neow.cursed_relic.index_draw", session="neow_offer"):
            raise EmulatorError(
                "No ReferenceNature Neow-offer session is advertised")
        if (not isinstance(nature_seed, int) or isinstance(nature_seed, bool) or
                nature_seed < 0 or nature_seed > 0xFFFFFFFFFFFFFFFF):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        result = self.session.call(
            "reference_nature_neow_offer_begin",
            state_handle=self.handle, nature_seed=nature_seed)
        return ReferenceNatureNeowOfferSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_trash_heap(self, choice_id: str,
                                          nature_seed: int):
        """Begin an atomic Trash Heap random reward session."""
        if self.session.capabilities.get(
                "referenceNatureTrashHeapSessionSchemaId") != (
                "sts2-v111-reference-nature-trash-heap-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Trash Heap sessions")
        sites = {
            "grab": "event.trash_heap.card_reward.index_draw",
            "dive": "event.trash_heap.relic_reward.index_draw",
        }
        if choice_id not in sites:
            raise ValueError("choice_id must be 'grab' or 'dive'")
        if not self.session.supports_reference_nature_site(
                sites[choice_id], session="trash_heap"):
            raise EmulatorError(
                "No ReferenceNature Trash Heap session is advertised")
        if (not isinstance(nature_seed, int) or isinstance(nature_seed, bool) or
                nature_seed < 0 or nature_seed > 0xFFFFFFFFFFFFFFFF):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        result = self.session.call(
            "reference_nature_trash_heap_begin",
            state_handle=self.handle, choice_id=choice_id,
            nature_seed=nature_seed)
        return ReferenceNatureTrashHeapSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_tablet_of_truth(
            self, nature_seed: int | None = None):
        """Begin or continue one Tablet of Truth decipher draw."""
        if self.session.capabilities.get(
                "referenceNatureTabletOfTruthSessionSchemaId") != (
                "sts2-v111-reference-nature-tablet-of-truth-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Tablet of Truth sessions")
        site_id = "event.tablet_of_truth.card.index_draw"
        if not self.session.supports_reference_nature_site(
                site_id, session="tablet_of_truth"):
            raise EmulatorError(
                "No ReferenceNature Tablet of Truth session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_tablet_of_truth_begin", **payload)
        return ReferenceNatureTabletOfTruthSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_trial(
            self, nature_seed: int | None = None):
        """Begin or continue the Glory Trial three-case choice."""
        if self.session.capabilities.get(
                "referenceNatureTrialSessionSchemaId") != (
                "sts2-v111-reference-nature-trial-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Trial sessions")
        site_id = "event.glory_trial.damage_amount.index_draw"
        if not self.session.supports_reference_nature_site(
                site_id, session="trial"):
            raise EmulatorError(
                "No ReferenceNature Trial session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call("reference_nature_trial_begin", **payload)
        return ReferenceNatureTrialSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_native_event_shuffle(
            self, nature_seed: int | None = None,
            action_id: str | None = None,
            site_id: str | None = None):
        """Begin a conditional Native event or victory-hook replay."""
        if self.session.capabilities.get(
                "referenceNatureNativeEventShuffleSessionSchemaId") != (
                "sts2-v111-reference-nature-native-event-shuffle-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature native event shuffle sessions")
        if action_id is not None and (
                not isinstance(action_id, str) or not action_id):
            raise ValueError("action_id must be a nonempty string")
        if site_id is not None and (
                not isinstance(site_id, str) or not site_id):
            raise ValueError("site_id must be a nonempty string")
        resolved_site_id = site_id or {
            None: "event.doors_light.shuffle_swap_index",
            "light": "event.doors_light.shuffle_swap_index",
            "fragrant_mushroom":
                "event.glory.random_upgrade.shuffle_swap_index",
            "choose_card_type":
                "event.glory_tinker.type_offer.shuffle_swap_index",
            "attack": "event.glory_tinker.rider_offer.shuffle_swap_index",
            "skill": "event.glory_tinker.rider_offer.shuffle_swap_index",
            "power": "event.glory_tinker.rider_offer.shuffle_swap_index",
            "unlock_chest":
                "event.war_historian.repy.relic_offer.shuffle_swap_index",
        }.get(action_id)
        if resolved_site_id is None:
            raise ValueError(
                "Provide a registered site_id or use a supported event action_id")
        if not self.session.supports_reference_nature_site(
                resolved_site_id, session="native_shuffle"):
            raise EmulatorError(
                "No ReferenceNature Native shuffle session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle}
        if action_id is not None:
            payload["action_id"] = action_id
        if site_id is not None:
            payload["site_id"] = site_id
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_native_event_shuffle_begin", **payload)
        return ReferenceNatureNativeEventShuffleSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_native_event_gold(
            self, action_id: str, nature_seed: int | None = None):
        """Control supported gold, Stone potion-slot, or Future potion-card draws during Native event entry."""
        if self.session.capabilities.get(
                "referenceNatureNativeEventGoldSessionSchemaId") != (
                "sts2-v111-reference-nature-native-event-gold-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Native event gold sessions")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be a nonempty string")
        site_ids = (
            "event.generated_gold.unit_float",
            "event.generated_gold.index_draw",
            "event.stone_of_all_time.potion_slot.index_draw",
            "event.future_of_potions.card_type.index_draw")
        if not all(self.session.supports_reference_nature_site(
                site_id, session="native_event_gold")
                for site_id in site_ids):
            raise EmulatorError(
                "No ReferenceNature Native event gold session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_native_event_gold_begin", **payload)
        return ReferenceNatureNativeEventGoldSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_native_event_relic(
            self, choice_id: str, nature_seed: int | None = None):
        """Control supported Native event relic draws atomically."""
        if self.session.capabilities.get(
                "referenceNatureNativeEventRelicSessionSchemaId") != (
                "sts2-v111-reference-nature-native-event-relic-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Native event relic sessions")
        if not isinstance(choice_id, str) or not choice_id:
            raise ValueError("choice_id must be a nonempty string")
        site_ids = (
            "relic_grab_bag.rarity.unit_float",
            "treasure.relic_grab_bag.model.index_draw")
        if not all(self.session.supports_reference_nature_site(
                site_id, session="native_event_relic")
                for site_id in site_ids):
            raise EmulatorError(
                "No ReferenceNature Native event relic session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "choice_id": choice_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_native_event_relic_begin", **payload)
        return ReferenceNatureNativeEventRelicSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_native_neow_relic_pickup(
            self, action_id: str, nature_seed: int | None = None):
        """Control relic draws triggered by a supported Native Neow pickup."""
        return self._begin_reference_nature_native_event_relic_action(
            action_id, nature_seed)

    def begin_reference_nature_native_battleworn_relic_reward(
            self, action_id: str, nature_seed: int | None = None):
        """Control the relic reward from a legal setting-three victory."""
        return self._begin_reference_nature_native_event_relic_action(
            action_id, nature_seed)

    def begin_reference_nature_native_pael_wing_relic_reward(
            self, action_id: str, nature_seed: int | None = None):
        """Control Pael's Wing's relic draw on every second card sacrifice."""
        return self._begin_reference_nature_native_event_relic_action(
            action_id, nature_seed)

    def _begin_reference_nature_native_event_relic_action(
            self, action_id: str, nature_seed: int | None):
        if self.session.capabilities.get(
                "referenceNatureNativeEventRelicSessionSchemaId") != (
                "sts2-v111-reference-nature-native-event-relic-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Native event relic sessions")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be a nonempty string")
        site_ids = (
            "relic_grab_bag.rarity.unit_float",
            "treasure.relic_grab_bag.model.index_draw")
        if not all(self.session.supports_reference_nature_site(
                site_id, session="native_event_relic")
                for site_id in site_ids):
            raise EmulatorError(
                "No ReferenceNature Native event relic session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_native_event_relic_begin", **payload)
        return ReferenceNatureNativeEventRelicSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_native_shovel(
            self, nature_seed: int | None = None):
        """Control the rarity and identity for a configured Native Shovel dig."""
        if self.session.capabilities.get(
                "referenceNatureNativeShovelSessionSchemaId") != (
                "sts2-v111-reference-nature-native-shovel-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Native Shovel sessions")
        site_ids = (
            "relic_grab_bag.rarity.unit_float",
            "treasure.relic_grab_bag.model.index_draw")
        if not all(self.session.supports_reference_nature_site(
                site_id, session="native_shovel")
                for site_id in site_ids):
            raise EmulatorError(
                "No ReferenceNature Native Shovel session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_native_shovel_begin", **payload)
        return ReferenceNatureNativeShovelSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_endless_conveyor_dish(
            self, action_id: str, nature_seed: int | None = None):
        """Begin Nature control of one weighted Conveyor roll-producing action."""
        if self.session.capabilities.get(
                "referenceNatureEndlessConveyorSessionSchemaId") != (
                "sts2-v111-reference-nature-endless-conveyor-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Endless Conveyor sessions")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be a nonempty string")
        site_id = "event.endless_conveyor.dish_weighted_ticket"
        if not self.session.supports_reference_nature_site(
                site_id, session="endless_conveyor"):
            raise EmulatorError(
                "No ReferenceNature Endless Conveyor session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_endless_conveyor_begin", **payload)
        return ReferenceNatureEndlessConveyorSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_glory_reflections(
            self, nature_seed: int | None = None):
        """Begin atomic Nature replay of Reflections' touch_mirror picks."""
        if self.session.capabilities.get(
                "referenceNatureGloryReflectionsSessionSchemaId") != (
                "sts2-v111-reference-nature-glory-reflections-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Glory Reflections sessions")
        sites = (
            "event.glory_reflections.downgrade_card.index_draw",
            "event.glory_reflections.upgrade_card.index_draw")
        if not all(self.session.supports_reference_nature_site(
                site_id, session="glory_reflections") for site_id in sites):
            raise EmulatorError(
                "No ReferenceNature Glory Reflections session coverage is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_glory_reflections_begin", **payload)
        return ReferenceNatureGloryReflectionsSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_infested_automaton(
            self, action_id: str, nature_seed: int | None = None):
        """Begin Nature sampling for Infested Automaton's selected card offer."""
        if self.session.capabilities.get(
                "referenceNatureInfestedAutomatonSessionSchemaId") != (
                "sts2-v111-reference-nature-infested-automaton-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Infested Automaton sessions")
        site_id = "event.hive.card_offer.index_draw"
        if not self.session.supports_reference_nature_site(
                site_id, session="infested_automaton"):
            raise EmulatorError(
                "No ReferenceNature Infested Automaton session is advertised")
        if action_id not in ("study", "touch_core"):
            raise ValueError("action_id must be 'study' or 'touch_core'")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_infested_automaton_begin", **payload)
        return ReferenceNatureInfestedAutomatonSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_punch_off_rewards(
            self, action_id: str, nature_seed: int | None = None):
        """Begin reward sampling for a legal Punch Off action that wins now."""
        if self.session.capabilities.get(
                "referenceNaturePunchOffRewardSessionSchemaId") != (
                "sts2-v111-reference-nature-punch-off-reward-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Punch Off reward sessions")
        sites = (
            "event.punch_off.relic_reward.index_draw",
            "event.punch_off.potion_reward.index_draw")
        if not all(self.session.supports_reference_nature_site(
                site_id, session="punch_off_reward") for site_id in sites):
            raise EmulatorError(
                "No ReferenceNature Punch Off reward session is advertised")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be a legal immediate winning action ID")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_punch_off_reward_begin", **payload)
        return ReferenceNaturePunchOffRewardSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_punch_off_fight(
            self, action_id: str, nature_seed: int | None = None):
        """Begin Nature sampling for Punch Off's two construct HP offsets."""
        if self.session.capabilities.get(
                "referenceNaturePunchOffFightSessionSchemaId") != (
                "sts2-v111-reference-nature-punch-off-fight-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Punch Off fight sessions")
        site_id = "event.punch_off.construct_hp_offset.index_draw"
        if not self.session.supports_reference_nature_site(
                site_id, session="punch_off_fight"):
            raise EmulatorError(
                "No ReferenceNature Punch Off fight session is advertised")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be the legal Punch Off fight action ID")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_punch_off_fight_begin", **payload)
        return ReferenceNaturePunchOffFightSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_merchant(self, action_id: str,
                                        nature_seed: int | None = None):
        """Begin a configured Shop entry or Alchemical Coffer purchase session."""
        if self.session.capabilities.get(
                "referenceNatureMerchantSessionSchemaId") != (
                "sts2-v111-reference-nature-merchant-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature merchant sessions")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be a nonempty legal configured Shop action ID")
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

    def begin_reference_nature_treasure(self, action_id: str,
                                        nature_seed: int | None = None):
        """Begin atomic ReferenceNature generation for a configured Treasure node."""
        if self.session.capabilities.get(
                "referenceNatureTreasureSessionSchemaId") != (
                "sts2-v111-reference-nature-treasure-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature treasure sessions")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be a nonempty legal Treasure-node action ID")
        if not self.session.supports_reference_nature_site(
                "relic_grab_bag.rarity.unit_float", session="treasure"):
            raise EmulatorError(
                "No ReferenceNature treasure session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call("reference_nature_treasure_begin", **payload)
        return ReferenceNatureTreasureSession.from_wire(
            self.session, result["result"])

    def begin_reference_nature_combat_reward(
            self, action_id: str, nature_seed: int | None = None):
        """Begin Nature sampling for a legal action that wins Native combat."""
        if self.session.capabilities.get(
                "referenceNatureCombatRewardSessionSchemaId") != (
                "sts2-v111-reference-nature-combat-reward-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature combat reward sessions")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError(
                "action_id must be a nonempty legal winning combat action ID")
        if not self.session.supports_reference_nature_site(
                "reward.potion_odds.unit_float", session="combat_reward"):
            raise EmulatorError(
                "No ReferenceNature combat reward session is advertised")
        if (nature_seed is not None and
                (not isinstance(nature_seed, int) or
                 isinstance(nature_seed, bool) or nature_seed < 0 or
                 nature_seed > 0xFFFFFFFFFFFFFFFF)):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        payload = {"state_handle": self.handle, "action_id": action_id}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        result = self.session.call(
            "reference_nature_combat_reward_begin", **payload)
        return ReferenceNatureCombatRewardSession.from_wire(
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
class ScriptedRunSession:
    """Whole-run replay over ordinary actions and typed random outcomes."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str
    boundary: str
    completed_action_count: int
    active_action_id: str | None
    next_sequence: int | None
    pending: dict | None
    decision_transcript: list
    legal_actions: list

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-scripted-run-session-v1"):
            raise EmulatorError(
                "Unsupported whole-run scripted session schema")
        state_handle = payload.get("runStateHandle")
        if not isinstance(state_handle, str) or not state_handle:
            raise EmulatorError(
                "Scripted run response omitted its committed run state")
        return cls(
            session, payload["scriptedRunHandle"], payload["fingerprint"],
            state_handle, payload["boundary"],
            payload["completedActionCount"], payload.get("activeActionId"),
            payload.get("nextSequence"), payload.get("pending"),
            payload.get("decisionTranscript", []),
            payload.get("legalActions", []))

    @property
    def complete(self):
        return self.boundary == "terminal"

    @property
    def state(self):
        """Last committed run boundary, even while an action is suspended."""
        return State(self.session, self.state_handle)

    def actions(self):
        return self.legal_actions

    def begin_action(self, action_id: str):
        if self.boundary != "player":
            raise EmulatorError(
                "Scripted run is not at a player action boundary")
        if not isinstance(action_id, str) or not action_id:
            raise ValueError("action_id must be a nonempty string")
        if not any(action.get("actionId") == action_id
                   for action in self.legal_actions):
            raise ValueError("action_id is not currently legal")
        result = self.session.call(
            "scripted_run_begin_action", scripted_run_handle=self.handle,
            fingerprint=self.fingerprint, action_id=action_id)
        return ScriptedRunSession.from_wire(self.session, result["result"])

    def resolve_next(self, transcript_entry: dict, candidate_labels=None):
        if self.boundary != "scripted" or self.pending is None:
            raise EmulatorError("Scripted run has no pending random request")
        if not isinstance(transcript_entry, dict):
            raise TypeError("transcript_entry must be a dictionary")
        payload = {
            "scripted_run_handle": self.handle,
            "fingerprint": self.fingerprint,
            "transcript_entry": transcript_entry,
        }
        if candidate_labels is not None:
            if (not isinstance(candidate_labels, (list, tuple)) or
                    any(not isinstance(label, str) or not label
                        for label in candidate_labels)):
                raise ValueError(
                    "candidate_labels must be nonempty strings")
            payload["candidate_labels"] = list(candidate_labels)
        result = self.session.call("scripted_run_resolve_next", **payload)
        return ScriptedRunSession.from_wire(self.session, result["result"])

    def status(self):
        result = self.session.call(
            "scripted_run_status", scripted_run_handle=self.handle)
        return ScriptedRunSession.from_wire(self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "scripted_run_fork", scripted_run_handle=self.handle)
        return ScriptedRunSession.from_wire(self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "scripted_run_save", scripted_run_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_scripted_run_many([self])


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
    """Atomic event-choice mechanics, player actions and sampler state."""
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
        """Apply one legal event player action and retain the sampler cursor."""
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
class ReferenceNatureNeowOfferSession:
    """Atomic Act 1 Neow offer generation and sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
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
                "sts2-v111-reference-nature-neow-offer-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Neow-offer session schema")
        return cls(
            session, payload["referenceNatureNeowOfferHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["eventId"], payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload["pending"],
            payload["offeredChoiceIds"], payload["eventRandomProvider"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("ReferenceNature Neow offers have not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_neow_offer_status",
            reference_nature_neow_offer_handle=self.handle)
        return ReferenceNatureNeowOfferSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one Neow offer draw; publish the run after the shuffle."""
        result = self.session.call(
            "reference_nature_neow_offer_resolve_next",
            reference_nature_neow_offer_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureNeowOfferSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_neow_offer_fork",
            reference_nature_neow_offer_handle=self.handle)
        return ReferenceNatureNeowOfferSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_neow_offer_save",
            reference_nature_neow_offer_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_neow_offer_many([self])


@dataclass(frozen=True)
class ReferenceNatureTrashHeapSession:
    """Atomic Trash Heap fixed-pool reward and sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str | None
    choice_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    selected_choice_id: str | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-trash-heap-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Trash Heap session schema")
        return cls(
            session, payload["referenceNatureTrashHeapHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["eventId"], payload["choiceId"],
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload["pending"],
            payload["selectedChoiceId"], payload["decisionTranscript"],
            payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Trash Heap reward has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_trash_heap_status",
            reference_nature_trash_heap_handle=self.handle)
        return ReferenceNatureTrashHeapSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample and commit the selected Trash Heap reward."""
        result = self.session.call(
            "reference_nature_trash_heap_resolve_next",
            reference_nature_trash_heap_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureTrashHeapSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_trash_heap_fork",
            reference_nature_trash_heap_handle=self.handle)
        return ReferenceNatureTrashHeapSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_trash_heap_save",
            reference_nature_trash_heap_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_trash_heap_many([self])


@dataclass(frozen=True)
class ReferenceNatureTabletOfTruthSession:
    """Atomic one-page Tablet of Truth card upgrade and Nature cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str | None
    choice_id: str
    page: int
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    next_page: int | None
    event_random_provider: str | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-tablet-of-truth-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Tablet of Truth session schema")
        return cls(
            session, payload["referenceNatureTabletOfTruthHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["eventId"], payload["choiceId"], payload["page"],
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload["pending"],
            payload["nextPage"], payload["eventRandomProvider"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Tablet of Truth page has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_tablet_of_truth_status",
            reference_nature_tablet_of_truth_handle=self.handle)
        return ReferenceNatureTabletOfTruthSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample and commit this page's eligible-card upgrade."""
        result = self.session.call(
            "reference_nature_tablet_of_truth_resolve_next",
            reference_nature_tablet_of_truth_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureTabletOfTruthSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_tablet_of_truth_fork",
            reference_nature_tablet_of_truth_handle=self.handle)
        return ReferenceNatureTabletOfTruthSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_tablet_of_truth_save",
            reference_nature_tablet_of_truth_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_tablet_of_truth_many(
            [self])


@dataclass(frozen=True)
class ReferenceNatureTrialSession:
    """Atomic Trial case selection and ReferenceNature cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str | None
    choice_id: str
    page: int
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    next_page: int | None
    event_random_provider: str | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-trial-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Trial session schema")
        return cls(
            session, payload["referenceNatureTrialHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["eventId"], payload["choiceId"], payload["page"],
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload["pending"],
            payload["nextPage"], payload["eventRandomProvider"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Trial case has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_trial_status",
            reference_nature_trial_handle=self.handle)
        return ReferenceNatureTrialSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample and commit the Trial case selection."""
        result = self.session.call(
            "reference_nature_trial_resolve_next",
            reference_nature_trial_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureTrialSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_trial_fork",
            reference_nature_trial_handle=self.handle)
        return ReferenceNatureTrialSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_trial_save",
            reference_nature_trial_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_trial_many([self])


@dataclass(frozen=True)
class ReferenceNatureNativeEventShuffleSession:
    """Atomic Native shuffle and ReferenceNature sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str | None
    site_id: str
    choice_id: str | None
    action_id: str
    page: int | None
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    next_page: int | None
    event_random_provider: str | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-native-event-shuffle-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature native event shuffle session schema")
        return cls(
            session, payload["referenceNatureNativeEventShuffleHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload.get("eventId"), payload["siteId"],
            payload.get("choiceId"),
            payload.get("actionId", payload.get("choiceId")),
            payload.get("page"),
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload["pending"],
            payload.get("nextPage"), payload.get("eventRandomProvider"),
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Native event shuffle has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_native_event_shuffle_status",
            reference_nature_native_event_shuffle_handle=self.handle)
        return ReferenceNatureNativeEventShuffleSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one bounded swap index; commit after the full shuffle."""
        result = self.session.call(
            "reference_nature_native_event_shuffle_resolve_next",
            reference_nature_native_event_shuffle_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureNativeEventShuffleSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_native_event_shuffle_fork",
            reference_nature_native_event_shuffle_handle=self.handle)
        return ReferenceNatureNativeEventShuffleSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_native_event_shuffle_save",
            reference_nature_native_event_shuffle_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_native_event_shuffle_many(
            [self])


@dataclass(frozen=True)
class ReferenceNatureNativeEventGoldSession:
    """Atomic supported Native event entry and ReferenceNature sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str
    action_id: str
    site_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-native-event-gold-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Native event gold session schema")
        return cls(
            session, payload["referenceNatureNativeEventGoldHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["eventId"], payload["actionId"], payload["siteId"],
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload["pending"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Native event gold has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_native_event_gold_status",
            reference_nature_native_event_gold_handle=self.handle)
        return ReferenceNatureNativeEventGoldSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one gold float; commit after the event entry finishes."""
        result = self.session.call(
            "reference_nature_native_event_gold_resolve_next",
            reference_nature_native_event_gold_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureNativeEventGoldSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_native_event_gold_fork",
            reference_nature_native_event_gold_handle=self.handle)
        return ReferenceNatureNativeEventGoldSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_native_event_gold_save",
            reference_nature_native_event_gold_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_native_event_gold_many(
            [self])


@dataclass(frozen=True)
class ReferenceNatureNativeEventRelicSession:
    """Atomic Native event relic reward and ReferenceNature sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str | None
    choice_id: str | None
    action_id: str
    site_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-native-event-relic-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Native event relic session schema")
        return cls(
            session, payload["referenceNatureNativeEventRelicHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["eventId"], payload.get("choiceId"),
            payload["actionId"], payload["siteId"],
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload["pending"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Native event relic has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_native_event_relic_status",
            reference_nature_native_event_relic_handle=self.handle)
        return ReferenceNatureNativeEventRelicSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one relic rarity or identity and commit after all pulls."""
        result = self.session.call(
            "reference_nature_native_event_relic_resolve_next",
            reference_nature_native_event_relic_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureNativeEventRelicSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_native_event_relic_fork",
            reference_nature_native_event_relic_handle=self.handle)
        return ReferenceNatureNativeEventRelicSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_native_event_relic_save",
            reference_nature_native_event_relic_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_native_event_relic_many(
            [self])


@dataclass(frozen=True)
class ReferenceNatureNativeShovelSession:
    """Atomic Native Shovel Reward and ReferenceNature sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    action_id: str
    site_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-native-shovel-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Native Shovel session schema")
        return cls(
            session, payload["referenceNatureNativeShovelHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["actionId"], payload["siteId"], payload["runPhase"],
            payload["boundary"], payload["nextOrdinal"], payload["pending"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Native Shovel has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_native_shovel_status",
            reference_nature_native_shovel_handle=self.handle)
        return ReferenceNatureNativeShovelSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample the rarity or identity, committing the dig after both draws."""
        result = self.session.call(
            "reference_nature_native_shovel_resolve_next",
            reference_nature_native_shovel_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureNativeShovelSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_native_shovel_fork",
            reference_nature_native_shovel_handle=self.handle)
        return ReferenceNatureNativeShovelSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_native_shovel_save",
            reference_nature_native_shovel_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_native_shovel_many(
            [self])


@dataclass(frozen=True)
class ReferenceNatureEndlessConveyorSession:
    """Atomic initial dish roll and ReferenceNature sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str | None
    site_id: str
    action_id: str
    page: int | None
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    selected_dish_id: str | None
    next_page: int | None
    event_random_provider: str | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-endless-conveyor-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Endless Conveyor session schema")
        return cls(
            session, payload["referenceNatureEndlessConveyorHandle"],
            payload["fingerprint"], payload.get("stateHandle"),
            payload.get("eventId"), payload["siteId"],
            payload["actionId"], payload.get("page"),
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload["pending"],
            payload.get("selectedDishId"), payload.get("nextPage"),
            payload.get("eventRandomProvider"),
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Endless Conveyor session has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_endless_conveyor_status",
            reference_nature_endless_conveyor_handle=self.handle)
        return ReferenceNatureEndlessConveyorSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample the initial weighted dish and commit it atomically."""
        result = self.session.call(
            "reference_nature_endless_conveyor_resolve_next",
            reference_nature_endless_conveyor_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureEndlessConveyorSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_endless_conveyor_fork",
            reference_nature_endless_conveyor_handle=self.handle)
        return ReferenceNatureEndlessConveyorSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_endless_conveyor_save",
            reference_nature_endless_conveyor_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_endless_conveyor_many(
            [self])


@dataclass(frozen=True)
class ReferenceNatureGloryReflectionsSession:
    """Atomic Reflections card picks and ReferenceNature sampler cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str | None
    site_id: str | None
    choice_id: str
    page: int | None
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    selected_choice_id: str | None
    next_page: int | None
    event_random_provider: str | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-glory-reflections-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Glory Reflections session schema")
        return cls(
            session, payload["referenceNatureGloryReflectionsHandle"],
            payload["fingerprint"], payload.get("stateHandle"),
            payload.get("eventId"), payload.get("siteId"),
            payload["choiceId"], payload.get("page"), payload["runPhase"],
            payload["boundary"], payload["nextOrdinal"],
            payload.get("pending"), payload.get("selectedChoiceId"),
            payload.get("nextPage"), payload.get("eventRandomProvider"),
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Glory Reflections has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_glory_reflections_status",
            reference_nature_glory_reflections_handle=self.handle)
        return ReferenceNatureGloryReflectionsSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one conditional card identity; commit after touch_mirror."""
        result = self.session.call(
            "reference_nature_glory_reflections_resolve_next",
            reference_nature_glory_reflections_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureGloryReflectionsSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_glory_reflections_fork",
            reference_nature_glory_reflections_handle=self.handle)
        return ReferenceNatureGloryReflectionsSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_glory_reflections_save",
            reference_nature_glory_reflections_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_glory_reflections_many(
            [self])


@dataclass(frozen=True)
class ReferenceNatureInfestedAutomatonSession:
    """One Infested Automaton card offer and ReferenceNature cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    event_id: str | None
    site_id: str | None
    choice_id: str
    page: int | None
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    selected_choice_id: str | None
    selected_card_id: str | None
    next_page: int | None
    event_random_provider: str | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-infested-automaton-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Infested Automaton session schema")
        return cls(
            session, payload["referenceNatureInfestedAutomatonHandle"],
            payload["fingerprint"], payload.get("stateHandle"),
            payload.get("eventId"), payload.get("siteId"),
            payload["choiceId"], payload.get("page"), payload["runPhase"],
            payload["boundary"], payload["nextOrdinal"],
            payload.get("pending"), payload.get("selectedChoiceId"),
            payload.get("selectedCardId"), payload.get("nextPage"),
            payload.get("eventRandomProvider"),
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Infested Automaton offer has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_infested_automaton_status",
            reference_nature_infested_automaton_handle=self.handle)
        return ReferenceNatureInfestedAutomatonSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample a card identity and commit the selected event action."""
        result = self.session.call(
            "reference_nature_infested_automaton_resolve_next",
            reference_nature_infested_automaton_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureInfestedAutomatonSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_infested_automaton_fork",
            reference_nature_infested_automaton_handle=self.handle)
        return ReferenceNatureInfestedAutomatonSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_infested_automaton_save",
            reference_nature_infested_automaton_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_infested_automaton_many(
            [self])


@dataclass(frozen=True)
class ReferenceNaturePunchOffRewardSession:
    """Punch Off's relic and potion choices and ReferenceNature cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    action_id: str
    event_id: str
    site_id: str | None
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    selected_relic: str | None
    selected_potion: str | None
    reward_relic: str | None
    reward_potion: str | None
    player_rewards_cursor_advanced: bool
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-punch-off-reward-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Punch Off reward session schema")
        return cls(
            session, payload["referenceNaturePunchOffRewardHandle"],
            payload["fingerprint"], payload.get("stateHandle"),
            payload["actionId"], payload["eventId"], payload.get("siteId"),
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload.get("pending"),
            payload.get("selectedRelic"), payload.get("selectedPotion"),
            payload.get("rewardRelic"), payload.get("rewardPotion"),
            payload.get("playerRewardsCursorAdvanced", False),
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Punch Off rewards have not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_punch_off_reward_status",
            reference_nature_punch_off_reward_handle=self.handle)
        return ReferenceNaturePunchOffRewardSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample the next reward and replay the action after both picks."""
        result = self.session.call(
            "reference_nature_punch_off_reward_resolve_next",
            reference_nature_punch_off_reward_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNaturePunchOffRewardSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_punch_off_reward_fork",
            reference_nature_punch_off_reward_handle=self.handle)
        return ReferenceNaturePunchOffRewardSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_punch_off_reward_save",
            reference_nature_punch_off_reward_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_punch_off_reward_many(
            [self])


@dataclass(frozen=True)
class ReferenceNaturePunchOffFightSession:
    """Punch Off's two construct HP offsets and Nature cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    action_id: str
    event_id: str
    site_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    selected_hp_offsets: list
    monster_combat_ids: list
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-punch-off-fight-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature Punch Off fight session schema")
        return cls(
            session, payload["referenceNaturePunchOffFightHandle"],
            payload["fingerprint"], payload.get("stateHandle"),
            payload["actionId"], payload["eventId"], payload["siteId"],
            payload["runPhase"], payload["boundary"],
            payload["nextOrdinal"], payload.get("pending"),
            payload["selectedHpOffsets"], payload["monsterCombatIds"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError("Punch Off fight setup has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_punch_off_fight_status",
            reference_nature_punch_off_fight_handle=self.handle)
        return ReferenceNaturePunchOffFightSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample the next construct HP offset and replay after both picks."""
        result = self.session.call(
            "reference_nature_punch_off_fight_resolve_next",
            reference_nature_punch_off_fight_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNaturePunchOffFightSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_punch_off_fight_fork",
            reference_nature_punch_off_fight_handle=self.handle)
        return ReferenceNaturePunchOffFightSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_punch_off_fight_save",
            reference_nature_punch_off_fight_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_punch_off_fight_many(
            [self])


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
class ReferenceNatureTreasureSession:
    """Atomic native Treasure entry, Chance Law replay and Nature cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    action_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    reward: dict | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-treasure-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature treasure session schema")
        return cls(
            session, payload["referenceNatureTreasureHandle"],
            payload["fingerprint"], payload["stateHandle"],
            payload["actionId"], payload["runPhase"],
            payload["boundary"], payload["nextOrdinal"],
            payload["pending"], payload["reward"],
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError(
                "ReferenceNature treasure generation has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_treasure_status",
            reference_nature_treasure_handle=self.handle)
        return ReferenceNatureTreasureSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one pending Chance Law and replay Treasure entry."""
        result = self.session.call(
            "reference_nature_treasure_resolve_next",
            reference_nature_treasure_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureTreasureSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_treasure_fork",
            reference_nature_treasure_handle=self.handle)
        return ReferenceNatureTreasureSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_treasure_save",
            reference_nature_treasure_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_treasure_many([self])


@dataclass(frozen=True)
class ReferenceNatureCombatRewardSession:
    """Atomic Native combat reward generation and ReferenceNature cursor."""
    session: "Session"
    handle: str
    fingerprint: str
    state_handle: str | None
    action_id: str
    run_phase: str
    boundary: str
    next_ordinal: int
    pending: dict | None
    reward: dict | None
    decision_transcript: list
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        if payload.get("schemaId") != (
                "sts2-v111-reference-nature-combat-reward-session-v1"):
            raise EmulatorError(
                "Unsupported ReferenceNature combat reward session schema")
        return cls(
            session, payload["referenceNatureCombatRewardHandle"],
            payload["fingerprint"], payload.get("stateHandle"),
            payload["actionId"], payload["runPhase"],
            payload["boundary"], payload["nextOrdinal"],
            payload.get("pending"), payload.get("reward"),
            payload["decisionTranscript"], payload["natureSampleCount"])

    @property
    def complete(self):
        return self.boundary == "committed"

    @property
    def state(self):
        if self.state_handle is None:
            raise EmulatorError(
                "ReferenceNature combat reward has not committed")
        return State(self.session, self.state_handle)

    def status(self):
        result = self.session.call(
            "reference_nature_combat_reward_status",
            reference_nature_combat_reward_handle=self.handle)
        return ReferenceNatureCombatRewardSession.from_wire(
            self.session, result["result"])

    def resolve_next(self):
        """Sample one reward Chance Law and replay the winning action."""
        result = self.session.call(
            "reference_nature_combat_reward_resolve_next",
            reference_nature_combat_reward_handle=self.handle,
            fingerprint=self.fingerprint)
        return ReferenceNatureCombatRewardSession.from_wire(
            self.session, result["result"])

    def fork(self):
        result = self.session.call(
            "reference_nature_combat_reward_fork",
            reference_nature_combat_reward_handle=self.handle)
        return ReferenceNatureCombatRewardSession.from_wire(
            self.session, result["result"])

    def save_snapshot(self):
        return self.session.call(
            "reference_nature_combat_reward_save",
            reference_nature_combat_reward_handle=self.handle)["snapshot"]

    def release(self):
        return self.session.release_reference_nature_combat_reward_many([self])


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
        if self.session._fair_observation_supported:
            return self.session.observe_fair(
                "controlled_combat", self.handle)["observation"]
        result = self.session.call("controlled_observe",
                                   controlled_handle=self.handle,
                                   policy_id="prototype-fair-v0")
        return json.loads(result["payloadJson"])

    def actions(self):
        if self.session._fair_observation_supported:
            return self.session.observe_fair(
                "controlled_combat", self.handle)["actions"]
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
    native_policy_baseline: bool

    @classmethod
    def from_wire(cls, session, payload):
        data = payload["result"]
        return cls(
            session, data["semanticHandle"], data["fingerprint"],
            data["boundary"], data["pending"], data["terminal"],
            data["outcome"], data["exitHp"],
            data.get("nativePolicyProvider") is not None)

    def status(self):
        return self.session.call(
            "semantic_status", semantic_handle=self.handle)["result"]

    def observe(self):
        """Fair player-only observation: never a privileged Nature event."""
        view_kind = ("semantic_native_policy" if self.native_policy_baseline
                     else "semantic_combat")
        if self.session.supports_fair_observation_view(view_kind):
            return self.session.observe_fair(
                view_kind, self.handle)["observation"]
        result = self.session.call(
            "semantic_observe", semantic_handle=self.handle,
            policy_id="prototype-fair-v0")
        return json.loads(result["payloadJson"])

    def actions(self):
        view_kind = ("semantic_native_policy" if self.native_policy_baseline
                     else "semantic_combat")
        if self.session.supports_fair_observation_view(view_kind):
            return self.session.observe_fair(
                view_kind, self.handle)["actions"]
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


@dataclass(frozen=True)
class SemanticReferenceCombat:
    """Semantic combat replayed with the isolated ReferenceNature sampler."""
    session: "Session"
    handle: str
    fingerprint: str
    boundary: str
    pending: dict | None
    terminal: bool
    outcome: str | None
    nature_sample_count: int

    @classmethod
    def from_wire(cls, session, payload):
        data = payload.get("result", payload)
        if data.get("schemaId") != session.capabilities.get(
                "referenceNatureSessionSchemaId"):
            raise EmulatorError(
                "Unsupported semantic ReferenceNature session schema")
        return cls(
            session, data["referenceNatureHandle"], data["fingerprint"],
            data["boundary"], data.get("pending"), data["terminal"],
            data.get("outcome"), data["natureSampleCount"])

    def status(self):
        result = self.session.call(
            "semantic_reference_status",
            reference_nature_handle=self.handle)
        return SemanticReferenceCombat.from_wire(self.session, result)

    def observe(self):
        """Return the fair player view, excluding the sampler cursor."""
        if self.session.supports_fair_observation_view(
                "semantic_reference"):
            return self.session.observe_fair(
                "semantic_reference", self.handle)["observation"]
        result = self.session.call(
            "semantic_reference_observe",
            reference_nature_handle=self.handle,
            policy_id="prototype-fair-v0")
        return json.loads(result["payloadJson"])

    def actions(self):
        if self.session.supports_fair_observation_view(
                "semantic_reference"):
            return self.session.observe_fair(
                "semantic_reference", self.handle)["actions"]
        result = self.session.call(
            "semantic_reference_observe",
            reference_nature_handle=self.handle,
            policy_id="prototype-fair-v0")
        return result["actions"]

    def observe_nature_choice(self):
        """Return the separate Nature-only view at a pending chance boundary."""
        return self.session.observe_nature_choice(
            "reference_combat", self.handle)

    def step(self, action_id: str):
        if self.boundary not in ("player", "player_choice"):
            raise EmulatorError("Expected a semantic ReferenceNature player decision")
        return SemanticReferenceCombat.from_wire(
            self.session, self.session.call(
                "semantic_reference_step",
                reference_nature_handle=self.handle,
                fingerprint=self.fingerprint, action_id=action_id))

    def resolve_next(self):
        if self.boundary != "nature":
            raise EmulatorError(
                "Expected a pending semantic ReferenceNature chance")
        return SemanticReferenceCombat.from_wire(
            self.session, self.session.call(
                "semantic_reference_resolve_next",
                reference_nature_handle=self.handle,
                fingerprint=self.fingerprint))

    def fork(self):
        return SemanticReferenceCombat.from_wire(
            self.session, self.session.call(
                "semantic_reference_fork",
                reference_nature_handle=self.handle))

    def save_snapshot(self):
        result = self.session.call(
            "semantic_reference_save",
            reference_nature_handle=self.handle)
        if result.get("schemaId") != self.session.capabilities.get(
                "referenceNatureSessionSchemaId"):
            raise EmulatorError(
                "Semantic ReferenceNature snapshot schema differs from negotiation")
        return result["snapshot"]

    def release(self):
        return self.session.release_semantic_reference_many([self])


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
            self.nature_relic_bag_snapshot_schema_id = (
                self.capabilities.get("natureRelicBagSnapshotSchemaId"))
            if self.capabilities.get("wireSchemaId") != "prototype-ai-jsonl-v0":
                raise EmulatorError("Unsupported emulator wire schema")
            snapshot_schema_id = self.capabilities.get("snapshotSchemaId")
            if not isinstance(snapshot_schema_id, str) or not snapshot_schema_id:
                raise EmulatorError("Emulator did not advertise an exact snapshot schema")
            supported_snapshot_schema_ids = self.capabilities.get(
                "snapshotSchemaIds")
            if supported_snapshot_schema_ids is None:
                supported_snapshot_schema_ids = [snapshot_schema_id]
                for legacy_schema_id in (
                        self.reference_nature_state_snapshot_schema_id,
                        self.nature_relic_bag_snapshot_schema_id):
                    if (isinstance(legacy_schema_id, str)
                            and legacy_schema_id
                            and legacy_schema_id not in
                            supported_snapshot_schema_ids):
                        supported_snapshot_schema_ids.append(legacy_schema_id)
            if (not isinstance(supported_snapshot_schema_ids, list)
                    or any(not isinstance(schema_id, str) or not schema_id
                           for schema_id in supported_snapshot_schema_ids)
                    or len(supported_snapshot_schema_ids) !=
                    len(set(supported_snapshot_schema_ids))
                    or snapshot_schema_id not in
                    supported_snapshot_schema_ids):
                raise EmulatorError("Invalid exact snapshot schema capabilities")
            simulation_snapshot_schema_id = self.capabilities.get(
                "simulationSnapshotSchemaId")
            if (simulation_snapshot_schema_id is not None
                    and simulation_snapshot_schema_id != snapshot_schema_id):
                raise EmulatorError(
                    "Simulation snapshot schema differs from negotiation")
            self.snapshot_schema_id = snapshot_schema_id
            self.supported_snapshot_schema_ids = frozenset(
                supported_snapshot_schema_ids)
            if self.capabilities.get("batchSchemaId") != "sts2-deterministic-batch-v1":
                raise EmulatorError("Emulator does not support deterministic batch v1")
            self._validate_stochastic_capabilities()
            self._semantic_deck_prefix_distribution_supported = (
                self._validate_semantic_deck_prefix_distribution_capabilities())
            if not self._semantic_deck_prefix_distribution_supported:
                self._semantic_deck_sequential_audit_supported = False
            self._fair_observation_supported = (
                self._validate_fair_observation_capabilities())
        except BaseException:
            self._process.terminate()
            self._process.wait(timeout=5)
            self._close_pipes()
            raise

    def _validate_fair_observation_capabilities(self):
        fields = ("fairObservationOperations",
                  "fairObservationViewKinds")
        schema_id = self.capabilities.get(
            "fairObservationEnvelopeSchemaId")
        if schema_id is None:
            if any(field in self.capabilities for field in fields):
                raise EmulatorError(
                    "Emulator returned partial fair-observation capabilities")
            return False
        if schema_id != "sts2-v111-fair-observation-envelope-v1":
            raise EmulatorError("Unsupported fair-observation envelope schema")
        operations = self.capabilities.get(fields[0])
        view_kinds = self.capabilities.get(fields[1])
        required_view_kinds = [
            "run_state", "controlled_combat", "semantic_combat"]
        if (not isinstance(operations, list)
                or any(not isinstance(item, str) or not item
                       for item in operations)
                or len(operations) != len(set(operations))
                or "fair_observe" not in operations
                or not isinstance(view_kinds, list)
                or any(not isinstance(item, str) or not item
                       for item in view_kinds)
                or len(view_kinds) != len(set(view_kinds))
                or any(item not in view_kinds
                       for item in required_view_kinds)):
            raise EmulatorError("Invalid fair-observation capabilities")
        if self.capabilities.get("aiSchemaId") != "prototype-ai-v0":
            raise EmulatorError(
                "Fair-observation capability has an unsupported player schema")
        return True

    def observe_fair(self, view_kind: str, handle: str) -> dict:
        """Return one validated public observation/action frame across modes."""
        if not self._fair_observation_supported:
            raise EmulatorError(
                "Emulator does not expose the shared fair-observation frame")
        if not self.supports_fair_observation_view(view_kind):
            raise EmulatorError(
                f"Emulator does not advertise fair view kind {view_kind!r}")
        if not isinstance(handle, str) or not handle:
            raise ValueError("handle must be a nonempty string")

        result = self.call("fair_observe", view_kind=view_kind,
                           view_handle=handle)
        frame = result.get("frame")
        if not isinstance(frame, dict):
            raise EmulatorError("Fair-observation response has no frame")
        expected_schema = self.capabilities[
            "fairObservationEnvelopeSchemaId"]
        if (frame.get("schemaId") != expected_schema
                or frame.get("viewKind") != view_kind
                or frame.get("handle") != handle
                or frame.get("policyId") != "prototype-fair-v0"
                or frame.get("observationSchemaId") !=
                self.capabilities.get("aiSchemaId")):
            raise EmulatorError(
                "Fair-observation envelope differs from negotiation")

        payload_json = frame.get("payloadJson")
        observation_hash = frame.get("observationHash")
        if (not isinstance(payload_json, str)
                or not isinstance(observation_hash, str)
                or not observation_hash):
            raise EmulatorError("Fair-observation payload is malformed")
        try:
            observation = json.loads(payload_json)
        except (TypeError, json.JSONDecodeError) as error:
            raise EmulatorError(
                "Fair-observation payload is not valid JSON") from error
        actual_hash = hashlib.sha256(
            payload_json.encode("utf-8")).hexdigest()
        actions = frame.get("actions")
        if (not isinstance(observation, dict)
                or actual_hash != observation_hash
                or not isinstance(actions, list)
                or any(not isinstance(action, dict)
                       or not isinstance(action.get("actionId"), str)
                       or not action["actionId"]
                       or not isinstance(action.get("kind"), str)
                       or not action["kind"]
                       or not isinstance(action.get("payloadJson"), str)
                       for action in actions)
                or len({action["actionId"] for action in actions}) !=
                len(actions)
                or type(frame.get("terminal")) is not bool):
            raise EmulatorError(
                "Fair-observation payload, hash, or action menu is invalid")
        return {**frame, "observation": observation}

    def supports_fair_observation_view(self, view_kind: str) -> bool:
        """Check whether this server negotiated one advertised fair view kind."""
        return (self._fair_observation_supported
                and isinstance(view_kind, str)
                and view_kind in self.capabilities.get(
                    "fairObservationViewKinds", []))

    def _validate_stochastic_capabilities(self):
        """Reject internally inconsistent stochastic hello advertisements."""
        manifest = self.capabilities.get("stochasticCapabilities")
        stochastic_fields = (
            "stochasticSessionCoverage",
            "stochasticChanceLawQueryCoverage",
            "semanticNatureChoiceViewSchemaId",
            "semanticNatureChoiceViewOperations",
            "semanticNatureChoiceViewSessionKinds",
        )
        if manifest is None:
            if any(field in self.capabilities for field in stochastic_fields):
                raise EmulatorError(
                    "Emulator returned partial stochastic capabilities")
            return
        if not isinstance(manifest, dict):
            raise EmulatorError("Invalid stochastic capability manifest")
        capability_schema = manifest.get("schemaId")
        if (capability_schema not in (
                "sts2-v111-stochastic-capabilities-v3",
                "sts2-v111-stochastic-capabilities-v4")
                or manifest.get("chanceLawSchemaId") !=
                "sts2-v111-chance-law-v1"
                or manifest.get("playerObservationSchemaId") !=
                self.capabilities.get("aiSchemaId")):
            raise EmulatorError(
                "Unsupported stochastic capability or observation schema")

        def string_list(value, field):
            if (not isinstance(value, list)
                    or any(not isinstance(item, str) or not item
                           for item in value)
                    or len(value) != len(set(value))):
                raise EmulatorError(
                    f"Invalid stochastic capability list {field!r}")
            return value

        chance_law_schema_ids = (
            string_list(manifest.get("chanceLawSchemaIds"),
                        "chanceLawSchemaIds")
            if capability_schema ==
            "sts2-v111-stochastic-capabilities-v4"
            else [manifest["chanceLawSchemaId"]])
        if (manifest["chanceLawSchemaId"] not in chance_law_schema_ids
                or not set(chance_law_schema_ids).issubset({
                    "sts2-v111-chance-law-v1",
                    "sts2-v111-chance-law-v2-uniform-index-range"})):
            raise EmulatorError(
                "Unsupported negotiated Chance Law schema list")

        nature_sites = set(string_list(
            manifest.get("natureChoiceSiteIds"), "natureChoiceSiteIds"))
        for field in ("providerModes", "probabilityStatuses",
                      "scriptedRunActionOperations",
                      "scriptedRunSessionOperations"):
            string_list(manifest.get(field), field)
        reference_nature_operations = set(string_list(
            manifest.get("referenceNatureOperations"),
            "referenceNatureOperations"))
        reference_nature_schema = manifest.get(
            "referenceNatureSessionSchemaId")
        if (reference_nature_schema != self.capabilities.get(
                "referenceNatureSessionSchemaId")):
            raise EmulatorError(
                "ReferenceNature session schema differs from the stochastic manifest")
        if reference_nature_schema is not None:
            required_semantic_reference_operations = {
                "semantic_reference_begin",
                "semantic_reference_observe",
                "semantic_reference_step",
                "semantic_reference_resolve_next",
                "semantic_reference_status",
                "semantic_reference_fork",
                "semantic_reference_save",
                "semantic_reference_load",
                "semantic_reference_release_many",
            }
            if not required_semantic_reference_operations.issubset(
                    reference_nature_operations):
                raise EmulatorError(
                    "ReferenceNature session is missing required semantic operations")

        view_schema = manifest.get("semanticNatureChoiceViewSchemaId")
        view_operations = string_list(
            manifest.get("semanticNatureChoiceViewOperations"),
            "semanticNatureChoiceViewOperations")
        view_session_kinds = string_list(
            manifest.get("semanticNatureChoiceViewSessionKinds"),
            "semanticNatureChoiceViewSessionKinds")
        if (view_schema !=
                "sts2-v111-semantic-nature-choice-view-v2"
                or self.capabilities.get(
                    "semanticNatureChoiceViewSchemaId") != view_schema):
            raise EmulatorError(
                "Nature-choice schema differs from the stochastic manifest")
        for field, advertised in (
                ("semanticNatureChoiceViewOperations", view_operations),
                ("semanticNatureChoiceViewSessionKinds",
                 view_session_kinds)):
            top_level = string_list(self.capabilities.get(field), field)
            if any(item not in advertised for item in top_level):
                raise EmulatorError(
                    f"Nature-choice {field} differs from the stochastic manifest")
            if (field == "semanticNatureChoiceViewSessionKinds"
                    and top_level != advertised):
                raise EmulatorError(
                    "Nature-choice session kinds differ from the stochastic manifest")
        if ("nature_choice_observe" not in view_operations
                or "nature_choice_observe" not in self.capabilities.get(
                    "semanticNatureChoiceViewOperations", [])):
            raise EmulatorError(
                "Nature-choice observation is missing from its manifest")

        coverage = self.capabilities.get("stochasticSessionCoverage")
        if (not isinstance(coverage, dict)
                or coverage.get("schemaId") !=
                "sts2-v111-stochastic-session-coverage-v1"):
            raise EmulatorError("Invalid stochastic session coverage")
        for field, schema in coverage.items():
            if not field.endswith("SessionSchemaId"):
                continue
            manifest_field = ("referenceNatureSessionSchemaId"
                              if field ==
                              "semanticReferenceNatureSessionSchemaId"
                              else field)
            if (not isinstance(schema, str) or not schema
                    or manifest.get(manifest_field) != schema):
                raise EmulatorError(
                    f"Stochastic session schema {field!r} is inconsistent")
        for field, site_ids in coverage.items():
            if not field.endswith("SiteIds"):
                continue
            advertised_sites = string_list(site_ids, field)
            if any(site_id not in nature_sites
                   for site_id in advertised_sites):
                raise EmulatorError(
                    f"Stochastic session coverage {field!r} contains an "
                    "unadvertised Nature-choice site")

        query_coverage = self.capabilities.get(
            "stochasticChanceLawQueryCoverage")
        if (not isinstance(query_coverage, dict)
                or query_coverage.get("schemaId") !=
                "sts2-v111-stochastic-chance-law-query-coverage-v1"
                or query_coverage.get("queryOperation") !=
                "chance_law_query"):
            raise EmulatorError("Invalid Chance Law query coverage")
        string_list(query_coverage.get("siteIds"), "chanceLawQuerySiteIds")

    def supports_reference_nature_site(self, site_id: str, *,
                                       session: str = "any") -> bool:
        """Check site coverage for a ReferenceNature continuation schema."""
        fields = {
            "semantic": "semanticReferenceNatureSiteIds",
            "run_action": "referenceNatureRunActionSiteIds",
            "event_choice": "referenceNatureEventChoiceSiteIds",
            "ancient_offer": "referenceNatureAncientOfferSiteIds",
            "neow_offer": "referenceNatureNeowOfferSiteIds",
            "trash_heap": "referenceNatureTrashHeapSiteIds",
            "tablet_of_truth": "referenceNatureTabletOfTruthSiteIds",
            "trial": "referenceNatureTrialSiteIds",
            "event_shuffle": "referenceNatureNativeEventShuffleSiteIds",
            "native_shuffle": "referenceNatureNativeEventShuffleSiteIds",
            "native_event_gold": "referenceNatureNativeEventGoldSiteIds",
            "native_event_relic": "referenceNatureNativeEventRelicSiteIds",
            "native_shovel": "referenceNatureNativeShovelSiteIds",
            "endless_conveyor": "referenceNatureEndlessConveyorSiteIds",
            "glory_reflections": "referenceNatureGloryReflectionsSiteIds",
            "infested_automaton": "referenceNatureInfestedAutomatonSiteIds",
            "punch_off_reward": "referenceNaturePunchOffRewardSiteIds",
            "punch_off_fight": "referenceNaturePunchOffFightSiteIds",
            "merchant": "referenceNatureMerchantSiteIds",
            "treasure": "referenceNatureTreasureSiteIds",
            "combat_reward": "referenceNatureCombatRewardSiteIds",
        }
        if session != "any" and session not in fields:
            raise ValueError(
                "session must be semantic, run_action, event_choice, ancient_offer, neow_offer, trash_heap, tablet_of_truth, trial, event_shuffle, native_shuffle, native_event_gold, native_event_relic, native_shovel, endless_conveyor, glory_reflections, infested_automaton, punch_off_reward, punch_off_fight, merchant, treasure, combat_reward, or any")
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

    def _validate_semantic_deck_prefix_distribution_capabilities(self):
        self._semantic_deck_sequential_audit_supported = False
        self._semantic_deck_sequential_audit_max_prefix_length = 0
        audit_maximum_field = (
            "semanticDeckPrefixDistributionSequentialAuditMaximumPrefixLength")
        fields = (
            "semanticDeckPrefixDistributionSchemaId",
            "semanticDeckPrefixDistributionOperations",
            "semanticDeckPrefixDistributionInputs",
            "semanticDeckPrefixDistributionScope",
        )
        present = [field in self.capabilities for field in fields]
        if not any(present):
            if audit_maximum_field in self.capabilities:
                raise EmulatorError(
                    "Sequential deck audit maximum was advertised without "
                    "deck distribution capabilities")
            return False
        if not all(present):
            raise EmulatorError(
                "Emulator returned partial semantic deck distribution capabilities")
        if self.capabilities[fields[0]] != (
                "sts2-v111-semantic-deck-prefix-distribution-v1"):
            raise EmulatorError(
                "Unsupported semantic deck prefix distribution schema")

        def string_list(value, field):
            if (not isinstance(value, list)
                    or any(not isinstance(item, str) or not item
                           for item in value)
                    or len(value) != len(set(value))):
                raise EmulatorError(
                    f"Invalid semantic deck distribution capability {field!r}")
            return value

        operations = string_list(self.capabilities[fields[1]], fields[1])
        inputs = string_list(self.capabilities[fields[2]], fields[2])
        required_inputs = {
            "state_handle (run with an idle Nature-owned combat deck)",
            "prefix_length (integer from 1 through the deck size)",
            "maximum_orders (optional integer from 1 through 65536; default 4096)",
        }
        self._semantic_deck_sequential_audit_supported = (
            "audit_sequential (optional boolean; composes successive "
            "QueryDraw supports)"
            in inputs)
        self._semantic_deck_sequential_audit_max_prefix_length = 0
        if audit_maximum_field in self.capabilities:
            audit_maximum = self.capabilities[audit_maximum_field]
            if type(audit_maximum) is not int or audit_maximum < 1:
                raise EmulatorError(
                    "Invalid sequential deck audit maximum prefix length")
            if not self._semantic_deck_sequential_audit_supported:
                raise EmulatorError(
                    "Sequential deck audit maximum was advertised without its input")
            self._semantic_deck_sequential_audit_max_prefix_length = (
                audit_maximum)
        else:
            self._semantic_deck_sequential_audit_supported = False
        scope = self.capabilities[fields[3]]
        if ("semantic_deck_prefix_distribution" not in operations
                or not required_inputs.issubset(inputs)
                or not isinstance(scope, str)
                or "Nature-only" not in scope
                or "UNVERIFIED" not in scope
                or "excluded from fair player observations" not in scope):
            raise EmulatorError(
                "Invalid semantic deck prefix distribution contract")
        return True

    def query_semantic_deck_prefix_distribution(
            self, state_handle: str, prefix_length: int, *,
            maximum_orders: int = 4096,
            audit_sequential: bool = False):
        """Enumerate a hidden Nature deck's exact ordered-prefix masses.

        This returns exact combinatorial counts for the declared uniform-pool
        model. The model is UNVERIFIED and the result is not a player view.
        """
        if not self._semantic_deck_prefix_distribution_supported:
            raise EmulatorError(
                "Emulator does not expose exact semantic deck prefix queries")
        if not isinstance(state_handle, str) or not state_handle:
            raise ValueError("state_handle must be a nonempty string")
        if type(prefix_length) is not int or prefix_length < 1:
            raise ValueError("prefix_length must be a positive integer")
        if (type(maximum_orders) is not int or
                maximum_orders < 1 or maximum_orders > 65536):
            raise ValueError(
                "maximum_orders must be an integer from 1 through 65536")
        if type(audit_sequential) is not bool:
            raise ValueError("audit_sequential must be a boolean")
        if (audit_sequential and
                not self._semantic_deck_sequential_audit_supported):
            raise EmulatorError(
                "Emulator does not advertise the sequential deck-law audit")
        if (audit_sequential and prefix_length >
                self._semantic_deck_sequential_audit_max_prefix_length):
            raise ValueError(
                "Sequential deck-law audit prefix length exceeds the "
                "negotiated maximum of " + str(
                    self._semantic_deck_sequential_audit_max_prefix_length))

        payload = {
            "state_handle": state_handle,
            "prefix_length": prefix_length,
            "maximum_orders": maximum_orders,
        }
        if audit_sequential:
            payload["audit_sequential"] = True
        result = self.call("semantic_deck_prefix_distribution", **payload)
        distribution = result.get("distribution")
        self._validate_semantic_deck_prefix_distribution(
            distribution, prefix_length=prefix_length)
        if audit_sequential and result.get(
                "sequentialChanceLawAudit") != "matched":
            raise EmulatorError(
                "Sequential deck-law audit did not report an exact match")
        if (not audit_sequential and result.get(
                "sequentialChanceLawAudit") is not None):
            raise EmulatorError(
                "Unexpected sequential deck-law audit result")
        return distribution

    @staticmethod
    def _validate_semantic_deck_prefix_distribution(
            distribution, *, prefix_length):
        if not isinstance(distribution, dict):
            raise EmulatorError(
                "Semantic deck prefix distribution is not an object")
        required_text = (
            "schemaId", "stateFingerprint", "conditioningPolicyId",
            "probabilityStatus", "probabilityProvenance",
            "outcomeFingerprint",
        )
        if any(not isinstance(distribution.get(field), str)
               or not distribution[field] for field in required_text):
            raise EmulatorError(
                "Semantic deck prefix distribution identity is malformed")
        if (distribution["schemaId"] !=
                "sts2-v111-semantic-deck-prefix-distribution-v1"
                or distribution["probabilityStatus"] != "UNVERIFIED"
                or type(distribution.get("prefixLength")) is not int
                or distribution["prefixLength"] != prefix_length):
            raise EmulatorError(
                "Semantic deck prefix distribution differs from negotiation")

        outcomes = distribution.get("outcomes")
        total_weight = distribution.get("totalWeight")
        if (not isinstance(outcomes, list) or not outcomes
                or type(total_weight) is not int or total_weight <= 0):
            raise EmulatorError(
                "Semantic deck prefix support is malformed")
        keys = set()
        support = []
        weight_sum = 0
        for outcome in outcomes:
            if not isinstance(outcome, dict):
                raise EmulatorError(
                    "Semantic deck prefix outcome is not an object")
            card_ids = outcome.get("cardInstanceIds")
            weight = outcome.get("weight")
            if (not isinstance(card_ids, list)
                    or len(card_ids) != prefix_length
                    or any(type(card_id) is not int for card_id in card_ids)
                    or len(card_ids) != len(set(card_ids))
                    or type(weight) is not int or weight <= 0):
                raise EmulatorError(
                    "Semantic deck prefix outcome is malformed")
            key = tuple(card_ids)
            if key in keys:
                raise EmulatorError(
                    "Semantic deck prefix support contains duplicates")
            keys.add(key)
            weight_sum += weight
            support.append({"card_instance_ids": card_ids,
                            "weight": weight})
        if (weight_sum != total_weight
                or _canonical_protocol_hash(support) !=
                distribution["outcomeFingerprint"]):
            raise EmulatorError(
                "Semantic deck prefix weights or fingerprint are invalid")

    def _validate_chance_law(self, law, *, expected_site_id=None,
                             expected_ordinal=None):
        if not isinstance(law, dict):
            raise EmulatorError("Chance Law response is not an object")

        def required_string(value, field):
            if not isinstance(value, str) or not value:
                raise EmulatorError(
                    f"Chance Law response has invalid {field!r}")
            return value

        manifest = self.capabilities["stochasticCapabilities"]
        supported_statuses = set(manifest["probabilityStatuses"])
        capability_schema_ids = manifest.get(
            "chanceLawSchemaIds", [manifest.get("chanceLawSchemaId")])
        law_schema = law.get("schemaId")
        if law_schema not in capability_schema_ids:
            raise EmulatorError("Chance Law schema differs from negotiation")
        for field in ("lawId", "eventId", "siteId", "operationKind",
                      "ownerId", "supportFingerprint",
                      "conditioningPolicyId", "conditioningFingerprint",
                      "probabilityStatus", "probabilityProvenance"):
            required_string(law.get(field), field)
        ordinal = law.get("ordinal")
        if type(ordinal) is not int or ordinal < 0:
            raise EmulatorError("Chance Law ordinal is invalid")
        if (expected_site_id is not None
                and law.get("siteId") != expected_site_id):
            raise EmulatorError(
                "Chance Law site differs from the requested site")
        if expected_ordinal is not None:
            law_ordinal = law.get("ordinal")
            if type(law_ordinal) is not int or law_ordinal != expected_ordinal:
                raise EmulatorError(
                    "Chance Law ordinal differs from the requested ordinal")
        if law.get("probabilityStatus") not in supported_statuses:
            raise EmulatorError("Chance Law probability status was not negotiated")

        outcomes = law.get("outcomes")
        total_weight = law.get("totalWeight")
        if (not isinstance(outcomes, list)
                or type(total_weight) is not int or total_weight <= 0):
            raise EmulatorError("Chance Law support is invalid")
        compact_range = law.get("uniformIndexRange")
        if law_schema == "sts2-v111-chance-law-v2-uniform-index-range":
            if outcomes or not isinstance(compact_range, dict):
                raise EmulatorError("Compact Chance Law support is invalid")
            start = compact_range.get("startInclusive")
            count = compact_range.get("count")
            prefix = compact_range.get("outcomeIdPrefix")
            if (type(start) is not int or start != 0
                    or type(count) is not int or count <= 65536
                    or prefix != "choice_index:" or total_weight != count
                    or law.get("probabilityStatus") != "UNVERIFIED"):
                raise EmulatorError("Compact Chance Law range is invalid")
            support = {
                "support_kind": "uniform_index_range_v1",
                "range": {
                    "start_inclusive": start,
                    "count": count,
                    "outcome_id_prefix": prefix,
                },
            }
        elif (law_schema != "sts2-v111-chance-law-v1"
              or compact_range is not None or not outcomes):
            raise EmulatorError("Explicit Chance Law support is invalid")
        else:
            outcome_ids = set()
            weight_sum = 0
            for outcome in outcomes:
                if not isinstance(outcome, dict):
                    raise EmulatorError("Chance Law outcome is not an object")
                outcome_id = required_string(outcome.get("outcomeId"),
                                             "outcomeId")
                weight = outcome.get("weight")
                if (type(weight) is not int or weight <= 0
                        or outcome_id in outcome_ids):
                    raise EmulatorError("Chance Law outcome support is invalid")
                outcome_ids.add(outcome_id)
                weight_sum += weight
            if weight_sum != total_weight:
                raise EmulatorError("Chance Law total weight is inconsistent")
            support = [{"outcome_id": outcome["outcomeId"],
                        "weight": outcome["weight"]}
                       for outcome in outcomes]
        if _canonical_protocol_hash(support) != law["supportFingerprint"]:
            raise EmulatorError("Chance Law support fingerprint is invalid")
        identity = {
            "schema_id": law["schemaId"],
            "site_id": law["siteId"],
            "operation_kind": law["operationKind"],
            "owner_id": law["ownerId"],
            "ordinal": ordinal,
            "support_fingerprint": law["supportFingerprint"],
            "conditioning_policy_id": law["conditioningPolicyId"],
            "conditioning_fingerprint": law["conditioningFingerprint"],
            "probability_status": law["probabilityStatus"],
            "probability_provenance": law["probabilityProvenance"],
        }
        if _canonical_protocol_hash(identity) != law["lawId"]:
            raise EmulatorError("Chance Law identifier is invalid")

    def _validate_nature_choice_view(self, choice):
        if not isinstance(choice, dict):
            raise EmulatorError("Nature-choice response is not an object")
        manifest = self.capabilities["stochasticCapabilities"]
        if choice.get("schemaId") != manifest.get(
                "semanticNatureChoiceViewSchemaId"):
            raise EmulatorError("Nature-choice envelope schema is invalid")
        law = choice.get("chanceLaw")
        self._validate_chance_law(law)
        for field in ("eventKind", "eventId", "siteId", "operationKind",
                      "eventSupportFingerprint", "lawSupportFingerprint",
                      "conditioningPolicyId", "conditioningFingerprint",
                      "probabilityStatus", "probabilityProvenance"):
            if not isinstance(choice.get(field), str) or not choice[field]:
                raise EmulatorError(
                    f"Nature-choice response has invalid {field!r}")
        if (choice.get("eventId") != law.get("eventId")
                or choice.get("siteId") != law.get("siteId")
                or choice.get("operationKind") != law.get("operationKind")
                or choice.get("lawSupportFingerprint") !=
                law.get("supportFingerprint")
                or choice.get("conditioningPolicyId") !=
                law.get("conditioningPolicyId")
                or choice.get("conditioningFingerprint") !=
                law.get("conditioningFingerprint")
                or choice.get("probabilityStatus") !=
                law.get("probabilityStatus")
                or choice.get("probabilityProvenance") !=
                law.get("probabilityProvenance")):
            raise EmulatorError(
                "Nature-choice envelope and Chance Law disagree")

        metadata = choice.get("eventMetadata")
        if not isinstance(metadata, dict) or "chanceLaw" in metadata:
            raise EmulatorError("Nature-choice event metadata is invalid")
        for field, envelope_field in (
                ("kind", "eventKind"), ("eventId", "eventId"),
                ("siteId", "siteId"), ("operationKind", "operationKind"),
                ("supportFingerprint", "eventSupportFingerprint"),
                ("probabilityStatus", "probabilityStatus"),
                ("boundaryFingerprint", "boundaryFingerprint")):
            if (field in metadata
                    and metadata[field] != choice.get(envelope_field)):
                raise EmulatorError(
                    "Nature-choice metadata differs from its envelope")

    def observe_nature_choice(self, session_kind: str,
                              session_handle: str) -> dict:
        """Return the negotiated Nature-only view of one pending chance.

        The generic endpoint accepts session handles from semantic combat,
        ReferenceNature boundaries, and controlled run/event continuations.
        It is separate from the fair player observation.
        """
        schema_id = self.capabilities.get(
            "semanticNatureChoiceViewSchemaId")
        operations = self.capabilities.get(
            "semanticNatureChoiceViewOperations", [])
        session_kinds = self.capabilities.get(
            "semanticNatureChoiceViewSessionKinds", [])
        if (schema_id != "sts2-v111-semantic-nature-choice-view-v2"
                or not isinstance(operations, list)
                or "nature_choice_observe" not in operations):
            raise EmulatorError(
                "Emulator does not expose the versioned Nature-choice view")
        if not isinstance(session_kind, str) or not session_kind:
            raise ValueError("session_kind must be a nonempty string")
        if (not isinstance(session_kinds, list)
                or session_kind not in session_kinds):
            raise EmulatorError(
                f"Emulator does not advertise Nature-choice session kind "
                f"{session_kind!r}")
        if not isinstance(session_handle, str) or not session_handle:
            raise ValueError("session_handle must be a nonempty string")

        result = self.call(
            "nature_choice_observe", session_kind=session_kind,
            session_handle=session_handle)
        if (result.get("schemaId") != schema_id
                or result.get("sessionKind") != session_kind
                or result.get("sessionHandle") != session_handle
                or (result.get("natureChoice") is not None
                    and not isinstance(result.get("natureChoice"), dict))):
            raise EmulatorError(
                "Nature-choice response differs from the negotiated session")
        if result.get("natureChoice") is not None:
            self._validate_nature_choice_view(result["natureChoice"])
        return result

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
              ascension=0, fidelity_id=None, nature_seed=None,
              unlocked_colorless_epochs=None):
        if mode not in {"v111", "prototype"}:
            raise ValueError("mode must be v111 or prototype")
        if nature_seed is not None and (
                isinstance(nature_seed, bool) or not isinstance(nature_seed, int)
                or nature_seed < 0 or nature_seed > 0xffffffffffffffff):
            raise ValueError("nature_seed must be an unsigned 64-bit integer")
        if unlocked_colorless_epochs is not None and (
                not isinstance(unlocked_colorless_epochs, list) or
                any(not isinstance(epoch, str) or not epoch.strip()
                    for epoch in unlocked_colorless_epochs)):
            raise ValueError(
                "unlocked_colorless_epochs must be a list of nonempty epoch IDs")
        if mode == "prototype" and (character != "silent" or first_act != "Overgrowth"
                                    or fidelity_id is not None or nature_seed is not None
                                    or unlocked_colorless_epochs is not None):
            raise ValueError("Legacy prototype reset supports Silent/Overgrowth without v111 overrides")
        payload = {"seed": seed, "ascension": ascension}
        if mode == "v111":
            payload.update(character=character, first_act=first_act)
            if fidelity_id is not None:
                payload["fidelity_id"] = fidelity_id
            if nature_seed is not None:
                payload["nature_seed"] = nature_seed
            if unlocked_colorless_epochs is not None:
                payload["unlocked_colorless_epochs"] = (
                    unlocked_colorless_epochs.copy())
        result = self.call("reset_v111" if mode == "v111" else "reset", **payload)
        return State(self, result["stateHandle"])

    def _validate_run_snapshot_schema(self, snapshot):
        if not isinstance(snapshot, str):
            raise TypeError("snapshot must be a JSON string")
        try:
            envelope = json.loads(snapshot)
        except json.JSONDecodeError as exception:
            raise ValueError("snapshot must contain valid JSON") from exception
        if not isinstance(envelope, dict):
            raise ValueError("snapshot envelope must be a JSON object")
        schema_id = envelope.get("schema")
        if (not isinstance(schema_id, str)
                or schema_id not in self.supported_snapshot_schema_ids):
            raise EmulatorError(
                "Snapshot schema was not advertised by the emulator")
        return envelope

    def load_snapshot(self, snapshot):
        self._validate_run_snapshot_schema(snapshot)
        return State(self, self.call("load_snapshot", snapshot=snapshot)["stateHandle"])

    def load_scripted_run_snapshot(self, snapshot: str):
        if self.capabilities.get("scriptedRunSessionSchemaId") != (
                "sts2-v111-scripted-run-session-v1"):
            raise EmulatorError(
                "Emulator does not expose whole-run scripted sessions")
        operations = self.capabilities.get(
            "scriptedRunSessionOperations", [])
        if (not isinstance(operations, list) or
                "scripted_run_load" not in operations):
            raise EmulatorError(
                "Whole-run scripted load is not advertised")
        result = self.call("scripted_run_load", snapshot=snapshot)
        return ScriptedRunSession.from_wire(self, result["result"])

    def release_scripted_run_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "Scripted run handle belongs to another session")
        return self.call(
            "scripted_run_release_many",
            scripted_run_handles=[item.handle for item in sessions])["released"]

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

    def begin_semantic_reference_combat(
            self, combat: SemanticCombat, nature_seed: int | None = None):
        """Replay semantic combat through the isolated ReferenceNature sampler."""
        if self.capabilities.get("referenceNatureSessionSchemaId") != (
                "sts2-v111-reference-nature-session-v1"):
            raise EmulatorError(
                "Emulator does not expose semantic ReferenceNature sessions")
        if not isinstance(combat, SemanticCombat) or combat.session is not self:
            raise ValueError("Semantic combat handle belongs to another session")
        if nature_seed is not None and (
                isinstance(nature_seed, bool) or not isinstance(nature_seed, int)
                or nature_seed < 0 or nature_seed > 0xffffffffffffffff):
            raise ValueError(
                "nature_seed must be an unsigned 64-bit integer")
        payload = {"semantic_handle": combat.handle}
        if nature_seed is not None:
            payload["nature_seed"] = nature_seed
        return SemanticReferenceCombat.from_wire(
            self, self.call("semantic_reference_begin", **payload))

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

    def load_semantic_reference_snapshot(self, snapshot: str):
        if self.capabilities.get("referenceNatureSessionSchemaId") != (
                "sts2-v111-reference-nature-session-v1"):
            raise EmulatorError(
                "Emulator does not expose semantic ReferenceNature sessions")
        return SemanticReferenceCombat.from_wire(
            self, self.call("semantic_reference_load", snapshot=snapshot))

    def release_semantic_reference_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "Semantic ReferenceNature handle belongs to another session")
        return self.call(
            "semantic_reference_release_many",
            reference_nature_handles=[item.handle for item in sessions])[
                "released"]

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

    def load_reference_nature_neow_offer_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureNeowOfferSessionSchemaId") != (
                "sts2-v111-reference-nature-neow-offer-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Neow-offer sessions")
        result = self.call("reference_nature_neow_offer_load",
                           snapshot=snapshot)
        return ReferenceNatureNeowOfferSession.from_wire(
            self, result["result"])

    def release_reference_nature_neow_offer_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Neow-offer handle belongs to another session")
        return self.call(
            "reference_nature_neow_offer_release_many",
            reference_nature_neow_offer_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_trash_heap_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureTrashHeapSessionSchemaId") != (
                "sts2-v111-reference-nature-trash-heap-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Trash Heap sessions")
        result = self.call("reference_nature_trash_heap_load",
                           snapshot=snapshot)
        return ReferenceNatureTrashHeapSession.from_wire(
            self, result["result"])

    def release_reference_nature_trash_heap_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Trash Heap handle belongs to another session")
        return self.call(
            "reference_nature_trash_heap_release_many",
            reference_nature_trash_heap_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_tablet_of_truth_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureTabletOfTruthSessionSchemaId") != (
                "sts2-v111-reference-nature-tablet-of-truth-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Tablet of Truth sessions")
        result = self.call("reference_nature_tablet_of_truth_load",
                           snapshot=snapshot)
        return ReferenceNatureTabletOfTruthSession.from_wire(
            self, result["result"])

    def release_reference_nature_tablet_of_truth_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Tablet of Truth handle belongs to another session")
        return self.call(
            "reference_nature_tablet_of_truth_release_many",
            reference_nature_tablet_of_truth_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_trial_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureTrialSessionSchemaId") != (
                "sts2-v111-reference-nature-trial-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Trial sessions")
        result = self.call("reference_nature_trial_load", snapshot=snapshot)
        return ReferenceNatureTrialSession.from_wire(
            self, result["result"])

    def release_reference_nature_trial_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Trial handle belongs to another session")
        return self.call(
            "reference_nature_trial_release_many",
            reference_nature_trial_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_native_event_shuffle_snapshot(
            self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureNativeEventShuffleSessionSchemaId") != (
                "sts2-v111-reference-nature-native-event-shuffle-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature native event shuffle sessions")
        result = self.call(
            "reference_nature_native_event_shuffle_load", snapshot=snapshot)
        return ReferenceNatureNativeEventShuffleSession.from_wire(
            self, result["result"])

    def release_reference_nature_native_event_shuffle_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature native event shuffle handle belongs to another session")
        return self.call(
            "reference_nature_native_event_shuffle_release_many",
            reference_nature_native_event_shuffle_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_native_event_gold_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureNativeEventGoldSessionSchemaId") != (
                "sts2-v111-reference-nature-native-event-gold-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Native event gold sessions")
        result = self.call(
            "reference_nature_native_event_gold_load", snapshot=snapshot)
        return ReferenceNatureNativeEventGoldSession.from_wire(
            self, result["result"])

    def load_reference_nature_native_event_relic_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureNativeEventRelicSessionSchemaId") != (
                "sts2-v111-reference-nature-native-event-relic-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Native event relic sessions")
        result = self.call(
            "reference_nature_native_event_relic_load", snapshot=snapshot)
        return ReferenceNatureNativeEventRelicSession.from_wire(
            self, result["result"])

    def load_reference_nature_native_shovel_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureNativeShovelSessionSchemaId") != (
                "sts2-v111-reference-nature-native-shovel-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Native Shovel sessions")
        result = self.call(
            "reference_nature_native_shovel_load", snapshot=snapshot)
        return ReferenceNatureNativeShovelSession.from_wire(
            self, result["result"])

    def release_reference_nature_native_event_gold_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Native event gold handle belongs to another session")
        return self.call(
            "reference_nature_native_event_gold_release_many",
            reference_nature_native_event_gold_handles=[
                item.handle for item in sessions])["released"]

    def release_reference_nature_native_event_relic_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Native event relic handle belongs to another session")
        return self.call(
            "reference_nature_native_event_relic_release_many",
            reference_nature_native_event_relic_handles=[
                item.handle for item in sessions])["released"]

    def release_reference_nature_native_shovel_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Native Shovel handle belongs to another session")
        return self.call(
            "reference_nature_native_shovel_release_many",
            reference_nature_native_shovel_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_endless_conveyor_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureEndlessConveyorSessionSchemaId") != (
                "sts2-v111-reference-nature-endless-conveyor-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Endless Conveyor sessions")
        result = self.call(
            "reference_nature_endless_conveyor_load", snapshot=snapshot)
        return ReferenceNatureEndlessConveyorSession.from_wire(
            self, result["result"])

    def release_reference_nature_endless_conveyor_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Endless Conveyor handle belongs to another session")
        return self.call(
            "reference_nature_endless_conveyor_release_many",
            reference_nature_endless_conveyor_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_glory_reflections_snapshot(
            self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureGloryReflectionsSessionSchemaId") != (
                "sts2-v111-reference-nature-glory-reflections-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Glory Reflections sessions")
        result = self.call(
            "reference_nature_glory_reflections_load", snapshot=snapshot)
        return ReferenceNatureGloryReflectionsSession.from_wire(
            self, result["result"])

    def release_reference_nature_glory_reflections_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Glory Reflections handle belongs to another session")
        return self.call(
            "reference_nature_glory_reflections_release_many",
            reference_nature_glory_reflections_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_infested_automaton_snapshot(
            self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureInfestedAutomatonSessionSchemaId") != (
                "sts2-v111-reference-nature-infested-automaton-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Infested Automaton sessions")
        result = self.call(
            "reference_nature_infested_automaton_load", snapshot=snapshot)
        return ReferenceNatureInfestedAutomatonSession.from_wire(
            self, result["result"])

    def release_reference_nature_infested_automaton_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Infested Automaton handle belongs to another session")
        return self.call(
            "reference_nature_infested_automaton_release_many",
            reference_nature_infested_automaton_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_punch_off_reward_snapshot(
            self, snapshot: str):
        if self.capabilities.get(
                "referenceNaturePunchOffRewardSessionSchemaId") != (
                "sts2-v111-reference-nature-punch-off-reward-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Punch Off reward sessions")
        result = self.call(
            "reference_nature_punch_off_reward_load", snapshot=snapshot)
        return ReferenceNaturePunchOffRewardSession.from_wire(
            self, result["result"])

    def release_reference_nature_punch_off_reward_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Punch Off reward handle belongs to another session")
        return self.call(
            "reference_nature_punch_off_reward_release_many",
            reference_nature_punch_off_reward_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_punch_off_fight_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNaturePunchOffFightSessionSchemaId") != (
                "sts2-v111-reference-nature-punch-off-fight-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature Punch Off fight sessions")
        result = self.call(
            "reference_nature_punch_off_fight_load", snapshot=snapshot)
        return ReferenceNaturePunchOffFightSession.from_wire(
            self, result["result"])

    def release_reference_nature_punch_off_fight_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature Punch Off fight handle belongs to another session")
        return self.call(
            "reference_nature_punch_off_fight_release_many",
            reference_nature_punch_off_fight_handles=[
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

    def load_reference_nature_treasure_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureTreasureSessionSchemaId") != (
                "sts2-v111-reference-nature-treasure-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature treasure sessions")
        result = self.call("reference_nature_treasure_load",
                           snapshot=snapshot)
        return ReferenceNatureTreasureSession.from_wire(
            self, result["result"])

    def release_reference_nature_treasure_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature treasure handle belongs to another session")
        return self.call(
            "reference_nature_treasure_release_many",
            reference_nature_treasure_handles=[
                item.handle for item in sessions])["released"]

    def load_reference_nature_combat_reward_snapshot(self, snapshot: str):
        if self.capabilities.get(
                "referenceNatureCombatRewardSessionSchemaId") != (
                "sts2-v111-reference-nature-combat-reward-session-v1"):
            raise EmulatorError(
                "Emulator does not expose ReferenceNature combat reward sessions")
        result = self.call(
            "reference_nature_combat_reward_load", snapshot=snapshot)
        return ReferenceNatureCombatRewardSession.from_wire(
            self, result["result"])

    def release_reference_nature_combat_reward_many(self, sessions):
        sessions = list(sessions)
        if any(item.session is not self for item in sessions):
            raise ValueError(
                "ReferenceNature combat reward handle belongs to another session")
        return self.call(
            "reference_nature_combat_reward_release_many",
            reference_nature_combat_reward_handles=[
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
