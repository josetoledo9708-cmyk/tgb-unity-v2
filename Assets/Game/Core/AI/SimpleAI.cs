using System.Collections.Generic;
using System.Linq;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.AI
{
    /// <summary>
    /// IA básica orientada a GANAR por la HISTORIA: prioriza poner en campo las piezas (SER y
    /// TIERRA), no desperdicia ranuras SER con duplicados, mantiene rampa de FD y cava con
    /// CONCEPTOs de robo. No termina el turno (el llamador llama EndTurn).
    /// </summary>
    public static class SimpleAI
    {
        public static void PlayTurn(GameEngine eng)
        {
            var s = eng.State;
            if (s.IsOver) return;
            var p = s.Active;

            // Nombres de las piezas de la historia del jugador (prioridad de juego).
            var pieces = eng.Catalog.FindHistoria(p.HistoriaId)?.Piezas ?? new List<string>();
            bool IsPiece(CardInstance c) => pieces.Contains(c.Nombre);
            bool InField(CardInstance c) =>
                p.Seres.Cards.Any(f => f.Nombre == c.Nombre) || p.Tierras.Cards.Any(f => f.Nombre == c.Nombre);

            // 1) Tapear todas las TIERRAs para generar FD.
            foreach (var t in p.Tierras.Cards.Where(x => !x.Tapped).ToList())
                eng.TapTierra(t);

            // 2) Jugar 1 TIERRA (gratis, 1/turno): preferir una pieza-TIERRA que falte en campo.
            var tierra = p.Mano.Cards
                .Where(c => c.Type == CardType.Tierra)
                .OrderByDescending(c => IsPiece(c) && !InField(c))
                .FirstOrDefault();
            if (tierra != null) eng.PlayTierra(tierra);

            // 3) Llenar ranuras SER: primero piezas que falten, nunca un nombre ya en campo.
            while (!p.Seres.IsFull)
            {
                var ser = p.Mano.Cards
                    .Where(c => CardTypeNames.IsSer(c.Type)
                                && (c.Def.Coste ?? 0) <= p.Fd
                                && !p.Seres.Cards.Any(f => f.Nombre == c.Nombre))
                    .OrderByDescending(c => IsPiece(c))
                    .ThenBy(c => c.Def.Coste ?? 0)
                    .FirstOrDefault();
                if (ser == null || !eng.PlaySer(ser).Ok) break;
            }

            // 3b) Usar los efectos ACTIVADOS de los SER en campo (si alcanza el FD).
            foreach (var ser in p.Seres.Cards.ToList())
            {
                if (p.SeresActivatedThisTurn.Contains(ser.InstanceId)) continue;
                if ((ser.Def.ActCost ?? 0) <= p.Fd) eng.ActivateSerEffect(ser);
            }

            // 4) Activar el DÍA actual si cumple condición y alcanza el FD.
            int dia = p.DiaActual;
            if (dia <= 7 && DiaConditions.Met(p, dia))
            {
                var diaCard = p.PilaDia.Cards.FirstOrDefault(d => PlayerState.DiaNumero(d) == dia);
                if (diaCard != null && p.Fd >= (diaCard.Def.Coste ?? 0))
                    eng.ActivateDia(useFree: false);
            }

            // 5) Jugar CONCEPTOs baratos (robo/búsqueda) para cavar hacia las piezas.
            for (int i = 0; i < 4; i++)
            {
                var con = p.Mano.Cards
                    .Where(c => c.Type == CardType.Concepto && (c.Def.Coste ?? 0) <= p.Fd)
                    .OrderBy(c => c.Def.Coste ?? 0)
                    .FirstOrDefault();
                if (con == null) break;
                // Carta de respuesta: colocar boca abajo (trampa) si la zona CONC está libre.
                bool asTrap = eng.Effects.IsResponse(con.Def.Id) && p.Concepto.Count == 0;
                if (!eng.PlayConcepto(con, faceDown: asTrap).Ok) break;
            }
        }
    }
}
