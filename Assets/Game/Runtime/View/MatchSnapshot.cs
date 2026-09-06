using System;
using Game.Core.Model;

namespace Game.Runtime.View
{
    /// <summary>
    /// COSTURAS de pruebas del lado de la vista. El campo las rellena y quien quiera las consume;
    /// nadie del juego depende de que estén rellenas, así que borrar el sistema de QA no rompe nada.
    /// </summary>
    public static class MatchSnapshot
    {
        /// <summary>Foto del campo en texto plano (turno, fase, ambos lados, mano y montones).
        /// La rellena el campo al iniciar la partida; devuelve null si no hay partida.</summary>
        public static Func<string>? Describe;

        /// <summary>Línea(s) extra a pintar en la ficha de una carta (marcas de comprobación por
        /// efecto). Devuelve null o vacío si no hay nada que añadir.</summary>
        public static Func<CardDefinition, string>? ExtraCardLine;
    }
}
