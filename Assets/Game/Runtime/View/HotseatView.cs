using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Game.Core.AI;
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
        [SerializeField] private int aiPlayer = 1; // jugador controlado por IA (-1 = ninguno)
        [SerializeField] private float turnSeconds = 180f; // temporizador por turno
        private bool _aiRunning;
        private float _turnTimer;
        private int _timerTurn = -1;

        // --- Tutorial (partida-tutorial #1): IA pasiva + explicaciones paso a paso ---
        private bool _tutorial;
        private int _tutIdx;
        private Texture2D? _auraTex;    // aura suave redondeada para resaltar
        private Rect _phaseRect;        // rect del panel de fase (para resaltarlo)
        private bool _gameOver;         // latch por-frame de State.IsOver (evita carrera Layout/Repaint)
        private int _gameWinner = -1;   // latch por-frame del ganador

        // Qué zona/carta iluminar en cada paso del tutorial.
        private enum TutHL { None, Tierra, Ser, Concepto, Dia, Historia, HandTierra, HandSer, HandConcepto, Phase, Fd }

        private static readonly (string msg, TutHL hl)[] TutSteps =
        {
            ("Bienvenido al campo. Aquí se libran las Historias de la Biblia. Te enseño tus zonas, tus cartas y cómo se gana antes de jugar.", TutHL.None),
            ("Estas 7 ranuras son de TIERRA. Las TIERRAs son tu base: colocas una por turno y, al taparlas, generan FD (tu recurso).", TutHL.Tierra),
            ("Estas 3 ranuras son de SER (personajes/criaturas). Se bajan pagando su coste en FD y actúan en tu campo.", TutHL.Ser),
            ("Esta ranura es de CONCEPTO: cartas de efecto puntual. Puedes jugarlas al momento, o dejarlas boca abajo para activarlas después.", TutHL.Concepto),
            ("Esta es la zona de DÍA (1→7). Los DÍAs marcan el avance; cada DÍA activado puede darte una recompensa, y llegar al DÍA 7 ES una victoria.", TutHL.Dia),
            ("Esta es la zona de HISTORIA: tu otra vía de victoria. Reúne sus 5 piezas en tu campo para ganar.", TutHL.Historia),
            ("El FD es tu recurso para jugar cartas. Aquí ves tu FD actual. En una partida normal empieza en 0 y se reinicia a 0 al comenzar cada uno de tus turnos.", TutHL.Fd),
            ("¿Cómo consigues FD? Tapeando tus TIERRAs en campo (clic en una TIERRA sin tapear): cada una suma su FD. Así pagas SERes, CONCEPTOs y DÍAs.", TutHL.Tierra),
            ("Ahora los TIPOS DE CARTA en tu mano. Esta es una TIERRA: se coloca en campo (1 por turno) y, al taparla, te da FD.", TutHL.HandTierra),
            ("Esta es una carta de SER: tus personajes. Se juega pagando su coste en FD y ocupa una de las 3 ranuras de SER.", TutHL.HandSer),
            ("Esta es una carta de CONCEPTO: efecto puntual. Puedes jugarla ya, o dejarla boca abajo y activarla más tarde —incluso en el turno del rival— si tienes el FD que pide.", TutHL.HandConcepto),
            ("Las cartas DÍA (1→7) avanzan la partida en orden. Cada DÍA activado puede darte una recompensa, y activar el DÍA 7 es una condición de VICTORIA.", TutHL.Dia),
            ("La HISTORIA es tu condición de victoria principal: reúne sus 5 piezas en el campo (TIERRAs y SERes) y ganarás.", TutHL.Historia),
            ("FASES del turno: PRELUDIO (se reinicia tu FD a 0) → GÉNESIS (robas) → PREPARACIÓN (juegas y tapeas) → ENTREGA (se comprueba la victoria). Aquí ves la fase y el tiempo.", TutHL.Phase),
            ("Objetivo de esta partida: reúne las 5 piezas de tu HISTORIA en el campo. Ya tienes 2 TIERRAs puestas y FD de sobra; te guío para bajar las 3 SER. El rival no atacará. ¡Adelante!", TutHL.None),
        };

        // --- Fase B: jugadas guiadas hasta la victoria garantizada ---
        private int _guideStep = -1;      // -1 = aún en la fase de explicación
        private bool _rewardGiven;
        public static bool ReturnRequested; // el menú (MenuShell) lo detecta para volver al MainMenu

        private static readonly (string msg, TutHL hl)[] GuideSteps =
        {
            ("Tienes las 2 primeras piezas (TIERRA) ya en el campo. Arrastra a ADÁN a una ranura de SER.", TutHL.HandSer),
            ("¡Bien! Ahora arrastra a EVA a otra ranura de SER.", TutHL.HandSer),
            ("Ya casi. Arrastra a LA SERPIENTE a la última ranura de SER.", TutHL.HandSer),
            ("¡Tus 5 piezas están en el campo! Pulsa SIGUIENTE FASE para completar tu Historia y GANAR.", TutHL.Phase),
        };

        private readonly RuntimeDecisionProvider _decisions = new();
        private AutoDecisionProvider _auto = new();
        private bool _busy;
        private System.Threading.Tasks.Task? _cmdTask;
        private CommandResult _cmdResult;
        private string _cmdName = "";

        private DecisionRequest? _decReq;
        private CardInstance? _decSelected;
        private readonly List<CardInstance> _decOrder = new();
        private Vector2 _decScroll;
        private System.Action? _deferredResolve;

        [Header("Cámara (ajustable en el Inspector)")]
        [SerializeField] private Vector3 camPos = new Vector3(0f, 26f, -5.7f);
        [SerializeField] private Vector3 camRotation = new Vector3(78.69f, 0f, 0f);
        [SerializeField] private float camFov = 31f;
        [SerializeField] private bool orthographic = false;
        [SerializeField] private float orthoSize = 9.5f;
        [SerializeField] private float lightIntensity = 0.85f;

        [Header("Arte de cartas (carpeta de imágenes)")]
        [SerializeField] private string artFolder = @"C:\Users\Rinco\Downloads";

        [Header("Fondo del campo (vacío = zonas procedurales)")]
        [SerializeField] private string backgroundPath = "";

        private GameEngine _engine = null!;
        private CardArtLibrary _art = null!;
        private Transform _board = null!;
        private readonly TextMesh[] _mazoCount = new TextMesh[2];
        private readonly TextMesh[] _retirCount = new TextMesh[2];
        private readonly TextMesh[] _fdLabel = new TextMesh[2];
        private readonly TextMesh[] _handCount = new TextMesh[2];
        private readonly List<CardView> _spawned = new();
        private CardView? _hovered;
        private CardView? _drag;
        private string _status = "";

        private readonly HashSet<int> _prevHandIds = new();
        private readonly HashSet<int> _wasTapped = new();
        private readonly Dictionary<int, int>[] _serSlot = { new(), new() };
        private readonly Dictionary<int, int>[] _tierraSlot = { new(), new() };
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

            _auto = new AutoDecisionProvider(catalog); // IA consciente de sus piezas al usar tutores
            _engine = new GameEngine(catalog) { Effects = CardEffects.BuildResolver(), Decisions = _auto };
            _engine.ResponseWindow = OnResponseWindow; // permite activar trampas en el turno rival
            _engine.StartGame(
                SampleDeckBuilder.Build(catalog, historiaP0, 40, pieceCopies: 3), // 3 copias de cada pieza
                SampleDeckBuilder.Build(catalog, historiaP1, 40, pieceCopies: 3),
                seed, firstPlayer: 0);

            EnsureResponseInHand(_engine.State.Players[0]); // P0 arranca con una trampa para probar

            _tutorial = PlayerPrefs.GetInt("tutorial_match", 0) == 1;
            if (_tutorial)
            {
                PlayerPrefs.SetInt("tutorial_match", 0); PlayerPrefs.Save();
                _tutIdx = 0; _guideStep = -1; _rewardGiven = false; ReturnRequested = false;
                SetupTutorialField(_engine.State.Players[0]); // mano fija + piezas preparadas para ganar
            }

            _status = "Partida iniciada.";
            BuildBoard();
            Rebuild();
            MaybeRunAI();
        }

        /// <summary>Garantiza que la mano de apertura tenga una carta de respuesta (para probar trampas).</summary>
        private void EnsureResponseInHand(PlayerState p)
        {
            if (p.Mano.Cards.Any(c => _engine.Effects.IsResponse(c.Def.Id))) return;
            var trap = p.Mazo.Cards.FirstOrDefault(c => _engine.Effects.IsResponse(c.Def.Id));
            if (trap == null) return;
            p.Mazo.Remove(trap);
            p.Mano.Add(trap);
        }

        private void MaybeRunAI()
        {
            if (_aiRunning || aiPlayer < 0 || _engine == null || _engine.State.IsOver) return;
            if (_engine.State.ActivePlayer == aiPlayer) StartCoroutine(AiTurn());
        }

        private IEnumerator AiTurn()
        {
            _aiRunning = true;
            _status = "IA pensando...";
            _engine.Decisions = _auto; // la IA decide sin UI
            yield return new WaitForSeconds(0.7f);

            // En hilo: así OnGUI sigue corriendo y puedes responder con una trampa.
            // En tutorial la IA es PASIVA (no juega): solo pasa el turno, para no amenazar al jugador.
            if (!_tutorial)
            {
                var t1 = System.Threading.Tasks.Task.Run(() =>
                {
                    try { SimpleAI.PlayTurn(_engine); }
                    catch (System.Exception e) { Debug.LogError(e); }
                });
                while (!t1.IsCompleted) yield return null;
            }
            Rebuild();
            yield return new WaitForSeconds(0.6f);

            if (!_engine.State.IsOver)
            {
                var t2 = System.Threading.Tasks.Task.Run(() =>
                {
                    try { _engine.EndTurn(); }
                    catch (System.Exception e) { Debug.LogError(e); }
                });
                while (!t2.IsCompleted) yield return null;
                Rebuild();
            }
            _aiRunning = false;
            MaybeRunAI(); // por si el siguiente turno también es IA
        }

        private void Update()
        {
            if (_engine == null) return;

            // Resolver decisión (Aceptar) fuera del ciclo OnGUI para no romper el GUILayout.
            if (_deferredResolve != null) { var a = _deferredResolve; _deferredResolve = null; a(); }

            // Latch estable por-frame de fin de partida: OnGUI (Layout y Repaint son eventos
            // distintos) debe ver el MISMO valor aunque el hilo de comandos cambie IsOver en medio.
            _gameOver = _engine.State.IsOver;
            _gameWinner = _engine.State.Winner ?? -1;

            // Fase B (guiada): avanzar el paso cuando el jugador cumple lo pedido.
            if (_tutorial && _tutIdx >= TutSteps.Length)
            {
                if (_guideStep < 0) _guideStep = 0;
                else if (_guideStep < GuideSteps.Length && GuideDone(_guideStep)) _guideStep++;
            }

            SnapshotLog(); // congelar el log una vez por frame (la IA lo escribe en otro hilo)
            _respShow = _respPending; // latch (lo activa el hilo de la IA): estable durante los pases de OnGUI

            // Comando humano en curso (en hilo): esperar a que termine o a resolver decisión.
            if (_busy)
            {
                if (_cmdTask != null && _cmdTask.IsCompleted)
                {
                    _busy = false;
                    _engine.Decisions = _auto;
                    _status = $"{_cmdName}: {(_cmdResult.Ok ? "OK" : _cmdResult.Error)}";
                    Rebuild();
                    MaybeRunAI();
                }
                return; // sin input/hover mientras se procesa; la decisión se atiende en OnGUI
            }

            TickTimer();

            if (_drag != null) { DragUpdate(); return; }

            var hit = RaycastCard();
            if (!ReferenceEquals(hit, _hovered))
            {
                if (_hovered != null) _hovered.SetHovered(false);
                _hovered = hit;
                if (_hovered != null) _hovered.SetHovered(true);
                UpdateHandCount();
            }

            if (!_engine.State.IsOver && Input.GetMouseButtonDown(0) && hit != null)
                OnPointerDown(hit);
        }

        private void OnPointerDown(CardView cv)
        {
            var s = _engine.State;
            if (TutBlocksPlay(cv.Card)) { _status = TutBlockMsg(); return; } // tutorial: solo la carta guiada

            // Carta jugable de tu mano (visible) -> arrastrar; resto (tapear TIERRA) -> clic.
            if (cv.OwnerId == s.ActivePlayer && !cv.FaceDown && s.Active.Mano.Cards.Contains(cv.Card) && IsPlayable(s.Active, cv.Card))
                BeginDrag(cv);
            else
                OnCardClicked(cv);
        }

        /// <summary>Tutorial: durante la explicación no se juega nada; en la fase guiada solo la carta esperada.</summary>
        private bool TutBlocksPlay(CardInstance? card)
        {
            if (!_tutorial || card == null) return false;
            if (_tutIdx < TutSteps.Length) return true;        // Fase A: bloquear todo
            return card.Nombre != ExpectedGuideCard();          // Fase B: solo la pieza del paso
        }

        private string? ExpectedGuideCard() => _guideStep switch
        {
            0 => "Adán",
            1 => "Eva",
            2 => "La Serpiente",
            _ => null,
        };

        private string TutBlockMsg()
        {
            if (_tutIdx < TutSteps.Length) return "Sigue el tutorial: pulsa Siguiente.";
            var e = ExpectedGuideCard();
            return e != null ? $"El tutorial pide jugar: {e}." : "Pulsa SIGUIENTE FASE para terminar tu Historia.";
        }

        private void BeginDrag(CardView cv)
        {
            _drag = cv;
            cv.transform.rotation = Quaternion.identity;      // enderezar la carta al arrastrarla
            if (_hovered != null) { _hovered.SetHovered(false); _hovered = null; }
            ShowGlowFor(cv.Card.Type, _engine.State.ActivePlayer);
        }

        private void DragUpdate()
        {
            _drag!.transform.position = CursorOnPlane(0.5f); // sigue al cursor, levantada
            _drag.transform.rotation = Quaternion.identity;  // se mantiene recta mientras se arrastra
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
                if (card.Type == CardType.Concepto) { PlayOrPlaceConcepto(card); return; }
                System.Func<CommandResult> cmd = card.Type == CardType.Tierra
                    ? () => _engine.PlayTierra(card)
                    : () => _engine.PlaySer(card);
                RunHumanCommand(card.Nombre, cmd);
            }
            else
            {
                Rebuild(); // snap-back
            }
        }

        /// <summary>
        /// CONCEPTO normal: se juega boca arriba. CONCEPTO de RESPUESTA: abre un modal para elegir
        /// "Jugar ya" o "Boca abajo" (trampa).
        /// </summary>
        private void PlayOrPlaceConcepto(CardInstance card)
        {
            if (_engine.Effects.IsResponse(card.Def.Id))
            {
                _placeCard = card; _placePending = true;
                Rebuild(); // devuelve la carta a la mano mientras el modal decide
            }
            else
            {
                RunHumanCommand(card.Nombre, () => _engine.PlayConcepto(card, faceDown: false));
            }
        }

        private Vector3 CursorOnPlane(float y)
        {
            if (Camera.main == null) return Vector3.zero;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            return plane.Raycast(ray, out var d) ? ray.GetPoint(d) : Vector3.zero;
        }

        // El resaltado de zonas al arrastrar ahora se dibuja como AURA en OnGUI (DrawPlayAura),
        // más suave que los quads sólidos. Mantener los quads ocultos.
        private void ShowGlowFor(CardType type, int player) => HideGlow();

        private void HideGlow()
        {
            for (int p = 0; p < 2; p++)
            {
                foreach (var g in _glowTierra[p]) g.SetActive(false);
                foreach (var g in _glowSer[p]) g.SetActive(false);
                _glowConcepto[p]?.SetActive(false);
            }
        }

        /// <summary>Ejecuta un comando del jugador en un hilo (para que las decisiones puedan pausar).</summary>
        private void RunHumanCommand(string name, System.Func<CommandResult> cmd)
        {
            if (_busy || _aiRunning) return;
            _busy = true;
            _cmdName = name;
            _engine.Decisions = _decisions; // interactivo durante el comando humano
            _cmdTask = System.Threading.Tasks.Task.Run(() =>
            {
                try { _cmdResult = cmd(); }
                catch (System.Exception e) { _cmdResult = CommandResult.Fail(e.Message); }
            });
        }

        private void UpdateHandCount()
        {
            for (int p = 0; p < 2; p++)
                if (_handCount[p] != null) _handCount[p].gameObject.SetActive(false);

            if (_hovered == null || _hovered.Card == null) return;
            int o = _hovered.OwnerId;
            if (_handCount[o] != null && _engine.State.Players[o].Mano.Cards.Contains(_hovered.Card))
            {
                _handCount[o].text = "Mano: " + _engine.State.Players[o].Mano.Count;
                _handCount[o].gameObject.SetActive(true);
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
            if (c.Type == CardType.Concepto)
                return !p.ConceptosBlockedThisTurn && p.Fd >= (c.Def.Coste ?? 0)
                       && !p.Concepto.Cards.Any(x => x.FaceDown); // trampa boca abajo bloquea otros CONCEPTOS
            if (CardTypeNames.IsSer(c.Type)) return !p.Seres.IsFull && p.Fd >= (c.Def.Coste ?? 0);
            return false;
        }

        private void OnCardClicked(CardView cv)
        {
            var s = _engine.State;
            if (cv.OwnerId != s.ActivePlayer || cv.Card == null) return;
            var p = s.Active;
            var card = cv.Card;
            System.Func<CommandResult>? cmd = null;

            if (p.Tierras.Cards.Contains(card) && !card.Tapped)
                cmd = () => _engine.TapTierra(card);               // tapear TIERRA -> FD
            else if (p.Seres.Cards.Contains(card))
                cmd = () => _engine.ActivateSerEffect(card);       // efecto ACTIVADO del SER
            else if (card.Type == CardType.Dia)
                cmd = () => _engine.ActivateDia(useFree: false);   // activar el DÍA actual
            else if (p.Mano.Cards.Contains(card))                  // mano no arrastrable -> intentar jugar
            {
                if (card.Type == CardType.Concepto) { PlayOrPlaceConcepto(card); return; }
                cmd = card.Type == CardType.Tierra
                    ? () => _engine.PlayTierra(card)
                    : () => _engine.PlaySer(card);
            }

            if (cmd != null) RunHumanCommand(card.Nombre, cmd);
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
                if (_fdLabel[p] != null) _fdLabel[p].text = "FD\n" + s.Players[p].Fd;
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

                var tSlots = AssignSlots(_tierraSlot[p], ps.Tierras.Cards, 7);
                foreach (var t in ps.Tierras.Cards)
                {
                    var tv = Spawn(t, p, BoardLayout.Tierra(p, tSlots[t.InstanceId]), false);
                    tv.SetTapped(t.Tapped, animate: t.Tapped && !_wasTapped.Contains(t.InstanceId));
                }

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

                var sSlots = AssignSlots(_serSlot[p], ps.Seres.Cards, 3);
                foreach (var ser in ps.Seres.Cards)
                    Spawn(ser, p, BoardLayout.Ser(p, sSlots[ser.InstanceId]), false);

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

            // Recordar qué TIERRAs ya estaban tapeadas (para animar solo el giro nuevo).
            _wasTapped.Clear();
            foreach (var pl in _engine.State.Players)
                foreach (var t in pl.Tierras.Cards)
                    if (t.Tapped) _wasTapped.Add(t.InstanceId);
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

        /// <summary>Asigna a cada carta un slot estable (no se reordenan al retirarse otra).</summary>
        private static Dictionary<int, int> AssignSlots(Dictionary<int, int> map,
                                                        IReadOnlyList<CardInstance> cards, int max)
        {
            var present = new HashSet<int>(cards.Select(c => c.InstanceId));
            foreach (var k in map.Keys.Where(k => !present.Contains(k)).ToList()) map.Remove(k);

            var used = new HashSet<int>(map.Values);
            foreach (var c in cards)
            {
                if (map.ContainsKey(c.InstanceId)) continue;
                int slot = 0;
                while (slot < max && used.Contains(slot)) slot++;
                if (slot >= max) slot = max - 1;
                map[c.InstanceId] = slot;
                used.Add(slot);
            }
            return map;
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
            cam.transform.rotation = Quaternion.Euler(camRotation); // pose exacta
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

            // Una sola luz, al centro de donde estaban las 2 anteriores: promedio de (52,-28) y (70,48).
            var oldFill = GameObject.Find("Fill Light Left");
            if (oldFill != null) Destroy(oldFill);

            var light = FindAnyObjectByType<Light>();
            if (light == null) light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = lightIntensity; // ajustable en el Inspector
            light.transform.rotation = Quaternion.Euler(61f, 10f, 0f); // centro de las 2 luces previas
            light.shadows = LightShadows.Soft;                          // sombra suave del grosor de la carta
            light.shadowStrength = 0.45f;
            light.color = new Color(1f, 0.93f, 0.80f);                  // cálida

            // Ambiente para levantar sombras sin lavar, tono cálido.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.35f, 0.30f);
        }

        // ---------------- tablero estático (mesa, zonas, etiquetas) ----------------

        private void BuildBoard()
        {
            _board = new GameObject("Board").transform;
            BuildGlow();     // resaltados de zona (ocultos hasta arrastrar)
            BuildHudLabels(); // contadores FD (mundo) + mano (al hover)

            // Fondo del campo: ruta de archivo si se indicó, si no el asset en Resources.
            var bg = LoadTextureFromFile(backgroundPath);
            if (bg == null) bg = Resources.Load<Texture2D>("FieldBackground");
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
            // Mismos ajustes que las cartas (Inspector aprobado).
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.716f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.31f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.31f);
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

        private void BuildHudLabels()
        {
            var gold = new Color(0.92f, 0.82f, 0.35f);
            for (int p = 0; p < 2; p++)
            {
                var mz = BoardLayout.Mazo(p);
                // FD al flanco OPUESTO del mazo, misma fila.
                var fdPos = new Vector3(Mathf.Sign(-mz.x) * 9f, 0f, mz.z);
                _fdLabel[p] = ZoneLabel("FD", fdPos, 42, 0.13f, gold);

                // Contador de mano al costado de la mano (oculto hasta hover).
                float handZ = (p == 0 ? -1f : 1f) * BoardLayout.HandZ;
                _handCount[p] = ZoneLabel("Mano", new Vector3(9f, 0f, handZ), 36, 0.10f, Color.white);
                _handCount[p].gameObject.SetActive(false);
            }
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

            var center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            var title = new GUIStyle(center) { fontSize = 13 };
            var big = new GUIStyle(center) { fontSize = 22, fontStyle = FontStyle.Bold };
            var sub = new GUIStyle(center) { fontSize = 15 };
            var timer = new GUIStyle(big) { fontSize = 20 };
            timer.normal.textColor = new Color(0.45f, 0.6f, 1f);

            float pw = 200f, ph = 200f;
            _phaseRect = new Rect(Screen.width - pw - 14f, (Screen.height - ph) * 0.5f, pw, ph);
            GUILayout.BeginArea(_phaseRect, GUI.skin.box);
            GUILayout.Space(6);
            GUILayout.Label("FASE ACTUAL", title);
            GUILayout.Label(PhaseName(s.Phase), big);
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUILayout.Label($"Turno {s.TurnNumber}", sub);
            GUILayout.Label(WhoseTurn(s), sub);
            GUILayout.Label(TimerStr(), timer);
            GUILayout.Space(4);

            if (_gameOver)
                GUILayout.Label($"FIN: gana P{_gameWinner} ({s.WinReason})", sub);
            else
            {
                bool humanTurn = aiPlayer < 0 || s.ActivePlayer != aiPlayer;
                // En el tutorial, SIGUIENTE FASE solo se habilita en el último paso guiado.
                bool tutOk = !_tutorial || _guideStep >= GuideSteps.Length - 1;
                GUI.enabled = humanTurn && !_aiRunning && !_busy && tutOk;
                if (GUILayout.Button("SIGUIENTE FASE", GUILayout.Height(28)))
                    RunHumanCommand("Terminar turno", () => _engine.EndTurn());
                GUI.enabled = true;
            }
            GUILayout.Label(_status, center);
            GUILayout.EndArea();

            if (_tutorial && _tutIdx < TutSteps.Length)
            {
                DrawTutHighlight(TutSteps[_tutIdx].hl); // ilumina la zona/carta mencionada

                float tw = 700f, th = 150f;
                GUILayout.BeginArea(new Rect((Screen.width - tw) * 0.5f, 18f, tw, th), GUI.skin.box); // arriba: no tapa mano/zonas
                GUILayout.Space(4);
                GUILayout.Label($"TUTORIAL   ({_tutIdx + 1}/{TutSteps.Length})", title);
                var wrap = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                GUILayout.Label(TutSteps[_tutIdx].msg, wrap, GUILayout.ExpandHeight(true));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(_tutIdx == TutSteps.Length - 1 ? "¡A jugar!" : "Siguiente", GUILayout.Height(30), GUILayout.Width(170)))
                    _tutIdx++;
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
                GUILayout.EndArea();
            }
            else if (_tutorial && _guideStep >= 0 && _guideStep < GuideSteps.Length && !_gameOver)
            {
                DrawTutHighlight(GuideSteps[_guideStep].hl); // ilumina la carta/panel del paso guiado
                float gw = 700f, gh = 120f;
                GUILayout.BeginArea(new Rect((Screen.width - gw) * 0.5f, 18f, gw, gh), GUI.skin.box);
                GUILayout.Space(4);
                GUILayout.Label($"TUTORIAL — Paso {_guideStep + 1}/{GuideSteps.Length}", title);
                var gwrap = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                GUILayout.Label(GuideSteps[_guideStep].msg, gwrap, GUILayout.ExpandHeight(true));
                GUILayout.EndArea();
            }

            if (_drag != null && _drag.Card != null) DrawPlayAura(_drag.Card.Type, s.ActivePlayer);

            if (_tutorial && _gameOver && _gameWinner == 0) DrawTutorialWin();

            if (_decisions.Pending != null)
            {
                DrawDecision(_decisions.Pending);
                if (_decSelected != null) // ver la carta elegida en el menú + sus efectos
                    DrawCardDetailFor(_decSelected, s.ActivePlayer);
            }
            else if (!_busy && _hovered != null && _hovered.Card != null)
            {
                DrawCardDetail(_hovered);
            }

            DrawHistory(s);

            if (_respShow && _respTrap != null) DrawResponsePrompt();
            else if (_placePending && _placeCard != null) DrawPlacement();
        }

        // ---------------- resaltado del tutorial (aura suave) ----------------

        /// <summary>Ilumina con aura dorada la zona/carta que explica el paso actual.</summary>
        private void DrawTutHighlight(TutHL hl)
        {
            if (hl == TutHL.None || Camera.main == null) return;

            var rects = new List<Rect>();
            switch (hl)
            {
                case TutHL.Tierra: for (int i = 0; i < 7; i++) rects.Add(ZoneRect(BoardLayout.Tierra(0, i))); break;
                case TutHL.Ser: for (int i = 0; i < 3; i++) rects.Add(ZoneRect(BoardLayout.Ser(0, i))); break;
                case TutHL.Concepto: rects.Add(ZoneRect(BoardLayout.Concepto(0))); break;
                case TutHL.Dia: AddCardOrZoneRect(rects, BoardLayout.Dia(0), c => c.Type == CardType.Dia); break;
                case TutHL.Historia: AddCardOrZoneRect(rects, BoardLayout.Historia(0), c => c.Type == CardType.Historia); break;
                case TutHL.HandTierra: AddHandRect(rects, c => c.Type == CardType.Tierra); break;
                case TutHL.HandSer: AddHandRect(rects, c => CardTypeNames.IsSer(c.Type)); break;
                case TutHL.HandConcepto: AddHandRect(rects, c => c.Type == CardType.Concepto); break;
                case TutHL.Phase: rects.Add(_phaseRect); break;
                case TutHL.Fd: rects.Add(FdRect()); break;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            var gold = new Color(1f, 0.82f, 0.32f);
            foreach (var r in rects) DrawAura(r, gold, pulse);
        }

        private Rect ZoneRect(Vector3 center) => WorldRect(center, 0.85f, 1.15f);

        private Rect FdRect()
        {
            var mz = BoardLayout.Mazo(0);
            var fd = new Vector3(Mathf.Sign(-mz.x) * 9f, 0f, mz.z); // igual que BuildHudLabels
            return WorldRect(fd, 1.1f, 0.9f);
        }

        /// <summary>Si hay una carta que cumple el filtro (en campo/mano de P0), usa su rect real; si no, la zona.</summary>
        private void AddCardOrZoneRect(List<Rect> rects, Vector3 zone, System.Func<CardInstance, bool> match)
        {
            var v = FindView(match);
            rects.Add(v != null ? ViewRect(v) : ZoneRect(zone));
        }

        /// <summary>Resalta la carta real de la mano (rect ajustado a la carta) que cumpla el filtro.</summary>
        private void AddHandRect(List<Rect> rects, System.Func<CardInstance, bool> match)
        {
            var mano = _engine.State.Players[0].Mano.Cards;
            var v = FindView(c => match(c) && mano.Contains(c));
            if (v != null) { rects.Add(ViewRect(v)); return; }
            // Respaldo: proyectar la posición teórica del abanico.
            int n = mano.Count;
            for (int i = 0; i < n; i++)
                if (match(mano[i])) { var (pos, _) = BoardLayout.HandFan(0, i, n); rects.Add(WorldRect(pos, 0.8f, 1.1f)); return; }
        }

        private CardView? FindView(System.Func<CardInstance, bool> match)
            => _spawned.FirstOrDefault(v => v != null && v.Card != null && v.OwnerId == 0 && match(v.Card));

        /// <summary>Rect en GUI ajustado a los límites reales (Renderer) de la carta mostrada.</summary>
        private Rect ViewRect(CardView v)
        {
            var rends = v.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return WorldRect(v.transform.position, 0.8f, 1.1f);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return ProjectBounds(b);
        }

        private Rect ProjectBounds(Bounds b)
        {
            var cam = Camera.main!;
            Vector3 c = b.center, e = b.extents;
            float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                var sp = cam.WorldToScreenPoint(corner);
                float gx = sp.x, gy = Screen.height - sp.y;
                minx = Mathf.Min(minx, gx); maxx = Mathf.Max(maxx, gx);
                miny = Mathf.Min(miny, gy); maxy = Mathf.Max(maxy, gy);
            }
            return Rect.MinMaxRect(minx, miny, maxx, maxy);
        }

        /// <summary>Proyecta un rectángulo del plano XZ del tablero a un Rect en coordenadas de GUI.</summary>
        private Rect WorldRect(Vector3 center, float hx, float hz)
        {
            var cam = Camera.main!;
            Vector3[] corners =
            {
                center + new Vector3(-hx, 0f, -hz),
                center + new Vector3( hx, 0f, -hz),
                center + new Vector3(-hx, 0f,  hz),
                center + new Vector3( hx, 0f,  hz),
            };
            float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
            foreach (var w in corners)
            {
                var sp = cam.WorldToScreenPoint(w);
                float gx = sp.x, gy = Screen.height - sp.y; // GUI: y hacia abajo
                minx = Mathf.Min(minx, gx); maxx = Mathf.Max(maxx, gx);
                miny = Mathf.Min(miny, gy); maxy = Mathf.Max(maxy, gy);
            }
            return Rect.MinMaxRect(minx, miny, maxx, maxy);
        }

        /// <summary>Aura luminosa suave y redondeada alrededor del rect (reemplaza el marco duro).</summary>
        private void DrawAura(Rect r, Color col, float pulse)
        {
            EnsureAuraTex();
            float pad = 20f + 6f * pulse; // el halo "respira"
            var outer = new Rect(r.x - pad, r.y - pad, r.width + 2f * pad, r.height + 2f * pad);
            var prev = GUI.color;
            GUI.color = new Color(col.r, col.g, col.b, 0.45f + 0.4f * pulse);
            GUI.DrawTexture(outer, _auraTex, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        /// <summary>Aura suave con esquinas redondeadas: blanco con alfa por SDF de rect redondeado
        /// (anillo brillante en el borde + interior tenue). Se tiñe con GUI.color.</summary>
        private void EnsureAuraTex()
        {
            if (_auraTex != null) return;
            const int N = 128;
            _auraTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
                    float d = SdRoundBox(p, new Vector2(0.60f, 0.60f), 0.34f); // <0 dentro, 0 borde
                    float ring = Mathf.Exp(-(d * d) / (2f * 0.17f * 0.17f));    // halo gaussiano en el borde
                    float inside = d < 0f ? 0.16f * Mathf.Clamp01(1f + d / 0.6f) : 0f;
                    float a = Mathf.Clamp01(ring * 0.95f + inside);
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            _auraTex.SetPixels(px);
            _auraTex.Apply();
        }

        private static float SdRoundBox(Vector2 p, Vector2 b, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - b.x + r, Mathf.Abs(p.y) - b.y + r);
            return Mathf.Min(Mathf.Max(q.x, q.y), 0f)
                   + new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - r;
        }

        /// <summary>Auras de las zonas válidas al arrastrar una carta (sustituye a los quads sólidos).</summary>
        private void DrawPlayAura(CardType type, int player)
        {
            if (Camera.main == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            if (type == CardType.Tierra)
            {
                var col = new Color(0.45f, 1f, 0.55f);
                for (int i = 0; i < 7; i++) DrawAura(ZoneRect(BoardLayout.Tierra(player, i)), col, pulse);
            }
            else if (type == CardType.Concepto)
            {
                DrawAura(ZoneRect(BoardLayout.Concepto(player)), new Color(0.82f, 0.55f, 1f), pulse);
            }
            else if (CardTypeNames.IsSer(type))
            {
                var col = new Color(0.5f, 0.75f, 1f);
                for (int i = 0; i < 3; i++) DrawAura(ZoneRect(BoardLayout.Ser(player, i)), col, pulse);
            }
        }

        // ---------------- Fase B: partida guiada y victoria garantizada ----------------

        /// <summary>Prepara el campo del jugador para el tutorial: 2 piezas TIERRA ya en campo,
        /// mano fija con las 3 piezas SER (+ 1 TIERRA y 1 CONCEPTO de muestra) y FD de sobra.</summary>
        private void SetupTutorialField(PlayerState p)
        {
            foreach (var c in p.Mano.Cards.ToList()) { p.Mano.Remove(c); p.Mazo.Add(c); } // limpiar mano

            foreach (var name in new[] { "El Jardín del Edén", "El Árbol del Fruto Prohibido" })
            {
                var c = PullFromMazo(p, name);
                if (c != null) { c.Tapped = false; c.TurnsLeftRemaining = c.Def.TurnsLeft ?? 0; p.Tierras.Add(c); }
            }

            foreach (var name in new[] { "Adán", "Eva", "La Serpiente" })
            {
                var c = PullFromMazo(p, name);
                if (c != null) p.Mano.Add(c);
            }
            var land = p.Mazo.Cards.FirstOrDefault(x => x.Type == CardType.Tierra);
            if (land != null) { p.Mazo.Remove(land); p.Mano.Add(land); }
            var conc = p.Mazo.Cards.FirstOrDefault(x => x.Type == CardType.Concepto);
            if (conc != null) { p.Mazo.Remove(conc); p.Mano.Add(conc); }

            p.Fd = 10; // suficiente para bajar Adán(3) + Eva(3) + Serpiente(2)
        }

        private static CardInstance? PullFromMazo(PlayerState p, string nombre)
        {
            var c = p.Mazo.Cards.FirstOrDefault(x => x.Nombre == nombre);
            if (c != null) p.Mazo.Remove(c);
            return c;
        }

        /// <summary>¿El jugador cumplió el paso guiado i?</summary>
        private bool GuideDone(int i)
        {
            var p = _engine.State.Players[0];
            return i switch
            {
                0 => p.Seres.Cards.Any(c => c.Nombre == "Adán"),
                1 => p.Seres.Cards.Any(c => c.Nombre == "Eva"),
                2 => p.Seres.Cards.Any(c => c.Nombre == "La Serpiente"),
                3 => _engine.State.IsOver,
                _ => false,
            };
        }

        private void GrantTutorialReward()
        {
            if (_rewardGiven) return;
            _rewardGiven = true;
            Game.Runtime.Menu.PlayerData.Monedas += 700;
            PlayerPrefs.SetInt("logro_primeros_pasos", 1);
            PlayerPrefs.SetInt("tutorial1_done", 1);
            PlayerPrefs.Save();
        }

        private void DrawTutorialWin()
        {
            GrantTutorialReward();
            const float w = 460f, h = 210f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), GUI.skin.box);
            var tt = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUILayout.Space(8);
            GUILayout.Label("¡VICTORIA!", tt);
            GUILayout.Label("Completaste tu Historia «La Caída del Edén».\nRecompensa: logro «Primeros Pasos» +700 monedas.",
                body, GUILayout.ExpandHeight(true));
            GUILayout.Space(6);
            if (GUILayout.Button("Volver al menú", GUILayout.Height(34)))
                _deferredResolve = () => { ReturnRequested = true; };
            GUILayout.Space(8);
            GUILayout.EndArea();
        }

        // --- cartas de respuesta (trampas) ---
        private CardInstance? _placeCard;          // CONCEPTO respuesta esperando colocación
        private bool _placePending;
        private CardInstance? _respTrap;           // trampa que el humano puede activar
        private string _respPrompt = "";
        private volatile bool _respPending;
        private bool _respShow;            // copia estable de _respPending para OnGUI
        private bool _respResult;
        private readonly System.Threading.ManualResetEventSlim _respDone = new(false);

        /// <summary>
        /// Ventana de respuesta (llamada por el motor en su hilo): el defensor puede activar una
        /// trampa boca abajo. La IA responde automáticamente; el humano vía modal Sí/No.
        /// </summary>
        private CardInstance? OnResponseWindow(int defender, CardInstance attacker, EffectCategory cat)
        {
            var dp = _engine.State.Players[defender];
            var trap = dp.Concepto.Cards.FirstOrDefault(c =>
                c.FaceDown && _engine.Effects.IsResponse(c.Def.Id) && dp.Fd >= (c.Def.Coste ?? 0)
                && ResponseRules.Applies(c.Def.Id, cat)); // la trampa debe aplicar a esa categoría
            if (trap == null) return null;

            if (defender == aiPlayer) return trap; // la IA responde si puede

            _respTrap = trap;
            _respPrompt = $"¿Activar {trap.Nombre}?  (anula el efecto de {attacker.Nombre}, -{trap.Def.Coste ?? 0} FD)";
            _respResult = false;
            _respDone.Reset();
            _respPending = true;
            _respDone.Wait();        // bloquea el hilo del motor hasta que OnGUI responda
            _respPending = false;
            return _respResult ? trap : null;
        }

        private void DrawPlacement()
        {
            const float w = 320f, h = 130f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), GUI.skin.box);
            GUILayout.Label($"{_placeCard!.Nombre} — carta de respuesta",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(6);
            if (GUILayout.Button("Jugar ya", GUILayout.Height(30)))
            {
                var c = _placeCard!; _placePending = false; _placeCard = null;
                _deferredResolve = () => RunHumanCommand(c.Nombre, () => _engine.PlayConcepto(c, faceDown: false));
            }
            if (GUILayout.Button("Boca abajo (trampa)", GUILayout.Height(30)))
            {
                var c = _placeCard!; _placePending = false; _placeCard = null;
                _deferredResolve = () => RunHumanCommand(c.Nombre, () => _engine.PlayConcepto(c, faceDown: true));
            }
            GUILayout.EndArea();
        }

        private void DrawResponsePrompt()
        {
            const float w = 380f, h = 120f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), GUI.skin.box);
            GUILayout.Label(_respPrompt,
                new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sí, activar", GUILayout.Height(30)))
                _deferredResolve = () => { _respResult = true; _respPending = false; _respDone.Set(); };
            if (GUILayout.Button("No", GUILayout.Height(30)))
                _deferredResolve = () => { _respResult = false; _respPending = false; _respDone.Set(); };
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private bool _showLog;            // minimizado por defecto (solo botón)
        private Vector2 _logScroll;
        private int _lastLogCount = -1;
        private GUIStyle _logStyle;
        private readonly List<string> _logView = new(); // copia estable del log para OnGUI

        /// <summary>Copia el final del log (en hilo principal) para que OnGUI no vea conteos cambiantes.</summary>
        private void SnapshotLog()
        {
            var log = _engine.State.Log;
            try
            {
                int n = log.Count;
                int start = Mathf.Max(0, n - 200);
                _logView.Clear();
                for (int i = start; i < n && i < log.Count; i++) _logView.Add(log[i]);
                if (n != _lastLogCount) { _lastLogCount = n; _logScroll.y = float.MaxValue; }
            }
            catch { /* el hilo de la IA reasignó la lista; se reintenta el próximo frame */ }
        }

        /// <summary>
        /// Historial: izquierda-centro (espejo del panel de fase/timer). Minimizado = un botón
        /// "HISTORIAL"; al pulsarlo se expande el log (State.Log) con auto-scroll y un botón
        /// "Minimizar" en la esquina para volver a botón.
        /// </summary>
        private void DrawHistory(GameState s)
        {
            if (!_showLog)
            {
                const float bw = 150f, bh = 42f;
                if (GUI.Button(new Rect(14f, (Screen.height - bh) * 0.5f, bw, bh), "HISTORIAL"))
                    _showLog = true;
                return;
            }

            const float w = 380f, h = 300f;
            GUILayout.BeginArea(new Rect(14f, (Screen.height - h) * 0.5f, w, h), GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("HISTORIAL", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Minimizar", GUILayout.Width(90)))
                _showLog = false;
            GUILayout.EndHorizontal();

            _logStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _logView.Count; i++) // snapshot estable (mismo conteo Layout/Repaint)
                GUILayout.Label(_logView[i], _logStyle);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawDecision(DecisionRequest req)
        {
            if (req != _decReq) { _decReq = req; _decSelected = null; _decOrder.Clear(); _decScroll = Vector2.zero; }

            const float thumbW = 84f, thumbH = 118f, gap = 8f;
            int visible = Mathf.Clamp(req.Options.Count, 1, 7);
            float w = visible * (thumbW + gap) + gap + 20f;
            float h = 30f + (thumbH + 40f) + 46f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), GUI.skin.box);

            var hdr = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label(req.Prompt, hdr);

            // Solo barra horizontal (vertical oculta con GUIStyle.none).
            _decScroll = GUILayout.BeginScrollView(_decScroll, true, false,
                GUI.skin.horizontalScrollbar, GUIStyle.none, GUI.skin.box, GUILayout.Height(thumbH + 40f));
            GUILayout.BeginHorizontal();
            var cap = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            foreach (var c in req.Options)
            {
                GUILayout.BeginVertical(GUILayout.Width(thumbW));
                bool sel = req.IsOrder ? _decOrder.Contains(c) : c == _decSelected;
                var prev = GUI.backgroundColor;
                if (sel) GUI.backgroundColor = new Color(0.45f, 0.8f, 1f);
                var tex = _art.Front(c.Nombre);
                var content = tex != null ? new GUIContent(tex) : new GUIContent(Trunc(c.Nombre, 12));
                if (GUILayout.Button(content, GUILayout.Width(thumbW), GUILayout.Height(thumbH)))
                    OnDecCardClick(req, c);
                GUI.backgroundColor = prev;
                string mark = req.IsOrder
                    ? (_decOrder.Contains(c) ? (_decOrder.IndexOf(c) + 1).ToString() : "·")
                    : (c == _decSelected ? "✓" : " ");
                GUILayout.Label($"{mark} {Trunc(c.Nombre, 11)}", cap);
                GUILayout.EndVertical();
                GUILayout.Space(gap);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            bool can = req.IsOrder ? _decOrder.Count == req.Options.Count : (_decSelected != null || req.Optional);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = can;
            if (GUILayout.Button("Aceptar", GUILayout.Width(150), GUILayout.Height(28)))
            {
                // Diferir el resolve a Update: cambiarlo dentro de OnGUI rompe el GUILayout.
                if (req.IsOrder)
                {
                    var order = new List<CardInstance>(_decOrder);
                    _deferredResolve = () => _decisions.ResolveOrder(req, order);
                }
                else
                {
                    var pick = _decSelected;
                    _deferredResolve = () => _decisions.Resolve(req, pick);
                }
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void OnDecCardClick(DecisionRequest req, CardInstance c)
        {
            if (req.IsOrder)
            {
                if (!_decOrder.Remove(c)) _decOrder.Add(c); // clic alterna en el orden
            }
            else _decSelected = c;
        }

        private static string Trunc(string s, int n) => s.Length > n ? s.Substring(0, n - 1) + "…" : s;

        private static string PhaseName(Phase ph) => ph switch
        {
            Phase.Preludio => "PRELUDIO",
            Phase.Genesis => "GÉNESIS",
            Phase.Preparacion => "PREPARACIÓN",
            Phase.Entrega => "ENTREGA",
            _ => ph.ToString().ToUpperInvariant()
        };

        private string WhoseTurn(GameState s)
        {
            if (s.ActivePlayer == aiPlayer) return "Turno IA";
            return s.ActivePlayer == 0 ? "Tu Turno" : "Turno Rival";
        }

        private string TimerStr()
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(_turnTimer));
            return $"{t / 60}:{t % 60:00}";
        }

        private void TickTimer()
        {
            var s = _engine.State;
            if (s.TurnNumber != _timerTurn) { _timerTurn = s.TurnNumber; _turnTimer = turnSeconds; }
            if (s.IsOver) return;
            bool humanTurn = aiPlayer < 0 || s.ActivePlayer != aiPlayer;
            if (!humanTurn) return; // la IA no consume el reloj
            _turnTimer -= Time.deltaTime;
            if (_turnTimer <= 0f && !_aiRunning && !_busy)
            {
                _turnTimer = 0f;
                RunHumanCommand("Tiempo agotado", () => _engine.EndTurn());
            }
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

            GUILayout.BeginArea(new Rect(10, 10, 540, 320), GUI.skin.box);
            DrawDetailBody(cv.Card, cv.OwnerId);
            DrawActivationHint(cv);
            GUILayout.EndArea();
        }

        /// <summary>Detalle de una carta concreta (usado también para la carta elegida en el menú).</summary>
        private void DrawCardDetailFor(CardInstance card, int owner)
        {
            _wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.BeginArea(new Rect(Screen.width - 550f, 10f, 540f, 320f), GUI.skin.box); // arriba a la derecha
            DrawDetailBody(card, owner);
            GUILayout.EndArea();
        }

        private void DrawDetailBody(CardInstance card, int owner)
        {
            _wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            var d = card.Def;

            GUILayout.BeginHorizontal();

            // Columna izquierda: imagen.
            var art = _art.Front(d.Nombre);
            if (art != null)
                GUILayout.Label(art, GUILayout.Width(195), GUILayout.Height(278)); // +30%

            // Columna derecha: nombre, datos y efectos.
            GUILayout.BeginVertical();
            GUILayout.Label(d.Nombre, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true });
            GUILayout.Label($"Tipo: {d.Type}");
            if (d.Coste.HasValue) GUILayout.Label($"Coste FD: {d.Coste}");
            if (d.Fd.HasValue) GUILayout.Label($"Genera FD: {d.Fd}");
            if (CardTypeNames.IsSer(d.Type))
                GUILayout.Label($"Duración: {card.DurLeft} (base {d.Dur}) · actCost {d.ActCost}");
            if (!string.IsNullOrEmpty(d.TipoConcepto)) GUILayout.Label($"Concepto: {d.TipoConcepto}");

            if (!string.IsNullOrEmpty(d.Condicion)) GUILayout.Label($"Condición: {d.Condicion}", _wrap);
            if (!string.IsNullOrEmpty(d.AlEntrar)) GUILayout.Label($"Al entrar: {d.AlEntrar}", _wrap);
            if (!string.IsNullOrEmpty(d.Activado)) GUILayout.Label($"Activado: {d.Activado}", _wrap);
            if (!string.IsNullOrEmpty(d.AlSalir)) GUILayout.Label($"Al salir: {d.AlSalir}", _wrap);
            if (!string.IsNullOrEmpty(d.Efecto)) GUILayout.Label($"Efecto: {d.Efecto}", _wrap);

            DrawHistoriaPieces(d, owner);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        /// <summary>HISTORIA: lista sus 5 piezas con ✓ si ya están en campo del dueño.</summary>
        private void DrawHistoriaPieces(CardDefinition d, int owner)
        {
            if (d.Type != CardType.Historia) return;
            var h = _engine.Catalog.FindHistoria(d.Id);
            if (h == null) return;
            var ps = _engine.State.Players[owner];
            GUILayout.Label("Piezas necesarias:");
            foreach (var pieza in h.Piezas)
            {
                bool en = ps.Tierras.Cards.Any(c => c.Nombre == pieza)
                       || ps.Seres.Cards.Any(c => c.Nombre == pieza);
                GUILayout.Label($"  {(en ? "✓" : "•")} {pieza}", _wrap);
            }
        }

        /// <summary>Indica si la carta puede activarse ahora (SER/DÍA del jugador activo).</summary>
        private void DrawActivationHint(CardView cv)
        {
            var s = _engine.State;
            if (cv.OwnerId != s.ActivePlayer) return;
            var p = s.Active;
            var card = cv.Card;

            if (p.Seres.Cards.Contains(card))
            {
                int cost = card.Def.ActCost ?? 0;
                if (p.SeresActivatedThisTurn.Contains(card.InstanceId))
                    GUILayout.Label(">> Efecto ya usado este turno");
                else if (p.Fd >= cost)
                    GUILayout.Label($">> Clic para activar (cuesta {cost} FD)");
                else
                    GUILayout.Label($">> FD insuficiente ({p.Fd}/{cost})");
            }
            else if (card.Type == CardType.Dia && PlayerState.DiaNumero(card) == p.DiaActual)
            {
                GUILayout.Label(DiaConditions.Met(p, p.DiaActual)
                    ? ">> Condición cumplida — clic para activar"
                    : ">> Condición NO cumplida");
            }
            else if (cv.Playable)
            {
                GUILayout.Label(">> Arrastra al campo para jugar");
            }
        }
    }
}
