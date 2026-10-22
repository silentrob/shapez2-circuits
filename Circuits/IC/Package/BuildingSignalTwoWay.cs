using System;
using System.Linq;
using Game.Core.Coordinates;

namespace Circuits.IC.Package
{
    // The declared connector for a package side: neither input nor output, because which one it is depends on
    // the circuit loaded. It is deliberately not a BuildingSignalJunction: the wire system gathers junctions
    // (with an `is` check) and would join every side of the package into one network. Vanilla inputs and
    // outputs only accept their opposite or a junction, so TwoWayCompatibilityHooks teaches them to accept
    // this too; without that the map shows a connection warning on anything placed against a package.
    [Serializable]
    public class BuildingSignalTwoWay : BuildingSignalIO
    {
        public override BuildingSignalIOType IOType => BuildingSignalIOType.Wire;

        public override bool IsCompatibleConnection(BuildingBaseIO otherIO)
        {
            return otherIO is BuildingSignalIO;
        }

        // Connector data for a package of the given shape: one two-way connector per connection, in connection
        // order, on the tile and edge each connection sits on (see PackageShape).
        public static IBuildingConnectorData For(PackageShape shape)
        {
            return For(shape, ChipVariants.AllConnections(shape));
        }

        // As above, for only the connections given (a bit per connection; see ChipVariants).
        public static IBuildingConnectorData For(PackageShape shape, int connections)
        {
            IBuildingIO[] connectors = Enumerable.Range(0, shape.Connections)
               .Where(connection => ChipVariants.Uses(connections, connection))
               .Select(connection =>
                {
                    (int x, int y) = shape.Tile(connection);
                    return (IBuildingIO)new BuildingSignalTwoWay
                    {
                        Position_L = new TileVector(x, y, 0),
                        TileDirection = DirectionOf(shape.Locate(connection).Edge)
                    };
                })
               .ToArray();

            TileVector[] tiles =
                (from y in Enumerable.Range(0, shape.Height)
                 from x in Enumerable.Range(0, shape.Width)
                 select new TileVector(x, y, 0)).ToArray();
            var bounds = new LocalTileBounds(min: TileVector.Zero, max: new TileVector(shape.Width - 1, shape.Height - 1, 0));
            return new BuildingConnectorData(
                allInputs: connectors,
                tiles: tiles,
                tileBounds: bounds,
                tileBoundsCenter: LocalVector.Lerp((LocalVector)bounds.Min, (LocalVector)bounds.Max, 0.5f),
                tileDimensions: bounds.Dimensions);
        }

        public static Side SideOf(TileDirection direction)
        {
            if (direction == TileDirection.North) return Side.North;
            if (direction == TileDirection.East) return Side.East;
            if (direction == TileDirection.South) return Side.South;
            return Side.West;
        }

        // The connection a connector is, by its tile and edge (see PackageShape).
        public int ConnectionIn(PackageShape shape)
        {
            Side edge = SideOf(TileDirection);
            return shape.IndexOf(edge, edge is Side.North or Side.South ? Position_L.x : Position_L.y);
        }

        public static TileDirection DirectionOf(Side side)
        {
            return side switch
            {
                Side.North => TileDirection.North,
                Side.East => TileDirection.East,
                Side.South => TileDirection.South,
                _ => TileDirection.West
            };
        }
    }
}
