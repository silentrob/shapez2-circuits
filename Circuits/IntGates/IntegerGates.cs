using System;
using System.Collections.Generic;
using System.Linq;
using Circuits.Kit;
using Core.Factory;
using Core.Localization;
using Game.Content.Features.Signals;
using Game.Content.Features.Signals.Conductor;
using Game.Content.Features.Signals.Connections;
using Game.Content.Features.Signals.Simulation;
using Game.Content.Features.Signals.Tick;
using Game.Core.Content.Buildings;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using Game.Core.Serialization;
using Game.Core.Simulation;
using JetBrains.Annotations;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Kit;
using ShapezShifter.Textures;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace Circuits.IntGates
{
    // The integer gates: buildings that work on the whole 32-bit number a wire carries (see IntegerOps), usable
    // on any platform and inside packages. Styled after the vanilla Compare gate: one building per family, its
    // operation picked from its side panel and drawn on top. Inputs come from the west (A) and north (B), the
    // result goes east.
    public static class IntegerGates
    {
        public sealed class Family
        {
            public readonly string Name;
            public readonly IntegerOp[] Ops;
            public readonly string[] Labels;
            public readonly string[] InputNames;
            public readonly BuildingDefinitionGroupId Group;

            public Family(string name, IntegerOp[] ops, string[] labels, string[] inputNames)
            {
                Name = name;
                Ops = ops;
                Labels = labels;
                InputNames = inputNames;
                Group = new BuildingDefinitionGroupId(name + "Group");
            }

            public int Inputs => IntegerOps.TakesTwo(Ops[0]) ? 2 : 1;

            public IntegerOp OpOf(IBuildingConfiguration configuration)
            {
                int mode = configuration is IntegerGateConfiguration gate ? gate.Mode : 0;
                return Ops[mode >= 0 && mode < Ops.Length ? mode : 0];
            }

            public string LabelOf(IBuildingConfiguration configuration)
            {
                return Labels[Array.IndexOf(Ops, OpOf(configuration))];
            }
        }

        public static readonly Family[] Families =
        {
            new("CircuitsBitwiseGate", new[] { IntegerOp.And, IntegerOp.Or, IntegerOp.Xor }, new[] { "AND", "OR", "XOR" }, new[] { "A", "B" }),
            new("CircuitsNotGate", new[] { IntegerOp.Not }, new[] { "NOT" }, new[] { "In" }),
            new("CircuitsShiftGate", new[] { IntegerOp.ShiftLeft, IntegerOp.ShiftRight }, new[] { "<<", ">>" }, new[] { "Value", "Amount" }),
            new("CircuitsArithmeticGate", new[] { IntegerOp.Add, IntegerOp.Subtract }, new[] { "+", "-" }, new[] { "A", "B" })
        };

        public static Family FamilyOf(BuildingDefinitionGroupId group)
        {
            return Array.Find(Families, family => family.Group.Equals(group));
        }

        public static void Register(BuildingDefinitionGroupId unlockedWith, BuildingDefinitionGroupId toolbarNeighbour, ILogger logger)
        {
            ModFolderLocator resources = ModDirectoryLocator.CreateLocator<CircuitsMod>().SubLocator("Resources");
            Sprite[] icons = Families
               .Select(family => FileTextureLoader.LoadTextureAsSprite(resources.SubPath($"{family.Name}_Icon.png"), out _))
               .ToArray();
            var toolbar = new ToolbarGroup(
                toolbarNeighbour,
                "toolbar.circuits-integer-gates.title",
                "toolbar.circuits-integer-gates.description",
                icons[0],
                icons);

            for (int i = 0; i < Families.Length; i++)
            {
                Register(Families[i], icons[i], toolbar, unlockedWith, logger);
            }
        }

        private static void Register(Family family, Sprite icon, IToolbarEntryInsertLocation toolbar, BuildingDefinitionGroupId unlockedWith, ILogger logger)
        {
            IBuildingGroupBuilder group = BuildingGroup.Create(family.Group)
               .WithTitle($"building-variant.{family.Name}.title".T())
               .WithDescription($"building-variant.{family.Name}.description".T())
               .WithIcon(icon)
               .AsNonTransportableBuilding()
               .WithPreferredPlacement(DefaultPreferredPlacementMode.Single);

            ISingleTileConnectorDataBuilder connectors = BuildingConnectors.SingleTile()
               .AddWireInput(WireConnectorConfig.CustomInput(TileDirection.West));
            if (family.Inputs == 2)
            {
                connectors = connectors.AddWireInput(WireConnectorConfig.CustomInput(TileDirection.North));
            }

            var drawData = new IntegerGateDrawData(family);
            IBuildingBuilder building = Building.Create(new BuildingDefinitionId(family.Name))
               .WithConnectorData(connectors.AddWireOutput(WireConnectorConfig.CustomOutput(TileDirection.East)).Build())
               .DynamicallyRendering<IntegerGateRenderer, IntegerGateSimulation, IntegerGateDrawData>(drawData)
               .WithStaticDrawData(IntegerGateModel.DrawData(family, drawData))
               .WithoutSound()
               .WithoutSimulationConfiguration()
               .WithoutEfficiencyData();

            CustomSimulationBuilding.Register(
                new TextFromDisplayBuilder(new AttachingBuildingBuilder(building, new IntegerGateConfigurationFactory())),
                group,
                new UnlockBuildingWithExistingSideUpgradeResearchProgressionExtender(new SideUpgradeUnlocking(unlockedWith)),
                toolbar,
                (definition, _) => new AtomicStatefulBuildingSimulationSystem<IntegerGateSimulation, IntegerGateSimulationState, IntegerGateConfiguration>(
                    new IntegerGateSimulationFactory(family),
                    definition.Id,
                    logger),
                _ => new IntegerGateModules(family));
        }
    }

    // A gate's operation: an index into its family's operations, changed from its side panel as the Compare
    // gate's is.
    public class IntegerGateConfiguration : IBuildingConfiguration
    {
        public int Mode;

        public void Sync(ISerializationVisitor visitor)
        {
            if (visitor.Writing)
            {
                visitor.WriteByte_1((byte)Mode);
            }
            else
            {
                Mode = visitor.ReadByte_1();
            }
        }

        public bool Equals(IEntityConfiguration other)
        {
            return other is IntegerGateConfiguration gate && gate.Mode == Mode;
        }
    }

    public class IntegerGateConfigurationFactory : IFactory<IBuildingConfiguration>
    {
        public IBuildingConfiguration Produce()
        {
            return new IntegerGateConfiguration();
        }
    }

    // Saved with each gate. The identifier is written into savegames: never rename it.
    [SyncableIdentifier("Circuits.IntegerGateState")]
    public class IntegerGateSimulationState : ISimulationState
    {
        public readonly SignalConductorInputState AState = new();
        public readonly SignalConductorInputState BState = new();

        public void Sync(ISerializationVisitor visitor)
        {
            AState.Sync(visitor);
            BState.Sync(visitor);
        }
    }

    // Mirrors the vanilla two-input gate's tick loop, so the gate adds no delay; reads its operation from its
    // configuration every tick, as the Compare gate does, so changing it applies at once.
    public class IntegerGateSimulation : Simulation<IntegerGateSimulationState>, ISignalSimulation, IUpdatableSimulation
    {
        public readonly IntegerGates.Family Family;
        public readonly SignalConductorInput AConductor;
        public readonly SignalConductorInput BConductor;
        public readonly SignalConductorOutput OutputConductor;
        private readonly IBuildingConfiguration Configuration;

        public int NumSignalProviders => 1;
        public int NumSignalReceivers => Family.Inputs;

        public IntegerGateSimulation(IntegerGates.Family family, IBuildingConfiguration configuration, IntegerGateSimulationState state)
            : base(state)
        {
            Family = family;
            Configuration = configuration;
            AConductor = new SignalConductorInput(state.AState);
            BConductor = new SignalConductorInput(state.BState);
            OutputConductor = new SignalConductorOutput();
        }

        // Receiver indices follow the order of the wire inputs in the connector data: A (west), then B (north).
        public ISignalReceiver GetSignalReceiver(int index)
        {
            return index == 0 ? AConductor : BConductor;
        }

        public ISignalProvider GetSignalProvider(int index)
        {
            return OutputConductor;
        }

        public void Update(Ticks startTicks, Ticks deltaTicks)
        {
            int signals = SignalSimulation.GetAmountOfSignalsThisUpdate(startTicks, deltaTicks);
            SignalTicks first = SignalTicks.FromTicks(startTicks);
            IntegerOp op = Family.OpOf(Configuration);
            for (int i = 0; i < signals; i++)
            {
                SignalTicks tick = new(first.NumOfTicks + i);
                AConductor.TryPopSignal(startTicks, tick, out ISignal a);
                ISignal b = NullSignal.Instance;
                if (Family.Inputs == 2)
                {
                    BConductor.TryPopSignal(startTicks, tick, out b);
                }

                OutputConductor.PushSignal(IntegerOps.Apply(op, a, b), startTicks, tick);
            }
        }
    }

    public class IntegerGateSimulationFactory : IFactory<IntegerGateConfiguration, IntegerGateSimulationState, IntegerGateSimulation>
    {
        private readonly IntegerGates.Family Family;

        public IntegerGateSimulationFactory(IntegerGates.Family family)
        {
            Family = family;
        }

        public IntegerGateSimulation Produce(IntegerGateConfiguration configuration, IntegerGateSimulationState state)
        {
            return new IntegerGateSimulation(Family, configuration, state);
        }
    }

    public class IntegerGateDrawData : IBuildingCustomDrawData
    {
        public readonly IntegerGates.Family Family;

        public IntegerGateDrawData(IntegerGates.Family family)
        {
            Family = family;
        }
    }

    // The font and material the vanilla Display writes numbers with, which the gates write their operation with.
    // Filled in once the game has built its buildings.
    public static class GateText
    {
        public static TMP_FontAsset Font;
        public static MaterialReference Material;
    }

    // After the gate is registered, borrows the Display's text font and material (see GateText).
    public class TextFromDisplayBuilder : IBuildingBuilder
    {
        private static readonly BuildingDefinitionId Display = new("DisplayDefaultInternalVariant");
        private readonly IBuildingBuilder Inner;

        public TextFromDisplayBuilder(IBuildingBuilder inner)
        {
            Inner = inner;
        }

        public BuildingDefinition BuildAndRegister(BuildingDefinitionGroup group, GameBuildings gameBuildings)
        {
            BuildingDefinition definition = Inner.BuildAndRegister(group, gameBuildings);
            if (gameBuildings.TryGetDefinition(Display, out IBuildingDefinition display)
                && display.CustomData.Get<IBuildingDrawData>().CustomDrawData is DisplayMetaBuildingDefinition.DrawData text)
            {
                GateText.Font = text.Font;
                GateText.Material = text.TextMaterial3D;
            }

            return definition;
        }
    }

    // Writes each gate's operation on its top, as the vanilla Display writes its number.
    [UsedImplicitly]
    public class IntegerGateRenderer : StatelessBuildingSimulationRenderer<IntegerGateSimulation, IntegerGateDrawData>
    {
        private static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
        private readonly ITextMeshAccessor Text;
        private readonly MaterialPropertyBlock White = new();

        public IntegerGateRenderer(IMapModel map, ITextMeshAccessor text) : base(map)
        {
            Text = text;
            White.SetColor(FaceColor, new Color(0.92f, 0.92f, 0.92f));
        }

        protected override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
        {
            if (GateText.Font == null || options.LOD.BuildingLOD > 2 || !options.Viewport.IsTileVisible(entity.Transform.Position))
            {
                return;
            }

            IMeshReference mesh = Text.GetMeshReference(entity.DrawData.Family.LabelOf(entity.Building.Configuration), GateText.Font);
            if (mesh == null)
            {
                return;
            }

            float scale = TextMeshUtils.CalculateScale(mesh, 0.2f, new float2(0.42f, 0.3f));
            WorldCoordinate position = entity.Transform.Position.ToCenter_W(IntegerGateModel.Top + 0.005f);
            options.Renderers.RegularNonInstanced.DrawMesh(
                mesh,
                GateText.Material,
                Matrix4x4.TRS((Vector3)position, Quaternion.Euler(90f, 0f, 0f), new Vector3(scale, scale, scale)),
                RenderCategory.BuildingsDynamic,
                White);
        }
    }

    // A gate's side panel: its operation (for families with more than one), then each pin's value.
    public class IntegerGateModules : SimulationBasedBuildingModuleDataProvider<IntegerGateSimulation>
    {
        private readonly IntegerGates.Family Family;

        public IntegerGateModules(IntegerGates.Family family)
        {
            Family = family;
        }

        protected override IEnumerable<IHUDSidePanelModuleData> GetSimulationModules(
            BuildingModel building,
            ILocalizedSimulation localizedSimulation,
            IntegerGateSimulation gate)
        {
            if (Family.Ops.Length > 1 && building.Configuration is IntegerGateConfiguration configuration)
            {
                yield return new HUDSidePanelModuleDropdownSelector.Data(
                    Family.Labels.Select(label => (IText)new RawText(label)),
                    Math.Max(0, Array.IndexOf(Family.Ops, Family.OpOf(configuration))),
                    index => configuration.Mode = index);
            }

            yield return new HUDSidePanelModuleWireInfo.Data(Family.InputNames[0], gate.AConductor);
            if (Family.Inputs == 2)
            {
                yield return new HUDSidePanelModuleWireInfo.Data(Family.InputNames[1], gate.BConductor);
            }

            yield return new HUDSidePanelModuleWireInfo.Data("Output", gate.OutputConductor);
        }
    }

    // A gate's look, made in code in the style of the vanilla gates but in the board's teal: a dark base, a teal
    // body with chamfered edges and a dark panel on top for the operation, and a socket on each pin's side.
    public static class IntegerGateModel
    {
        private static readonly Vector2 Base = PaletteMesh.Cell(6, 3); // Pl: near-black
        private static readonly Vector2 Body = PaletteMesh.Cell(5, 13); // Mc: teal, as the IC Board
        private static readonly Vector2 Panel = PaletteMesh.Cell(6, 2); // Pl: darkest
        private static readonly Vector2 Socket = PaletteMesh.Cell(3, 2); // Mb: silver
        private static readonly Vector2 Out = PaletteMesh.Cell(1, 1); // Ac: the accent colour marks the output

        public const float Top = 0.3f;

        public static BuildingDrawData DrawData(IntegerGates.Family family, IBuildingCustomDrawData customDrawData)
        {
            var mesh = new PaletteMesh()
               .Chamfered(new(-0.44f, 0f, -0.44f), new(0.44f, 0.06f, 0.44f), 0.03f, Base)
               .Chamfered(new(-0.38f, 0.06f, -0.38f), new(0.38f, Top - 0.01f, 0.38f), 0.06f, Body)
               .Box(new(-0.25f, Top - 0.012f, -0.2f), new(0.25f, Top, 0.2f), Panel)
               .Box(new(-0.5f, 0.03f, -0.06f), new(-0.37f, 0.15f, 0.06f), Socket)
               .Box(new(0.37f, 0.03f, -0.06f), new(0.5f, 0.15f, 0.06f), Out);
            if (family.Inputs == 2)
            {
                mesh.Box(new(-0.06f, 0.03f, 0.37f), new(0.06f, 0.15f, 0.5f), Socket);
            }

            Mesh built = mesh.Build(family.Name);
            LOD6Mesh lods = MeshLod.Create().AddLod0Mesh(built).BuildLod6Mesh();
            ((ILODMesh)lods).TryGet(0, out IMeshReference closest);
            return new BuildingDrawData(
                renderVoidBelow: false,
                mainMeshPerLayer: new ILODMesh[] { lods, lods, lods },
                isolatedBlueprintMesh: lods,
                combinedBlueprintMesh: lods,
                previewMesh: closest,
                glassMesh: new LODEmptyMesh(),
                colliders: ClickBox.For(built),
                customDrawData: customDrawData,
                hasCustomOverviewMesh: false,
                customOverviewMesh: null,
                simulationRendererDrawsMainMesh: false);
        }
    }
}
