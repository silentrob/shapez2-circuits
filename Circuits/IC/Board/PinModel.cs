using System.Reflection;
using Circuits.Kit;
using Game.Core.Coordinates;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Kit;
using UnityEngine;

namespace Circuits.IC.Board
{
    // A notch pin's look, made in code: a dark 4x1 strip filling a board notch, like a circuit board's edge
    // connector. Its origin tile, where the wire plugs in (west, into the board), carries a bezelled housing on which the
    // vanilla renderer draws the pin's value; the other three carry a silver shroud on the outer edge, accent-coloured contact fingers, and
    // the first of them an arrow showing which way the signal flows. The strip runs south of the origin tile;
    // the mirrored variant runs north.
    public static class PinModel
    {
        // Palette cells (MaterialLUT column, row): see ICPackageModel for why these.
        private static readonly Vector2 Strip = PaletteMesh.Cell(6, 3); // Pl: near-black
        private static readonly Vector2 Housing = PaletteMesh.Cell(6, 4); // Pl: dim grey
        private static readonly Vector2 Socket = PaletteMesh.Cell(3, 2); // Mb: silver
        private static readonly Vector2 Arrow = PaletteMesh.Cell(3, 3); // Mb: bright silver
        private static readonly Vector2 Finger = PaletteMesh.Cell(1, 1); // Ac: the player's accent colour

        public const int Length = 4;

        // The tiles the pin covers, from the origin southward (or northward when mirrored). +y is south.
        public static IBuildingConnectorData Connectors(bool input, bool mirrored)
        {
            int step = mirrored ? -1 : 1;
            IMultiTileConnectorDataBuilder builder = BuildingConnectors.MultiTile();
            for (int tile = 1; tile < Length; tile++)
            {
                builder.AddTile(new TileVector(0, tile * step, 0));
            }

            var origin = new TileVector(0, 0, 0);
            return input
                ? builder.AddWireOutput(origin, WireConnectorConfig.CustomOutput(TileDirection.West)).Build()
                : builder.AddWireInput(origin, WireConnectorConfig.CustomInput(TileDirection.West)).Build();
        }

        public static BuildingDrawData DrawData(bool input, bool mirrored, IBuildingCustomDrawData customDrawData)
        {
            customDrawData = Fitted(customDrawData);
            Mesh mesh = Mesh(input, mirrored, ValueHeight(customDrawData));
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

        // The vanilla draw data with the value drawn smaller, to sit inside the housing's bezel: vanilla draws it
        // to fill most of a tile, and a Display draws larger than a Constant Signal. A copy, since the vanilla
        // building shares the original.
        private const float DisplayScale = 0.72f;
        private const float ConstantScale = 0.9f;

        private static IBuildingCustomDrawData Fitted(IBuildingCustomDrawData customDrawData)
        {
            switch (customDrawData)
            {
                case DisplayMetaBuildingDefinition.DrawData display:
                    var fittedDisplay = Copy(display);
                    fittedDisplay.BaseScale *= DisplayScale;
                    fittedDisplay.OverlayScale *= DisplayScale;
                    return fittedDisplay;
                case ConstantSignalMetaBuildingDefinition.DrawData constant:
                    var fittedConstant = Copy(constant);
                    fittedConstant.BaseScale *= ConstantScale;
                    fittedConstant.OverlayScale *= ConstantScale;
                    // Vanilla sets the value off-centre to suit its own model; ours centres it in the bezel.
                    fittedConstant.ValueRenderOrigin = new LocalVector(0f, 0f, constant.ValueRenderOrigin.z);
                    return fittedConstant;
                default:
                    return customDrawData;
            }
        }

        private static T Copy<T>(T original) where T : new()
        {
            var copy = new T();
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                field.SetValue(copy, field.GetValue(original));
            }

            return copy;
        }

        // How high the vanilla renderer draws the value, so the housing stays just below it.
        private static float ValueHeight(IBuildingCustomDrawData customDrawData)
        {
            return customDrawData switch
            {
                DisplayMetaBuildingDefinition.DrawData display => display.ValueRenderOrigin.z,
                ConstantSignalMetaBuildingDefinition.DrawData constant => constant.ValueRenderOrigin.z,
                _ => 0.25f
            };
        }

