using System;
using ShapezShifter.Flow;

namespace Circuits.Kit
{
    // As VanillaCloneBuilder, but the copy gets its own footprint, connectors and model, plus a mirrored variant
    // (the F key). It keeps the vanilla building's simulation data and configuration, so the vanilla simulation
    // and renderer still run it. The renderer draws by the custom draw data that the variant's model carries,
    // made from the vanilla one (which a variant may adjust, say to fit its value to the model). The mirrored variant is the primary
    // with north and south swapped, as the game expects; it needs its simulation and side panel registered too
    // (CustomSimulationBuilding does both).
    public class ReshapedCloneBuilder : IBuildingBuilder
    {
        private readonly BuildingDefinitionId VanillaId;
        private readonly Variant Primary;
        private readonly Variant Mirrored;

        // A variant's id, connectors, and model made from the vanilla custom draw data.
        public class Variant
        {
            public readonly BuildingDefinitionId Id;
            public readonly IBuildingConnectorData Connectors;
            public readonly Func<IBuildingCustomDrawData, BuildingDrawData> DrawData;

            public Variant(BuildingDefinitionId id, IBuildingConnectorData connectors, Func<IBuildingCustomDrawData, BuildingDrawData> drawData)
            {
                Id = id;
                Connectors = connectors;
                DrawData = drawData;
            }
        }

        public ReshapedCloneBuilder(BuildingDefinitionId vanillaId, Variant primary, Variant mirrored)
        {
            VanillaId = vanillaId;
            Primary = primary;
            Mirrored = mirrored;
        }

        public BuildingDefinition BuildAndRegister(BuildingDefinitionGroup group, GameBuildings gameBuildings)
        {
            IBuildingDefinition vanilla = gameBuildings.GetDefinition(VanillaId);
            IBuildingCustomDrawData customDrawData = vanilla.CustomData.Get<IBuildingDrawData>().CustomDrawData;

            BuildingDefinition primary = Copy(vanilla, Primary, customDrawData);
            GameBuildingsAccess.BindToGroup(primary, group);
            GameBuildingsAccess.Register(gameBuildings, group, primary);

            BuildingDefinition mirrored = Copy(vanilla, Mirrored, customDrawData);
            GameBuildingsAccess.BindToGroup(mirrored, group);
            primary.CustomData.Attach(new Mirroring(mirrored, isMirrored: false));
            mirrored.CustomData.Attach(new Mirroring(primary, isMirrored: true));
            GameBuildingsAccess.Register(gameBuildings, group, mirrored);

            return primary;
        }

        private static BuildingDefinition Copy(IBuildingDefinition vanilla, Variant variant, IBuildingCustomDrawData customDrawData)
        {
            var copy = new BuildingDefinition(variant.Id, variant.Connectors);
            foreach (object data in vanilla.CustomData.All)
            {
                // Group membership and mirroring belong to the vanilla building; shape and model are the variant's.
                if (data is not (IBuildingDefinitionGroup or EntityPlacementPreferenceData
                    or EntityReplacementPreferenceData or DefaultPreferredPlacementMode
                    or IBuildingMirroringDefinition or IBuildingConnectorData or IBuildingDrawData
                    or IBuildingCustomDrawData))
                {
                    copy.CustomData.Attach(data);
                }
            }

            copy.CustomData.Attach(variant.Connectors);
            BuildingDrawData drawData = variant.DrawData(customDrawData);
            copy.CustomData.Attach(drawData);
            if (drawData.CustomDrawData != null)
            {
                copy.CustomData.Attach(drawData.CustomDrawData);
            }

            return copy;
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
