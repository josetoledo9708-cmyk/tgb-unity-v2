using UnityEngine;
using Game.Core.Model;

namespace Game.Runtime.View
{
    /// <summary>
    /// Posiciones en el mundo (plano XZ) de cada ranura del campo, en espejo entre jugadores.
    /// El jugador 0 (tú) queda cerca de la cámara (Z negativo); el 1 (rival) al fondo (Z positivo).
    /// </summary>
    public static class BoardLayout
    {
        public const float SlotX = 2.0f;   // separación horizontal entre ranuras
        public const float HandZ = 7.0f;
        public const float BackRowZ = 5.0f; // tierras + mazo
        public const float FrontRowZ = 2.4f; // zonas tipadas
        public const float MazoX = 8.0f;

        private static float SideZ(int player) => player == 0 ? -1f : 1f;

        /// <summary>Posición de la ranura i (0..6) de una fila, centrada en X.</summary>
        private static Vector3 Slot(int player, float zRow, int i, int count)
        {
            float startX = -SlotX * (count - 1) / 2f;
            float x = startX + i * SlotX;
            // Espejo: el rival invierte el eje X para que su "derecha" sea la izquierda nuestra.
            if (player == 1) x = -x;
            return new Vector3(x, 0f, zRow * SideZ(player));
        }

        public static Vector3 Tierra(int player, int index) => Slot(player, BackRowZ, index, 7);

        public static Vector3 Mazo(int player)
        {
            float x = player == 0 ? MazoX : -MazoX;
            return new Vector3(x, 0f, BackRowZ * SideZ(player));
        }

        public static Vector3 Hand(int player, int index, int count)
            => Slot(player, HandZ, index, Mathf.Max(count, 1));

        /// <summary>Posición + rotación de una carta de la mano en abanico (cartas solapadas en arco).</summary>
        public static (Vector3 pos, Quaternion rot) HandFan(int player, int index, int count)
        {
            int side = player == 0 ? -1 : 1; // -1 = cerca de cámara
            float half = (count - 1) / 2f;
            float t = index - half;                       // -half .. +half
            float anglePer = Mathf.Min(7f, 42f / Mathf.Max(count, 1));
            const float spacing = 1.15f;                  // < ancho de carta (1.4) -> solapan
            const float bow = 0.14f;

            float x = t * spacing;
            if (player == 1) x = -x;                      // espejo
            float z = HandZ * side + side * (bow * t * t); // los extremos curvan hacia el jugador
            float yaw = t * anglePer;
            if (player == 1) yaw = -yaw;

            return (new Vector3(x, 0.02f, z), Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>
        /// Fila frontal tipada. Orden para el jugador (derecha->izquierda):
        /// 0 descarte, 1 CONCEPTO, 2-4 SER, 5 DIA, 6 HISTORIA.
        /// </summary>
        public static Vector3 FrontSlot(int player, int logicalIndex)
        {
            // logicalIndex 0..6 en orden lógico; en X, "derecha" del jugador = índice 0.
            int i = 6 - logicalIndex; // a posición de izquierda(0)->derecha(6) antes del espejo
            return Slot(player, FrontRowZ, i, 7);
        }

        public static Vector3 Descarte(int player) => FrontSlot(player, 0);
        public static Vector3 Concepto(int player) => FrontSlot(player, 1);
        public static Vector3 Ser(int player, int serIndex) => FrontSlot(player, 2 + Mathf.Clamp(serIndex, 0, 2));
        public static Vector3 Dia(int player) => FrontSlot(player, 5);
        public static Vector3 Historia(int player) => FrontSlot(player, 6);

        public static Color ColorFor(CardType t) => t switch
        {
            CardType.Tierra => new Color(0.30f, 0.55f, 0.20f),
            CardType.SerDivino => new Color(0.85f, 0.78f, 0.35f),
            CardType.SerHumano => new Color(0.75f, 0.45f, 0.30f),
            CardType.SerAnimal => new Color(0.55f, 0.40f, 0.25f),
            CardType.Concepto => new Color(0.45f, 0.40f, 0.75f),
            CardType.Dia => new Color(0.80f, 0.65f, 0.25f),
            CardType.Historia => new Color(0.25f, 0.60f, 0.55f),
            _ => new Color(0.5f, 0.5f, 0.5f)
        };
    }
}
