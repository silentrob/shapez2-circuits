using System;
using Game.Content.Features.Signals;
using Game.Content.Features.Signals.Conductor;

namespace Circuits.IC
{
    // Runs a netlist for one signal tick, matching the vanilla gates: boolean gates output IntegerSignal 1 or 0
    // from truthiness, and a net driven by several sources merges them with the game's own rule (nothing plus
    // X is X, two different values make a Conflict). Allocates nothing per tick.
    public sealed class NetlistEvaluator
    {
        private readonly Netlist Netlist;
        private readonly ISignal[] Nets;

        public NetlistEvaluator(Netlist netlist)
        {
            Netlist = netlist;
            Nets = new ISignal[netlist.NetCount];
        }

        // sideInputs: the value arriving on each side. sideOutputs: filled with what each side drives; sides
        // that are not outputs drive nothing, which merges away on the wire they touch.
        public void Evaluate(ISignal[] sideInputs, ISignal[] sideOutputs)
        {
            Array.Fill(Nets, NullSignal.Instance);

            for (int side = 0; side < Netlist.SideCount; side++)
            {
                if (Netlist.SideRoles[side] == SideRole.Input)
                {
                    Drive(Netlist.SideNets[side], sideInputs[side]);
                }
            }

            foreach (Gate gate in Netlist.Gates)
            {
                Drive(gate.Output, Compute(gate));
            }

            for (int side = 0; side < Netlist.SideCount; side++)
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
            switch (gate.Kind)
            {
                case GateKind.Constant:
                    return IntegerSignal.Get(gate.Value);
                case GateKind.Transistor:
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
    }
}
