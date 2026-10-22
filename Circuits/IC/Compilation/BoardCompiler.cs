using System;
using System.Collections.Generic;
using System.Linq;

namespace Circuits.IC.Compilation
{
    public sealed class CompileResult
    {
        public readonly Netlist Netlist;
        public readonly IReadOnlyList<string> Errors;

        public bool Succeeded => Netlist != null;

        private CompileResult(Netlist netlist, IReadOnlyList<string> errors)
        {
            Netlist = netlist;
            Errors = errors;
        }

        public static CompileResult Success(Netlist netlist)
        {
            return new CompileResult(netlist, Array.Empty<string>());
        }

        public static CompileResult Failure(IReadOnlyList<string> errors)
        {
            return new CompileResult(null, errors);
        }
    }

    // Turns a board's parts into a netlist:
    //   1. Trace nets: connectors joined by wires, or facing each other directly. A wire joins its junctions;
    //      facing connectors join when the game would connect them (not input to input, or output to output).
    //   2. Bind pins to package sides, and gates to the nets on their connectors.
    //   3. Copy in the firmware of chips on the board: their gates join the board's, on the board's nets where
    //      the chip connects and on nets of their own inside it.
    //   4. Order gates so each comes after the gates driving its inputs. Feedback loops are refused for now.
    public static class BoardCompiler
    {
        public static CompileResult Compile(string name, IReadOnlyList<Part> parts)
        {
            return Compile(name, PackageShape.Single, parts);
        }

        public static CompileResult Compile(string name, PackageShape shape, IReadOnlyList<Part> parts)
        {
            var errors = new List<string>();
            var ports = new List<(Part Part, Port Port)>();
            foreach (Part part in parts)
            {
                ports.AddRange(part.Ports.Select(port => (part, port)));
            }

            var sets = new DisjointSets(ports.Count);
            JoinWireJunctions(ports, sets);
            JoinFacingConnectors(ports, sets);

            // Number the nets that gates and pins use; wires alone need no net.
            var netOfSet = new Dictionary<int, int>();
            int NetOf(int portIndex)
            {
                int set = sets.Find(portIndex);
                if (!netOfSet.TryGetValue(set, out int net))
                {
                    net = netOfSet.Count;
                    netOfSet.Add(set, net);
                }

                return net;
            }

            var portIndex = 0;
            var sideRoles = new SideRole[shape.Connections];
            var sideNets = Enumerable.Repeat(-1, shape.Connections).ToArray();
            var sidePins = new string[shape.Connections];
            var gates = new List<(Gate Gate, Part Part)>();
            var packages = new List<(Part Part, int[] Nets)>();
            foreach (Part part in parts)
            {
                int first = portIndex;
                portIndex += part.Ports.Length;
                if (part.Kind == PartKind.Wire)
                {
                    continue;
                }

                int[] nets = Enumerable.Range(first, part.Ports.Length).Select(NetOf).ToArray();
                switch (part.Kind)
                {
                    case PartKind.InputPin:
                    case PartKind.OutputPin:
                        BindPin(part, shape, nets, sideRoles, sideNets, sidePins, errors);
                        break;
                    case PartKind.Package:
                        packages.Add((part, nets));
                        break;
                    case PartKind.Constant:
                        gates.Add((
                            part.Signal != null
                                ? Gate.Constant(OnlyOutput(part, nets), part.Signal)
                                : Gate.Constant(OnlyOutput(part, nets), part.Value),
                            part));
                        break;
                    default:
                        if (ToGate(part, nets, errors) is { } gate)
                        {
                            gates.Add((gate, part));
                        }

                        break;
                }
            }

            if (sideRoles.All(role => role == SideRole.Unused))
            {
                errors.Add("The board has no IC pins: place IC Input Pins and IC Output Pins on its edges.");
            }

            // A chip's own nets are numbered after the board's.
            int netCount = netOfSet.Count;
            foreach ((Part package, int[] nets) in packages)
            {
                Expand(package, nets, ref netCount, gates, errors);
            }

            Gate[] ordered = Order(gates, errors);
            return errors.Count > 0
                ? CompileResult.Failure(errors)
                : CompileResult.Success(new Netlist(name, shape, netCount, sideRoles, sideNets, ordered));
        }

