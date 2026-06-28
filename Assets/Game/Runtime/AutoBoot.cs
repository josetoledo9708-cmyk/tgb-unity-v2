using UnityEngine;
using Game.Runtime.View;

namespace Game.Runtime
{
    /// <summary>
    /// Asegura que al entrar en Play SIEMPRE exista un HotseatView, sin importar la escena
    /// abierta. Evita el "no aparece nada" cuando la escena no tiene el GameController.
    /// </summary>
    public static class AutoBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGame()
        {
            if (Object.FindAnyObjectByType<HotseatView>() != null) return;
            var go = new GameObject("GameController (auto)");
            go.AddComponent<HotseatView>();
        }
    }
}
