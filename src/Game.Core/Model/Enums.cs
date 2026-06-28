namespace Game.Core.Model
{
    public enum CardType
    {
        Dia,
        Tierra,
        SerDivino,
        SerHumano,
        SerAnimal,
        Concepto,
        Historia
    }

    public enum EffectTrigger
    {
        AlEntrar,
        AlEntrarCondicional,
        AlSalir,
        AlTapearse,
        AlSerDestruida,
        EfectoActivado,
        EfectoPasivoContinuo,
        Penalizacion,
        UsoUnico,
        Respuesta,
        EfectoDiferido,
        EfectoDeArea,
        EfectoGlobal,
        AlActivarElDia,
        AlFinalDeLaEntrega
    }

    public enum Phase
    {
        Preludio,
        Genesis,
        Preparacion,
        Entrega
    }

    public enum VictoryMode
    {
        FieldAtEntrega,
        OnPiecePlay
    }

    public enum VictoryId
    {
        I,
        II,
        III
    }

    public static class CardTypeNames
    {
        /// <summary>Mapea la clave del JSON (p.ej. "SER_DIVINO") al enum CardType.</summary>
        public static CardType FromJsonKey(string key)
        {
            switch (key)
            {
                case "DIA": return CardType.Dia;
                case "TIERRA": return CardType.Tierra;
                case "SER_DIVINO": return CardType.SerDivino;
                case "SER_HUMANO": return CardType.SerHumano;
                case "SER_ANIMAL": return CardType.SerAnimal;
                case "CONCEPTO": return CardType.Concepto;
                case "HISTORIA": return CardType.Historia;
                default: throw new System.ArgumentException($"Clave de tipo desconocida: {key}");
            }
        }

        public static bool IsSer(CardType t)
            => t == CardType.SerDivino || t == CardType.SerHumano || t == CardType.SerAnimal;
    }
}
