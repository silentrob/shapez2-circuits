using System.Collections.Generic;
using UnityEngine;

namespace Circuits.Kit
{
    // Builds simple meshes for buildings, coloured the way the game colours building meshes: every vertex's UV
    // points at a cell of the building material's 16x16 palette texture (MaterialLUT; see TextureDump). A cell
    // is named by its column and row as labelled in that texture, e.g. column 6 ("Pl"), row 3.
    //
    // Building space: +x east, +y up, +z north, one tile = one unit, centred on the tile.
    public class PaletteMesh
    {
        private readonly List<Vector3> Vertices = new();
        private readonly List<Vector2> Uvs = new();
        private readonly List<int> Triangles = new();

        public static Vector2 Cell(int column, int row)
        {
            return new Vector2((column + 0.5f) / 16f, 1f - (row + 0.5f) / 16f);
        }

        public PaletteMesh Box(Vector3 min, Vector3 max, Vector2 cell)
        {
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            // Each face's corners run clockwise seen from outside, Unity's front-face winding.
            Quad(new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0), cell); // top
            Quad(new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1), cell); // bottom
            Quad(new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), new(x1, y0, z0), cell); // south (-z)
            Quad(new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1), new(x0, y0, z1), cell); // north (+z)
            Quad(new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), new(x1, y0, z1), cell); // east (+x)
            Quad(new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0), new(x0, y0, z0), cell); // west (-x)
            return this;
        }

        // A box whose top is inset by the given amount on every side, so its sides slope: a chamfered edge, as
        // vanilla buildings have, which catches the light where a plain box looks flat.
        public PaletteMesh Chamfered(Vector3 min, Vector3 max, float inset, Vector2 cell)
        {
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            float u0 = x0 + inset, u1 = x1 - inset, w0 = z0 + inset, w1 = z1 - inset;
            // As Box, with the top corners moved in.
            Quad(new(u0, y1, w0), new(u0, y1, w1), new(u1, y1, w1), new(u1, y1, w0), cell); // top
            Quad(new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1), cell); // bottom
            Quad(new(x0, y0, z0), new(u0, y1, w0), new(u1, y1, w0), new(x1, y0, z0), cell); // south (-z)
            Quad(new(x1, y0, z1), new(u1, y1, w1), new(u0, y1, w1), new(x0, y0, z1), cell); // north (+z)
            Quad(new(x1, y0, z0), new(u1, y1, w0), new(u1, y1, w1), new(x1, y0, z1), cell); // east (+x)
            Quad(new(x0, y0, z1), new(u0, y1, w1), new(u0, y1, w0), new(x0, y0, z0), cell); // west (-x)
            return this;
        }

        // A flat triangle, visible from above and below. Each face has its own vertices: sharing them would
        // average the two faces' opposite normals to zero, and an unlit face renders near-black.
        public PaletteMesh Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 cell)
        {
            int first = Vertices.Count;
            Vertices.AddRange(new[] { a, b, c, a, c, b });
            Uvs.AddRange(new[] { cell, cell, cell, cell, cell, cell });
            Triangles.AddRange(new[] { first, first + 1, first + 2, first + 3, first + 4, first + 5 });
            return this;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(Vertices);
            mesh.SetUVs(0, Uvs);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 cell)
        {
            int first = Vertices.Count;
            Vertices.AddRange(new[] { a, b, c, d });
            Uvs.AddRange(new[] { cell, cell, cell, cell });
            Triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }
    }
}
