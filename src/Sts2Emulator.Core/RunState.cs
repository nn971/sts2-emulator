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
    Swift
}

public sealed record PrototypeCardEnchantment(
    PrototypeCardEnchantmentKind Kind,
    int Amount = 1);

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
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    RngBundle? NativeRunRng = null)
{
    public RunState Fork() => this with
    {
        Player = Player.Fork(),
        Rng = Rng.Fork(),
        NativeRunRng = NativeRunRng?.Fork(),
        ExtensionState = ExtensionState.Clone(),
        World = World?.Fork(),
        Configuration = Configuration?.Fork()
    };
}
