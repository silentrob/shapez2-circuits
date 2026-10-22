using Circuits.Kit;
using Game.Content.Features.Signals;
using JetBrains.Annotations;
using ShapezShifter.Kit;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace Circuits.IC.Package
{
    public class ICPackageDrawData : IBuildingCustomDrawData
    {
        public readonly LOD6Mesh SideArrowOff = ICPackageModel.SideArrow(on: false);
        public readonly LOD6Mesh SideArrowOn = ICPackageModel.SideArrow(on: true);
    }

    // Only satisfies the type constraint of ShapezShifter's DynamicallyRendering, which attaches the draw data.
    // Keyed to the game's ConnectableBuildingSimulation, which packages never use, so it never draws.
    [UsedImplicitly]
    public class ICPackageDrawDataCarrier : StatelessBuildingSimulationRenderer<ICPackageSimulation, ICPackageDrawData>
    {
        public ICPackageDrawDataCarrier(IMapModel map) : base(map) { }
    }

    // Draws a package's side arrows: on each side the loaded circuit uses, an arrow on the body pointing in (an
    // input) or out (an output), lit while that side's signal is on.
    // Packages register through their own connectable simulation, so this builds on ConnectableRenderer rather
    // than the game's building renderers.
    [UsedImplicitly]
    public class ICPackageRenderer : ConnectableRenderer<ICConnectableSimulation, ICPackageSimulation>
    {
        // Set by circuits.dumppalette; the next package drawn saves the building material's textures, since the
        // theme's materials are only at hand while drawing.
        public static volatile bool DumpPaletteRequested;
        public static ILogger Logger;

        public ICPackageRenderer(IMapModel map) : base(map) { }

        protected override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
            if (DumpPaletteRequested)
            {
                DumpPaletteRequested = false;
                DumpPalette(options);
            }

            var drawData = entity.Building.Definition.CustomDrawDataAs<ICPackageDrawData>();
            if (drawData == null)
            {
                return;
            }

#pragma warning disable CS0618 // Theme is obsolete, but the vanilla button renderer reads it the same way
            IMaterialReference material = options.Theme.BaseResources.BuildingMaterial[options.LOD.BuildingMaterialLOD];
#pragma warning restore CS0618

            ICPackageSimulation package = entity.Simulation;
            Matrix4x4 building = FastMatrix.ByTransform(in entity.Transform);
            PackageShape shape = package.Netlist.Shape;
            for (int side = 0; side < package.Netlist.Connections; side++)
            {
                SideRole role = package.Netlist.SideRoles[side];
                if (role == SideRole.Unused)
                {
                    continue;
                }

                ISignal value = role == SideRole.Input
                    ? package.SideInputs[side].GetMostRecent(force: true)
                    : package.SideOutputs[side].GetMostRecent();

                // Edges run north, east, south, west: a quarter turn each, clockwise from above. Each connection's
                // arrow sits on its own tile (building space has z north, so y south becomes -z).
                (Side edge, _) = shape.Locate(side);
                (int x, int y) = shape.Tile(side);
                Quaternion toSide = Quaternion.Euler(0, 90f * (int)edge, 0);
                Quaternion pointing = role == SideRole.Output ? toSide : toSide * Quaternion.Euler(0, 180f, 0);
                Vector3 position = new Vector3(x, 0, -y)
                                   + toSide * new Vector3(0, ICPackageModel.BodyTop + 0.003f, ICPackageModel.ArrowDistance);

                LOD6Mesh arrow = value.IsTruthy() ? drawData.SideArrowOn : drawData.SideArrowOff;
                if (((ILODMesh)arrow).TryGet(options.LOD.BuildingLOD, out IMeshReference mesh))
                {
                    options.Renderers.DynamicBuildingParts.Add(mesh, material, building * Matrix4x4.TRS(position, pointing, Vector3.one));
                }
            }
        }

        private static void DumpPalette(FrameDrawOptions options)
        {
#pragma warning disable CS0618 // as above
            var material = (MaterialReference)options.Theme.BaseResources.BuildingMaterial[options.LOD.BuildingMaterialLOD];
#pragma warning restore CS0618
            string folder = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubPath("palette-dump");
            TextureDump.Dump(material.GetMaterialInternal(), folder, Logger);
        }
    }
}
