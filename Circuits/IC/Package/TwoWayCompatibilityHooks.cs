using System;
using System.Reflection;
using MonoMod.RuntimeDetour;

namespace Circuits.IC.Package
{
    // Makes vanilla signal inputs and outputs accept a package's two-way side as a valid neighbour, as they
    // already accept wire junctions. The map uses this check to decide whether facing connectors are joined;
    // without it a display or button placed against a package shows a connection warning, although the
    // signal flows (that is decided separately, by TwoWaySideConnector in the simulation).
    public sealed class TwoWayCompatibilityHooks : IDisposable
    {
        private readonly Hook InputHook;
        private readonly Hook OutputHook;

        public TwoWayCompatibilityHooks()
        {
            InputHook = new Hook(
                IsCompatibleConnectionOf(typeof(BuildingSignalInput)),
                new Func<Func<BuildingSignalInput, BuildingBaseIO, bool>, BuildingSignalInput, BuildingBaseIO, bool>(
                    (original, self, other) => other is BuildingSignalTwoWay || original(self, other)));

            OutputHook = new Hook(
                IsCompatibleConnectionOf(typeof(BuildingSignalOutput)),
                new Func<Func<BuildingSignalOutput, BuildingBaseIO, bool>, BuildingSignalOutput, BuildingBaseIO, bool>(
                    (original, self, other) => other is BuildingSignalTwoWay || original(self, other)));
        }

        public void Dispose()
        {
            InputHook.Dispose();
            OutputHook.Dispose();
        }

        private static MethodInfo IsCompatibleConnectionOf(Type type)
        {
            // BuildingBaseIO also has an IsCompatibleConnection overload for IEntityConnector, so match the
            // parameter type too.
            return type.GetMethod(
                nameof(BuildingBaseIO.IsCompatibleConnection),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(BuildingBaseIO) },
                modifiers: null);
        }
    }
}
