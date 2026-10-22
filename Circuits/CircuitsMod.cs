using System;
using Circuits.IC;
using Circuits.IC.Board;
using Circuits.IC.Commands;
using Circuits.IC.Compilation;
using Circuits.IC.Library;
using Circuits.IC.Package;
using Circuits.Kit;
using Circuits.Latch;
using Core.Localization;
using Game.Core.Content.Buildings;
using JetBrains.Annotations;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Hijack;
using ShapezShifter.Kit;
using ShapezShifter.Textures;
using ILogger = Core.Logging.ILogger;

namespace Circuits
{
    [UsedImplicitly]
    public class CircuitsMod : IMod
    {
        // The latch sits after the vanilla AND gate in the toolbar and unlocks with it.
        private static readonly BuildingDefinitionGroupId AndGateGroup = new("LogicGateAndVariant");

        // The latch wears the vanilla Transistor's model, and its mirrored model for the mirrored variant.
        private static readonly BuildingDefinitionId Transistor = new("LogicGateIfInternalVariant");
        private static readonly BuildingDefinitionId TransistorMirrored = new("LogicGateIfInternalVariantMirrored");

        private readonly RewirerHandle MirroredLatchHandle;
        private readonly TwoWayCompatibilityHooks TwoWayHooks;
        private readonly ICConsoleCommands Commands;
        private readonly BoardPlacementRules BoardRules;

        public CircuitsMod(ILogger logger)
        {
            MirroredBuildingBuilder latch = AddLatch(logger);
            MirroredLatchHandle = GameRewirers.AddRewirer(new MirroredLatchRewirer(latch, logger));
            // The IC buildings get their own group in the Wires toolbar, after Logic Gates (where the latch stays).
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");
            var icToolbar = new ToolbarGroup(
                AndGateGroup,
                "toolbar.circuits-integrated-circuits.title",
                "toolbar.circuits-integrated-circuits.description",
                FileTextureLoader.LoadTextureAsSprite(resources.SubPath("IC_Icon.png"), out _));

            ICPackageRenderer.Logger = logger;
            AddICPackage(icToolbar, logger);
            AddPins(icToolbar, logger);
            BoardIsland.Register(AndGateGroup, logger);
            BoardRules = Optional(
                () => new BoardPlacementRules(),
                "IC Boards accept any building and pins can go on any platform",
                logger);
            this.AttachSaveData<CircuitLibrary>();
            ICSession.Library = this.GetSaveData<CircuitLibrary>;
            ICSession.Logger = logger;
            TwoWayHooks = Optional(
                () => new TwoWayCompatibilityHooks(),
                "IC Packages will show connection warnings",
                logger);
            Commands = Optional(
                () => new ICConsoleCommands(logger),
                "the circuits console commands are unavailable",
                logger);
        }

        public void Dispose()
        {
            GameRewirers.RemoveRewirer(MirroredLatchHandle);
            TwoWayHooks?.Dispose();
            Commands?.Dispose();
            BoardRules?.Dispose();
        }

        // For hooks into private game code, which a game update can break: the mod carries on without the
        // feature rather than stopping the game from loading.
        private static T Optional<T>(Func<T> create, string consequence, ILogger logger) where T : class
        {
            try
            {
                return create();
            }
            catch (Exception e)
            {
                logger.Error?.Log($"[Circuits] Setup failed, so {consequence}: {e}");
                return null;
            }
        }

        private static MirroredBuildingBuilder AddLatch(ILogger logger)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");

            IBuildingGroupBuilder group = BuildingGroup.Create(new BuildingDefinitionGroupId("CircuitsLatchGroup"))
               .WithTitle("building-variant.CircuitsLatch.title".T())
               .WithDescription("building-variant.CircuitsLatch.description".T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(resources.SubPath("Latch_Icon.png"), out _))
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
               .WithDefaultStructureOverview();

            // Pins follow the Transistor model: Data where its input is (north, or south when mirrored), Reset
            // where its gate is (west), output east. Input order is the simulation's receiver order: Data, Reset.
            var drawData = new LatchDrawData();
            var mirroredDrawData = new LatchDrawData();

