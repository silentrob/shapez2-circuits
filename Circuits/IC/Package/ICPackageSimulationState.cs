using System;
using System.Linq;
using Game.Content.Features.Signals;
using Game.Content.Features.Signals.Conductor;
using Game.Core.Serialization;
using Game.Core.Simulation;

namespace Circuits.IC.Package
{
    // Saved with each package: what is arriving on each connection. Sized for the largest package; a smaller
    // one uses the first of them. The identifier is written into savegames: never rename it.
    //   1: four connections (1x1 packages only).
    //   2: MaxConnections connections.
    //   3: adds what each latch in the firmware holds, after the connections.
    //   4: adds each feedback loop's value from the previous tick, after the latches.
    [SyncableIdentifier("Circuits.ICPackageState")]
    public class ICPackageSimulationState : ISimulationState
    {
        private const int Version = 4;

        public const int MaxConnections = 8;

        public readonly SignalConductorInputState[] SideInputStates =
            Enumerable.Range(0, MaxConnections).Select(_ => new SignalConductorInputState()).ToArray();

        // What each latch in the firmware holds (see NetlistEvaluator.Held); sized by the simulation.
        public ISignal[] Held = Array.Empty<ISignal>();

        // Each feedback loop's value from the previous tick (see NetlistEvaluator.Previous); sized likewise.
        public ISignal[] Previous = Array.Empty<ISignal>();

        // A state saves through the input attached to it. The simulation attaches one to each connection it uses;
        // these keep the rest (a smaller package's) saveable.
        public ICPackageSimulationState()
        {
            foreach (SignalConductorInputState side in SideInputStates)
            {
                _ = new SignalConductorInput(side);
            }
        }

        public void Sync(ISerializationVisitor visitor)
        {
            int version = Version;
            visitor.SyncInt_4(ref version);
            int count = version >= 2 ? MaxConnections : Netlist.SideCount;
            for (int side = 0; side < count; side++)
            {
                SideInputStates[side].Sync(visitor);
            }

            if (version >= 3)
            {
                Held = SyncSignals(visitor, Held);
            }

            if (version >= 4)
            {
                Previous = SyncSignals(visitor, Previous);
            }
        }

        // Fills the array in place when its size matches, so a simulation already holding it sees what was read.
        private static ISignal[] SyncSignals(ISerializationVisitor visitor, ISignal[] signals)
        {
            int count = signals.Length;
            visitor.SyncInt_4(ref count);
            if (count != signals.Length)
            {
                signals = new ISignal[count];
            }

            for (int slot = 0; slot < count; slot++)
            {
                visitor.GetSerializer<ISignal>().Sync(ref signals[slot]);
            }

            return signals;
        }
    }
}
