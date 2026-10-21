using Game.Content.Features.Signals.Conductor;
using Game.Core.Serialization;
using Game.Core.Simulation;

namespace Circuits.IC.Package
{
    // Saved with each package. The identifier is written into savegames: never rename it.
    [SyncableIdentifier("Circuits.ICPackageState")]
    public class ICPackageSimulationState : ISimulationState
    {
        private const int Version = 1;

        public readonly SignalConductorInputState[] SideInputStates =
        {
            new(), new(), new(), new()
        };

        public void Sync(ISerializationVisitor visitor)
        {
            int version = Version;
            visitor.SyncInt_4(ref version);
            foreach (SignalConductorInputState side in SideInputStates)
            {
                side.Sync(visitor);
            }
        }
    }
}
