namespace Sts2Emulator.Core;

/// <summary>
/// An explicit later-act map inspection/quest fixture. This is not a
/// playable native route, and regular Hive/Glory rooms stay unsupported.
/// </summary>
public static class PrototypeNativeLaterActFixture
{
    /// <summary>
    /// Begin a specifically named, source-backed Glory event on a
    /// generated Unknown node. This is an oracle/test fixture, not a
    /// substitute for the unresolved native event selection bag.
    /// </summary>
    public static RunState CreateGloryHungryForMushrooms(
        string seed, int ascension = 0) =>
        CreateGloryEvent(seed,
            PrototypeNativeGloryEvents.HungryForMushroomsId, ascension);

    public static RunState CreateGloryEvent(
        string seed, string eventId, int ascension = 0)
    {
        if (eventId is not (
            PrototypeNativeGloryEvents.HungryForMushroomsId
            or PrototypeNativeGloryEvents.ReflectionsId
            or PrototypeNativeGloryEvents.RoundTeaPartyId
            or PrototypeNativeGloryEvents.GraveOfTheForgottenId
            or PrototypeNativeGloryEvents.TrialId
            or PrototypeNativeGloryEvents.BattlewornDummyId
            or PrototypeNativeGloryEvents.TinkerTimeId))
            throw new NotSupportedException(
                "The requested Glory event does not have a supported " +
                "full-map isolated event fixture.");

        var state = Create(seed, 3, ascension);
        var world = state.World!;
        var point = world.Map.Nodes.First(node =>
            node.RoomType == PrototypeRoomType.Unknown
            && node.NextNodeIds is { Length: > 0 });
        var id = eventId;
        return state with
        {
            World = world with
            {
                Floor = point.Floor,
                ActiveRoom = PrototypeRoomType.Event,
                Map = world.Map with { CurrentNodeId = point.NodeId },
                Event = new EventState(id),
                EventHistory = world.EventIds.Append(id).ToArray()
            },
            Phase = RunPhase.Event
        };
    }

    /// <summary>
    /// Prepare a named source-backed Hive/Glory formation in isolation,
    /// with the native encounter-local RNG and the independent monster
    /// contexts. Native entry names (e.g. BowlbugsNormal) must be supplied
    /// explicitly; emulator registry ids are not native hash keys.
    /// This does not start combat, roll HP or enable gated later-act rooms.
    /// The caller must supply native total floor, map coordinates, and the
    /// first enemy CombatId (after the player and any earlier creatures).
    /// </summary>
    public static NativeEncounterFormationV111 GenerateEncounterFormation(
        string seed,
        int act,
        string emulatorEncounterId,
        string nativeEncounterEntry,
        int totalFloor,
        long firstMonsterCombatId,
        int? mapColumn = null,
        int? mapRow = null)
    {
        _ = PrototypeNativeLaterActMap.ProfileId(act);
        var encounter = PrototypeContent.Encounter(emulatorEncounterId);
        if (encounter.MinAct > act || encounter.MaxAct < act)
            throw new ArgumentException(
                $"Encounter '{emulatorEncounterId}' is not valid in act {act}.",
                nameof(emulatorEncounterId));

        return NativeEncounterFormationFactoryV111.Generate(
            seed, encounter, nativeEncounterEntry, totalFloor,
            currentActIndex: act - 1,
            firstMonsterCombatId, mapColumn, mapRow);
    }

