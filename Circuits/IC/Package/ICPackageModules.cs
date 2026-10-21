using System.Collections.Generic;
using Game.Core.Map.Simulation;

namespace Circuits.IC.Package
{
    // Side panel for a selected package: the circuit's name, then each side's role and value.
    public class ICPackageModules : SimulationBasedBuildingModuleDataProvider<ICPackageSimulation>
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
            ICPackageSimulation package)
        {
            for (int side = 0; side < Netlist.SideCount; side++)
            {
                string name = $"{package.Netlist.Name}: {(Side)side}";
                switch (package.Netlist.SideRoles[side])
                {
                    case SideRole.Input:
                        yield return new HUDSidePanelModuleWireInfo.Data($"{name} in", package.SideInputs[side]);
                        break;
                    case SideRole.Output:
                        yield return new HUDSidePanelModuleWireInfo.Data($"{name} out", package.SideOutputs[side]);
                        break;
                }
            }
        }
    }
}
