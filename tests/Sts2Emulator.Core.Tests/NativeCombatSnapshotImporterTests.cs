using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeCombatSnapshotImporterTests
{
    private const string NativeState = """
    {
      "player": {
        "hp": 63, "max_hp": 70, "block": 5,
        "powers": [{"model_id":"Dexterity","amount":1}]
      },
      "combat": {
        "energy": 2, "max_energy": 3, "turn": 1,
        "creatures": [
          {"id":"creature:1","model_id":"Silent","hp":63,"max_hp":70,
           "block":5,"alive":true,"powers":[]},
          {"id":"creature:2","model_id":"JawWorm","hp":35,"max_hp":35,
           "block":0,"alive":true,"powers":[]}
        ],
        "hand": [
          {"instance_id":11,"model_id":"Strike_Silent","upgrades":0,
           "target_type":"AnyEnemy","cost":1}
        ],
        "draw_pile": [],
        "discard_pile": [
          {"instance_id":12,"model_id":"Defend_Silent","upgrades":0,
           "target_type":"Self","cost":1}
        ],
        "exhaust_pile": []
      }
    }
    """;

    [Fact]
    public void VisibleStateIsParsedAndNeverFalselyDeclaredStepReady()
    {
        using var doc = JsonDocument.Parse(NativeState);
        var result = NativeCombatSnapshotImporter.Import(
            doc.RootElement, NativeCombatSnapshotImporter.RequiredVisibleFields);
        Assert.Equal("PARTIAL", result.Status);
        Assert.False(result.ReadyForDeterministicStep);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(63, result.Snapshot.Hp);
        Assert.Equal(2, result.Snapshot.Energy);
        Assert.Equal(2, result.Snapshot.Creatures.Length);
        Assert.Equal(11, result.Snapshot.Hand[0].InstanceId);
        Assert.Contains("$.combat.rng_state", result.MissingContinuationFields);
        Assert.Empty(result.MissingFields);
    }

    [Fact]
    public void MissingFieldRejectsImportWithoutFabricatingDefaults()
    {
        string partial = NativeState.Replace("\"max_energy\": 3, ", "");
        using var doc = JsonDocument.Parse(partial);
        var result = NativeCombatSnapshotImporter.Import(doc.RootElement);
        Assert.Equal("UNSUPPORTED", result.Status);
        Assert.Null(result.Snapshot);
        Assert.Contains("$.combat.max_energy", result.MissingFields);
    }

    [Fact]
    public void DuplicateCardInstanceAcrossPilesIsRejected()
    {
        string duplicated = NativeState.Replace("\"instance_id\":12", "\"instance_id\":11");
        using var doc = JsonDocument.Parse(duplicated);
        var result = NativeCombatSnapshotImporter.Import(
            doc.RootElement, NativeCombatSnapshotImporter.RequiredVisibleFields);
        Assert.Equal("UNSUPPORTED", result.Status);
        Assert.Contains("DUPLICATE_CARD_INSTANCE_ACROSS_ZONES", result.Diagnostics);
    }

    [Fact]
    public void MalformedCreatureOrUnassignedIdentityIsRejected()
    {
        using var doc = JsonDocument.Parse(NativeState.Replace("creature:2", "creature:UNASSIGNED"));
        var result = NativeCombatSnapshotImporter.Import(doc.RootElement);
        Assert.Equal("UNSUPPORTED", result.Status);
        Assert.Contains("INVALID_CREATURE_ID_OR_SCALARS", result.Diagnostics);
    }

    [Fact]
    public void PresentFieldsStillRequireExternalCoverageAttestation()
    {
        using var doc = JsonDocument.Parse(NativeState);
        var result = NativeCombatSnapshotImporter.Import(doc.RootElement);
        Assert.Equal("PARTIAL", result.Status);
        Assert.Empty(result.VerifiedFields);
        Assert.Contains("unaudited", result.Diagnostics[0]);
    }
}
