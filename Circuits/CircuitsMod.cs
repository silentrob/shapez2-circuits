using System;
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

        public CircuitsMod(ILogger logger)
        {
            MirroredBuildingBuilder latch = AddLatch(logger);
            MirroredLatchHandle = GameRewirers.AddRewirer(new MirroredLatchRewirer(latch, logger));
            BuildingDefinitionGroupId packageGroup = AddICPackage(logger);
            AddPins(logger);
            this.AttachSaveData<CircuitLibrary>();
            TwoWayHooks = Optional(
                () => new TwoWayCompatibilityHooks(),
                "IC Packages will show connection warnings",
                logger);
            Commands = Optional(
                () => new ICConsoleCommands(packageGroup, this.GetSaveData<CircuitLibrary>, logger),
                "the circuits console commands are unavailable",
                logger);
        }

        public void Dispose()
        {
            GameRewirers.RemoveRewirer(MirroredLatchHandle);
            TwoWayHooks?.Dispose();
            Commands?.Dispose();
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
        private static void AddPins(ILogger logger)
        {
            var constantSignal = new BuildingDefinitionId("ConstantSignalDefaultInternalVariant");
            AddPin(
                BoardReader.InputPinGroup,
                "CircuitsICInputPin",
                "IC_InputPin_Icon.png",
                constantSignal,
                (definition, _) => new AtomicStatelessBuildingSimulationSystem<ConstantSignalSimulation, ConstantSignalConfiguration>(
                    new ConstantSignalSimulationFactory(),
                    definition.Id,
                    logger),
                logger);

            var display = new BuildingDefinitionId("DisplayDefaultInternalVariant");
            AddPin(
                BoardReader.OutputPinGroup,
                "CircuitsICOutputPin",
                "IC_OutputPin_Icon.png",
                display,
                (definition, _) => new AtomicStatefulBuildingSimulationSystem<DisplaySimulation, DisplaySimulationState>(
                    new DisplaySimulationFactory(),
                    definition.Id,
                    logger),
                logger);
        }

        private static void AddPin(
            BuildingDefinitionGroupId groupId,
            string name,
            string icon,
            BuildingDefinitionId vanilla,
            Func<BuildingDefinition, SimulationSystemsDependencies, ISimulationSystem> simulation,
            ILogger logger)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");
            IBuildingGroupBuilder group = BuildingGroup.Create(groupId)
               .WithTitle($"building-variant.{name}.title".T())
               .WithDescription($"building-variant.{name}.description".T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(resources.SubPath(icon), out _))
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
               // A board's notches are where its pins naturally go, as space belt ports use a platform's notches.
               .AllowedOnNotches()
               .WithDefaultStructureOverview();

            CustomSimulationBuilding.Register(
                new VanillaCloneBuilder(vanilla, new BuildingDefinitionId(name)),
                group,
                new UnlockBuildingWithExistingSideUpgradeResearchProgressionExtender(new SideUpgradeUnlocking(AndGateGroup)),
                new AfterToolbarEntry(AndGateGroup, logger),
                simulation,
                lookup => VanillaSidePanel.Of(lookup, vanilla));
        }

        // A 1x1 package with four two-way sides, running the circuit in its configuration (empty when placed).
        private static BuildingDefinitionGroupId AddICPackage(ILogger logger)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");
            var groupId = new BuildingDefinitionGroupId("CircuitsICPackage1x1Group");

            IBuildingGroupBuilder group = BuildingGroup.Create(groupId)
               .WithTitle("building-variant.CircuitsICPackage1x1.title".T())
               .WithDescription("building-variant.CircuitsICPackage1x1.description".T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(resources.SubPath("IC_Icon.png"), out _))
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
               .WithDefaultStructureOverview();

            IBuildingConnectorData connectors = BuildingSignalTwoWay.SingleTile();

            IBuildingBuilder package = Building.Create(new BuildingDefinitionId("CircuitsICPackage1x1"))
               .WithConnectorData(connectors)
               .DynamicallyRendering<ICPackageRenderer, ICPackageSimulation, ICPackageDrawData>(new ICPackageDrawData())
               .WithCopiedStaticDrawData(new BuildingDefinitionId("LogicGateAndInternalVariant"))
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithoutEfficiencyData();

            CustomSimulationBuilding.Register(
                new AttachingBuildingBuilder(package, new ICPackageConfigurationFactory()),
                group,
                new UnlockBuildingWithExistingSideUpgradeResearchProgressionExtender(new SideUpgradeUnlocking(AndGateGroup)),
                new AfterToolbarEntry(AndGateGroup, logger),
                (definition, _) => new ICPackageSimulationSystem(definition.Id, logger),
                _ => new ICPackageModules());

            return groupId;
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
