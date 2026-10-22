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
using Unity.Mathematics;
using ILogger = Core.Logging.ILogger;

namespace Circuits.IC.Board
{
    // The IC Boards: platforms for designing circuits, in the package shapes (1x1, 2x1 and 2x2 chunks). Each
    // chunk is a foundation's (a 14x14 square, merged with its neighbours, and a notch on every outer edge,
    // where pins go) but with no space belt connectors, since a board never carries shapes or fluids.
    // BoardPlacementRules keeps them to signal buildings. Built as the official BiggerPlatforms sample builds
    // its foundations. Chunks are counted from the north-west chunk, x east and y south.
    public static class BoardIsland
    {
        // The 1x1 board keeps the id it had before boards came in sizes, so saved boards still load.
        public static readonly IslandDefinitionId Id = new("CircuitsICBoard");

        private static readonly IslandDefinitionGroupId FoundationGroup = new("FoundationGroup_1x1");

        private static readonly (PackageShape Shape, IslandDefinitionId Id, IslandDefinitionGroupId Group, string Name)[] Boards =
        {
            (PackageShape.Single, Id, new IslandDefinitionGroupId("CircuitsICBoardGroup"), "CircuitsICBoard"),
            (PackageShape.Wide, new IslandDefinitionId("CircuitsICBoard2x1"), new IslandDefinitionGroupId("CircuitsICBoard2x1Group"), "CircuitsICBoard2x1"),
            (PackageShape.Square, new IslandDefinitionId("CircuitsICBoard2x2"), new IslandDefinitionGroupId("CircuitsICBoard2x2Group"), "CircuitsICBoard2x2")
        };

        public static bool IsBoard(IslandDefinitionId island)
        {
            return Array.Exists(Boards, board => board.Id == island);
        }

        // The package shape a board builds, or null for any other platform.
        public static PackageShape ShapeOf(IslandDefinitionId island)
        {
            return Array.Find(Boards, board => board.Id == island).Shape;
        }

        public static void Register(BuildingDefinitionGroupId unlockedWith, ILogger logger)
        {
            // Each board after the one before it, falling back on the 1x1 foundation when that one's toolbar
            // entry is not there yet (see AfterToolbarEntry).
            var after = new List<IslandDefinitionGroupId> { FoundationGroup };
            foreach ((PackageShape shape, IslandDefinitionId id, IslandDefinitionGroupId groupId, string name) in Boards)
            {
                Register(shape, id, groupId, name, after.ToArray(), unlockedWith, logger);
                after.Insert(0, groupId);
            }
        }

        private static void Register(
            PackageShape shape,
            IslandDefinitionId id,
            IslandDefinitionGroupId groupId,
            string name,
            IslandDefinitionGroupId[] after,
            BuildingDefinitionGroupId unlockedWith,
            ILogger logger)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");

            IIslandGroupBuilder group = IslandGroup.Create(groupId)
               .WithTitle($"island-layout.{name}.title".T())
               .WithDescription($"island-layout.{name}.description".T())
               .WithIcon(FileTextureLoader.LoadTextureAsSprite(
                    resources.SubPath(shape.Equals(PackageShape.Single) ? "IC_Board_Icon.png" : $"IC_Board{shape}_Icon.png"),
                    out _))
               .AsNonTransportableIsland()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single);

            ChunkLayoutLookup<ChunkVector, IslandChunkData> layout = Layout(shape);
            IIslandBuilder island = Island.Create(id)
               .WithLayout(layout)
               .WithCustomCollider(Collider(shape))
               .WithConnectorData(new IslandConnectorData(
                    Array.Empty<EntityIO<LocalChunkPivot, IIslandConnector>>(),
                    layout.GetChunkPositions()))
               .WithInteraction(flippable: false, canHoldBuildings: true)
               .WithDefaultChunkCost()
               // No machine frame: BoardModel draws a circuit board around the floor instead.
               .WithRenderingOptions(new HomogeneousChunkDrawing(new ChunkPlatformDrawingContext()), drawPlayingField: true);

            AtomicIslands.Extend()
               .AllScenarios()
               .WithIsland(new BoardIslandBuilder(island, shape), group)
               .UnlockedWithExistingSideUpgrade(new SideUpgradeUnlocking(unlockedWith))
               .WithDefaultPlacement()
               .InToolbar(new AfterToolbarEntry(after, logger))
               .WithoutSimulation()
               .WithCustomModules(new BoardModules())
               .Build();
        }

        // What space view's hover and click test against. ShapezShifter's WithBoundingCollider spans from the
        // first chunk's origin to the last's, one chunk short each way, so a one-chunk platform gets a box of
        // size zero and can never be selected. Centred on the north-west chunk, in tiles (20 to a chunk), y south;
        // the height only needs to catch a ray from above.
        private static IEnumerable<CollisionBox> Collider(PackageShape shape)
        {
            const float chunk = CoordinateConstants.TilesPerIslandLayer;
            var centre = new LocalVector((shape.Width - 1) * chunk / 2, (shape.Height - 1) * chunk / 2, 0);
            return new[] { new CollisionBox(centre, new LocalDimension(new float3(shape.Width * chunk, shape.Height * chunk, 10))) };
        }

        // The shape's chunks, each with a notch on every edge that has no neighbouring chunk, as foundations have.
        private static ChunkLayoutLookup<ChunkVector, IslandChunkData> Layout(PackageShape shape)
        {
            ChunkVector[] chunks =
                (from y in Enumerable.Range(0, shape.Height)
                 from x in Enumerable.Range(0, shape.Width)
                 select new ChunkVector(x, y, 0)).ToArray();

            return new ChunkLayoutLookup<ChunkVector, IslandChunkData>(chunks.Select(chunk =>
            {
                ChunkDirection[] notches = GridRotation.RotationsInClockwiseOrder
                   .Select(rotation => rotation.ToChunkDirection())
                   .Where(direction => !chunks.Contains(chunk + ChunkVector.ByDirection(direction)))
                   .ToArray();
                IslandChunkData data = IslandLayoutFactory.CreateIslandChunkData(chunk, notches, chunks, true, false, out _);
                return new KeyValuePair<ChunkVector, IslandChunkData>(chunk, data);
            }));
        }
    }
}
