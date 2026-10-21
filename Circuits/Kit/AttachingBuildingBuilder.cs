using System.Linq;
using ShapezShifter.Flow;

namespace Circuits.Kit
{
    // Wraps a ShapezShifter building builder to attach extra custom data to the definition it builds, for data
    // ShapezShifter's fluent builder has no step for, such as a building configuration factory.
    //
    // ShapezShifter creates the definition once but registers it again every time the game builds a map (the
    // menu's background scene, then each savegame), so data is attached only if it is not there already: the
    // game refuses to look up data of a type attached twice.
    public class AttachingBuildingBuilder : IBuildingBuilder
    {
        private readonly IBuildingBuilder Inner;
        private readonly object[] Data;

        public AttachingBuildingBuilder(IBuildingBuilder inner, params object[] data)
        {
            Inner = inner;
            Data = data;
        }

        public BuildingDefinition BuildAndRegister(BuildingDefinitionGroup group, GameBuildings gameBuildings)
        {
            BuildingDefinition definition = Inner.BuildAndRegister(group, gameBuildings);
            foreach (object data in Data)
            {
                if (!definition.CustomData.All.Contains(data))
                {
                    definition.CustomData.Attach(data);
                }
            }

            return definition;
        }
    }
}
