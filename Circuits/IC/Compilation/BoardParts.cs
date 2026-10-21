using System;

namespace Circuits.IC.Compilation
{
    // The compiler's view of a board: what each building is and where its connectors are, without any game
    // types, so the compiler can be tested on its own. BoardReader builds these from the map.

    public enum PartKind { Wire, And, Or, Xor, Not, Transistor, Constant, InputPin, OutputPin }

    public enum PortKind { In, Out, Junction }

    public enum Facing { North, East, South, West, Up, Down }

    // A connector's spot: the tile it is on and the side it faces. Two connectors meet when each sits at the
    // other's counterpart: the neighbouring tile, facing back.
    public readonly struct Pivot : IEquatable<Pivot>
    {
        public readonly int X, Y, Z;
        public readonly Facing Facing;

        public Pivot(int x, int y, int z, Facing facing)
        {
            X = x;
            Y = y;
            Z = z;
            Facing = facing;
        }

        // North is towards smaller y, as in the game.
        public Pivot Counterpart()
        {
            return Facing switch
            {
                Facing.North => new Pivot(X, Y - 1, Z, Facing.South),
                Facing.East => new Pivot(X + 1, Y, Z, Facing.West),
                Facing.South => new Pivot(X, Y + 1, Z, Facing.North),
                Facing.West => new Pivot(X - 1, Y, Z, Facing.East),
                Facing.Up => new Pivot(X, Y, Z + 1, Facing.Down),
                _ => new Pivot(X, Y, Z - 1, Facing.Up)
            };
        }

        public bool Equals(Pivot other)
        {
            return X == other.X && Y == other.Y && Z == other.Z && Facing == other.Facing;
        }

        public override bool Equals(object obj)
        {
            return obj is Pivot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((X * 397 ^ Y) * 397 ^ Z) * 397 ^ (int)Facing;
        }
    }

    // A connector. Junctions are a wire's ends; a wire joins all its junctions with the same Group into one
    // net (a bridge has two groups, so its two crossing wires stay apart).
    public readonly struct Port
    {
        public readonly Pivot Pivot;
        public readonly PortKind Kind;
        public readonly int Group;

        public Port(Pivot pivot, PortKind kind, int group = 0)
        {
            Pivot = pivot;
            Kind = kind;
            Group = group;
        }
    }

    public sealed class Part
    {
        public readonly PartKind Kind;

        // Names the building and its tile in error messages.
        public readonly string Label;

        // Inputs in the order the game binds them (for a Transistor: input, then gate).
        public readonly Port[] Ports;

        // Pins only: the package side the pin stands for.
        public readonly Side Side;

        // Constants only.
        public readonly int Value;

        public Part(PartKind kind, string label, Port[] ports, Side side = Side.North, int value = 0)
        {
            Kind = kind;
            Label = label;
            Ports = ports;
            Side = side;
            Value = value;
        }
    }
}
