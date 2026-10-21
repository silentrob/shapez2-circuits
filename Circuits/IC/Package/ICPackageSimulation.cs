using Game.Content.Features.Signals;
using Game.Content.Features.Signals.Conductor;
using Game.Content.Features.Signals.Simulation;
using Game.Content.Features.Signals.Tick;
using Game.Core.Simulation;

namespace Circuits.IC.Package
{
    // Runs a netlist behind four two-way sides. Every side has both a receiving buffer and a sending output;
    // the netlist decides per side which one matters (see TwoWaySideConnector). Mirrors the vanilla gate tick
    // loop, so a package adds no delay.
    public class ICPackageSimulation : Simulation<ICPackageSimulationState>, IUpdatableSimulation
    {
        public readonly Netlist Netlist;
        public readonly SignalConductorInput[] SideInputs = new SignalConductorInput[Netlist.SideCount];
        public readonly SignalConductorOutput[] SideOutputs = new SignalConductorOutput[Netlist.SideCount];

        private readonly NetlistEvaluator Evaluator;
        private readonly ISignal[] Arriving = new ISignal[Netlist.SideCount];
        private readonly ISignal[] Leaving = new ISignal[Netlist.SideCount];

        public ICPackageSimulation(ICPackageSimulationState state, Netlist netlist) : base(state)
        {
            Netlist = netlist;
            Evaluator = new NetlistEvaluator(netlist);
            for (int side = 0; side < Netlist.SideCount; side++)
            {
                SideInputs[side] = new SignalConductorInput(state.SideInputStates[side]);
                SideOutputs[side] = new SignalConductorOutput();
            }
        }

        public void Update(Ticks startTicks, Ticks deltaTicks)
        {
            int signals = SignalSimulation.GetAmountOfSignalsThisUpdate(startTicks, deltaTicks);
            SignalTicks first = SignalTicks.FromTicks(startTicks);
            for (int i = 0; i < signals; i++)
            {
                SignalTicks tick = new(first.NumOfTicks + i);
                for (int side = 0; side < Netlist.SideCount; side++)
                {
                    SideInputs[side].TryPopSignal(startTicks, tick, out Arriving[side]);
                }

                Evaluator.Evaluate(Arriving, Leaving);

                for (int side = 0; side < Netlist.SideCount; side++)
                {
                    SideOutputs[side].PushSignal(Leaving[side], startTicks, tick);
                }
            }
        }
    }
}
