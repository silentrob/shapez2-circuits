# Circuits: Integrated Circuits (ICs)

Status: draft for review, 2026-10-21. Covers the IC feature of the Circuits mod. The latch (done) is the
first gate; this spec covers everything else.

## 1. What the player gets

Late-game wires let you build logic out of gates, but every copy of a circuit has to be built and
wired again by hand. ICs let you **design a circuit once and stamp it out as a single building**:

1. Place an **IC Board**, a special platform for designing circuits. Build a circuit on it with wires,
   gates, virtual shape processors and signal transmitters, and put **IC Pins** on its edges.
   The board runs live, so you can test the circuit as you build it.
2. **Compile** the board. The circuit is saved to the game's **IC library** under a name you choose.
3. Place an **IC Package** of the same size on any machine platform and **load** the circuit into it.
   The package behaves exactly like the board, as one building.
4. Copy, paste and blueprint packages like any building; the circuit travels with them.
5. Packages can be placed on boards, so circuits nest.

### Scale: chunks become tiles

A package is its board shrunk by one scale step: **each chunk of the board becomes one tile of the
package**, and **each chunk edge on the board's outline becomes one connection on that side of the
package**. So:

| Board | Package | Connections |
|---|---|---|
| 1×1 chunks | 1×1 tile | 4, one per side |
| 2×1 chunks | 2×1 tiles | 6 |
| 2×2 chunks | 2×2 tiles | 8 |

A pin placed along a board edge comes out of the package at the matching side and position.

### The "Logic view"

The original idea was a third zoom level next to the Space and Machine views. We are **not** building a
new view: the game hard-codes its two views in about 40 places, and a third would mean patching all of
them (see *Rejected alternatives*). The board gives the same experience on the existing Machine view.
It is a real platform, so the 3D camera, zoom, rotation, panning and every normal building control work
on it unchanged. A hotkey that jumps the camera to your boards (milestone 5) makes it feel like a place
you go to.

## 2. Concepts

| Term | What it is |
|---|---|
| **IC Board** | A new platform type, in the sizes above. Only signal buildings, pins and IC Packages can be built on it. Each board has a stable id and a name. |
| **IC Input Pin** / **IC Output Pin** | New 1×1 buildings that exist only on board edges. On the board, an input pin emits a test value you set and an output pin shows what reaches it. |
| **Netlist** | The compiled form of a circuit: its nodes, the nets between them and the side map. Plain data. |
| **IC library** | The named netlists in this savegame. |
| **IC Package** | A building, one per board size, that runs a netlist loaded into it. |

## 3. Design decisions

### 3.1 Each package carries its own copy of the netlist

A package stores its netlist in its **building configuration**, the same per-building settings the game
uses for a Constant Signal's value or a Label's text.

- The game already saves configurations, and **copies them into blueprints and copy/paste**. So a
  blueprint of packages works in another save, and on the Workshop.
- The simulation reads the netlist from the configuration, so it never looks anything up at run time.
  That matters because simulations run on worker threads (`SimulationGraph` updates clusters in
  parallel), where reading the map is unsafe.

The package also records the netlist's **library id and revision**, so it can tell when the library
copy has moved on (section 3.7).

*Rejected:* a package that only holds a reference to its board, evaluated by reading the board. Platform
ids are not saved and get reassigned on every load, cross-thread map reads are unsafe, and routing
signals through the game's global channels would add a fixed one-second delay.

### 3.2 Boards

Each board size is a custom island type built with ShapezShifter's island builder
(`canHoldBuildings: true`), with the same chunk layout as a machine foundation of that size. It has an
island configuration holding a **GUID and a name**, saved with the island, because island ids are not
stable across loads.

**Placement rules.** A detour on `BaseMapInteractionMode.AllowBuildingGroup` /
`IslandAllowBuildingPlacement` limits boards to the buildings in section 3.5, pins and packages, and
forbids pins everywhere else. Belts, pipes and anything with belt or fluid connections are refused on
boards, so belt readers, belt filters and pipe gates can never be part of an IC.

### 3.3 Pins and sides

Each **chunk edge on the board's outline is one pin slot**. A chunk's buildable area is a 14×14 square
plus a notch at the middle of each side, where space belts attach. A pin occupies a slot by being placed
in that side's **notch** (the natural place, as belt ports use notches; pins are allowed on notches) or on
the square's outermost row along that edge. Edges come from the game's own tile data
(`IslandChunkData`: buildable and notch flags), in the platform's frame: a pin in a notch belongs to the
side the notch points to; on the square, to the one direction in which the next tile is off the square,
notch tiles counting as off it. A slot holds at most one pin, an input or an output; compile fails if a
slot has two. An empty slot is an unused connection.

