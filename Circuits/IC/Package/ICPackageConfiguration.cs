using System;
using Core.Factory;
using Game.Core.Serialization;

namespace Circuits.IC.Package
{
    // A package's per-building settings: the netlist it runs. The game saves configurations with the building
    // and copies them into blueprints and copy/paste, so a package's circuit travels with it. A configuration
    // never changes once the package exists: loading a circuit replaces the package (see ICConsoleCommands),
    // so the simulation can read it without locking.
    public class ICPackageConfiguration : IBuildingConfiguration
    {
        private const int Version = 1;

        public Netlist Netlist { get; private set; }

        public ICPackageConfiguration() : this(Netlist.Empty) { }

        public ICPackageConfiguration(Netlist netlist)
        {
            Netlist = netlist;
        }

        public void Sync(ISerializationVisitor visitor)
        {
            int version = Version;
            visitor.SyncInt_4(ref version);

            string encoded = visitor.Writing ? NetlistCodec.Encode(Netlist) : null;
            visitor.SyncString_4(ref encoded);
            if (!visitor.Writing)
            {
                Netlist = NetlistCodec.TryDecode(encoded, out Netlist netlist) ? netlist : Netlist.Empty;
            }
        }

        public bool Equals(IEntityConfiguration other)
        {
            return other is ICPackageConfiguration package
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
