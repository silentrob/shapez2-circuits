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

        // Connector data for a single-tile package with one two-way connector per side.
        public static IBuildingConnectorData SingleTile()
        {
            Side[] sides = { Side.North, Side.East, Side.South, Side.West };
            IBuildingIO[] connectors = sides
               .Select(side => (IBuildingIO)new BuildingSignalTwoWay { Position_L = TileVector.Zero, TileDirection = DirectionOf(side) })
               .ToArray();

            var bounds = new LocalTileBounds(min: TileVector.Zero, max: TileVector.Zero);
            return new BuildingConnectorData(
                allInputs: connectors,
                tiles: new[] { TileVector.Zero },
                tileBounds: bounds,
                tileBoundsCenter: LocalVector.Lerp((LocalVector)bounds.Min, (LocalVector)bounds.Max, 0.5f),
                tileDimensions: bounds.Dimensions);
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
