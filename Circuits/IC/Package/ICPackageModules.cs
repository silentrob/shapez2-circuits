using System.Collections.Generic;
using Core.Localization;
using Game.Core.Map.Simulation;
using UnityEngine;

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
            yield return CircuitSection(building);

            // Live side values: the game shows these rows only in its editor (see the spec, section 3.9).
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

        // The loaded circuit: its name as the heading, then its revision and what each side does.
        private static IHUDSidePanelModuleData CircuitSection(BuildingModel building)
        {
            var configuration = building.Configuration as ICPackageConfiguration;
            Netlist netlist = configuration?.Netlist ?? Netlist.Empty;
            Sprite icon = building.Definition.CustomData.TryGet(out IBuildingDefinitionGroup group) ? group.Icon : null;

            if (ReferenceEquals(netlist, Netlist.Empty) || netlist.Name == Netlist.Empty.Name)
            {
                return new HUDSidePanelModuleGenericSection.Data(
                    icon,
                    new RawText("No circuit loaded"),
                    new RawText("Select it and use circuits.load in the console (F1)."));
            }

            string revision = configuration.Revision > 0 ? $"Revision {configuration.Revision}" : "Built-in test circuit";
            return new HUDSidePanelModuleGenericSection.Data(
                icon,
                new RawText(netlist.Name),
                new RawText($"{revision}. {netlist.DescribeSides()}."));
        }
    }
}