Position, not a number, decides where a pin comes out, so the board reads like the package: the pin on
the north edge of a 1×1 board is the package's north connection.

#### Two-way sides

A package side has to be an input or an output depending on the circuit loaded, but the game fixes a
building connector's direction when it builds definitions at load. Signals on wires are two-way, so each
package side is **one two-way connection**. From the decompiled code:

- *Two connectors on one side are impossible.* `BuildingConnectorData` keys connectors by pivot (tile
  and side) with `ToDictionary`, so a second connector on a side throws at load.
- *Wire-style junctions are no good either.* `SignalNetworkSystem.TryGetJunctions` joins all of a
  building's junction connectors into one network (only the vanilla bridge is special-cased, by id), so
  junctions on several sides would short them together.
- *What works:* the link between a wire and a building is made in the simulation graph, not the
  definition. Where a wire ends, its network places a `SignalNetworkConnector`, which is both receiver
  and provider. The graph pairs connectors with `a.TryConnect(b) || b.TryConnect(a)`. So the package's
  simulation gives each side its own connector, of a type the network's `TryConnect` declines (neither
  `ISignalProviderConnector` nor `ISignalReceiverConnector`). The graph then asks the package's
  connector, which links **both** ways: the package's side output to the network's receiver, and the
  network's provider to the package's side input.
- *Zero delay is kept.* Clusters order simulations by comparing the two connectors' `UpdatePriority`.
  Each side reports the vanilla priority for its role in the loaded circuit: an input side updates after
  its wire (`MeLast`), an output side before it (`MeFirst`), an unused side `DontCare`. That is exactly
  how vanilla gates order themselves. Loading a different circuit re-registers the package so the
  cluster re-sorts.
- *Buildings placed directly against a side work too.* A vanilla building's output or input connector is
  one-way, so the side links only that direction: an output facing the side feeds its input, and the
  side's output feeds an input facing it. (Found in milestone 1 testing: the first version linked wires
  only.)
- *Unused and input sides drive nothing,* which merges away on the wire (nothing plus X is X), so a
  package never disturbs a wire it only listens to.
- *Signals leaving a package show on the wire.* An output side pushes into the wire's network like any
  vanilla output, so inspecting the wire in the machine view shows the value, conflicts included.

The definition declares each side as a mod connector type, `BuildingSignalTwoWay`: a signal connector
that is neither input nor output, and deliberately not a junction (the wire system gathers junctions with
an `is` check and would join every side into one network). The map decides whether facing connectors are
joined, and shows a warning when they are not, with `IsCompatibleConnection`; vanilla inputs and outputs
only accept their opposite or a junction, so two small hooks on `BuildingSignalInput` and
`BuildingSignalOutput` make them accept a two-way side too. (Found in milestone 1 testing: with sides
declared as inputs, a display against a package worked but showed a warning.) The package therefore registers its own simulation system and connectable
simulation instead of the game's `ConnectableBuildingSimulation`, which binds connectors by their
declared direction.

Milestone 1 proves this on a 1×1 package before anything else is built. **Fallback** if it fails: one
generated building variant per input/output pattern (16 for 1×1, 64 for 2×1, 256 for 2×2), swapped in
when a circuit is loaded. Certain to work, but it grows too fast for larger packages.

### 3.4 Compiling

Compiling runs **on the main thread**, between ticks, triggered by the player (section 3.9).

1. Collect the board's buildings with `IMapModel.GetIslandBuildings`.
2. Reject the board if any building is not supported (section 3.5), naming the building and its tile.
3. Trace **nets**: groups of connectors joined by wires. Wires are ordinary buildings, so this is a
   flood fill over wire connectors, with bridges as the game models them (one bridge building is two
   junctions; see `SignalNetworkSystem.TryGetJunctions`). We trace the map ourselves rather than read
   the live simulation's network objects, which belong to the simulation thread.
4. Build the netlist: one node per building, one net per traced network, the side map from pin
   positions, and any nested package expanded inline (section 3.6).
5. Order the nodes for evaluation (section 3.8) and record any feedback loops.
6. Check the node count is within the limit (section 3.10).
7. Store it in the library under the board's name, increasing the revision if the name exists.

