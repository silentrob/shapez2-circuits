using System;
using System.Linq;
using Game.Core.Content.Buildings;
using ShapezShifter.Flow.Atomic;

namespace Circuits.Kit
{
    // Selects the research side upgrade that unlocks a vanilla building group, so a modded building
    // unlocks alongside it in every scenario that has it (e.g. the logic gates research).
    public class SideUpgradeUnlocking : ISideUpgradeSelector
    {
        private readonly BuildingDefinitionGroupId GroupId;

        public SideUpgradeUnlocking(BuildingDefinitionGroupId groupId)
        {
            GroupId = groupId;
        }

        public ResearchSideUpgrade Select(ScenarioId scenarioId, ResearchProgression progression)
        {
            return progression.SideUpgrades.FirstOrDefault(Unlocks)
                   ?? throw new Exception($"No side upgrade unlocks {GroupId} in scenario {scenarioId}");
        }

        private bool Unlocks(ResearchSideUpgrade upgrade)
        {
            return upgrade.Rewards.OfType<ResearchRewardBuildingGroup>()
                          .Any(reward => reward.BuildingDefinitionGroupId.Equals(GroupId));
        }
    }
}
