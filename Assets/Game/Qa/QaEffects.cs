using System.Collections.Generic;
using System.Linq;
using Game.Core.Effects;
using Game.Core.Model;

namespace Game.Qa
{
    /// <summary>Un efecto concreto a comprobar. La UNIDAD es el efecto, no la carta: una carta con
    /// "Bendecido" y "Bendición" son DOS cosas que probar, y aprobar una no dice nada de la otra.</summary>
    public readonly struct QaEffectId
    {
        public readonly string CardId;
        public readonly EffectTrigger Trigger;
        public readonly string CardName;
        public readonly string Text;

        public QaEffectId(string cardId, EffectTrigger trigger, string cardName, string text)
        {
            CardId = cardId; Trigger = trigger; CardName = cardName; Text = text;
        }

        /// <summary>Clave estable del efecto. En este juego el registro ya está indexado por
        /// (carta, trigger), así que ese par identifica el efecto sin ambigüedad.</summary>
        public string Key => CardId + "|" + Trigger;

        /// <summary>Etiqueta que ve el jugador, con los términos del juego.</summary>
        public string Label => Etiqueta(Trigger);

        public static string Etiqueta(EffectTrigger t) => t switch
        {
            EffectTrigger.AlEntrar => "Bendecido (al entrar)",
            EffectTrigger.EfectoActivado => "Bendición (efecto activado)",
            EffectTrigger.AlTapearse => "Al tapearse",
            EffectTrigger.AlSalir => "Al salir",
            EffectTrigger.AlSerDestruida => "Al ser destruida",
            EffectTrigger.UsoUnico => "Uso único",
            EffectTrigger.Respuesta => "Respuesta (trampa)",
            EffectTrigger.AlActivarElDia => "Al activar el DÍA",
            _ => t.ToString(),
        };
    }

    /// <summary>
    /// Resuelve QUÉ hay que comprobar en cada carta. Vive dentro de la carpeta de QA (no en el
    /// juego) para que todo el sistema se pueda borrar de una pieza.
    /// </summary>
    public static class QaEffects
    {
        /// <summary>Texto impreso que corresponde a ese trigger, para enseñarlo en el cuadro.</summary>
        public static string TextFor(CardDefinition d, EffectTrigger t)
        {
            string txt = t switch
            {
                EffectTrigger.AlEntrar => d.AlEntrar,
                EffectTrigger.EfectoActivado => d.Activado,
                EffectTrigger.AlSalir => d.AlSalir,
                _ => null,
            };
            return !string.IsNullOrWhiteSpace(txt) ? txt : (d.Efecto ?? "(sin texto en el catálogo)");
        }

        /// <summary>Todos los efectos comprobables de una carta: los que el motor tiene realmente
        /// registrados. Si una carta no promete nada en ese momento, no hay nada que preguntar.</summary>
        public static IEnumerable<QaEffectId> Of(CardDefinition d, IEffectResolver effects)
        {
            foreach (EffectTrigger t in System.Enum.GetValues(typeof(EffectTrigger)))
                if (effects.HasEffect(d.Id, t))
                    yield return new QaEffectId(d.Id, t, d.Nombre, TextFor(d, t));
        }

        /// <summary>Progreso "n/total efectos" de un conjunto de cartas.</summary>
        public static (int hechos, int total, int pendientes) Progress(
            IEnumerable<CardDefinition> cards, IEffectResolver effects)
        {
            int hechos = 0, total = 0, pend = 0;
            foreach (var d in cards.Distinct())
                foreach (var ef in Of(d, effects))
                {
                    total++;
                    var e = QaStore.Find(ef.Key);
                    if (e == null) continue;
                    if (e.IsFinal()) hechos++;
                    else pend++;
                }
            return (hechos, total, pend);
        }
    }
}
