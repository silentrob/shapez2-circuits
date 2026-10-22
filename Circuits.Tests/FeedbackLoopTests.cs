using System.Collections.Generic;
using System.Linq;
using Circuits.IC;
using Circuits.IC.Compilation;
using Game.Content.Features.Signals;
using Xunit;

namespace Circuits.Tests
{
    // Feedback loops, against what the game does on a board (measured 2026-10-22): a loop is delayed by one tick
    // at one point, so a NOT feeding itself and a ring of three NOTs both flip every tick, and an SR latch made of
    // two NOR gates sets, holds and resets.
    public class FeedbackLoopTests
    {
        private static readonly ISignal Nothing = NullSignal.Instance;
        private static readonly ISignal On = IntegerSignal.Get(1);
        private static readonly ISignal Off = IntegerSignal.Get(0);

        // Each gate sits on its own tile, two apart so neighbours don't face each other; its inputs face west and
        // north, its output east.
        private static Part Gate(PartKind kind, string label, int slot, int inputs)
        {
            int x = slot * 2;
            var ports = new List<Port> { new(new Pivot(x, 0, 0, Facing.West), PortKind.In) };
            if (inputs == 2)
            {
                ports.Add(new Port(new Pivot(x, 0, 0, Facing.North), PortKind.In));
            }

            ports.Add(new Port(new Pivot(x, 0, 0, Facing.East), PortKind.Out));
            return new Part(kind, label, ports.ToArray());
        }

        private static Pivot In(Part gate, int input = 0) => gate.Ports.Where(p => p.Kind == PortKind.In).ElementAt(input).Pivot;
        private static Pivot Out(Part gate) => gate.Ports.First(p => p.Kind == PortKind.Out).Pivot;

        // A wire joining these connectors (its ends face each of them), wherever they are.
        private static Part Wire(params Pivot[] connectors)
        {
            return new Part(PartKind.Wire, "wire", connectors.Select(c => new Port(c.Counterpart(), PortKind.Junction)).ToArray());
        }

        private static Part Pin(PartKind kind, Side side, int x)
        {
            return new Part(kind, kind.ToString(), new[] { new Port(new Pivot(x, 9, 0, Facing.North), kind == PartKind.InputPin ? PortKind.Out : PortKind.In) }, side);
        }

        private static ISignal[] Tick(NetlistEvaluator evaluator, ISignal north = null, ISignal west = null)
        {
            var leaving = new ISignal[Netlist.SideCount];
            evaluator.Evaluate(new[] { north ?? Nothing, Nothing, Nothing, west ?? Nothing }, leaving);
            return leaving;
        }

        private static Netlist Compiled(List<Part> parts)
        {
            CompileResult result = BoardCompiler.Compile("Loop", parts);
            Assert.Empty(result.Errors);
            return result.Netlist;
        }

        [Fact]
        public void ANotFeedingItselfFlipsEveryTick()
        {
            Part not = Gate(PartKind.Not, "not", 0, 1);
            Part east = Pin(PartKind.OutputPin, Side.East, 5);
            var evaluator = new NetlistEvaluator(Compiled(new List<Part> { not, east, Wire(Out(not), In(not), east.Ports[0].Pivot) }));

            ISignal[] seen = Enumerable.Range(0, 4).Select(_ => Tick(evaluator)[(int)Side.East]).ToArray();

            Assert.Equal(new[] { On, Off, On, Off }, seen);
        }

        [Fact]
        public void ARingOfThreeNotsFlipsEveryTickToo()
        {
            Part a = Gate(PartKind.Not, "a", 0, 1), b = Gate(PartKind.Not, "b", 1, 1), c = Gate(PartKind.Not, "c", 2, 1);
            Part east = Pin(PartKind.OutputPin, Side.East, 5);
            var evaluator = new NetlistEvaluator(Compiled(new List<Part>
            {
                a, b, c, east,
                Wire(Out(a), In(b)),
                Wire(Out(b), In(c)),
                Wire(Out(c), In(a), east.Ports[0].Pivot)
            }));

            ISignal[] seen = Enumerable.Range(0, 4).Select(_ => Tick(evaluator)[(int)Side.East]).ToArray();

            Assert.NotEqual(seen[0], seen[1]);
            Assert.NotEqual(seen[1], seen[2]);
            Assert.NotEqual(seen[2], seen[3]);
        }

        // S from the north pin, R from the west pin, Q out east: each NOR is an OR then a NOT, cross-coupled.
        private static NetlistEvaluator NorLatch()
        {
            Part orS = Gate(PartKind.Or, "or S", 0, 2), notS = Gate(PartKind.Not, "not S", 1, 1);
            Part orR = Gate(PartKind.Or, "or R", 2, 2), notR = Gate(PartKind.Not, "not R", 3, 1);
            Part s = Pin(PartKind.InputPin, Side.North, 6), r = Pin(PartKind.InputPin, Side.West, 7);
            Part q = Pin(PartKind.OutputPin, Side.East, 8);
            return new NetlistEvaluator(Compiled(new List<Part>
            {
                orS, notS, orR, notR, s, r, q,
                Wire(s.Ports[0].Pivot, In(orS, 0)),
                Wire(r.Ports[0].Pivot, In(orR, 0)),
                Wire(Out(orS), In(notS)),
                Wire(Out(orR), In(notR)),
                Wire(Out(notS), In(orR, 1), q.Ports[0].Pivot),
                Wire(Out(notR), In(orS, 1))
            }));
        }

        [Fact]
        public void AnSrLatchOfTwoNorsSetsHoldsAndResets()
        {
            NetlistEvaluator latch = NorLatch();

            Tick(latch, north: On);
            Assert.Equal(Off, Tick(latch, north: On)[(int)Side.East]);
            Assert.Equal(Off, Tick(latch)[(int)Side.East]);
            Assert.Equal(Off, Tick(latch)[(int)Side.East]);

            Tick(latch, west: On);
            Assert.Equal(On, Tick(latch, west: On)[(int)Side.East]);
            Assert.Equal(On, Tick(latch)[(int)Side.East]);
            Assert.Equal(On, Tick(latch)[(int)Side.East]);
        }

        [Fact]
        public void BothReleasedAtOnceSettlesWithoutFlickering()
        {
            NetlistEvaluator latch = NorLatch();
            Tick(latch, north: On, west: On);
            Tick(latch, north: On, west: On);

            ISignal[] seen = Enumerable.Range(0, 6).Select(_ => Tick(latch)[(int)Side.East]).ToArray();

            Assert.All(seen.Skip(2), value => Assert.Equal(seen[1], value));
        }

        [Fact]
        public void ALoopsValueCarriesIntoANewEvaluatorAndTheCodec()
        {
            Part not = Gate(PartKind.Not, "not", 0, 1);
            Part east = Pin(PartKind.OutputPin, Side.East, 5);
            Netlist netlist = Compiled(new List<Part> { not, east, Wire(Out(not), In(not), east.Ports[0].Pivot) });
            var first = new NetlistEvaluator(netlist);
            Tick(first);

            Assert.True(NetlistCodec.TryDecode(NetlistCodec.Encode(netlist), out Netlist decoded));
            var reloaded = new NetlistEvaluator(decoded, previous: first.Previous);

            Assert.Equal(Off, Tick(reloaded)[(int)Side.East]);
        }
    }
}
