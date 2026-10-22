using Core.Logging;

namespace Circuits.IC.Package
{
    // Creates a package simulation for every placed package, as the game's atomic building systems do, but
    // with two-way sides, running the netlist in the package's configuration.
    // A package of another shape's firmware (which flashing never makes) runs as no firmware.
    public class ICPackageSimulationSystem : AtomicBuildingSimulationSystem<ICConnectableSimulation>
    {
        private readonly PackageShape Shape;
        private readonly IGateResolver Resolver;

        public ICPackageSimulationSystem(BuildingDefinitionId definitionId, PackageShape shape, IGateResolver resolver, ILogger logger)
            : base(definitionId, logger)
        {
            Shape = shape;
            Resolver = resolver;
        }

        protected override ICConnectableSimulation CreateConnectableSimulation(BuildingInstance building)
        {
            Netlist netlist = building.Configuration is ICPackageConfiguration package && package.Netlist.Shape.Equals(Shape)
                ? package.Netlist
                : Netlist.EmptyOf(Shape);
            var state = building.State.New<ICPackageSimulationState>();
            return new ICConnectableSimulation(building, new ICPackageSimulation(state, netlist, Resolver));
        }
    }
}
