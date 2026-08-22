using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>Hace palpitar el alfa de un Graphic entre min y max (para resaltar/llamar la atención).</summary>
    [DisallowMultipleComponent]
    public sealed class AlphaPulse : MonoBehaviour
    {
        public Graphic graphic;
        public float min = 0.2f;
        public float max = 0.75f;
        public float speed = 3f;

        private void Awake() { if (graphic == null) graphic = GetComponent<Graphic>(); }

        private void Update()
        {
            if (graphic == null) return;
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
            var c = graphic.color; c.a = Mathf.Lerp(min, max, t); graphic.color = c;
        }
    }
}
