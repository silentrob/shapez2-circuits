using JetBrains.Annotations;
using UnityEngine;

namespace Circuits.Latch
{
    public class LatchDrawData : IBuildingCustomDrawData
    {
        // The borrowed Transistor model's indicator window, filled in once the game has built its buildings.
        // Null until then, and if the Transistor's draw data ever changes shape.
        public LODMeshAsset Window;
    }

    // Draws the borrowed Transistor model's window in red, marking the latch's Reset side.
    // The game finds and constructs renderers by reflection.
    [UsedImplicitly]
    public class LatchRenderer : StatelessBuildingSimulationRenderer<LatchSimulation, LatchDrawData>
    {
        public LatchRenderer(IMapModel map) : base(map) { }

        protected override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
            LODMeshAsset window = entity.DrawData.Window;
            if (window == null || !window.TryGet(options.LOD.BuildingLOD, out IMeshReference mesh))
            {
                return;
            }

#pragma warning disable CS0618 // Theme is obsolete, but the vanilla Transistor and button renderers read it the same way
            MaterialReference red = options.Theme.BaseResources.WireConflictMaterial;
#pragma warning restore CS0618
            options.Renderers.DynamicBuildingParts.Add(
                mesh,
                red,
                FastMatrix.TranslateRotate(entity.Transform.Position.ToCenter_W(), entity.Transform.Rotation));
        }
    }
}
