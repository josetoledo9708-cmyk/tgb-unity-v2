using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>Reproduce una secuencia de sprites sobre un Image (flipbook). Puede reproducir un
    /// rango [from,to], en bucle o una vez, y avisa al terminar.</summary>
    [DisallowMultipleComponent]
    public sealed class Flipbook : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 12f;

        private Image _img;
        private float _t;
        private int _i;
        private int _from, _to;
        private bool _loop;
        private bool _playing;
        private System.Action _onComplete;

        private void Awake() => _img = GetComponent<Image>();

        public void ShowFrame(int i)
        {
            if (_img == null) _img = GetComponent<Image>();
            if (frames != null && i >= 0 && i < frames.Length && _img != null) _img.sprite = frames[i];
        }

        /// <summary>Reproduce del frame <paramref name="from"/> al <paramref name="to"/> (inclusive).</summary>
        public void Play(int from, int to, bool loop = false, System.Action onComplete = null)
        {
            if (frames == null || frames.Length == 0) { onComplete?.Invoke(); return; }
            _from = Mathf.Clamp(from, 0, frames.Length - 1);
            _to = Mathf.Clamp(to, 0, frames.Length - 1);
            _loop = loop; _onComplete = onComplete;
            _i = _from; _t = 0f; _playing = true;
            ShowFrame(_i);
        }

        public void Stop() => _playing = false;

        public bool IsPlaying => _playing;

        private void Update()
        {
            if (!_playing || _img == null || frames == null || frames.Length == 0) return;
            _t += Time.unscaledDeltaTime * fps;
            while (_t >= 1f)
            {
                _t -= 1f;
                if (_i >= _to)
                {
                    if (_loop) { _i = _from; }
                    else { _playing = false; ShowFrame(_to); _onComplete?.Invoke(); return; }
                }
                else _i++;
                ShowFrame(_i);
            }
        }
    }
}
