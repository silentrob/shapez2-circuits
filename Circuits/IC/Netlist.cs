using System;
using System.Linq;

namespace Circuits.IC
{
    // A package edge. On a 1x1 package each edge is one connection, numbered in this order (see PackageShape).
    public enum Side { North, East, South, West }

    public enum SideRole { Unused, Input, Output }

    // Never reorder or remove: kinds are saved by number. Transistor passes its first input while its second is
    // truthy; Constant has no inputs and outputs its Value as an IntegerSignal, or its Signal (any other value,
    // serialized by the game) when it has one. The Virtual kinds are the vanilla virtual shape processors, run
    // with the game's own shape operations (see IGateResolver), inputs and outputs in their connector order.
    // Latch is the Circuits latch (inputs Data, Reset): it keeps a value between ticks (see LatchRule).
    public enum GateKind
    {
        And,
        Or,
        Xor,
        Not,
        Transistor,
        Constant,
        VirtualRotateCw,
        VirtualRotateCcw,
        VirtualHalfCut,
        VirtualStack,
        VirtualUnstack,
        VirtualPaint,
        VirtualCrystallize,
        VirtualSwapHalves,
        VirtualPushPin,
        VirtualAnalyze,
        Latch
    }

    public sealed class Gate
    {
        public readonly GateKind Kind;
        public readonly int[] Inputs;
        public readonly int[] Outputs;
        public readonly int Value;
        public readonly string Signal;

        public int Output => Outputs[0];

        public Gate(GateKind kind, int output, params int[] inputs)
            : this(kind, new[] { output }, inputs) { }

        public Gate(GateKind kind, int[] outputs, int[] inputs)
        {
            Kind = kind;
            Outputs = outputs;
            Inputs = inputs;
        }

        public static Gate Constant(int output, int value)
        {
            return new Gate(output, value, null);
        }

        public static Gate Constant(int output, string signal)
        {
            return new Gate(output, 0, signal);
        }

        private Gate(int output, int value, string signal)
        {
            Kind = GateKind.Constant;
            Outputs = new[] { output };
            Inputs = Array.Empty<int>();
            Value = value;
            Signal = signal;
        }

        public static int InputCount(GateKind kind)
        {
            return kind switch
            {
                GateKind.Constant => 0,
                GateKind.Not or GateKind.VirtualRotateCw or GateKind.VirtualRotateCcw or GateKind.VirtualHalfCut
                    or GateKind.VirtualUnstack or GateKind.VirtualPushPin or GateKind.VirtualAnalyze => 1,
                _ => 2
            };
        }

        public static int OutputCount(GateKind kind)
        {
            return kind is GateKind.VirtualUnstack or GateKind.VirtualSwapHalves or GateKind.VirtualAnalyze ? 2 : 1;
        }

        public static bool IsVirtual(GateKind kind)
        {
            return kind >= GateKind.VirtualRotateCw && kind <= GateKind.VirtualAnalyze;
        }
    }

    // The compiled form of a circuit, for a package of one shape: nets are numbered 0..NetCount-1, each of the
    // shape's connections (the "sides" below) is bound to a net or unused, and gates are listed in evaluation
    // order (every gate after the gates driving its inputs).
    public sealed class Netlist
    {
        // How many gates keep a value between ticks (latches), which a package saves.
        public int Latches => Gates.Count(gate => gate.Kind == GateKind.Latch);

        // A 1x1 package's connections.
        public const int SideCount = 4;

        public const string EmptyName = "Empty";

        // What a 1x1 package runs before a circuit is loaded: every side unused, so it drives nothing.
        public static readonly Netlist Empty = EmptyOf(PackageShape.Single);

        public readonly string Name;
        public readonly PackageShape Shape;
        public readonly int NetCount;
        public readonly SideRole[] SideRoles;
        public readonly int[] SideNets;
        public readonly Gate[] Gates;

        public int Connections => Shape.Connections;

        public Netlist(string name, int netCount, SideRole[] sideRoles, int[] sideNets, Gate[] gates)
            : this(name, PackageShape.Single, netCount, sideRoles, sideNets, gates)
        {
        }

        public Netlist(string name, PackageShape shape, int netCount, SideRole[] sideRoles, int[] sideNets, Gate[] gates)
        {
            if (sideRoles.Length != shape.Connections || sideNets.Length != shape.Connections)
            {
                throw new ArgumentException($"A {shape} netlist binds exactly {shape.Connections} connections");
            }

            Name = name;
            Shape = shape;
            NetCount = netCount;
            SideRoles = sideRoles;
            SideNets = sideNets;
            Gates = gates;
        }

        public static Netlist EmptyOf(PackageShape shape)
        {
            return new Netlist(
                EmptyName,
                shape,
                netCount: 0,
                sideRoles: new SideRole[shape.Connections],
                sideNets: Enumerable.Repeat(-1, shape.Connections).ToArray(),
                gates: Array.Empty<Gate>());
        }

        // "West in, East out" ("West 1 in, East 2 out" on larger packages): what each used connection does.
        public string DescribeSides()
        {
            string[] sides = Enumerable.Range(0, Connections)
               .Where(side => SideRoles[side] != SideRole.Unused)
               .Select(side => $"{Shape.Name(side)} {(SideRoles[side] == SideRole.Input ? "in" : "out")}")
               .ToArray();
            return sides.Length == 0 ? "no connections" : string.Join(", ", sides);
        }
    }
}
