using System;
using Game.Core.Model;

namespace Game.Core.Effects
{
    /// <summary>Una resolución de efecto que acaba de ocurrir en la partida.</summary>
    public readonly struct EffectPlay
    {
        /// <summary>La carta cuyo efecto se disparó.</summary>
        public readonly CardInstance Card;
        /// <summary>Momento por el que se disparó (AlEntrar, EfectoActivado, UsoUnico…).</summary>
        public readonly EffectTrigger Trigger;
        /// <summary>true si la carta tenía un handler registrado para ese trigger. Si es false no hay
        /// nada que comprobar: la carta no promete ningún efecto en ese momento.</summary>
        public readonly bool HadHandler;
        /// <summary>Jugador dueño de la carta.</summary>
        public readonly int OwnerId;

        public EffectPlay(CardInstance card, EffectTrigger trigger, bool hadHandler, int ownerId)
        {
            Card = card; Trigger = trigger; HadHandler = hadHandler; OwnerId = ownerId;
        }
    }

    /// <summary>
    /// COSTURA de pruebas: el motor avisa aquí cada vez que resuelve un efecto y NO sabe quién
    /// escucha. Sin suscriptores no hace absolutamente nada, así que se puede borrar el sistema de
    /// QA entero sin tocar el juego.
    ///
    /// Ojo: los comandos del jugador corren en un hilo aparte, así que el evento puede llegar FUERA
    /// del hilo principal de Unity. Quien escuche debe encolar y atender desde el hilo principal.
    /// </summary>
    public static class EffectLog
    {
        public static event Action<EffectPlay>? Fired;

        public static void Raise(CardInstance card, EffectTrigger trigger, bool hadHandler, int ownerId)
        {
            var h = Fired;
            if (h == null) return; // sin sistema de pruebas: coste cero
            try { h(new EffectPlay(card, trigger, hadHandler, ownerId)); }
            catch { /* una prueba rota jamás debe tumbar la partida */ }
        }
    }
}
