using System.Collections.Generic;
using Circuits.IC;
using Circuits.IC.Compilation;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    // The Circuits latch inside a package: it keeps a value between ticks, as the latch building does.
    public class LatchTests
    {
        private static readonly ISignal Nothing = NullSignal.Instance;

        // Data on the west, Reset on the north, the held value out on the east.
        private static Netlist Latch()
        {
            return new Netlist(
                "Latch",
                netCount: 3,
                sideRoles: new[] { SideRole.Input, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { 1, 2, -1, 0 },
                gates: new[] { new Gate(GateKind.Latch, 2, 0, 1) });
        }

        private static ISignal Tick(NetlistEvaluator evaluator, ISignal data, ISignal reset)
        {
            var leaving = new ISignal[Netlist.SideCount];
            evaluator.Evaluate(new[] { reset, Nothing, Nothing, data }, leaving);
            return leaving[(int)Side.East];
        }

        [Fact]
        public void ALatchHoldsUntilReset()
        {
            var evaluator = new NetlistEvaluator(Latch());

            Assert.Same(Nothing, Tick(evaluator, Nothing, Nothing));
            Assert.Equal(IntegerSignal.Get(7), Tick(evaluator, IntegerSignal.Get(7), Nothing));
            Assert.Equal(IntegerSignal.Get(7), Tick(evaluator, Nothing, Nothing));
            Assert.Equal(IntegerSignal.Get(7), Tick(evaluator, IntegerSignal.Get(0), Nothing));
            Assert.Same(Nothing, Tick(evaluator, IntegerSignal.Get(3), IntegerSignal.Get(1)));
            Assert.Same(Nothing, Tick(evaluator, Nothing, Nothing));
        }

        [Fact]
        public void AHeldValueCarriesIntoANewEvaluator()
        {
            var first = new NetlistEvaluator(Latch());
            Tick(first, IntegerSignal.Get(5), Nothing);

            // As a package reloaded from a save: its state hands the evaluator what the latches held.
            var reloaded = new NetlistEvaluator(Latch(), held: first.Held);

            Assert.Equal(IntegerSignal.Get(5), Tick(reloaded, Nothing, Nothing));
        }

        [Fact]
        public void ALatchSurvivesARoundTrip()
        {
            Assert.True(NetlistCodec.TryDecode(NetlistCodec.Encode(Latch()), out Netlist decoded));
            Assert.Equal(GateKind.Latch, decoded.Gates[0].Kind);
            Assert.Equal(new[] { 0, 1 }, decoded.Gates[0].Inputs);
            Assert.Equal(1, decoded.Latches);
        }

        [Fact]
        public void ALatchOnABoardCompilesWithDataThenReset()
        {
            var parts = new List<Part>
            {
                new(PartKind.InputPin, "data", new[] { new Port(new Pivot(0, 1, 0, Facing.East), PortKind.Out) }, Side.West),
                new(PartKind.InputPin, "reset", new[] { new Port(new Pivot(1, 0, 0, Facing.South), PortKind.Out) }, Side.North),
                new(PartKind.Latch, "latch", new[]
                {
                    new Port(new Pivot(1, 1, 0, Facing.West), PortKind.In),
                    new Port(new Pivot(1, 1, 0, Facing.North), PortKind.In),
                    new Port(new Pivot(1, 1, 0, Facing.East), PortKind.Out)
                }),
                new(PartKind.OutputPin, "out", new[] { new Port(new Pivot(2, 1, 0, Facing.West), PortKind.In) }, Side.East)
            };

            CompileResult result = BoardCompiler.Compile("Latch", parts);

            Assert.Empty(result.Errors);
            var evaluator = new NetlistEvaluator(result.Netlist);
            Assert.Equal(IntegerSignal.Get(1), Tick(evaluator, IntegerSignal.Get(1), Nothing));
            Assert.Equal(IntegerSignal.Get(1), Tick(evaluator, Nothing, Nothing));
            Assert.Same(Nothing, Tick(evaluator, Nothing, IntegerSignal.Get(1)));
        }
    }
}
