using System.Collections.Generic;
using Circuits.Kit;
using ShapezShifter.Flow;

namespace Circuits.IC.Package
{
    // A chip declares a connector only where its firmware connects, so wires dock only there, as they would on a
    // vanilla building. A building's connectors are fixed per definition, so each shape gets one hidden variant
    // per set of used connections (a bit per connection: 16 for 1x1, 64 for 2x1, 256 for 2x2), all in the
    // chip's group, and flashing places the variant that matches the firmware (Firmware.Flash).
    //
    // The variant with every connection keeps the chip's original id, so chips saved before variants still load,
    // and is the one the toolbar places: the game's placers take at most two variants per group, so the others
    // are registered outside the group's variant list (they still name the group, so they are chips).
    public static class ChipVariants
    {
        public static string Name(PackageShape shape)
        {
            return $"CircuitsICPackage{shape}";
        }

        public static int AllConnections(PackageShape shape)
        {
            return (1 << shape.Connections) - 1;
        }

        public static BuildingDefinitionId Id(PackageShape shape, int connections)
        {
            return new BuildingDefinitionId(
                connections == AllConnections(shape) ? Name(shape) : $"{Name(shape)}Uses{connections}");
        }

        // The connections a netlist uses, a bit per connection.
        public static int ConnectionsOf(Netlist netlist)
        {
            int connections = 0;
            for (int side = 0; side < netlist.Connections; side++)
            {
                if (netlist.SideRoles[side] != SideRole.Unused)
                {
                    connections |= 1 << side;
                }
            }

            return connections;
        }

        public static bool Uses(int connections, int connection)
        {
            return (connections & (1 << connection)) != 0;
        }
    }

    // Registers a chip shape's variants (see ChipVariants) around the chip ShapezShifter builds, which declares
    // every connection and is the one returned (placed from the toolbar). The others are made afresh for every
    // map the game builds, as the game expects new definitions for each, and listed on it (BuildingVariants) so
    // CustomSimulationBuilding gives each its simulation and side panel.
    public class ChipVariantsBuilder : IBuildingBuilder
    {
        private readonly IBuildingBuilder Inner;
        private readonly PackageShape Shape;
        private readonly IBuildingCustomDrawData CustomDrawData;

        public ChipVariantsBuilder(IBuildingBuilder inner, PackageShape shape, IBuildingCustomDrawData customDrawData)
        {
            Inner = inner;
            Shape = shape;
            CustomDrawData = customDrawData;
        }

        public BuildingDefinition BuildAndRegister(BuildingDefinitionGroup group, GameBuildings gameBuildings)
        {
            BuildingDefinition every = Inner.BuildAndRegister(group, gameBuildings);

            var others = new List<BuildingDefinition>();
            for (int connections = 0; connections < ChipVariants.AllConnections(Shape); connections++)
            {
                IBuildingConnectorData connectors = BuildingSignalTwoWay.For(Shape, connections);
                var variant = new BuildingDefinition(ChipVariants.Id(Shape, connections), connectors);
                foreach (object data in every.CustomData.All)
                {
                    if (data is not (IBuildingConnectorData or IBuildingDrawData or BuildingVariants))
                    {
                        variant.CustomData.Attach(data);
                    }
                }

                variant.CustomData.Attach(connectors);
                variant.CustomData.Attach(ICPackageModel.DrawData(Shape, connections, CustomDrawData));
                GameBuildingsAccess.RegisterOutsideGroup(gameBuildings, variant);
                others.Add(variant);
            }

            // The chip ShapezShifter built is reused for every map, so this replaces the last map's list.
            every.CustomData.AttachOrReplace(new BuildingVariants(others));
            return every;
        }
    }
}