        private static Mesh Mesh(bool input, bool mirrored, float valueHeight)
        {
            // Mesh space has +z north; tiles run south (-z), or north when mirrored. The board is west (-x).
            float s = mirrored ? -1 : 1;
            var mesh = new PaletteMesh();
            const float deck = 0.07f;

            // The strip, and a silver shroud along its outer edge past the housing, like a connector's.
            Chamfered(mesh, -0.44f, 0.44f, 0.46f * s, -(Length - 0.54f) * s, 0f, deck, 0.03f, Strip);
            Chamfered(mesh, 0.3f, 0.42f, -0.6f * s, -(Length - 0.6f) * s, deck, deck + 0.04f, 0.015f, Socket);

            // The housing, with a silver bezel framing the value the vanilla renderer draws on it.
            float top = Mathf.Max(0.14f, valueHeight);
            Chamfered(mesh, -0.42f, 0.42f, 0.42f * s, -0.42f * s, deck, top - 0.02f, 0.04f, Housing);
            const float outer = 0.36f, inner = 0.31f;
            AddBox(mesh, -outer, outer, inner, outer, top - 0.025f, top + 0.01f, Socket);
            AddBox(mesh, -outer, outer, -inner, -outer, top - 0.025f, top + 0.01f, Socket);
            AddBox(mesh, -outer, -inner, -inner, inner, top - 0.025f, top + 0.01f, Socket);
            AddBox(mesh, inner, outer, -inner, inner, top - 0.025f, top + 0.01f, Socket);

            // Where the wire plugs in: a collar and a socket, out to the tile's edge.
            Chamfered(mesh, -0.5f, -0.36f, 0.08f * s, -0.08f * s, 0.02f, 0.16f, 0.02f, Socket);
            AddBox(mesh, -0.5f, -0.4f, 0.05f * s, -0.05f * s, 0.16f, 0.17f, Housing);

            // On the next tile, a raised plate with an arrow: towards the board for an input, away for an output.
            float z = -1f * s;
            Chamfered(mesh, -0.32f, 0.22f, z - 0.22f, z + 0.22f, deck, deck + 0.025f, 0.03f, Housing);
            float y = deck + 0.027f;
            float tip = input ? -0.26f : 0.16f, back = input ? 0.16f : -0.26f;
            mesh.Triangle(new(back, y, z - 0.15f), new(tip, y, z), new(back, y, z + 0.15f), Arrow);

            // Contact fingers on the last tiles.
            for (int tile = 2; tile < Length; tile++)
            {
                foreach (float offset in new[] { -0.26f, 0f, 0.26f })
                {
                    float centre = (-tile + offset) * s;
                    Chamfered(mesh, -0.32f, 0.24f, centre - 0.065f, centre + 0.065f, deck, deck + 0.03f, 0.02f, Finger);
                }
            }

            return mesh.Build($"CircuitsIC{(input ? "Input" : "Output")}Pin{(mirrored ? "Mirrored" : "")}");
        }

        // A box between two corners given as x and z ranges (in either order), from bottom to top.
        private static void AddBox(PaletteMesh mesh, float x0, float x1, float z0, float z1, float bottom, float top, Vector2 cell)
        {
            mesh.Box(
                new Vector3(Mathf.Min(x0, x1), bottom, Mathf.Min(z0, z1)),
                new Vector3(Mathf.Max(x0, x1), top, Mathf.Max(z0, z1)),
                cell);
        }

        private static void Chamfered(PaletteMesh mesh, float x0, float x1, float z0, float z1, float bottom, float top, float inset, Vector2 cell)
        {
            mesh.Chamfered(
                new Vector3(Mathf.Min(x0, x1), bottom, Mathf.Min(z0, z1)),
                new Vector3(Mathf.Max(x0, x1), top, Mathf.Max(z0, z1)),
                inset,
                cell);
        }
    }
}
