using System;

namespace Circuits.IC
{
    // A package side, indexed in this order everywhere a netlist or package refers to sides.
    public enum Side { North, East, South, West }

    public enum SideRole { Unused, Input, Output }

    public enum GateKind { And, Or, Xor, Not }

    public sealed class Gate
    {
        public readonly GateKind Kind;
        public readonly int[] Inputs;
        public readonly int Output;

        public Gate(GateKind kind, int output, params int[] inputs)
        {
            Kind = kind;
            Output = output;
            Inputs = inputs;
        }
    }

    // The compiled form of a circuit: nets are numbered 0..NetCount-1, each side is bound to a net or unused,
    // and gates are listed in evaluation order (every gate after the gates driving its inputs).
    public sealed class Netlist
    {
        public const int SideCount = 4;

        // What a package runs before a circuit is loaded: every side unused, so it drives nothing.
        public static readonly Netlist Empty = new(
            "Empty",
            netCount: 0,
            sideRoles: new[] { SideRole.Unused, SideRole.Unused, SideRole.Unused, SideRole.Unused },
            sideNets: new[] { -1, -1, -1, -1 },
            gates: Array.Empty<Gate>());

        public readonly string Name;
        public readonly int NetCount;
        public readonly SideRole[] SideRoles;
        public readonly int[] SideNets;
        public readonly Gate[] Gates;

        public Netlist(string name, int netCount, SideRole[] sideRoles, int[] sideNets, Gate[] gates)
        {
            if (sideRoles.Length != SideCount || sideNets.Length != SideCount)
            {
                throw new ArgumentException($"A netlist binds exactly {SideCount} sides");
            }

            Name = name;
            NetCount = netCount;
            SideRoles = sideRoles;
            SideNets = sideNets;
            Gates = gates;
        }
    }
}
