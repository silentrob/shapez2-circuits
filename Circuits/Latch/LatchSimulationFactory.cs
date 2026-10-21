using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace Circuits.Latch
{
    public class LatchSimulationFactory : IFactory<LatchSimulationState, LatchSimulation>
    {
        public LatchSimulation Produce(LatchSimulationState state)
        {
            return new LatchSimulation(state);
        }
    }

    // The latch has no tunable speed, so its configuration is empty. ShapezShifter still registers it
    // as a buffable, which is harmless for an object with no buffable fields.
    public class LatchConfiguration { }

    internal class LatchFactoryBuilder
        : IBuildingSimulationFactoryBuilder<LatchSimulation, LatchSimulationState, LatchConfiguration>
    {
        public IFactory<LatchSimulationState, LatchSimulation> BuildFactory(
            SimulationSystemsDependencies dependencies,
            out LatchConfiguration config)
        {
            config = new LatchConfiguration();
            return new LatchSimulationFactory();
        }
    }
}
