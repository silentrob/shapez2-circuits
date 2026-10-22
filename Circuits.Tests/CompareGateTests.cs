using Circuits.IC;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    // The vanilla Compare gate inside packages: 1 or 0, as vanilla computes it.
    public class CompareGateTests
    {
        [Theory]
        [InlineData(GateKind.CompareEqual, 3, 3, true)]
        [InlineData(GateKind.CompareEqual, 3, 4, false)]
        [InlineData(GateKind.CompareNotEqual, 3, 4, true)]
        [InlineData(GateKind.CompareGreater, 4, 3, true)]
        [InlineData(GateKind.CompareGreater, 3, 3, false)]
        [InlineData(GateKind.CompareGreaterEqual, 3, 3, true)]
        [InlineData(GateKind.CompareLess, 2, 3, true)]
        [InlineData(GateKind.CompareLessEqual, 4, 3, false)]
        public void EachComparisonMatchesVanilla(GateKind kind, int a, int b, bool holds)
        {
            Assert.Equal(IntegerSignal.Get(holds), CompareRule.Apply(kind, IntegerSignal.Get(a), IntegerSignal.Get(b)));
        }

        [Fact]
        public void OrderingsHoldOnlyBetweenNumbers()
        {
            Assert.Equal(IntegerSignal.Get(false), CompareRule.Apply(GateKind.CompareLess, NullSignal.Instance, IntegerSignal.Get(3)));
            Assert.Equal(IntegerSignal.Get(true), CompareRule.Apply(GateKind.CompareEqual, NullSignal.Instance, NullSignal.Instance));
        }

        [Fact]
        public void ACompareGateRunsInsideAPackage()
        {
            // A on the west, B on the north, A > B out on the east.
            var netlist = new Netlist(
                "Greater",
                netCount: 3,
                sideRoles: new[] { SideRole.Input, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { 1, 2, -1, 0 },
                gates: new[] { new Gate(GateKind.CompareGreater, 2, 0, 1) });
            Assert.True(NetlistCodec.TryDecode(NetlistCodec.Encode(netlist), out Netlist decoded));
            var leaving = new ISignal[Netlist.SideCount];

            new NetlistEvaluator(decoded).Evaluate(
                new ISignal[] { IntegerSignal.Get(2), NullSignal.Instance, NullSignal.Instance, IntegerSignal.Get(5) },
                leaving);

            Assert.Equal(IntegerSignal.Get(true), leaving[(int)Side.East]);
        }
    }
}
