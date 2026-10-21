using System.Collections.Generic;
using System.Reflection;

namespace Circuits.Kit
{
    // Finds the side panel provider the game registered for a vanilla building, so a copy of that building
    // (VanillaCloneBuilder) can show the same panel, dialogs included. The game registers its own providers
    // before mods add theirs. The lookup keeps them private, hence reflection.
    public static class VanillaSidePanel
    {
        private static readonly FieldInfo Providers =
            typeof(BuildingsModulesLookup).GetField("BuildingModulesMap", BindingFlags.Instance | BindingFlags.NonPublic);

        public static IBuildingModules Of(BuildingsModulesLookup lookup, BuildingDefinitionId vanillaId)
        {
            return ((Dictionary<BuildingDefinitionId, IBuildingModules>)Providers.GetValue(lookup))[vanillaId];
        }
    }
}
