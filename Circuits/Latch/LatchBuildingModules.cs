using System.Collections.Generic;
using Game.Core.Map.Simulation;

namespace Circuits.Latch
{
    // Side panel for a selected latch: the current value on each pin, as the vanilla gates show.
    public class LatchBuildingModules : SimulationBasedBuildingModuleDataProvider<LatchSimulation>
    {
        public override IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IBuildingDefinition definition)
        {
            foreach (IHUDSidePanelModuleData module in base.GetInfoModules(definition))
            {
                yield return module;
            }

            if (definition.CustomData.TryGet(out IBuildingDefinitionGroup group) && group.StructureOverview != null)
            {
                yield return new HUDSidePanelModuleStructureOverview.Data(group.StructureOverview);
            }
        }

        protected override IEnumerable<IHUDSidePanelModuleData> GetSimulationModules(
            BuildingModel building,
            ILocalizedSimulation localizedSimulation,
            LatchSimulation latch)
        {
            yield return new HUDSidePanelModuleWireInfo.Data("Data", latch.DataConductor);
            yield return new HUDSidePanelModuleWireInfo.Data("Reset", latch.ResetConductor);
            yield return new HUDSidePanelModuleWireInfo.Data("Output", latch.OutputConductor);
        }
    }
}
