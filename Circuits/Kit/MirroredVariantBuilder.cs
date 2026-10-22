using ShapezShifter.Flow;

namespace Circuits.Kit
{
    // Gives a building made with ShapezShifter's builder a mirrored variant (the F key): the same building with its
    // own connectors and model, linked both ways as the game links a building and its mirror. For buildings
    // registered through CustomSimulationBuilding, which then gives the variant its simulation and side panel.
    // ShapezShifter reuses the building it built for every map the game builds, so its link is replaced each time;
    // the variant is made afresh.
    public class MirroredVariantBuilder : IBuildingBuilder
    {
        private readonly IBuildingBuilder Inner;
        private readonly BuildingDefinitionId MirroredId;
        private readonly IBuildingConnectorData Connectors;
        private readonly BuildingDrawData DrawData;

        public MirroredVariantBuilder(IBuildingBuilder inner, BuildingDefinitionId mirroredId, IBuildingConnectorData connectors, BuildingDrawData drawData)
        {
            Inner = inner;
            MirroredId = mirroredId;
            Connectors = connectors;
            DrawData = drawData;
        }

        public BuildingDefinition BuildAndRegister(BuildingDefinitionGroup group, GameBuildings gameBuildings)
        {
            BuildingDefinition primary = Inner.BuildAndRegister(group, gameBuildings);

            var mirrored = new BuildingDefinition(MirroredId, Connectors);
            foreach (object data in primary.CustomData.All)
            {
                if (data is not (IBuildingConnectorData or IBuildingDrawData or IBuildingMirroringDefinition))
                {
                    mirrored.CustomData.Attach(data);
                }
            }

            mirrored.CustomData.Attach(Connectors);
            mirrored.CustomData.Attach(DrawData);
            primary.CustomData.AttachOrReplace<IBuildingMirroringDefinition>(new Mirroring(mirrored, isMirrored: false));
            mirrored.CustomData.Attach(new Mirroring(primary, isMirrored: true));
            GameBuildingsAccess.Register(gameBuildings, group, mirrored);
            return primary;
        }

        private class Mirroring : IBuildingMirroringDefinition
        {
            public IBuildingDefinition MirroredDefinition { get; }
            public bool IsMirrored { get; }

            public Mirroring(IBuildingDefinition mirroredDefinition, bool isMirrored)
            {
                MirroredDefinition = mirroredDefinition;
                IsMirrored = isMirrored;
            }
        }
    }
}
