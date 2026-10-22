using System;
using System.Linq;
using Core.Localization;
using Game.Core.Content.Buildings;
using ShapezShifter.Flow.Toolbar;
using UnityEngine;

namespace Circuits.Kit
{
    // A toolbar group of the mod's own (a sub-nav, like the vanilla Logic Gates group), placed in the same
    // category as a vanilla group, right after it. Entries added through this location go into the group;
    // the first entry creates it. The group is found again by its title's translation key, so every entry of
    // the group shares one instance.
    public class ToolbarGroup : IToolbarEntryInsertLocation
    {
        private readonly BuildingDefinitionGroupId Neighbour;
        private readonly string TitleKey;
        private readonly string DescriptionKey;
        private readonly Sprite Icon;

        // neighbour: a vanilla building whose toolbar group the new group follows.
        public ToolbarGroup(BuildingDefinitionGroupId neighbour, string titleKey, string descriptionKey, Sprite icon)
        {
            Neighbour = neighbour;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            Icon = icon;
        }

        public void AddEntry(ToolbarData toolbarData, IToolbarElementData elementData)
        {
            GroupToolbarElementData group = FindOrCreate(toolbarData);
            group.InsertAtIndex(elementData, group.Children.Length);
        }

        private GroupToolbarElementData FindOrCreate(ToolbarData toolbarData)
        {
            if (!TryFindNeighbourGroup(toolbarData.RootToolbarElement, out IParentToolbarElementData category, out int index))
            {
                throw new InvalidOperationException($"No toolbar group holds {Neighbour}");
            }

            GroupToolbarElementData existing = category.Children.OfType<GroupToolbarElementData>()
               .FirstOrDefault(group => group.Title?.Id.Id == TitleKey);
            if (existing != null)
            {
                return existing;
            }

            var neighbour = (ParentToolbarElementData)category.Children.ElementAt(index);
            var created = new GroupToolbarElementData
            {
                Title = new LazyLocalizedText(new TranslationId(TitleKey)),
                Description = new LazyLocalizedText(new TranslationId(DescriptionKey)),
                Icon = Icon,
                SectionIndex = neighbour.SectionIndex,
                RememberPreferredChild = true,
                Children = Array.Empty<IToolbarElementData>()
            };
            category.InsertAtIndex(created, index + 1);
            return created;
        }

        // Finds the group holding the neighbour building, and its parent (a category) and position there.
        private bool TryFindNeighbourGroup(IParentToolbarElementData parent, out IParentToolbarElementData category, out int index)
        {
            IToolbarElementData[] children = parent.Children.ToArray();
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] is IParentToolbarElementData group)
                {
                    if (group.Children.OfType<BuildingBasedPlacementToolbarElementData>()
                       .Any(entry => entry.BuildingDefinition.Id.Equals(Neighbour)))
                    {
                        category = parent;
                        index = i;
                        return true;
                    }

                    if (TryFindNeighbourGroup(group, out category, out index))
                    {
                        return true;
                    }
                }
            }

            category = null;
            index = -1;
            return false;
        }
    }
}
