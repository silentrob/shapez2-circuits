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
    public static class CustomSimulationBuilding
    {
        public static void Register(
            IBuildingBuilder building,
            IBuildingGroupBuilder group,
            IBuildingResearchProgressionExtender research,
            IToolbarEntryInsertLocation toolbarLocation,
            Func<BuildingDefinition, SimulationSystemsDependencies, ISimulationSystem> simulationSystem,
            IBuildingModules modules)
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

                RewirerChainLink<PlacementToolbarElementData> toolbar =
                    placement.ThenContinueRewiringWith<PlacementToolbarElementData>(
                        result => ToolbarEntry(result, toolbarLocation));

                RewirerChainLink sidePanel = buildings.ThenContinueRewiringWith(
                    definition => new BuildingModulesExtender(definition, new CustomBuildingsModulesData(modules)));

                IWaitAllRewirers all = AggregatedChain.WaitFor(simulation).And(toolbar).And(sidePanel);
                all.AfterHijack.Register(OnApplied);

                void OnApplied()
                {
                    all.AfterHijack.Unregister(OnApplied);
                    BuildExtenders();
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
                Applied.Invoke();
            }
        }
    }
}
