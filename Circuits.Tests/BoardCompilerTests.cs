using System.Collections.Generic;
using System.Linq;
using Circuits.IC;
using Circuits.IC.Compilation;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    // Boards are laid out on a small grid. North is towards smaller y. Every part is one tile.
    public class BoardCompilerTests
    {
        private static readonly ISignal Nothing = NullSignal.Instance;

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(0, 1, 1, 0)]
        [InlineData(1, 0, 1, 0)]
        [InlineData(1, 1, 0, 1)]
        public void AHalfAdderBoardCompilesToAWorkingHalfAdder(int a, int b, int sum, int carry)
        {
            // A runs down column 1 into both gates' west inputs; B runs down column 3 into their east inputs.
            // The XOR outputs north to Sum, the AND south to Carry.
            //
            //   y=0        .      Sum↑    B↓
            //   y=1   A→   ┬      XOR     ┬
            //   y=2        └      AND     ┘
            //   y=3        .      Carry↓
            var parts = new List<Part>
            {
                Pin(PartKind.InputPin, Side.West, 0, 1, Facing.East),
                Pin(PartKind.InputPin, Side.North, 3, 0, Facing.South),
                Wire(1, 1, Facing.West, Facing.East, Facing.South),
                Wire(1, 2, Facing.North, Facing.East),
                Wire(3, 1, Facing.North, Facing.West, Facing.South),
                Wire(3, 2, Facing.North, Facing.West),
                Gate(PartKind.Xor, 2, 1, outFacing: Facing.North, Facing.West, Facing.East),
                Gate(PartKind.And, 2, 2, outFacing: Facing.South, Facing.West, Facing.East),
                Pin(PartKind.OutputPin, Side.East, 2, 0, Facing.South),
                Pin(PartKind.OutputPin, Side.South, 2, 3, Facing.North)
            };

            ISignal[] leaving = Run(Compiled(parts), north: Bit(b), west: Bit(a));

            Assert.Equal(Bit(sum), leaving[(int)Side.East]);
            Assert.Equal(Bit(carry), leaving[(int)Side.South]);
        }

        [Fact]
        public void PinsBindToTheirSidesAndGatesToTheirNets()
        {
            // Input pin on the west, facing east into a NOT, whose output faces east into an output pin.
            var parts = new List<Part>
            {
                Pin(PartKind.InputPin, Side.West, 0, 0, Facing.East),
                Gate(PartKind.Not, 1, 0, outFacing: Facing.East, Facing.West),
                Pin(PartKind.OutputPin, Side.East, 2, 0, Facing.West)
            };

            Netlist netlist = Compiled(parts);

            Assert.Equal(Bit(0), Run(netlist, west: Bit(1))[(int)Side.East]);
            Assert.Equal(Bit(1), Run(netlist, west: Nothing)[(int)Side.East]);
        }

        [Fact]
        public void WiresCarryASignalAcrossTheBoard()
        {
            var parts = new List<Part>
            {
                Pin(PartKind.InputPin, Side.West, 0, 0, Facing.East),
                Wire(1, 0, Facing.West, Facing.East),
                Wire(2, 0, Facing.West, Facing.South),
                Wire(2, 1, Facing.North, Facing.East),
                Pin(PartKind.OutputPin, Side.East, 3, 1, Facing.West)
            };

            Assert.Equal(Bit(1), Run(Compiled(parts), west: Bit(1))[(int)Side.East]);
        }

        [Fact]
        public void ABridgeKeepsItsTwoCrossingWiresApart()
        {
            // West feeds east through the bridge; north feeds south through it. They must not merge.
            var parts = new List<Part>
            {
                Pin(PartKind.InputPin, Side.West, 0, 1, Facing.East),
                Pin(PartKind.InputPin, Side.North, 1, 0, Facing.South),
                Bridge(1, 1),
                Pin(PartKind.OutputPin, Side.East, 2, 1, Facing.West),
                Pin(PartKind.OutputPin, Side.South, 1, 2, Facing.North)
            };

            ISignal[] leaving = Run(Compiled(parts), north: Bit(0), west: Bit(1));

            Assert.Equal(Bit(1), leaving[(int)Side.East]);
            Assert.Equal(Bit(0), leaving[(int)Side.South]);
        }

        [Fact]
        public void InputsFacingInputsDoNotConnect()
        {
            // The output pin's input faces the NOT's input, not its output: nothing reaches the pin.
            var parts = new List<Part>
            {
                Pin(PartKind.InputPin, Side.West, 0, 0, Facing.East),
                Gate(PartKind.Not, 1, 0, outFacing: Facing.West, Facing.East),
                Pin(PartKind.OutputPin, Side.East, 2, 0, Facing.West)
            };

            Assert.Same(Nothing, Run(Compiled(parts), west: Bit(1))[(int)Side.East]);
        }

        [Fact]
        public void GatesAreOrderedSoChainsSettleInOneTick()
        {
            // Listed output-first, so the compiler has to reorder: west → NOT → NOT → east.
            var parts = new List<Part>
            {
                Pin(PartKind.OutputPin, Side.East, 3, 0, Facing.West),
                Gate(PartKind.Not, 2, 0, outFacing: Facing.East, Facing.West),
                Gate(PartKind.Not, 1, 0, outFacing: Facing.East, Facing.West),
                Pin(PartKind.InputPin, Side.West, 0, 0, Facing.East)
            };

            Assert.Equal(Bit(1), Run(Compiled(parts), west: Bit(1))[(int)Side.East]);
        }

        [Fact]
        public void TransistorPassesItsInputWhileTheGateIsOn()
        {
            // Input from the west, gate from the north, output east.
            var parts = new List<Part>
            {
                Pin(PartKind.InputPin, Side.West, 0, 1, Facing.East),
                Pin(PartKind.InputPin, Side.North, 1, 0, Facing.South),
                Gate(PartKind.Transistor, 1, 1, outFacing: Facing.East, Facing.West, Facing.North),
                Pin(PartKind.OutputPin, Side.East, 2, 1, Facing.West)
            };
            Netlist netlist = Compiled(parts);

            Assert.Equal(Bit(1), Run(netlist, north: Bit(1), west: Bit(1))[(int)Side.East]);
            Assert.Same(Nothing, Run(netlist, north: Bit(0), west: Bit(1))[(int)Side.East]);
        }

        [Fact]
        public void ConstantsDriveTheirNet()
        {
            var parts = new List<Part>
            {
                new Part(PartKind.Constant, "Constant", new[] { new Port(new Pivot(1, 0, 0, Facing.East), PortKind.Out) }, value: 7),
                Pin(PartKind.OutputPin, Side.East, 2, 0, Facing.West)
            };

            Assert.Equal(IntegerSignal.Get(7), Run(Compiled(parts))[(int)Side.East]);
        }

        [Fact]
        public void TwoPinsOnOneSideAreRefused()
        {
            var parts = new List<Part>
            {
                Pin(PartKind.InputPin, Side.West, 0, 0, Facing.East),
                Pin(PartKind.InputPin, Side.West, 0, 3, Facing.East)
            };

            CompileResult result = BoardCompiler.Compile("Board", parts);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Contains("both on the West edge"));
        }

        [Fact]
        public void ABoardWithoutPinsIsRefused()
        {
            CompileResult result = BoardCompiler.Compile("Board", new[] { Wire(0, 0, Facing.West, Facing.East) });

            Assert.False(result.Succeeded);
        }

        [Fact]
        public void FeedbackLoopsAreRefusedForNow()
        {
            // Two NOTs in a ring: (1,0) feeds (2,0), whose output runs south and back round through wires into
            // (1,0)'s input.
            var parts = new List<Part>
            {
                Gate(PartKind.Not, 1, 0, outFacing: Facing.East, Facing.West),
                Gate(PartKind.Not, 2, 0, outFacing: Facing.South, Facing.West),
                Wire(2, 1, Facing.North, Facing.West),
                Wire(1, 1, Facing.East, Facing.West),
                Wire(0, 1, Facing.East, Facing.North),
                Wire(0, 0, Facing.South, Facing.East),
                Pin(PartKind.OutputPin, Side.East, 5, 5, Facing.West)
            };

            CompileResult result = BoardCompiler.Compile("Board", parts);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Contains("Feedback loops"));
        }

        private static Part Pin(PartKind kind, Side side, int x, int y, Facing facing)
        {
            PortKind port = kind == PartKind.InputPin ? PortKind.Out : PortKind.In;
            return new Part(kind, $"{kind} at ({x},{y})", new[] { new Port(new Pivot(x, y, 0, facing), port) }, side);
        }

        private static Part Wire(int x, int y, params Facing[] ends)
        {
            return new Part(
                PartKind.Wire,
                $"Wire at ({x},{y})",
                ends.Select(end => new Port(new Pivot(x, y, 0, end), PortKind.Junction)).ToArray());
        }

        private static Part Bridge(int x, int y)
        {
            return new Part(
                PartKind.Wire,
                $"Bridge at ({x},{y})",
                new[]
                {
                    new Port(new Pivot(x, y, 0, Facing.West), PortKind.Junction, group: 0),
                    new Port(new Pivot(x, y, 0, Facing.East), PortKind.Junction, group: 0),
                    new Port(new Pivot(x, y, 0, Facing.North), PortKind.Junction, group: 1),
                    new Port(new Pivot(x, y, 0, Facing.South), PortKind.Junction, group: 1)
                });
        }

        private static Part Gate(PartKind kind, int x, int y, Facing outFacing, params Facing[] inputs)
        {
            Port[] ports = inputs.Select(input => new Port(new Pivot(x, y, 0, input), PortKind.In))
               .Append(new Port(new Pivot(x, y, 0, outFacing), PortKind.Out))
               .ToArray();
            return new Part(kind, $"{kind} at ({x},{y})", ports);
        }

        private static Netlist Compiled(IReadOnlyList<Part> parts)
        {
            CompileResult result = BoardCompiler.Compile("Board", parts);
            Assert.Empty(result.Errors);
            return result.Netlist;
        }

        private static ISignal Bit(int value)
        {
            return IntegerSignal.Get(value != 0);
        }

        private static ISignal[] Run(Netlist netlist, ISignal north = null, ISignal west = null)
        {
            ISignal[] arriving = { north ?? Nothing, Nothing, Nothing, west ?? Nothing };
            var leaving = new ISignal[Netlist.SideCount];
            new NetlistEvaluator(netlist).Evaluate(arriving, leaving);
            return leaving;
        }
    }
}
