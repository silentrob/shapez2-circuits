using System.Collections.Generic;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using Game.Core.Rendering.Culling;
using Game.Core.Simulation;
using UnityEngine;

namespace Circuits.Kit
{
    // The game's StatelessBuildingSimulationRenderer for a building with its own connectable simulation type.
    // The game picks a renderer by (connectable simulation type, simulation type), and its building renderers
    // are fixed to ConnectableBuildingSimulation, so a building registered through a connectable simulation of
    // its own (an IC package's two-way sides) would get no renderer. Draws each visible building's dynamic
    // parts through OnDrawDynamic, culled by chunk as the vanilla renderer does. Subclasses are found and
    // constructed by the game by reflection (constructor dependencies are injected).
    public abstract class ConnectableRenderer<TConnectable, TSimulation>
        : SimulationRenderer<TConnectable, TSimulation>, IBuildingSimulationRenderer
        where TConnectable : ILocalizedTileSimulation
        where TSimulation : ISimulation
    {
        public readonly struct Entity
        {
            public readonly BuildingModel Building;
            public readonly TSimulation Simulation;
            public readonly GlobalTileTransform Transform;
            public readonly Bounds Bounds;

            public Entity(BuildingModel building, TSimulation simulation)
            {
                Building = building;
                Simulation = simulation;
                Transform = building.Transform;
                Bounds = building.Bounds_G.ToWorldBounds();
            }
        }

        protected readonly IMapModel Map;

        private readonly Dictionary<GlobalChunkCoordinate, Dictionary<ILocalizedSimulation, Entity>> EntitiesByChunk = new();
        private FrameDrawOptions CachedDrawOptions;

        protected ConnectableRenderer(IMapModel map)
        {
            Map = map;
        }

        public override bool ShouldDraw(FrameDrawOptionsNoLOD options)
        {
            return !options.InOverviewMode;
        }

        public override void Draw(FrameDrawOptionsNoLOD options, MapCullResult cullResult)
        {
            if (EntitiesByChunk.Count == 0)
            {
                return;
            }

            options.CloneIntoLOD(ref CachedDrawOptions);
            foreach (ChunkCullResult chunk in cullResult.Chunks)
            {
                if (!chunk.LOD.ShouldDrawDynamicBuildingSimulations
                    || !CachedDrawOptions.ShouldRenderPlatformContentsAtLayer(chunk.Chunk_G.z)
                    || !EntitiesByChunk.TryGetValue(chunk.Chunk_G, out Dictionary<ILocalizedSimulation, Entity> entities))
                {
                    continue;
                }

                CachedDrawOptions.LOD = chunk.LOD;
                foreach (Entity entity in entities.Values)
                {
                    if (chunk.FullyVisibleInFrustum || GeometryUtility.TestPlanesAABB(options.CameraPlanes, entity.Bounds))
                    {
                        OnDrawDynamic(in entity, CachedDrawOptions);
                    }
                }
            }
        }

        public override void RegisterSimulation(ILocalizedSimulation localizedSimulation)
        {
            var tiles = (ILocalizedTileSimulation)localizedSimulation;
            BuildingModel building = Map.GetBuilding(tiles.GetOccupiedTile(0));
            GlobalChunkCoordinate chunk = building.Transform.Position.ToChunkCoordinate();
            if (!EntitiesByChunk.TryGetValue(chunk, out Dictionary<ILocalizedSimulation, Entity> entities))
            {
                EntitiesByChunk.Add(chunk, entities = new Dictionary<ILocalizedSimulation, Entity>());
            }

            entities[localizedSimulation] = new Entity(building, (TSimulation)localizedSimulation.Simulation);
        }

        public override void UnregisterSimulation(ILocalizedSimulation localizedSimulation)
        {
            var tiles = (ILocalizedTileSimulation)localizedSimulation;
            GlobalChunkCoordinate chunk = tiles.GetOccupiedTile(0).ToChunkCoordinate();
            if (EntitiesByChunk.TryGetValue(chunk, out Dictionary<ILocalizedSimulation, Entity> entities)
                && entities.Remove(localizedSimulation)
                && entities.Count == 0)
            {
                EntitiesByChunk.Remove(chunk);
            }
        }

        protected abstract void OnDrawDynamic(in Entity entity, FrameDrawOptions options);
    }
}
