using System;
using System.Collections.Generic;
using System.Linq;
using Circuits.IC.Board;
using Circuits.IC.Compilation;
using Circuits.IC.Library;
using Circuits.IC.Package;
using Game.Core;
using Game.Core.Content.Buildings;
using Game.Core.Blueprint;
using Game.Core.Coordinates;
using Game.Core.Serialization;

namespace Circuits.IC
{
    // What can be done with firmware, shared by the console commands and the side panels:
    //   build a board's circuit into firmware, keeping the board's layout with it;
    //   flash firmware onto chips (IC packages), or update them to a newer revision;
    //   restore a firmware's layout onto an empty board.
    // Everything that changes the map goes through the game's undoable building action, scheduled between
    // simulation updates. Runs on the main thread.
    public static class Firmware
    {
        public static readonly BuildingDefinitionGroupId ChipGroup = new("CircuitsICPackage1x1Group");

        // Each chip size's group, and the shape of firmware it runs.
        private static readonly (BuildingDefinitionGroupId Group, PackageShape Shape)[] Chips =
        {
            (ChipGroup, PackageShape.Single),
            (new BuildingDefinitionGroupId("CircuitsICPackage2x1Group"), PackageShape.Wide),
            (new BuildingDefinitionGroupId("CircuitsICPackage2x2Group"), PackageShape.Square)
        };

        // The group of chips that run firmware of this shape.
        public static BuildingDefinitionGroupId ChipGroupOf(PackageShape shape)
        {
            return Array.Find(Chips, chip => chip.Shape.Equals(shape)).Group;
        }

        public static bool IsChip(BuildingModel building)
        {
            return ChipShape(building) != null;
        }

        // The shape of firmware a chip runs, or null when the building is not a chip.
        public static PackageShape ChipShape(BuildingModel building)
        {
            return building.Definition.CustomData.TryGet(out IBuildingDefinitionGroup group)
                ? Array.Find(Chips, chip => chip.Group.Equals(group.Id)).Shape
                : null;
        }

        // Whether firmware fits a chip: firmware runs on chips of the shape of the board it was built on.
        public static bool Fits(BuildingModel chip, Netlist netlist)
        {
            return netlist.Shape.Equals(ChipShape(chip));
        }

        public static bool IsBoard(IslandModel island)
        {
            return BoardIsland.IsBoard(island.DefinitionId);
        }

        public static BoardConfiguration BoardSettings(IslandModel board)
        {
            return board.Configuration as BoardConfiguration;
        }

        public static ICPackageConfiguration ChipSettings(BuildingModel chip)
        {
            return chip.Configuration as ICPackageConfiguration;
        }

        // Builds the board's circuit into firmware named after the board. Returns the library entry, or null
        // with the errors.
        public static CircuitEntry Build(IslandModel board, string name, out IReadOnlyList<string> errors)
        {
            var problems = new List<string>();
            IReadOnlyList<Part> parts = BoardReader.Read(board, problems);
            CompileResult result = problems.Count > 0
                ? CompileResult.Failure(problems)
                : BoardCompiler.Compile(name, BoardIsland.ShapeOf(board.DefinitionId), parts);
            errors = result.Errors;
            if (!result.Succeeded)
            {
                return null;
            }

            return ICSession.Library().Store(name, NetlistCodec.Encode(result.Netlist), CaptureLayout(board));
        }

        // Flashes firmware onto chips by replacing each with a new chip holding it (see ICPackageConfiguration).
        // Chips of another shape are left alone; false when none fit.
        public static bool Flash(IReadOnlyCollection<BuildingModel> chips, Netlist netlist, int revision)
        {
            chips = chips.Where(chip => Fits(chip, netlist)).ToList();
            if (chips.Count == 0)
            {
                return false;
            }

            // One netlist shared by every new chip: configurations never change once placed.
            var delete = chips.Select(chip => new DeleteBuildingPayload(chip)).ToList();
            var place = chips
               .Select(chip => new PlaceBuildingPayload(
                    chip.Island.Id,
                    chip.Definition,
                    new ICPackageConfiguration(netlist, revision),
                    chip.Transform_I))
               .ToList();
            return Schedule(new ModifyBuildingsPayload(place, delete));
        }

