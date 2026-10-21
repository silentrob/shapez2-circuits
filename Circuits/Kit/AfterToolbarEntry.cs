using System.Linq;
using Game.Core.Content.Buildings;
using ShapezShifter.Flow.Toolbar;
using ILogger = Core.Logging.ILogger;

namespace Circuits.Kit
{
    // Inserts a toolbar entry right after the vanilla entry for a building group, wherever it sits in the
    // toolbar tree. Unlike ShapezShifter's index-based locators this survives the toolbar being reordered.
    public class AfterToolbarEntry : IToolbarEntryInsertLocation
    {
        private readonly BuildingDefinitionGroupId GroupId;
        private readonly ILogger Logger;

        public AfterToolbarEntry(BuildingDefinitionGroupId groupId, ILogger logger)
        {
            GroupId = groupId;
            Logger = logger;
        }

        public void AddEntry(ToolbarData toolbarData, IToolbarElementData elementData)
        {
            if (TryInsertAfter(toolbarData.RootToolbarElement, elementData))
            {
                return;
            }

            IParentToolbarElementData root = toolbarData.RootToolbarElement;
            Logger.Warning?.Log($"Toolbar entry for {GroupId} not found; adding to the end of the toolbar");
            root.InsertAtIndex(elementData, root.Children.Count());
        }

        private bool TryInsertAfter(IParentToolbarElementData parent, IToolbarElementData elementData)
        {
            // Indices count every child, separators included, as InsertAtIndex does.
            IToolbarElementData[] children = parent.Children.ToArray();
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] is BuildingBasedPlacementToolbarElementData entry
                    && entry.BuildingDefinition.Id.Equals(GroupId))
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
