using JetBrains.Annotations;

namespace Circuits.IC.Package
{
    public class ICPackageDrawData : IBuildingCustomDrawData { }

    // Draws nothing beyond the borrowed model for now. ShapezShifter needs custom draw data and a matching
    // renderer to copy a model; the game finds and constructs renderers by reflection.
    [UsedImplicitly]
    public class ICPackageRenderer : StatelessBuildingSimulationRenderer<ICPackageSimulation, ICPackageDrawData>
    {
        public ICPackageRenderer(IMapModel map) : base(map) { }
    }
}
