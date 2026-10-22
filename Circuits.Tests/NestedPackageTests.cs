using System.Collections.Generic;
using Circuits.IC;
using Circuits.IC.Compilation;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    // A chip on a board is compiled in: its firmware's gates join the board's.
    public class NestedPackageTests
    {
        private static readonly ISignal Nothing = NullSignal.Instance;

        // NAND as a chip: A on the west, B on the north, out on the east.
        private static Netlist Nand()
        {
            return new Netlist(
                "NAND",
                netCount: 4,
                sideRoles: new[] { SideRole.Input, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { 1, 3, -1, 0 },
                gates: new[] { new Gate(GateKind.And, 2, 0, 1), new Gate(GateKind.Not, 3, 2) });
        }

        // Board: West pin -> chip's west; North pin -> chip's north; chip's east -> NOT -> East pin. So East is
        // A AND B, through a NAND chip and a NOT.
        private static List<Part> Board()
        {
            return new List<Part>
            {
                new(PartKind.InputPin, "west", new[] { new Port(new Pivot(0, 1, 0, Facing.East), PortKind.Out) }, Side.West),
                new(PartKind.InputPin, "north", new[] { new Port(new Pivot(1, 0, 0, Facing.South), PortKind.Out) }, Side.North),
                new("chip", new[]
                {
                    new Port(new Pivot(1, 1, 0, Facing.West), PortKind.In),
                    new Port(new Pivot(1, 1, 0, Facing.North), PortKind.In),
                    new Port(new Pivot(1, 1, 0, Facing.East), PortKind.Out)
                }, Nand(), new[] { (int)Side.West, (int)Side.North, (int)Side.East }),
                new(PartKind.Not, "not", new[]
                {
                    new Port(new Pivot(2, 1, 0, Facing.West), PortKind.In),
                    new Port(new Pivot(2, 1, 0, Facing.East), PortKind.Out)
                }),
                new(PartKind.OutputPin, "east", new[] { new Port(new Pivot(3, 1, 0, Facing.West), PortKind.In) }, Side.East)
            };
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(0, 1, 0)]
        [InlineData(1, 0, 0)]
        [InlineData(1, 1, 1)]
        public void AChipOnABoardRunsAsPartOfIt(int a, int b, int and)
        {
            CompileResult result = BoardCompiler.Compile("AND from NAND", Board());

            Assert.Empty(result.Errors);
            ISignal[] arriving = { IntegerSignal.Get(b != 0), Nothing, Nothing, IntegerSignal.Get(a != 0) };
            var leaving = new ISignal[Netlist.SideCount];
            new NetlistEvaluator(result.Netlist).Evaluate(arriving, leaving);
            Assert.Equal(IntegerSignal.Get(and != 0), leaving[(int)Side.East]);
        }

        [Fact]
        public void TheChipsOwnNetsGetNumbersOfTheirOwn()
        {
            Netlist netlist = BoardCompiler.Compile("AND from NAND", Board()).Netlist;

            // The board's nets (west, north, chip's east, east) plus the NAND's inner AND result.
            Assert.Equal(5, netlist.NetCount);
            Assert.Equal(3, netlist.Gates.Length);
        }
    }
}
