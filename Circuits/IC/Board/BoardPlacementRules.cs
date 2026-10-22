using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Circuits.IC.Compilation;
using Game.Core.Content.Buildings;
using MonoMod.RuntimeDetour;

namespace Circuits.IC.Board
{
    // Where buildings may go, around IC Boards:
    //   on a board, only buildings whose every connector is a signal connector, so no belts, pipes, machines,
    //     belt readers or pipe gates; and no wire transmitters to neighbouring platforms, as pins take their place;
    //   IC pins, only on a board.
    // The game asks BaseMapInteractionMode.AllowBuildingGroup both to place a building and to colour the
    // placement preview, so one hook covers both.
    public sealed class BoardPlacementRules : IDisposable
    {
        private static readonly FieldInfo ModeField =
            typeof(BaseMapInteractionMode).GetField("Mode", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly HashSet<string> NotOnBoards = new() { "WireTransmitterSenderVariant", "WireTransmitterReceiverVariant" };

        private readonly Hook Hook;

        // Whether each building group may go on a board, worked out once per game mode (its buildings).
        private GameMode CachedFor;
        private readonly Dictionary<BuildingDefinitionGroupId, bool> AllowedOnBoard = new();

        public BoardPlacementRules()
        {
            Hook = new Hook(
                typeof(BaseMapInteractionMode).GetMethod(nameof(BaseMapInteractionMode.AllowBuildingGroup)),
                new Func<Func<BaseMapInteractionMode, Player, BuildingDefinitionGroupId, IslandDefinitionId, bool>,
                    BaseMapInteractionMode, Player, BuildingDefinitionGroupId, IslandDefinitionId, bool>(Allow));
        }

        public void Dispose()
        {
            Hook.Dispose();
        }

        private bool Allow(
            Func<BaseMapInteractionMode, Player, BuildingDefinitionGroupId, IslandDefinitionId, bool> original,
            BaseMapInteractionMode mode,
            Player player,
            BuildingDefinitionGroupId group,
            IslandDefinitionId island)
        {
            bool allowed = original(mode, player, group, island);
            if (!allowed || island == IslandDefinitionId.Invalid)
            {
                return allowed;
            }

            bool onBoard = island == BoardIsland.Id;
            if (group.Equals(BoardReader.InputPinGroup) || group.Equals(BoardReader.OutputPinGroup))
            {
                return onBoard;
            }

            return !onBoard || IsAllowedOnBoard((GameMode)ModeField.GetValue(mode), group);
        }

        private bool IsAllowedOnBoard(GameMode gameMode, BuildingDefinitionGroupId group)
        {
            if (!ReferenceEquals(CachedFor, gameMode))
            {
                AllowedOnBoard.Clear();
                CachedFor = gameMode;
            }

            if (!AllowedOnBoard.TryGetValue(group, out bool allowed))
            {
                allowed = !NotOnBoards.Contains(group.Id)
                          && gameMode.Buildings.TryGetDefinitionGroup(group, out IBuildingDefinitionGroup definitions)
                          && definitions.Definitions.All(SignalOnly);
                AllowedOnBoard.Add(group, allowed);
            }

            return allowed;
        }

        private static bool SignalOnly(IBuildingDefinition definition)
        {
            return definition.CustomData.Get<IBuildingConnectorData>().AllBuildingConnectors.All(io => io is BuildingSignalIO);
        }
    }
}
