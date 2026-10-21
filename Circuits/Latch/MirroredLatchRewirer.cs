using System.Collections.Generic;
using Circuits.Kit;
using ShapezShifter.Hijack;
using ILogger = Core.Logging.ILogger;

namespace Circuits.Latch
{
    // Gives the mirrored latch variant what ShapezShifter gives the primary one: a simulation and a side panel.
    public class MirroredLatchRewirer : ISimulationSystemsRewirer, IBuildingModulesRewirer
    {
        private readonly MirroredBuildingBuilder Builder;
        private readonly ILogger Logger;

        public MirroredLatchRewirer(MirroredBuildingBuilder builder, ILogger logger)
        {
            Builder = builder;
            Logger = logger;
        }

        public void ModifySimulationSystems(
            ICollection<ISimulationSystem> simulationSystems,
            SimulationSystemsDependencies dependencies)
        {
            if (Builder.Mirrored != null)
            {
                simulationSystems.Add(
                    new AtomicStatefulBuildingSimulationSystem<LatchSimulation, LatchSimulationState>(
                        simulationFactory: new LatchSimulationFactory(),
                        buildingDefinitionId: Builder.Mirrored.Id,
                        logger: Logger));
            }
        }

        public void AddModules(BuildingsModulesLookup modulesLookup)
        {
            if (Builder.Mirrored != null)
            {
                modulesLookup.AddModule(
                    variant: Builder.Mirrored.Id,
                    buildingSimulationData: Builder.Mirrored,
                    buildingModulesProvider: new LatchBuildingModules());
            }
        }
    }
}
