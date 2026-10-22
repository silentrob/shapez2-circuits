using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Content.Buildings;
using Game.Core.Coordinates;
using Game.Placement.Data;
using Game.Placement.Processing;
using MonoMod.RuntimeDetour;

namespace Circuits.IC.Board
{
    // Dragging a wire into a board's notch places an IC pin there, as dragging one into a machine platform's
    // notch places a wire transmitter: the game's wire placer turns a wire's end in a notch into a transmitter
    // (PathAtNotchesUpgradeToPortsProcessor), and on an IC Board this swaps it for the pin. A wire dragged out
    // towards the notch becomes an Output Pin (the game's sender), one dragged in from it an Input Pin (its
    // receiver). The pin's wire end goes where the wire meets the notch, facing into the board, with the pin
    // running along the notch whichever way fits (its mirrored variant runs the other way). A wire meeting one of
    // the notch's two middle tiles leaves no room for a 4x1 pin either way, so it gets none.
    public sealed class NotchPinPlacement : IDisposable
    {
        private static readonly BuildingDefinitionGroupId Sender = new("WireTransmitterSenderVariant");
        private static readonly BuildingDefinitionGroupId Receiver = new("WireTransmitterReceiverVariant");

        private readonly Hook Hook;

        public NotchPinPlacement()
        {
            Hook = new Hook(
                typeof(PathAtNotchesUpgradeToPortsProcessor).GetMethod(nameof(PathAtNotchesUpgradeToPortsProcessor.Process)),
                new Action<Action<PathAtNotchesUpgradeToPortsProcessor, IPlacementData, PlacementInputHolder, IMapModel, IReadOnlyMapLayoutModel, IPlacementErrors>,
                    PathAtNotchesUpgradeToPortsProcessor, IPlacementData, PlacementInputHolder, IMapModel, IReadOnlyMapLayoutModel, IPlacementErrors>(
                    (original, self, placement, input, realMap, virtualMap, errors) =>
                    {
                        original(self, placement, input, realMap, virtualMap, errors);
                        SwapTransmittersForPins(placement, realMap);
                    }));
        }

        public void Dispose()
        {
            Hook.Dispose();
        }

        private static void SwapTransmittersForPins(IPlacementData placement, IMapModel map)
        {
            var buildings = new HashSet<BuildingPlacement>();
            placement.GetAllBuildings(buildings);
            foreach (BuildingPlacement building in buildings)
            {
                if (!building.Descriptor.Definition.CustomData.TryGet(out IBuildingDefinitionGroup group)
                    || !(group.Id.Equals(Sender) || group.Id.Equals(Receiver)))
                {
                    continue;
                }

                GlobalTileCoordinate position = building.Descriptor.Transform.Position;
                if (!map.TryGetIsland(position, out IslandModel island) || !BoardIsland.IsBoard(island.DefinitionId))
                {
                    continue;
                }

                if (TryPin(island, position, output: group.Id.Equals(Sender), out BuildingDescriptor pin))
                {
                    placement.ReplaceBuildingPlacement(new BuildingPlacement(pin, building.PlacementAllowability, building.PlacementIndex));
                }
                else
                {
                    placement.RemoveBuildingPlacement(building);
                }
            }
        }

        private static bool TryPin(IslandModel island, GlobalTileCoordinate position, bool output, out BuildingDescriptor pin)
        {
            pin = default;
            var layout = new IslandLayoutQuery(island.ToDescriptor(), island.Map.MaxBuildingLayer);
            ChunkDirection? notch = layout.GetNotchFlag_G(in position);
            if (!notch.HasValue)
            {
                return false;
            }

            // A pin's wire end faces west unrotated, so it faces into the board when its east faces out.
            TileDirection outward = notch.Value.ToTileDirection();
            GridRotation rotation = GridRotation.RotationsInClockwiseOrder.First(r => r.ToTileDirection() == outward);
            var transform = new GlobalTileTransform(position, rotation);
            string name = output ? "CircuitsICOutputNotchPin" : "CircuitsICInputNotchPin";

#pragma warning disable CS0618 // Obsolete static access, as vanilla's own static helpers use
            GameBuildings buildings = StaticGameCoreAccessor.G.Mode.Buildings;
#pragma warning restore CS0618
            foreach (string variant in new[] { name, name + "Mirrored" })
            {
                if (!buildings.TryGetDefinition(new BuildingDefinitionId(variant), out IBuildingDefinition definition))
                {
                    continue;
                }

                bool fits = definition.CustomData.Get<IBuildingConnectorData>().Tiles
                   .All(tile =>
                    {
                        GlobalTileCoordinate at = tile.ToGlobal(transform);
                        return layout.GetNotchFlag_G(in at) == notch;
                    });
                if (fits)
                {
                    definition.TryCreateConfiguration(out IBuildingConfiguration configuration);
                    pin = new BuildingDescriptor(definition, in transform, configuration, new SimulationStateContainer());
                    return true;
                }
            }

            return false;
        }
    }
}
