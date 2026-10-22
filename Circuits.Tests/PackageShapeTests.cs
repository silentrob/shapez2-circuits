using System.Linq;
using Circuits.IC;
using Circuits.IC.Compilation;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    public class PackageShapeTests
    {
        [Fact]
        public void A1x1PackageNumbersItsConnectionsAsSidesDo()
        {
            PackageShape shape = PackageShape.Single;

            Assert.Equal(Netlist.SideCount, shape.Connections);
            foreach (Side side in new[] { Side.North, Side.East, Side.South, Side.West })
            {
                Assert.Equal((int)side, shape.IndexOf(side, 0));
                Assert.Equal(side.ToString(), shape.Name((int)side));
            }
        }

        [Fact]
        public void ConnectionsRunClockwiseFromTheNorthWestCorner()
        {
            PackageShape shape = PackageShape.Wide;

            string[] names = Enumerable.Range(0, shape.Connections).Select(shape.Name).ToArray();

            Assert.Equal(new[] { "North 1", "North 2", "East", "South 2", "South 1", "West" }, names);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(2, 2)]
        public void EveryConnectionIsFoundAgainByItsEdgeAndPosition(int width, int height)
        {
            Assert.True(PackageShape.TryGet(width, height, out PackageShape shape));

            for (int connection = 0; connection < shape.Connections; connection++)
            {
                (Side edge, int position) = shape.Locate(connection);
                Assert.Equal(connection, shape.IndexOf(edge, position));
            }
        }

        [Fact]
        public void EachConnectionSitsOnTheTileAtItsEdge()
        {
            PackageShape shape = PackageShape.Square;

            Assert.Equal((0, 0), shape.Tile(shape.IndexOf(Side.North, 0)));
            Assert.Equal((1, 0), shape.Tile(shape.IndexOf(Side.North, 1)));
            Assert.Equal((1, 1), shape.Tile(shape.IndexOf(Side.East, 1)));
            Assert.Equal((0, 1), shape.Tile(shape.IndexOf(Side.South, 0)));
            Assert.Equal((0, 0), shape.Tile(shape.IndexOf(Side.West, 0)));
        }

        [Fact]
        public void ALargerNetlistSurvivesARoundTrip()
        {
            PackageShape shape = PackageShape.Square;
            var roles = new SideRole[shape.Connections];
            int[] nets = Enumerable.Repeat(-1, shape.Connections).ToArray();
            roles[shape.IndexOf(Side.West, 1)] = SideRole.Input;
            nets[shape.IndexOf(Side.West, 1)] = 0;
            roles[shape.IndexOf(Side.East, 0)] = SideRole.Output;
            nets[shape.IndexOf(Side.East, 0)] = 1;
            var original = new Netlist("Square", shape, 2, roles, nets, new[] { new Gate(GateKind.Not, 1, 0) });

            Assert.True(NetlistCodec.TryDecode(NetlistCodec.Encode(original), out Netlist decoded));
            Assert.Equal(shape, decoded.Shape);
            Assert.Equal(roles, decoded.SideRoles);
            Assert.Equal(nets, decoded.SideNets);
        }

        [Fact]
        public void ABoardCompilesToItsShapesConnections()
        {
            PackageShape shape = PackageShape.Wide;
            int input = shape.IndexOf(Side.North, 1);
            int output = shape.IndexOf(Side.South, 0);
            var parts = new[]
            {
                new Part(PartKind.InputPin, "in", new[] { new Port(new Pivot(0, 0, 0, Facing.South), PortKind.Out) }, input),
                new Part(PartKind.Not, "not", new[]
                {
                    new Port(new Pivot(0, 1, 0, Facing.North), PortKind.In),
                    new Port(new Pivot(0, 1, 0, Facing.South), PortKind.Out)
                }),
                new Part(PartKind.OutputPin, "out", new[] { new Port(new Pivot(0, 2, 0, Facing.North), PortKind.In) }, output)
            };

            CompileResult result = BoardCompiler.Compile("Wide", shape, parts);

            Assert.Empty(result.Errors);
            var arriving = Enumerable.Repeat<ISignal>(NullSignal.Instance, shape.Connections).ToArray();
            var leaving = new ISignal[shape.Connections];
            new NetlistEvaluator(result.Netlist).Evaluate(arriving, leaving);
            Assert.Equal(IntegerSignal.Get(true), leaving[output]);
            Assert.Equal("North 2 in, South 1 out", result.Netlist.DescribeSides());
        }
    }
}
