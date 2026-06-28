using Game.Core.Data;
using Game.Core.Model;

namespace Game.Core.Tests
{
    /// <summary>Atajo a SampleDeckBuilder (Core) para los tests.</summary>
    public static class TestDecks
    {
        public static DeckDefinition BuildSample(CardCatalog cat, string historiaId, int size = 40)
            => SampleDeckBuilder.Build(cat, historiaId, size);
    }
}
