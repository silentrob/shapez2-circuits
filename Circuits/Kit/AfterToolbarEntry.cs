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
    public class AfterToolbarEntry : IToolbarEntryInsertLocation
    {
        private readonly Func<IToolbarElementData, bool> IsNeighbour;
        private readonly string Describe;
        private readonly ILogger Logger;

        public AfterToolbarEntry(BuildingDefinitionGroupId groupId, ILogger logger)
            : this(
                entry => entry is BuildingBasedPlacementToolbarElementData building && building.BuildingDefinition.Id.Equals(groupId),
                groupId.ToString(),
                logger) { }

        public AfterToolbarEntry(IslandDefinitionGroupId groupId, ILogger logger)
            : this(
                entry => entry is IslandBasedPlacementToolbarElementData island && island.IslandGroup.Id.Equals(groupId),
                groupId.ToString(),
                logger) { }

        private AfterToolbarEntry(Func<IToolbarElementData, bool> isNeighbour, string describe, ILogger logger)
        {
            IsNeighbour = isNeighbour;
            Describe = describe;
            Logger = logger;
        }

        public void AddEntry(ToolbarData toolbarData, IToolbarElementData elementData)
        {
            if (TryInsertAfter(toolbarData.RootToolbarElement, elementData))
            {
                return;
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
