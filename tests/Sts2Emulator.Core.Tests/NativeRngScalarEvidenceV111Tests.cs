using System.Text.Json;
using Sts2Emulator.Core;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeRngScalarEvidenceV111Tests
{
    private static readonly NativeRngSnapshot Before = new(12, 1, 2, 3, 4);
    private static readonly NativeRngSnapshot After = new(13, 5, 6, 7, 8);

    [Theory]
    [InlineData("unit_double", 0.375, null)]
    [InlineData("boolean", null, true)]
    [InlineData("boolean", null, false)]
    public void TypedNativeSourceValuesValidate(
        string kind, double? number, bool? boolean)
    {
        var eventValue = new NativeRngScalarEvidenceV111(
            "UNATTRIBUTED", "run.monster_ai", "run", 0,
            kind, number, boolean, Before, After);
        eventValue.Validate();
        Assert.False(eventValue.Attributed);
    }

    [Fact]
    public void InvalidScalarValuesAndCountersAreRejected()
    {
        NativeRngScalarEvidenceV111 Good() => new(
            "UNATTRIBUTED", "run.monster_ai", "run", 0,
            "unit_double", 0.125, null, Before, After);
        Assert.Throws<ArgumentException>(() =>
            (Good() with { SelectedDouble = 1.0 }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Good() with { SelectedBool = true }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Good() with { CursorAfter = Before }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Good() with { Origin = "rendered_frame_poll" }).Validate());
    }

    [Fact]
    public void RangedFloatSourceRequiresExactTypedBoundsIncludingEqualBounds()
    {
        var good = new NativeRngScalarEvidenceV111(
            "UNATTRIBUTED", "run.monster_ai", "creature:7", 0,
            "range_float", null, null, Before, After,
            FloatMin: 2.5f, FloatMax: 2.5f, SelectedFloat: 2.5f);
        good.Validate();
        Assert.False(good.Attributed);
        Assert.Throws<ArgumentException>(() =>
            (good with { FloatMax = 1.0f }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (good with { SelectedFloat = float.NaN }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (good with { SelectedDouble = 0.1 }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (good with { SelectedFloat = null }).Validate());
    }

    [Fact]
    public async Task OriginalRangedFloatResultAndBoundsAreSerializedInActionSidecar()
    {
        string file = Path.Combine(Path.GetTempPath(),
            "sts2-nextfloat-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var recorder = new NativeActionEnvelopeRecorder(
                file, "synthetic-sha", "synthetic-run",
                nativeRngHookInstalled: true,
                nativeRngFloatHookInstalled: true);
            recorder.NativeRngScalarBoundary(new(
                "UNATTRIBUTED", "run.monster_ai", "creature:2", 0,
                "range_float", null, null, Before, After,
                FloatMin: -1.25f, FloatMax: 4.5f, SelectedFloat: 0.75f));
            recorder.Finish("Combat ended", null);
            await recorder.DisposeAsync();
            var records = File.ReadAllLines(file)
                .Select(x => JsonDocument.Parse(x).RootElement.Clone()).ToArray();
            Assert.True(records[0].GetProperty("data")
                .GetProperty("native_rng_float_hook_installed").GetBoolean());
            var ev = Assert.Single(records, x => x.GetProperty("type")
                .GetString() == "native_rng_scalar_boundary");
            Assert.Equal("Rng.NextFloat(float,float)",
                ev.GetProperty("native_type").GetString());
            var data = ev.GetProperty("data");
            Assert.Equal("range_float", data.GetProperty("operation_kind").GetString());
            Assert.Equal(-1.25f, data.GetProperty("float_min").GetSingle());
            Assert.Equal(4.5f, data.GetProperty("float_max").GetSingle());
            Assert.Equal(0.75f, data.GetProperty("selected_float").GetSingle());
            Assert.Equal("UNATTRIBUTED", data.GetProperty("site_id").GetString());
            Assert.Equal("creature:2", data.GetProperty("owner_id").GetString());
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
            if (File.Exists(file + ".incomplete")) File.Delete(file + ".incomplete");
        }
    }

    [Fact]
    public async Task SourceScalarEventKeepsExecutorChronology()
    {
        string path = Path.Combine(Path.GetTempPath(),
            "sts2-rng-scalar-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var recorder = new NativeActionEnvelopeRecorder(
                path, "synthetic-sha", "synthetic-run",
                nativeRngHookInstalled: true,
                nativeRngDoubleHookInstalled: true,
                nativeRngBoolHookInstalled: true);
            var action = new object();
            recorder.Start(action, "RngSourceTestAction",
                JsonSerializer.SerializeToElement(new { hp = 50 }));
            recorder.NativeRngScalarBoundary(new(
                "UNATTRIBUTED", "run.monster_ai", "run", 0,
                "unit_double", 0.125, null, Before, After));
            recorder.Complete(action,
                JsonSerializer.SerializeToElement(new { hp = 50 }));
            recorder.Finish("Combat ended", null);
            await recorder.DisposeAsync();
            var records = File.ReadAllLines(path)
                .Select(x => JsonDocument.Parse(x).RootElement.Clone()).ToArray();
            var header = records[0].GetProperty("data");
            Assert.True(header.GetProperty("native_rng_double_hook_installed").GetBoolean());
            Assert.True(header.GetProperty("native_rng_bool_hook_installed").GetBoolean());
            var observed = Assert.Single(records, x =>
                x.GetProperty("type").GetString() == "native_rng_scalar_boundary");
            Assert.Equal(1, observed.GetProperty("action_id").GetInt64());
            var data = observed.GetProperty("data");
            Assert.Equal("unit_double", data.GetProperty("operation_kind").GetString());
            Assert.Equal(0.125, data.GetProperty("selected_double").GetDouble());
            Assert.Equal("source_primitive_hook", data.GetProperty("origin").GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".incomplete")) File.Delete(path + ".incomplete");
        }
    }
}
