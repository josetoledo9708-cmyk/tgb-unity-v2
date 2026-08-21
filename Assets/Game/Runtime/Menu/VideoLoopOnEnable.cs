using UnityEngine;
using UnityEngine.Video;

namespace Game.Runtime.Menu
{
    /// <summary>Arranca (y mantiene en bucle) el VideoPlayer cada vez que el objeto se activa, no solo
    /// al construirse. Necesario porque las pantallas se crean inactivas y se activan al mostrarse.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class VideoLoopOnEnable : MonoBehaviour
    {
        private VideoPlayer _vp;

        private void Awake() => _vp = GetComponent<VideoPlayer>();

        private void OnEnable()
        {
            if (_vp == null) _vp = GetComponent<VideoPlayer>();
            if (_vp == null) return;
            _vp.isLooping = true;
            _vp.Play();
        }
    }
}
