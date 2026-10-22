using System;
using Circuits.IC.Library;
using ILogger = Core.Logging.ILogger;

namespace Circuits.IC
{
    // What the current game session offers the mod's commands and side panels: the player (selection, map),
    // the action manager (undoable building changes) and the dialog stack. Captured when the session sets up
    // its console (ICConsoleCommands), and replaced for each session. Side panels are built by the game, so
    // they reach these here rather than through their constructors.
    public static class ICSession
    {
        public static Player Player { get; private set; }
        public static PlayerActionManager Actions { get; private set; }
        public static IHUDDialogStack Dialogs { get; private set; }

        // The current savegame's firmware library.
        public static Func<CircuitLibrary> Library { get; set; }
        public static ILogger Logger { get; set; }

        public static bool Ready => Player != null && Actions != null;

        public static void Begin(Player player, PlayerActionManager actions, IHUDDialogStack dialogs)
        {
            Player = player;
            Actions = actions;
            Dialogs = dialogs;
        }
    }
}
