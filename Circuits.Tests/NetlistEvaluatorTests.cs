using Circuits.IC;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    public class NetlistEvaluatorTests
    {
        private static readonly ISignal Nothing = NullSignal.Instance;

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(0, 1, 1, 0)]
        [InlineData(1, 0, 1, 0)]
        [InlineData(1, 1, 0, 1)]
        public void HalfAdderMatchesItsTruthTable(int a, int b, int sum, int carry)
        {
            ISignal[] leaving = Run(TestCircuits.HalfAdder(), north: Bit(b), west: Bit(a));

            Assert.Equal(Bit(sum), leaving[(int)Side.East]);
            Assert.Equal(Bit(carry), leaving[(int)Side.South]);
        }

        [Fact]
        public void InputSidesDriveNothing()
        {
            ISignal[] leaving = Run(TestCircuits.HalfAdder(), north: Bit(1), west: Bit(1));

            Assert.Same(Nothing, leaving[(int)Side.North]);
            Assert.Same(Nothing, leaving[(int)Side.West]);
        }

        [Fact]
        public void MissingInputsAreFalsyLikeVanilla()
        {
            ISignal[] leaving = Run(TestCircuits.HalfAdder(), north: Nothing, west: Bit(1));

            Assert.Equal(Bit(1), leaving[(int)Side.East]);
            Assert.Equal(Bit(0), leaving[(int)Side.South]);
        }

        [Fact]
        public void DifferentValuesOnOneNetMakeAConflict()
        {
            // North and west both drive net 0, which goes out to the east.
            var netlist = new Netlist(
                "Two drivers",
                netCount: 1,
                sideRoles: new[] { SideRole.Input, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { 0, 0, -1, 0 },
                gates: new Gate[0]);

            Assert.IsType<ConflictSignal>(Run(netlist, north: Bit(1), west: Bit(0))[(int)Side.East]);
            Assert.Equal(Bit(1), Run(netlist, north: Bit(1), west: Bit(1))[(int)Side.East]);
            Assert.Equal(Bit(1), Run(netlist, north: Bit(1), west: Nothing)[(int)Side.East]);
        }

        [Theory]
        [InlineData("and", 0, 0, 0)]
        [InlineData("and", 1, 1, 1)]
        [InlineData("and", 1, 0, 0)]
        [InlineData("or", 0, 0, 0)]
        [InlineData("or", 0, 1, 1)]
        [InlineData("xor", 1, 1, 0)]
        [InlineData("xor", 1, 0, 1)]
        [InlineData("nand", 1, 1, 0)]
        [InlineData("nand", 0, 1, 1)]
        [InlineData("nand", 0, 0, 1)]
        [InlineData("nor", 0, 0, 1)]
        [InlineData("nor", 1, 0, 0)]
        [InlineData("xnor", 1, 1, 1)]
        [InlineData("xnor", 0, 0, 1)]
        [InlineData("xnor", 0, 1, 0)]
        public void TwoInputCircuitsMatchTheirGates(string circuit, int a, int b, int output)
        {
            ISignal[] leaving = Run(TestCircuits.ByName[circuit](), north: Bit(b), west: Bit(a));

            Assert.Equal(Bit(output), leaving[(int)Side.East]);
        }

        [Theory]
        [InlineData(1, 0, 0, 1)]
        [InlineData(0, 1, 0, 0)]
        [InlineData(1, 0, 1, 0)]
        [InlineData(0, 1, 1, 1)]
        public void MuxPicksAUnlessSelectIsOn(int a, int b, int select, int output)
        {
            ISignal[] arriving = { Bit(b), Nothing, Bit(select), Bit(a) };
            var leaving = new ISignal[Netlist.SideCount];

            new NetlistEvaluator(TestCircuits.Mux()).Evaluate(arriving, leaving);

            Assert.Equal(Bit(output), leaving[(int)Side.East]);
        }

        [Fact]
        public void BufferPassesAnySignalThroughUnchanged()
        {
            ISignal conflict = ConflictSignal.Instance;

            Assert.Same(conflict, Run(TestCircuits.ByName["buffer"](), west: conflict)[(int)Side.East]);
            Assert.Same(Nothing, Run(TestCircuits.ByName["buffer"](), west: Nothing)[(int)Side.East]);
        }

        [Fact]
        public void TheEmptyNetlistDrivesNothing()
        {
            ISignal[] leaving = Run(Netlist.Empty, north: Bit(1), west: Bit(1));

            Assert.All(leaving, signal => Assert.Same(Nothing, signal));
        }

        [Fact]
        public void NotGateInvertsTruthiness()
        {
            var netlist = new Netlist(
                "Inverter",
                netCount: 2,
                sideRoles: new[] { SideRole.Unused, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { -1, 1, -1, 0 },
                gates: new[] { new Gate(GateKind.Not, 1, 0) });

            Assert.Equal(Bit(1), Run(netlist, west: Nothing)[(int)Side.East]);
            Assert.Equal(Bit(0), Run(netlist, west: Bit(1))[(int)Side.East]);
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
