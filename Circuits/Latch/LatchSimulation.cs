using Game.Content.Features.Signals;
using Game.Content.Features.Signals.Conductor;
using Game.Content.Features.Signals.Connections;
using Game.Content.Features.Signals.Simulation;
using Game.Content.Features.Signals.Tick;
using Game.Core.Simulation;

namespace Circuits.Latch
{
    // Holds the last truthy signal seen on Data until Reset is truthy. Gates output 0 for false, so only
    // truthy values are captured: from gates this is a set/reset latch, from a belt reader it remembers
    // the last shape after the belt empties. Mirrors the vanilla LogicGate2In1OutSimulation tick loop.
    public class LatchSimulation : Simulation<LatchSimulationState>, ISignalSimulation, IUpdatableSimulation
    {
        public readonly SignalConductorInput DataConductor;
        public readonly SignalConductorInput ResetConductor;
        public readonly SignalConductorOutput OutputConductor;

        public int NumSignalProviders => 1;
        public int NumSignalReceivers => 2;

        public LatchSimulation(LatchSimulationState state) : base(state)
        {
            DataConductor = new SignalConductorInput(state.DataConductorState);
            ResetConductor = new SignalConductorInput(state.ResetConductorState);
            OutputConductor = new SignalConductorOutput();
        }

        // Receiver indices follow the order of the wire inputs in the building's connector data.
        public ISignalReceiver GetSignalReceiver(int index)
        {
            return index == 0 ? DataConductor : ResetConductor;
        }

        public ISignalProvider GetSignalProvider(int index)
        {
            return OutputConductor;
        }

        public void Update(Ticks startTicks, Ticks deltaTicks)
        {
            int signals = SignalSimulation.GetAmountOfSignalsThisUpdate(startTicks, deltaTicks);
            SignalTicks first = SignalTicks.FromTicks(startTicks);
            for (int i = 0; i < signals; i++)
            {
                SignalTicks tick = new(first.NumOfTicks + i);
                DataConductor.TryPopSignal(startTicks, tick, out ISignal data);
                ResetConductor.TryPopSignal(startTicks, tick, out ISignal reset);
                State.Held = Next(State.Held, data, reset);
                OutputConductor.PushSignal(State.Held, startTicks, tick);
            }
        }

        public static ISignal Next(ISignal held, ISignal data, ISignal reset)
        {
            if (reset.IsTruthy())
            {
                return NullSignal.Instance;
            }

            return data.IsTruthy() ? data : held;
        }
    }
}