Errors appear in the board's side panel and in the log, each naming the building and tile at fault.

### 3.5 What works inside an IC

Everything signal-based works; nothing that touches belts or fluids can be built on a board.

| Building | Inside a package |
|---|---|
| Wire, Wire Bridge | Define nets. |
| AND, OR, XOR, NOT, Compare, Transistor (IF) | Same output as vanilla. Compare's configured operation is copied in. |
| Constant Signal | Its value is copied in. |
| Latch, and future Circuits gates | Keep per-package state (section 3.8). |
| Virtual shape processors: rotators, half cutter, stacker, unstacker, painter, crystal generator, swapper, pin pusher, analyzer | Run the same shape operations as vanilla (`CreateVirtualProcessorSystems` builds them from `ShapeOperations`). |
| Global and controlled signal transmitters and receivers | **Not in V0** (decided 2026-10-22): refused when building firmware. Later, they would share channels with the rest of the world, including vanilla's one-second channel delay. |
| Button | Keeps the state it had on the board; it cannot be pressed inside a package. |
| Display | Allowed so a board can show values while testing; shows nothing inside a package. |
| IC Package | Expanded inline (section 3.6). |
| IC Input Pin, IC Output Pin | The interface. |
| Wire transmitters to adjacent platforms | Not allowed: pins take their place. |
| Belts, pipes, belt readers, belt filters, pipe gates, machines | Not allowed on boards. |

Every vanilla behaviour the evaluator copies is checked against the decompiled vanilla simulation and
covered by a test.

### 3.6 Nested ICs are expanded inline

A package on a board is compiled by copying its netlist into the parent's, with fresh node and net
ids. The parent then has no runtime dependency on the child. Compile refuses a circuit that contains
itself, directly or through other circuits; the library records each entry's children to check this.
The node limit applies after expansion.

### 3.7 Library and updates

The library lives in the save, using ShapezShifter's mod save data (`AttachSaveData<T>`, stored as JSON
in the savegame). Each entry holds: id (GUID), name, revision, board size, the netlist, the board's GUID
if the board still exists, and the ids of nested circuits.

When an entry's revision moves past a package's, the package's side panel says **"Update available"**,
with buttons to update this package or **all packages using this circuit**. Packages never update
silently: a change that breaks a running factory should be a choice.

Deleting a board does not delete its library entry, and packages keep working from their own copy.

**Firmware keeps its board's layout** (milestone 4). A library entry stores, besides the netlist, the
board's buildings: definition, position and rotation on the board, and configuration (so input pins keep
their test values). Without it, a deleted or rebuilt board would leave firmware that can never be edited
again. An empty IC Board's panel offers **Restore** for any firmware: it places that layout onto the board,
through the game's undoable building action, ready to edit and build as a new revision.

### 3.8 Evaluation and timing

The package simulation (`ICSimulation`) implements `ISignalSimulation` and `IUpdatableSimulation`, with
signal conductors on its sides (section 3.3). Each signal tick (12 per second), it:

1. reads its input sides;
2. evaluates the nodes in order, writing each node's output to its net;
3. writes its output sides.

Rules, matching vanilla so a package behaves exactly like its board:

- **No delay.** A signal passes through a whole IC in the same tick, as it passes through a chain of
  vanilla gates. Global channels keep their vanilla one-second delay.
- **Merging.** A net driven by several outputs uses the game's merge rule (`SignalUtils.GetMergedValue`):
  nothing plus X is X, and two different values make a Conflict.
- **Feedback loops** are cut at one point; that node reads the value from the previous tick. Vanilla
  falls back to a previous-update value in the same situation (`SignalBuffer`'s adaptive delay). Where
  the cut goes is decided at compile time and is stable.
- **State.** Latch values, loop-cut values and channel buffers live in the package's simulation state,
  saved with the game under a fixed `SyncableIdentifier` (never renamed).
- **Latches need no loops.** A latch is a gate with a stored value, not a feedback loop: inside a package
  it is a stateful gate whose value lives in the package state. It can come before general loops.
- **Matching the board's loop timing.** Reference loops built on a board (a NOT feeding itself, a ring of
  three NOTs, an SR latch from two NOR gates) showed a loop is delayed by one tick at one point; their
  behaviour is the evaluator's tests (`FeedbackLoopTests`; milestone 5).

