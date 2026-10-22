using System;
using System.Collections.Generic;
using System.Linq;
using Circuits.Kit;
using Core.Localization;
using Game.Core.Content.Buildings;
using Game.Core.Content.Islands;
using Game.Core.Coordinates;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Kit;
using ShapezShifter.Textures;
using ILogger = Core.Logging.ILogger;

namespace Circuits.IC.Board
{
    // The IC Board: a platform for designing circuits, the same 1x1 chunk as a foundation (a 14x14 square and
    // a notch on each side, where pins go) but with no space belt connectors, since a board never carries
    // shapes or fluids. BoardPlacementRules keeps it to signal buildings. Built as the official
    // BiggerPlatforms sample builds its foundations.
    public static class BoardIsland
    {
        public static readonly IslandDefinitionId Id = new("CircuitsICBoard");
        private static readonly IslandDefinitionGroupId GroupId = new("CircuitsICBoardGroup");
        private static readonly IslandDefinitionGroupId FoundationGroup = new("FoundationGroup_1x1");

        public static void Register(BuildingDefinitionGroupId unlockedWith, ILogger logger)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");

            IIslandGroupBuilder group = IslandGroup.Create(GroupId)
               .WithTitle("island-layout.CircuitsICBoard.title".T())
               .WithDescription("island-layout.CircuitsICBoard.description".T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(resources.SubPath("IC_Board_Icon.png"), out _))
               .AsNonTransportableIsland()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single);

            ChunkLayoutLookup<ChunkVector, IslandChunkData> layout = SingleChunkLayout();
            IIslandBuilder island = Island.Create(Id)
               .WithLayout(layout)
               .WithBoundingCollider()
               .WithConnectorData(new IslandConnectorData(
                    Array.Empty<EntityIO<LocalChunkPivot, IIslandConnector>>(),
                    layout.GetChunkPositions()))
               .WithInteraction(flippable: false, canHoldBuildings: true)
               .WithDefaultChunkCost()
               .WithRenderingOptions(new HomogeneousChunkDrawing(ChunkPlatformDrawingContext.DrawAll()), drawPlayingField: true);

            AtomicIslands.Extend()
               .AllScenarios()
               .WithIsland(island, group)
               .UnlockedWithExistingSideUpgrade(new SideUpgradeUnlocking(unlockedWith))
               .WithDefaultPlacement()
               .InToolbar(new AfterToolbarEntry(FoundationGroup, logger))
               .WithoutSimulation()
               .WithoutModules()
               .Build();
        }

        // One chunk with a notch on every side, as a 1x1 foundation has.
        private static ChunkLayoutLookup<ChunkVector, IslandChunkData> SingleChunkLayout()
        {
            var chunk = new ChunkVector(0, 0, 0);
            ChunkDirection[] notches = GridRotation.RotationsInClockwiseOrder.Select(rotation => rotation.ToChunkDirection()).ToArray();
            IslandChunkData data = IslandLayoutFactory.CreateIslandChunkData(chunk, notches, new[] { chunk }, true, false, out _);
            return new ChunkLayoutLookup<ChunkVector, IslandChunkData>(
                new[] { new KeyValuePair<ChunkVector, IslandChunkData>(chunk, data) });
        }
    }
}
