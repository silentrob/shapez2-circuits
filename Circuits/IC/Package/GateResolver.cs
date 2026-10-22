using System.Collections.Generic;
using Game.Content.Features.Signals;
using ShapezShifter.Hijack;

namespace Circuits.IC.Package
{
    // The game's side of a netlist (see IGateResolver): its virtual processors run the game's own shape
    // operations, made as the game makes them for its virtual processor buildings (GameSessionOrchestrator), so a
    // package computes exactly what the same buildings would on its board. One per game session, shared by every
    // package size; constants are read once each.
    public sealed class GateResolver : IGateResolver
    {
        private static GateResolver Current;
        private static SimulationSystemsDependencies CurrentDependencies;

        private readonly ShapeOperations Shapes;
        private readonly Dictionary<string, ISignal> ConstantsRead = new();

        private GateResolver(SimulationSystemsDependencies dependencies)
        {
            GameMode mode = dependencies.Mode;
            Shapes = new ShapeOperations(
                dependencies.ShapeRegistry,
                dependencies.ShapeIdManager,
                mode.ShapesConfiguration,
                mode.ShapeColorScheme,
                mode.ShapesConfiguration.PartCount,
                mode.MaxShapeLayers);
        }

        public static GateResolver For(SimulationSystemsDependencies dependencies)
        {
            if (!ReferenceEquals(dependencies, CurrentDependencies))
            {
                Current = new GateResolver(dependencies);
                CurrentDependencies = dependencies;
            }

            return Current;
        }

        // As BuiltinSimulationSystems.CreateVirtualProcessorSystems pairs each virtual building with its operation.
        public IItemOperation Operation(GateKind kind)
        {
            return kind switch
            {
                GateKind.VirtualRotateCw => Shapes.Rotate[0],
                GateKind.VirtualRotateCcw => Shapes.Rotate[1],
                GateKind.VirtualHalfCut => Shapes.Cut,
                GateKind.VirtualStack => Shapes.Stack,
                GateKind.VirtualUnstack => Shapes.Unstack,
                GateKind.VirtualPaint => Shapes.PaintTopmost,
                GateKind.VirtualCrystallize => Shapes.Crystallize,
                GateKind.VirtualSwapHalves => Shapes.SwapHalves,
                GateKind.VirtualPushPin => Shapes.PushPin,
                GateKind.VirtualAnalyze => Shapes.Analyze,
                _ => null
            };
        }

        public ISignal Constant(string signal)
        {
            lock (ConstantsRead)
            {
                if (!ConstantsRead.TryGetValue(signal, out ISignal value))
                {
                    ConstantsRead.Add(signal, value = SignalBlob.Decode(signal));
                }

                return value;
            }
        }
    }
}
