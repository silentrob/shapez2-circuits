using System;

namespace Circuits.IC
{
    // A package's size in tiles, which is its board's size in chunks, and its connections: one per tile edge
    // on the outline, as each chunk edge of a board has a notch. Connections are numbered clockwise from the
    // north-west corner: the north edge west to east, the east edge north to south, the south edge east to
    // west, the west edge south to north. A 1x1 package's are North, East, South, West, as Side numbers them.
    // Tiles are counted from the north-west tile, x east and y south, as the game counts them.
    public sealed class PackageShape : IEquatable<PackageShape>
    {
        public static readonly PackageShape Single = new(1, 1);
        public static readonly PackageShape Wide = new(2, 1);
        public static readonly PackageShape Square = new(2, 2);

        public static readonly PackageShape[] All = { Single, Wide, Square };

        public readonly int Width;
        public readonly int Height;

        public int Connections => 2 * (Width + Height);

        private PackageShape(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public static bool TryGet(int width, int height, out PackageShape shape)
        {
            shape = Array.Find(All, candidate => candidate.Width == width && candidate.Height == height);
            return shape != null;
        }

        // "2x1".
        public override string ToString()
        {
            return $"{Width}x{Height}";
        }

        // The edge a connection is on, and its position along that edge counted from the west (north and south
        // edges) or the north (east and west edges).
        public (Side Edge, int Position) Locate(int connection)
        {
            int c = connection;
            if (c < Width) return (Side.North, c);
            c -= Width;
            if (c < Height) return (Side.East, c);
            c -= Height;
            if (c < Width) return (Side.South, Width - 1 - c);
            c -= Width;
            if (c < Height) return (Side.West, Height - 1 - c);
            throw new ArgumentOutOfRangeException(nameof(connection), connection, $"a {this} package has {Connections} connections");
        }

        public int IndexOf(Side edge, int position)
        {
            int along = edge is Side.North or Side.South ? Width : Height;
            if (position < 0 || position >= along)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, $"the {edge} edge of a {this} package");
            }

            return edge switch
            {
                Side.North => position,
                Side.East => Width + position,
                Side.South => Width + Height + (Width - 1 - position),
                _ => 2 * Width + Height + (Height - 1 - position)
            };
        }

        // The tile a connection is on.
        public (int X, int Y) Tile(int connection)
        {
            (Side edge, int position) = Locate(connection);
            return edge switch
            {
                Side.North => (position, 0),
                Side.East => (Width - 1, position),
                Side.South => (position, Height - 1),
                _ => (0, position)
            };
        }

        // "North" on a 1x1 package; "North 2" (counted from the west, or the north) on larger ones.
        public string Name(int connection)
        {
            (Side edge, int position) = Locate(connection);
            int along = edge is Side.North or Side.South ? Width : Height;
            return along == 1 ? edge.ToString() : $"{edge} {position + 1}";
        }

        public bool Equals(PackageShape other)
        {
            return other is not null && other.Width == Width && other.Height == Height;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageShape);
        }

        public override int GetHashCode()
        {
            return Width * 31 + Height;
        }
    }
}
