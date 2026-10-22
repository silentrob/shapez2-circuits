using System;
using Core.Factory;
using Game.Core.Serialization;
using ShapezShifter.Flow;

namespace Circuits.IC.Board
{
    // A board's own settings, saved with the platform: its name (which its firmware is built under) and an id
    // that stays the same across loads, unlike the game's island ids. The name is changed in place from the
    // board's panel, as a Label's text is.
    public class BoardConfiguration : IIslandConfiguration
    {
        private const int Version = 1;

        public string Name = "";
        public string Id = Guid.NewGuid().ToString("N");

        public void Sync(ISerializationVisitor visitor)
        {
            int version = Version;
            visitor.SyncInt_4(ref version);
            visitor.SyncString_4(ref Name);
            visitor.SyncString_4(ref Id);
        }

        public bool Equals(IEntityConfiguration other)
        {
            return other is BoardConfiguration board && board.Id == Id && board.Name == Name;
        }
    }

    // Gives the board definition its configuration factory, replacing the null one ShapezShifter's island
    // builder attaches, and its look (BoardModel). ShapezShifter registers the definition again for every map the game builds, so this
    // replaces rather than adds.
    public class BoardIslandBuilder : IIslandBuilder
    {
        private readonly IIslandBuilder Inner;

        public BoardIslandBuilder(IIslandBuilder inner)
        {
            Inner = inner;
        }

        public IslandDefinition BuildAndRegister(IslandDefinitionGroup group, GameIslands gameIslands)
        {
            IslandDefinition definition = Inner.BuildAndRegister(group, gameIslands);
            definition.CustomData.AttachOrReplace<IFactory<IIslandConfiguration>>(new Factory());
            definition.CustomData.AttachOrReplace(BoardModel.DrawData());
            return definition;
        }

        private class Factory : IFactory<IIslandConfiguration>
        {
            public IIslandConfiguration Produce()
            {
                return new BoardConfiguration();
            }
        }
    }
}
