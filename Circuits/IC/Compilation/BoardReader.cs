using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content.Features.Signals;
using Game.Core.Content.Buildings;
using Circuits.IC.Board;
using Game.Core.Coordinates;

namespace Circuits.IC.Compilation
{
    // Reads a board platform into the compiler's parts. Runs on the main thread, between simulation updates,
    // and reads only the map, never the simulation.
    //
    // A board is an IC Board platform (1x1, 2x1 or 2x2 chunks). Which package connection a pin stands for is the
    // board edge it sits on and the chunk along that edge, measured in the platform's own frame, so a rotated
    // board still compiles the same circuit. A pin
    // is on an edge when the next tile outward is not buildable, which follows the platform's real outline
    // (its inset border and notches) rather than the chunk's 20x20 square.
    public static class BoardReader
    {
        public static readonly BuildingDefinitionGroupId InputPinGroup = new("CircuitsICInputPinGroup");
        public static readonly BuildingDefinitionGroupId OutputPinGroup = new("CircuitsICOutputPinGroup");
        public static readonly BuildingDefinitionGroupId InputNotchPinGroup = new("CircuitsICInputNotchPinGroup");
        public static readonly BuildingDefinitionGroupId OutputNotchPinGroup = new("CircuitsICOutputNotchPinGroup");

        private static readonly BuildingDefinitionId Bridge = new("WireDefaultBridgeInternalVariant");

        private static readonly Dictionary<string, PartKind> KindsByGroup = new()
        {
            ["WireDefaultVariant"] = PartKind.Wire,
            ["WireBridgeVariant"] = PartKind.Wire,
            ["LogicGateAndVariant"] = PartKind.And,
            ["LogicGateOrVariant"] = PartKind.Or,
            ["LogicGateXOrVariant"] = PartKind.Xor,
            ["LogicGateNotVariant"] = PartKind.Not,
            ["LogicGateIfVariant"] = PartKind.Transistor,
            ["ConstantSignalDefaultVariant"] = PartKind.Constant,
            [InputPinGroup.Id] = PartKind.InputPin,
            [OutputPinGroup.Id] = PartKind.OutputPin,
            [InputNotchPinGroup.Id] = PartKind.InputPin,
            [OutputNotchPinGroup.Id] = PartKind.OutputPin
        };

        private const int ChunkTiles = 20;

        public static IReadOnlyList<Part> Read(IslandModel board, List<string> errors)
        {
            var parts = new List<Part>();
            PackageShape shape = BoardIsland.ShapeOf(board.DefinitionId);
            if (shape == null)
            {
                errors.Add("Circuits are compiled on an IC Board: place one from the Platforms toolbar.");
                return parts;
            }

#pragma warning disable CS0618 // Obsolete as "inconvenient", but it is how the game itself reads a platform's buildable tiles
            IslandLayoutQuery layout = board.LayoutQuery;
#pragma warning restore CS0618
            foreach (BuildingModel building in board.Buildings)
            {
                ChunkTileCoordinate tile = building.Transform.Position.ToChunkCoordinate(board.Transform);
                string label = $"{Describe(building.Definition.Id)} at ({tile.x}, {tile.y})";
                string group = building.Definition.CustomData.TryGet(out IBuildingDefinitionGroup definitionGroup)
                    ? definitionGroup.Id.Id
                    : "";

                if (group.StartsWith("Display"))
                {
                    // Displays help test a board and do nothing inside a package.
                    continue;
                }

                if (!KindsByGroup.TryGetValue(group, out PartKind kind))
                {
                    errors.Add($"{label} can't be part of a circuit yet.");
                    continue;
                }

                Port[] ports = Ports(building);
                switch (kind)
                {
                    case PartKind.Constant:
                        AddConstant(building, label, ports, parts, errors);
                        break;
                    case PartKind.InputPin:
                    case PartKind.OutputPin:
                        IslandChunkData chunk = layout.GetChunk_UNSAFE_I(building.Transform_I.Position);
                        // The tile is counted from the board's first chunk; the chunk's own data counts from it.
                        tile = new ChunkTileCoordinate(
                            tile.x - ChunkTiles * chunk.Chunk_L.x,
                            tile.y - ChunkTiles * chunk.Chunk_L.y,
                            tile.z);
                        bool notchOnly = group == InputNotchPinGroup.Id || group == OutputNotchPinGroup.Id;
                        if (TryEdge(chunk, tile, notchOnly, out Side side, out string problem))
                        {
                            // Which chunk along that edge: chunks count from the west (north and south edges)
                            // or the north (east and west edges), as a package's connections do.
                            int position = side is Side.North or Side.South ? chunk.Chunk_L.x : chunk.Chunk_L.y;
                            parts.Add(new Part(kind, label, ports, shape.IndexOf(side, position)));
                        }
                        else
                        {
                            errors.Add($"{label} {problem}");
                        }

                        break;
                    default:
                        parts.Add(new Part(kind, label, ports));
                        break;
                }
            }

            return parts;
        }

