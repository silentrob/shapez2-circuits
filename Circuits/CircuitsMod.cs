using System;
using Circuits.IC;
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

        public CircuitsMod(ILogger logger)
        {
            MirroredBuildingBuilder latch = AddLatch(logger);
            MirroredLatchHandle = GameRewirers.AddRewirer(new MirroredLatchRewirer(latch, logger));
            AddICPackage(logger);
            TwoWayHooks = CreateTwoWayHooks(logger);
        }

        public void Dispose()
        {
            GameRewirers.RemoveRewirer(MirroredLatchHandle);
            TwoWayHooks?.Dispose();
        }

        // The hooks only remove a cosmetic connection warning, so if a game update breaks them the mod carries on
        // without them rather than stopping the game from loading.
        private static TwoWayCompatibilityHooks CreateTwoWayHooks(ILogger logger)
        {
            try
            {
                return new TwoWayCompatibilityHooks();
            }
            catch (Exception e)
            {
                logger.Error?.Log($"[Circuits] Connection hooks failed; IC Packages will show connection warnings: {e}");
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

        // Milestone 1: a 1x1 package with four two-way sides running a fixed half adder.
        private static void AddICPackage(ILogger logger)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");

            IBuildingGroupBuilder group = BuildingGroup.Create(new BuildingDefinitionGroupId("CircuitsICPackage1x1Group"))
               .WithTitle("building-variant.CircuitsICPackage1x1.title".T())
               .WithDescription("building-variant.CircuitsICPackage1x1.description".T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(resources.SubPath("IC_Icon.png"), out _))
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
               .WithDefaultStructureOverview();

            IBuildingConnectorData connectors = BuildingSignalTwoWay.SingleTile();

            IBuildingBuilder building = Building.Create(new BuildingDefinitionId("CircuitsICPackage1x1"))
               .WithConnectorData(connectors)
               .DynamicallyRendering<ICPackageRenderer, ICPackageSimulation, ICPackageDrawData>(new ICPackageDrawData())
               .WithCopiedStaticDrawData(new BuildingDefinitionId("LogicGateAndInternalVariant"))
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithoutEfficiencyData();

            Netlist netlist = TestCircuits.HalfAdder();
            CustomSimulationBuilding.Register(
                building,
                group,
                new UnlockBuildingWithExistingSideUpgradeResearchProgressionExtender(new SideUpgradeUnlocking(AndGateGroup)),
                new AfterToolbarEntry(AndGateGroup, logger),
                (definition, _) => new ICPackageSimulationSystem(definition.Id, netlist, logger),
                new ICPackageModules());
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
