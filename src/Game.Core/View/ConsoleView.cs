using System.Linq;
using System.Text;
using Game.Core.Model;

namespace Game.Core.View
{
    /// <summary>
    /// Render de texto del estado, agnóstico de Unity. Sirve para hotseat por consola y
    /// para diagnóstico. La presentación 3D real irá en la capa Runtime de Unity.
    /// </summary>
    public static class ConsoleView
    {
        public static string Render(GameState s)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"== Turno {s.TurnNumber} | Fase {s.Phase} | Activo P{s.ActivePlayer} ==");
            if (s.IsOver)
                sb.AppendLine($"** FIN: gana P{s.Winner} por Victoria {s.WinReason} **");

            foreach (var p in s.Players)
            {
                sb.AppendLine($"P{p.Id}  FD={p.Fd}  DiaActual={p.DiaActual}  Historia={p.HistoriaId}");
                sb.AppendLine($"   Mano:{p.Mano.Count} Mazo:{p.Mazo.Count} Retirados:{p.Retirados.Count} PilaDia:{p.PilaDia.Count}");
                sb.AppendLine($"   Tierras[{p.Tierras.Count}/7]: {Names(p.Tierras)}");
                sb.AppendLine($"   Seres[{p.Seres.Count}/3]: {SeresNames(p.Seres)}");
                sb.AppendLine($"   Concepto: {(p.Concepto.Count > 0 ? "[boca abajo]" : "-")}");
            }
            return sb.ToString();
        }

        private static string Names(Zone z)
            => z.Count == 0 ? "-" : string.Join(", ",
                z.Cards.Select(c => c.Nombre + (c.Tapped ? "(tap)" : "")));

        private static string SeresNames(Zone z)
            => z.Count == 0 ? "-" : string.Join(", ",
                z.Cards.Select(c => $"{c.Nombre}(d{c.DurLeft})"));
    }
}
