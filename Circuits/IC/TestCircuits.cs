namespace Circuits.IC
{
    // Hand-written netlists, until boards can be compiled (milestone 3).
    public static class TestCircuits
    {
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

        private static T[] Sides<T>(T north, T east, T south, T west)
        {
            return new[] { north, east, south, west };
        }
    }
}
