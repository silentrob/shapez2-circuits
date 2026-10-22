using System;
using System.Collections.Generic;
using System.Linq;
using Circuits.IC.Library;
using Core.Localization;
using Game.Core.Map.Simulation;
using UnityEngine;

namespace Circuits.IC.Package
{
    // Side panel for a selected chip: its firmware and the controls to flash it, then each side's role and value.
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
            foreach (IHUDSidePanelModuleData module in FirmwareModules(building))
            {
                yield return module;
            }

            // Live side values: the game shows these rows only in its editor (see the spec, section 3.9).
            for (int side = 0; side < package.Netlist.Connections; side++)
            {
                string name = $"{package.Netlist.Name}: {package.Netlist.Shape.Name(side)}";
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

        // The firmware on the chip: its name as the heading, then its revision and what each side does. Then
        // flashing: any firmware in the library onto this chip, and, when the library has a newer revision of
        // the chip's firmware, updating this chip or every chip running it.
        private static IEnumerable<IHUDSidePanelModuleData> FirmwareModules(BuildingModel building)
        {
            ICPackageConfiguration configuration = Firmware.ChipSettings(building);
            Netlist netlist = configuration?.Netlist ?? Netlist.Empty;
            Sprite icon = building.Definition.CustomData.TryGet(out IBuildingDefinitionGroup group) ? group.Icon : null;

            if (FirmwareText.IsEmpty(netlist))
            {
                yield return new HUDSidePanelModuleGenericSection.Data(
                    icon,
                    new RawText("No firmware"),
                    new RawText("Build a board into firmware, then flash it onto this chip."));
            }
            else
            {
                string revision = configuration.Revision > 0 ? $"Revision {configuration.Revision}" : "Built-in test circuit";
                yield return new HUDSidePanelModuleGenericSection.Data(
                    icon,
                    new RawText(netlist.Name),
                    new RawText($"{revision}. {netlist.DescribeSides()}."));
            }

            if (!ICSession.Ready)
            {
                yield break;
            }

            List<CircuitEntry> library = ICSession.Library().Circuits;
            CircuitEntry current = FirmwareText.IsEmpty(netlist) ? null : ICSession.Library().Find(netlist.Name);
            if (current != null && configuration.Revision > 0 && current.Revision > configuration.Revision)
            {
                yield return new HUDSidePanelModuleGenericButton.Data(
                    new RawText($"Update to revision {current.Revision}"),
                    () => Firmware.Flash(new[] { building }, current));

                BuildingModel[] outdated = Firmware.Outdated(current);
                if (outdated.Length > 1)
                {
                    yield return new HUDSidePanelModuleGenericButton.Data(
                        new RawText($"Update all {outdated.Length} chips running {current.Name}"),
                        () => Firmware.Flash(Firmware.Outdated(current), current));
                }
            }


            // Only firmware built on a board of this chip's shape runs on it.
            CircuitEntry[] entries = library.Where(entry => Firmware.ShapeOf(entry).Equals(Firmware.ChipShape(building))).ToArray();
            if (entries.Length == 0)
            {
                yield break;
            }

            int chosen = current != null ? Math.Max(0, Array.IndexOf(entries, current)) : 0;
            yield return new HUDSidePanelModuleDropdownSelector.Data(
                entries.Select(entry => (IText)new RawText($"{entry.Name} (revision {entry.Revision})")),
                chosen,
                index => chosen = index);
            yield return new HUDSidePanelModuleGenericButton.Data(
                new RawText("Flash firmware"),
                () => Firmware.Flash(new[] { building }, entries[chosen]));
        }
    }
}
