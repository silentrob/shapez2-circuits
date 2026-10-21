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
