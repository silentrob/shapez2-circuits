using System;
using System.Collections.Generic;
using System.Reflection;
using ShapezShifter.Flow;
using ILogger = Core.Logging.ILogger;

namespace Circuits.Kit
{
    // Wraps a ShapezShifter building builder so the building gets a mirrored variant (the F key), which
    // ShapezShifter does not support. The game flips between a group's two variants, the second being the
    // first with north and south swapped, so this registers that second variant right after the first,
    // in the same step, with a vanilla building's mirrored model.
    public class MirroredBuildingBuilder : IBuildingBuilder
    {
        private static readonly FieldInfo DefinitionsById =
            typeof(GameBuildings).GetField("_DefinitionsById", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo AddInternalVariant =
            typeof(BuildingDefinitionGroup).GetMethod("AddInternalVariant", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly IBuildingBuilder Primary;
        private readonly BuildingDefinitionId MirroredId;
        private readonly IBuildingConnectorData MirroredConnectors;
        private readonly BuildingDefinitionId MirroredModel;
        private readonly IBuildingCustomDrawData MirroredDrawData;
        private readonly Action<GameBuildings> AfterRegister;
        private readonly ILogger Logger;

        // The mirrored variant from the latest building creation; null when its model was missing.
        public BuildingDefinition Mirrored { get; private set; }

        public MirroredBuildingBuilder(
            IBuildingBuilder primary,
            BuildingDefinitionId mirroredId,
            IBuildingConnectorData mirroredConnectors,
            BuildingDefinitionId mirroredModel,
            IBuildingCustomDrawData mirroredDrawData,
            Action<GameBuildings> afterRegister,
            ILogger logger)
        {
            Primary = primary;
            MirroredId = mirroredId;
            MirroredConnectors = mirroredConnectors;
            MirroredModel = mirroredModel;
            MirroredDrawData = mirroredDrawData;
            AfterRegister = afterRegister;
            Logger = logger;
        }

        public BuildingDefinition BuildAndRegister(BuildingDefinitionGroup group, GameBuildings gameBuildings)
        {
            BuildingDefinition primary = Primary.BuildAndRegister(group, gameBuildings);
            Mirrored = null;
            AfterRegister(gameBuildings);

            if (!gameBuildings.TryGetDefinition(MirroredModel, out IBuildingDefinition model))
            {
                Logger.Warning?.Log($"[Circuits] {MirroredModel} not found; {primary.Id} cannot be mirrored");
                return primary;
            }

            var mirrored = new BuildingDefinition(MirroredId, MirroredConnectors);
            foreach (object data in primary.CustomData.All)
            {
                if (data is not (IBuildingConnectorData or IBuildingDrawData or IBuildingCustomDrawData))
                {
                    mirrored.CustomData.Attach(data);
                }
            }

            mirrored.CustomData.Attach(MirroredConnectors);
            mirrored.CustomData.Attach(MirroredDrawData);
            mirrored.CustomData.Attach(CopyDrawData(model.CustomData.Get<IBuildingDrawData>(), MirroredDrawData));

            primary.CustomData.Attach(new Mirroring(mirrored, isMirrored: false));
            mirrored.CustomData.Attach(new Mirroring(primary, isMirrored: true));

            AddInternalVariant.Invoke(group, new object[] { mirrored });
            ((Dictionary<BuildingDefinitionId, IBuildingDefinition>)DefinitionsById.GetValue(gameBuildings))
               .Add(MirroredId, mirrored);

            Mirrored = mirrored;
            return primary;
        }

        // As ShapezShifter's WithCopiedStaticDrawData, but from the given model and with our own draw data.
        private static BuildingDrawData CopyDrawData(IBuildingDrawData model, IBuildingCustomDrawData customDrawData)
        {
            return new BuildingDrawData(
                renderVoidBelow: model.RenderVoidBelow,
                mainMeshPerLayer: model.MainMeshPerLayer,
                isolatedBlueprintMesh: model.IsolatedBlueprintMesh,
                combinedBlueprintMesh: model.CombinedBlueprintMesh,
                previewMesh: model.PreviewMesh,
                glassMesh: model.GlassMesh,
                colliders: model.Colliders,
                customDrawData: customDrawData,
                hasCustomOverviewMesh: model.HasCustomOverviewMesh,
                customOverviewMesh: model.CustomOverviewMesh,
                simulationRendererDrawsMainMesh: model.SimulationRendererDrawsMainMesh);
        }

        private class Mirroring : IBuildingMirroringDefinition
        {
            public IBuildingDefinition MirroredDefinition { get; }
            public bool IsMirrored { get; }

            public Mirroring(IBuildingDefinition mirroredDefinition, bool isMirrored)
            {
                MirroredDefinition = mirroredDefinition;
                IsMirrored = isMirrored;
            }
        }
    }
}
