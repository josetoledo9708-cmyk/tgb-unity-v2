using System.Linq;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.AI
{
    /// <summary>
    /// IA básica: juega el turno del jugador ACTIVO con heurísticas simples. No termina el
    /// turno (el llamador llama EndTurn, para poder pausar/animar entre acciones).
    /// </summary>
    public static class SimpleAI
    {
        public static void PlayTurn(GameEngine eng)
        {
            var s = eng.State;
            if (s.IsOver) return;
            var p = s.Active;

            // 1) Tapear todas las TIERRAs para generar FD.
            foreach (var t in p.Tierras.Cards.Where(x => !x.Tapped).ToList())
                eng.TapTierra(t);

            // 2) Jugar 1 TIERRA de la mano (gratis, 1/turno).
            var tierra = p.Mano.Cards.FirstOrDefault(c => c.Type == CardType.Tierra);
            if (tierra != null) eng.PlayTierra(tierra);

            // 3) Jugar SER más baratos asequibles hasta llenar las 3 ranuras.
            while (!p.Seres.IsFull)
            {
                var ser = p.Mano.Cards
                    .Where(c => CardTypeNames.IsSer(c.Type) && (c.Def.Coste ?? 0) <= p.Fd)
                    .OrderBy(c => c.Def.Coste ?? 0)
                    .FirstOrDefault();
                if (ser == null || !eng.PlaySer(ser).Ok) break;
            }

            // 4) Activar el DÍA actual si cumple condición y alcanza el FD.
            int dia = p.DiaActual;
            if (dia <= 7 && DiaConditions.Met(p, dia))
            {
                var diaCard = p.PilaDia.Cards.FirstOrDefault(d => PlayerState.DiaNumero(d) == dia);
                if (diaCard != null && p.Fd >= (diaCard.Def.Coste ?? 0))
                    eng.ActivateDia(useFree: false);
            }

            // 5) Jugar un CONCEPTO barato beneficioso (robo/búsqueda) si alcanza el FD.
            var con = p.Mano.Cards
                .Where(c => c.Type == CardType.Concepto && (c.Def.Coste ?? 0) <= p.Fd)
                .OrderBy(c => c.Def.Coste ?? 0)
                .FirstOrDefault();
            if (con != null) eng.PlayConcepto(con, faceDown: false);
        }
    }
}
