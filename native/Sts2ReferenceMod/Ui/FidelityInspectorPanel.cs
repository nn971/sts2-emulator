#if STS2_NATIVE
using System.Text.Json;
using Godot;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Trace;

namespace Sts2ReferenceMod.Ui;

/// <summary>
/// Optional read-only in-game timeline. Native capture and the durable JSONL
/// journal remain the authority; UI readers maintain independent cursors.
/// F8 toggles visibility. Controlled stepping awaits validated action dispatch.
/// </summary>
public sealed partial class FidelityInspectorPanel : CanvasLayer
{
    private const int MaxShownDecisions = 250;
    private NativeRecordingSession? _session;
    private string? _file;
    private int _cursor;
    private int _lastCount = -1;
    private long _lastEventSeq = -1;
    private PanelContainer? _panel;
    private Label? _status;
    private Label? _decision;
    private TextEdit? _content;
    private Button? _back;
    private Button? _forward;
    private string _sourceStatus = "RNG source hook: UNKNOWN";
    private string _agentStatus = "Agent spectator: DISABLED";

    public override void _Ready()
    {
        Layer = 90;
        Visible = false;

        _panel = new PanelContainer
        {
            Position = new Vector2(16, 18),
            CustomMinimumSize = new Vector2(580, 395)
        };
        AddChild(_panel);
        var column = new VBoxContainer();
        _panel.AddChild(column);
        var title = new Label
        {
            Text = "STS2 Native Fidelity Inspector — passive reference"
        };
        column.AddChild(title);
        _status = new Label { Text = "Native-only; parity comparison pending." };
        column.AddChild(_status);

        var buttons = new HBoxContainer();
        column.AddChild(buttons);
        _back = new Button { Text = "◀ Previous" };
        _forward = new Button { Text = "Next ▶" };
        var export = new Button { Text = "Export diagnostic" };
        var hide = new Button { Text = "Hide (F8)" };
        buttons.AddChild(_back);
        buttons.AddChild(_forward);
        buttons.AddChild(export);
        buttons.AddChild(hide);
        _back.Pressed += () => { _cursor = Math.Max(0, _cursor - 1); Refresh(); };
        _forward.Pressed += () => { _cursor++; Refresh(); };
        export.Pressed += ExportDiagnostic;
        hide.Pressed += () => Visible = false;

        _decision = new Label { Text = "No decisions yet" };
        column.AddChild(_decision);
        _content = new TextEdit
        {
            Editable = false,
            CustomMinimumSize = new Vector2(550, 300),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        column.AddChild(_content);
        column.AddChild(new Label
        {
            Text = "Human-play observer: game action dispatch remains unmodified."
        });
    }

    public void UseSession(NativeRecordingSession session, string path)
    {
        _session = session;
        _file = path;
        _cursor = 0;
        _lastCount = -1;
        _lastEventSeq = -1;
        Refresh();
    }

    public void SetCaptureCapabilities(bool integerHook, bool floatHook,
        bool spectatorEnabled)
    {
        _sourceStatus = "RNG native source: int " +
            (integerHook ? "HOOKED" : "UNAVAILABLE") + " / float " +
            (floatHook ? "HOOKED" : "UNAVAILABLE") +
            " (parity verification offline)";
        _agentStatus = "Agent spectator: " +
            (spectatorEnabled ? "READ-ONLY LOCAL FEED" : "DISABLED") +
            " / action injection DISABLED";
        if (Visible) Refresh();
    }

    public void Toggle() { Visible = !Visible; if (Visible) Refresh(); }

    public void Tick()
    {
        if (!Visible || _session is null) return;
        int count = _session.RecordedDecisions.Count;
        long seq = _session.Journal.LastSequence;
        if (count != _lastCount || seq != _lastEventSeq)
        {
            bool followTail = _cursor == Math.Max(0, _lastCount - 1);
            if (followTail) _cursor = Math.Max(0, count - 1);
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_status is null || _content is null || _decision is null
            || _back is null || _forward is null) return;

        var decisions = _session?.RecordedDecisions;
        int count = decisions?.Count ?? 0;
        _lastCount = count;
        _lastEventSeq = _session?.Journal.LastSequence ?? 0;
        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, count - 1));
        _back.Disabled = _cursor == 0;
        _forward.Disabled = count == 0 || _cursor == count - 1;
        _status.Text = (_session?.IsIncomplete == true ? "INCOMPLETE_CAPTURE" : "Native-only capture")
            + " · " + _sourceStatus + " · " + _agentStatus + " · parity NOT_TESTED";
        if (count == 0)
        {
            _decision.Text = "Awaiting first stable native combat decision";
            _content.Text = "The capture adapter records human choices without injecting actions.";
            return;
        }

        var selected = decisions![_cursor];
        _decision.Text = "Decision " + selected.DecisionId + " (" + (_cursor + 1) +
            "/" + count + ") · " + selected.Phase;
        var events = _session!.Journal.ReadAfter(selected.EventSeq, 8192)
            .Where(e => e.DecisionId == selected.DecisionId)
            .Take(128)
            .Select(e => new
            {
                seq = e.EventSeq, kind = e.Kind, actor = e.ActorId,
                target = e.TargetId, payload = e.Payload
            }).ToArray();
        string full = JsonSerializer.Serialize(new
        {
            decision_id = selected.DecisionId,
            state_hash = selected.StateHash,
            observation_revision = selected.ObservationRevision,
            legal_actions = selected.LegalActions,
            coverage = selected.Coverage,
            state = selected.State,
            events
        }, new JsonSerializerOptions { WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        _content.Text = full;
    }

    private void ExportDiagnostic()
    {
        if (_session is null || _file is null || _status is null)
            return;
        // Build the export snapshot on the Godot main thread; disk IO runs
        // separately. The trace itself is already persisted by AsyncTraceWriter.
        var snapshot = JsonSerializer.Serialize(new
        {
            format = "sts2_inspector_native_diagnostic_v1",
            source_trace = _file,
            decisions = _session.RecordedDecisions.TakeLast(MaxShownDecisions).ToArray(),
            events = _session.Journal.ReadAfter(
                Math.Max(0, _session.Journal.LastSequence - 4096), 4096),
            completeness = _session.IsIncomplete ? "INCOMPLETE_CAPTURE" : "ONGOING",
            parity = "NOT_TESTED",
            rng = "RNG_UNAUDITED"
        }, new JsonSerializerOptions { WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        string path = _file + ".inspection.json";
        _ = Task.Run(async () =>
        {
            try { await File.WriteAllTextAsync(path, snapshot).ConfigureAwait(false); }
            catch (Exception error)
            {
                Console.Error.WriteLine("[Sts2ReferenceMod] Diagnostic export failed: " + error);
            }
        });
        _status.Text = "Export requested: " + path;
    }
}
#endif
