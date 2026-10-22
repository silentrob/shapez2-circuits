using System;
using Circuits.Kit;
using ShapezShifter.Kit;
using UnityEngine;

namespace Circuits.IC.Package
{
    // The IC package's look, made in code: a dark chip body on three silver legs per side, with an accent bar on
    // its north edge marking the top (pin 1), so its orientation is clear however it is rotated. Which sides are
    // inputs or outputs depends on the circuit loaded, so those are drawn live (ICPackageRenderer) as arrows on
    // the body, using SideArrow.
    public static class ICPackageModel
    {
        // Palette cells (MaterialLUT column, row).
        private static readonly Vector2 Body = PaletteMesh.Cell(6, 3); // Pl: near-black
        private static readonly Vector2 Legs = PaletteMesh.Cell(3, 3); // Mb: bright silver
        private static readonly Vector2 Socket = PaletteMesh.Cell(3, 2); // Mb: silver, a shade darker
        // Ac: the accent colour vanilla buildings mark their connectors with (follows the player's accent
        // theme). Not the Em column: that one is emissive, and blooms into a glare.
        private static readonly Vector2 PinOne = PaletteMesh.Cell(1, 1);

        public const float BodyTop = 0.21f;
        private const float BodyHalf = 0.32f;

        public static BuildingDrawData DrawData(IBuildingCustomDrawData customDrawData)
        {
            Mesh mesh = Chip();
            LOD6Mesh lods = MeshLod.Create().AddLod0Mesh(mesh).BuildLod6Mesh();
            ((ILODMesh)lods).TryGet(0, out IMeshReference closest);
            return new BuildingDrawData(
                renderVoidBelow: false,
                mainMeshPerLayer: new ILODMesh[] { lods, lods, lods },
                isolatedBlueprintMesh: lods,
                combinedBlueprintMesh: lods,
                previewMesh: closest,
                glassMesh: new LODEmptyMesh(),
                colliders: BoundingBoxHelper.CreateBasicCollider(mesh),
                customDrawData: customDrawData,
                hasCustomOverviewMesh: false,
                customOverviewMesh: null,
                simulationRendererDrawsMainMesh: false);
        }

        // An arrow lying on the body, pointing north (+z); the renderer turns it to each side, and flips it to
        // point inward on input sides. Its tip stops short of the pin-1 bar. Drawn with the building material, not
        // the glowing wire materials, which bloom on a shape this small. Two colours: a side whose signal is off,
        // and one whose signal is on. Both from cells that render as plain colours: the Em column and the accent
        // rows past orange are emissive and bloom into a glare, and the Mc column renders dark and metallic.
        public static LOD6Mesh SideArrow(bool on)
        {
            Vector2 colour = on ? PaletteMesh.Cell(3, 3) : PaletteMesh.Cell(6, 4); // Mb: bright silver; Pl: dim grey
            Mesh arrow = new PaletteMesh()
               .Triangle(new(-0.075f, 0, -0.055f), new(0, 0, 0.06f), new(0.075f, 0, -0.055f), colour)
               .Build(on ? "CircuitsICSideArrowOn" : "CircuitsICSideArrowOff");
            return MeshLod.Create().AddLod0Mesh(arrow).BuildLod6Mesh();
        }

        // How far from the centre each side's arrow sits.
        public const float ArrowDistance = 0.18f;

        private static Mesh Chip()
        {
            var mesh = new PaletteMesh()
               .Box(new(-BodyHalf, 0.05f, -BodyHalf), new(BodyHalf, BodyTop, BodyHalf), Body)
               .Box(new(-0.07f, BodyTop, 0.25f), new(0.07f, BodyTop + 0.015f, 0.30f), PinOne);

            foreach (Func<float, float, Vector3> side in Sides)
            {
                // A socket in the middle of each side, out to the tile edge, for a wire to plug into, as vanilla
                // buildings have where they connect.
                AddBox(mesh, side(-0.055f, BodyHalf - 0.02f), side(0.055f, 0.5f), 0.025f, 0.135f, Socket);

                // A leg either side of it, from under the body's edge.
                foreach (float along in new[] { -0.17f, 0.17f })
                {
                    AddBox(mesh, side(along - 0.035f, BodyHalf - 0.02f), side(along + 0.035f, 0.44f), 0f, 0.13f, Legs);
                }
            }

            return mesh.Build("CircuitsICPackage");
        }

        // A box between two ground-level corners (in either order), from bottom to top.
        private static void AddBox(PaletteMesh mesh, Vector3 a, Vector3 b, float bottom, float top, Vector2 cell)
        {
            Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
            mesh.Box(new Vector3(min.x, bottom, min.z), new Vector3(max.x, top, max.z), cell);
        }

        // Maps (position along the side, distance out from the centre) to a point on each side, at ground level.
        private static readonly Func<float, float, Vector3>[] Sides =
        {
            (along, outward) => new Vector3(along, 0, outward), // north
            (along, outward) => new Vector3(outward, 0, along), // east
            (along, outward) => new Vector3(along, 0, -outward), // south
            (along, outward) => new Vector3(-outward, 0, along) // west
        };
    }
}
