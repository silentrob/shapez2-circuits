using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Circuits.IC.Library;
using Circuits.IC.Package;
using Core.Dependency;
using MonoMod.RuntimeDetour;
using ILogger = Core.Logging.ILogger;

namespace Circuits.IC.Commands
{
    // Sets up the mod for each game session: captures the session's player, action manager and dialog stack
    // (ICSession), which ShapezShifter's console hook does not pass on, by hooking the game's console setup and
    // reading them from the session's container. Also registers the console commands (F1), which do what the
    // side panels do, on the selected buildings:
    //   circuits.build <name>      name the board the selected building is on, and build it into firmware
    //   circuits.flash <firmware>  flash firmware onto the selected chips (Ctrl+Z undoes it)
    //   circuits.list              the firmware library, then the built-in test circuits
    //   circuits.info              the firmware on each selected chip
    //   circuits.dumppalette       development aid: save the building palette (see TextureDump)
    public sealed class ICConsoleCommands : IDisposable
    {
        private static readonly MethodInfo InitConsoleCommands = typeof(GameSessionOrchestrator).GetMethod(
            "Init_9_ConsoleCommands",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SessionContainer = typeof(GameSessionOrchestrator).GetField(
            "DependencyContainer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ILogger Logger;
        private readonly Hook Hook;

        public ICConsoleCommands(ILogger logger)
        {
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
                ICSession.Begin(container.Resolve<Player>(), container.Resolve<PlayerActionManager>(), container.Resolve<IHUDDialogStack>());

                var console = container.Resolve<IDebugConsole>();
                console.Register("circuits.build", new DebugConsole.StringOption("name"), Logged(Build));
                console.Register("circuits.flash", new DebugConsole.StringOption("firmware"), Logged(Flash));
                console.Register("circuits.list", Logged(List));
                console.Register("circuits.info", Logged(Info));
                console.Register("circuits.dumppalette", Logged(DumpPalette));
            }
            catch (Exception e)
            {
                Logger.Error?.Log($"[Circuits] Could not set up the session: {e}");
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

        private static void Build(DebugConsole.CommandContext context)
        {
            IslandModel[] boards = ICSession.Player.InteractionState.BuildingSelection
               .Select(building => building.Island)
               .Where(Firmware.IsBoard)
               .GroupBy(island => island.Id)
               .Select(group => group.First())
               .ToArray();
            if (boards.Length != 1)
            {
                context.Output("Select a building on one IC Board to build it.");
                return;
            }

            string name = FirmwareText.CleanName(context.GetString(0));
            if (Firmware.BoardSettings(boards[0]) is { } settings)
            {
                settings.Name = name;
            }

            foreach (string line in FirmwareText.BuildReport(boards[0], name))
            {
                context.Output(line);
            }
        }

        private static void Flash(DebugConsole.CommandContext context)
        {
            string name = context.GetString(0);
            if (!TryFind(name, out Netlist netlist, out int revision))
            {
                context.Output($"No firmware '{name}'. circuits.list shows the library.");
                return;
            }

            BuildingModel[] chips = SelectedChips();
            if (chips.Length == 0)
            {
                context.Output("Select one or more IC chips first.");
                return;
            }

            context.Output(
                Firmware.Flash(chips, netlist, revision)
                    ? $"Flashing {netlist.Name} onto {chips.Length} chip(s)."
                    : "The game would not replace the selected chips.");
        }

        private static void List(DebugConsole.CommandContext context)
        {
            List<CircuitEntry> library = ICSession.Library().Circuits;
            context.Output(library.Count == 0 ? "Firmware library: empty. Build a board to add to it." : "Firmware library:");
            foreach (CircuitEntry entry in library)
            {
                string described = NetlistCodec.TryDecode(entry.Netlist, out Netlist netlist) ? netlist.DescribeSides() : "unreadable";
                context.Output($"  {entry.Name} (revision {entry.Revision}): {described}");
            }

            context.Output("Built-in test circuits:");
            foreach (KeyValuePair<string, Func<Netlist>> circuit in TestCircuits.ByName)
            {
                context.Output($"  {circuit.Key}: {circuit.Value().DescribeSides()}");
            }
        }

        private static void Info(DebugConsole.CommandContext context)
        {
            BuildingModel[] chips = SelectedChips();
            if (chips.Length == 0)
            {
                context.Output("Select one or more IC chips first.");
                return;
            }

            foreach (BuildingModel chip in chips)
            {
                context.Output(FirmwareText.Describe(Firmware.ChipSettings(chip)));
            }
        }

        // Development aid for the IC model's colours: see TextureDump.
        private static void DumpPalette(DebugConsole.CommandContext context)
        {
            ICPackageRenderer.DumpPaletteRequested = true;
            context.Output("The building palette will be saved to the mod's palette-dump folder the next time an IC chip is on screen.");
        }

        // The library first, so built firmware can take a test circuit's name. Test circuits have revision 0.
        private static bool TryFind(string name, out Netlist netlist, out int revision)
        {
            CircuitEntry entry = ICSession.Library().Find(name);
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

        private static BuildingModel[] SelectedChips()
        {
            return ICSession.Player.InteractionState.BuildingSelection.Where(Firmware.IsChip).ToArray();
        }
    }
}
