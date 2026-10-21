using ShapezShifter.Flow;

namespace Circuits.Kit
{
    // Builds a copy of a vanilla building under a new id and group: same connectors, model, draw data,
    // configuration factory and sound. Running the vanilla simulation (registered by the caller) also gets
    // the vanilla renderer, which the game picks by simulation type, so the copy looks and behaves exactly
    // like the original while the mod can tell it apart. The copy is made at registration, when the game's
    // vanilla definitions exist, and afresh for every map the game builds.
    public class VanillaCloneBuilder : IBuildingBuilder
    {
        private readonly BuildingDefinitionId VanillaId;
        private readonly BuildingDefinitionId CloneId;

        public VanillaCloneBuilder(BuildingDefinitionId vanillaId, BuildingDefinitionId cloneId)
        {
            VanillaId = vanillaId;
            CloneId = cloneId;
        }

        public BuildingDefinition BuildAndRegister(BuildingDefinitionGroup group, GameBuildings gameBuildings)
        {
            IBuildingDefinition vanilla = gameBuildings.GetDefinition(VanillaId);
            var clone = new BuildingDefinition(CloneId, vanilla.CustomData.Get<IBuildingConnectorData>());
            foreach (object data in vanilla.CustomData.All)
            {
                // Group membership and mirroring belong to the vanilla building; the clone gets its own group.
                if (data is not (IBuildingDefinitionGroup or EntityPlacementPreferenceData
                    or EntityReplacementPreferenceData or DefaultPreferredPlacementMode
                    or IBuildingMirroringDefinition))
                {
                    clone.CustomData.Attach(data);
                }
            }

            GameBuildingsAccess.BindToGroup(clone, group);
            GameBuildingsAccess.Register(gameBuildings, group, clone);
            return clone;
        }
    }
}
