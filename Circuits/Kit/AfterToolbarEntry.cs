using System;
using System.Linq;
using Game.Core.Content.Buildings;
using Game.Core.Content.Islands;
using ShapezShifter.Flow.Toolbar;
using ILogger = Core.Logging.ILogger;

namespace Circuits.Kit
{
    // Inserts a toolbar entry right after the vanilla entry for a building or platform group, wherever it sits
    // in the toolbar tree. Unlike ShapezShifter's index-based locators this survives the toolbar being reordered.
    // Platform groups can be given in order of preference: the game adds mod toolbar entries in no set order, so
    // an entry meant to follow another mod entry names a vanilla one to fall back on, which keeps it in the
    // right toolbar (an island entry among buildings would switch to the machine view mid-placement).
    public class AfterToolbarEntry : IToolbarEntryInsertLocation
    {
        private readonly Func<IToolbarElementData, bool>[] Neighbours;
        private Func<IToolbarElementData, bool> IsNeighbour;
        private readonly string Describe;
        private readonly ILogger Logger;

        public AfterToolbarEntry(BuildingDefinitionGroupId groupId, ILogger logger)
            : this(
                entry => entry is BuildingBasedPlacementToolbarElementData building && building.BuildingDefinition.Id.Equals(groupId),
                groupId.ToString(),
                logger) { }

        public AfterToolbarEntry(IslandDefinitionGroupId groupId, ILogger logger)
            : this(new[] { groupId }, logger) { }

        public AfterToolbarEntry(IslandDefinitionGroupId[] groupIds, ILogger logger)
            : this(
                groupIds.Select(groupId => (Func<IToolbarElementData, bool>)(entry =>
                    entry is IslandBasedPlacementToolbarElementData island && island.IslandGroup.Id.Equals(groupId))).ToArray(),
                string.Join(" or ", groupIds.Select(groupId => groupId.ToString())),
                logger) { }

        private AfterToolbarEntry(Func<IToolbarElementData, bool> isNeighbour, string describe, ILogger logger)
            : this(new[] { isNeighbour }, describe, logger) { }

        private AfterToolbarEntry(Func<IToolbarElementData, bool>[] neighbours, string describe, ILogger logger)
        {
            Neighbours = neighbours;
            Describe = describe;
            Logger = logger;
        }

        // The toolbar as last built, for development aids (see ICConsoleCommands' dumpicons).
        public static ToolbarData LastToolbar { get; private set; }

        public void AddEntry(ToolbarData toolbarData, IToolbarElementData elementData)
        {
            LastToolbar = toolbarData;
            foreach (Func<IToolbarElementData, bool> neighbour in Neighbours)
            {
                IsNeighbour = neighbour;
                if (TryInsertAfter(toolbarData.RootToolbarElement, elementData))
                {
                    return;
                }
            }

            IParentToolbarElementData root = toolbarData.RootToolbarElement;
            Logger.Warning?.Log($"Toolbar entry for {Describe} not found; adding to the end of the toolbar");
            root.InsertAtIndex(elementData, root.Children.Count());
        }

        private bool TryInsertAfter(IParentToolbarElementData parent, IToolbarElementData elementData)
        {
            // Indices count every child, separators included, as InsertAtIndex does.
            IToolbarElementData[] children = parent.Children.ToArray();
            for (int i = 0; i < children.Length; i++)
            {
                if (IsNeighbour(children[i]))
                {
                    parent.InsertAtIndex(elementData, i + 1);
                    return true;
                }

                if (children[i] is IParentToolbarElementData child && TryInsertAfter(child, elementData))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
