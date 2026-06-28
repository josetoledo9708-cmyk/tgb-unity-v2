using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Runtime.View
{
    /// <summary>
    /// Demo jugable en hotseat: monta el motor, dibuja el campo en 3D (cartas generadas por
    /// código) y deja jugar por clic. Es un punto de partida; la presentación final (prefabs,
    /// animaciones, arrastrar y soltar) se construye encima.
    ///
    /// Uso: añadir este componente a un GameObject vacío en una escena y pulsar Play.
    /// Clic en carta de tu mano = jugarla; clic en tu TIERRA = tapearla; botón "Terminar turno".
    /// </summary>
    public sealed class HotseatView : MonoBehaviour
    {
        [SerializeField] private string historiaP0 = "h1";
        [SerializeField] private string historiaP1 = "h2";
        [SerializeField] private ulong seed = 12345;

        [Header("Cámara (ajustable en el Inspector)")]
        [SerializeField] private Vector3 camPos = new Vector3(0f, 27f, -8f);
        [SerializeField] private Vector3 camLookAt = new Vector3(0f, 0f, 0f);
        [SerializeField] private float camFov = 33f;
        [SerializeField] private bool orthographic = false;
        [SerializeField] private float orthoSize = 9.5f;
        [SerializeField] private float lightIntensity = 1.0f;

        [Header("Arte de cartas (carpeta de imágenes)")]
        [SerializeField] private string artFolder = @"C:\Users\Rinco\Downloads";

        [Header("Fondo del campo")]
        [SerializeField] private string backgroundPath =
            @"C:\Users\Rinco\OneDrive\Escritorio\ASDADADSSA\Imagenes\Menu\FieldBackgound.png";

        private GameEngine _engine = null!;
        private CardArtLibrary _art = null!;
        private Transform _board = null!;
        private readonly TextMesh[] _mazoCount = new TextMesh[2];
        private readonly TextMesh[] _retirCount = new TextMesh[2];
        private readonly List<CardView> _spawned = new();
        private CardView? _hovered;
        private CardView? _drag;
        private string _status = "";

        private readonly HashSet<int> _prevHandIds = new();
        private readonly List<GameObject>[] _glowTierra = { new(), new() };
        private readonly List<GameObject>[] _glowSer = { new(), new() };
        private readonly GameObject?[] _glowConcepto = new GameObject?[2];

        private void Start()
        {
            EnsureSceneRig();

            string path = Path.Combine(Application.streamingAssetsPath, "catalogo.v3.json");
            if (!File.Exists(path)) { Debug.LogError($"Falta catálogo en {path}"); return; }
            var catalog = UnityCatalogLoader.FromJson(File.ReadAllText(path));

            _art = new CardArtLibrary(artFolder);
            if (!_art.Available)
                Debug.LogWarning($"Sin arte de cartas en '{artFolder}' (se usan quads de color).");

            _engine = new GameEngine(catalog) { Effects = CardEffects.BuildResolver() };
            _engine.StartGame(
                SampleDeckBuilder.Build(catalog, historiaP0, 40),
                SampleDeckBuilder.Build(catalog, historiaP1, 40),
                seed, firstPlayer: 0);

            _status = "Partida iniciada.";
            BuildBoard();
            Rebuild();
        }

        private void Update()
        {
            if (_engine == null) return;

            if (_drag != null) { DragUpdate(); return; }

            var hit = RaycastCard();
            if (!ReferenceEquals(hit, _hovered))
            {
                if (_hovered != null) _hovered.SetHovered(false);
                _hovered = hit;
                if (_hovered != null) _hovered.SetHovered(true);
            }

            if (!_engine.State.IsOver && Input.GetMouseButtonDown(0) && hit != null)
                OnPointerDown(hit);
        }

        private void OnPointerDown(CardView cv)
        {
            var s = _engine.State;
            // Carta jugable de tu mano (visible) -> arrastrar; resto (tapear TIERRA) -> clic.
            if (cv.OwnerId == s.ActivePlayer && !cv.FaceDown && s.Active.Mano.Cards.Contains(cv.Card) && IsPlayable(s.Active, cv.Card))
                BeginDrag(cv);
            else
                OnCardClicked(cv);
        }

        private void BeginDrag(CardView cv)
        {
            _drag = cv;
            if (_hovered != null) { _hovered.SetHovered(false); _hovered = null; }
            ShowGlowFor(cv.Card.Type, _engine.State.ActivePlayer);
        }

        private void DragUpdate()
        {
            _drag!.transform.position = CursorOnPlane(0.5f); // sigue al cursor, levantada
            if (Input.GetMouseButtonUp(0)) EndDrag();
        }

        private void EndDrag()
        {
            var cv = _drag!;
            _drag = null;
            HideGlow();

            var card = cv.Card;
            bool inField = Mathf.Abs(cv.transform.position.z) < BoardLayout.HandZ - 1f;
            if (inField && !_engine.State.IsOver)
            {
                var r = card.Type switch
                {
                    CardType.Tierra => _engine.PlayTierra(card),
                    CardType.Concepto => _engine.PlayConcepto(card, faceDown: false),
                    _ => _engine.PlaySer(card)
                };
                _status = $"{card.Nombre}: {(r.Ok ? "OK" : r.Error)}";
            }
            Rebuild(); // jugada o snap-back
        }

        private Vector3 CursorOnPlane(float y)
        {
            if (Camera.main == null) return Vector3.zero;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            return plane.Raycast(ray, out var d) ? ray.GetPoint(d) : Vector3.zero;
        }

        private void ShowGlowFor(CardType type, int player)
        {
            HideGlow();
            if (type == CardType.Tierra)
                foreach (var g in _glowTierra[player]) g.SetActive(true);
            else if (type == CardType.Concepto)
                _glowConcepto[player]?.SetActive(true);
            else if (CardTypeNames.IsSer(type))
                foreach (var g in _glowSer[player]) g.SetActive(true);
        }

        private void HideGlow()
        {
            for (int p = 0; p < 2; p++)
            {
                foreach (var g in _glowTierra[p]) g.SetActive(false);
                foreach (var g in _glowSer[p]) g.SetActive(false);
                _glowConcepto[p]?.SetActive(false);
            }
        }

        private CardView? RaycastCard()
        {
            if (Camera.main == null) return null;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out var hit)
                ? hit.collider.GetComponentInParent<CardView>()
                : null;
        }

        private bool IsPlayable(PlayerState p, CardInstance c)
        {
            if (c.Type == CardType.Tierra) return !p.TierraPlayedThisTurn && !p.Tierras.IsFull;
            if (c.Type == CardType.Concepto) return !p.ConceptosBlockedThisTurn && p.Fd >= (c.Def.Coste ?? 0);
            if (CardTypeNames.IsSer(c.Type)) return !p.Seres.IsFull && p.Fd >= (c.Def.Coste ?? 0);
            return false;
        }

        private void OnCardClicked(CardView cv)
        {
            var s = _engine.State;
            if (cv.OwnerId != s.ActivePlayer || cv.Card == null) return;
            var p = s.Active;
            CommandResult r;

            if (p.Mano.Cards.Contains(cv.Card))
            {
                r = cv.Card.Type switch
                {
                    CardType.Tierra => _engine.PlayTierra(cv.Card),
                    CardType.Concepto => _engine.PlayConcepto(cv.Card, faceDown: false),
                    _ => _engine.PlaySer(cv.Card)
                };
            }
            else if (p.Tierras.Cards.Contains(cv.Card) && !cv.Card.Tapped)
            {
                r = _engine.TapTierra(cv.Card);
            }
            else return;

            _status = $"{cv.Card.Nombre}: {(r.Ok ? "OK" : r.Error)}";
            Rebuild();
        }

        // ---------------- render ----------------

        private void Rebuild()
        {
            foreach (var v in _spawned) if (v != null) Destroy(v.gameObject);
            _spawned.Clear();
            _hovered = null; // los CardView se destruyen; evita referencia colgante
            var s = _engine.State;

            for (int p = 0; p < 2; p++)
            {
                if (_mazoCount[p] != null) _mazoCount[p].text = "MAZO\n" + s.Players[p].Mazo.Count;
                if (_retirCount[p] != null) _retirCount[p].text = "RETIR.\n" + s.Players[p].Retirados.Count;
            }

            var curHandIds = new HashSet<int>();

            for (int p = 0; p < 2; p++)
            {
                var ps = s.Players[p];
                bool hideHand = p != 0; // ocultar la mano del rival

                for (int i = 0; i < ps.Mano.Count; i++)
                {
                    var card = ps.Mano.Cards[i];
                    curHandIds.Add(card.InstanceId);
                    bool playable = p == s.ActivePlayer && !s.IsOver && IsPlayable(ps, card);
                    var (fpos, frot) = BoardLayout.HandFan(p, i, ps.Mano.Count);
                    // Mano rival gira 180° (espejo) -> no necesita flip de textura.
                    var v = Spawn(card, p, fpos, hideHand, playable, frot, flipTexture: p == 0);
                    // Carta recién robada/añadida: animarla desde el mazo.
                    if (!_prevHandIds.Contains(card.InstanceId))
                        StartCoroutine(Deal(v.transform, BoardLayout.Mazo(p) + Vector3.up * 0.3f, fpos));
                }

                for (int i = 0; i < ps.Tierras.Count; i++)
                    Spawn(ps.Tierras.Cards[i], p, BoardLayout.Tierra(p, i), false);

                // Mazo: montón de reversos escalonados (parece pila de cartas).
                if (ps.Mazo.Count > 0)
                {
                    int stack = Mathf.Clamp(ps.Mazo.Count, 1, 12);
                    for (int i = 0; i < stack; i++)
                        Spawn(ps.Mazo.Top!, p, BoardLayout.Mazo(p) + Vector3.up * (i * 0.035f), faceDown: true);
                }

                // Retirados: la última carta que llegó, boca arriba.
                if (ps.Retirados.Count > 0)
                {
                    var top = ps.Retirados.Cards[ps.Retirados.Count - 1];
                    Spawn(top, p, BoardLayout.Descarte(p) + Vector3.up * 0.02f, faceDown: false);
                }

                for (int i = 0; i < ps.Seres.Count; i++)
                    Spawn(ps.Seres.Cards[i], p, BoardLayout.Ser(p, i), false);

                if (ps.Concepto.Top != null)
                    Spawn(ps.Concepto.Top, p, BoardLayout.Concepto(p), true);

                if (ps.Historia != null) // HISTORIA se muestra en horizontal (girada 90°)
                    Spawn(ps.Historia, p, BoardLayout.Historia(p), false, false, Quaternion.Euler(0f, 90f, 0f));

                var diaTop = ps.PilaDia.Cards.FirstOrDefault(d => PlayerState.DiaNumero(d) == ps.DiaActual);
                if (diaTop != null)
                    Spawn(diaTop, p, BoardLayout.Dia(p), false);
            }

            _prevHandIds.Clear();
            _prevHandIds.UnionWith(curHandIds); // base para detectar nuevas cartas el próximo rebuild
        }

        private CardView Spawn(CardInstance c, int owner, Vector3 pos, bool faceDown,
                               bool playable = false, Quaternion? rot = null, bool flipTexture = true)
        {
            var v = CardView.Create(transform);
            v.Bind(c, owner, faceDown, _art.Front(c.Nombre), _art.Back(), flipTexture);
            v.Playable = playable;
            v.Place(pos, rot ?? Quaternion.identity);
            _spawned.Add(v);
            return v;
        }

        private IEnumerator Deal(Transform t, Vector3 from, Vector3 to)
        {
            if (t == null) yield break;
            const float dur = 0.25f;
            float e = 0f;
            t.position = from;
            while (e < dur && t != null)
            {
                e += Time.deltaTime;
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / dur));
                yield return null;
            }
            if (t != null) t.position = to;
        }

        private void EnsureSceneRig()
        {
            // Reusa la cámara existente o crea una, y SIEMPRE la reposiciona al ángulo del tablero.
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = orthographic;
            if (orthographic) cam.orthographicSize = orthoSize; // sin perspectiva (tablero parejo)
            else cam.fieldOfView = camFov;
            cam.transform.position = camPos;
            cam.transform.LookAt(camLookAt); // mira al centro del tablero (más cenital)
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

            var light = FindAnyObjectByType<Light>();
            if (light == null) light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = lightIntensity; // ajustable en el Inspector
            light.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // cenital: ilumina de lleno las cartas planas
            // Ambiente bajo para levantar sombras sin lavar (los materiales son mate, sin glare).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.30f, 0.33f);
        }

        // ---------------- tablero estático (mesa, zonas, etiquetas) ----------------

        private void BuildBoard()
        {
            _board = new GameObject("Board").transform;
            BuildGlow(); // resaltados de zona (ocultos hasta arrastrar)

            // Si hay imagen de fondo, la usamos como campo y ocultamos las zonas procedurales.
            var bg = LoadTextureFromFile(backgroundPath);
            if (bg != null) { BuildBackground(bg); return; }

            var table = new Color(0.16f, 0.12f, 0.08f);
            var pad = new Color(0.11f, 0.10f, 0.08f);
            var gold = new Color(0.80f, 0.66f, 0.28f);
            var label = new Color(0.78f, 0.72f, 0.52f);

            Box(new Vector3(0f, -0.10f, 0f), new Vector3(26f, 0.02f, 22f), table);   // mesa
            Box(new Vector3(0f, -0.02f, 0f), new Vector3(20f, 0.04f, 0.12f), gold);  // línea central

            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < 7; i++) Pad(BoardLayout.Tierra(p, i), pad);
                ZoneLabel("TIERRAS", BoardLayout.Tierra(p, 3), 40, 0.11f, label);

                var mp = BoardLayout.Mazo(p);
                Pad(mp, pad);
                _mazoCount[p] = ZoneLabel("MAZO", mp + new Vector3(Mathf.Sign(mp.x) * 1.4f, 0f, 0f), 30, 0.08f, gold);

                var dp = BoardLayout.Descarte(p);
                Pad(dp, pad);
                _retirCount[p] = ZoneLabel("RETIR.", dp + new Vector3(Mathf.Sign(dp.x) * 1.4f, 0f, 0f), 30, 0.08f, label);

                Pad(BoardLayout.Concepto(p), pad);
                ZoneLabel("CONC.", BoardLayout.Concepto(p), 32, 0.09f, label);

                for (int si = 0; si < 3; si++)
                {
                    Pad(BoardLayout.Ser(p, si), pad);
                    ZoneLabel("SER", BoardLayout.Ser(p, si), 34, 0.10f, label);
                }

                Pad(BoardLayout.Dia(p), pad);
                ZoneLabel("DÍA", BoardLayout.Dia(p), 34, 0.10f, label);

                Pad(BoardLayout.Historia(p), pad);
                ZoneLabel("HISTORIA", BoardLayout.Historia(p), 28, 0.075f, label);
            }
        }

        private void BuildBackground(Texture2D tex)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_board, false);
            go.transform.position = new Vector3(0f, -0.05f, 0f);
            go.transform.localScale = new Vector3(30f, 0.02f, 16.85f); // ~16:9
            Destroy(go.GetComponent<Collider>());
            var m = go.GetComponent<MeshRenderer>().material;
            m.color = Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f); // mate, sin glare
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            // Misma corrección 180° que las cartas (cara superior del cubo mapea "de cabeza").
            var sc = new Vector2(-1f, -1f);
            var off = new Vector2(1f, 1f);
            m.mainTextureScale = sc;
            m.mainTextureOffset = off;
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTextureScale("_BaseMap", sc);
                m.SetTextureOffset("_BaseMap", off);
            }
        }

        private static Texture2D? LoadTextureFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (t.LoadImage(File.ReadAllBytes(path)))
                {
                    t.wrapMode = TextureWrapMode.Clamp;
                    return t;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"No se pudo cargar el fondo {path}: {e.Message}");
            }
            return null;
        }

        private void BuildGlow()
        {
            var gT = new Color(0.35f, 1.0f, 0.45f); // TIERRA verde
            var gS = new Color(0.45f, 0.7f, 1.0f);  // SER azul
            var gC = new Color(0.75f, 0.5f, 1.0f);  // CONCEPTO morado
            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < 7; i++) _glowTierra[p].Add(Glow(BoardLayout.Tierra(p, i), gT));
                for (int s = 0; s < 3; s++) _glowSer[p].Add(Glow(BoardLayout.Ser(p, s), gS));
                _glowConcepto[p] = Glow(BoardLayout.Concepto(p), gC);
            }
        }

        private GameObject Glow(Vector3 slot, Color c)
        {
            var go = Box(new Vector3(slot.x, 0.01f, slot.z), new Vector3(1.7f, 0.02f, 2.3f), c);
            go.SetActive(false);
            return go;
        }

        private GameObject Box(Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_board, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>()); // no bloquear el raycast de cartas
            var m = go.GetComponent<MeshRenderer>().material;
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return go;
        }

        private void Pad(Vector3 slotPos, Color color)
            => Box(new Vector3(slotPos.x, -0.04f, slotPos.z), new Vector3(1.55f, 0.02f, 2.15f), color);

        private TextMesh ZoneLabel(string txt, Vector3 slotPos, int fontSize, float charSize,
                                   Color col, bool yUp = false)
        {
            var go = new GameObject("ZoneLabel");
            go.transform.SetParent(_board, false);
            go.transform.position = new Vector3(slotPos.x, yUp ? 0.12f : 0.0f, slotPos.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = txt;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.color = col;
            return tm;
        }

        // ---------------- HUD (IMGUI, sin paquetes) ----------------

        private void OnGUI()
        {
            if (_engine == null) return;
            var s = _engine.State;
            GUILayout.BeginArea(new Rect(Screen.width - 370, 10, 360, 220), GUI.skin.box);
            GUILayout.Label($"Turno {s.TurnNumber} · Fase {s.Phase} · Activo P{s.ActivePlayer}");
            GUILayout.Label($"P0  FD={s.Players[0].Fd}  Día={s.Players[0].DiaActual}  Mano={s.Players[0].Mano.Count}");
            GUILayout.Label($"P1  FD={s.Players[1].Fd}  Día={s.Players[1].DiaActual}  Mano={s.Players[1].Mano.Count}");
            GUILayout.Label(_status);

            if (s.IsOver)
                GUILayout.Label($"FIN: gana P{s.Winner} (Victoria {s.WinReason})");
            else if (GUILayout.Button("Terminar turno"))
            {
                var r = _engine.EndTurn();
                _status = r.Ok ? "Turno terminado." : r.Error;
                Rebuild();
            }
            GUILayout.EndArea();

            if (_hovered != null && _hovered.Card != null)
                DrawCardDetail(_hovered);
        }

        private GUIStyle? _wrap;

        private void DrawCardDetail(CardView cv)
        {
            _wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

            if (cv.FaceDown)
            {
                GUILayout.BeginArea(new Rect(10, 10, 330, 60), GUI.skin.box);
                GUILayout.Label("Carta oculta");
                GUILayout.EndArea();
                return;
            }

            var d = cv.Card.Def;
            GUILayout.BeginArea(new Rect(10, 10, 340, 470), GUI.skin.box);

            var art = _art.Front(d.Nombre);
            if (art != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(art, GUILayout.Width(150), GUILayout.Height(214));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Label(d.Nombre, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Label($"Tipo: {d.Type}");
            if (d.Coste.HasValue) GUILayout.Label($"Coste FD: {d.Coste}");
            if (d.Fd.HasValue) GUILayout.Label($"Genera FD: {d.Fd}");
            if (CardTypeNames.IsSer(d.Type))
                GUILayout.Label($"Duración: {cv.Card.DurLeft} (base {d.Dur}) · actCost {d.ActCost}");
            if (!string.IsNullOrEmpty(d.TipoConcepto)) GUILayout.Label($"Concepto: {d.TipoConcepto}");

            if (!string.IsNullOrEmpty(d.AlEntrar)) GUILayout.Label($"Al entrar: {d.AlEntrar}", _wrap);
            if (!string.IsNullOrEmpty(d.Activado)) GUILayout.Label($"Activado: {d.Activado}", _wrap);
            if (!string.IsNullOrEmpty(d.AlSalir)) GUILayout.Label($"Al salir: {d.AlSalir}", _wrap);
            if (!string.IsNullOrEmpty(d.Efecto)) GUILayout.Label($"Efecto: {d.Efecto}", _wrap);
            if (cv.Playable) GUILayout.Label(">> Jugable (clic)");
            GUILayout.EndArea();
        }
    }
}
