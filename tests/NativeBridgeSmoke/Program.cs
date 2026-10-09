using System.Collections;
using System.Reflection;

namespace NativeBridgeSmoke
{
    internal static class Program
    {
        private static readonly Type Recorder =
            typeof(Sts2ReferenceBridge.ReferenceBridgeMod).Assembly.GetType(
                "Sts2ReferenceBridge.PassiveReferenceRecorder", throwOnError: true)!;

        private static object? Call(string method, params object?[] arguments)
        {
            var target = Recorder.GetMethod(
                method, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new Exception("Missing recorder method " + method);
            return target.Invoke(null, arguments);
        }

        private static void Set(string field, object? value)
        {
            var target = Recorder.GetField(
                field, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new Exception("Missing recorder field " + field);
            target.SetValue(null, value);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        public static void Main()
        {
            // ModelId is a reference type, not a primitive. Ensure
            // compact snapshot identity is not reduced to its type.
            var id = new MegaCrit.Sts2.Core.Models.ModelId(
                "CARD", "SILENT_STRIKE");
            Check(Equals(Call("SummarizeOpaque", id), "CARD.SILENT_STRIKE"),
                "Native ModelId projection lost source identity");

            // The native EventModel lazily initializes CurrentOptions,
            // which must never be invoked by a passive recorder.
            var evt = new MockEvent();
            var nativeEvent = (MockEventBase)evt;
            var field = typeof(MockEventBase).GetField(
                "_currentOptions",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            field.SetValue(nativeEvent, new List<MockOption>
            {
                new() { TextKey = "MOCK.options.SAFE" }
            });

            var run = new MockRun();
            var manager = new MockManager
            {
                EventSynchronizer = new MockEventSync
                {
                    Events = [evt]
                },
                Run = run
            };
            Set("_runManager", manager);
            Set("_corpusMode", "underdocks-silent-act1");

            var options = (object[])Call(
                "CaptureActiveEventOptions", manager)!;
            Check(options.Length == 1, "No mock event observed");
            var projectedEvent = (Dictionary<string, object?>)options[0];
            Check(Equals(projectedEvent["id"], "EVENT.MOCK"),
                "Event identity absent");
            var projectedOptions = (object[])projectedEvent["options"]!;
            Check(projectedOptions.Length == 1,
                "Derived event lost private base-class options");
            var projectedOption =
                (Dictionary<string, object?>)projectedOptions[0];
            Check(Equals(projectedOption["text_key"], "MOCK.options.SAFE"),
                "Event option key absent");

            var snapshot = (Dictionary<string, object?>)Call(
                "CaptureRunObservation", "run_manager.RunStarted")!;
            Check(Equals(snapshot["run_seed"], "mock-seed"),
                "Run seed was not read from RunRngSet");
            var room = (Dictionary<string, object?>)snapshot["current_room"]!;
            Check(Equals(room["model_id"], "EVENT.MOCK"),
                "Room model identity missing");
            Check(Equals(room["room_type"], "Event"),
                "Room category missing");
            var map = (Dictionary<string, object?>)snapshot["map"]!;
            Check(((object[])map["points"]!).Length == 1,
                "Map topology missing");
            Check(snapshot["current_map_coord"] is Dictionary<string, object?>,
                "Current map coordinate missing");
            Check(!((Dictionary<string, object?>)snapshot["current_map_coord"]!)
                .ContainsKey("not_a_coordinate"),
                "Unexpected coordinate projection");
            Console.WriteLine(
                "Native bridge compile and source-reflection smoke: PASS");
        }
    }

    public sealed class MockOption
    {
        public string TextKey { get; set; } = "";
        public bool IsLocked => false;
        public bool IsProceed => false;
        public bool WasChosen => false;
    }

    public abstract class MockEventBase
    {
        private List<MockOption>? _currentOptions;
        public IReadOnlyList<MockOption> CurrentOptions =>
            throw new InvalidOperationException(
                "Observer must not call the lazy CurrentOptions getter");
        public MegaCrit.Sts2.Core.Models.ModelId Id { get; } =
            new("EVENT", "MOCK");
        public bool IsFinished => false;
        public string LayoutType => "Default";
        public MockRng Rng { get; } = new();
        public event Action<MockEventBase>? StateChanged;
        public event Action? EnteringEventCombat;
        public void Trigger()
        {
            StateChanged?.Invoke(this);
            EnteringEventCombat?.Invoke();
        }
    }

    public sealed class MockEvent : MockEventBase { }

    public sealed class MockEventSync
    {
        public object[] Events { get; set; } = [];
    }

    public sealed class MockManager
    {
        public MockEventSync EventSynchronizer { get; set; } = new();
        public MockRun Run { get; set; } = new();
        public MockRun DebugOnlyGetState() => Run;
    }

    public sealed class MockRng
    {
        public object ToSerializable() =>
            new { State = 21UL, Counter = 1UL };
    }

    public sealed class MockRngSet
    {
        public string StringSeed => "mock-seed";
        public object ToSerializable() => new
        {
            Seed = "mock-seed",
            Rngs = new Dictionary<string, object>()
        };
    }

    public struct MockCoord
    {
        public int col;
        public int row;
        public MockCoord(int c, int r)
        {
            col = c;
            row = r;
        }
    }

    public sealed class MockPoint
    {
        public string PointType => "Event";
        public bool CanBeModified => true;
        public MockCoord coord = new(1, 1);
        public HashSet<MockPoint> Children { get; } = [];
    }

    public sealed class MockMap
    {
        private readonly MockPoint point = new();
        public MockPoint StartingMapPoint => point;
        public MockPoint BossMapPoint => point;
        public MockPoint? SecondBossMapPoint => null;
        public int GetColumnCount() => 7;
        public int GetRowCount() => 15;
        public IEnumerable<MockPoint> GetAllMapPoints()
        {
            yield return point;
        }
    }

    public sealed class MockRoom
    {
        public int Id => 8;
        public string RoomType => "Event";
        public MegaCrit.Sts2.Core.Models.ModelId ModelId { get; } =
            new("EVENT", "MOCK");
    }

    public sealed class MockRun
    {
        public int CurrentActIndex => 0;
        public int ActFloor => 1;
        public int TotalFloor => 1;
        public int AscensionLevel => 0;
        public string GameMode => "Standard";
        public int CurrentRoomCount => 1;
        public MockCoord CurrentMapCoord => new(1, 1);
        public MockRoom CurrentRoom { get; } = new();
        public MockRoom BaseRoom => CurrentRoom;
        public MockMap Map { get; } = new();
        public MockPoint CurrentMapPoint => Map.StartingMapPoint;
        public MockRngSet Rng { get; } = new();
        public object[] Players => [];
        public object? CurrentMapPointHistoryEntry => null;
        public object[] VisitedEventIds => [];
        public object[] VisitedMapCoords => [];
    }
}

namespace MegaCrit.Sts2.Core.Models
{
    public sealed record ModelId(string Category, string Entry)
    {
        public override string ToString() => Category + "." + Entry;
    }
}

namespace MegaCrit.Sts2.Core.Modding
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ModInitializerAttribute(string initializer)
        : Attribute
    {
        public string Initializer => initializer;
    }
}

namespace Godot
{
    public class Node { }
}
