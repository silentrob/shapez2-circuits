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

Each **chunk edge on the board's outline is one pin slot**. A pin occupies a slot by being placed on the
board's outermost tile row along that chunk edge, anywhere along it. A slot holds at most one pin, an
input or an output; compile fails if a slot has two. An empty slot is an unused connection.

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
| Global and controlled signal transmitters and receivers | Share channels with the rest of the world, including vanilla's one-second channel delay. |
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

Adding side-panel buttons and dialogs means hooking the game's HUD, which ShapezShifter does not cover.
Milestones 2 and 3 therefore start with **console commands** (F1), which the mod can add through
ShapezShifter's console hook, and the UI follows in milestone 4:

```
ic.compile <name>      compile the board under the cursor into the library
ic.load <name>         load a circuit into the package under the cursor
ic.list                list the library
```

### 3.10 Packages

One package building per board size: 1×1, 2×1 and 2×2 in v1. Larger sizes follow if the two-way sides
of section 3.3 work, since the fallback grows too fast beyond 2×2.

Node limit per package: 256 after expansion, to be tuned once measured.

Models: a plain generated box with side markers in v1, so input and output sides can be told apart once a
circuit is loaded. Proper models later.

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
   `ic.load` loads from a built-in set of test circuits.
3. **Boards and compiling.** 1×1 board, placement rules, pins, compiler, library in save data,
   `ic.compile` and `ic.list`. First fully player-made IC.
4. **UI.** Side panels for board, pin and package; load dialog; update flow; compile errors shown on
   the board.
5. **Depth.** 2×1 and 2×2 boards and packages, nested ICs, virtual shape processors and channels inside
   ICs, the jump-to-board hotkey.
6. **Release.** Package models, translations, Workshop page using the ShapezShifter logo, compatibility
   notes.

## 7. Open questions

1. **Buttons and displays inside ICs.** Section 3.5 proposes a button keeps its board state and a
   display shows nothing. Acceptable?

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
