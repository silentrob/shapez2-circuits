using Game.Content.Features.Signals;
using Game.Content.Features.Signals.Conductor;
using Game.Content.Features.Signals.Simulation;
using Game.Content.Features.Signals.Tick;
using Game.Core.Simulation;

namespace Circuits.IC.Package
{
    // Runs a netlist behind its package's two-way sides (one per connection). Every side has both a receiving buffer and a sending output;
    // the netlist decides per side which one matters (see TwoWaySideConnector). Mirrors the vanilla gate tick
    // loop, so a package adds no delay.
    public class ICPackageSimulation : Simulation<ICPackageSimulationState>, IUpdatableSimulation
    {
        public readonly Netlist Netlist;
        public readonly SignalConductorInput[] SideInputs;
        public readonly SignalConductorOutput[] SideOutputs;

        private readonly NetlistEvaluator Evaluator;
        private readonly ISignal[] Arriving;
        private readonly ISignal[] Leaving;

        public ICPackageSimulation(ICPackageSimulationState state, Netlist netlist, IGateResolver resolver) : base(state)
        {
            Netlist = netlist;
            // The evaluator keeps latch and loop values in the state's arrays, so they are saved with the package.
            Evaluator = new NetlistEvaluator(netlist, resolver, state.Held, state.Previous);
            state.Held = Evaluator.Held;
            state.Previous = Evaluator.Previous;
            SideInputs = new SignalConductorInput[netlist.Connections];
            SideOutputs = new SignalConductorOutput[netlist.Connections];
            Arriving = new ISignal[netlist.Connections];
            Leaving = new ISignal[netlist.Connections];
            for (int side = 0; side < Netlist.Connections; side++)
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
                for (int side = 0; side < Netlist.Connections; side++)
                {
                    SideInputs[side].TryPopSignal(startTicks, tick, out Arriving[side]);
                }

                Evaluator.Evaluate(Arriving, Leaving);

                for (int side = 0; side < Netlist.Connections; side++)
                {
                    SideOutputs[side].PushSignal(Leaving[side], startTicks, tick);
                }
            }
        }
    }
}