The evaluator is plain arrays, built once, with no allocation per tick and no map access. Shape
operations and channel access get their dependencies (shape registry, channel registry) from the
simulation factory, as vanilla's virtual processors and receivers do.

### 3.9 Controls

The board is an ordinary platform, so building on it uses the normal camera and controls. On top:

- **Board side panel:** name field, **Compile** button, last compile result and errors.
- **Package side panel:** current circuit and revision, each side's role and live value, **Load circuit**
  (a list from the library, filtered to circuits of the package's size), **Update** when available.
  Live values need our own panel module: the game's `HUDSidePanelModuleWireInfo`, which vanilla gates
  use, only shows in the Unity editor (`ShouldShow() => Application.isEditor`), so players never see it.
  Until then, inspect the wires on a package's sides to read its values.
- **Pin side panel:** on input pins, the test value.
- **Hotkey:** jump the camera between boards (milestone 5).

**The game's side panel modules cover most of this without hooking the HUD.** A building's side panel
provider can return, besides the editor-only wire rows: `HUDSidePanelModuleInfoText(text)`,
`HUDSidePanelModuleGenericSection(icon, title, description)`, `HUDSidePanelModuleGenericButton(text,
action)` and `HUDSidePanelModuleDropdownSelector(values, current, onChange)`. Text is fixed when the panel opens,
so live values still need a module of our own.

Since milestone 4 the panels are the UI (`IC/Board/BoardModules`, `IC/Package/ICPackageModules`):

- **Board** (select the platform itself; an island module provider): its name and its firmware's library
  revision; **Rename** (a text dialog); **Build firmware**, which asks for a name if the board has none and
  shows the result or errors in a dialog; and, on an empty board, a firmware dropdown and **Restore**.
- **Chip**: its firmware, name, revision and sides; **Update to revision N** and **Update all chips
  running X** when the library holds a newer revision; a firmware dropdown and **Flash firmware**.

The **console commands** (F1) of milestones 2 and 3 remain, renamed for firmware. They work on the
selected buildings and follow ShapezShifter's convention of prefixing a mod's commands with its name:

```
circuits.build <name>      name the selected building's board and build it into firmware
circuits.flash <firmware>  flash firmware onto the selected chips
circuits.list              the firmware library, then the built-in test circuits
circuits.info              the firmware on each selected chip
```

They register by hooking the game's console setup (`GameSessionOrchestrator.Init_9_ConsoleCommands`)
directly rather than through ShapezShifter's console hook, which passes on only the console, because
flashing needs the session's player, action manager and dialog stack (kept in `IC/ICSession` for the
panels too).

**Flashing replaces the chip.** A chip's configuration never changes once placed. Flashing
deletes each selected package and places a new one holding the circuit, in one of the game's own
undoable building actions (`ActionModifyBuildings`, scheduled through `PlayerActionManager`). So the
change happens between simulation updates, the new package's sides are ordered for their new roles, the
simulation reads its netlist without locking, and Ctrl+Z undoes a flash.

### 3.10 Packages

One package building per board size: 1×1, 2×1 and 2×2 in v1. Larger sizes follow if the two-way sides
of section 3.3 work, since the fallback grows too fast beyond 2×2.

Node limit per package: 256 after expansion, to be tuned once measured.

**Model (built, 1×1).** Made in code (`IC/Package/ICPackageModel`, `Kit/PaletteMesh`): a near-black chip
body, a silver socket mid-side where a wire plugs in, a leg either side of it, and an accent-coloured bar on
the north edge marking the top (pin 1), so orientation is clear however a package is rotated. On each side
the loaded circuit uses, a live arrow lies on the body, pointing in for an input or out for an output,
silver while that side's signal is on and grey while off (`ICPackageRenderer`).

Findings, for future models:

