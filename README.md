# Circuits: a shapez 2 mod

Logic for the wires layer of [shapez 2](https://store.steampowered.com/app/2162800/shapez_2/): design circuits
on a board, build them into firmware, and run them on chips you place anywhere.

## What it adds

- **Integrated circuits.** Design a circuit on an **IC Board** (1×1, 2×1 or 2×2), mark its inputs and outputs
  with **IC Pins** in the board's notches, and click **Build firmware**. Flash the firmware onto an **IC
  Package** of the same size on any platform; it behaves exactly like the board, as one building, with no added
  delay.
  - Everything signal-based works inside a chip: wires, all the vanilla logic gates, virtual shape processors,
    constants (numbers, shapes, colours), buttons, latches, feedback loops and other chips.
  - Rebuilding a board makes a new revision; chips show **Update** and **Update all**. **Edit board** on a chip
    places its firmware's board as a blueprint, and **Restore** puts a design back on an empty board.
  - Chips copy, paste and go into blueprints with their firmware, and only connect where their firmware does.
- **Latch.** Holds the last signal on its Data input until Reset is on. Any value: a number, a shape or a
  colour.
- **Integer gates.** A wire carries a 32-bit number; these work on all of it, so one wire can carry 32 flags:
  - **Bitwise Gate**: AND, OR, XOR (AND with a constant masks bits out)
  - **Bitwise NOT**
  - **Shift Gate**: left or right by an amount
  - **Arithmetic Gate**: add or subtract

The design, and the reasoning behind it, is in [docs/ic-spec.md](docs/ic-spec.md).

## Install

1. Subscribe to the two mods Circuits builds on, in the Steam Workshop:
   [ShapezShifter](https://steamcommunity.com/sharedfiles/filedetails/?id=3542611357) and
   [MonoMod.RuntimeDetour](https://steamcommunity.com/sharedfiles/filedetails/?id=3542712030).
2. Build Circuits (below); it builds straight into the game's mods folder.
3. Enable it in the game's mod menu.

Circuits changes savegames and disables achievements while it is on. Saves made with it need it to load, so
keep a copy of any save you want to go back to without it.

## Quick start

1. Research **Logic Gates** to unlock everything.
2. Place an **IC Board** from the Platforms toolbar (in its own IC Boards group).
3. Drag a wire into a notch: dragging out to the notch places an **Output Pin**, in from it an **Input Pin**.
   Build a circuit between them.
4. In space view, select the board and click **Build firmware**.
5. Place an **IC Package** from the Integrated Circuits group in the Wires toolbar, select it, pick the firmware
   and click **Flash firmware**.

## Build

Needs the .NET SDK (8 or later) and shapez 2 installed through Steam, with both Workshop dependencies
subscribed.

```
dotnet build Circuits/Circuits.csproj
dotnet test Circuits.Tests
```

The build finds the game and the Workshop mods at their default Steam paths. For another install, set
`SPZ2_PATH` to the game's `shapez 2_Data\Managed` folder (launching the game once with
`--set-modding-env-vars` sets it); see `Directory.Build.props`. Restart the game after building.

## Status

Version 0.1, before its first Workshop release. Not in this version: global and controlled transmitters inside
chips.

## Credits

Built with [ShapezShifter](https://github.com/tobspr-games/shapez2-shifter). The IC Board icons use shapez 2's
own foundation and wires icons. shapez 2 is made by tobspr Games.

## Licence

[MIT](LICENSE)
