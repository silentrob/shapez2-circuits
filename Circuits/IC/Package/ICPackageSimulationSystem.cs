using Core.Logging;

namespace Circuits.IC.Package
{
    // Creates a package simulation for every placed package, as the game's atomic building systems do, but
    // with two-way sides. Milestone 1 runs one fixed netlist; milestone 2 reads it from the building.
    public class ICPackageSimulationSystem : AtomicBuildingSimulationSystem<ICConnectableSimulation>
    {
        private readonly Netlist Netlist;

        public ICPackageSimulationSystem(BuildingDefinitionId definitionId, Netlist netlist, ILogger logger)
            : base(definitionId, logger)
        {
            Netlist = netlist;
        }

        protected override ICConnectableSimulation CreateConnectableSimulation(BuildingInstance building)
        {
            var state = building.State.New<ICPackageSimulationState>();
            return new ICConnectableSimulation(building, new ICPackageSimulation(state, Netlist));
        }
    }
}
