using System.Collections.Generic;
using Circuits.Kit;
using Game.Core.Rendering.Islands;
using ShapezShifter.Kit;
using UnityEngine;

namespace Circuits.IC.Board
{
    // The board's look: a circuit board, so designing on it reads as finer work than a machine platform. The
    // game draws a platform's floor with one material for every platform, so the board keeps the vanilla floor
    // (and its grid) and replaces the heavy machine frame around it: BoardIsland turns the frame off, and this
    // draws a substrate under the floor's edge, an accent line around the play area, traces and pads on the
    // border and a mounting hole in each corner. Drawn by the game's island mesh drawer, with the building
    // material, so it uses the same palette as the chip and pins.
    //
    // Island space: centred on the chunk, +x east, +y up, +z north, one tile = one unit, the floor at y = 0.
    // The play area spans -7..7 on both axes; each notch is 4 tiles wide (-2..2) and 1 deep, out to 8.
    public static class BoardModel
    {
        // Palette cells (MaterialLUT column, row): see ICPackageModel.
        private static readonly Vector2 Substrate = PaletteMesh.Cell(5, 13); // Mc: teal, metallic
        private static readonly Vector2 EdgeLine = PaletteMesh.Cell(1, 1); // Ac: the player's accent colour
        private static readonly Vector2 Trace = PaletteMesh.Cell(3, 2); // Mb: silver
        private static readonly Vector2 Pad = PaletteMesh.Cell(3, 3); // Mb: bright silver
        private static readonly Vector2 Hole = PaletteMesh.Cell(6, 2); // Pl: near-black

        private const float PlayHalf = 7f;
        private const float NotchHalf = 2f;
        private const float BoardHalf = 8.7f;
        private const float SubstrateTop = -0.06f;

        public static IslandMeshDrawer.Data DrawData()
        {
            LOD6Mesh lods = MeshLod.Create().AddLod0Mesh(Mesh()).BuildLod6Mesh();
            return new IslandMeshDrawer.Data(new ILODMeshMaterial[] { new RuntimeLODMeshMaterial(lods, new BuildingMaterial()) });
        }

        private static Mesh Mesh()
        {
            var mesh = new PaletteMesh();
            mesh.Chamfered(new(-BoardHalf, -0.7f, -BoardHalf), new(BoardHalf, SubstrateTop, BoardHalf), 0.2f, Substrate);

            foreach (Side side in Sides)
            {
                // The accent line along the play area's edge, broken by the notch.
                EdgeSegment(mesh, side, -PlayHalf - 0.12f, -NotchHalf);
                EdgeSegment(mesh, side, NotchHalf, PlayHalf + 0.12f);

                // Two traces either side of the notch, each ending in a pad.
                foreach (float sign in new[] { -1f, 1f })
                {
                    foreach (float outward in new[] { 7.55f, 8.1f })
                    {
                        float from = sign * (NotchHalf + 0.5f), to = sign * (outward > 8f ? 6.2f : 6.8f);
                        Flat(mesh, side, from, to, outward - 0.05f, outward + 0.05f, SubstrateTop, SubstrateTop + 0.02f, Trace);
                        Flat(mesh, side, to - 0.14f, to + 0.14f, outward - 0.14f, outward + 0.14f, SubstrateTop, SubstrateTop + 0.03f, Pad);
                    }
                }
            }

            // Mounting holes: a silver ring (a chamfered block) with a dark centre.
            foreach (float x in new[] { -8f, 8f })
            {
                foreach (float z in new[] { -8f, 8f })
                {
                    mesh.Chamfered(new(x - 0.32f, SubstrateTop, z - 0.32f), new(x + 0.32f, SubstrateTop + 0.05f, z + 0.32f), 0.08f, Pad);
                    mesh.Box(new(x - 0.14f, SubstrateTop + 0.05f, z - 0.14f), new(x + 0.14f, SubstrateTop + 0.06f, z + 0.14f), Hole);
                }
            }

            return mesh.Build("CircuitsICBoard");
        }

        private static void EdgeSegment(PaletteMesh mesh, Side side, float from, float to)
        {
            Flat(mesh, side, from, to, PlayHalf, PlayHalf + 0.12f, SubstrateTop, 0.02f, EdgeLine);
        }

        // A box on one side of the board, given along that side and outward from the centre.
        private static void Flat(PaletteMesh mesh, Side side, float along0, float along1, float out0, float out1, float bottom, float top, Vector2 cell)
        {
            Vector3 a = side(along0, out0), b = side(along1, out1);
            mesh.Box(
                new Vector3(Mathf.Min(a.x, b.x), bottom, Mathf.Min(a.z, b.z)),
                new Vector3(Mathf.Max(a.x, b.x), top, Mathf.Max(a.z, b.z)),
                cell);
        }

        private delegate Vector3 Side(float along, float outward);

        private static readonly Side[] Sides =
        {
            (along, outward) => new Vector3(along, 0, outward), // north
            (along, outward) => new Vector3(outward, 0, along), // east
            (along, outward) => new Vector3(along, 0, -outward), // south
            (along, outward) => new Vector3(-outward, 0, along) // west
        };

        // The theme's building material, looked up when first drawn: the theme is loaded with the game session,
        // after the board's definition is made.
        private class BuildingMaterial : ILODMaterial
        {
            private ILODMaterial Material;

            public IMaterialReference this[int index] => Resolve()?[index];

            public bool TryGet(int lod, out IMaterialReference materialReference)
            {
                ILODMaterial material = Resolve();
                if (material == null)
                {
                    materialReference = null;
                    return false;
                }

                return material.TryGet(lod, out materialReference);
            }

            private ILODMaterial Resolve()
            {
                return Material ??= Resources.Load<VisualThemeBaseResources>("SpaceThemeBaseResources")?.BuildingMaterial;
            }
        }
    }
}
