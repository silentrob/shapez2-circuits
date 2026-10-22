using System.Collections.Generic;
using System.Linq;
using Circuits.IC;
using Circuits.IC.Compilation;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    // Virtual shape processors and non-number constants as far as they can be checked without the game: they
    // compile, order and survive the codec. Their shape operations are the game's own (GateResolver), checked in
    // game.
    public class VirtualProcessorTests
    {
        [Fact]
        public void ATwoOutputGateAndASignalConstantSurviveARoundTrip()
        {
            var original = new Netlist(
                "Shapes",
                netCount: 4,
                sideRoles: new[] { SideRole.Output, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { 1, 2, -1, 0 },
                gates: new[]
                {
                    Gate.Constant(3, "12:AAEC"),
                    new Gate(GateKind.VirtualUnstack, new[] { 1, 2 }, new[] { 0 })
                });

            Assert.True(NetlistCodec.TryDecode(NetlistCodec.Encode(original), out Netlist decoded));
            Assert.Equal("12:AAEC", decoded.Gates[0].Signal);
            Assert.Equal(GateKind.VirtualUnstack, decoded.Gates[1].Kind);
            Assert.Equal(new[] { 1, 2 }, decoded.Gates[1].Outputs);
        }

        [Fact]
        public void AGateReadingASecondOutputComesAfterIt()
        {
            // West pin -> unstacker; its second output -> NOT -> East pin; its first output -> South pin.
            var parts = new List<Part>
            {
                new(PartKind.Not, "not", new[]
                {
                    new Port(new Pivot(2, 1, 0, Facing.West), PortKind.In),
                    new Port(new Pivot(2, 1, 0, Facing.East), PortKind.Out)
                }),
                new(PartKind.VirtualUnstack, "unstacker", new[]
                {
                    new Port(new Pivot(1, 0, 0, Facing.West), PortKind.In),
                    new Port(new Pivot(1, 0, 0, Facing.South), PortKind.Out),
                    new Port(new Pivot(1, 0, 0, Facing.East), PortKind.Out)
                }),
                new(PartKind.Wire, "wire", new[]
                {
                    new Port(new Pivot(2, 0, 0, Facing.West), PortKind.Junction),
                    new Port(new Pivot(2, 0, 0, Facing.South), PortKind.Junction)
                }),
                new(PartKind.Wire, "wire", new[]
                {
                    new Port(new Pivot(2, 1, 0, Facing.North), PortKind.Junction),
                    new Port(new Pivot(1, 1, 0, Facing.East), PortKind.Junction)
                }),
                new(PartKind.InputPin, "in", new[] { new Port(new Pivot(0, 0, 0, Facing.East), PortKind.Out) }, Side.West),
                new(PartKind.OutputPin, "east", new[] { new Port(new Pivot(3, 1, 0, Facing.West), PortKind.In) }, Side.East),
                new(PartKind.OutputPin, "south", new[] { new Port(new Pivot(1, 1, 0, Facing.North), PortKind.In) }, Side.South)
            };

            CompileResult result = BoardCompiler.Compile("Unstack", parts);

            Assert.Empty(result.Errors);
            GateKind[] order = result.Netlist.Gates.Select(gate => gate.Kind).ToArray();
            Assert.True(System.Array.IndexOf(order, GateKind.VirtualUnstack) < System.Array.IndexOf(order, GateKind.Not));
        }

        [Fact]
        public void AProcessorWithMissingConnectorsIsRefused()
        {
            var parts = new List<Part>
            {
                new(PartKind.VirtualStack, "stacker", new[]
                {
                    new Port(new Pivot(1, 0, 0, Facing.West), PortKind.In),
                    new Port(new Pivot(1, 0, 0, Facing.East), PortKind.Out)
                }),
                new(PartKind.InputPin, "in", new[] { new Port(new Pivot(0, 0, 0, Facing.East), PortKind.Out) }, Side.West)
            };

            CompileResult result = BoardCompiler.Compile("Broken", parts);

            Assert.Contains(result.Errors, error => error.Contains("stacker has 1 inputs and 1 outputs"));
        }

        [Fact]
        public void WithoutTheGameAProcessorDrivesNothing()
        {
            var netlist = new Netlist(
                "Rotate",
                netCount: 2,
                sideRoles: new[] { SideRole.Unused, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { -1, 1, -1, 0 },
                gates: new[] { new Gate(GateKind.VirtualRotateCw, 1, 0) });
            ISignal[] arriving = { NullSignal.Instance, NullSignal.Instance, NullSignal.Instance, IntegerSignal.Get(1) };
            var leaving = new ISignal[Netlist.SideCount];

            new NetlistEvaluator(netlist).Evaluate(arriving, leaving);

            Assert.Same(NullSignal.Instance, leaving[(int)Side.East]);
        }
    }
}
