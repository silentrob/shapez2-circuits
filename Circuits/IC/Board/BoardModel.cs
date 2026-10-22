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
    // draws a substrate under the floor's edge, an accent line around the play area broken at each notch, traces
    // and pads on the border beside each notch and a mounting hole in each corner. Drawn by the game's island
    // mesh drawer, with the building material, so it uses the same palette as the chip and pins.
    //
    // Board space: tiles from the centre of the north-west chunk, x east and y south as the game counts them,
    // the floor at height 0. Each chunk is 20 tiles; the play area reaches 7 tiles past the outer chunks'
    // centres, and each notch is 4 tiles wide (centred on its chunk) and 1 deep. The island's mesh space is
    // Unity's, with z north, so y becomes -z.
    public static class BoardModel
    {
        // Palette cells (MaterialLUT column, row): see ICPackageModel.
        private static readonly Vector2 Substrate = PaletteMesh.Cell(5, 13); // Mc: teal, metallic
        private static readonly Vector2 EdgeLine = PaletteMesh.Cell(1, 1); // Ac: the player's accent colour
        private static readonly Vector2 Trace = PaletteMesh.Cell(3, 2); // Mb: silver
        private static readonly Vector2 Pad = PaletteMesh.Cell(3, 3); // Mb: bright silver
        private static readonly Vector2 Hole = PaletteMesh.Cell(6, 2); // Pl: near-black

        private const float Chunk = 20f;
        private const float PlayReach = 7f;
        private const float NotchHalf = 2f;
        private const float BoardReach = 8.7f;
        private const float SubstrateTop = -0.06f;

        public static IslandMeshDrawer.Data DrawData(PackageShape shape)
        {
            LOD6Mesh lods = MeshLod.Create().AddLod0Mesh(Mesh(shape)).BuildLod6Mesh();
            return new IslandMeshDrawer.Data(new ILODMeshMaterial[] { new RuntimeLODMeshMaterial(lods, new BuildingMaterial()) });
        }

        private static Mesh Mesh(PackageShape shape)
        {
            var mesh = new PaletteMesh();
            float east = (shape.Width - 1) * Chunk, south = (shape.Height - 1) * Chunk;

            mesh.Chamfered(
                ToMesh(-BoardReach, south + BoardReach, -0.7f),
                ToMesh(east + BoardReach, -BoardReach, SubstrateTop),
                0.2f,
                Substrate);

            foreach (Edge edge in Edges(shape))
            {
                // The accent line along the play area's edge, broken by each chunk's notch.
                float from = -PlayReach - 0.12f;
                for (int chunk = 0; chunk < edge.Chunks; chunk++)
                {
                    float notch = chunk * Chunk;
                    Flat(mesh, edge, from, notch - NotchHalf, PlayReach, PlayReach + 0.12f, SubstrateTop, 0.02f, EdgeLine);
                    from = notch + NotchHalf;
                }

                Flat(mesh, edge, from, (edge.Chunks - 1) * Chunk + PlayReach + 0.12f, PlayReach, PlayReach + 0.12f, SubstrateTop, 0.02f, EdgeLine);

                // Two traces either side of each notch, each ending in a pad.
                for (int chunk = 0; chunk < edge.Chunks; chunk++)
                {
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        foreach (float outward in new[] { 7.55f, 8.1f })
                        {
                            float start = chunk * Chunk + sign * (NotchHalf + 0.5f);
                            float end = chunk * Chunk + sign * (outward > 8f ? 6.2f : 6.8f);
                            Flat(mesh, edge, start, end, outward - 0.05f, outward + 0.05f, SubstrateTop, SubstrateTop + 0.02f, Trace);
                            Flat(mesh, edge, end - 0.14f, end + 0.14f, outward - 0.14f, outward + 0.14f, SubstrateTop, SubstrateTop + 0.03f, Pad);
                        }
                    }
                }
            }

            // Mounting holes in the corners: a silver ring (a chamfered block) with a dark centre.
            foreach (float x in new[] { -8f, east + 8f })
            {
                foreach (float y in new[] { -8f, south + 8f })
                {
                    mesh.Chamfered(ToMesh(x - 0.32f, y + 0.32f, SubstrateTop), ToMesh(x + 0.32f, y - 0.32f, SubstrateTop + 0.05f), 0.08f, Pad);
                    mesh.Box(ToMesh(x - 0.14f, y + 0.14f, SubstrateTop + 0.05f), ToMesh(x + 0.14f, y - 0.14f, SubstrateTop + 0.06f), Hole);
                }
            }

            return mesh.Build($"CircuitsICBoard{shape}");
        }

        // One outer edge of the board: where it starts, the way along it, the way out of the board, and how many
        // chunks it spans.
        private readonly struct Edge
        {
            public readonly Vector2 Start, Along, Outward;
            public readonly int Chunks;

            public Edge(Vector2 start, Vector2 along, Vector2 outward, int chunks)
            {
                Start = start;
                Along = along;
                Outward = outward;
                Chunks = chunks;
            }
        }

        private static IEnumerable<Edge> Edges(PackageShape shape)
        {
            float east = (shape.Width - 1) * Chunk, south = (shape.Height - 1) * Chunk;
            yield return new Edge(new(0, 0), new(1, 0), new(0, -1), shape.Width); // north
            yield return new Edge(new(east, 0), new(0, 1), new(1, 0), shape.Height); // east
            yield return new Edge(new(0, south), new(1, 0), new(0, 1), shape.Width); // south
            yield return new Edge(new(0, 0), new(0, 1), new(-1, 0), shape.Height); // west
        }

        // A box on one edge of the board, given along the edge (from its first chunk's centre) and outward from
        // the chunks' centres, between two heights.
        private static void Flat(PaletteMesh mesh, Edge edge, float along0, float along1, float out0, float out1, float bottom, float top, Vector2 cell)
        {
            Vector2 a = edge.Start + edge.Along * along0 + edge.Outward * out0;
            Vector2 b = edge.Start + edge.Along * along1 + edge.Outward * out1;
            mesh.Box(ToMesh(Mathf.Min(a.x, b.x), Mathf.Max(a.y, b.y), bottom), ToMesh(Mathf.Max(a.x, b.x), Mathf.Min(a.y, b.y), top), cell);
        }

        private static Vector3 ToMesh(float x, float y, float height)
        {
            return new Vector3(x, height, -y);
        }

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
