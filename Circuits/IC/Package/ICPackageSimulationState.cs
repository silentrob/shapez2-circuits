using System.Linq;
using Game.Content.Features.Signals.Conductor;
using Game.Core.Serialization;
using Game.Core.Simulation;

namespace Circuits.IC.Package
{
    // Saved with each package: what is arriving on each connection. Sized for the largest package; a smaller
    // one uses the first of them. The identifier is written into savegames: never rename it.
    //   1: four connections (1x1 packages only).
    //   2: MaxConnections connections.
    [SyncableIdentifier("Circuits.ICPackageState")]
    public class ICPackageSimulationState : ISimulationState
    {
        private const int Version = 2;

        public const int MaxConnections = 8;

        public readonly SignalConductorInputState[] SideInputStates =
            Enumerable.Range(0, MaxConnections).Select(_ => new SignalConductorInputState()).ToArray();

        public void Sync(ISerializationVisitor visitor)
        {
            int version = Version;
            visitor.SyncInt_4(ref version);
            int count = version >= 2 ? MaxConnections : Netlist.SideCount;
            for (int side = 0; side < count; side++)
            {
                SideInputStates[side].Sync(visitor);
            }
        }
    }
}