- **Colour comes from UVs.** The building material (`UberBuildingMinimalMaterial`) colours a mesh by where
  each vertex's UV points in its 16×16 palette texture (`_MaterialLUT`), cell centre `((col+0.5)/16,
  1-(row+0.5)/16)`; the game's own accent recolouring shifts these UVs (`AccentColorMeshCreator`).
  `circuits.dumppalette` saves the texture (`Kit/TextureDump`). Columns, as labelled in it: Ac accent, Em,
  Mb, Mn, Mc, Pl, Br, Gr.
- **Some cells glow.** The Em column and the accent rows after the first are emissive and bloom into a
  glare on small shapes; so do the wire materials (`WireActiveTruthyMaterial` and friends). The Mc column
  renders dark and metallic. Plain and safe: Pl (dark greys), Mb (silvers), Ac row 1 (accent orange).
- **Double-sided faces need their own vertices.** Sharing vertices between a face and its back averages
  their normals to zero, and an unlit face renders near-black whatever its colour.
- **Packages need their own renderer base.** The game picks a renderer by (connectable simulation type,
  simulation type), and its building renderers are fixed to `ConnectableBuildingSimulation`; packages use
  `ICConnectableSimulation`, so `Kit/ConnectableRenderer` ports the vanilla per-chunk draw loop for any
  connectable type. ShapezShifter's `DynamicallyRendering` still needs a vanilla-style renderer type as a
  constraint, satisfied by a placeholder that never draws (`ICPackageDrawDataCarrier`).

Packages are mirrorable with F, using the mirrored-variant builder the latch already uses: mirroring a
package swaps its north and south connections, as the game does for every mirrored building.

### 3.11 Saves and compatibility

- Circuits is marked *affects savegames*, so a save played with it will not load without it, and a
  save made without it will not load with it. This is the game's own rule (`SavegameModdingContextDivergenceBarrier`).
- The package configuration, the simulation state and the library JSON all start with a format
  version, so later versions of the mod can read older saves.
- Building ids, `SyncableIdentifier`s and translation keys are never renamed once released.

### 3.12 Research and cost

- Boards, pins and packages unlock with the vanilla **Logic Gates** research, as the latch does.
- Everything is free to place for now.

### 3.13 Data on a signal line

Explored 2026-10-22, from the game's signal model:

- A wire carries one value per signal tick, and that value can be a full 32-bit integer
  (`IntegerSignal`), not only 0 or 1. Gates treat any non-zero value as on; a Constant Signal can hold
  any number. So one wire already carries 32 bits per tick; the game only lacks gates that read or
  change them.
- Signals tick 12 times a second (`SignalSimulation.SignalsPerSecond`), so anything sent over time
  moves about 12 values a second per wire.

Ways to carry more over one line:

| Approach | Needs | Bandwidth | Verdict |
|---|---|---|---|
| Bit packing: masks, shifts, bitwise AND/OR/XOR, add | New integer gates | 32 bits per line, instantly | Best value: no latency, deterministic, and the wire supports it already. |
| Multiplexing by address | Gates (Transistors make a 2-way mux today) | One value at a time, instantly | Buildable now; integer gates make it compact. |
| Shapes as packets (each layer and quadrant holds a part and a colour) | The virtual processors, as field accessors | Many fields per signal | Works, but awkward to read and write: a curiosity. |
| Time-division / serial | Clock, counters, latches | About 12 bits a second | Slow; a puzzle more than a tool. |
| PWM | Counters and comparators | Slower still | For blinking displays, not data. |

So bandwidth comes from integer gates, and state (latches, loops) is what time-based schemes are built
from; they are separate pieces.

**Integer gates (decided 2026-10-22: usable anywhere).** New Circuits buildings, like the latch, on any
platform and inside packages: bitwise AND, OR, XOR and NOT on whole integers, shift left and right, mask
and extract, add and subtract. The vanilla Compare gate joins them inside packages. Players then build
multiplexers, serial links and PWM from them, rather than the mod providing each.

**What loops are for.** Memory and timing from plain gates: toggles (a button pressed once for on, again
for off), counters (count shapes past a belt reader, act at N), clocks (blink a display, pulse a filter),
edge detectors (act only when a signal changes) and sequencers (cycle a painter through colours). The
latch covers the most common need, remembering a value, so general loops follow when clocks and counters
are wanted.

## 4. ShapezShifter gaps to fill

| Gap | Plan |
|---|---|
| Per-building configuration: ShapezShifter's config overload of `WithSimulation` throws `NotImplementedException`. | Attach an `IFactory<IBuildingConfiguration>` to the definition, and register the game's stateful-with-configuration simulation system through our own simulation-system hook (the pattern `MirroredLatchRewirer` already uses). |
| Connection direction per circuit | The spike in section 3.3. |
| Multi-tile wire buildings | ShapezShifter's multi-tile connector builder supports wire pins; confirmed in milestone 1 with the 2×1 package. |
| Side panels with buttons, dialogs | Our own HUD hooks (milestone 4). Console commands first. |
| Placement restrictions per platform | Detour on `BaseMapInteractionMode`. |
| Island configuration (board GUID and name) | Replace the configuration factory ShapezShifter's island builder attaches. |

Each gap that stays general (configuration, mirroring) is a candidate to offer back to ShapezShifter.

## 5. Code layout

```
Circuits/
  CircuitsMod.cs          registers everything
  Kit/                    general helpers (toolbar slot, research unlock, mirroring, configuration)
  Latch/                  done
  IC/
    Netlist/              Netlist, nodes, nets, side map; versioned serialization
    Evaluation/           NetlistEvaluator: ordering, loop cuts, per-tick evaluation
    Compilation/          BoardCompiler: board buildings -> netlist; errors
    Library/              ICLibrary in mod save data
    Package/              ICSimulation, state, configuration, factory, side panel, renderer
    Board/                board islands, placement rules, pins
    Commands/             console commands
