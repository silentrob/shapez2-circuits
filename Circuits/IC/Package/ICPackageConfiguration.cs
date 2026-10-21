using System;
using Core.Factory;
using Game.Core.Serialization;

namespace Circuits.IC.Package
{
    // A package's per-building settings: the netlist it runs. The game saves configurations with the building
    // and copies them into blueprints and copy/paste, so a package's circuit travels with it. A configuration
    // never changes once the package exists: loading a circuit replaces the package (see ICConsoleCommands),
    // so the simulation can read it without locking.
    //
    // Format versions, all still read:
    //   1: the netlist.
    //   2: adds the library revision the netlist came from.
    public class ICPackageConfiguration : IBuildingConfiguration
    {
        private const int Version = 2;

        public Netlist Netlist { get; private set; }

        // The library revision the circuit was loaded from; 0 for a built-in test circuit, or a package saved
        // before revisions were recorded.
        public int Revision { get; private set; }

        public ICPackageConfiguration() : this(Netlist.Empty, 0) { }

        public ICPackageConfiguration(Netlist netlist, int revision)
        {
            Netlist = netlist;
            Revision = revision;
        }

        public void Sync(ISerializationVisitor visitor)
        {
            int version = Version;
            visitor.SyncInt_4(ref version);

            string encoded = visitor.Writing ? NetlistCodec.Encode(Netlist) : null;
            visitor.SyncString_4(ref encoded);

            int revision = Revision;
            if (version >= 2)
            {
                visitor.SyncInt_4(ref revision);
            }

            if (!visitor.Writing)
            {
                Netlist = NetlistCodec.TryDecode(encoded, out Netlist netlist) ? netlist : Netlist.Empty;
                Revision = version >= 2 ? revision : 0;
            }
        }

        public bool Equals(IEntityConfiguration other)
        {
            return other is ICPackageConfiguration package
                   && package.Revision == Revision
                   && (ReferenceEquals(package.Netlist, Netlist)
                       || NetlistCodec.Encode(package.Netlist) == NetlistCodec.Encode(Netlist));
        }
    }

    // Attached to the package definition, so placing a package gives it an empty configuration, and loading a
    // savegame or pasting a blueprint creates one to read into. Placement asks for IFactory<IEntityConfiguration>,
    // loading for IFactory<IBuildingConfiguration>; IFactory is covariant, so this answers both.
    public class ICPackageConfigurationFactory : IFactory<IBuildingConfiguration>
    {
        public IBuildingConfiguration Produce()
        {
            return new ICPackageConfiguration();
        }
    }
}
