using System;
using System.Collections.Generic;

namespace Circuits.IC
{
    // Hand-written netlists to load into packages (circuits.load), until boards can be compiled (milestone 3).
    // Two-input circuits take A from the west and B from the north, and output to the east.
    public static class TestCircuits
    {
        public static readonly IReadOnlyDictionary<string, Func<Netlist>> ByName = new Dictionary<string, Func<Netlist>>
        {
            ["half-adder"] = HalfAdder,
            ["and"] = () => TwoInput("AND", GateKind.And),
            ["or"] = () => TwoInput("OR", GateKind.Or),
            ["xor"] = () => TwoInput("XOR", GateKind.Xor),
            ["nand"] = () => TwoInputInverted("NAND", GateKind.And),
            ["nor"] = () => TwoInputInverted("NOR", GateKind.Or),
            ["xnor"] = () => TwoInputInverted("XNOR", GateKind.Xor),
            ["not"] = Not,
            ["buffer"] = Buffer,
            ["mux"] = Mux
        };

        // A in on the west, B in on the north; Sum (A XOR B) out on the east, Carry (A AND B) out on the south.
        public static Netlist HalfAdder()
        {
            const int a = 0, b = 1, sum = 2, carry = 3;
            return new Netlist(
                "Half adder",
                netCount: 4,
                sideRoles: Sides(north: SideRole.Input, east: SideRole.Output, south: SideRole.Output, west: SideRole.Input),
                sideNets: Sides(north: b, east: sum, south: carry, west: a),
                gates: new[]
                {
                    new Gate(GateKind.Xor, sum, a, b),
                    new Gate(GateKind.And, carry, a, b)
                });
        }

        // East is A when Select (south) is off, B when it is on.
        public static Netlist Mux()
        {
            const int a = 0, b = 1, select = 2, notSelect = 3, pickA = 4, pickB = 5, output = 6;
            return new Netlist(
                "Multiplexer",
                netCount: 7,
                sideRoles: Sides(north: SideRole.Input, east: SideRole.Output, south: SideRole.Input, west: SideRole.Input),
                sideNets: Sides(north: b, east: output, south: select, west: a),
                gates: new[]
                {
                    new Gate(GateKind.Not, notSelect, select),
                    new Gate(GateKind.And, pickA, a, notSelect),
                    new Gate(GateKind.And, pickB, b, select),
                    new Gate(GateKind.Or, output, pickA, pickB)
                });
        }

        private static Netlist TwoInput(string name, GateKind kind)
        {
            const int a = 0, b = 1, output = 2;
            return new Netlist(
                name,
                netCount: 3,
                sideRoles: Sides(north: SideRole.Input, east: SideRole.Output, south: SideRole.Unused, west: SideRole.Input),
                sideNets: Sides(north: b, east: output, south: -1, west: a),
                gates: new[] { new Gate(kind, output, a, b) });
        }

        private static Netlist TwoInputInverted(string name, GateKind kind)
        {
            const int a = 0, b = 1, inner = 2, output = 3;
            return new Netlist(
                name,
                netCount: 4,
                sideRoles: Sides(north: SideRole.Input, east: SideRole.Output, south: SideRole.Unused, west: SideRole.Input),
                sideNets: Sides(north: b, east: output, south: -1, west: a),
                gates: new[] { new Gate(kind, inner, a, b), new Gate(GateKind.Not, output, inner) });
        }

        private static Netlist Not()
        {
            return new Netlist(
                "NOT",
                netCount: 2,
                sideRoles: Sides(north: SideRole.Unused, east: SideRole.Output, south: SideRole.Unused, west: SideRole.Input),
                sideNets: Sides(north: -1, east: 1, south: -1, west: 0),
                gates: new[] { new Gate(GateKind.Not, 1, 0) });
        }

        // West straight through to east, with no gate: the signal passes unchanged, shapes and colors included.
        private static Netlist Buffer()
        {
            return new Netlist(
                "Buffer",
                netCount: 1,
                sideRoles: Sides(north: SideRole.Unused, east: SideRole.Output, south: SideRole.Unused, west: SideRole.Input),
                sideNets: Sides(north: -1, east: 0, south: -1, west: 0),
                gates: Array.Empty<Gate>());
        }

        private static T[] Sides<T>(T north, T east, T south, T west)
        {
            return new[] { north, east, south, west };
        }
    }
}
