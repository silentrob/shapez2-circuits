using Circuits.IC;
using Circuits.IntGates;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    public class IntegerGateTests
    {
        [Theory]
        [InlineData(IntegerOp.And, 0b1100, 0b1010, 0b1000)]
        [InlineData(IntegerOp.Or, 0b1100, 0b1010, 0b1110)]
        [InlineData(IntegerOp.Xor, 0b1100, 0b1010, 0b0110)]
        [InlineData(IntegerOp.Not, 0, 0, -1)]
        [InlineData(IntegerOp.ShiftLeft, 1, 4, 16)]
        [InlineData(IntegerOp.ShiftLeft, 16, -4, 1)]
        [InlineData(IntegerOp.ShiftLeft, 1, 32, 0)]
        [InlineData(IntegerOp.ShiftRight, 16, 4, 1)]
        [InlineData(IntegerOp.ShiftRight, -1, 28, 15)]
        [InlineData(IntegerOp.Add, 5, 7, 12)]
        [InlineData(IntegerOp.Add, int.MaxValue, 1, int.MinValue)]
        [InlineData(IntegerOp.Subtract, 5, 7, -2)]
        public void EachOperationComputesItsResult(IntegerOp op, int a, int b, int result)
        {
            Assert.Equal(result, IntegerOps.Compute(op, a, b));
        }

        [Fact]
        public void AnythingButANumberCountsAsZero()
        {
            Assert.Equal(IntegerSignal.Get(5), IntegerOps.Apply(IntegerOp.Add, IntegerSignal.Get(5), NullSignal.Instance));
            Assert.Equal(IntegerSignal.Get(0), IntegerOps.Apply(IntegerOp.And, NullSignal.Instance, NullSignal.Instance));
        }

        [Fact]
        public void AMaskPicksBitsOutInsideAPackage()
        {
            // Value on the west, mask on the north, value AND mask out on the east.
            var netlist = new Netlist(
                "Mask",
                netCount: 3,
                sideRoles: new[] { SideRole.Input, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { 1, 2, -1, 0 },
                gates: new[] { new Gate(Gate.Integer(IntegerOp.And), 2, 0, 1) });
            ISignal[] arriving = { IntegerSignal.Get(0b0110), NullSignal.Instance, NullSignal.Instance, IntegerSignal.Get(0b1111_0101) };
            var leaving = new ISignal[Netlist.SideCount];

            new NetlistEvaluator(netlist).Evaluate(arriving, leaving);

            Assert.Equal(IntegerSignal.Get(0b0100), leaving[(int)Side.East]);
            Assert.True(NetlistCodec.TryDecode(NetlistCodec.Encode(netlist), out Netlist decoded));
            Assert.Equal(Gate.Integer(IntegerOp.And), decoded.Gates[0].Kind);
        }
    }
}
