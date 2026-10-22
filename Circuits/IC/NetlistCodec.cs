using System;
using System.IO;
using System.Text;

namespace Circuits.IC
{
    // Turns a netlist into a string and back, for storing in a package's building configuration (and so in
    // savegames and blueprints). Versioned: decoding must keep reading every version ever written.
    //   1: gates And, Or, Xor, Not.
    //   2: adds Transistor and Constant; a Constant gate is followed by its value.
    //   3: adds the package shape (width and height bytes) after the name; earlier versions are 1x1.
    //   4: adds the virtual shape processor gates; a Constant's value is followed by its signal ("" for none),
    //      and a gate with two outputs ends with the second.
    //   5: adds the Latch gate.
    //   6: adds the integer gates.
    //   7: a gate other than a Constant ends with which of its inputs read the previous tick (a byte of bits).
    //   8: adds the Compare gates.
    public static class NetlistCodec
    {
        private const int Version = 8;

        // Far beyond any real circuit; only guards against allocating from corrupt counts.
        private const int MaxCount = 1_000_000;

        public static string Encode(Netlist netlist)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Version);
                writer.Write(netlist.Name);
                writer.Write((byte)netlist.Shape.Width);
                writer.Write((byte)netlist.Shape.Height);
                writer.Write(netlist.NetCount);
                for (int side = 0; side < netlist.Connections; side++)
                {
                    writer.Write((byte)netlist.SideRoles[side]);
                    writer.Write(netlist.SideNets[side]);
                }

                writer.Write(netlist.Gates.Length);
                foreach (Gate gate in netlist.Gates)
                {
                    writer.Write((byte)gate.Kind);
                    writer.Write(gate.Output);
                    writer.Write(gate.Inputs.Length);
                    foreach (int input in gate.Inputs)
                    {
                        writer.Write(input);
                    }

                    if (gate.Kind == GateKind.Constant)
                    {
                        writer.Write(gate.Value);
                        writer.Write(gate.Signal ?? "");
                    }

                    for (int extra = 1; extra < gate.Outputs.Length; extra++)
                    {
                        writer.Write(gate.Outputs[extra]);
                    }

                    if (gate.Kind != GateKind.Constant)
                    {
                        writer.Write((byte)gate.Delayed);
                    }
                }
            }

            return Convert.ToBase64String(stream.ToArray());
        }

        // Rejects anything malformed rather than handing the simulation a netlist that indexes outside its nets.
        public static bool TryDecode(string encoded, out Netlist netlist)
        {
            netlist = null;
            if (string.IsNullOrEmpty(encoded))
            {
                return false;
            }

            try
            {
                using var reader = new BinaryReader(new MemoryStream(Convert.FromBase64String(encoded)), Encoding.UTF8);
                int version = reader.ReadInt32();
                if (version < 1 || version > Version)
                {
                    return false;
                }

                string name = reader.ReadString();
                PackageShape shape = PackageShape.Single;
                if (version >= 3 && !PackageShape.TryGet(reader.ReadByte(), reader.ReadByte(), out shape))
                {
                    return false;
                }

                int netCount = reader.ReadInt32();
                if (netCount < 0 || netCount > MaxCount)
                {
                    return false;
                }

                var roles = new SideRole[shape.Connections];
                var nets = new int[shape.Connections];
                for (int side = 0; side < shape.Connections; side++)
                {
                    roles[side] = (SideRole)reader.ReadByte();
                    nets[side] = reader.ReadInt32();
                    if (!Enum.IsDefined(typeof(SideRole), roles[side])
                        || (roles[side] != SideRole.Unused && !IsNet(nets[side], netCount)))
                    {
                        return false;
                    }
                }

                int gateCount = reader.ReadInt32();
                if (gateCount < 0 || gateCount > MaxCount)
                {
                    return false;
                }

                var gates = new Gate[gateCount];
                for (int i = 0; i < gates.Length; i++)
                {
                    var kind = (GateKind)reader.ReadByte();
                    int output = reader.ReadInt32();
                    int inputCount = reader.ReadInt32();
                    if (!Enum.IsDefined(typeof(GateKind), kind) || inputCount != InputCount(kind)
                        || (version < 4 && kind > GateKind.Constant)
                        || (version < 5 && kind > GateKind.VirtualAnalyze)
                        || (version < 6 && kind > GateKind.Latch)
                        || (version < 8 && kind > GateKind.IntegerSubtract))
                    {
                        return false;
                    }

                    var inputs = new int[inputCount];
                    for (int j = 0; j < inputs.Length; j++)
                    {
                        inputs[j] = reader.ReadInt32();
                    }

                    if (!IsNet(output, netCount)
                        || Array.Exists(inputs, input => !IsNet(input, netCount)))
                    {
                        return false;
                    }

                    if (kind == GateKind.Constant)
                    {
                        int value = reader.ReadInt32();
                        string signal = version >= 4 ? reader.ReadString() : "";
                        gates[i] = signal.Length > 0 ? Gate.Constant(output, signal) : Gate.Constant(output, value);
                        continue;
                    }

                    var outputs = new int[Gate.OutputCount(kind)];
                    outputs[0] = output;
                    for (int extra = 1; extra < outputs.Length; extra++)
                    {
                        outputs[extra] = reader.ReadInt32();
                        if (!IsNet(outputs[extra], netCount))
                        {
                            return false;
                        }
                    }

                    int delayed = version >= 7 ? reader.ReadByte() : 0;
                    gates[i] = new Gate(kind, outputs, inputs, delayed);
                }

                netlist = new Netlist(name, shape, netCount, roles, nets, gates);
                return true;
            }
            catch (Exception e) when (e is FormatException or EndOfStreamException or IOException)
            {
                return false;
            }
        }

        public static int InputCount(GateKind kind)
        {
            return Gate.InputCount(kind);
        }

        private static bool IsNet(int net, int netCount)
        {
            return net >= 0 && net < netCount;
        }
    }
}
