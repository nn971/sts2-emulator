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

public sealed record PlayerState(
    int Hp,
    int MaxHp,
    int Gold,
    CardInstance[] Deck,
    RelicInstance[] Relics,
    PotionInstance?[] PotionSlots,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    CharacterResourceState? Resources = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<RelicInstance> ActiveRelics => Relics.Where(relic => !relic.IsMelted);

    public PlayerState Fork() => this with
    {
        Deck = (CardInstance[])Deck.Clone(),
        Relics = (RelicInstance[])Relics.Clone(),
        PotionSlots = (PotionInstance?[])PotionSlots.Clone(),
        Resources = Resources?.Fork()
    };
}

/// <summary>
/// Canonical run-state envelope. World contains typed whole-run mechanics for implemented
/// rulesets; ExtensionState remains available for versioned reference data that has not
/// yet earned a stable typed representation.
/// </summary>
public sealed record RunState(
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
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    RunConfiguration? Configuration = null,
    RngBundle? NativeRunRng = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    ControlledEventChoiceTraceV111[]? ControlledEventChoiceHistory = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    PrototypeControlledEventRewardChoiceContinuationV111?
        ControlledEventRewardChoice = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    ControlledRunActionTraceV111[]? ControlledRunActionHistory = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    PrototypeControlledRunActionContinuationV111? ControlledRunActionChoice = null,
    ReferenceNatureStateV111? ReferenceNatureState = null)
{
    /// <summary>
    /// Authoritative in-memory run-level provider state. The legacy-named
    /// properties below remain the snapshot wire projection for compatibility.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public RunStochasticStateV111 Stochastic { get; init; } =
        new(Rng, NativeRunRng, ReferenceNatureState);

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

    public RunState Fork() => this with
    {
        Player = Player.Fork(),
        Stochastic = Stochastic.Fork(),
        ControlledEventChoiceHistory = ControlledEventChoiceHistory?.Select(
            trace => trace.Fork()).ToArray(),
        ControlledEventRewardChoice = ControlledEventRewardChoice?.Fork(),
        ControlledRunActionHistory = ControlledRunActionHistory?.Select(
            trace => trace.Fork()).ToArray(),
        ControlledRunActionChoice = ControlledRunActionChoice?.Fork(),
        ExtensionState = ExtensionState.Clone(),
        World = World?.Fork(),
        Configuration = Configuration?.Fork()
    };
}
