using System.Collections.Generic;

namespace Circuits.Kit
{
    // Attached to a building's placed definition: the other definitions registered with it (in its group) that
    // run the same simulation and side panel, so CustomSimulationBuilding sets them up too.
    public class BuildingVariants
    {
        public readonly IReadOnlyList<BuildingDefinition> Definitions;

        public BuildingVariants(IReadOnlyList<BuildingDefinition> definitions)
        {
            Definitions = definitions;
        }
    }
}