    /// <summary>
    /// A fast, independent combat-entry RNG snapshot for pinned Hive/Glory
    /// encounters. Unlike Create(...), this bypasses map-room entry entirely.
    /// It samples native run.niche HP and commits native run.monster_ai
    /// intents, but deliberately omits player deck, rewards, card draw,
    /// combat hooks and cross-battle run-RNG history. It is not a playable run.
    /// </summary>
    public static (
        CombatState Combat, NativeEncounterFormationV111 Formation,
        RngBundle NativeStreams) CreateCombatRngPreEntry(
        string seed, int act, string emulatorEncounterId,
        string nativeEncounterEntry, int totalFloor, int ascension = 0,
        long firstMonsterCombatId = 1, int? mapColumn = null,
        int? mapRow = null)
    {
        if (ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        var formation = GenerateEncounterFormation(
            seed, act, emulatorEncounterId, nativeEncounterEntry,
            totalFloor, firstMonsterCombatId, mapColumn, mapRow);
        var enemies = formation.Enemies.Select((spec, index) =>
        {
            var definition = PrototypeContent.Enemy(spec.EnemyId);
            var starting = definition.HpRangeAt(act, ascension).Max;
            var powers = (definition.StartingPowers
                    ?? Array.Empty<PrototypeStartingPowerSpec>())
                .Select((power, index) =>
                    new PrototypePowerInstanceState(
                        power.PowerId, power.StacksAt(ascension), index + 1))
                .Where(power => power.Stacks > 0).ToArray();
            return new EnemyCombatState(
                InstanceId: index + 1,
                EnemyId: spec.EnemyId,
                Hp: starting,
                MaxHp: starting,
                Block: powers.Sum(power => power.Stacks
                    * PrototypeContent.Power(power.PowerId)
                        .EnemyStartingBlockPerStack),
                MoveIndex: 0,
                Statuses: new Dictionary<string, int>(StringComparer.Ordinal),
                Powers: powers,
                FormationPosition: spec.FormationPosition,
                SlotName: spec.SlotName,
                LeaderEnemyInstanceId: spec.LeaderFormationPosition is { } leader
                    ? Array.FindIndex(formation.Enemies, other =>
                        other.FormationPosition == leader) + 1
                    : null,
                NonSummonMovesUntilEligible: definition.NonSummonMovesBeforeEligible);
        }).ToArray();

        var bare = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1,
            Act: act,
            Ascension: ascension);
        return (bare, formation, NativeRngV111.CreateBundle(seed));
    }

    public static CombatState CreateCombatRngSnapshot(
        string seed,
        int act,
        string emulatorEncounterId,
        string nativeEncounterEntry,
        int totalFloor,
        int ascension = 0,
        long firstMonsterCombatId = 1,
        int? mapColumn = null,
        int? mapRow = null,
        bool commitInitialIntents = true)
    {
        var prepared = CreateCombatRngPreEntry(
            seed, act, emulatorEncounterId, nativeEncounterEntry,
            totalFloor, ascension, firstMonsterCombatId, mapColumn, mapRow);
        var native = NativeCombatRngEntryV111.AttachAndRollInitialHp(
            prepared.Combat, prepared.Formation, prepared.NativeStreams);
        return commitInitialIntents
            ? PrototypeGameEngine.CommitEnemyIntents(
                native, PrototypeRng.CreateBundle(seed))
            : native;
    }

    public static RunState Create(
        string seed, int act, int ascension = 0, int lanternKeys = 0)
    {
        _ = PrototypeNativeLaterActMap.ProfileId(act);
        if (lanternKeys < 0 || lanternKeys > 32
            || (lanternKeys > 0 && act != 3))
            throw new ArgumentOutOfRangeException(nameof(lanternKeys),
                "Lantern Keys are supported by this fixture in Glory only.");

        var engine = new PrototypeGameEngine();
        var state = engine.Step(
            PrototypeGameFactory.Create(seed, ascension),
            GameAction.Empty("start_run")).State;
        var world = state.World
            ?? throw new InvalidOperationException("Run did not initialize.");

        // Remove unrelated six-floor prototype map consumption from the
        // compatibility state. Native map generation itself uses a fresh
        // act-local stream and leaves this bundle untouched.
        var rng = state.Rng.Fork();
        var clean = PrototypeRng.CreateBundle(seed);
        var index = Array.FindIndex(rng.Streams,
            stream => stream.StreamId == "map");
        var cleanIndex = Array.FindIndex(clean.Streams,
            stream => stream.StreamId == "map");
        rng.Streams[index] = clean.Streams[cleanIndex].Fork();

        var map = PrototypeNativeLaterActMap.GenerateNative(seed, act, ascension);
        var keyId = PrototypeNativeHiveEvents.LanternKeyCardId;
        var deck = state.Player.Deck.Concat(
            Enumerable.Range(0, lanternKeys).Select(i =>
                new CardInstance(world.NextCardInstanceId + i,
                    keyId, 0, PrototypeJson.EmptyObject()))).ToArray();

        return state with
        {
            Player = state.Player with { Deck = deck },
            Rng = rng,
            World = world with
            {
                Act = act,
                Floor = 0,
                Map = map,
                ActiveRoom = null,
                Combat = null,
                Event = null,
                Reward = null,
                Shop = null,
                ActOneRegion = null,
                ActOneEncounterPool = null,
                NativeOvergrowthOpening = false,
                NextCardInstanceId = world.NextCardInstanceId + lanternKeys
            },
            Phase = RunPhase.MapChoice
        };
    }
}
