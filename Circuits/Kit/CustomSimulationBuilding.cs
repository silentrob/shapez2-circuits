using System;
using System.Collections.Generic;
using Core.Events;
using Game.Core.Content.Buildings;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Hijack;

namespace Circuits.Kit
{
    // ShapezShifter's AtomicBuildings.Extend() chain (research, building, placement, toolbar, side panel),
    // but with the simulation system supplied by the caller. AtomicBuildings always registers the game's
    // ConnectableBuildingSimulation, which binds connectors by their declared direction; buildings that
    // need their own connectable simulation (IC packages' two-way sides) register through this instead.
    // A building with a mirrored variant (see ReshapedCloneBuilder) or other variants (BuildingVariants) gets the
    // simulation and side panel for each.
    // With no toolbar location, the building stays usable (saves, blueprints) but cannot be picked to place.
    public static class CustomSimulationBuilding
    {
        public static void Register(
            IBuildingBuilder building,
            IBuildingGroupBuilder group,
            IBuildingResearchProgressionExtender research,
            IToolbarEntryInsertLocation toolbarLocation,
            Func<BuildingDefinition, SimulationSystemsDependencies, ISimulationSystem> simulationSystem,
            Func<BuildingsModulesLookup, IBuildingModules> sidePanel)
        {
            BuildExtenders();

            // As in ShapezShifter: each link applies once its predecessor has, and the chain re-arms once every
            // branch has applied, ready for the next savegame load.
            void BuildExtenders()
            {
                RewirerChainLink scenario = RewirerChain.BeginRewiringWith(new ResearchExtender(research, group.GroupId));

                RewirerChainLink<BuildingDefinition> buildings =
                    scenario.ThenContinueRewiringWith<BuildingDefinition>(() => new BuildingsExtender(building, group));

                RewirerChainLink simulation = buildings.ThenContinueRewiringWith(
                    definition => new SimulationSystemExtender(definition, simulationSystem));

                RewirerChainLink<BuildingPlacementResult> placement =
                    buildings.ThenContinueRewiringWith<BuildingPlacementResult>(
                        definition => new DefaultBuildingPlacementExtender(definition));

                RewirerChainLink modules = buildings.ThenContinueRewiringWith(
                    definition => new SidePanelExtender(definition, sidePanel));

                IWaitAllRewirers all;
                if (toolbarLocation != null)
                {
                    RewirerChainLink<PlacementToolbarElementData> toolbar =
                        placement.ThenContinueRewiringWith<PlacementToolbarElementData>(
                            result => ToolbarEntry(result, toolbarLocation));
                    all = AggregatedChain.WaitFor(simulation).And(toolbar).And(modules);
                }
                else
                {
                    all = AggregatedChain.WaitFor(simulation).And(placement).And(modules);
                }

                all.AfterHijack.Register(OnApplied);

                void OnApplied()
                {
                    all.AfterHijack.Unregister(OnApplied);
                    BuildExtenders();
                }
            }
        }

        private static IEnumerable<BuildingDefinition> Variants(BuildingDefinition definition)
        {
            if (definition.CustomData.TryGet(out IBuildingMirroringDefinition mirroring) && !mirroring.IsMirrored
                && mirroring.MirroredDefinition is BuildingDefinition mirrored)
            {
                yield return mirrored;
            }

            if (definition.CustomData.TryGet(out BuildingVariants variants))
            {
                foreach (BuildingDefinition variant in variants.Definitions)
                {
                    yield return variant;
                }
            }
        }

        private static ToolbarRewirer ToolbarEntry(BuildingPlacementResult placement, IToolbarEntryInsertLocation location)
        {
            var group = placement.Building.CustomData.Get<IBuildingDefinitionGroup>();
            return new ToolbarRewirer(placement.InitiatorId, group.Title, group.Description, group.Icon, location);
        }

        // ShapezShifter's GameScenarioBuildingExtender, which is internal; applies to every scenario.
        private class ResearchExtender : IGameScenarioRewirer, IChainableRewirer
        {
            private readonly IBuildingResearchProgressionExtender Research;
            private readonly BuildingDefinitionGroupId GroupId;
            private readonly MultiRegisterEvent Applied = new();

            public IEvent AfterHijack => Applied;

            public ResearchExtender(IBuildingResearchProgressionExtender research, BuildingDefinitionGroupId groupId)
            {
                Research = research;
                GroupId = groupId;
            }

            public GameScenario ModifyGameScenario(GameScenario gameScenario)
            {
                Research.ExtendResearch(gameScenario.UniqueId, gameScenario.Progression, GroupId);
                Applied.Invoke();
                return gameScenario;
            }
        }

        // As ShapezShifter's BuildingModulesExtender, but the provider is chosen with the lookup in hand, so a
        // building can borrow a vanilla building's provider (see VanillaSidePanel).
        private class SidePanelExtender : IBuildingModulesRewirer, IChainableRewirer
        {
            private readonly BuildingDefinition Definition;
            private readonly Func<BuildingsModulesLookup, IBuildingModules> SidePanel;
            private readonly MultiRegisterEvent Applied = new();

            public IEvent AfterHijack => Applied;

            public SidePanelExtender(BuildingDefinition definition, Func<BuildingsModulesLookup, IBuildingModules> sidePanel)
            {
                Definition = definition;
                SidePanel = sidePanel;
            }

            public void AddModules(BuildingsModulesLookup modulesLookup)
            {
                IBuildingModules provider = SidePanel(modulesLookup);
                modulesLookup.AddModule(Definition.Id, Definition, provider);
                foreach (BuildingDefinition variant in Variants(Definition))
                {
                    modulesLookup.AddModule(variant.Id, variant, provider);
                }

                Applied.Invoke();
            }
        }

        private class SimulationSystemExtender : ISimulationSystemsRewirer, IChainableRewirer
        {
            private readonly BuildingDefinition Definition;
            private readonly Func<BuildingDefinition, SimulationSystemsDependencies, ISimulationSystem> Create;
            private readonly MultiRegisterEvent Applied = new();

            public IEvent AfterHijack => Applied;

            public SimulationSystemExtender(
                BuildingDefinition definition,
                Func<BuildingDefinition, SimulationSystemsDependencies, ISimulationSystem> create)
            {
                Definition = definition;
                Create = create;
            }

            public void ModifySimulationSystems(
                ICollection<ISimulationSystem> simulationSystems,
                SimulationSystemsDependencies dependencies)
            {
                simulationSystems.Add(Create(Definition, dependencies));
                foreach (BuildingDefinition variant in Variants(Definition))
                {
                    simulationSystems.Add(Create(variant, dependencies));
                }

                Applied.Invoke();
            }
        }
    }
}
