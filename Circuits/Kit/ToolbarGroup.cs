using System;
using System.Linq;
using Core.Localization;
using Game.Core.Content.Buildings;
using Game.Core.Content.Islands;
using ShapezShifter.Flow.Toolbar;
using UnityEngine;

namespace Circuits.Kit
{
    // A toolbar group of the mod's own (a sub-nav, like the vanilla Logic Gates group), placed in the same
    // category as a vanilla group, right after it. Entries added through this location go into the group;
    // the first entry creates it. The group is found again by its title's translation key, so every entry of
    // the group shares one instance. The game adds mod entries in no set order, so a group given its entries'
    // order keeps them in it, wherever each arrives. A mod entry carries no more than its title, icon and placer
    // id, so entries are told apart by their icons (ByIcon); entries without a ranked icon follow, as they come.
    public class ToolbarGroup : IToolbarEntryInsertLocation
    {
        private readonly Func<IToolbarElementData, bool> IsNeighbour;
        private readonly string Describe;
        private readonly Func<IToolbarElementData, int> Rank;
        private readonly string TitleKey;
        private readonly string DescriptionKey;
        private readonly Sprite Icon;

        // neighbour: a vanilla building whose toolbar group the new group follows; order: the icons of the entries
        // to show first, in order.
        public ToolbarGroup(
            BuildingDefinitionGroupId neighbour,
            string titleKey,
            string descriptionKey,
            Sprite icon,
            params Sprite[] order)
            : this(
                entry => entry is BuildingBasedPlacementToolbarElementData building && building.BuildingDefinition.Id.Equals(neighbour),
                neighbour.ToString(),
                ByIcon(order),
                titleKey,
                descriptionKey,
                icon) { }

        // neighbour: a vanilla platform whose toolbar group the new group follows; order as above.
        public ToolbarGroup(
            IslandDefinitionGroupId neighbour,
            string titleKey,
            string descriptionKey,
            Sprite icon,
            params Sprite[] order)
            : this(
                entry => entry is IslandBasedPlacementToolbarElementData island && island.IslandGroup.Id.Equals(neighbour),
                neighbour.ToString(),
                ByIcon(order),
                titleKey,
                descriptionKey,
                icon) { }

        private static Func<IToolbarElementData, int> ByIcon(Sprite[] order)
        {
            return entry =>
            {
                int rank = entry is IPresentableToolbarElementData presentable ? Array.IndexOf(order, presentable.Icon) : -1;
                return rank < 0 ? order.Length : rank;
            };
        }

        private ToolbarGroup(
            Func<IToolbarElementData, bool> isNeighbour,
            string describe,
            Func<IToolbarElementData, int> rank,
            string titleKey,
            string descriptionKey,
            Sprite icon)
        {
            IsNeighbour = isNeighbour;
            Describe = describe;
            Rank = rank;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            Icon = icon;
        }

        public void AddEntry(ToolbarData toolbarData, IToolbarElementData elementData)
        {
            GroupToolbarElementData group = FindOrCreate(toolbarData);
            int rank = Rank(elementData);
            group.InsertAtIndex(elementData, group.Children.Count(child => Rank(child) <= rank));
        }

        private GroupToolbarElementData FindOrCreate(ToolbarData toolbarData)
        {
            if (!TryFindNeighbourGroup(toolbarData.RootToolbarElement, out IParentToolbarElementData category, out int index))
            {
                throw new InvalidOperationException($"No toolbar group holds {Describe}");
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

        // Finds the group holding the neighbour, and its parent (a category) and position there.
        private bool TryFindNeighbourGroup(IParentToolbarElementData parent, out IParentToolbarElementData category, out int index)
        {
            IToolbarElementData[] children = parent.Children.ToArray();
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] is IParentToolbarElementData group)
                {
                    if (group.Children.Any(IsNeighbour))
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