        private static void AddConstant(BuildingModel building, string label, Port[] ports, List<Part> parts, List<string> errors)
        {
            ISignal value = building.Configuration is ConstantSignalConfiguration configuration
                ? configuration.Value
                : NullSignal.Instance;
            switch (value)
            {
                case NullSignal:
                    // An unset constant drives nothing, inside a package as on the board.
                    break;
                case IntegerSignal integer:
                    parts.Add(new Part(PartKind.Constant, label, ports, value: integer.Value));
                    break;
                default:
                    errors.Add($"{label} holds {value}; only number constants can be part of a circuit yet.");
                    break;
            }
        }

        // Connectors in the order the game binds them, so a gate's inputs keep their meaning (a Transistor's
        // input, then gate). A bridge carries two wires: the game pairs its junctions in order.
        private static Port[] Ports(BuildingModel building)
        {
            bool bridge = building.Definition.Id == Bridge;
            var ports = new List<Port>();
            int junction = 0;
            foreach (IBuildingIO io in building.Definition.CustomData.Get<IBuildingConnectorData>().AllBuildingConnectors)
            {
                PortKind kind;
                switch (io)
                {
                    case BuildingSignalInput:
                        kind = PortKind.In;
                        break;
                    case BuildingSignalOutput:
                        kind = PortKind.Out;
                        break;
                    case BuildingSignalJunction:
                        kind = PortKind.Junction;
                        break;
                    default:
                        continue;
                }

                GlobalTilePivot pivot = ((BuildingBaseIO)io).Pivot(building.Transform);
                int group = kind == PortKind.Junction && bridge ? junction++ / 2 : 0;
                ports.Add(new Port(ToPivot(pivot), kind, group));
            }

            return ports.ToArray();
        }

        private static Pivot ToPivot(in GlobalTilePivot pivot)
        {
            return new Pivot(pivot.Position.x, pivot.Position.y, pivot.Position.z, ToFacing(pivot.Direction));
        }

        private static Facing ToFacing(TileDirection direction)
        {
            if (direction == TileDirection.North) return Facing.North;
            if (direction == TileDirection.East) return Facing.East;
            if (direction == TileDirection.South) return Facing.South;
            if (direction == TileDirection.West) return Facing.West;
            return direction == TileDirection.Up ? Facing.Up : Facing.Down;
        }

