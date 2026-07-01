using UnityEngine;
using Game.Runtime.View;

namespace Game.Runtime.Menu
{
    /// <summary>
    /// Arranca el menú sobre la escena existente SIN tocar el campo: corre AfterSceneLoad (antes del
    /// Start de HotseatView), desactiva el campo para que no construya la partida todavía, y crea el
    /// MenuShell. Al pulsar "Jugar", MenuShell reactiva HotseatView (recién ahí corre su Start).
    /// </summary>
    public static class MenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Object.FindAnyObjectByType<MenuShell>() != null) return; // ya existe

            var board = Object.FindAnyObjectByType<HotseatView>(FindObjectsInactive.Include);
            if (board != null) board.enabled = false; // no arrancar el campo hasta "Jugar"

            var go = new GameObject("MenuShell");
            var shell = go.AddComponent<MenuShell>();
            shell.SetBoard(board);
        }
    }
}