        // Copies a chip's gates in: each of its connected connections' nets becomes the board net on that port,
        // and its other nets get new numbers from netCount on.
        private static void Expand(Part package, int[] portNets, ref int netCount, List<(Gate Gate, Part Part)> gates, List<string> errors)
        {
            Netlist nested = package.Nested;
            int[] map = Enumerable.Repeat(-1, nested.NetCount).ToArray();
            for (int port = 0; port < portNets.Length; port++)
            {
                int connection = package.PortConnections[port];
                int inner = nested.SideNets[connection];
                if (map[inner] == -1)
                {
                    map[inner] = portNets[port];
                }
                else if (map[inner] != portNets[port])
                {
                    errors.Add($"{package.Label} joins two of its connections inside ({nested.Shape.Name(connection)} "
                               + "and another) to separate wires on this board; chips like that can't be nested yet.");
                    return;
                }
            }

            for (int inner = 0; inner < map.Length; inner++)
            {
                if (map[inner] == -1)
                {
                    map[inner] = netCount++;
                }
            }

            foreach (Gate gate in nested.Gates)
            {
                Gate copy = gate.Kind == GateKind.Constant
                    ? gate.Signal != null
                        ? Gate.Constant(map[gate.Output], gate.Signal)
                        : Gate.Constant(map[gate.Output], gate.Value)
                    : new Gate(gate.Kind, gate.Outputs.Select(net => map[net]).ToArray(), gate.Inputs.Select(net => map[net]).ToArray());
                gates.Add((copy, package));
            }
        }

        private static void JoinWireJunctions(List<(Part Part, Port Port)> ports, DisjointSets sets)
        {
            var firstOfGroup = new Dictionary<(Part, int), int>();
            for (int i = 0; i < ports.Count; i++)
            {
                if (ports[i].Port.Kind != PortKind.Junction)
                {
                    continue;
                }

                (Part, int) group = (ports[i].Part, ports[i].Port.Group);
                if (firstOfGroup.TryGetValue(group, out int first))
                {
                    sets.Union(first, i);
                }
                else
                {
                    firstOfGroup.Add(group, i);
                }
            }
        }

        private static void JoinFacingConnectors(List<(Part Part, Port Port)> ports, DisjointSets sets)
        {
            var byPivot = new Dictionary<Pivot, int>();
            for (int i = 0; i < ports.Count; i++)
            {
                byPivot[ports[i].Port.Pivot] = i;
            }

            for (int i = 0; i < ports.Count; i++)
            {
                if (byPivot.TryGetValue(ports[i].Port.Pivot.Counterpart(), out int other)
                    && Compatible(ports[i].Port.Kind, ports[other].Port.Kind))
                {
                    sets.Union(i, other);
                }
            }
        }

        // As the game's IsCompatibleConnection: junctions meet anything; otherwise an input meets an output.
        private static bool Compatible(PortKind a, PortKind b)
        {
            return a == PortKind.Junction || b == PortKind.Junction || a != b;
        }

        private static void BindPin(
            Part pin,
            PackageShape shape,
            int[] nets,
            SideRole[] sideRoles,
            int[] sideNets,
            string[] sidePins,
            List<string> errors)
        {
            int side = pin.Connection;
            if (side < 0 || side >= shape.Connections)
            {
                errors.Add($"{pin.Label} is not on one of the board's {shape.Connections} connections.");
                return;
            }

            if (sidePins[side] != null)
            {
                errors.Add($"{pin.Label} and {sidePins[side]} are both at the {shape.Name(side)} connection; each holds one pin.");
                return;
            }

            if (nets.Length != 1)
            {
                errors.Add($"{pin.Label} should have exactly one connector.");
                return;
            }

            sidePins[side] = pin.Label;
            sideRoles[side] = pin.Kind == PartKind.InputPin ? SideRole.Input : SideRole.Output;
            sideNets[side] = nets[0];
        }

        private static int OnlyOutput(Part part, int[] nets)
        {
            return nets[Array.FindIndex(part.Ports, port => port.Kind == PortKind.Out)];
        }

