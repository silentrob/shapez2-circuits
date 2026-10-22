using System;
using System.Collections.Generic;
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

        public static BuildingDrawData DrawData(PackageShape shape, IBuildingCustomDrawData customDrawData)
        {
            return DrawData(shape, ChipVariants.AllConnections(shape), customDrawData);
        }

        // Made once per shape and set of connections, then reused for every map the game builds.
        private static readonly Dictionary<(PackageShape, int), Mesh> Meshes = new();

        // A chip with a socket only on the connections given (see ChipVariants).
        public static BuildingDrawData DrawData(PackageShape shape, int connections, IBuildingCustomDrawData customDrawData)
        {
            if (!Meshes.TryGetValue((shape, connections), out Mesh mesh))
            {
                Meshes.Add((shape, connections), mesh = Chip(shape, connections));
            }

            LOD6Mesh lods = MeshLod.Create().AddLod0Mesh(mesh).BuildLod6Mesh();
            ((ILODMesh)lods).TryGet(0, out IMeshReference closest);
            return new BuildingDrawData(
                renderVoidBelow: false,
                mainMeshPerLayer: new ILODMesh[] { lods, lods, lods },
                isolatedBlueprintMesh: lods,
                combinedBlueprintMesh: lods,
                previewMesh: closest,
                glassMesh: new LODEmptyMesh(),
                colliders: ClickBox.For(mesh),
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

        // Building space has z north and the shape's tiles run east (x) and south (-z) from the origin tile.
        private static Mesh Chip(PackageShape shape, int connections)
        {
            float east = shape.Width - 1, south = shape.Height - 1;
            var mesh = new PaletteMesh()
               .Box(new(-BodyHalf, 0.05f, -south - BodyHalf), new(east + BodyHalf, BodyTop, BodyHalf), Body)
               .Box(new(-0.07f, BodyTop, 0.25f), new(0.07f, BodyTop + 0.015f, 0.30f), PinOne);

            for (int connection = 0; connection < shape.Connections; connection++)
            {
                Func<float, float, Vector3> side = Sides[(int)shape.Locate(connection).Edge];
                (int x, int y) = shape.Tile(connection);
                var tile = new Vector3(x, 0, -y);

                // A socket in the middle of each used connection's edge, out to the tile edge, for a wire to plug
                // into, as vanilla buildings have where they connect.
                if (ChipVariants.Uses(connections, connection))
                {
                    AddBox(mesh, tile + side(-0.055f, BodyHalf - 0.02f), tile + side(0.055f, 0.5f), 0.025f, 0.135f, Socket);
                }

                // A leg either side of it, from under the body's edge.
                foreach (float along in new[] { -0.17f, 0.17f })
                {
                    AddBox(mesh, tile + side(along - 0.035f, BodyHalf - 0.02f), tile + side(along + 0.035f, 0.44f), 0f, 0.13f, Legs);
                }
            }

            return mesh.Build($"{ChipVariants.Name(shape)}Uses{connections}");
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
