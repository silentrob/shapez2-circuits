using System.Collections.Generic;
using System.Reflection;

namespace Circuits.Kit
{
    // Registers a definition the way ShapezShifter's BuildingBuilder does, which reaches the game's private
    // members through a publicizer; here by reflection, as the mod only needs these two.
    public static class GameBuildingsAccess
    {
        private static readonly FieldInfo DefinitionsById =
            typeof(GameBuildings).GetField("_DefinitionsById", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo AddInternalVariant =
            typeof(BuildingDefinitionGroup).GetMethod("AddInternalVariant", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Register(GameBuildings gameBuildings, BuildingDefinitionGroup group, BuildingDefinition definition)
        {
            AddInternalVariant.Invoke(group, new object[] { definition });
            RegisterOutsideGroup(gameBuildings, definition);
        }

        // Registers a definition the game can place, save and load, without listing it among its group's variants:
        // the game's placers handle at most two variants per group (a building and its mirror). The definition
        // still names its group in its custom data, for selection and research.
        public static void RegisterOutsideGroup(GameBuildings gameBuildings, BuildingDefinition definition)
        {
            ((Dictionary<BuildingDefinitionId, IBuildingDefinition>)DefinitionsById.GetValue(gameBuildings))
               .Add(definition.Id, definition);
        }

        // As ShapezShifter's BuildingBuilder binds a definition to its group, for placement and replacement.
        public static void BindToGroup(BuildingDefinition definition, BuildingDefinitionGroup group)
        {
            definition.CustomData.AttachOrReplace(group);
            definition.CustomData.AttachOrReplace(
                new EntityPlacementPreferenceData(
                    autoSnapToConnectors: group.AutoConnect,
                    connectorsAutoSnapScoreMultiplier: group.AutoAttractIOScoreMultiplier));
            definition.CustomData.AttachOrReplace(
                new EntityReplacementPreferenceData(
                    allowNonForcingReplacementByEntitiesInDifferentGroup: group.AllowNonForcingReplacementByOtherBuildings,
                    isTransportBuilding: group.IsTransportBuilding,
                    shouldSkipReplacementIOChecks: group.ShouldSkipReplacementIOChecks));
            definition.CustomData.AttachOrReplace(group.DefaultPreferredPlacementMode);
        }
    }
}
