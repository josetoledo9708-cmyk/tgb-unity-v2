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
using Game.Runtime.Net;

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
        [SerializeField] private ulong seed = 0; // 0 = semilla aleatoria por partida (ver InitMatch)
        [SerializeField] private int aiPlayer = 1; // jugador controlado por IA (-1 = ninguno)
        [SerializeField] private float turnSeconds = 180f; // temporizador por turno
        private bool _aiRunning;
        private float _turnTimer;
        private double _turnDeadline;   // hora (reloj real) en que se acaba el turno
        private int _timerTurn = -1;

        /// <summary>Segundos de reloj REAL desde el arranque. Sigue avanzando con el juego pausado
        /// o sin foco, a diferencia de Time.time / Time.deltaTime.</summary>
        private static double RealTime => System.Diagnostics.Stopwatch.GetTimestamp()
                                          / (double)System.Diagnostics.Stopwatch.Frequency;

        // --- Tutorial (partida-tutorial #1): IA pasiva + explicaciones paso a paso ---
        private bool _tutorial;
        private int _tutIdx;
        private Texture2D? _auraTex;    // aura suave redondeada para resaltar
        private Rect _phaseRect;        // rect del panel de fase (para resaltarlo)
        private bool _gameOver;         // latch por-frame de State.IsOver (evita carrera Layout/Repaint)
        private int _gameWinner = -1;   // latch por-frame del ganador

        // Qué zona/carta iluminar en cada paso del tutorial.
        private enum TutHL { None, Tierra, Ser, Concepto, Dia, Historia, HandTierra, HandSer, HandConcepto, Phase, Fd, TapTierra }

        private static readonly (string msg, TutHL hl)[] TutSteps =
        {
            ("Bienvenido al campo. Aquí se libran las Historias de la Biblia. Te enseño tus zonas, tus cartas y cómo se gana antes de jugar.", TutHL.None),
            ("Estas 7 ranuras son de TIERRA. Las TIERRAs son tu base: colocas una por turno y, al taparlas, generan FD (tu recurso).", TutHL.Tierra),
            ("Estas 3 ranuras son de SER (personajes/criaturas). Se bajan pagando su coste en FD y actúan en tu campo.", TutHL.Ser),
            ("Esta ranura es de CONCEPTO: cartas de efecto puntual. Puedes jugarlas al momento, o dejarlas boca abajo para activarlas después.", TutHL.Concepto),
            ("Esta es la zona de DÍA (1→7). Los DÍAs marcan el avance; cada DÍA activado puede darte una recompensa, y llegar al DÍA 7 ES una victoria.", TutHL.Dia),
            ("Esta es la zona de HISTORIA: tu otra vía de victoria. Reúne sus 5 piezas en tu campo para ganar.", TutHL.Historia),
            ("El FD es tu recurso para jugar cartas. Aquí ves tu FD actual. En una partida NORMAL empieza en 0 y se reinicia a 0 al comenzar cada turno, hasta que giras una TIERRA. (Solo para este tutorial te regalo 10 FD, pero recuerda: normalmente empiezas en 0).", TutHL.Fd),
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
        private CardInstance? _playInfoCard; // carta recién jugada cuyo efecto se está explicando
        private bool _awaitInfoAck;          // esperando "Entendido" tras jugar una pieza

        // --- Multijugador en red (LAN) ---
        private bool _net;          // partida en red (lockstep de comandos)
        private int _netMyPlayer;   // 0 host, 1 cliente
        private int LocalPlayer => _net ? _netMyPlayer : 0; // lado propio (mano visible / input)

        /// <summary>Rol VISUAL de un jugador de motor para BoardLayout: 0 = mi lado (cerca de cámara),
        /// 1 = lado rival (lejos). Así, en red, el cliente (P1 de motor) también ve SU lado abajo,
        /// sin tocar la cámara (que rotarla 180° voltea el fondo y el arte de las cartas).</summary>
        private int Vis(int enginePlayer) => enginePlayer == LocalPlayer ? 0 : 1;

        // --- Tutorial #2: IA activa que pierde por deck-out; explica mecánicas al usarlas ---
        private bool _tutorial2;
        private bool _reward2Given;
        private readonly HashSet<string> _t2Explained = new();
        private string _t2Info = "";
        private bool _t2Ack;                 // popup de explicación abierto (pausa a la IA)
        public static bool ReturnRequested; // el menú (MenuShell) lo detecta para volver al MainMenu

        private static readonly (string msg, TutHL hl)[] GuideSteps =
        {
            ("Practiquemos el FD: haz clic en la TIERRA resaltada para taparla. Mira cómo tu FD (izquierda) sube al hacerlo.", TutHL.TapTierra),
            ("¡Así se genera FD! Tienes las 2 primeras piezas (TIERRA) en el campo. Ahora arrastra a ADÁN a una ranura de SER.", TutHL.HandSer),
            ("¡Bien! Ahora arrastra a EVA a otra ranura de SER.", TutHL.HandSer),
            ("Ya casi. Arrastra a LA SERPIENTE a la última ranura de SER.", TutHL.HandSer),
            ("¡HISTORIA COMPLETADA! Colocaste sus 5 piezas en el campo (2 TIERRAs + 3 SERes). Al TERMINAR tu turno con SIGUIENTE FASE, en la fase de ENTREGA se comprobará y GANARÁS la partida.", TutHL.Phase),
        };

        private readonly RuntimeDecisionProvider _decisions = new();
        private AutoDecisionProvider _auto = new();
        private bool _busy;
        private float _netBusySince = -1f; // red: momento en que se envió el comando (para soltar el bloqueo si se pierde)
        private System.Threading.Tasks.Task? _cmdTask;
        private CommandResult _cmdResult;
        private string _cmdName = "";

        private DecisionRequest? _decReq;
        private DecisionRequest? _decView;   // latch por-frame de _decisions.Pending (lo escribe el hilo de comando)
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

        private bool _needInit;

        /// <summary>El menú llama a esto para arrancar una PARTIDA NUEVA (no al reanudar tras pausa).</summary>
        public void NewMatch() => _needInit = true;

        // El campo se enciende/apaga con `enabled` desde el menú. Solo se re-inicializa cuando el menú
        // pidió una partida nueva (NewMatch). Al REANUDAR (cerrar Opciones) se conserva la partida; si
        // era turno de la IA, se reanuda su turno.
        private void OnEnable()
        {
            if (!_needInit && _engine != null) MaybeRunAI(); // reanudar: recuperar turno de IA si aplica
        }

        private void OnDisable()
        {
            StopAllCoroutines();   // pausa: corta turnos de IA en curso
            _aiRunning = false;    // permite reanudar el turno de IA al volver
            NetPlay.PeerDisconnected -= OnPeerDisconnected;
        }

        /// <summary>El rival se desconectó a media partida (red): gano yo por abandono.</summary>
        private void OnPeerDisconnected()
        {
            if (_engine == null || _engine.State.IsOver) return;
            _engine.State.DeclareWinner(LocalPlayer, VictoryId.Abandono);
        }

        private void InitMatch()
        {
            CleanupPreviousMatch();

            EnsureSceneRig();

            var catalog = UnityCatalogLoader.LoadDefault();
            if (catalog == null) { Debug.LogError("Falta catálogo (Resources/catalogo_v3 o StreamingAssets)."); return; }

            _art = new CardArtLibrary(artFolder);
            if (!_art.Available)
                Debug.LogWarning($"Sin arte de cartas en '{artFolder}' (se usan quads de color).");

            _auto = new AutoDecisionProvider(catalog); // IA consciente de sus piezas al usar tutores
            _engine = new GameEngine(catalog) { Effects = CardEffects.BuildResolver(), Decisions = _auto };
            _engine.ResponseWindow = OnResponseWindow; // permite activar trampas en el turno rival
            _engine.CardRevealed = OnCardRevealed;     // cartas públicas (buscadas del mazo): las ve el rival

            // Modo RED: misma semilla en ambos lados (motor determinista), sin IA.
            _net = NetPlay.Active;
            if (_net) { _netMyPlayer = NetPlay.MyPlayer; aiPlayer = -1; }
            // Semilla: en red la fija el host (ambos motores deben coincidir). En local, una semilla
            // NUEVA por partida para que el mazo se baraje distinto cada vez; `seed` en el Inspector
            // solo se respeta si se le pone un valor != 0 (útil para reproducir un bug concreto).
            ulong matchSeed = _net ? NetPlay.Seed
                                   : (seed != 0 ? seed : (ulong)System.DateTime.UtcNow.Ticks);
            NetPlay.PeerDisconnected -= OnPeerDisconnected;
            if (_net) NetPlay.PeerDisconnected += OnPeerDisconnected;

            // Mazo del jugador: el ELEGIDO en el menú si lo hay; si no, el mazo por defecto de la
            // historia. (En red no se usa la selección local: ambos lados deben construir lo mismo.)
            string hP0 = (!_net && !string.IsNullOrEmpty(Game.Runtime.Menu.PlayerData.SelectedHistoriaId))
                ? Game.Runtime.Menu.PlayerData.SelectedHistoriaId : historiaP0;
            var deckP0 = (!_net && Game.Runtime.Menu.PlayerData.SelectedDeck != null && Game.Runtime.Menu.PlayerData.SelectedDeck.Count > 0)
                ? new DeckDefinition(Game.Runtime.Menu.PlayerData.SelectedDeck.ToList(), hP0)
                : SampleDeckBuilder.Build(catalog, hP0, 40, pieceCopies: 3, variant: 0);

            // Rival: la variante B de su historia, para que una misma partida enfrente dos
            // conjuntos de cartas distintos y salgan a la luz más interacciones (v0.01, pruebas).
            var deckP1 = SampleDeckBuilder.Build(catalog, historiaP1, 40, pieceCopies: 3, variant: 1);

            _engine.StartGame(deckP0, deckP1, matchSeed, firstPlayer: 0);

            if (!_net) EnsureResponseInHand(_engine.State.Players[0]); // (determinismo en red: no alterar la mano)

            _tutorial = !_net && PlayerPrefs.GetInt("tutorial_match", 0) == 1;
            if (_tutorial)
            {
                PlayerPrefs.SetInt("tutorial_match", 0); PlayerPrefs.Save();
                _tutIdx = 0; _guideStep = -1; _rewardGiven = false; ReturnRequested = false;
                _awaitInfoAck = false; _playInfoCard = null;
                SetupTutorialField(_engine.State.Players[0]); // mano fija + piezas preparadas para ganar
            }

            _tutorial2 = !_net && PlayerPrefs.GetInt("tutorial2_match", 0) == 1;
            if (_tutorial2)
            {
                PlayerPrefs.SetInt("tutorial2_match", 0); PlayerPrefs.Save();
                _reward2Given = false; _t2Explained.Clear(); ReturnRequested = false;
                SetupTutorial2(_engine.State.Players[1]); // rival no puede completar su Historia + demuestra mecánicas
                _t2Info = T2Intro; _t2Ack = true; // mensaje inicial de objetivo/aviso
            }

            MatchSnapshot.Describe = DescribeBoard; // costura de pruebas (la consume quien quiera)

            _status = "Partida iniciada.";
            BuildBoard();
            Rebuild();
            MaybeRunAI();
        }

        /// <summary>Limpia por completo la partida anterior antes de montar una nueva (el board se
        /// re-enciende con `enabled`, pero Start no vuelve a correr).</summary>
        private void CleanupPreviousMatch()
        {
            StopAllCoroutines(); // corta cualquier turno de IA en curso

            if (_board != null) { Destroy(_board.gameObject); _board = null!; }
            foreach (var v in _spawned) if (v != null) Destroy(v.gameObject);
            _spawned.Clear();

            _hovered = null; _drag = null;
            _prevHandIds.Clear(); _wasTapped.Clear();
            for (int p = 0; p < 2; p++)
            {
                _serSlot[p].Clear(); _tierraSlot[p].Clear();
                _glowTierra[p].Clear(); _glowSer[p].Clear(); _glowConcepto[p] = null;
            }

            _busy = false; _netBusySince = -1f; _aiRunning = false; _cmdTask = null;
            _decView = null; _decReq = null; _decSelected = null; _decOrder.Clear();
            _deferredResolve = null;
            _placePending = false; _placeCard = null; _respShow = false; _respPending = false; _respTrap = null;
            _showLog = false; _lastLogCount = -1; _logView.Clear();
            _gameOver = false; _gameWinner = -1; _timerTurn = -1;
            _retiradosOpen = -1; _revealCard = null;

            _net = false; aiPlayer = 1; // por defecto: partida local con IA en P1 (se re-decide abajo)

            // Estado de tutoriales (se re-decide abajo según los flags).
            _tutorial = false; _tutorial2 = false;
            _tutIdx = 0; _guideStep = -1; _awaitInfoAck = false; _playInfoCard = null;
            _rewardGiven = false; _reward2Given = false;
            _t2Ack = false; _t2Explained.Clear();
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
            yield return new WaitForSeconds(_tutorial2 ? 1.0f : 0.6f);

            if (_tutorial2)
            {
                // Tutorial #2: IA guionizada en el hilo principal (para pausar y explicar mecánicas).
                yield return T2AiTurn(); // juega y termina su turno ella misma
            }
            else
            {
                // En hilo: así OnGUI sigue corriendo y puedes responder con una trampa.
                // En el tutorial #1 la IA es PASIVA (no juega): solo pasa el turno.
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
            }

            _aiRunning = false;
            MaybeRunAI(); // por si el siguiente turno también es IA
        }

        private void Update()
        {
            if (_needInit) { _needInit = false; InitMatch(); } // arranque diferido (flags de tutorial ya escritos)
            if (_engine == null) return;

            // Resolver decisión (Aceptar) fuera del ciclo OnGUI para no romper el GUILayout.
            if (_deferredResolve != null) { var a = _deferredResolve; _deferredResolve = null; a(); }

            // RED: aplicar los comandos ya ordenados por el servidor (mismo orden en ambos lados).
            if (_net)
                while (NetPlay.Incoming.Count > 0) ApplyNetCommand(NetPlay.Incoming.Dequeue());

            // Latch estable por-frame de fin de partida: OnGUI (Layout y Repaint son eventos
            // distintos) debe ver el MISMO valor aunque el hilo de comandos cambie IsOver en medio.
            _gameOver = _engine.State.IsOver;
            _gameWinner = _engine.State.Winner ?? -1;
            _decView = _decisions.Pending; // estable durante Layout y Repaint del mismo frame

            // Fase B (guiada): avanzar el paso cuando el jugador cumple lo pedido.
            if (_tutorial && _tutIdx >= TutSteps.Length)
            {
                if (_guideStep < 0) _guideStep = 0;
                else if (_guideStep < GuideSteps.Length && !_awaitInfoAck && !_busy
                         && _decisions.Pending == null && GuideDone(_guideStep))
                {
                    // Al jugar una pieza (SER), pausar para explicar su efecto antes de seguir.
                    bool serStep = _guideStep >= 1 && _guideStep <= 3;
                    var just = serStep ? _engine.State.Players[0].Seres.Cards.FirstOrDefault(c => c.Nombre == ExpectedGuideCard()) : null;
                    if (just != null) { _playInfoCard = just; _awaitInfoAck = true; }
                    else _guideStep++;
                }
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
                // Red: si el comando enviado no vuelve (paquete perdido/rechazado), soltar el bloqueo
                // en vez de dejar el campo injugable para siempre.
                else if (_netBusySince >= 0f && Time.unscaledTime - _netBusySince > 3f)
                {
                    _busy = false; _netBusySince = -1f;
                    _status = "Sin respuesta del rival; intenta de nuevo.";
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

            if (!_engine.State.IsOver && _retiradosOpen < 0 && Input.GetMouseButtonDown(0) && hit != null)
                OnPointerDown(hit);
        }

        private void OnPointerDown(CardView cv)
        {
            var s = _engine.State;

            // Zona RETIRADOS: no se juega, se consulta. Abre la lista completa de esa zona.
            if (cv.Card != null && s.Players[cv.OwnerId].Retirados.Cards.Contains(cv.Card))
            {
                _retiradosOpen = cv.OwnerId;
                return;
            }

            if (_net && s.ActivePlayer != _netMyPlayer) return; // red: solo actúas en tu turno
            if (TutBlocksPlay(cv.Card)) { _status = TutBlockMsg(); return; } // tutorial: solo la carta guiada

            // Carta jugable de tu mano (visible) -> arrastrar; resto (tapear TIERRA) -> clic.
            if (cv.OwnerId == s.ActivePlayer && !cv.FaceDown && s.Active.Mano.Cards.Contains(cv.Card) && IsPlayable(s.Active, cv.Card))
                BeginDrag(cv);
            else
                OnCardClicked(cv);
        }

        /// <summary>Tutorial: en la explicación no se juega nada; en la fase guiada solo la acción esperada.</summary>
        private bool TutBlocksPlay(CardInstance? card)
        {
            if (!_tutorial || card == null) return false;
            if (_tutIdx < TutSteps.Length) return true;        // Fase A: bloquear todo
            var p = _engine.State.Players[0];
            if (_guideStep == 0)                                // paso tapear: solo TIERRA propia en campo
                return !(card.Type == CardType.Tierra && p.Tierras.Cards.Contains(card));
            return card.Nombre != ExpectedGuideCard();          // pasos SER: solo la pieza esperada
        }

        private string? ExpectedGuideCard() => _guideStep switch
        {
            1 => "Adán",
            2 => "Eva",
            3 => "La Serpiente",
            _ => null,
        };

        private string TutBlockMsg()
        {
            if (_tutIdx < TutSteps.Length) return "Sigue el tutorial: pulsa Siguiente.";
            if (_guideStep == 0) return "Haz clic en la TIERRA resaltada para generar FD.";
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
                Act(card.Type == CardType.Tierra ? NetCmd.PlayTierra : NetCmd.PlaySer, card);
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
                Act(NetCmd.PlayConcepto, card, 0);
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

        /// <summary>Punto único de acción del jugador: en red envía el comando (lockstep); local lo ejecuta.</summary>
        private void Act(NetCmd cmd, CardInstance card, byte flag = 0)
        {
            if (_net)
            {
                if (_busy) return; // ya hay un comando en camino (ida y vuelta por red); evita jugar 2 a la vez
                if (_engine.State.ActivePlayer != _netMyPlayer) return; // solo en tu turno
                _busy = true; _netBusySince = Time.unscaledTime;
                NetPlay.Submit?.Invoke(new NetCommand(cmd, card?.InstanceId ?? 0, flag));
                return;
            }
            string name = card?.Nombre ?? "Acción";
            switch (cmd)
            {
                case NetCmd.PlayTierra: RunHumanCommand(name, () => _engine.PlayTierra(card)); break;
                case NetCmd.TapTierra: RunHumanCommand(name, () => _engine.TapTierra(card)); break;
                case NetCmd.PlaySer: RunHumanCommand(name, () => _engine.PlaySer(card)); break;
                case NetCmd.PlayConcepto: RunHumanCommand(name, () => _engine.PlayConcepto(card, flag == 1)); break;
                case NetCmd.ActivateDia: RunHumanCommand("DÍA", () => _engine.ActivateDia(false)); break;
                case NetCmd.ActivateSer: RunHumanCommand(name, () => _engine.ActivateSerEffect(card)); break;
                case NetCmd.ActivateTrap: RunHumanCommand(name, () => _engine.ActivateTrap(card)); break;
                case NetCmd.EndTurn: RunHumanCommand("Terminar turno", () => _engine.EndTurn()); break;
            }
        }

        /// <summary>Aplica en el motor un comando recibido por la red (ambos lados, mismo orden).</summary>
        private void ApplyNetCommand(NetCommand c)
        {
            _engine.Decisions = _auto; // decisiones deterministas en red
            var card = c.InstanceId != 0 ? FindInstance(c.InstanceId) : null;
            try
            {
                switch (c.Cmd)
                {
                    case NetCmd.PlayTierra: if (card != null) _engine.PlayTierra(card); break;
                    case NetCmd.TapTierra: if (card != null) _engine.TapTierra(card); break;
                    case NetCmd.PlaySer: if (card != null) _engine.PlaySer(card); break;
                    case NetCmd.PlayConcepto: if (card != null) _engine.PlayConcepto(card, c.Flag == 1); break;
                    case NetCmd.ActivateDia: _engine.ActivateDia(false); break;
                    case NetCmd.ActivateSer: if (card != null) _engine.ActivateSerEffect(card); break;
                    case NetCmd.ActivateTrap: if (card != null) _engine.ActivateTrap(card); break;
                    case NetCmd.EndTurn: _engine.EndTurn(); break;
                }
            }
            catch (System.Exception e) { Debug.LogError(e); }
            _busy = false; _netBusySince = -1f; // desbloquea el envío de un nuevo comando (ver Act)
            Rebuild();
        }

        private CardInstance? FindInstance(int id)
        {
            foreach (var pl in _engine.State.Players)
                foreach (var z in new[] { pl.Mano, pl.Tierras, pl.Seres, pl.Concepto, pl.PilaDia, pl.Retirados, pl.Mazo })
                    for (int i = 0; i < z.Cards.Count; i++)
                        if (z.Cards[i].InstanceId == id) return z.Cards[i];
            return null;
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

            NetCmd? act = null;
            if (p.Concepto.Cards.Contains(card) && card.FaceDown) act = NetCmd.ActivateTrap; // trampa propia -> activarla
            else if (p.Tierras.Cards.Contains(card) && !card.Tapped) act = NetCmd.TapTierra; // tapear TIERRA -> FD
            else if (p.Seres.Cards.Contains(card)) act = NetCmd.ActivateSer;                  // efecto ACTIVADO del SER
            else if (card.Type == CardType.Dia) act = NetCmd.ActivateDia;                     // activar el DÍA actual
            else if (p.Mano.Cards.Contains(card))                                             // mano no arrastrable -> jugar
            {
                if (card.Type == CardType.Concepto) { PlayOrPlaceConcepto(card); return; }
                act = card.Type == CardType.Tierra ? NetCmd.PlayTierra : NetCmd.PlaySer;
            }

            if (act.HasValue) Act(act.Value, card);
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
                bool hideHand = p != LocalPlayer; // ocultar la mano del rival (en red, la del oponente)

                for (int i = 0; i < ps.Mano.Count; i++)
                {
                    var card = ps.Mano.Cards[i];
                    curHandIds.Add(card.InstanceId);
                    bool playable = p == s.ActivePlayer && !s.IsOver && IsPlayable(ps, card);
                    var (fpos, frot) = BoardLayout.HandFan(Vis(p), i, ps.Mano.Count);
                    // Mano rival gira 180° (espejo) -> no necesita flip de textura.
                    var v = Spawn(card, p, fpos, hideHand, playable, frot, flipTexture: Vis(p) == 0);
                    // Carta recién robada/añadida: animarla desde el mazo.
                    if (!_prevHandIds.Contains(card.InstanceId))
                        StartCoroutine(Deal(v.transform, BoardLayout.Mazo(Vis(p)) + Vector3.up * 0.3f, fpos));
                }

                var tSlots = AssignSlots(_tierraSlot[p], ps.Tierras.Cards, 7);
                foreach (var t in ps.Tierras.Cards)
                {
                    var tv = Spawn(t, p, BoardLayout.Tierra(Vis(p), tSlots[t.InstanceId]), false);
                    tv.SetTapped(t.Tapped, animate: t.Tapped && !_wasTapped.Contains(t.InstanceId));
                }

                // Mazo: montón de reversos escalonados (parece pila de cartas).
                if (ps.Mazo.Count > 0)
                {
                    int stack = Mathf.Clamp(ps.Mazo.Count, 1, 12);
                    for (int i = 0; i < stack; i++)
                        Spawn(ps.Mazo.Top!, p, BoardLayout.Mazo(Vis(p)) + Vector3.up * (i * 0.035f), faceDown: true);
                }

                // Retirados: la última carta que llegó, boca arriba.
                if (ps.Retirados.Count > 0)
                {
                    var top = ps.Retirados.Cards[ps.Retirados.Count - 1];
                    Spawn(top, p, BoardLayout.Descarte(Vis(p)) + Vector3.up * 0.02f, faceDown: false);
                }

                var sSlots = AssignSlots(_serSlot[p], ps.Seres.Cards, 3);
                foreach (var ser in ps.Seres.Cards)
                    Spawn(ser, p, BoardLayout.Ser(Vis(p), sSlots[ser.InstanceId]), false);

                if (ps.Concepto.Top != null)
                    Spawn(ps.Concepto.Top, p, BoardLayout.Concepto(Vis(p)), ps.Concepto.Top.FaceDown);

                if (ps.Historia != null) // HISTORIA se muestra en horizontal (girada 90°)
                    Spawn(ps.Historia, p, BoardLayout.Historia(Vis(p)), false, false, Quaternion.Euler(0f, 90f, 0f));

                var diaTop = ps.PilaDia.Cards.FirstOrDefault(d => PlayerState.DiaNumero(d) == ps.DiaActual);
                if (diaTop != null)
                    Spawn(diaTop, p, BoardLayout.Dia(Vis(p)), false);
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
            cam.transform.rotation = Quaternion.Euler(camRotation); // pose exacta, SIEMPRE la misma
            // Nota: el lado "mío abajo" del cliente en red se logra con Vis() (posiciones), no rotando
            // la cámara — rotarla voltearía el fondo del tablero y el arte de las cartas (texto de cabeza).
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
                int vp = Vis(p);
                for (int i = 0; i < 7; i++) Pad(BoardLayout.Tierra(vp, i), pad);
                ZoneLabel("TIERRAS", BoardLayout.Tierra(vp, 3), 40, 0.11f, label);

                var mp = BoardLayout.Mazo(vp);
                Pad(mp, pad);
                _mazoCount[p] = ZoneLabel("MAZO", mp + new Vector3(Mathf.Sign(mp.x) * 1.4f, 0f, 0f), 30, 0.08f, gold);

                var dp = BoardLayout.Descarte(vp);
                Pad(dp, pad);
                _retirCount[p] = ZoneLabel("RETIR.", dp + new Vector3(Mathf.Sign(dp.x) * 1.4f, 0f, 0f), 30, 0.08f, label);

                Pad(BoardLayout.Concepto(vp), pad);
                ZoneLabel("CONC.", BoardLayout.Concepto(vp), 32, 0.09f, label);

                for (int si = 0; si < 3; si++)
                {
                    Pad(BoardLayout.Ser(vp, si), pad);
                    ZoneLabel("SER", BoardLayout.Ser(vp, si), 34, 0.10f, label);
                }

                Pad(BoardLayout.Dia(vp), pad);
                ZoneLabel("DÍA", BoardLayout.Dia(vp), 34, 0.10f, label);

                Pad(BoardLayout.Historia(vp), pad);
                ZoneLabel("HISTORIA", BoardLayout.Historia(vp), 28, 0.075f, label);
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
            var sh = CardView.SafeShader(); if (sh != null) m.shader = sh; // evita magenta en Android
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
                int vp = Vis(p);
                var mz = BoardLayout.Mazo(vp);
                // FD al flanco OPUESTO del mazo, misma fila.
                var fdPos = new Vector3(Mathf.Sign(-mz.x) * 9f, 0f, mz.z);
                _fdLabel[p] = ZoneLabel("FD", fdPos, 42, 0.13f, gold);

                // Contador de mano al costado de la mano (oculto hasta hover).
                float handZ = (vp == 0 ? -1f : 1f) * BoardLayout.HandZ;
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
                int vp = Vis(p);
                for (int i = 0; i < 7; i++) _glowTierra[p].Add(Glow(BoardLayout.Tierra(vp, i), gT));
                for (int s = 0; s < 3; s++) _glowSer[p].Add(Glow(BoardLayout.Ser(vp, s), gS));
                _glowConcepto[p] = Glow(BoardLayout.Concepto(vp), gC);
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
            var sh = CardView.SafeShader(); if (sh != null) m.shader = sh; // evita magenta en Android
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

        // Escala del HUD IMGUI (más grande en móvil: pantallas pequeñas de alta densidad).
        private static float GuiScale => Application.platform == RuntimePlatform.Android ? 1.7f : 1f;
        private float _gs = 1f, _gw, _gh; // escala + ancho/alto "lógicos" (Screen / escala)

        private void OnGUI()
        {
            if (_engine == null) return;
            var s = _engine.State;

            _gs = GuiScale;
            GUI.matrix = _gs == 1f ? Matrix4x4.identity : Matrix4x4.Scale(new Vector3(_gs, _gs, 1f));
            _gw = Screen.width / _gs; _gh = Screen.height / _gs; // todo el HUD trabaja en coords lógicas

            var center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            var title = new GUIStyle(center) { fontSize = 13 };
            var big = new GUIStyle(center) { fontSize = 22, fontStyle = FontStyle.Bold };
            var sub = new GUIStyle(center) { fontSize = 15 };
            var timer = new GUIStyle(big) { fontSize = 20 };
            timer.normal.textColor = new Color(0.45f, 0.6f, 1f);

            float pw = 200f, ph = 200f;
            _phaseRect = new Rect(_gw - pw - 14f, (_gh - ph) * 0.5f, pw, ph);
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
                bool netOk = !_net || s.ActivePlayer == _netMyPlayer; // en red, solo en tu turno
                GUI.enabled = humanTurn && !_aiRunning && !_busy && tutOk && netOk;
                if (GUILayout.Button("SIGUIENTE FASE", GUILayout.Height(28)))
                    Act(NetCmd.EndTurn, null);
                GUI.enabled = true;
            }
            GUILayout.Label(_status, center);
            GUILayout.EndArea();

            if (_tutorial && _tutIdx < TutSteps.Length)
            {
                DrawTutHighlight(TutSteps[_tutIdx].hl); // ilumina la zona/carta mencionada

                float tw = 700f, th = 150f;
                GUILayout.BeginArea(new Rect((_gw - tw) * 0.5f, 18f, tw, th), GUI.skin.box); // arriba: no tapa mano/zonas
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
            else if (_tutorial && _guideStep >= 0 && _guideStep < GuideSteps.Length && !_gameOver && !_awaitInfoAck)
            {
                DrawTutHighlight(GuideSteps[_guideStep].hl); // ilumina la carta/panel del paso guiado
                float gw = 700f, gh = 120f;
                GUILayout.BeginArea(new Rect((_gw - gw) * 0.5f, 18f, gw, gh), GUI.skin.box);
                GUILayout.Space(4);
                GUILayout.Label($"TUTORIAL — Paso {_guideStep + 1}/{GuideSteps.Length}", title);
                var gwrap = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                GUILayout.Label(GuideSteps[_guideStep].msg, gwrap, GUILayout.ExpandHeight(true));
                GUILayout.EndArea();
            }

            if (_drag != null && _drag.Card != null) DrawPlayAura(_drag.Card.Type, s.ActivePlayer);

            if (_tutorial && _awaitInfoAck && _playInfoCard != null && !_gameOver) DrawPlayInfo();

            if (_tutorial2 && _t2Ack && !_gameOver) DrawT2Info();

            if (_tutorial && _gameOver && _gameWinner == 0) DrawTutorialWin();
            if (_tutorial2 && _gameOver && _gameWinner == 0) DrawTutorial2Win();
            if (!_tutorial && !_tutorial2 && _gameOver) DrawMatchEnd(); // partida normal: cierre + volver

            if (_retiradosOpen >= 0) DrawRetiradosList();   // lista vertical de la zona Retirados
            DrawRevealBanner();                              // carta hecha pública (la ve el rival)

            if (_decView != null)
            {
                DrawDecision(_decView);
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
                case TutHL.TapTierra:
                {
                    var pl = _engine.State.Players[0];
                    var v = FindView(c => c.Type == CardType.Tierra && pl.Tierras.Cards.Contains(c) && !c.Tapped);
                    if (v != null) rects.Add(ViewRect(v));
                    break;
                }
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
                float gx = sp.x / _gs, gy = (Screen.height - sp.y) / _gs; // a coords lógicas (aura alineada bajo la GUI escalada)
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
                float gx = sp.x / _gs, gy = (Screen.height - sp.y) / _gs; // GUI: y hacia abajo (coords lógicas)
                minx = Mathf.Min(minx, gx); maxx = Mathf.Max(maxx, gx);
                miny = Mathf.Min(miny, gy); maxy = Mathf.Max(maxy, gy);
            }
            return Rect.MinMaxRect(minx, miny, maxx, maxy);
        }

        /// <summary>Aura luminosa suave y redondeada alrededor del rect (reemplaza el marco duro).</summary>
        private void DrawAura(Rect r, Color col, float pulse)
        {
            EnsureAuraTex();
            float pad = 7f + 3f * pulse; // marco delgado y ajustado que "respira"
            var outer = new Rect(r.x - pad, r.y - pad, r.width + 2f * pad, r.height + 2f * pad);
            var prev = GUI.color;
            GUI.color = new Color(col.r, col.g, col.b, 0.55f + 0.4f * pulse);
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
                    float d = SdRoundBox(p, new Vector2(0.74f, 0.74f), 0.22f); // <0 dentro, 0 borde
                    float ring = Mathf.Exp(-(d * d) / (2f * 0.085f * 0.085f));  // halo delgado en el borde
                    float inside = d < 0f ? 0.05f : 0f;                        // interior apenas visible
                    float a = Mathf.Clamp01(ring + inside);
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
            player = Vis(player);
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
                0 => p.Tierras.Cards.Any(t => t.Tapped),
                1 => p.Seres.Cards.Any(c => c.Nombre == "Adán"),
                2 => p.Seres.Cards.Any(c => c.Nombre == "Eva"),
                3 => p.Seres.Cards.Any(c => c.Nombre == "La Serpiente"),
                4 => _engine.State.IsOver,
                _ => false,
            };
        }

        private void GrantTutorialReward()
        {
            if (_rewardGiven) return;
            _rewardGiven = true;
            // Solo DESBLOQUEAR el logro; el oro se reclama desde la pantalla de Logros
            // (así el tutorial guía al jugador a ese apartado en lugar de saltárselo).
            PlayerPrefs.SetInt("logro_primeros_pasos", 1);
            PlayerPrefs.SetInt("tutorial1_done", 1);
            PlayerPrefs.Save();
        }

        /// <summary>Texto legible con los efectos de una carta (para explicarla al jugarla).</summary>
        private static string CardEffectText(CardDefinition d)
        {
            var sb = new System.Text.StringBuilder();
            if (d.Coste.HasValue) sb.Append($"Coste: {d.Coste} FD   ");
            if (d.Fd.HasValue) sb.Append($"Genera: {d.Fd} FD   ");
            if (d.Dur.HasValue) sb.Append($"Duración: {d.Dur} turnos");
            if (sb.Length > 0) sb.Append("\n\n");

            void Add(string label, string? v) { if (!string.IsNullOrEmpty(v)) sb.Append(label).Append(v).Append("\n\n"); }
            Add("Bendecido (al entrar): ", d.AlEntrar);
            Add("Bendición (clic sobre la carta en campo para usarla): ", d.Activado);
            Add("Al salir: ", d.AlSalir);
            Add("Efecto: ", d.Efecto);
            Add("Condición: ", d.Condicion);
            return sb.ToString().TrimEnd();
        }

        /// <summary>Explica el efecto de la pieza recién jugada; "Entendido" continúa el tutorial.</summary>
        private void DrawPlayInfo()
        {
            var d = _playInfoCard!.Def;
            const float w = 580f, h = 320f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);
            var tt = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUILayout.Space(6);
            GUILayout.Label($"Jugaste: {d.Nombre}", tt);
            GUILayout.Space(4);

            var body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            GUILayout.BeginHorizontal();
            var art = _art.Front(d.Nombre);
            if (art != null) GUILayout.Label(art, GUILayout.Width(150), GUILayout.Height(214));
            GUILayout.Label(CardEffectText(d), body, GUILayout.ExpandHeight(true));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Entendido", GUILayout.Height(32), GUILayout.Width(160)))
                _deferredResolve = () => { _awaitInfoAck = false; _playInfoCard = null; _guideStep++; };
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.EndArea();
        }

        private void DrawTutorialWin()
        {
            GrantTutorialReward();
            const float w = 460f, h = 210f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);
            var tt = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUILayout.Space(8);
            GUILayout.Label("¡VICTORIA!", tt);
            GUILayout.Label("Completaste tu Historia «La Caída del Edén».\nDesbloqueaste el logro «Primeros Pasos»: reclámalo (+700) en Misiones y Logros.",
                body, GUILayout.ExpandHeight(true));
            GUILayout.Space(6);
            if (GUILayout.Button("Volver al menú", GUILayout.Height(34)))
                _deferredResolve = () => { ReturnRequested = true; };
            GUILayout.Space(8);
            GUILayout.EndArea();
        }

        /// <summary>Cierre de una partida NORMAL (no tutorial): resultado + volver al menú.</summary>
        private void DrawMatchEnd()
        {
            bool win = _gameWinner == LocalPlayer; // en red, "gana P0" no es "gano yo": compara contra MI lado
            const float w = 620f, h = 260f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);
            var tt = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            var reason = _engine.State.WinReason;
            string how = reason == VictoryId.III ? "Se completó una Historia (5 piezas en el campo)."
                : reason == VictoryId.II ? "Un jugador se quedó sin cartas en el mazo (deck-out)."
                : reason == VictoryId.I ? "Se completó el ciclo de los 7 DÍAs."
                : reason == VictoryId.Abandono ? "El rival abandonó la partida."
                : "";
            GUILayout.Space(14);
            GUILayout.Label(win ? "¡VICTORIA!" : "DERROTA", tt);
            GUILayout.Label((win ? "Ganaste la partida. " : "El rival ganó la partida. ") + how, body, GUILayout.ExpandHeight(true));
            GUILayout.Space(8);
            if (GUILayout.Button("Volver al menú", GUILayout.Height(44), GUILayout.Width(260)))
                _deferredResolve = () => { ReturnRequested = true; };
            GUILayout.Space(12);
            GUILayout.EndArea();
        }

        // ---------------- Tutorial #2: partida normal contra IA guionizada que demuestra mecánicas ----------------

        private bool _t2AiActing; // la IA scriptada actúa en el hilo principal (evita bloquear por respuestas del humano)

        private const string T2Enter = "Muchas cartas tienen efectos BENDECIDO (al entrar al campo): se disparan una sola vez, en cuanto la carta se juega. El rival acaba de activar uno.";
        private const string T2Dia = "El rival activó una carta DÍA. Los DÍAs (1→7) otorgan recompensas al activarse.\n\n⚠ CUIDADO: si el rival activa su DÍA 7 completa su ciclo y GANA la partida, aunque no complete su Historia. Vigila su avance… y avanza también los tuyos.";
        private const string T2Ser = "El rival jugó un SER y usó su BENDICIÓN (se hace clic sobre el SER en campo, pagando su coste). Muchas BENDICIONES sirven para ROBAR o AÑADIR cartas y ganar ventaja. Tus SER también tienen la suya.";
        private const string T2Trap = "El rival colocó un CONCEPTO BOCA ABAJO: una TRAMPA. Podrá activarla en TU turno (si guardó el FD necesario) para responder a tus acciones. ¡Cuidado con lo que haces frente a cartas ocultas!";
        private const string T2Dur = "Los SER permanecen en el campo un número limitado de TURNOS (su duración). Cuando se agota, el SER se va a Retirados. Algunas cartas dan PROTECCIÓN para que no sean destruidos ni retirados. Un SER del rival acaba de agotar su duración.";
        private const string T2Intro = "Gana completando tu HISTORIA reuniendo sus 5 piezas en el campo.\n\nPero ahora te enfrentas a un rival que también intentará completar la suya para ganar. Juega con cuidado: coloca TIERRAs, genera FD, baja tus piezas y activa tus DÍAs. Te iré señalando las mecánicas nuevas conforme aparezcan.";

        /// <summary>Prepara al rival del tutorial #2: no puede completar su Historia (le falta 1 pieza),
        /// y su mano trae cartas para DEMOSTRAR mecánicas (tierra con efecto, SER con efecto activado, trampa).</summary>
        private void SetupTutorial2(PlayerState p)
        {
            // Quitar TODAS las copias de una pieza de su Historia (h2: "Sem") -> nunca podrá completarla.
            foreach (var c in p.Mazo.Cards.Where(c => c.Nombre == "Sem").ToList()) p.Mazo.Remove(c);
            foreach (var c in p.Mano.Cards.Where(c => c.Nombre == "Sem").ToList()) p.Mano.Remove(c);

            // Sembrar la mano con las cartas de demostración (desde el mazo si están).
            foreach (var c in p.Mano.Cards.ToList()) { p.Mano.Remove(c); p.Mazo.Add(c); } // mano limpia
            void Pull(System.Func<CardInstance, bool> match)
            {
                var c = p.Mazo.Cards.FirstOrDefault(match);
                if (c != null) { p.Mazo.Remove(c); p.Mano.Add(c); }
            }
            void PullCheapest(System.Func<CardInstance, bool> match)
            {
                var c = p.Mazo.Cards.Where(match).OrderBy(x => x.Def.Coste ?? 0).FirstOrDefault();
                if (c != null) { p.Mazo.Remove(c); p.Mano.Add(c); }
            }
            Pull(c => c.Nombre == "Río Tigris");                    // TIERRA con efecto AL ENTRAR
            Pull(c => c.Type == CardType.Tierra && c.Nombre != "Río Tigris"); // otra TIERRA (FD)
            Pull(c => c.Type == CardType.Tierra && c.Nombre != "Río Tigris"); // y otra
            PullCheapest(c => CardTypeNames.IsSer(c.Type) && !string.IsNullOrEmpty(c.Def.Activado)); // SER con efecto activado (barato)
            Pull(c => c.Type == CardType.Concepto && _engine.Effects.IsResponse(c.Def.Id));  // TRAMPA
        }

        private static bool HasEnterEffect(CardInstance c)
        {
            var d = c.Def;
            return !string.IsNullOrEmpty(d.AlEntrar)
                   || (!string.IsNullOrEmpty(d.Efecto) && d.Efecto.ToUpperInvariant().Contains("AL ENTRAR"));
        }

        private void AiDo(System.Func<CommandResult> cmd) { try { cmd(); } catch (System.Exception e) { Debug.LogError(e); } }

        private IEnumerator T2Say(string key, string text)
        {
            _t2Explained.Add(key);
            _t2Info = text; _t2Ack = true;
            while (_t2Ack) yield return null; // pausa hasta "Entendido"
        }

        /// <summary>Turno guionizado del rival: juega en flujo normal y demuestra una mecánica nueva por vez.</summary>
        private IEnumerator T2AiTurn()
        {
            _t2AiActing = true;
            var p = _engine.State.Players[1];
            int logStart = _engine.State.Log.Count;

            // Duración: si un SER del rival se agotó en su Preludio, explícalo (una vez).
            if (!_t2Explained.Contains("dur") && LogHas("agota su duración"))
                yield return T2Say("dur", T2Dur);

            // A) Jugar una TIERRA (preferir una con efecto al entrar, si aún no se explicó).
            CardInstance tierra = null;
            if (!_t2Explained.Contains("enter"))
                tierra = p.Mano.Cards.FirstOrDefault(c => c.Type == CardType.Tierra && HasEnterEffect(c));
            if (tierra == null) tierra = p.Mano.Cards.FirstOrDefault(c => c.Type == CardType.Tierra);
            if (tierra != null && !p.TierraPlayedThisTurn && !p.Tierras.IsFull)
            {
                yield return new WaitForSeconds(0.9f); // "piensa" antes de jugar
                bool hadEffect = HasEnterEffect(tierra);
                AiDo(() => _engine.PlayTierra(tierra)); Rebuild(); yield return new WaitForSeconds(1.1f);
                if (hadEffect && !_t2Explained.Contains("enter")) yield return T2Say("enter", T2Enter);
            }

            // B) Tapear TIERRAs para generar FD (una a una, con pausa).
            foreach (var t in p.Tierras.Cards.Where(t => !t.Tapped).ToList())
            {
                AiDo(() => _engine.TapTierra(t)); Rebuild();
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(0.4f);

            // C) Activar el DÍA una vez (demostración + aviso). Solo una vez para no acercarlo al DÍA 7.
            if (!_t2Explained.Contains("dia"))
            {
                var dia = p.PilaDia.Cards.FirstOrDefault(d => PlayerState.DiaNumero(d) == p.DiaActual);
                int coste = dia?.Def.Coste ?? 99;
                if (dia != null && DiaConditions.Met(p, p.DiaActual) && p.Fd >= coste)
                {
                    yield return new WaitForSeconds(0.8f);
                    AiDo(() => _engine.ActivateDia(false)); Rebuild(); yield return new WaitForSeconds(1.0f);
                    yield return T2Say("dia", T2Dia);
                }
            }

            // D) Jugar un SER con efecto activado y activarlo (demostración de robo/ventaja).
            if (!_t2Explained.Contains("ser_act"))
            {
                var ser = p.Mano.Cards.FirstOrDefault(c => CardTypeNames.IsSer(c.Type)
                    && !string.IsNullOrEmpty(c.Def.Activado) && !p.Seres.IsFull && p.Fd >= (c.Def.Coste ?? 0));
                if (ser != null)
                {
                    yield return new WaitForSeconds(0.8f);
                    AiDo(() => _engine.PlaySer(ser)); Rebuild(); yield return new WaitForSeconds(1.1f);
                    if (!p.SeresActivatedThisTurn.Contains(ser.InstanceId) && p.Fd >= (ser.Def.ActCost ?? 0))
                    { AiDo(() => _engine.ActivateSerEffect(ser)); Rebuild(); yield return new WaitForSeconds(1.0f); }
                    yield return T2Say("ser_act", T2Ser);
                }
            }

            // E) Colocar una TRAMPA boca abajo (deja el FD sin gastar para poder activarla en tu turno).
            if (!_t2Explained.Contains("trap") && !p.Concepto.Cards.Any(x => x.FaceDown) && !p.Concepto.IsFull)
            {
                var trap = p.Mano.Cards.FirstOrDefault(c => c.Type == CardType.Concepto && _engine.Effects.IsResponse(c.Def.Id));
                if (trap != null)
                {
                    yield return new WaitForSeconds(0.8f);
                    AiDo(() => _engine.PlayConcepto(trap, faceDown: true)); Rebuild(); yield return new WaitForSeconds(1.0f);
                    yield return T2Say("trap", T2Trap);
                }
            }

            // F) Terminar el turno (pasa al jugador). En hilo para no bloquear si algún efecto pide respuesta.
            yield return new WaitForSeconds(0.9f);
            _t2AiActing = false;
            if (!_engine.State.IsOver)
            {
                var te = System.Threading.Tasks.Task.Run(() =>
                {
                    try { _engine.EndTurn(); }
                    catch (System.Exception e) { Debug.LogError(e); }
                });
                while (!te.IsCompleted) yield return null;
                Rebuild();
            }
            _ = logStart; // (reservado por si se quiere acotar el escaneo del log)
        }

        private bool LogHas(string sub)
        {
            var log = _engine.State.Log;
            for (int i = 0; i < log.Count; i++) if (log[i].Contains(sub)) return true;
            return false;
        }

        private void DrawT2Info()
        {
            const float w = 580f, h = 290f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);
            var tt = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            GUILayout.Space(6);
            GUILayout.Label("TUTORIAL", tt);
            GUILayout.Label(_t2Info, body, GUILayout.ExpandHeight(true));
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Entendido", GUILayout.Height(32), GUILayout.Width(160)))
                _deferredResolve = () => { _t2Ack = false; };
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.EndArea();
        }

        private void GrantTutorial2Reward()
        {
            if (_reward2Given) return;
            _reward2Given = true;
            PlayerPrefs.SetInt("logro_inicio_historia", 1); // logro reclamable en Logros
            PlayerPrefs.SetInt("mp_unlocked", 1);           // desbloquea Multijugador
            PlayerPrefs.SetInt("tutorial2_done", 1);
            PlayerPrefs.Save();
        }

        private void DrawTutorial2Win()
        {
            GrantTutorial2Reward();
            const float w = 470f, h = 230f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);
            var tt = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUILayout.Space(8);
            GUILayout.Label("¡VICTORIA!", tt);
            var reason = _engine.State.WinReason;
            string how = reason == VictoryId.III ? "Completaste tu Historia «La Caída del Edén» reuniendo sus 5 piezas en el campo."
                : reason == VictoryId.II ? "El rival se quedó sin cartas en el mazo (deck-out) y perdió."
                : reason == VictoryId.I ? "Ganaste completando el ciclo de los 7 DÍAs."
                : "¡Ganaste la partida!";
            GUILayout.Label(how + " ¡Ya dominas lo esencial!\nDesbloqueaste el MULTIJUGADOR y el logro «El Inicio de la Historia»: reclámalo (+1000) en Misiones y Logros.",
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
            // La IA scriptada del tutorial #2 corre en el hilo principal: no bloquear pidiendo respuesta al humano.
            if (_t2AiActing && defender != aiPlayer) return null;
            if (_net) return null; // TODO(red): trampas de respuesta aún no soportadas en lockstep
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
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);
            GUILayout.Label($"{_placeCard!.Nombre} — carta de respuesta",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(6);
            if (GUILayout.Button("Jugar ya", GUILayout.Height(30)))
            {
                var c = _placeCard!; _placePending = false; _placeCard = null;
                _deferredResolve = () => Act(NetCmd.PlayConcepto, c, 0);
            }
            if (GUILayout.Button("Boca abajo (trampa)", GUILayout.Height(30)))
            {
                var c = _placeCard!; _placePending = false; _placeCard = null;
                _deferredResolve = () => Act(NetCmd.PlayConcepto, c, 1);
            }
            GUILayout.EndArea();
        }

        private void DrawResponsePrompt()
        {
            const float w = 380f, h = 120f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);
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
                if (GUI.Button(new Rect(14f, (_gh - bh) * 0.5f, bw, bh), "HISTORIAL"))
                    _showLog = true;
                return;
            }

            const float w = 380f, h = 300f;
            GUILayout.BeginArea(new Rect(14f, (_gh - h) * 0.5f, w, h), GUI.skin.box);

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

        /// <summary>Cuadro de sí/no para los efectos OPCIONALES (p. ej. Río Tigris).</summary>
        private void DrawYesNoDecision(DecisionRequest req)
        {
            const float w = 420f, h = 150f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);
            GUILayout.Space(10);
            GUILayout.Label(req.Prompt,
                new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, alignment = TextAnchor.MiddleCenter },
                GUILayout.ExpandHeight(true));
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Sí", GUILayout.Width(140f), GUILayout.Height(40f)))
                _deferredResolve = () => _decisions.ResolveYesNo(req, true);
            GUILayout.Space(16);
            if (GUILayout.Button("No", GUILayout.Width(140f), GUILayout.Height(40f)))
                _deferredResolve = () => _decisions.ResolveYesNo(req, false);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.EndArea();
        }

        private void DrawDecision(DecisionRequest req)
        {
            if (req != _decReq) { _decReq = req; _decSelected = null; _decOrder.Clear(); _decScroll = Vector2.zero; }

            // Pregunta de SÍ/NO (efectos opcionales): sin cartas, solo dos botones.
            if (req.IsYesNo) { DrawYesNoDecision(req); return; }

            const float thumbW = 84f, thumbH = 118f, gap = 8f;
            int visible = Mathf.Clamp(req.Options.Count, 1, 7);
            float w = visible * (thumbW + gap) + gap + 20f;
            float h = 30f + (thumbH + 40f) + 46f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, (_gh - h) / 2f, w, h), GUI.skin.box);

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
            // El reloj corre contra una HORA LÍMITE absoluta, no acumulando deltaTime: así no se
            // congela al abrir Opciones (que apaga este componente) ni al perder el foco de la ventana.
            if (s.TurnNumber != _timerTurn) { _timerTurn = s.TurnNumber; _turnDeadline = RealTime + turnSeconds; }
            if (_tutorial || _tutorial2) { _turnDeadline = RealTime + turnSeconds; _turnTimer = turnSeconds; return; } // el tutorial no consume tiempo
            if (s.IsOver) return;
            bool humanTurn = aiPlayer < 0 || s.ActivePlayer != aiPlayer;
            if (!humanTurn) { _turnDeadline = RealTime + turnSeconds; _turnTimer = turnSeconds; return; } // la IA no consume el reloj
            _turnTimer = (float)(_turnDeadline - RealTime);
            if (_turnTimer <= 0f && !_aiRunning && !_busy)
            {
                _turnTimer = 0f;
                // En red el fin de turno DEBE viajar por el relay (lockstep) y solo lo manda el jugador
                // activo: ejecutarlo local en ambos lados desincronizaría los motores.
                if (_net)
                {
                    if (s.ActivePlayer == _netMyPlayer) Act(NetCmd.EndTurn, null);
                }
                else RunHumanCommand("Tiempo agotado", () => _engine.EndTurn());
            }
        }

        private GUIStyle? _wrap;

        // --- Revelación pública (carta buscada del mazo: ambos jugadores deben verla) ---

        private CardInstance? _revealCard;
        private string _revealWho = "";
        private double _revealUntil;

        private void OnCardRevealed(int playerId, CardInstance card, string motivo)
        {
            _revealCard = card;
            _revealWho = (playerId == LocalPlayer ? "Tú" : "El rival") + " — " + motivo;
            _revealUntil = RealTime + 4.0; // visible unos segundos
        }

        private void DrawRevealBanner()
        {
            if (_revealCard == null) return;
            if (RealTime > _revealUntil) { _revealCard = null; return; }

            const float w = 380f, h = 150f;
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, 60f, w, h), GUI.skin.box);
            GUILayout.Label("CARTA REVELADA",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label(_revealWho, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(4);
            DrawDetailBody(_revealCard, _revealCard.OwnerId);
            GUILayout.EndArea();
        }

        // --- Lista de la zona Retirados (clic sobre la zona) ---

        private int _retiradosOpen = -1;   // id del jugador cuya zona se está viendo (-1 = cerrada)
        private Vector2 _retiradosScroll;

        private void DrawRetiradosList()
        {
            var p = _engine.State.Players[_retiradosOpen];
            const float w = 380f;
            float h = Mathf.Min(_gh - 80f, 460f);
            GUILayout.BeginArea(new Rect((_gw - w) / 2f, 40f, w, h), GUI.skin.box);
            GUILayout.Label($"RETIRADOS — {(_retiradosOpen == LocalPlayer ? "tuyos" : "del rival")} ({p.Retirados.Count})",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(4);

            _retiradosScroll = GUILayout.BeginScrollView(_retiradosScroll, GUILayout.ExpandHeight(true));
            if (p.Retirados.Count == 0) GUILayout.Label("(vacía)");
            else
                foreach (var c in p.Retirados.Cards)
                    GUILayout.Label($"{c.Nombre}  —  {c.Type}");
            GUILayout.EndScrollView();

            GUILayout.Space(4);
            if (GUILayout.Button("Cerrar", GUILayout.Height(32))) _retiradosOpen = -1;
            GUILayout.EndArea();
        }

        private void DrawCardDetail(CardView cv)
        {
            _wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

            // Pila del MAZO: en vez de la carta, cuántas cartas quedan.
            var owner = _engine.State.Players[cv.OwnerId];
            if (cv.FaceDown && owner.Mazo.Cards.Contains(cv.Card))
            {
                GUILayout.BeginArea(new Rect(10, 10, 330, 60), GUI.skin.box);
                GUILayout.Label(cv.OwnerId == LocalPlayer ? "TU MAZO" : "MAZO DEL RIVAL",
                    new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                GUILayout.Label($"Quedan {owner.Mazo.Count} cartas");
                GUILayout.EndArea();
                return;
            }

            // Boca abajo: solo se oculta al RIVAL. Tus propias trampas se leen con normalidad
            // (necesitas saber qué guardaste para decidir si la activas).
            if (cv.FaceDown && cv.OwnerId != LocalPlayer)
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
            GUILayout.BeginArea(new Rect(_gw - 550f, 10f, 540f, 320f), GUI.skin.box); // arriba a la derecha
            DrawDetailBody(card, owner);
            GUILayout.EndArea();
        }

        /// <summary>Foto del campo en texto plano, para poder reproducir después lo que se vio.
        /// Es la costura MatchSnapshot.Describe: la consume el sistema de pruebas, si está.</summary>
        private string DescribeBoard()
        {
            if (_engine == null) return "(sin partida)";
            var s = _engine.State;
            var sb = new System.Text.StringBuilder();
            sb.Append("Turno ").Append(s.TurnNumber)
              .Append("  Fase ").Append(s.Phase)
              .Append("  Activo P").Append(s.ActivePlayer);
            if (s.IsOver) sb.Append("  [FIN: gana P").Append(s.Winner).Append(' ').Append(s.WinReason).Append(']');
            sb.AppendLine();

            for (int p = 0; p < 2; p++)
            {
                var ps = s.Players[p];
                sb.Append(p == LocalPlayer ? "TU " : "RIVAL ").Append("(P").Append(p).Append(')')
                  .Append("  FD ").Append(ps.Fd)
                  .Append("  DIA ").Append(ps.DiaActual)
                  .Append("  Mazo ").Append(ps.Mazo.Count)
                  .Append("  Mano ").Append(ps.Mano.Count)
                  .AppendLine();
                sb.Append("   TIERRAS: ").AppendLine(Zona(ps.Tierras.Cards, t => t.Tapped ? " [tap]" : ""));
                sb.Append("   SERES:   ").AppendLine(Zona(ps.Seres.Cards, c => $" (d{c.DurLeft})"));
                sb.Append("   CONCEPTO:").AppendLine(Zona(ps.Concepto.Cards, c => c.FaceDown ? " [oculta]" : ""));
                sb.Append("   HISTORIA:").AppendLine(ps.Historia != null ? " " + ps.Historia.Nombre : " -");
                sb.Append("   MANO:    ").AppendLine(Zona(ps.Mano.Cards, _ => ""));
                sb.Append("   RETIRADOS:").AppendLine(Zona(ps.Retirados.Cards, _ => ""));
            }
            return sb.ToString();

            string Zona(System.Collections.Generic.List<CardInstance> cards, System.Func<CardInstance, string> sufijo)
                => cards.Count == 0 ? " -" : " " + string.Join(", ", cards.Select(c => c.Nombre + sufijo(c)));
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
            if (!string.IsNullOrEmpty(d.AlEntrar)) GUILayout.Label($"Bendecido: {d.AlEntrar}", _wrap);
            if (!string.IsNullOrEmpty(d.Activado)) GUILayout.Label($"Bendición: {d.Activado}", _wrap);
            if (!string.IsNullOrEmpty(d.AlSalir)) GUILayout.Label($"Al salir: {d.AlSalir}", _wrap);
            if (!string.IsNullOrEmpty(d.Efecto)) GUILayout.Label($"Efecto: {d.Efecto}", _wrap);

            DrawHistoriaPieces(d, owner);

            // Costura de pruebas: marcas de comprobación por efecto. Sin sistema de QA no hay nada.
            var extra = MatchSnapshot.ExtraCardLine?.Invoke(d);
            if (!string.IsNullOrEmpty(extra)) GUILayout.Label(extra, _wrap);

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
