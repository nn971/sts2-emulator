using System.Text.Json;

namespace Sts2Emulator.Core;

public enum PrototypeCardEnchantmentKind
{
    Spiral,
    Glam,
    Inky,
    Sown,
    Slither,
    Nimble,
    Steady,
    PerfectFit,
    SoulsPower,
    TezcatarasEmber,
    Goopy,
    Clone,
    Imbued,
    Instinct,
    Swift,
    Sharp,
    Vigorous
}

public sealed record PrototypeCardEnchantment(
    PrototypeCardEnchantmentKind Kind,
    int Amount = 1,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool Disabled = false);

public sealed record CardInstance(
    long InstanceId,
    string CardId,
    int UpgradeLevel,
    JsonElement PersistentState,
    PrototypeCardEnchantment? Enchantment = null);

public sealed record RelicInstance(
    string RelicId,
    JsonElement PersistentState,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool IsWax = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool IsMelted = false);

public sealed record PotionInstance(
    string PotionId,
    JsonElement PersistentState);

public sealed record PlayerState
{
    private CardInstance[]? _deck;
    private RelicInstance[]? _relics;
    private PotionInstance?[]? _potionSlots;
    private CharacterResourceState? _resources;

    [System.Text.Json.Serialization.JsonConstructor]
    public PlayerState(
        int Hp,
        int MaxHp,
        int Gold,
        CardInstance[] Deck,
        RelicInstance[] Relics,
        PotionInstance?[] PotionSlots,
        CharacterResourceState? Resources = null)
    {
        this.Hp = Hp;
        this.MaxHp = MaxHp;
        this.Gold = Gold;
        this.Deck = Deck;
        this.Relics = Relics;
        this.PotionSlots = PotionSlots;
        this.Resources = Resources;
    }

    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Gold { get; init; }
    public CardInstance[] Deck
    {
        get => _deck is null ? null! : (CardInstance[])_deck.Clone();
        init => _deck = Copy(value);
    }
    public RelicInstance[] Relics
    {
        get => _relics is null ? null! : (RelicInstance[])_relics.Clone();
        init => _relics = Copy(value);
    }
    public PotionInstance?[] PotionSlots
    {
        get => _potionSlots is null ? null! : (PotionInstance?[])_potionSlots.Clone();
        init => _potionSlots = Copy(value);
    }
    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CharacterResourceState? Resources
    {
        get => _resources;
        init => _resources = value?.Fork();
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<RelicInstance> ActiveRelics => Relics.Where(relic => !relic.IsMelted);

    public PlayerState Fork() => new(
        Hp,
        MaxHp,
        Gold,
        _deck!,
        _relics!,
        _potionSlots!,
        _resources);

    public void Deconstruct(
        out int Hp,
        out int MaxHp,
        out int Gold,
        out CardInstance[] Deck,
        out RelicInstance[] Relics,
        out PotionInstance?[] PotionSlots,
        out CharacterResourceState? Resources)
    {
        Hp = this.Hp;
        MaxHp = this.MaxHp;
        Gold = this.Gold;
        Deck = this.Deck;
        Relics = this.Relics;
        PotionSlots = this.PotionSlots;
        Resources = this.Resources;
    }

    private static T[]? Copy<T>(T[]? values) =>
        values is null ? null : (T[])values.Clone();
}

/// <summary>
/// Canonical run-state envelope. World contains typed whole-run mechanics for implemented
/// rulesets; ExtensionState remains available for versioned reference data that has not
/// yet earned a stable typed representation.
/// </summary>
public sealed partial record RunState(
    string GameBuild,
    string EmulatorSchema,
    string RunId,
    string RunSeed,
    long DecisionIndex,
    RunPhase Phase,
    PlayerState Player,
    RngBundle Rng,
    JsonElement ExtensionState,
    RunWorldState? World = null,
    int Ascension = 0,
    RunConfiguration? Configuration = null,
    RngBundle? NativeRunRng = null,
    ControlledEventChoiceTraceV111[]? ControlledEventChoiceHistory = null,
    PrototypeControlledEventRewardChoiceContinuationV111?
        ControlledEventRewardChoice = null,
    ControlledRunActionTraceV111[]? ControlledRunActionHistory = null,
    PrototypeControlledRunActionContinuationV111? ControlledRunActionChoice = null,
    ReferenceNatureStateV111? ReferenceNatureState = null)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for the complete run. Legacy-named
    /// members remain API projections; v4 snapshots serialize this envelope.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<RunMechanicsStateV111,
        RunStochasticStateV111> Simulation { get; init; } = new(
            new RunMechanicsStateV111(
                GameBuild, EmulatorSchema, RunId, RunSeed, DecisionIndex,
                Phase, Player, ExtensionState, World, Ascension, Configuration),
            new RunStochasticStateV111(
                Rng, NativeRunRng, ReferenceNatureState,
                ControlledEventChoiceHistory, ControlledEventRewardChoice,
                ControlledRunActionHistory, ControlledRunActionChoice));

