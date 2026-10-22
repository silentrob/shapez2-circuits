using System;
using Game.Content.Features.Signals;
using Circuits.IntGates;
using Circuits.Latch;
using Game.Content.Features.Signals.Conductor;

namespace Circuits.IC
{
    // What a netlist needs from the game beyond its signal types: the shape operations its virtual processors run,
    // and the signals its non-number constants hold. A package gets one from the game (GateResolver); without one,
    // those gates drive nothing.
    public interface IGateResolver
    {
        // IItemOperation1In1Out, 1In2Out, 2In1Out or 2In2Out, by the kind's inputs and outputs; null if unknown.
        IItemOperation Operation(GateKind kind);

        // A Constant gate's serialized signal; NullSignal if it cannot be read.
        ISignal Constant(string signal);
    }

    // Runs a netlist for one signal tick, matching the vanilla gates, virtual processors and the Circuits latch,
    // whose values are kept in the array the package saves (Held, one per latch, in gate order). Boolean gates output
    // IntegerSignal 1 or 0 from truthiness, virtual processors run their shape operation when every input holds
    // an item (as vanilla's Virtual*Simulation does) and drive nothing otherwise, and a net driven by several
    // sources merges them with the game's own rule (nothing plus X is X, two different values make a Conflict).
    // Everything the game resolves is looked up once, here; a tick allocates nothing.
    public sealed class NetlistEvaluator
    {
        private readonly Netlist Netlist;
        private readonly ISignal[] Nets;
        private readonly IItemOperation[] Operations;
        private readonly ISignal[] Constants;
        private readonly int[] LatchSlots;

        // What each latch holds, carried from tick to tick; a package passes the array its state saves.
        public readonly ISignal[] Held;

        public NetlistEvaluator(Netlist netlist, IGateResolver resolver = null, ISignal[] held = null)
        {
            Netlist = netlist;
            Nets = new ISignal[netlist.NetCount];
            Operations = new IItemOperation[netlist.Gates.Length];
            Constants = new ISignal[netlist.Gates.Length];
            LatchSlots = new int[netlist.Gates.Length];
            int latches = 0;
            for (int i = 0; i < netlist.Gates.Length; i++)
            {
                Gate gate = netlist.Gates[i];
                if (gate.Kind == GateKind.Latch)
                {
                    LatchSlots[i] = latches++;
                }
                else if (Gate.IsVirtual(gate.Kind))
                {
                    Operations[i] = resolver?.Operation(gate.Kind);
                }
                else if (gate.Kind == GateKind.Constant)
                {
                    Constants[i] = gate.Signal == null
                        ? IntegerSignal.Get(gate.Value)
                        : resolver?.Constant(gate.Signal) ?? NullSignal.Instance;
                }
            }

            Held = held != null && held.Length == latches ? held : new ISignal[latches];
            for (int slot = 0; slot < Held.Length; slot++)
            {
                Held[slot] ??= NullSignal.Instance;
            }
        }

        // sideInputs: the value arriving on each side. sideOutputs: filled with what each side drives; sides
        // that are not outputs drive nothing, which merges away on the wire they touch.
        public void Evaluate(ISignal[] sideInputs, ISignal[] sideOutputs)
        {
            Array.Fill(Nets, NullSignal.Instance);

            for (int side = 0; side < Netlist.Connections; side++)
            {
                if (Netlist.SideRoles[side] == SideRole.Input)
                {
                    Drive(Netlist.SideNets[side], sideInputs[side]);
                }
            }

            for (int i = 0; i < Netlist.Gates.Length; i++)
            {
                Gate gate = Netlist.Gates[i];
                if (gate.Kind == GateKind.Latch)
                {
                    int slot = LatchSlots[i];
                    Held[slot] = LatchRule.Next(Held[slot], Nets[gate.Inputs[0]], Nets[gate.Inputs[1]]);
                    Drive(gate.Output, Held[slot]);
                }
                else if (Gate.IsVirtual(gate.Kind))
                {
                    RunVirtual(gate, Operations[i]);
                }
                else
                {
                    Drive(gate.Output, gate.Kind == GateKind.Constant ? Constants[i] : Compute(gate));
                }
            }

            for (int side = 0; side < Netlist.Connections; side++)
            {
                sideOutputs[side] = Netlist.SideRoles[side] == SideRole.Output
                    ? Nets[Netlist.SideNets[side]]
                    : NullSignal.Instance;
            }
        }

        private void Drive(int net, ISignal value)
        {
            Nets[net] = SignalUtils.GetMergedValue(Nets[net], value);
        }

        private ISignal Compute(Gate gate)
        {
            if (Gate.IsInteger(gate.Kind))
            {
                IntegerOp op = Gate.IntegerOpOf(gate.Kind);
                return IntegerOps.Apply(op, Nets[gate.Inputs[0]], IntegerOps.TakesTwo(op) ? Nets[gate.Inputs[1]] : NullSignal.Instance);
            }

            if (gate.Kind == GateKind.Transistor)
            {
                return Nets[gate.Inputs[1]].IsTruthy() ? Nets[gate.Inputs[0]] : NullSignal.Instance;
            }

            bool a = Nets[gate.Inputs[0]].IsTruthy();
            return gate.Kind switch
            {
                GateKind.And => IntegerSignal.Get(a && Nets[gate.Inputs[1]].IsTruthy()),
                GateKind.Or => IntegerSignal.Get(a || Nets[gate.Inputs[1]].IsTruthy()),
                GateKind.Xor => IntegerSignal.Get(a ^ Nets[gate.Inputs[1]].IsTruthy()),
                GateKind.Not => IntegerSignal.Get(!a),
                _ => throw new ArgumentOutOfRangeException(nameof(gate), gate.Kind, null)
            };
        }

        private void RunVirtual(Gate gate, IItemOperation operation)
        {
            ISignal first = NullSignal.Instance, second = NullSignal.Instance;
            if (Nets[gate.Inputs[0]].TryGetItem(out IItem a))
            {
                switch (operation)
                {
                    case IItemOperation1In1Out one when one.TryExecute(a, out IItem output):
                        first = output.GetSignal();
                        break;
                    case IItemOperation1In2Out split when split.TryExecute(a, out IItem output1, out IItem output2):
                        first = output1.GetSignal();
                        second = output2.GetSignal();
                        break;
                    case IItemOperation2In1Out join when Nets[gate.Inputs[1]].TryGetItem(out IItem b)
                                                         && join.TryExecute(a, b, out IItem output):
                        first = output.GetSignal();
                        break;
                    case IItemOperation2In2Out both when Nets[gate.Inputs[1]].TryGetItem(out IItem b)
                                                         && both.TryExecute(a, b, out IItem output1, out IItem output2):
                        first = output1.GetSignal();
                        second = output2.GetSignal();
                        break;
                }
            }

            Drive(gate.Outputs[0], first);
            if (gate.Outputs.Length > 1)
            {
                Drive(gate.Outputs[1], second);
            }
        }
    }
}
