using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Circuits.IC.Library;
using Circuits.IC.Package;

namespace Circuits.IC
{
    // Wording shared by the console commands and the side panels.
    public static class FirmwareText
    {
        // Firmware names are one word, so they can be typed in the console: spaces become dashes.
        public static string CleanName(string name)
        {
            string cleaned = Regex.Replace((name ?? "").Trim(), @"\s+", "-");
            return cleaned.Length > 40 ? cleaned.Substring(0, 40) : cleaned;
        }

        // Builds the board and reports the result as lines.
        public static IEnumerable<string> BuildReport(IslandModel board, string name)
        {
            if (name.Length == 0)
            {
                return new[] { "Name the board before building it." };
            }

            CircuitEntry entry = Firmware.Build(board, name, out IReadOnlyList<string> errors);
            if (entry == null)
            {
                return new[] { $"{name} did not build:" }.Concat(errors.Select(error => $"  {error}"));
            }

            NetlistCodec.TryDecode(entry.Netlist, out Netlist netlist);
            return new[]
            {
                $"Built {entry.Name}, revision {entry.Revision}: {netlist?.DescribeSides()}, {netlist?.Gates.Length} gate(s).",
                $"Flash it onto chips from a chip's panel, or with circuits.flash {entry.Name}."
            };
        }

        // "adder, revision 2: West in, East in, North out, South out", or what an empty chip holds.
        public static string Describe(ICPackageConfiguration settings)
        {
            Netlist netlist = settings?.Netlist ?? Netlist.Empty;
            if (IsEmpty(netlist))
            {
                return "No firmware";
            }

            string revision = settings.Revision > 0 ? $", revision {settings.Revision}" : " (built-in test circuit)";
            return $"{netlist.Name}{revision}: {netlist.DescribeSides()}";
        }

        public static bool IsEmpty(Netlist netlist)
        {
            return netlist.Name == Netlist.EmptyName;
        }
    }
}
