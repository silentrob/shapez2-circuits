using System;
using System.Collections.Generic;
using System.Linq;
using Circuits.IC.Library;
using Core.Localization;

namespace Circuits.IC.Board
{
    // A board's side panel (select the board platform itself): its name and whether its firmware is in the
    // library; Rename; Build firmware (the whole board is the source); and, on an empty board, Restore, which
    // places a firmware's layout back onto it for editing. Results and errors show in a dialog.
    public class BoardModules : IIslandModuleDataProvider
    {
        public IEnumerable<IHUDSidePanelModuleData> GetStats()
        {
            return Array.Empty<IHUDSidePanelModuleData>();
        }

        public IEnumerable<IHUDSidePanelModuleData> GetModules(IslandModel board)
        {
            BoardConfiguration settings = Firmware.BoardSettings(board);
            if (settings == null || !ICSession.Ready)
            {
                yield break;
            }

            string name = settings.Name;
            CircuitEntry built = name.Length > 0 ? ICSession.Library().Find(name) : null;
            CircuitEntry[] restorable = board.BuildingsCount == 0
                ? ICSession.Library().Circuits.Where(entry => entry.Layout != null).ToArray()
                : Array.Empty<CircuitEntry>();

            string description = built != null
                ? $"Its firmware is in the library at revision {built.Revision}. Building again makes revision {built.Revision + 1}."
                : "Build it to turn its circuit into firmware for IC chips.";
            if (restorable.Length > 0)
            {
                description += " Or restore a firmware's board here to edit it.";
            }

            yield return new HUDSidePanelModuleGenericSection.Data(
                null,
                new RawText(name.Length > 0 ? name : "Unnamed board"),
                new RawText(description));

            // On an empty board, Restore comes first: the panel sits below the game's own modules and can run
            // off the bottom of the screen.
            if (restorable.Length > 0)
            {
                int chosen = 0;
                yield return new HUDSidePanelModuleDropdownSelector.Data(
                    restorable.Select(entry => (IText)new RawText($"{entry.Name} (revision {entry.Revision})")),
                    0,
                    index => chosen = index);
                yield return new HUDSidePanelModuleGenericButton.Data(
                    new RawText("Restore"),
                    () => Restore(board, settings, restorable[chosen]));
            }

            yield return new HUDSidePanelModuleGenericButton.Data(new RawText("Build firmware"), () => Build(board, settings));
            yield return new HUDSidePanelModuleGenericButton.Data(new RawText("Rename"), () => Rename(settings, null));
        }

        private static void Build(IslandModel board, BoardConfiguration settings)
        {
            if (settings.Name.Length == 0)
            {
                Rename(settings, () => Build(board, settings));
                return;
            }

            List<string> report = FirmwareText.BuildReport(board, settings.Name).ToList();
            Inform(report[0].StartsWith("Built") ? "Firmware built" : "Build failed", string.Join("\n", report));
        }

        private static void Restore(IslandModel board, BoardConfiguration settings, CircuitEntry entry)
        {
            if (Firmware.Restore(board, entry, out string problem))
            {
                // Building the restored board again continues the same firmware.
                settings.Name = entry.Name;
            }
            else
            {
                Inform("Restore failed", problem);
            }
        }

        private static void Rename(BoardConfiguration settings, Action then)
        {
            HUDDialogSimpleInput dialog = ICSession.Dialogs.Show(Globals.Resources.UIDialogSimpleInputPrefab);
            dialog.Init(
                new RawText("Name this board"),
                new RawText("Its firmware is built under this name. One word: spaces become dashes."),
                "global.btn-confirm".T(),
                inputCorrector: FirmwareText.CleanName,
                defaultValue: new RawText(settings.Name));
            dialog.OnConfirmed.Register(name =>
            {
                settings.Name = FirmwareText.CleanName(name);
                if (settings.Name.Length > 0)
                {
                    then?.Invoke();
                }
            });
        }

        private static void Inform(string title, string text)
        {
            ICSession.Dialogs.Show(Globals.Resources.UIDialogSimpleInfoPrefab)
               .Init(new RawText(title), new RawText(text), "global.btn-ok".T());
        }
    }
}