        // Development aid: the board's buildable tiles as a 20x20 grid in the frame pins are measured in
        // (# buildable, n a notch, . not, P a pin, B another building), to check the edge test against the real
        // outline.
        public static IEnumerable<string> BuildableMap(IslandModel board)
        {
            BuildingModel any = board.Buildings.FirstOrDefault();
            if (any.Equals(default(BuildingModel)))
            {
                yield break;
            }

#pragma warning disable CS0618 // see Read
            IslandChunkData chunk = board.LayoutQuery.GetChunk_UNSAFE_I(any.Transform_I.Position);
#pragma warning restore CS0618
            var occupied = new Dictionary<(int, int), char>();
            foreach (BuildingModel building in board.Buildings)
            {
                ChunkTileCoordinate tile = building.Transform.Position.ToChunkCoordinate(board.Transform);
                bool pin = building.Definition.CustomData.TryGet(out IBuildingDefinitionGroup group) && IsPinGroup(group.Id);
                occupied[(tile.x, tile.y)] = pin ? 'P' : 'B';
            }

            for (int y = 0; y < ChunkTiles; y++)
            {
                var row = new char[ChunkTiles];
                for (int x = 0; x < ChunkTiles; x++)
                {
                    row[x] = occupied.TryGetValue((x, y), out char mark) ? mark
                        : !IsBuildable(chunk, x, y, 0) ? '.'
                        : chunk.GetNotchDirection_UNSAFE_L(new ChunkTileCoordinate(x, y, 0)).HasValue ? 'n'
                        : '#';
                }

                yield return $"{y,2} {new string(row)}";
            }
        }

        // The board edge a tile is on (for a notch pin, its wire end). In a notch (where space belts attach, mid-side): the side the notch points
        // to. On the main square: the one direction in which the next tile is off the square, counting notch
        // tiles as off it, since a pin at the middle of an edge sits right beside its notch. North is towards
        // smaller y.
        private static bool TryEdge(IslandChunkData chunk, ChunkTileCoordinate tile, bool notchOnly, out Side side, out string problem)
        {
            ChunkDirection? notch = chunk.GetNotchDirection_UNSAFE_L(new ChunkTileCoordinate(tile.x, tile.y, 0));
            if (notch.HasValue && TrySideOf(notch.Value, out side))
            {
                problem = null;
                return true;
            }

            if (notchOnly)
            {
                side = Side.North;
                problem = "must fill one of the board's notches, the gaps in the middle of each edge.";
                return false;
            }

            (Side Side, int Dx, int Dy)[] outward =
            {
                (Side.North, 0, -1),
                (Side.East, 1, 0),
                (Side.South, 0, 1),
                (Side.West, -1, 0)
            };
            Side[] open = outward
               .Where(step => !IsMainSquare(chunk, tile.x + step.Dx, tile.y + step.Dy))
               .Select(step => step.Side)
               .ToArray();

            side = open.FirstOrDefault();
            problem = open.Length switch
            {
                1 => null,
                0 => "must sit on the board's outer edge (its outermost row, or in a notch).",
                _ => "is in a corner; move it along the edge so it belongs to one side."
            };
            return problem == null;
        }

        public static bool IsPinGroup(BuildingDefinitionGroupId group)
        {
            return group.Equals(InputPinGroup) || group.Equals(OutputPinGroup)
                   || group.Equals(InputNotchPinGroup) || group.Equals(OutputNotchPinGroup);
        }

        private static bool IsBuildable(IslandChunkData chunk, int x, int y, short z)
        {
            return x >= 0 && x < ChunkTiles && y >= 0 && y < ChunkTiles
                   && chunk.IsBuildableTile_UNSAFE_L(new ChunkTileCoordinate(x, y, z));
        }

        private static bool IsMainSquare(IslandChunkData chunk, int x, int y)
        {
            return IsBuildable(chunk, x, y, 0) && !chunk.GetNotchDirection_UNSAFE_L(new ChunkTileCoordinate(x, y, 0)).HasValue;
        }

        private static bool TrySideOf(ChunkDirection direction, out Side side)
        {
            (ChunkDirection Direction, Side Side)[] sides =
            {
                (ChunkDirection.North, Side.North),
                (ChunkDirection.East, Side.East),
                (ChunkDirection.South, Side.South),
                (ChunkDirection.West, Side.West)
            };
            foreach ((ChunkDirection candidate, Side candidateSide) in sides)
            {
                if (candidate.Equals(direction))
                {
                    side = candidateSide;
                    return true;
                }
            }

            side = Side.North;
            return false;
        }

        private static string Describe(BuildingDefinitionId id)
        {
            const string suffix = "InternalVariant";
            string name = id.Name;
            int at = name.IndexOf(suffix, StringComparison.Ordinal);
            return at > 0 ? name.Remove(at, suffix.Length) : name;
        }
    }
}
