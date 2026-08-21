using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Runtime.Menu
{
    /// <summary>Prepara y arranca el VideoPlayer en bucle cada vez que el objeto se activa. Mantiene el
    /// RawImage oculto hasta que hay primer frame, así se ve el fondo estático de respaldo (sin negro
    /// ni retardo perceptible) hasta que el video está listo.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class VideoLoopOnEnable : MonoBehaviour
    {
        private VideoPlayer _vp;
        private RawImage _raw;

        private void Awake()
        {
            _vp = GetComponent<VideoPlayer>();
            _raw = GetComponent<RawImage>();
        }

        private void OnEnable()
        {
            if (_vp == null) _vp = GetComponent<VideoPlayer>();
            if (_vp == null) return;
            _vp.isLooping = true;
            if (_raw != null) _raw.enabled = false; // se ve el fondo estático mientras prepara
            _vp.prepareCompleted -= OnPrepared;
            _vp.prepareCompleted += OnPrepared;
            if (_vp.isPrepared) OnPrepared(_vp);
            else _vp.Prepare();
        }

        private void OnDisable()
        {
            if (_vp != null) _vp.prepareCompleted -= OnPrepared;
        }

        private void OnPrepared(VideoPlayer vp)
        {
            vp.Play();
            if (_raw != null) _raw.enabled = true;
        }
    }
}