    [System.Text.Json.Serialization.JsonIgnore]
    public RunMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    /// <summary>
    /// Run-level provider state. The legacy-named properties below remain
    /// the snapshot wire projection for compatibility.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public RunStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public SemanticOtherChanceStateV111? SemanticOtherChance
    {
        get => Stochastic.SemanticOtherChance;
        init => Stochastic = RunStochasticStateV111.WithSemanticOtherChance(
            Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public SemanticRewardAuditV111? SemanticRewardAudit
    {
        get => Stochastic.SemanticRewardAudit;
        init => Stochastic = RunStochasticStateV111.WithSemanticRewardAudit(
            Stochastic, value);
    }

    public string GameBuild
    {
        get => Mechanics.GameBuild;
        init => Mechanics = Mechanics with { GameBuild = value };
    }

    public string EmulatorSchema
    {
        get => Mechanics.EmulatorSchema;
        init => Mechanics = Mechanics with { EmulatorSchema = value };
    }

    public string RunId
    {
        get => Mechanics.RunId;
        init => Mechanics = Mechanics with { RunId = value };
    }

    public string RunSeed
    {
        get => Mechanics.RunSeed;
        init => Mechanics = Mechanics with { RunSeed = value };
    }

    public long DecisionIndex
    {
        get => Mechanics.DecisionIndex;
        init => Mechanics = Mechanics with { DecisionIndex = value };
    }

    public RunPhase Phase
    {
        get => Mechanics.Phase;
        init => Mechanics = Mechanics with { Phase = value };
    }

    public PlayerState Player
    {
        get => Mechanics.Player;
        init => Mechanics = Mechanics with { Player = value };
    }

    public JsonElement ExtensionState
    {
        get => Mechanics.ExtensionState;
        init => Mechanics = Mechanics with { ExtensionState = value };
    }

    public RunWorldState? World
    {
        get => Mechanics.World;
        init => Mechanics = Mechanics with { World = value };
    }

    public int Ascension
    {
        get => Mechanics.Ascension;
        init => Mechanics = Mechanics with { Ascension = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RunConfiguration? Configuration
    {
        get => Mechanics.Configuration;
        init => Mechanics = Mechanics with { Configuration = value };
    }

    public RngBundle Rng
    {
        get => Stochastic.PrototypeRng;
        init => Stochastic = RunStochasticStateV111.WithPrototypeRng(
            Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RngBundle? NativeRunRng
    {
        get => Stochastic.NativeRunRng;
        init => Stochastic = RunStochasticStateV111.WithNativeRunRng(
            Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ReferenceNatureStateV111? ReferenceNatureState
    {
        get => Stochastic.ReferenceNature;
        init => Stochastic = RunStochasticStateV111.WithReferenceNature(
            Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ReferenceNatureRunInitializationTranscriptV111?
        ReferenceNatureRunInitializationTranscript
    {
        get => Stochastic.ReferenceNatureRunInitializationTranscript;
        init => Stochastic = RunStochasticStateV111
            .WithReferenceNatureRunInitializationTranscript(
                Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReferenceNatureRunInitializationPending
    {
        get => Stochastic.ReferenceNatureRunInitializationPending;
        init => Stochastic = RunStochasticStateV111
            .WithReferenceNatureRunInitializationPending(Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ReferenceNatureMapGenerationAuditV111[]?
        ReferenceNatureMapGenerationHistory
    {
        get => Stochastic.ReferenceNatureMapGenerationHistory;
        init => Stochastic = RunStochasticStateV111
            .WithReferenceNatureMapGenerationHistory(Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ReferenceNatureAncientOfferSessionSnapshotV111[]?
        ReferenceNatureAncientOfferHistory
    {
        get => Stochastic.ReferenceNatureAncientOfferHistory;
        init => Stochastic = RunStochasticStateV111
            .WithReferenceNatureAncientOfferHistory(Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ControlledEventChoiceTraceV111[]? ControlledEventChoiceHistory
    {
        get => Stochastic.ControlledEventChoiceHistory;
        init => Stochastic =
            RunStochasticStateV111.WithControlledEventChoiceHistory(
                Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeControlledEventRewardChoiceContinuationV111?
        ControlledEventRewardChoice
    {
        get => Stochastic.ControlledEventRewardChoice;
        init => Stochastic =
            RunStochasticStateV111.WithControlledEventRewardChoice(
                Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ControlledRunActionTraceV111[]? ControlledRunActionHistory
    {
        get => Stochastic.ControlledRunActionHistory;
        init => Stochastic =
            RunStochasticStateV111.WithControlledRunActionHistory(
                Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeControlledRunActionContinuationV111?
        ControlledRunActionChoice
    {
        get => Stochastic.ControlledRunActionChoice;
        init => Stochastic =
            RunStochasticStateV111.WithControlledRunActionChoice(
                Stochastic, value);
    }

    public RunState Fork() => this with
    {
        Player = Player.Fork(),
        Stochastic = Stochastic.Fork(),
        ExtensionState = ExtensionState.Clone(),
        World = World?.Fork(),
        Configuration = Configuration?.Fork()
    };
}