        public static bool Flash(IReadOnlyCollection<BuildingModel> chips, CircuitEntry entry)
        {
            return NetlistCodec.TryDecode(entry.Netlist, out Netlist netlist) && Flash(chips, netlist, entry.Revision);
        }

        // Chips running an older revision of this firmware, anywhere on the map.
        public static BuildingModel[] Outdated(CircuitEntry entry)
        {
            return ICSession.Player.CurrentMap.Buildings
               .Where(IsChip)
               .Where(chip => ChipSettings(chip) is { } settings
                              && settings.Revision > 0
                              && settings.Revision < entry.Revision
                              && string.Equals(settings.Netlist.Name, entry.Name, StringComparison.OrdinalIgnoreCase)
                              && settings.Netlist.Shape.Equals(ShapeOf(entry)))
               .ToArray();
        }

        // Places a firmware's layout onto an empty board.
        public static bool Restore(IslandModel board, CircuitEntry entry, out string problem)
        {
            if (entry.Layout == null)
            {
                problem = $"{entry.Name} was built before boards were kept, so it has no layout to restore.";
                return false;
            }

            if (board.BuildingsCount > 0)
            {
                problem = "Restore needs an empty board.";
                return false;
            }

            PackageShape shape = BoardIsland.ShapeOf(board.DefinitionId);
            if (!shape.Equals(ShapeOf(entry)))
            {
                problem = $"{entry.Name} was built on a {ShapeOf(entry)} board; this board is {shape}.";
                return false;
            }

            var converter = Converter();
            var place = new List<PlaceBuildingPayload>();
            foreach (LayoutBuilding building in entry.Layout.Buildings)
            {
#pragma warning disable CS0618 // Obsolete static access, as vanilla's own static helpers use
                if (!StaticGameCoreAccessor.G.Mode.Buildings.TryGetDefinition(new BuildingDefinitionId(building.Definition), out IBuildingDefinition definition))
#pragma warning restore CS0618
                {
                    problem = $"{building.Definition} no longer exists in this game.";
                    return false;
                }

                IBuildingConfiguration configuration = null;
                if (building.Configuration != null && definition.TryCreateConfiguration(out configuration))
                {
                    converter.DeserializeByteArrayIntoData(
                        Convert.FromBase64String(building.Configuration),
                        (GameVersion)entry.Layout.GameVersion,
                        configuration.Sync);
                }

                var transform = new IslandTileTransform(
                    new IslandTileCoordinate(building.X, building.Y, building.Z),
                    new GridRotation(building.Rotation));
                place.Add(new PlaceBuildingPayload(board.Id, definition, configuration, transform));
            }

            problem = null;
            return Schedule(new ModifyBuildingsPayload(place));
        }

        // The board shape a firmware was built on: its netlist's package shape.
        public static PackageShape ShapeOf(CircuitEntry entry)
        {
            return NetlistCodec.TryDecode(entry.Netlist, out Netlist netlist) ? netlist.Shape : PackageShape.Single;
        }

        private static BoardLayout CaptureLayout(IslandModel board)
        {
            var converter = Converter();
            var layout = new BoardLayout { GameVersion = (int)GameVersionEnvironment.CurrentVersion };
            foreach (BuildingModel building in board.Buildings)
            {
                IslandTileTransform transform = building.Transform_I;
                layout.Buildings.Add(new LayoutBuilding
                {
                    Definition = building.Definition.Id.Name,
                    X = transform.Position.x,
                    Y = transform.Position.y,
                    Z = transform.Position.z,
                    Rotation = (byte)transform.Rotation.Value,
                    Configuration = building.Configuration == null
                        ? null
                        : Convert.ToBase64String(converter.SerializeDataIntoByteArray(building.Configuration.Sync))
                });
            }

            return layout;
        }

        private static BlueprintRawDataConverter Converter()
        {
#pragma warning disable CS0618 // as above
            return new BlueprintRawDataConverter(StaticGameCoreAccessor.G.DataSerializers, ICSession.Logger);
#pragma warning restore CS0618
        }

        private static bool Schedule(ModifyBuildingsPayload payload)
        {
            var action = new ActionModifyBuildings(ICSession.Player.CurrentMap, ICSession.Player, payload);
            return ICSession.Actions.TryScheduleAction(action);
        }
    }
}
