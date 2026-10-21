using Core.Logging;

namespace Circuits.IC.Package
{
    // Creates a package simulation for every placed package, as the game's atomic building systems do, but
    // with two-way sides, running the netlist in the package's configuration.
    public class ICPackageSimulationSystem : AtomicBuildingSimulationSystem<ICConnectableSimulation>
    {
        public ICPackageSimulationSystem(BuildingDefinitionId definitionId, ILogger logger)
            : base(definitionId, logger) { }

        protected override ICConnectableSimulation CreateConnectableSimulation(BuildingInstance building)
        {
            Netlist netlist = building.Configuration is ICPackageConfiguration package ? package.Netlist : Netlist.Empty;
            var state = building.State.New<ICPackageSimulationState>();
            return new ICConnectableSimulation(building, new ICPackageSimulation(state, netlist));
        }
    }
}
