using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Circuits.IC.Package;
using Core.Dependency;
using Game.Core.Content.Buildings;
using MonoMod.RuntimeDetour;
using ILogger = Core.Logging.ILogger;

namespace Circuits.IC.Commands
{
    // Console commands (F1) for working with packages until the UI exists (milestone 4):
    //   circuits.list            the circuits that can be loaded
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
        private readonly ILogger Logger;
        private readonly Hook Hook;

        public ICConsoleCommands(BuildingDefinitionGroupId packageGroup, ILogger logger)
        {
            PackageGroup = packageGroup;
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

                console.Register("circuits.list", List);
                console.Register(
                    "circuits.load",
                    new DebugConsole.StringOption("circuit"),
                    context => Load(context, player, actions));
                console.Register("circuits.info", context => Info(context, player));
            }
            catch (Exception e)
            {
                Logger.Error?.Log($"[Circuits] Could not register console commands: {e}");
            }
        }

        private static void List(DebugConsole.CommandContext context)
        {
            foreach (KeyValuePair<string, Func<Netlist>> circuit in TestCircuits.ByName)
            {
                context.Output($"{circuit.Key}: {Describe(circuit.Value())}");
            }
        }

        private void Load(DebugConsole.CommandContext context, Player player, PlayerActionManager actions)
        {
            string name = context.GetString(0);
            if (!TestCircuits.ByName.TryGetValue(name, out Func<Netlist> circuit))
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
            Netlist netlist = circuit();
            var delete = packages.Select(package => new DeleteBuildingPayload(package)).ToList();
            var place = packages
               .Select(package => new PlaceBuildingPayload(
                    package.Island.Id,
                    package.Definition,
                    new ICPackageConfiguration(netlist),
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
                Netlist netlist = package.Configuration is ICPackageConfiguration configuration
                    ? configuration.Netlist
                    : Netlist.Empty;
                context.Output($"{netlist.Name}: {Describe(netlist)}");
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

        private static string Describe(Netlist netlist)
        {
            IEnumerable<string> sides = Enumerable.Range(0, Netlist.SideCount)
               .Where(side => netlist.SideRoles[side] != SideRole.Unused)
               .Select(side => $"{(Side)side} {(netlist.SideRoles[side] == SideRole.Input ? "in" : "out")}");
            string described = string.Join(", ", sides);
            return described.Length == 0 ? "no connections" : described;
        }
    }
}