        // Inputs and outputs in connector order, which is the order the game binds them in.
        private static Gate ToGate(Part part, int[] nets, List<string> errors)
        {
            GateKind kind = part.Kind switch
            {
                PartKind.And => GateKind.And,
                PartKind.Or => GateKind.Or,
                PartKind.Xor => GateKind.Xor,
                PartKind.Not => GateKind.Not,
                PartKind.Transistor => GateKind.Transistor,
                PartKind.VirtualRotateCw => GateKind.VirtualRotateCw,
                PartKind.VirtualRotateCcw => GateKind.VirtualRotateCcw,
                PartKind.VirtualHalfCut => GateKind.VirtualHalfCut,
                PartKind.VirtualStack => GateKind.VirtualStack,
                PartKind.VirtualUnstack => GateKind.VirtualUnstack,
                PartKind.VirtualPaint => GateKind.VirtualPaint,
                PartKind.VirtualCrystallize => GateKind.VirtualCrystallize,
                PartKind.VirtualSwapHalves => GateKind.VirtualSwapHalves,
                PartKind.VirtualPushPin => GateKind.VirtualPushPin,
                PartKind.VirtualAnalyze => GateKind.VirtualAnalyze,
                PartKind.Latch => GateKind.Latch,
                _ => throw new ArgumentOutOfRangeException(nameof(part), part.Kind, null)
            };

            int[] Nets(PortKind portKind)
            {
                return part.Ports.Select((port, i) => (port, i)).Where(p => p.port.Kind == portKind)
                   .Select(p => nets[p.i]).ToArray();
            }

            int[] inputs = Nets(PortKind.In), outputs = Nets(PortKind.Out);
            if (inputs.Length != Gate.InputCount(kind) || outputs.Length != Gate.OutputCount(kind))
            {
                errors.Add($"{part.Label} has {inputs.Length} inputs and {outputs.Length} outputs; expected "
                           + $"{Gate.InputCount(kind)} and {Gate.OutputCount(kind)}.");
                return null;
            }

            return new Gate(kind, outputs, inputs);
        }

        // Kahn's algorithm over "gate A drives a net gate B reads". Gates left over sit on a feedback loop.
        private static Gate[] Order(List<(Gate Gate, Part Part)> gates, List<string> errors)
        {
            var drivers = new Dictionary<int, List<int>>();
            for (int i = 0; i < gates.Count; i++)
            {
                foreach (int output in gates[i].Gate.Outputs.Distinct())
                {
                    if (!drivers.TryGetValue(output, out List<int> list))
                    {
                        drivers.Add(output, list = new List<int>());
                    }

                    list.Add(i);
                }
            }

            var waitingOn = new int[gates.Count];
            var readers = new List<int>[gates.Count];
            for (int i = 0; i < gates.Count; i++)
            {
                readers[i] = new List<int>();
            }

            for (int i = 0; i < gates.Count; i++)
            {
                foreach (int input in gates[i].Gate.Inputs.Distinct())
                {
                    if (drivers.TryGetValue(input, out List<int> list))
                    {
                        foreach (int driver in list)
                        {
                            readers[driver].Add(i);
                            waitingOn[i]++;
                        }
                    }
                }
            }

            var ready = new Queue<int>(Enumerable.Range(0, gates.Count).Where(i => waitingOn[i] == 0));
            var ordered = new List<Gate>();
            while (ready.Count > 0)
            {
                int gate = ready.Dequeue();
                ordered.Add(gates[gate].Gate);
                foreach (int reader in readers[gate])
                {
                    if (--waitingOn[reader] == 0)
                    {
                        ready.Enqueue(reader);
                    }
                }
            }

            if (ordered.Count < gates.Count)
            {
                IEnumerable<string> looped = Enumerable.Range(0, gates.Count).Where(i => waitingOn[i] > 0)
                   .Select(i => gates[i].Part.Label);
                errors.Add($"Feedback loops are not supported yet. These gates are on one: {string.Join(", ", looped)}.");
            }

            return ordered.ToArray();
        }

        private sealed class DisjointSets
        {
            private readonly int[] Parent;

            public DisjointSets(int count)
            {
                Parent = Enumerable.Range(0, count).ToArray();
            }

            public int Find(int i)
            {
                while (Parent[i] != i)
                {
                    Parent[i] = Parent[Parent[i]];
                    i = Parent[i];
                }

                return i;
            }

            public void Union(int a, int b)
            {
                Parent[Find(a)] = Find(b);
            }
        }
    }
}
