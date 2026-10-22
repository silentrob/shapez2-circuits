using System;
using System.Collections.Generic;

namespace Circuits.IC.Library
{
    // The firmware in this savegame, saved with it as JSON through ShapezShifter's mod save data (the file is
    // named after this type: never rename or move it). Chips carry their own copy of their firmware, so the
    // library is where firmware is found by name, not what keeps placed chips running. Public fields are the
    // JSON format: add, never rename or remove.
    public class CircuitLibrary
    {
        public int Version = 1;
        public List<CircuitEntry> Circuits = new();

        public CircuitEntry Find(string name)
        {
            return Circuits.Find(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        // Stores built firmware under its name; building over an existing name makes a new revision.
        public CircuitEntry Store(string name, string encodedNetlist, BoardLayout layout)
        {
            CircuitEntry entry = Find(name);
            if (entry == null)
            {
                entry = new CircuitEntry { Name = name };
                Circuits.Add(entry);
            }

            entry.Revision++;
            entry.Netlist = encodedNetlist;
            entry.Layout = layout;
            return entry;
        }
    }

    public class CircuitEntry
    {
        public string Name;
        public int Revision;

        // NetlistCodec's encoding.
        public string Netlist;

        // The board the firmware was built from, so it can be restored onto a board and edited again. Null for
        // firmware built before layouts were kept.
        public BoardLayout Layout;
    }

    // A board's buildings, as placed on it.
    public class BoardLayout
    {
        // The game version the configurations were written with, to read them back.
        public int GameVersion;
        public List<LayoutBuilding> Buildings = new();
    }

    public class LayoutBuilding
    {
        public string Definition;
        public short X, Y, Z;
        public byte Rotation;

        // The building's configuration (an input pin's test value, a constant's value), base64; null if none.
        public string Configuration;
    }
}