            IBuildingBuilder primary = Building.Create(new BuildingDefinitionId("CircuitsLatch"))
               .WithConnectorData(LatchConnectors(TileDirection.North))
               .DynamicallyRendering<LatchRenderer, LatchSimulation, LatchDrawData>(drawData)
               .WithCopiedStaticDrawData(Transistor)
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithoutEfficiencyData();

            var building = new MirroredBuildingBuilder(
                primary,
                new BuildingDefinitionId("CircuitsLatchMirrored"),
                LatchConnectors(TileDirection.South),
                TransistorMirrored,
                mirroredDrawData,
                afterRegister: buildings =>
                {
                    drawData.Window = TransistorWindow(buildings, Transistor, logger);
                    mirroredDrawData.Window = TransistorWindow(buildings, TransistorMirrored, logger);
                },
                logger);

            AtomicBuildings.Extend()
               .AllScenarios()
               .WithBuilding(building, group)
               .UnlockedWithExistingSideUpgrade(new SideUpgradeUnlocking(AndGateGroup))
               .WithDefaultPlacement()
               .InToolbar(new AfterToolbarEntry(AndGateGroup, logger))
               .WithSimulation(new LatchFactoryBuilder(), logger)
               .WithCustomModules(new LatchBuildingModules())
               .WithoutPrediction()
               .Build();

            return building;
        }

        // Pins mark a board's interface. They are copies of vanilla buildings, so testing a board live uses
        // the game's own tools: an Input Pin is a Constant Signal (its value dialog sets the test input) and an
        // Output Pin is a Display (it shows what reaches it). BoardReader tells them apart by their groups.
        // Notch pins are 4x1, filling a board notch, with the wire at one end (F flips the end). The first,
        // 1x1 pins stay registered for boards and firmware that already use them, but are off the toolbar.
        private static void AddPins(IToolbarEntryInsertLocation toolbar, ILogger logger)
        {
            var constantSignal = new BuildingDefinitionId("ConstantSignalDefaultInternalVariant");
            Func<BuildingDefinition, SimulationSystemsDependencies, ISimulationSystem> constantSimulation =
                (definition, _) => new AtomicStatelessBuildingSimulationSystem<ConstantSignalSimulation, ConstantSignalConfiguration>(
                    new ConstantSignalSimulationFactory(),
                    definition.Id,
                    logger);
            var display = new BuildingDefinitionId("DisplayDefaultInternalVariant");
            Func<BuildingDefinition, SimulationSystemsDependencies, ISimulationSystem> displaySimulation =
                (definition, _) => new AtomicStatefulBuildingSimulationSystem<DisplaySimulation, DisplaySimulationState>(
                    new DisplaySimulationFactory(),
                    definition.Id,
                    logger);

            AddPin(
                BoardReader.InputNotchPinGroup,
                "CircuitsICInputNotchPin",
                "IC_InputPin_Icon.png",
                NotchPin(constantSignal, "CircuitsICInputNotchPin", input: true),
                constantSignal,
                constantSimulation,
                toolbar);
            AddPin(
                BoardReader.OutputNotchPinGroup,
                "CircuitsICOutputNotchPin",
                "IC_OutputPin_Icon.png",
                NotchPin(display, "CircuitsICOutputNotchPin", input: false),
                display,
                displaySimulation,
                toolbar);

            AddPin(
                BoardReader.InputPinGroup,
                "CircuitsICInputPin",
                "IC_InputPin_Icon.png",
                new VanillaCloneBuilder(constantSignal, new BuildingDefinitionId("CircuitsICInputPin")),
                constantSignal,
                constantSimulation,
                toolbar: null);
            AddPin(
                BoardReader.OutputPinGroup,
                "CircuitsICOutputPin",
                "IC_OutputPin_Icon.png",
                new VanillaCloneBuilder(display, new BuildingDefinitionId("CircuitsICOutputPin")),
                display,
                displaySimulation,
                toolbar: null);
        }

