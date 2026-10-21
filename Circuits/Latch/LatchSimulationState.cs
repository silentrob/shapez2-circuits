using Game.Content.Features.Signals;
using Game.Content.Features.Signals.Conductor;
using Game.Core.Serialization;
using Game.Core.Simulation;

namespace Circuits.Latch
{
    // Saved with each latch. The identifier is written into savegames: never rename it.
    [SyncableIdentifier("Circuits.LatchState")]
    public class LatchSimulationState : ISimulationState
    {
        public readonly SignalConductorInputState DataConductorState = new();
        public readonly SignalConductorInputState ResetConductorState = new();
        public ISignal Held = NullSignal.Instance;

        public void Sync(ISerializationVisitor visitor)
        {
            DataConductorState.Sync(visitor);
            ResetConductorState.Sync(visitor);
            visitor.GetSerializer<ISignal>().Sync(ref Held);
        }
    }
}
