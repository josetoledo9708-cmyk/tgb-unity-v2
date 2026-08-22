using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>Partículas doradas simples para uGUI: puntos suaves que suben desde abajo (detrás del
    /// tomo) y se desvanecen, en bucle. Sin ParticleSystem, todo por código.</summary>
    [DisallowMultipleComponent]
    public sealed class GoldParticles : MonoBehaviour
    {
        public Sprite dotSprite;
        public int count = 16;      // pool total (máximo, p. ej. el doble)
        public int baseCount = 16;  // activas normalmente; al hacer boost se activan todas
        public float life = 2.4f;
        public float riseMin = 30f, riseMax = 75f;
        public float halfWidth = 70f, startY = -95f, driftX = 14f;
        public Color color = new Color(1f, 0.86f, 0.5f, 1f);
        public Vector2 sizeRange = new Vector2(4f, 11f);

        private RectTransform _root;
        private RectTransform[] _rt;
        private Image[] _img;
        private float[] _vy, _vx, _age, _lifeA;
        private int _active;

        private void Awake() => _root = (RectTransform)transform;

        /// <summary>Activa todas las partículas (hover) o vuelve a la cantidad normal.</summary>
        public void SetBoost(bool on)
        {
            if (_rt == null) { _active = on ? count : baseCount; return; }
            int na = Mathf.Clamp(on ? count : baseCount, 0, _rt.Length);
            for (int i = _active; i < na; i++) Respawn(i, Random.Range(0f, life)); // arranca las nuevas
            _active = na;
        }

        private void Start()
        {
            _rt = new RectTransform[count];
            _img = new Image[count];
            _vy = new float[count]; _vx = new float[count];
            _age = new float[count]; _lifeA = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_root, false);
                var im = go.GetComponent<Image>();
                im.sprite = dotSprite; im.raycastTarget = false; im.color = color;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                _rt[i] = rt; _img[i] = im;
                Respawn(i, Random.Range(0f, life)); // desfasadas para que no salgan todas a la vez
            }
            _active = Mathf.Clamp(baseCount, 0, count);
        }

        private void Respawn(int i, float age)
        {
            _age[i] = age;
            _lifeA[i] = life * Random.Range(0.8f, 1.2f);
            _vy[i] = Random.Range(riseMin, riseMax);
            _vx[i] = Random.Range(-driftX, driftX);
            float s = Random.Range(sizeRange.x, sizeRange.y);
            _rt[i].anchoredPosition = new Vector2(Random.Range(-halfWidth, halfWidth), startY);
            _rt[i].sizeDelta = new Vector2(s, s);
        }

        private void Update()
        {
            if (_rt == null) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _rt.Length; i++)
            {
                if (i >= _active) { if (_img[i].color.a != 0f) { var cc = _img[i].color; cc.a = 0f; _img[i].color = cc; } continue; }
                _age[i] += dt;
                if (_age[i] >= _lifeA[i]) { Respawn(i, 0f); continue; }
                var p = _rt[i].anchoredPosition;
                p.y += _vy[i] * dt; p.x += _vx[i] * dt;
                _rt[i].anchoredPosition = p;
                float t = _age[i] / _lifeA[i];
                var c = color; c.a = color.a * Mathf.Sin(t * Mathf.PI); // aparece y se apaga suave
                _img[i].color = c;
            }
        }
    }
}