        private static IBuildingBuilder NotchPin(BuildingDefinitionId vanilla, string name, bool input)
        {
            return new ReshapedCloneBuilder(
                vanilla,
                new ReshapedCloneBuilder.Variant(
                    new BuildingDefinitionId(name),
                    PinModel.Connectors(input, mirrored: false),
                    drawData => PinModel.DrawData(input, mirrored: false, drawData)),
                new ReshapedCloneBuilder.Variant(
                    new BuildingDefinitionId(name + "Mirrored"),
                    PinModel.Connectors(input, mirrored: true),
                    drawData => PinModel.DrawData(input, mirrored: true, drawData)));
        }

        private static void AddPin(
            BuildingDefinitionGroupId groupId,
            string name,
            string icon,
            IBuildingBuilder building,
            BuildingDefinitionId vanilla,
            Func<BuildingDefinition, SimulationSystemsDependencies, ISimulationSystem> simulation,
            IToolbarEntryInsertLocation toolbar)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");
            IBuildingGroupBuilder group = BuildingGroup.Create(groupId)
               .WithTitle($"building-variant.{name}.title".T())
               .WithDescription($"building-variant.{name}.description".T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(resources.SubPath(icon), out _))
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
               // A board's notches are where its pins go, as space belt ports use a platform's notches.
               .AllowedOnNotches()
               .WithDefaultStructureOverview();

            CustomSimulationBuilding.Register(
                building,
                group,
                new UnlockBuildingWithExistingSideUpgradeResearchProgressionExtender(new SideUpgradeUnlocking(AndGateGroup)),
                toolbar,
                simulation,
                lookup => VanillaSidePanel.Of(lookup, vanilla));
        }

        // A 1x1 package with four two-way sides, running the circuit in its configuration (empty when placed).
        private static void AddICPackage(IToolbarEntryInsertLocation toolbar, ILogger logger)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");
            IBuildingGroupBuilder group = BuildingGroup.Create(IC.Firmware.ChipGroup)
               .WithTitle("building-variant.CircuitsICPackage1x1.title".T())
               .WithDescription("building-variant.CircuitsICPackage1x1.description".T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(resources.SubPath("IC_Icon.png"), out _))
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
               .WithDefaultStructureOverview();

            IBuildingConnectorData connectors = BuildingSignalTwoWay.SingleTile();
            var drawData = new ICPackageDrawData();

            IBuildingBuilder package = Building.Create(new BuildingDefinitionId("CircuitsICPackage1x1"))
               .WithConnectorData(connectors)
               .DynamicallyRendering<ICPackageDrawDataCarrier, ICPackageSimulation, ICPackageDrawData>(drawData)
               .WithStaticDrawData(ICPackageModel.DrawData(drawData))
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithoutEfficiencyData();

            CustomSimulationBuilding.Register(
                new AttachingBuildingBuilder(package, new ICPackageConfigurationFactory()),
                group,
                new UnlockBuildingWithExistingSideUpgradeResearchProgressionExtender(new SideUpgradeUnlocking(AndGateGroup)),
                toolbar,
                (definition, _) => new ICPackageSimulationSystem(definition.Id, logger),
                _ => new ICPackageModules());
        }

        private static IBuildingConnectorData LatchConnectors(TileDirection data)
        {
            return BuildingConnectors.SingleTile()
               .AddWireInput(WireConnectorConfig.CustomInput(data))
               .AddWireInput(WireConnectorConfig.CustomInput(TileDirection.West))
               .AddWireOutput(WireConnectorConfig.CustomOutput(TileDirection.East))
               .Build();
        }

        private static LODMeshAsset TransistorWindow(GameBuildings buildings, BuildingDefinitionId model, ILogger logger)
        {
            if (buildings.TryGetDefinition(model, out IBuildingDefinition transistor)
                && transistor.CustomData.TryGet(out IBuildingCustomDrawData drawData)
                && drawData is LogicGateIfMetaBuildingDefinition.DrawData transistorDrawData)
            {
                return transistorDrawData.ActiveIndicatorMesh;
            }

            logger.Warning?.Log($"[Circuits] {model} window not found; latches will have no reset marker");
            return null;
        }
    }
}
