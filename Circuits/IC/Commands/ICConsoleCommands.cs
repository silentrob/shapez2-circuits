using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Circuits.IC.Compilation;
using Circuits.IC.Library;
using Circuits.IC.Package;
using Core.Dependency;
using Game.Core.Content.Buildings;
using MonoMod.RuntimeDetour;
using ILogger = Core.Logging.ILogger;

namespace Circuits.IC.Commands
{
    // Console commands (F1) for working with boards and packages until the UI exists (milestone 4):
    //   circuits.compile <name>  compile the board the selected building is on into the library
    //   circuits.list            the circuits that can be loaded: the library, then the test circuits
    //   circuits.load <circuit>  load a circuit into the selected packages
    //   circuits.info            show the circuit in each selected package
    //
    // Loading replaces each selected package with a new one holding the circuit, through the game's own
    // undoable building action, so the replacement is scheduled safely between simulation updates and the
    // new package's sides are ordered for their new roles. Ctrl+Z undoes it.
    //
    // The commands need the session's player and action manager, which ShapezShifter's console hook does
    // not pass on, so this hooks the same game method itself and reads them from the session's container.
    public sealed class ICConsoleCommands : IDisposable
    {
        private static readonly MethodInfo InitConsoleCommands = typeof(GameSessionOrchestrator).GetMethod(
            "Init_9_ConsoleCommands",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SessionContainer = typeof(GameSessionOrchestrator).GetField(
            "DependencyContainer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly BuildingDefinitionGroupId PackageGroup;
        private readonly Func<CircuitLibrary> Library;
        private readonly ILogger Logger;
        private readonly Hook Hook;

        // library: the current savegame's library (it changes when another savegame loads).
        public ICConsoleCommands(BuildingDefinitionGroupId packageGroup, Func<CircuitLibrary> library, ILogger logger)
        {
            PackageGroup = packageGroup;
            Library = library;
            Logger = logger;
            Hook = new Hook(
                InitConsoleCommands,
                new Action<Action<GameSessionOrchestrator, IGameData, GlobalsData>, GameSessionOrchestrator, IGameData,
                    GlobalsData>(
                    (original, session, gameData, globals) =>
                    {
                        original(session, gameData, globals);
                        Register(session);
                    }));
        }

        public void Dispose()
        {
            Hook.Dispose();
        }

        private void Register(GameSessionOrchestrator session)
        {
            try
            {
                var container = (DependencyContainer)SessionContainer.GetValue(session);
                var console = container.Resolve<IDebugConsole>();
                var player = container.Resolve<Player>();
                var actions = container.Resolve<PlayerActionManager>();

                console.Register(
                    "circuits.compile",
                    new DebugConsole.StringOption("name"),
                    Logged(context => Compile(context, player)));
                console.Register("circuits.list", Logged(List));
                console.Register(
                    "circuits.load",
                    new DebugConsole.StringOption("circuit"),
                    Logged(context => Load(context, player, actions)));
                console.Register("circuits.info", Logged(context => Info(context, player)));
            }
            catch (Exception e)
            {
                Logger.Error?.Log($"[Circuits] Could not register console commands: {e}");
            }
        }

        // Echoes a command's console output to the game's log, where it can be read after the session.
        private Action<DebugConsole.CommandContext> Logged(Action<DebugConsole.CommandContext> command)
        {
            return context =>
            {
                Action<string> console = context.Output;
                context.Output = line =>
                {
                    console(line);
                    Logger.Info?.Log($"[Circuits] {line}");
                };
                command(context);
            };
        }

        private void Compile(DebugConsole.CommandContext context, Player player)
        {
            string name = context.GetString(0);
            IslandModel[] boards = player.InteractionState.BuildingSelection
               .Select(building => building.Island)
               .GroupBy(island => island.Id)
               .Select(group => group.First())
               .ToArray();
            if (boards.Length != 1)
            {
                context.Output("Select a building on the board to compile (and only on that board).");
                return;
            }

            var errors = new List<string>();
            IReadOnlyList<Part> parts = BoardReader.Read(boards[0], errors);
            CompileResult result = errors.Count > 0 ? CompileResult.Failure(errors) : BoardCompiler.Compile(name, parts);
            if (!result.Succeeded)
            {
                context.Output($"{name} did not compile:");
                foreach (string error in result.Errors)
                {
                    context.Output($"  {error}");
                }

                // Log only (too long for the console): the board's outline, for checking pin edges.
                Logger.Info?.Log("[Circuits] Board tiles (# buildable, n notch, . not, P pin, B building), x 0-19 across:");
                foreach (string row in BoardReader.BuildableMap(boards[0]))
                {
                    Logger.Info?.Log($"[Circuits] {row}");
                }

                return;
            }

            CircuitEntry entry = Library().Store(name, NetlistCodec.Encode(result.Netlist));
            context.Output(
                $"Compiled {entry.Name} (revision {entry.Revision}): {result.Netlist.DescribeSides()}, "
                + $"{result.Netlist.Gates.Length} gate(s). Load it with circuits.load {entry.Name}.");
        }

        private void List(DebugConsole.CommandContext context)
        {
            List<CircuitEntry> library = Library().Circuits;
            context.Output(library.Count == 0 ? "Library: empty. circuits.compile adds to it." : "Library:");
            foreach (CircuitEntry entry in library)
            {
                string described = NetlistCodec.TryDecode(entry.Netlist, out Netlist netlist) ? netlist.DescribeSides() : "unreadable";
                context.Output($"  {entry.Name} (revision {entry.Revision}): {described}");
            }

            context.Output("Test circuits:");
            foreach (KeyValuePair<string, Func<Netlist>> circuit in TestCircuits.ByName)
            {
                context.Output($"  {circuit.Key}: {circuit.Value().DescribeSides()}");
            }
        }

        // The library first, so a compiled circuit can take a test circuit's name. Test circuits have revision 0.
        private bool TryFind(string name, out Netlist netlist, out int revision)
        {
            CircuitEntry entry = Library().Find(name);
            if (entry != null)
            {
                revision = entry.Revision;
                return NetlistCodec.TryDecode(entry.Netlist, out netlist);
            }

            revision = 0;
            if (TestCircuits.ByName.TryGetValue(name, out Func<Netlist> circuit))
            {
                netlist = circuit();
                return true;
            }

            netlist = null;
            return false;
        }

        private void Load(DebugConsole.CommandContext context, Player player, PlayerActionManager actions)
        {
            string name = context.GetString(0);
            if (!TryFind(name, out Netlist netlist, out int revision))
            {
                context.Output($"No circuit '{name}'. circuits.list shows them.");
                return;
            }

            BuildingModel[] packages = SelectedPackages(player);
            if (packages.Length == 0)
            {
                context.Output("Select one or more IC packages first.");
                return;
            }

            // One netlist shared by every new package: configurations never change once placed.
            var delete = packages.Select(package => new DeleteBuildingPayload(package)).ToList();
            var place = packages
               .Select(package => new PlaceBuildingPayload(
                    package.Island.Id,
                    package.Definition,
                    new ICPackageConfiguration(netlist, revision),
                    package.Transform_I))
               .ToList();

            var action = new ActionModifyBuildings(player.CurrentMap, player, new ModifyBuildingsPayload(place, delete));
            context.Output(
                actions.TryScheduleAction(action)
                    ? $"Loading {netlist.Name} into {packages.Length} package(s)."
                    : "The game would not replace the selected packages.");
        }

        private void Info(DebugConsole.CommandContext context, Player player)
        {
            BuildingModel[] packages = SelectedPackages(player);
            if (packages.Length == 0)
            {
                context.Output("Select one or more IC packages first.");
                return;
            }

            foreach (BuildingModel package in packages)
            {
                var configuration = package.Configuration as ICPackageConfiguration;
                Netlist netlist = configuration?.Netlist ?? Netlist.Empty;
                string revision = configuration?.Revision > 0 ? $" (revision {configuration.Revision})" : "";
                context.Output($"{netlist.Name}{revision}: {netlist.DescribeSides()}");
            }
        }

        private BuildingModel[] SelectedPackages(Player player)
        {
            return player.InteractionState.BuildingSelection.Where(IsPackage).ToArray();
        }

        private bool IsPackage(BuildingModel building)
        {
            return building.Definition.CustomData.TryGet(out IBuildingDefinitionGroup group) && group.Id.Equals(PackageGroup);
        }
    }
}
