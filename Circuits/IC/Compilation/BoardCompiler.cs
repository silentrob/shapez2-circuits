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
    //   3. Order gates so each comes after the gates driving its inputs. Feedback loops are refused for now.
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
                    case PartKind.Constant:
                        gates.Add((Gate.Constant(OnlyOutput(part, nets), part.Value), part));
                        break;
                    default:
                        gates.Add((ToGate(part, nets), part));
                        break;
                }
            }

            if (sideRoles.All(role => role == SideRole.Unused))
            {
                errors.Add("The board has no IC pins: place IC Input Pins and IC Output Pins on its edges.");
            }

            Gate[] ordered = Order(gates, errors);
            return errors.Count > 0
                ? CompileResult.Failure(errors)
                : CompileResult.Success(new Netlist(name, shape, netOfSet.Count, sideRoles, sideNets, ordered));
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

        private static Gate ToGate(Part part, int[] nets)
        {
            GateKind kind = part.Kind switch
            {
                PartKind.And => GateKind.And,
                PartKind.Or => GateKind.Or,
                PartKind.Xor => GateKind.Xor,
                PartKind.Not => GateKind.Not,
                PartKind.Transistor => GateKind.Transistor,
                _ => throw new ArgumentOutOfRangeException(nameof(part), part.Kind, null)
            };

            int[] inputs = part.Ports.Select((port, i) => (port, i)).Where(p => p.port.Kind == PortKind.In)
               .Select(p => nets[p.i]).ToArray();
            return new Gate(kind, OnlyOutput(part, nets), inputs);
        }

        // Kahn's algorithm over "gate A drives a net gate B reads". Gates left over sit on a feedback loop.
        private static Gate[] Order(List<(Gate Gate, Part Part)> gates, List<string> errors)
        {
            var drivers = new Dictionary<int, List<int>>();
            for (int i = 0; i < gates.Count; i++)
            {
                if (!drivers.TryGetValue(gates[i].Gate.Output, out List<int> list))
                {
                    drivers.Add(gates[i].Gate.Output, list = new List<int>());
                }

                list.Add(i);
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
