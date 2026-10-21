using System.Collections.Generic;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using Game.Core.Simulation;

namespace Circuits.IC.Package
{
    // Places a package simulation in the world, as the game's ConnectableBuildingSimulation does, but gives
    // each side (declared as a BuildingSignalTwoWay) a TwoWaySideConnector instead of a one-way input or
    // output.
    public class ICConnectableSimulation : IConnectableBuildingSimulation
    {
        private readonly List<ISimulationConnector> Connectors = new();
        private readonly IBuildingConnectorData ConnectorData;

        public BuildingInstance Building { get; }
        public GlobalTileTransform Transform => Building.Transform;
        public ISimulation Simulation { get; }
        public GlobalTileBounds TileBounds { get; }
        public GlobalChunkBounds ChunkBounds => TileBounds.ToChunkBounds();
        public int NumConnectors => Connectors.Count;
        public int NumOccupiedChunks => 1;
        public int NumOccupiedTiles => ConnectorData.Tiles.Length;

        public ICConnectableSimulation(BuildingInstance building, ICPackageSimulation simulation)
        {
            ConnectorData = building.Definition.CustomData.Get<IBuildingConnectorData>();
            Building = building;
            Simulation = simulation;
            TileBounds = ConnectorData.TileBounds.ToGlobal(Transform);

            foreach (BuildingSignalTwoWay side in ConnectorData.BuildingConnectorsOfType<BuildingSignalTwoWay>())
            {
                int index = (int)SideOf(side.TileDirection);
                Connectors.Add(
                    new TwoWaySideConnector(
                        simulation.SideInputs[index],
                        simulation.SideOutputs[index],
                        simulation.Netlist.SideRoles[index],
                        side.Pivot(Transform)));
            }
        }

        public ISimulationConnector GetConnector(int index)
        {
            return Connectors[index];
        }

        public GlobalTileCoordinate GetOccupiedTile(int index)
        {
            return ConnectorData.Tiles[index].ToGlobal(Transform);
        }

        public GlobalChunkCoordinate GetOccupiedChunk(int index)
        {
            return Transform.Position.ToChunkCoordinate();
        }

        // Sides are local to the building: the netlist's north is the package's north before rotation.
        private static Side SideOf(TileDirection direction)
        {
            if (direction == TileDirection.North) return Side.North;
            if (direction == TileDirection.East) return Side.East;
            if (direction == TileDirection.South) return Side.South;
            return Side.West;
        }
    }
}