docs/ic-spec.md           this file
```

The netlist and evaluator do not depend on the map, so they can be tested away from the game. Whether
a test project can load the game's signal types outside Unity is checked in milestone 1; if it cannot,
the evaluator uses its own small signal type and converts at the package's sides.

## 6. Milestones

Each one ends with something playable.

1. **Spike and fixed IC.** Settle connection direction (section 3.3). Then a 1×1 package running a
   hard-coded netlist (a half adder), with the evaluator and its tests. Proves: package sides,
   configuration path, state, evaluator matches vanilla.
2. **Netlists in packages.** Netlist in the package configuration, saved and copied by blueprints.
   `circuits.load` loads from a built-in set of test circuits.
3. **Boards and compiling.** First fully player-made IC. In two parts:
   - **3a, compiling (built).** Pins, compiler, library in save data, `circuits.compile`. Any 1×1-chunk
     platform serves as a board. Pins are copies of vanilla buildings (`Kit/VanillaCloneBuilder`), running
     the vanilla simulation, renderer and side panel: an Input Pin is a Constant Signal, so its value
     dialog sets the board's test input, and an Output Pin is a Display. A pin's side is the notch or edge it
     sits on (section 3.3); a failed compile logs the board's tile map. Compiles wires, bridges, AND, OR, XOR, NOT,
     Transistor and number constants; displays are skipped. Refused with a message for now: feedback
     loops, latches, Compare, buttons, shape and color constants, virtual processors, transmitters and
     nested packages. (Virtual processors and shape and color constants followed in milestone 5.)
   - **3b, the board platform (built).** The IC Board island (`IC/Board/BoardIsland`): one chunk with a
     notch on each side, like a 1×1 foundation, but no space belt connectors; in the Platforms toolbar
     after the 1×1 foundation. It has its own collision box, because ShapezShifter's bounding collider is
     empty for a one-chunk platform, which leaves it unselectable in space view. Circuits compile only on a board. Placement rules
     (`IC/Board/BoardPlacementRules`, a hook on `BaseMapInteractionMode.AllowBuildingGroup`, which both
     placement and its preview ask): a board takes only buildings whose connectors are all signal
     connectors, and no wire transmitters to neighbouring platforms; pins go only on boards. The package
     and pins sit in their own **Integrated Circuits** group in the Wires toolbar, after Logic Gates
     (`Kit/ToolbarGroup`); the latch stays among the gates. The board's name and GUID move to milestone 4,
     with its side panel.
4. **UI and firmware (built, awaiting play test).** Decided 2026-10-22:
   1. **Firmware naming.** A package is a chip that runs firmware: `circuits.compile` becomes
      `circuits.build`, `circuits.load` becomes `circuits.flash`, the library is the firmware library, an
      empty chip has "no firmware". Internal names stay (the library's type name names its save file).
   2. **Board identity.** A board's name (and GUID), saved in its island configuration, set from its panel.
   3. **Board panel.** Select the board platform itself: its name, a **Build** button (the whole board is
      the source; no need to select its contents) and the last build's result or errors.
   4. **Firmware keeps the board layout** (section 3.7); an empty board's panel offers **Restore**.
   5. **Chip panel.** A firmware dropdown and **Flash** button; **Update** and **Update all** when the library
      holds a newer revision.
   6. Live side values in the panel: optional; the side arrows cover most of it.

   Considered and not chosen: chips following a board automatically, auto-building on every change, and
   saving a box selection of the board's contents like a blueprint.
4b. **Board look.** Decided 2026-10-22: the board stays 14×14 (one chunk can't shrink in space view), and
   looks like a small-scale activity instead: a Shapez-styled circuit board.
   1. **Notch pins (built).** 4×1 Input and Output Pins that fill a notch, one per side, with the wire at one
      end and a mirrored variant (F) for the other (`IC/Board/PinModel`, `Kit/ReshapedCloneBuilder`: a vanilla
      Constant Signal or Display in a new shape and model, its value drawn smaller and centred in a bezel).
      Only notches count for them. The 1×1 pins stay registered for existing boards, off the toolbar.
   2. **Board surface (built).** The machine frame is off (an empty `ChunkPlatformDrawingContext`), and
      `IC/Board/BoardModel` draws a circuit board through the island's `IslandMeshDrawer.Data` with the
      building material: a teal substrate under the floor's edge, an accent line around the play area broken
      at the notches, traces and pads on the border, and a mounting hole in each corner. The floor and its grid
      stay vanilla, since every platform shares the floor material.
5. **Depth.** 2×1 and 2×2 boards and packages, nested ICs, virtual shape processors and channels inside
   ICs, the jump-to-board hotkey.
   - **Larger sizes (built).** `IC/PackageShape` numbers a shape's connections clockwise from the north-west
     corner (the north edge west to east, then east, south and west), so a 1×1's are North, East, South,
     West as before; names count from the west or north ("North 2"). Netlists carry their shape (codec v3;
     earlier versions are 1×1). Boards 2×1 and 2×2 (`BoardIsland`) have a notch on every outer chunk edge,
     and a pin's connection is its notch's edge and chunk along it. Packages 2×1 and 2×2 have a two-way
     connector per connection, run only firmware of their shape (flashing, Update and Restore all match
     on shape), and save room for eight connections (package state v2). Toolbar entries that follow
     another mod entry fall back on a vanilla one, as the game adds mod entries in no set order; an island
     entry left among buildings switched the view mid-placement and blocked zooming in.
   - **Virtual shape processors and value constants (built).** Section 3.5 always listed these as working
     inside a package, but milestone 3a's compiler refused them and nothing tracked the gap; caught
     2026-10-22. The ten virtual processors (rotators, half cutter, stacker, unstacker, painter, crystal
     generator, halves swapper, pin pusher, analyzer) compile to gates that run the game's own shape
     operations: `IC/Package/GateResolver` makes a `ShapeOperations` exactly as the game does for its virtual
     buildings, and the evaluator runs one when every input holds an item and drives nothing otherwise, as
     vanilla's `Virtual*Simulation` does. Inputs and outputs keep connector order; the unstacker, analyzer and
     halves swapper have two outputs. A Constant Signal holding a shape, a colour or any other value keeps it
     as the game serializes it (`IC/SignalBlob`). Netlist codec v4. Still refused: Compare, buttons,
     transmitters, latches and loops.
   - **Connections only where the firmware connects (built).** A chip declaring all its connections made wires
     dock on unused ones. Connectors are fixed per building definition, so each shape has a hidden variant per
     set of used connections (16, 64 and 256: the section 3.3 fallback, used here for docking rather than
     direction), all in the chip's group (`IC/Package/ChipVariants`). Flashing places the variant matching the
     firmware; sockets are drawn only on its connections. The every-connection variant keeps the original
     id, so older chips load, and is the one the toolbar places: the game's placers take at most two
     variants per group, so the others are registered outside the group's variant list.
   - **Nested ICs and buttons (built).** A chip on a board compiles in (section 3.6): its firmware's gates join
     the board's, each used connection on the board net its port touches, its other nets numbered after
     the board's, so nesting any depth flattens to one netlist and runs no differently. A chip with no
     firmware is refused, and so, for now, is one whose firmware joins two connections inside to separate
     board wires. A button compiles to its board state, as section 3.5 says: on sends 1, off sends nothing,
     as vanilla's does.
   - **Next, in order (decided 2026-10-22).** Latches inside packages, as stateful gates (section 3.8);
     integer gates, usable anywhere (section 3.13); then feedback loops, calibrated against reference
     loops built on a board (section 3.8).
   - **Latches inside packages (built).** A Latch gate (inputs Data then Reset, in connector order) runs the
     same rule as the latch building (`Latch/LatchRule`), in the same tick, so it adds no delay. What each
     latch holds lives in the package state (v3, after the connections) and survives saving; netlist codec
     v5. A latch on a chip nested in a board is copied in like any gate, with its own slot.
   - **Integer gates (built).** Four buildings in an Integer Gates group in the Wires toolbar
     (`IntGates/IntegerGates`), styled after the Compare gate (an operation picked in the side panel, written
     on top with the Display's font) but teal: Bitwise (AND, OR, XOR), Bitwise NOT, Shift (left, right; amount
     from the north) and Arithmetic (add, subtract). Inputs west then north, output east. `IntGates/IntegerOps`
     is shared by the buildings and packages: anything not a number counts as 0, the result is always a
     number, right shifts fill with zeros, a negative amount shifts the other way, sums wrap. Netlist codec
     v6. Masking with shapes was considered and set aside: shapes have a part and a colour per quadrant and
     layer, and converting them to and from numbers is far more complex than an integer mask.
   - **Feedback loops (built).** Measured on a board 2026-10-22: a NOT feeding itself flips every tick (about six
     times a second), a ring of three NOTs flips at the same speed, and an SR latch of two NOR gates (OR then
     NOT) sets, holds and resets, settling without flicker when both inputs drop together. So the game
     delays a loop by one tick at one point, not one tick per gate. The compiler orders gates as before and,
     when only loops are left, cuts the first waiting gate in board order (so a rebuild cuts the same place):
     its inputs still driven by waiting gates read their net's value from the end of the previous tick. Each
     gate records its cut inputs (netlist codec v7); the evaluator keeps each cut net's previous value, saved
     in the package state (v4) like latch values. A nested chip keeps its own cuts. Which of two equally
     valid outcomes an SR latch settles on when both inputs drop at once can depend on where the cut is, so
     it may differ from the board in that one case.
   - **Wires dragged into a notch place a pin (built).** As the game's wire placer turns a wire's end in a
     machine platform's notch into a wire transmitter (`PathAtNotchesUpgradeToPortsProcessor`), a hook on it
     swaps that for an IC pin on a board (`IC/Board/NotchPinPlacement`): dragged out to the notch, an Output
     Pin; in from it, an Input Pin. The pin's wire end goes where the wire meets the notch, facing into the
     board, running along the notch whichever way fits. A wire meeting one of the notch's two middle tiles
     leaves no room for a 4x1 pin, so it gets none.
   - **Compare gates (built).** The vanilla Compare gate compiles to one gate kind per comparison, read from its
     configuration, running vanilla's rule (`IC/CompareRule`): 1 or 0; Equal and Not Equal compare any two
     signals, the orderings hold only between two numbers. Netlist codec v8. Every vanilla logic gate now
     works inside a package.
   - **Transmitters inside packages: not in V0** (decided 2026-10-22). Global and controlled transmitters and
     receivers stay refused when building firmware. They would give a chip connections beyond its sides,
     carrying the channels' one-second delay, which wants more thought than V0 allows. Revisit after release.
6. **Release.** Package models, translations, Workshop page using the ShapezShifter logo, compatibility
   notes.

## 7. Open questions

None at present.

Settled 2026-10-22: buttons keep their board state inside a package and displays show nothing (built);
integer gates are new buildings usable anywhere (section 3.13), now that ICs exist.

Settled 2026-10-21: package connections follow the chunk-to-tile scale and are two-way; free to place;
no new gates before ICs; everything signal-based works inside an IC; belts and fluids are not allowed on
boards; the library is per save and blueprints carry ICs as they carry any building's settings; signals
leaving a package show when you inspect the wire.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Two-way sides fail in game (section 3.3) | Proved first, in milestone 1; fallback of one variant per pattern for 1×1 to 2×2. |
| Game updates break detours and private-member access (`_DefinitionsById`, HUD hooks) | Keep hooks few and in `Kit/`; fail loudly in the log; test each game update before the Workshop build. |
| The evaluator differs from vanilla in an edge case (conflicts, loops, Compare, shape operations) | Tests per vanilla building, built from the decompiled simulations; a debug command that compares a package with its live board. |
| Large ICs slow the simulation | Node limit; plain-array evaluator; measure before raising the limit. |
| HUD work is larger than expected | Console commands keep every feature usable before the UI exists. |

## Rejected alternatives

- **A true third "Logic view".** Views are a closed two-value enum (`PlayerInteractionBaseState`) used
  across state, camera, toolbar, placement, sound, HUD and rendering, with `switch`es that throw on
  anything else. The game also holds exactly one map. Too many patches, broken by every update.
- **Packages that reference their board at run time.** See 3.1.
- **Numbered pins.** Position on the board edge decides the package side instead, so the board reads
  like the package.
