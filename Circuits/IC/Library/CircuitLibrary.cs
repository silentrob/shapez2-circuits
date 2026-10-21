using System;
using System.Collections.Generic;

namespace Circuits.IC.Library
{
    // The compiled circuits in this savegame, saved with it as JSON through ShapezShifter's mod save data.
    // Packages carry their own copy of a circuit, so the library is where circuits are found by name, not
    // what keeps placed packages running. Public fields are the JSON format: add, never rename or remove.
    public class CircuitLibrary
    {
        public int Version = 1;
        public List<CircuitEntry> Circuits = new();

        public CircuitEntry Find(string name)
        {
            return Circuits.Find(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        // Stores a compiled circuit under its name; compiling over an existing name makes a new revision.
        public CircuitEntry Store(string name, string encodedNetlist)
        {
            CircuitEntry entry = Find(name);
            if (entry == null)
            {
                entry = new CircuitEntry { Name = name };
                Circuits.Add(entry);
            }

            entry.Revision++;
            entry.Netlist = encodedNetlist;
            return entry;
        }
    }

    public class CircuitEntry
    {
        public string Name;
        public int Revision;

        // NetlistCodec's encoding.
        public string Netlist;
    }
}
