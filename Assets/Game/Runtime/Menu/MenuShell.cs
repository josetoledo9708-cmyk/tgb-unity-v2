using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core.Model;
using Game.Runtime; // UnityCatalogLoader
using Game.Runtime.View; // CardArtLibrary

namespace Game.Runtime.Menu
{
    /// <summary>
    /// Cáscara de menús (réplica del Shell+stack de Godot) con Canvas uGUI generado por código.
    /// Cada apartado es una pantalla apilable; el botón Volver hace pop. "Jugar" carga la escena
    /// del campo (Main). Un solo MonoBehaviour construye todo.
    /// </summary>
    [DefaultExecutionOrder(1000)] // su OnGUI se dibuja después (encima) del campo IMGUI
    public sealed class MenuShell : MonoBehaviour
    {
        public enum Screen { MainMenu, Historias, ContraIA, Multijugador, MisMazos, SelectDeck, ChooseHistoria, DeckBuilder, Misiones, Tienda, Tomos, Opciones }

        private Canvas _canvas;
        private RectTransform _root;      // contenedor de la pantalla activa
        private GameObject _current;
        private Screen _currentScreen = (Screen)(-1);
        private GameObject _modal; // overlay opcional (opciones de mazo, renombrar) encima de la pantalla activa
        private readonly List<Screen> _stack = new();
        // Pantallas con fondo en VIDEO: se cachean (desactivar en vez de destruir) para no recrear el
        // VideoPlayer en cada navegación → sin el parpadeo del "Prepare". Al reusarlas se refrescan sus
        // datos dinámicos vía _onShow.
        private static readonly HashSet<Screen> _cacheable = new() { Screen.MainMenu, Screen.Tomos };
        private readonly Dictionary<Screen, GameObject> _cache = new();
        private readonly Dictionary<Screen, System.Action> _onShow = new();
        private ScreenFader _fader; // cortina de transición entre menús
        private Game.Runtime.View.HotseatView _board;
        private CardCatalog _catalog;      // catálogo de cartas, para previews reales en los modales
        private CardArtLibrary _cardArt;   // arte de carta, misma carpeta que usa el campo
        private string _chosenHistoriaId;  // carta Historia elegida al crear una nueva historia/mazo
        private bool _inMatch;             // hay una partida en curso (campo encendido, menú oculto)
        private Texture2D _gearTex;        // icono de engranaje para el overlay IMGUI de la partida

        /// <summary>El campo a activar cuando el jugador entra a una partida (se deja desactivado).</summary>
        public void SetBoard(Game.Runtime.View.HotseatView board) => _board = board;

        /// <summary>Carga perezosa del catálogo + arte de carta (solo la primera vez que se necesita).</summary>
        private void EnsureCatalog()
        {
            if (_catalog != null) return;
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, "catalogo.v3.json");
                if (File.Exists(path)) _catalog = UnityCatalogLoader.FromJson(File.ReadAllText(path));
            }
            catch (System.Exception e) { Debug.LogWarning("Menú: no se pudo cargar el catálogo: " + e.Message); }
            _cardArt = new CardArtLibrary(@"C:\Users\Rinco\Downloads"); // misma carpeta que HotseatView.artFolder
        }

        private void Start()
        {
            EnsureEventSystem();
            BuildCanvas();
            PreloadArt();              // precarga el arte pesado en el arranque (no al abrir cada menú)
            Push(Screen.MainMenu);
            PrewarmCacheable(Screen.Tomos); // pre-construye Tomos detrás → su video ya está listo al entrar
        }

        /// <summary>Construye una pantalla cacheable de antemano y la deja viva DETRÁS de la actual,
        /// para que su VideoPlayer prepare/reproduzca ya y no haya retardo la primera vez que se entra.</summary>
        private void PrewarmCacheable(Screen s)
        {
            if (!_cacheable.Contains(s) || _cache.ContainsKey(s)) return;
            var go = Build(s).gameObject;
            _cache[s] = go;
            go.transform.SetAsFirstSibling(); // detrás de la pantalla actual (queda oculta pero activa)
        }

        /// <summary>Carga y cachea de una vez el arte pesado (cartas Historia + fondos) al arrancar,
        /// para que ninguna pantalla sufra el "tirón" de cargarlo la primera vez.</summary>
        private void PreloadArt()
        {
            for (int i = 1; i <= 7; i++)
            {
                MenuAssets.Sprite("cartas_historia/carta_h" + i);
                MenuAssets.Sprite("historias/bg_h" + i);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        private void BuildCanvas()
        {
            var go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
        }

        // --- navegación (pila) ---

        private void Push(Screen s)
        {
            _stack.Add(s);
            Show(s);
        }

        private void Pop()
        {
            if (_stack.Count <= 1) return; // nunca sale del MainMenu
            _stack.RemoveAt(_stack.Count - 1);
            Show(_stack[_stack.Count - 1]);
        }

        private void Show(Screen s)
        {
            CloseModal();
            // ocultar la actual: si es cacheable la dejamos VIVA y activa detrás (así su VideoPlayer no
            // se detiene ni se vuelve a "Prepare" → sin parpadeo); si no, la destruimos.
            if (_current != null && !(_cacheable.Contains(_currentScreen) && _cache.ContainsKey(_currentScreen)))
                Destroy(_current);

            // mostrar destino: reusar del caché (traer al frente) o construir
            if (_cacheable.Contains(s) && _cache.TryGetValue(s, out var cached) && cached != null)
            {
                cached.SetActive(true);
                cached.transform.SetAsLastSibling(); // al frente (tapa a las cacheadas que quedan detrás)
                if (_onShow.TryGetValue(s, out var refresh)) refresh?.Invoke(); // refresca datos dinámicos
                _current = cached;
            }
            else
            {
                var built = Build(s).gameObject; // nuevo hijo = al frente
                if (_cacheable.Contains(s)) _cache[s] = built;
                _current = built;
            }
            _currentScreen = s;

            // cortina de transición (cubre el instante del cambio y da tiempo a cargar el fondo)
            EnsureFade();
            _fader.transform.SetAsLastSibling();
            _fader.Play(1.2f);
        }

        private void EnsureFade()
        {
            if (_fader != null) return;
            var go = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(ScreenFader));
            go.transform.SetParent(_root, false);
            MenuTheme.Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); img.raycastTarget = false;
            _fader = go.GetComponent<ScreenFader>();
        }

        private void CloseModal()
        {
            if (_modal != null) { Destroy(_modal); _modal = null; }
        }

        private RectTransform Build(Screen s) => s switch
        {
            Screen.MainMenu     => BuildMainMenu(),
            Screen.Historias    => BuildHistorias(),
            Screen.ContraIA     => BuildContraIA(),
            Screen.Multijugador => BuildSimple("MULTIJUGADOR", "Partida LAN 1v1 — próximamente."),
            Screen.MisMazos     => BuildMisMazos(),
            Screen.SelectDeck   => BuildSelectDeck(),
            Screen.ChooseHistoria => BuildChooseHistoria(),
            Screen.DeckBuilder  => BuildDeckBuilder(),
            Screen.Misiones     => BuildSimple("MISIONES Y LOGROS", "Completa misiones para ganar monedas."),
            Screen.Tienda       => BuildTienda(),
            Screen.Tomos        => BuildTomos(),
            Screen.Opciones     => BuildOpciones(),
            _ => BuildMainMenu()
        };

        // --- fondo + TopBar comunes ---

        private RectTransform NewScreen(string name, string bgSprite, Color? fallback = null)
        {
            var screen = MenuTheme.Panel(_root, name);
            var bg = MenuAssets.Sprite(bgSprite);
            if (bg != null) MenuTheme.Picture(screen, "BG", bg, preserveAspect: false).GetComponent<Image>().type = Image.Type.Simple;
            else MenuTheme.Rect(screen, "BG", fallback ?? new Color(0.05f, 0.06f, 0.12f, 1f));
            var bgImg = screen.Find("BG") as RectTransform;
            if (bgImg != null) MenuTheme.Stretch(bgImg);
            return screen;
        }

        /// <summary>Pone un video en bucle a pantalla completa justo encima del BG estático (que queda
        /// de respaldo). No captura toques. <paramref name="resPath"/> es la ruta en Resources.</summary>
        private void AddVideoBackground(RectTransform screen, string resPath)
        {
            var clip = Resources.Load<UnityEngine.Video.VideoClip>(resPath);
            if (clip == null) return; // sin video: se queda el fondo estático
            var rt = new RenderTexture(1280, 720, 0);
            var go = new GameObject("BGVideo", typeof(RectTransform), typeof(RawImage), typeof(UnityEngine.Video.VideoPlayer));
            go.transform.SetParent(screen, false);
            MenuTheme.Stretch((RectTransform)go.transform);
            go.transform.SetSiblingIndex(1); // encima del BG (índice 0), debajo del resto de la UI
            var raw = go.GetComponent<RawImage>();
            raw.texture = rt; raw.raycastTarget = false;
            var vp = go.GetComponent<UnityEngine.Video.VideoPlayer>();
            vp.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.clip = clip;
            vp.isLooping = true;
            vp.playOnAwake = true;
            vp.waitForFirstFrame = true;
            vp.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
            go.AddComponent<VideoLoopOnEnable>(); // reproduce al activarse la pantalla (no solo al construirla)
            vp.Play();
        }

        private void TopBar(RectTransform screen, bool withCoins)
        {
            var bar = MenuTheme.Panel(screen, "TopBar");
            MenuTheme.Anchor(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -64f), new Vector2(-16f, -12f));
            if (withCoins)
            {
                var coinGo = new GameObject("Coins", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                coinGo.transform.SetParent(bar, false);
                var crt = (RectTransform)coinGo.transform;
                MenuTheme.Anchor(crt, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(220f, 0f));
                var hb = coinGo.GetComponent<HorizontalLayoutGroup>();
                hb.spacing = 8f; hb.childAlignment = TextAnchor.MiddleLeft; hb.childForceExpandWidth = false;
                var icon = MenuTheme.Picture(crt, "CoinIcon", MenuAssets.Sprite("Moneda"));
                ((RectTransform)icon.transform).sizeDelta = new Vector2(40f, 40f);
                var lbl = MenuTheme.Label(crt, PlayerData.Monedas.ToString(), 26, MenuTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
                ((RectTransform)lbl.transform).sizeDelta = new Vector2(120f, 40f);
            }
        }

        private void BackButton(RectTransform screen)
        {
            // Mismo diseño que TUTORIAL del menú principal: recuadro Botones.png + texto oro sin espesor.
            var tb = MenuTheme.TextButton(screen, "VOLVER", 14, Pop, 120f, 40f, thicken: false);
            MenuTheme.Anchor((RectTransform)tb.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -70f), new Vector2(156f, -30f));
        }

        private void Title(RectTransform screen, string text)
        {
            var t = MenuTheme.Label(screen, text, 40, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(0f, -14f));
        }

        // --- MAIN MENU ---

        private RectTransform BuildMainMenu()
        {
            var screen = NewScreen("MainMenu", "main_menu_bg");
            TryVideoBackground(screen, "Menu/MM"); // video en bucle detrás (fallback = imagen estática)

            // --- Perfil arriba-izquierda ---
            var prof = MenuTheme.Rect(screen, "Profile", new Color(0.10f, 0.09f, 0.16f, 0.85f));
            MenuTheme.Anchor((RectTransform)prof.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -74f), new Vector2(280f, -16f));
            prof.gameObject.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            var av = MenuTheme.Rect(prof.transform, "Avatar", new Color(0.30f, 0.28f, 0.55f, 1f));
            MenuTheme.Anchor((RectTransform)av.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -22f), new Vector2(52f, 22f));
            MenuTheme.Label(av.transform, "W", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            var pname = MenuTheme.Label(prof.transform, "Jugador", 18, MenuTheme.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            MenuTheme.GoldMetalText(pname); // mismo oro metálico que los botones
            MenuTheme.Anchor((RectTransform)pname.transform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(62f, -4f), new Vector2(-6f, -6f));
            var pinfo = MenuTheme.Label(prof.transform, "Nv 1 · 0 amigos", 13, new Color(0.75f, 0.72f, 0.6f), TextAnchor.LowerLeft);
            MenuTheme.Anchor((RectTransform)pinfo.transform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(62f, 6f), new Vector2(-6f, 0f));

            // --- Cluster arriba-derecha: monedas + tutorial + engranaje ---
            var coinIcon = MenuTheme.Picture(screen, "CoinIcon", MenuAssets.Sprite("Moneda"));
            MenuTheme.Anchor((RectTransform)coinIcon.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300f, -56f), new Vector2(-262f, -18f));
            var coinLbl = MenuTheme.Label(screen, PlayerData.Monedas.ToString(), 24, MenuTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            MenuTheme.GoldMetalText(coinLbl); // mismo oro metálico que los botones
            MenuTheme.Anchor((RectTransform)coinLbl.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-256f, -56f), new Vector2(-150f, -18f));
            var tut = MenuTheme.TextButton(screen, "TUTORIAL", 14, LaunchGame, 110f, 38f, thicken: false);
            MenuTheme.Anchor((RectTransform)tut.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-146f, -56f), new Vector2(-52f, -18f));

            // Engranaje = icono cog dorado procedural, SIN caja detrás.
            var gearGo = new GameObject("Btn_Gear", typeof(RectTransform), typeof(Image), typeof(Button));
            gearGo.transform.SetParent(screen, false);
            var gearImg = gearGo.GetComponent<Image>();
            gearImg.sprite = MenuGraphics.Gear(72, 8);
            gearImg.color = MenuTheme.Gold;
            gearImg.preserveAspect = true;
            var gearBtn = gearGo.GetComponent<Button>();
            gearBtn.targetGraphic = gearImg;
            var gcol = gearBtn.colors;
            gcol.normalColor = Color.white;
            gcol.highlightedColor = new Color(1.25f, 1.2f, 1.05f, 1f);
            gcol.pressedColor = new Color(0.8f, 0.75f, 0.55f, 1f);
            gearBtn.colors = gcol;
            gearBtn.onClick.AddListener(() => ShowOpcionesModal());
            gearGo.AddComponent<HoverScale>();
            MenuTheme.Anchor((RectTransform)gearGo.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-44f, -54f), new Vector2(-10f, -20f));

            // --- Logo centrado (+30%, crece simétrico desde su propio centro) ---
            var logo = MenuTheme.Picture(screen, "Logo", MenuAssets.Sprite("logo_tgb"));
            MenuTheme.Anchor((RectTransform)logo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-234f, -281.5f), new Vector2(234f, -8.5f));

            // --- 4 botones DISEÑADOS con uGUI (marco dorado + interior oscuro) ---
            var list = MenuTheme.VBox(screen, 6f, 0, TextAnchor.MiddleCenter); // mas juntos
            MenuTheme.Anchor((RectTransform)list.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240f, -280f), new Vector2(240f, 50f));
            DesignedMenuButton(list.transform, "HISTORIAS", () => Push(Screen.Historias));
            DesignedMenuButton(list.transform, "MULTIJUGADOR", () => Push(Screen.Multijugador));
            DesignedMenuButton(list.transform, "CONSTRUCTOR DE HISTORIAS", () => Push(Screen.MisMazos));
            DesignedMenuButton(list.transform, "MISIONES Y LOGROS", () => Push(Screen.Misiones));

            // --- Tienda = estandarte a la izquierda-centro ---
            var tienda = FloatingIcon(screen, "buttons/btn_tienda", "TIENDA", () => Push(Screen.Tienda));
            MenuTheme.Anchor((RectTransform)tienda.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, -120f), new Vector2(150f, 120f));

            // --- Tomos = libro ornamentado a la derecha, a la altura media de los botones (+20%) ---
            var tomos = FloatingIcon(screen, "Tomes", "TOMOS", () => Push(Screen.Tomos));
            MenuTheme.Anchor((RectTransform)tomos.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-325f, -287f), new Vector2(-75f, 57f)); // marco de atrás ~30% más grande
            // partículas doradas ambientales que suben desde detrás del tomo
            var tomoPart = new GameObject("TomoParticles", typeof(RectTransform)).GetComponent<RectTransform>();
            tomoPart.SetParent(tomos.transform, false);
            tomoPart.anchorMin = tomoPart.anchorMax = new Vector2(0.5f, 0.5f); tomoPart.pivot = new Vector2(0.5f, 0.5f);
            tomoPart.sizeDelta = new Vector2(160f, 220f); tomoPart.anchoredPosition = Vector2.zero;
            var tomoParticles = tomoPart.gameObject.AddComponent<GoldParticles>();
            tomoParticles.dotSprite = GlowSprite();
            tomoParticles.count = 29; // más partículas

            // resplandor radial suave detrás del libro (oculto; se ilumina al pasar el cursor)
            var tomoGlow = MenuTheme.Picture(tomos.transform, "TomoGlow", GlowSprite(), preserveAspect: false);
            tomoGlow.raycastTarget = false;
            tomoGlow.color = new Color(1f, 0.85f, 0.5f, 0f); // oro, alfa 0 al inicio
            var tgr = (RectTransform)tomoGlow.transform;
            tgr.anchorMin = tgr.anchorMax = new Vector2(0.5f, 0.5f); tgr.pivot = new Vector2(0.5f, 0.5f);
            tgr.sizeDelta = new Vector2(300f, 360f); tgr.anchoredPosition = Vector2.zero;

            // libro 3D al frente cubriendo el tomo del botón; SOLO este crece al pasar el cursor
            var tomoBook = MenuTheme.Picture(tomos.transform, "TomoBook", MenuAssets.Sprite("diseno_tomo"), preserveAspect: true);
            tomoBook.raycastTarget = false;
            var tbr = (RectTransform)tomoBook.transform;
            tbr.anchorMin = tbr.anchorMax = new Vector2(0.5f, 0.5f); tbr.pivot = new Vector2(0.5f, 0.5f);
            tbr.sizeDelta = new Vector2(138f, 184f); tbr.anchoredPosition = Vector2.zero;
            var tomoHover = tomos.gameObject.AddComponent<HoverScale>(); // hover en el botón: agranda el libro + resplandor
            tomoHover.target = tomoBook.transform;
            tomoHover.glow = tomoGlow;
            tomoHover.glowAlpha = 1f;

            _onShow[Screen.MainMenu] = () => coinLbl.text = PlayerData.Monedas.ToString(); // refrescar monedas al reusar del caché
            return screen;
        }

        private void DesignedMenuButton(Transform parent, string label, System.Action onClick,
                                        float width = 410f, float height = 70f, // largo reducido, sin icono
                                        System.Func<Transform, RectTransform> icon = null)
        {
            var b = MenuTheme.DesignedButton(parent, label, onClick, width, height, icon);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = height;
        }

        private void AddMenuButton(Transform parent, string sprite, string fallbackText, System.Action onClick,
                                   float width = 340f, float height = 58f)
        {
            var s = MenuAssets.Sprite(sprite);
            var b = s != null
                ? MenuTheme.ImageButton(parent, s, onClick, width, height)
                : MenuTheme.TextButton(parent, fallbackText, 20, onClick, width, height);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = height;
        }

        private Button FloatingIcon(RectTransform screen, string sprite, string fallback, System.Action onClick)
        {
            var s = MenuAssets.Sprite(sprite);
            return s != null
                ? MenuTheme.ImageButton(screen, s, onClick, 120f, 120f)
                : MenuTheme.TextButton(screen, fallback, 16, onClick, 120f, 60f);
        }

        /// <summary>Fondo de video en bucle (si el VideoClip existe en Resources); si no, no hace nada.</summary>
        private void TryVideoBackground(RectTransform screen, string clipPath)
        {
            if (Application.isMobilePlatform) return; // en móvil: solo imagen estática (batería/rendimiento)
            var clip = Resources.Load<UnityEngine.Video.VideoClip>(clipPath);
            if (clip == null) return;

            var rt = new RenderTexture(1280, 720, 0) { name = "MenuBGVideoRT" };
            var go = new GameObject("BGVideo", typeof(RectTransform), typeof(RawImage), typeof(UnityEngine.Video.VideoPlayer));
            go.transform.SetParent(screen, false);
            go.transform.SetSiblingIndex(1); // encima del BG estático, debajo de la UI
            MenuTheme.Stretch((RectTransform)go.transform);
            go.transform.localScale = new Vector3(1f, 1.19f, 1f); // escala Y del video

            var raw = go.GetComponent<RawImage>();
            raw.texture = rt;

            var vp = go.GetComponent<UnityEngine.Video.VideoPlayer>();
            vp.clip = clip;
            vp.isLooping = true;
            vp.skipOnDrop = true;
            vp.waitForFirstFrame = true;
            vp.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.aspectRatio = UnityEngine.Video.VideoAspectRatio.FitOutside; // cubre la pantalla
            // Audio Direct con la pista muteada (settings del Inspector).
            vp.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.Direct;
            vp.controlledAudioTrackCount = 1;
            vp.EnableAudioTrack(0, true);
            vp.SetDirectAudioMute(0, true);
            vp.prepareCompleted += p => p.SetDirectAudioMute(0, true); // asegura mute tras preparar
            vp.playOnAwake = true;
            vp.Play();
        }

        // --- pantallas simples (título + texto + volver) ---

        private RectTransform BuildSimple(string title, string body)
        {
            var screen = NewScreen(title, "fondo_constructor", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.45f));
            Title(screen, title);
            BackButton(screen);
            var t = MenuTheme.Label(screen, body, 22, new Color(0.9f, 0.86f, 0.72f));
            MenuTheme.Anchor((RectTransform)t.transform, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f), Vector2.zero, Vector2.zero);
            return screen;
        }

        private RectTransform BuildListScreen(string title, string bg, List<(string label, System.Action act)> items)
        {
            var screen = NewScreen(title, bg, MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.5f));
            Title(screen, title);
            BackButton(screen);
            var list = MenuTheme.VBox(screen, 12f, 0, TextAnchor.UpperCenter);
            MenuTheme.Anchor((RectTransform)list.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-220f, 40f), new Vector2(220f, -90f));
            foreach (var (label, act) in items)
            {
                var b = MenuTheme.TextButton(list.transform, label, 20, act, 420f, 54f);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            }
            return screen;
        }

        private List<(string, System.Action)> HistoriaButtons()
        {
            var items = new List<(string, System.Action)>();
            string[] nombres =
            {
                "1 · La Caída del Edén", "2 · El Diluvio Universal", "3 · La Alianza con Abraham",
                "4 · Sodoma y Gomorra", "5 · El Primer Fratricidio", "6 · La Escalera al Cielo",
                "7 · José, el Salvador de Egipto"
            };
            for (int i = 0; i < nombres.Length; i++)
            {
                int idx = i;
                items.Add((nombres[i], () => { PlayerData.SelectedHistoriaId = "h" + (idx + 1); LaunchGame(); }));
            }
            return items;
        }

        // --- HISTORIAS: grid de tarjetas por campaña (color + estrellas + estado) ---

        private struct HistoriaCard
        {
            public string label, historiaId, desc;
            public int stars;      // 1-4
            public bool completed; // TODO: progreso real cuando exista guardado
            public Color color;
        }

        private static readonly HistoriaCard[] Historias =
        {
            new HistoriaCard { label = "Tutorial", historiaId = null, stars = 1,
                desc = "Aprende las mecánicas del juego.", completed = true, color = new Color(0.10f, 0.35f, 0.15f) },
            new HistoriaCard { label = "La Caída del Edén", historiaId = "h1", stars = 1,
                desc = "El Jardín, el fruto prohibido, la serpiente.", completed = true, color = new Color(0.55f, 0.10f, 0.35f) },
            new HistoriaCard { label = "El Primer Fratricidio", historiaId = "h5", stars = 2,
                desc = "Adán, Eva y la maldición de Caín.", completed = true, color = new Color(0.45f, 0.10f, 0.08f) },
            new HistoriaCard { label = "El Diluvio Universal", historiaId = "h2", stars = 2,
                desc = "Noé, el Arca y el monte Ararat.", completed = true, color = new Color(0.10f, 0.25f, 0.55f) },
            new HistoriaCard { label = "La Alianza con Abraham", historiaId = "h3", stars = 3,
                desc = "Abraham, Sara y el pacto en Canaán.", completed = true, color = new Color(0.55f, 0.42f, 0.08f) },
            new HistoriaCard { label = "La Destrucción de Sodoma", historiaId = "h4", stars = 3,
                desc = "Lot, los ángeles y el fuego del cielo.", completed = true, color = new Color(0.55f, 0.28f, 0.08f) },
            new HistoriaCard { label = "La Escalera al Cielo", historiaId = "h6", stars = 4,
                desc = "Jacob, Esaú y los ángeles en Betel.", completed = true, color = new Color(0.30f, 0.12f, 0.45f) },
            new HistoriaCard { label = "José, el Salvador de Egipto", historiaId = "h7", stars = 4,
                desc = "José, el Faraón y la tierra de Gosén.", completed = false, color = new Color(0.50f, 0.40f, 0.10f) },
        };

        private RectTransform BuildHistorias()
        {
            var screen = NewScreen("Historias", "fondo_historias", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.18f)); // menos oscuro: resalta la imagen de fondo
            BackButton(screen);

            var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
            grid.transform.SetParent(screen, false);
            MenuTheme.Anchor((RectTransform)grid.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-460f, 40f), new Vector2(460f, -160f));
            grid.cellSize = new Vector2(440f, 106f);
            grid.spacing = new Vector2(20f, 14f);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;

            foreach (var d in Historias) BuildHistoriaCard(grid.transform, d);
            return screen;
        }

        private void BuildHistoriaCard(Transform parent, HistoriaCard d)
        {
            var card = new GameObject("Card_" + d.label, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            var img = card.GetComponent<Image>();
            img.sprite = MenuGraphics.Rounded(64, 10);
            img.type = Image.Type.Sliced;

            var btn = card.GetComponent<Button>();
            var cols = btn.colors;
            cols.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cols.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cols;
            btn.onClick.AddListener(() =>
            {
                if (d.historiaId == null) { LaunchGame(); return; } // Tutorial: sin elegir mazo
                PlayerData.SelectedHistoriaId = d.historiaId;
                Push(Screen.SelectDeck); // a continuación, elegir con qué mazo jugar
            });
            card.AddComponent<HoverScale>();
            card.AddComponent<GlintOnHover>(); // reflejo luminoso del título dorado al pasar el cursor

            // Fondo del recuadro = imagen de la historia (recortada al aspecto), enmascarada a las
            // esquinas redondeadas + scrim izquierdo para legibilidad. Si no hay imagen, color plano.
            var photo = d.historiaId != null ? MenuAssets.Sprite("historias/bg_" + d.historiaId) : null;
            if (photo != null)
            {
                img.color = Color.white;
                var mask = card.AddComponent<Mask>();
                mask.showMaskGraphic = true;

                var ph = new GameObject("Photo", typeof(RectTransform), typeof(Image));
                ph.transform.SetParent(card.transform, false);
                var phi = ph.GetComponent<Image>();
                phi.sprite = photo; phi.type = Image.Type.Simple; phi.preserveAspect = false; phi.raycastTarget = false;
                MenuTheme.Stretch((RectTransform)ph.transform);
                btn.targetGraphic = phi; // el hover ilumina la foto

                var sc = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
                sc.transform.SetParent(card.transform, false);
                var sci = sc.GetComponent<Image>();
                sci.sprite = MenuGraphics.HGradient(new Color(0f, 0f, 0f, 0.72f), new Color(0f, 0f, 0f, 0.05f));
                sci.raycastTarget = false;
                MenuTheme.Stretch((RectTransform)sc.transform);
            }
            else img.color = d.color;

            var title = MenuTheme.Label(card.transform, d.label, 20, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            MenuTheme.GoldMetalText(title); // oro metálico (identidad del menú)
            CardShadow(title);
            MenuTheme.Anchor((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -36f), new Vector2(-16f, -8f));

            var starsGo = new GameObject("Stars", typeof(RectTransform), typeof(Text));
            starsGo.transform.SetParent(card.transform, false);
            var starsTxt = starsGo.GetComponent<Text>();
            starsTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // glifos ★/☆ fiables
            starsTxt.fontSize = 16;
            starsTxt.alignment = TextAnchor.MiddleLeft;
            starsTxt.supportRichText = true;
            starsTxt.text = StarsRichText(d.stars);
            CardShadow(starsTxt);
            MenuTheme.Anchor((RectTransform)starsGo.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 10f), new Vector2(80f, -38f));

            var desc = MenuTheme.Label(card.transform, d.desc, 15, new Color(0.95f, 0.93f, 0.85f), TextAnchor.MiddleLeft);
            CardShadow(desc);
            MenuTheme.Anchor((RectTransform)desc.transform, new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(84f, 10f), new Vector2(0f, -38f));

            var status = MenuTheme.Label(card.transform, d.completed ? "COMPLETADA ✓" : "DISPONIBLE", 16,
                d.completed ? new Color(0.6f, 0.95f, 0.6f) : new Color(0.95f, 0.9f, 0.7f), TextAnchor.MiddleRight, FontStyle.Bold);
            CardShadow(status);
            MenuTheme.Anchor((RectTransform)status.transform, new Vector2(0.68f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-16f, 0f));
        }

        /// <summary>Sombra negra bajo un texto para que resalte sobre la imagen del recuadro.</summary>
        private static void CardShadow(Graphic g)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.9f);
            s.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static string StarsRichText(int filled)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#FFFFFF>");
            for (int i = 0; i < filled; i++) sb.Append('★');
            sb.Append("</color><color=#665544>");
            for (int i = filled; i < 4; i++) sb.Append('☆');
            sb.Append("</color>");
            return sb.ToString();
        }

        // --- CONTRA IA: seleccionar mazo ---

        private RectTransform BuildContraIA()
        {
            var screen = NewScreen("ContraIA", "fondo_constructor", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.5f));
            Title(screen, "ELIGE TU HISTORIA");
            BackButton(screen);

            var list = MenuTheme.VBox(screen, 10f, 0, TextAnchor.UpperCenter);
            MenuTheme.Anchor((RectTransform)list.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-260f, 80f), new Vector2(260f, -90f));

            MenuTheme.Label(list.transform, "MIS MAZOS", 18, MenuTheme.GoldDim).gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            var mios = PlayerData.Mazos();
            if (mios.Count == 0)
                MenuTheme.Label(list.transform, "(sin mazos guardados)", 15, new Color(0.7f, 0.65f, 0.5f)).gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            foreach (var m in mios)
            {
                var deck = m;
                var b = MenuTheme.TextButton(list.transform, deck.nombre, 18, () => { PlayerData.SelectedHistoriaId = deck.historiaId; PlayerData.SelectedDeck = deck.cartas; LaunchGame(); }, 460f, 50f);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            }

            MenuTheme.Label(list.transform, "PREDEFINIDOS", 18, MenuTheme.GoldDim).gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            foreach (var (label, act) in HistoriaButtons())
            {
                var b = MenuTheme.TextButton(list.transform, label, 17, act, 460f, 48f);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            }
            return screen;
        }

        // --- MIS MAZOS (grid de "libros", uno por mazo guardado) ---

        private static readonly Dictionary<string, Color> HistoriaColor = new()
        {
            { "h1", new Color(0.55f, 0.10f, 0.35f) }, { "h2", new Color(0.10f, 0.25f, 0.55f) },
            { "h3", new Color(0.55f, 0.42f, 0.08f) }, { "h4", new Color(0.55f, 0.28f, 0.08f) },
            { "h5", new Color(0.45f, 0.10f, 0.08f) }, { "h6", new Color(0.30f, 0.12f, 0.45f) },
            { "h7", new Color(0.50f, 0.40f, 0.10f) },
        };

        private static string HistoriaName(string id) => id switch
        {
            "h1" => "La Caída del Edén", "h2" => "El Diluvio Universal", "h3" => "La Alianza con Abraham",
            "h4" => "La Destrucción de Sodoma", "h5" => "El Primer Fratricidio", "h6" => "La Escalera al Cielo",
            "h7" => "José, el Salvador de Egipto", _ => id ?? "Sin historia"
        };

        private RectTransform BuildMisMazos()
        {
            var screen = NewScreen("MisMazos", "creador/Fondo", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.15f)); // fondo más iluminado (como el Godot)
            Title(screen, "MIS HISTORIAS"); // mismo rótulo que usa el Godot para esta pantalla
            BackButton(screen);
            DecorBook(screen);

            var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
            grid.transform.SetParent(screen, false);
            MenuTheme.Anchor((RectTransform)grid.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-480f, 40f), new Vector2(480f, -90f));
            grid.cellSize = new Vector2(220f, 300f);
            grid.spacing = new Vector2(18f, 18f);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

            // "Crear Nueva Historia" SIEMPRE primero: así nunca desaparece de la 1ª pantalla al
            // tener muchas historias (a diferencia del Godot, donde iba al final).
            BuildMazoNewCard(grid.transform, () => Push(Screen.ChooseHistoria));

            var mazosGuardados = PlayerData.Mazos();
            for (int i = 0; i < mazosGuardados.Count; i++)
            {
                int idx = i; // captura por valor: cada tarjeta abre el modal de SU mazo
                BuildMazoBookCard(grid.transform, mazosGuardados[i], () => ShowMazoOptions(idx));
            }
            return screen;
        }

        /// <summary>Tras elegir una HISTORIA: qué mazo usar para jugarla (mismos "libros" que Mis Mazos).</summary>
        private RectTransform BuildSelectDeck()
        {
            var screen = NewScreen("SelectDeck", "creador/Fondo", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.35f));
            Title(screen, "ELIGE TU HISTORIA");
            BackButton(screen);
            DecorBook(screen);

            var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
            grid.transform.SetParent(screen, false);
            MenuTheme.Anchor((RectTransform)grid.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-480f, 40f), new Vector2(480f, -90f));
            grid.cellSize = new Vector2(220f, 300f);
            grid.spacing = new Vector2(18f, 18f);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

            var hid = PlayerData.SelectedHistoriaId;
            foreach (var m in PlayerData.Mazos())
            {
                if (m.historiaId != hid) continue; // solo mazos de la historia elegida
                var deck = m;
                BuildMazoBookCard(grid.transform, deck, () => { PlayerData.SelectedDeck = deck.cartas; LaunchGame(); });
            }
            // Sin mazo propio guardado: se usa el mismo mazo por defecto que arma el rival IA
            // para esa historia (SampleDeckBuilder), no un mazo al azar.
            BuildMazoNewCard(grid.transform, () => { PlayerData.SelectedDeck = null; LaunchGame(); },
                             plusLabel: "★", bottomLabel: "MAZO\nPOR DEFECTO", useCreateAsset: false);
            return screen;
        }

        /// <summary>"Elige tu historia": al crear una nueva historia, muestra las cartas HISTORIA
        /// para elegir la que guiará el mazo. SIGUIENTE se habilita al seleccionar una.</summary>
        private RectTransform BuildChooseHistoria()
        {
            var screen = NewScreen("ChooseHistoria", "fondo_constructor", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.25f));
            Title(screen, "ELIGE TU HISTORIA");
            BackButton(screen);

            _chosenHistoriaId = null;

            // 7 historias en 2 filas centradas (4 arriba, 3 abajo) para que quede simétrico.
            var stories = new List<HistoriaCard>();
            foreach (var d in Historias) if (d.historiaId != null) stories.Add(d);

            const float cellW = 264f, cellH = 189f;

            HorizontalLayoutGroup MakeRow(string nm, float topY)
            {
                var go = new GameObject(nm, typeof(RectTransform), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(screen, false);
                var hl = go.GetComponent<HorizontalLayoutGroup>();
                hl.spacing = 20f;
                hl.childAlignment = TextAnchor.MiddleCenter; // centra las cartas en la fila
                hl.childControlWidth = false; hl.childControlHeight = false;
                hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
                // fila de ancho fijo centrada horizontalmente (top-anchored).
                MenuTheme.Anchor((RectTransform)go.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-600f, topY - cellH), new Vector2(600f, topY));
                return hl;
            }
            var row1 = MakeRow("Row1", -170f);
            var row2 = MakeRow("Row2", -170f - cellH - 22f);

            var outlines = new List<Outline>();
            Button siguiente = null;

            for (int i = 0; i < stories.Count; i++)
            {
                var d = stories[i];
                string id = d.historiaId;
                var carta = MenuAssets.Sprite("cartas_historia/carta_" + id);
                var rowParent = i < 4 ? row1.transform : row2.transform;

                var cardGo = new GameObject("HistCard_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(rowParent, false);
                ((RectTransform)cardGo.transform).sizeDelta = new Vector2(cellW, cellH);
                var le = cardGo.AddComponent<LayoutElement>();
                le.preferredWidth = cellW; le.preferredHeight = cellH;
                var bg = cardGo.GetComponent<Image>();
                bg.sprite = MenuGraphics.Rounded(64, 12);
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.06f, 0.06f, 0.12f, 0.9f);

                if (carta != null)
                {
                    var ph = new GameObject("Carta", typeof(RectTransform), typeof(Image));
                    ph.transform.SetParent(cardGo.transform, false);
                    var phi = ph.GetComponent<Image>();
                    phi.sprite = carta; phi.preserveAspect = true; phi.raycastTarget = false;
                    MenuTheme.Anchor((RectTransform)ph.transform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
                }
                else
                {
                    var lbl = MenuTheme.Label(cardGo.transform, d.label, 16, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                    MenuTheme.Stretch((RectTransform)lbl.transform);
                }

                var outline = cardGo.AddComponent<Outline>(); // marco dorado de selección
                outline.effectColor = MenuTheme.Gold;
                outline.effectDistance = new Vector2(3f, -3f);
                outline.enabled = false;
                outlines.Add(outline);

                cardGo.AddComponent<HoverScale>();
                var cbtn = cardGo.GetComponent<Button>();
                cbtn.targetGraphic = bg;
                cbtn.onClick.AddListener(() =>
                {
                    _chosenHistoriaId = id;
                    foreach (var o in outlines) o.enabled = false;
                    outline.enabled = true;
                    if (siguiente != null) siguiente.interactable = true;
                });
            }

            // SIGUIENTE (abajo-derecha): con la carta elegida, pasa al armador de mazos.
            siguiente = MenuTheme.TextButton(screen, "SIGUIENTE", 16, () =>
            {
                if (_chosenHistoriaId == null) return;
                Push(Screen.DeckBuilder);
            }, 200f, 50f, thicken: false);
            siguiente.interactable = false;
            MenuTheme.Anchor((RectTransform)siguiente.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-224f, 22f), new Vector2(-24f, 72f));

            return screen;
        }

        /// <summary>Decoración esquina inferior-derecha (libro cerrado), como en la referencia.</summary>
        private void DecorBook(RectTransform screen)
        {
            var sp = MenuAssets.Sprite("creador/LibroDecor");
            if (sp == null) return;
            var img = MenuTheme.Picture(screen, "DecorBook", sp);
            img.raycastTarget = false;
            // más grande y más al rincón inferior-derecho (como el Godot).
            MenuTheme.Anchor((RectTransform)img.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-418f, -160f), new Vector2(100f, 301f));
        }

        // --- opciones de un mazo guardado (modal: Editar/Portada/Renombrar/Borrar/Cerrar) ---

        private void ShowMazoOptions(int index)
        {
            var mazos = PlayerData.Mazos();
            if (index < 0 || index >= mazos.Count) return;
            var m = mazos[index];

            CloseModal();
            EnsureCatalog();
            var overlay = MenuTheme.Panel(_root, "MazoModal");
            overlay.transform.SetAsLastSibling();
            _modal = overlay.gameObject;
            var dim = MenuTheme.Rect(overlay, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseModal());

            // panel propio, acorde al resto del menú: navy redondeado + borde dorado.
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(overlay, false);
            panel.sprite = MenuGraphics.Rounded(64, 16); panel.type = Image.Type.Sliced;
            panel.color = new Color(0.06f, 0.06f, 0.14f, 0.98f);
            panel.gameObject.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(720f, 470f);

            // carta representativa DENTRO del panel (izquierda)
            var prevBox = new GameObject("Prev", typeof(RectTransform)).GetComponent<RectTransform>();
            prevBox.SetParent(panel.transform, false);
            prevBox.anchorMin = prevBox.anchorMax = new Vector2(0f, 0.5f); prevBox.pivot = new Vector2(0f, 0.5f);
            prevBox.sizeDelta = new Vector2(224f, 330f); prevBox.anchoredPosition = new Vector2(30f, 0f);
            // Muestra la PORTADA elegida (compartida con el modal de guardado): arte si es carta
            // HISTORIA; ficha real si es una carta del catálogo; si no, un DÍA como respaldo.
            string ins = string.IsNullOrEmpty(m.insignia) ? (m.historiaId ?? "") : m.insignia;
            var portadaArt = MenuAssets.Sprite("cartas_historia/carta_" + ins);
            if (portadaArt != null)
            {
                var box = new GameObject("Portada", typeof(RectTransform), typeof(Image));
                box.transform.SetParent(prevBox, false);
                var bimg = box.GetComponent<Image>();
                bimg.sprite = MenuGraphics.Rounded(64, 10); bimg.type = Image.Type.Sliced; bimg.color = MenuTheme.MetalGold;
                MenuTheme.Stretch((RectTransform)box.transform);
                var pic = MenuTheme.Picture(box.transform, "Art", portadaArt, preserveAspect: true);
                pic.raycastTarget = false;
                MenuTheme.Anchor((RectTransform)pic.transform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            }
            else if (_catalog != null && _catalog.Cards.TryGetValue(ins, out var insDef))
                BuildCardPreview(prevBox, insDef);
            else
            {
                int diaNum = 1 + System.Math.Abs((m.nombre + "|" + m.historiaId).GetHashCode()) % 7;
                if (_catalog != null && _catalog.TryGet("dia" + diaNum, out var diaDef)) BuildCardPreview(prevBox, diaDef);
            }

            // lado derecho: título + resumen + botones (más cortos)
            var title = MenuTheme.Label(panel.transform, m.nombre, 26, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(272f, -58f), new Vector2(-24f, -14f));

            var sub = MenuTheme.Label(panel.transform, $"Historia: {HistoriaName(m.historiaId)}\n{m.cartas.Count} cartas", 15, new Color(0.85f, 0.82f, 0.72f), TextAnchor.UpperCenter);
            MenuTheme.Anchor((RectTransform)sub.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(272f, -108f), new Vector2(-24f, -60f));

            var list = MenuTheme.VBox(panel.transform, 10f, 0, TextAnchor.UpperCenter);
            MenuTheme.Anchor((RectTransform)list.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(272f, 24f), new Vector2(-24f, -116f));
            AddModalOption(list.transform, "EDITAR CARTAS", MenuTheme.Gold, () => { CloseModal(); Push(Screen.DeckBuilder); }, width: 320f);
            AddModalOption(list.transform, "CAMBIAR PORTADA", MenuTheme.Gold, () => ShowChangePortadaDialog(index), width: 320f);
            AddModalOption(list.transform, "RENOMBRAR", MenuTheme.Gold, () => ShowRenameDialog(index), width: 320f);
            AddModalOption(list.transform, "BORRAR", new Color(0.9f, 0.32f, 0.32f), () => { DeleteMazo(index); CloseModal(); RefreshMisMazos(); }, width: 320f);
            AddModalOption(list.transform, "CERRAR", new Color(0.8f, 0.78f, 0.7f), CloseModal, width: 320f);
        }

        /// <summary>Panel propio (navy + borde dorado) centrado, con título. Devuelve el panel.</summary>
        private RectTransform CustomPanel(RectTransform overlay, string titleText, float w, float h)
        {
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(overlay, false);
            panel.sprite = MenuGraphics.Rounded(64, 16); panel.type = Image.Type.Sliced;
            panel.color = new Color(0.06f, 0.06f, 0.14f, 0.98f);
            panel.gameObject.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(w, h);
            var title = MenuTheme.Label(panel.transform, titleText, 24, MenuTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -54f), new Vector2(0f, -14f));
            return prt;
        }

        private void ShowChangePortadaDialog(int index)
        {
            var mazos = PlayerData.Mazos();
            if (index < 0 || index >= mazos.Count) return;
            var m = mazos[index];
            EnsureCatalog();

            CloseModal();
            var overlay = MenuTheme.Panel(_root, "PortadaModal");
            overlay.transform.SetAsLastSibling();
            _modal = overlay.gameObject;
            var dim = MenuTheme.Rect(overlay, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseModal());

            var panel = CustomPanel(overlay, "CAMBIAR PORTADA", 660f, 520f);

            var uniq = new List<string>(); var seen = new HashSet<string>();
            foreach (var cid in m.cartas) if (seen.Add(cid)) uniq.Add(cid);
            string hid = m.historiaId ?? "";
            string sel = string.IsNullOrEmpty(m.insignia) ? hid : m.insignia;

            var content = MakeScroll(panel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 84f), new Vector2(-24f, -70f), true);
            var ig = content.gameObject.AddComponent<GridLayoutGroup>();
            ig.cellSize = new Vector2(84f, 104f); ig.spacing = new Vector2(8f, 8f);
            ig.constraint = GridLayoutGroup.Constraint.FixedColumnCount; ig.constraintCount = 6;
            ig.childAlignment = TextAnchor.UpperLeft;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BuildPortadaTiles(content, hid, uniq, sel, id => sel = id);

            var guardar = ChipButton(panel, "GUARDAR PORTADA");
            var grt = (RectTransform)guardar.transform;
            grt.anchorMin = new Vector2(0.5f, 0f); grt.anchorMax = new Vector2(0.5f, 0f); grt.pivot = new Vector2(0.5f, 0f);
            grt.sizeDelta = new Vector2(300f, 52f); grt.anchoredPosition = new Vector2(0f, 20f);
            guardar.onClick.AddListener(() =>
            {
                var latest = PlayerData.Mazos();
                if (index >= 0 && index < latest.Count) { latest[index].insignia = sel; PlayerData.SaveMazos(latest); }
                CloseModal();
                RefreshMisMazos();
            });
        }

        private void ShowRenameDialog(int index)
        {
            var mazos = PlayerData.Mazos();
            if (index < 0 || index >= mazos.Count) return;
            var m = mazos[index];

            CloseModal();
            var overlay = MenuTheme.Panel(_root, "RenameModal");
            overlay.transform.SetAsLastSibling();
            _modal = overlay.gameObject;
            var dim = MenuTheme.Rect(overlay, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseModal());

            var panel = CustomPanel(overlay, "RENOMBRAR MAZO", 520f, 240f);

            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(panel, false);
            var inputImg = inputGo.GetComponent<Image>();
            inputImg.color = new Color(0.02f, 0.03f, 0.08f, 1f);
            MenuTheme.Anchor((RectTransform)inputGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -118f), new Vector2(-30f, -74f));
            var field = inputGo.GetComponent<InputField>();
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(inputGo.transform, false);
            var txt = textGo.GetComponent<Text>();
            txt.font = MenuAssets.Font(); txt.color = MenuTheme.Gold; txt.fontSize = 18; txt.alignment = TextAnchor.MiddleLeft; txt.supportRichText = false;
            MenuTheme.Anchor((RectTransform)textGo.transform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));
            field.textComponent = txt; field.text = m.nombre; field.characterLimit = 24;

            void DoSave()
            {
                var latest = PlayerData.Mazos();
                if (index >= 0 && index < latest.Count && !string.IsNullOrWhiteSpace(field.text))
                {
                    latest[index].nombre = field.text.Trim();
                    PlayerData.SaveMazos(latest);
                }
                CloseModal();
                RefreshMisMazos();
            }

            var save = AddModalOption(panel, "GUARDAR", MenuTheme.Gold, DoSave, width: 200f);
            MenuTheme.Anchor((RectTransform)save.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-210f, 24f), new Vector2(-10f, 72f));
            var cancel = AddModalOption(panel, "CANCELAR", new Color(0.8f, 0.78f, 0.7f), CloseModal, width: 200f);
            MenuTheme.Anchor((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(10f, 24f), new Vector2(210f, 72f));
        }

        private static void DeleteMazo(int index)
        {
            var mazos = PlayerData.Mazos();
            if (index < 0 || index >= mazos.Count) return;
            mazos.RemoveAt(index);
            PlayerData.SaveMazos(mazos);
        }

        /// <summary>Reconstruye la pantalla Mis Mazos in-place (tras borrar/renombrar).</summary>
        private void RefreshMisMazos()
        {
            if (_stack.Count > 0 && _stack[_stack.Count - 1] == Screen.MisMazos) Show(Screen.MisMazos);
        }

        /// <summary>
        /// Miniatura de carta REAL (arte + nombre + condición/efecto) flotando junto al panel del
        /// modal, superpuesta al borde izquierdo. Usa CardArtLibrary (misma carpeta que el campo).
        /// </summary>
        private void BuildCardPreview(RectTransform parent, CardDefinition def)
        {
            var box = new GameObject("CardPreview", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(parent, false);
            var bimg = box.GetComponent<Image>();
            bimg.sprite = MenuGraphics.Rounded(64, 10);
            bimg.type = Image.Type.Sliced;
            bimg.color = MenuTheme.MetalGold;
            var brt = (RectTransform)box.transform;
            MenuTheme.Stretch(brt); // llena el contenedor

            var innGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innGo.transform.SetParent(brt, false);
            var iimg = innGo.GetComponent<Image>();
            iimg.sprite = MenuGraphics.Rounded(64, 8);
            iimg.type = Image.Type.Sliced;
            iimg.color = new Color(0.04f, 0.05f, 0.10f, 0.98f);
            iimg.raycastTarget = false;
            var irt = (RectTransform)innGo.transform;
            MenuTheme.Anchor(irt, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            // Orden vertical (como la referencia): título arriba → imagen en medio → condición/efecto abajo.
            var title = MenuTheme.Label(irt, def.Nombre, 16, MenuTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)title.transform, new Vector2(0f, 0.87f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-40f, 0f));

            var texture = _cardArt != null && _cardArt.Available ? _cardArt.Front(def.Nombre) : null;
            if (texture != null)
            {
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                var pic = MenuTheme.Picture(irt, "Art", sprite, preserveAspect: false);
                pic.raycastTarget = false;
                MenuTheme.Anchor((RectTransform)pic.transform, new Vector2(0f, 0.34f), new Vector2(1f, 0.85f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
            }

            var badge = MenuTheme.Rect(irt, "Badge", MenuTheme.MetalGold);
            badge.sprite = MenuGraphics.Rounded(32, 16);
            var badgeRt = (RectTransform)badge.transform;
            badgeRt.sizeDelta = new Vector2(30f, 30f);
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-18f, -18f);
            var idxLbl = MenuTheme.Label(badge.transform, def.Id.Replace("dia", ""), 14, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Stretch((RectTransform)idxLbl.transform);

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(def.Condicion)) lines.Add($"Condición: {def.Condicion}");
            if (!string.IsNullOrEmpty(def.Efecto)) lines.Add($"Efecto: {def.Efecto}");
            var info = MenuTheme.Label(irt, string.Join("\n\n", lines), 11, new Color(0.85f, 0.82f, 0.72f), TextAnchor.UpperLeft);
            MenuTheme.Anchor((RectTransform)info.transform, new Vector2(0f, 0f), new Vector2(1f, 0.32f), new Vector2(8f, 6f), new Vector2(-8f, -4f));
        }

        /// <summary>Panel modal: usa minimenu/fondo (marco real) si está disponible; si no, fallback
        /// procedural (marco dorado redondeado + interior oscuro).</summary>
        private RectTransform ModalPanel(RectTransform overlay, float width, float height, out RectTransform inner)
        {
            var fondoSprite = MenuAssets.Sprite("minimenu/fondo");
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay, false);
            var pimg = panel.GetComponent<Image>();
            var prt = (RectTransform)panel.transform;

            if (fondoSprite != null)
            {
                pimg.sprite = fondoSprite;
                pimg.type = Image.Type.Sliced;
                pimg.color = Color.white;
                MenuTheme.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width / 2f, -height / 2f), new Vector2(width / 2f, height / 2f));
                var innGo0 = new GameObject("Inner", typeof(RectTransform));
                innGo0.transform.SetParent(panel.transform, false);
                inner = (RectTransform)innGo0.transform;
                MenuTheme.Anchor(inner, Vector2.zero, Vector2.one, new Vector2(28f, 28f), new Vector2(-28f, -28f));
                return prt;
            }

            pimg.sprite = MenuGraphics.Rounded(64, 18);
            pimg.type = Image.Type.Sliced;
            pimg.color = MenuTheme.MetalGold;
            MenuTheme.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width / 2f, -height / 2f), new Vector2(width / 2f, height / 2f));

            var innGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innGo.transform.SetParent(panel.transform, false);
            var iimg = innGo.GetComponent<Image>();
            iimg.sprite = MenuGraphics.Rounded(64, 16);
            iimg.type = Image.Type.Sliced;
            iimg.color = new Color(0.05f, 0.06f, 0.12f, 0.98f);
            iimg.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)innGo.transform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            inner = (RectTransform)innGo.transform;
            return prt;
        }

        /// <summary>Opción del modal. Si <paramref name="spriteKey"/> carga, usa esa imagen (icono+texto
        /// baked); si no, cae al botón de texto plano.</summary>
        private Button AddModalOption(Transform parent, string label, Color color, System.Action onClick,
                                      float width = 440f, string spriteKey = null)
        {
            // Mismo diseño que el botón VOLVER: recuadro Botones.png + texto oro metálico
            // (rojo plano para acciones de peligro como BORRAR).
            var go = new GameObject("Opt_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, 48f);
            var img = go.GetComponent<Image>();
            var rec = MenuAssets.Sprite("Botones");
            if (rec != null) { img.sprite = rec; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = new Color(0.06f, 0.05f, 0.02f, 0.92f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cols = btn.colors;
            cols.normalColor = Color.white;
            cols.highlightedColor = new Color(1.15f, 1.1f, 0.95f, 1f);
            cols.pressedColor = new Color(0.85f, 0.8f, 0.6f, 1f);
            btn.colors = cols;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            go.AddComponent<HoverScale>();
            go.AddComponent<LayoutElement>().preferredHeight = 48f;

            bool danger = color.r > color.g + 0.15f; // rojo (BORRAR)
            var txt = MenuTheme.Label(rt, label, 18, danger ? color : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            txt.raycastTarget = false;
            MenuTheme.Stretch((RectTransform)txt.transform);
            if (!danger)
            {
                txt.gameObject.AddComponent<MetallicGoldGradient>(); // oro metálico como VOLVER
                go.AddComponent<GlintOnHover>();                     // reflejo al pasar el cursor (como el menú principal)
            }
            return btn;
        }

        /// <summary>Marco "libro" común. Usa Mazo.png si está disponible; si no, fallback procedural
        /// (borde dorado redondeado + interior negro).</summary>
        private RectTransform BookFrame(Transform parent, out RectTransform inner)
        {
            var go = new GameObject("Book", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            var mazoSprite = MenuAssets.Sprite("creador/Mazo");

            RectTransform inn;
            if (mazoSprite != null)
            {
                img.sprite = mazoSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;

                var innGo = new GameObject("Inner", typeof(RectTransform));
                innGo.transform.SetParent(go.transform, false);
                inn = (RectTransform)innGo.transform;
                MenuTheme.Anchor(inn, Vector2.zero, Vector2.one, new Vector2(24f, 30f), new Vector2(-24f, -30f));
            }
            else
            {
                img.sprite = MenuGraphics.Rounded(64, 14);
                img.type = Image.Type.Sliced;
                img.color = MenuTheme.MetalGold;

                var innGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
                innGo.transform.SetParent(go.transform, false);
                var iimg = innGo.GetComponent<Image>();
                iimg.sprite = MenuGraphics.Rounded(64, 12);
                iimg.type = Image.Type.Sliced;
                iimg.color = Color.black;
                iimg.raycastTarget = false;
                inn = (RectTransform)innGo.transform;
                MenuTheme.Anchor(inn, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            }

            inner = inn;
            return (RectTransform)go.transform;
        }

        private void BuildMazoBookCard(Transform parent, DeckEntry m, System.Action onClick)
        {
            var card = BookFrame(parent, out var inner);
            var hid = m.historiaId ?? ""; // saneo: datos viejos podrían no traer historiaId

            var coverColor = HistoriaColor.TryGetValue(hid, out var c) ? c : MenuTheme.PanelBg;
            // Portada = carta INSIGNIA elegida. Si la insignia tiene arte (carta HISTORIA) se muestra
            // la imagen; si no (DÍA/normal) se muestra su ficha (color+nombre). Fallback: carta historia.
            string ins = string.IsNullOrEmpty(m.insignia) ? hid : m.insignia;
            var insArt = MenuAssets.Sprite("cartas_historia/carta_" + ins);
            Image cover;
            if (insArt != null)
            {
                cover = MenuTheme.Picture(inner, "Cover", insArt, preserveAspect: false);
                cover.color = Color.white;
            }
            else if (_catalog != null && _catalog.Cards.TryGetValue(ins, out var insDef))
            {
                cover = MenuTheme.Rect(inner, "Cover", TypeColorDeck(insDef.Type));
                cover.color = TypeColorDeck(insDef.Type);
                var tl = MenuTheme.Label(cover.transform, TypeLabel(insDef.Type), 9, new Color(0.85f, 0.82f, 0.7f), TextAnchor.UpperLeft, FontStyle.Bold);
                MenuTheme.Anchor((RectTransform)tl.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -16f), new Vector2(-6f, -3f));
                var nm = MenuTheme.Label(cover.transform, insDef.Nombre, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                MenuTheme.Anchor((RectTransform)nm.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 100f), new Vector2(-6f, -22f));
            }
            else
            {
                var hojaSprite = MenuAssets.Sprite("creador/Hoja");
                cover = hojaSprite != null
                    ? MenuTheme.Picture(inner, "Cover", hojaSprite, preserveAspect: false)
                    : MenuTheme.Rect(inner, "Cover", coverColor);
                cover.color = coverColor;
            }
            cover.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)cover.transform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f)); // la carta llena todo el interior

            // scrim inferior para legibilidad del texto del mazo
            var scrim = MenuTheme.Rect(cover.transform, "Scrim", Color.white);
            scrim.sprite = MenuGraphics.VGradient(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.9f));
            scrim.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)scrim.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 96f));

            var badge = MenuTheme.Rect(inner, "Badge", MenuTheme.MetalGold);
            badge.sprite = MenuGraphics.Rounded(32, 16);
            var brt = (RectTransform)badge.transform;
            brt.sizeDelta = new Vector2(28f, 28f);
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-20f, -20f);
            var idxLabel = MenuTheme.Label(badge.transform, string.IsNullOrEmpty(hid) ? "?" : hid.Replace("h", ""), 14, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Stretch((RectTransform)idxLabel.transform);

            // nombre del mazo + nº de cartas, centrados sobre el scrim inferior
            var nameLbl = MenuTheme.Label(inner, m.nombre.ToUpperInvariant(), 16, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)nameLbl.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 44f), new Vector2(-6f, 84f));

            var countLbl = MenuTheme.Label(inner, $"{m.cartas.Count} CARTAS · MAZO", 11, new Color(0.82f, 0.78f, 0.66f), TextAnchor.MiddleCenter);
            MenuTheme.Anchor((RectTransform)countLbl.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 22f), new Vector2(-6f, 44f));

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick());
            card.gameObject.AddComponent<HoverScale>();
        }

        private void BuildMazoNewCard(Transform parent, System.Action onClick,
                                      string plusLabel = "+", string bottomLabel = "CREAR NUEVA\nHISTORIA",
                                      bool useCreateAsset = true)
        {
            var createSprite = useCreateAsset ? MenuAssets.Sprite("creador/BotonCrearNueva") : null;
            GameObject card;
            if (createSprite != null)
            {
                // La imagen ya trae el icono + texto "CREAR NUEVA HISTORIA" baked.
                card = new GameObject("Book_Nuevo", typeof(RectTransform), typeof(Image));
                card.transform.SetParent(parent, false);
                var img = card.GetComponent<Image>();
                img.sprite = createSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                var cardRt = BookFrame(parent, out var inner);
                card = cardRt.gameObject;
                var plus = MenuTheme.Label(inner, plusLabel, 40, MenuTheme.MetalGold, TextAnchor.MiddleCenter, FontStyle.Bold);
                MenuTheme.Anchor((RectTransform)plus.transform, new Vector2(0f, 0.35f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
                var lbl = MenuTheme.Label(inner, bottomLabel, 14, new Color(0.85f, 0.82f, 0.7f), TextAnchor.MiddleCenter, FontStyle.Bold);
                MenuTheme.Anchor((RectTransform)lbl.transform, new Vector2(0f, 0f), new Vector2(1f, 0.35f), new Vector2(6f, 6f), new Vector2(-6f, 0f));
            }

            var btn = card.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick());
            card.AddComponent<HoverScale>();
        }

        // --- CONSTRUCTOR (resumen 3 pasos) ---

        private RectTransform BuildDeckBuilder()
        {
            EnsureCatalog();
            var screen = NewScreen("DeckBuilder", "fondo_constructor", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.4f));

            string historiaId = _chosenHistoriaId;
            var piezas = HistoriaPiezas(historiaId);

            // --- estado del mazo ---
            var counts = new Dictionary<string, int>();
            const int MAXTOTAL = 40;

            // --- filtros ---
            CardType? typeFilter = null;
            int fdFilter = -1, tFilter = -1;
            bool historiaOnly = false;
            string search = "";

            // --- colección (sin cartas HISTORIA) ordenada por tipo y nombre ---
            // Las cartas DÍA y HISTORIA no van al mazo (las DÍA son externas y siempre se llevan;
            // no ocupan ninguno de los 40 espacios).
            var all = new List<CardDefinition>();
            if (_catalog != null)
                foreach (var c in _catalog.Cards.Values)
                    if (c.Type != CardType.Historia && c.Type != CardType.Dia) all.Add(c);
            all.Sort((a, b) => { int t = TypeOrder(a.Type).CompareTo(TypeOrder(b.Type)); return t != 0 ? t : string.Compare(a.Nombre, b.Nombre, System.StringComparison.OrdinalIgnoreCase); });

            // ===== panel derecho: MAZO =====
            var deckPanel = new GameObject("DeckPanel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            deckPanel.transform.SetParent(screen, false);
            deckPanel.sprite = MenuGraphics.Rounded(64, 14); deckPanel.type = Image.Type.Sliced;
            deckPanel.color = new Color(0.05f, 0.05f, 0.10f, 0.85f);
            deckPanel.gameObject.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            MenuTheme.Anchor((RectTransform)deckPanel.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-290f, 78f), new Vector2(-16f, -128f));

            var counterLbl = MenuTheme.Label(deckPanel.transform, "0 / 40", 22, MenuTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)counterLbl.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -42f), new Vector2(-10f, -8f));

            var deckContent = MakeScroll(deckPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-10f, -50f), true);
            var deckVL = deckContent.gameObject.AddComponent<VerticalLayoutGroup>();
            deckVL.spacing = 2f; deckVL.childForceExpandWidth = true; deckVL.childForceExpandHeight = false;
            deckVL.childControlWidth = true; deckVL.childControlHeight = true;
            deckContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ===== grid de la colección (izquierda) =====
            var gridContent = MakeScroll(screen, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 78f), new Vector2(-306f, -128f), true);
            var gl = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(120f, 156f); gl.spacing = new Vector2(9f, 9f); // 7 col × ~3 filas visibles
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 7;
            gl.childAlignment = TextAnchor.UpperLeft;
            gridContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            int Total() { int n = 0; foreach (var kv in counts) n += kv.Value; return n; }

            void RefreshDeck()
            {
                for (int i = deckContent.childCount - 1; i >= 0; i--) Destroy(deckContent.GetChild(i).gameObject);
                foreach (var kv in counts)
                {
                    if (kv.Value <= 0) continue;
                    string cid = kv.Key; int qty = kv.Value;
                    string nm = (_catalog != null && _catalog.Cards.TryGetValue(cid, out var dd)) ? dd.Nombre : cid;
                    var row = new GameObject("D_" + cid, typeof(RectTransform), typeof(Image));
                    row.transform.SetParent(deckContent, false);
                    var ri = row.GetComponent<Image>();
                    ri.color = new Color(0.12f, 0.12f, 0.20f, 0.7f);
                    row.AddComponent<LayoutElement>().preferredHeight = 26f;
                    var lab = MenuTheme.Label(row.transform, $"{nm}   x{qty}", 13, new Color(0.92f, 0.88f, 0.72f), TextAnchor.MiddleLeft);
                    MenuTheme.Anchor((RectTransform)lab.transform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
                    var pc = row.AddComponent<PointerClicks>();
                    pc.onLeft = () => { RemoveCard(cid); };
                }
                counterLbl.text = Total() + " / 40";
            }

            void AddCard(string id)
            {
                if (Total() >= MAXTOTAL) return;
                counts.TryGetValue(id, out var q);
                if (q >= CopiasPropias(id)) return; // no más copias de las que se poseen
                counts[id] = q + 1; RefreshDeck();
            }
            void RemoveCard(string id)
            {
                if (counts.TryGetValue(id, out var q) && q > 0)
                {
                    if (q - 1 <= 0) counts.Remove(id); else counts[id] = q - 1;
                    RefreshDeck();
                }
            }

            bool Match(CardDefinition c)
            {
                if (typeFilter.HasValue && c.Type != typeFilter.Value) return false;
                int fd = c.Type == CardType.Tierra ? (c.Fd ?? 0) : (c.Coste ?? 0);
                if (fdFilter >= 0) { if (fdFilter == 5) { if (fd < 5) return false; } else if (fd != fdFilter) return false; }
                int tt = c.Dur ?? 0;
                if (tFilter >= 0) { if (tFilter == 5) { if (tt < 5) return false; } else if (tt != tFilter) return false; }
                if (historiaOnly && (piezas == null || !piezas.Contains(NormName(c.Nombre)))) return false;
                if (search.Length > 0 && NormName(c.Nombre).IndexOf(NormName(search), System.StringComparison.Ordinal) < 0) return false;
                return true;
            }

            void RefreshGrid()
            {
                for (int i = gridContent.childCount - 1; i >= 0; i--) Destroy(gridContent.GetChild(i).gameObject);
                foreach (var c in all) if (Match(c)) BuildDeckTile(gridContent, c, AddCard, RemoveCard);
            }

            // ===== barra de filtros (arriba) =====
            var typeRow = new GameObject("TypeRow", typeof(RectTransform)).GetComponent<RectTransform>();
            typeRow.SetParent(screen, false);
            MenuTheme.Anchor(typeRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -56f), new Vector2(-16f, -14f));
            string[] tNames = { "TODOS", "TIERRA", "SER-HUMANO", "SER-DIVINO", "SER-ANIMAL", "CONCEPTO" };
            CardType?[] tVals = { null, CardType.Tierra, CardType.SerHumano, CardType.SerDivino, CardType.SerAnimal, CardType.Concepto };
            var typeChips = new List<Image>();
            float tx = 0f;
            for (int i = 0; i < tNames.Length; i++)
            {
                int gi = i; var val = tVals[i];
                float w = 34f + tNames[i].Length * 9.6f;
                var chip = MakeChip(typeRow, tNames[i], tx, w, i == 0, () =>
                {
                    typeFilter = val;
                    for (int k = 0; k < typeChips.Count; k++) SetChipOn(typeChips[k], k == gi);
                    RefreshGrid();
                });
                typeChips.Add(chip); tx += w + 10f;
            }

            // fila 2: FD / T / HISTORIA / buscador
            var row2 = new GameObject("FilterRow2", typeof(RectTransform)).GetComponent<RectTransform>();
            row2.SetParent(screen, false);
            MenuTheme.Anchor(row2, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -104f), new Vector2(-16f, -62f));

            float rx = 0f;
            MakeTag(row2, "FD:", ref rx);
            string[] fdN = { "+", "1", "2", "3", "4", "5+" }; int[] fdV = { -1, 1, 2, 3, 4, 5 };
            var fdChips = new List<Image>();
            for (int i = 0; i < fdN.Length; i++)
            {
                int gi = i; int val = fdV[i];
                var chip = MakeChip(row2, fdN[i], rx, 44f, i == 0, () =>
                {
                    fdFilter = val;
                    for (int k = 0; k < fdChips.Count; k++) SetChipOn(fdChips[k], k == gi);
                    RefreshGrid();
                });
                fdChips.Add(chip); rx += 48f;
            }
            rx += 16f;
            MakeTag(row2, "T:", ref rx);
            string[] tN = { "+", "2", "3", "4", "5+" }; int[] tV = { -1, 2, 3, 4, 5 };
            var tChips = new List<Image>();
            for (int i = 0; i < tN.Length; i++)
            {
                int gi = i; int val = tV[i];
                var chip = MakeChip(row2, tN[i], rx, 44f, i == 0, () =>
                {
                    tFilter = val;
                    for (int k = 0; k < tChips.Count; k++) SetChipOn(tChips[k], k == gi);
                    RefreshGrid();
                });
                tChips.Add(chip); rx += 48f;
            }
            rx += 18f;
            Image histChip = null;
            histChip = MakeChip(row2, "HISTORIA", rx, 118f, false, () =>
            {
                historiaOnly = !historiaOnly;
                SetChipOn(histChip, historiaOnly);
                RefreshGrid();
            });
            rx += 132f;

            // buscador por nombre
            var searchGo = new GameObject("Search", typeof(RectTransform), typeof(Image), typeof(InputField));
            searchGo.transform.SetParent(row2, false);
            var srt = (RectTransform)searchGo.transform;
            srt.anchorMin = new Vector2(0f, 0.5f); srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f); srt.anchoredPosition = new Vector2(rx, 0f); srt.sizeDelta = new Vector2(280f, 36f);
            var simg = searchGo.GetComponent<Image>(); simg.color = new Color(0.02f, 0.03f, 0.08f, 0.9f);
            var sfield = searchGo.GetComponent<InputField>();
            var sph = MenuTheme.Label(searchGo.transform, "Buscar carta…", 13, new Color(0.6f, 0.58f, 0.5f), TextAnchor.MiddleLeft);
            MenuTheme.Anchor((RectTransform)sph.transform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            var stext = MenuTheme.Label(searchGo.transform, "", 13, new Color(0.95f, 0.92f, 0.8f), TextAnchor.MiddleLeft);
            MenuTheme.Anchor((RectTransform)stext.transform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            sfield.textComponent = stext; sfield.placeholder = sph; sfield.targetGraphic = simg;
            sfield.onValueChanged.AddListener(v => { search = v ?? ""; RefreshGrid(); });

            // ===== SIGUIENTE: abre el modal para nombrar, elegir carta insignia y guardar =====
            var sig = MenuTheme.TextButton(screen, "SIGUIENTE", 16, () => ShowSaveDeckDialog(historiaId, counts), 200f, 50f, thicken: false);
            MenuTheme.Anchor((RectTransform)sig.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-224f, 18f), new Vector2(-24f, 68f));

            // VOLVER también abajo (izquierda).
            var volver = MenuTheme.TextButton(screen, "VOLVER", 14, Pop, 130f, 48f, thicken: false);
            MenuTheme.Anchor((RectTransform)volver.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 18f), new Vector2(154f, 66f));

            RefreshDeck();
            RefreshGrid();
            return screen;
        }

        /// <summary>Ficha de carta en el grid del constructor: color por tipo, nombre, coste; click
        /// izq = añade, click der / mantener = vista flotante con +/-.</summary>
        private void BuildDeckTile(Transform parent, CardDefinition c, System.Action<string> add, System.Action<string> remove)
        {
            var tile = new GameObject("T_" + c.Id, typeof(RectTransform), typeof(Image));
            tile.transform.SetParent(parent, false);
            var bg = tile.GetComponent<Image>();
            bg.sprite = MenuGraphics.Rounded(48, 8); bg.type = Image.Type.Sliced;
            bg.color = TypeColorDeck(c.Type);
            tile.AddComponent<Outline>().effectColor = new Color(0.7f, 0.6f, 0.3f, 0.5f);

            var tl = MenuTheme.Label(tile.transform, TypeLabel(c.Type), 8, new Color(0.85f, 0.82f, 0.7f), TextAnchor.UpperLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)tl.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -16f), new Vector2(-6f, -3f));

            var nm = MenuTheme.Label(tile.transform, c.Nombre, 11, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)nm.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(5f, 16f), new Vector2(-5f, -16f));

            var cost = MenuTheme.Label(tile.transform, CostText(c), 9, new Color(0.95f, 0.9f, 0.7f), TextAnchor.LowerRight, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)cost.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 3f), new Vector2(-6f, 16f));

            // copias poseídas (arriba-derecha)
            var own = MenuTheme.Label(tile.transform, "x" + CopiasPropias(c.Id), 10, new Color(0.85f, 0.92f, 0.7f), TextAnchor.UpperRight, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)own.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -16f), new Vector2(-6f, -3f));

            var pc = tile.AddComponent<PointerClicks>();
            pc.onLeft = () => add(c.Id);
            pc.onRight = () => ShowCardFloat(c, add, remove);
            // sin HoverScale: al agrandarse cerca de una orilla la máscara del scroll la recortaba.
        }

        /// <summary>Vista flotante de la carta (centro) con botones +/- para añadir/quitar del mazo.</summary>
        private void ShowCardFloat(CardDefinition c, System.Action<string> add, System.Action<string> remove)
        {
            CloseModal();
            var overlay = MenuTheme.Panel(_root, "CardFloat");
            overlay.transform.SetAsLastSibling();
            _modal = overlay.gameObject;
            var dim = MenuTheme.Rect(overlay, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseModal()); // tocar fuera cierra

            // carta centrada, SIN marco ornamentado
            var card = new GameObject("BigCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(overlay, false);
            var ci = card.GetComponent<Image>();
            ci.sprite = MenuGraphics.Rounded(48, 14); ci.type = Image.Type.Sliced;
            ci.color = TypeColorDeck(c.Type);
            card.AddComponent<Outline>().effectColor = MenuTheme.Gold;
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(380f, 460f); crt.anchoredPosition = new Vector2(0f, 45f);

            var head = MenuTheme.Label(card.transform, c.Nombre, 22, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)head.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -58f), new Vector2(-12f, -12f));
            var sub = MenuTheme.Label(card.transform, TypeLabel(c.Type) + "   ·   " + CostText(c), 14, new Color(0.9f, 0.86f, 0.7f), TextAnchor.UpperCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)sub.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -82f), new Vector2(-12f, -58f));

            string body = "";
            if (!string.IsNullOrEmpty(c.Efecto)) body += c.Efecto + "\n";
            if (!string.IsNullOrEmpty(c.AlEntrar)) body += "Al entrar: " + c.AlEntrar + "\n";
            if (!string.IsNullOrEmpty(c.Activado)) body += "Activado: " + c.Activado + "\n";
            if (!string.IsNullOrEmpty(c.AlSalir)) body += "Al salir: " + c.AlSalir + "\n";
            if (!string.IsNullOrEmpty(c.Condicion)) body += "Condición: " + c.Condicion + "\n";
            var txt = MenuTheme.Label(card.transform, body.TrimEnd(), 14, new Color(0.95f, 0.93f, 0.85f), TextAnchor.UpperLeft);
            MenuTheme.Anchor((RectTransform)txt.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 14f), new Vector2(-16f, -92f));

            // botones +/- (diseño de chip) debajo de la carta
            var less = ChipButton(overlay, "−  Quitar");
            less.onClick.AddListener(() => remove(c.Id));
            var lrt = (RectTransform)less.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f); lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(170f, 48f); lrt.anchoredPosition = new Vector2(-95f, -215f);
            var more = ChipButton(overlay, "+  Añadir");
            more.onClick.AddListener(() => add(c.Id));
            var mrt = (RectTransform)more.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.5f); mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.sizeDelta = new Vector2(170f, 48f); mrt.anchoredPosition = new Vector2(95f, -215f);
        }

        /// <summary>Rellena una rejilla con las opciones de PORTADA (carta HISTORIA con arte + cartas
        /// DÍA + cartas del mazo). Marca la actual y notifica la selección.</summary>
        private void BuildPortadaTiles(Transform content, string historiaId, IEnumerable<string> deckUniqueIds, string current, System.Action<string> onSelect)
        {
            var outlines = new List<Outline>();
            void Add(string id, CardDefinition def)
            {
                var art = MenuAssets.Sprite("cartas_historia/carta_" + id); // solo la carta HISTORIA tiene arte
                var t = new GameObject("Ins_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
                t.transform.SetParent(content, false);
                var bg = t.GetComponent<Image>();
                bg.sprite = MenuGraphics.Rounded(48, 8); bg.type = Image.Type.Sliced;
                if (art != null)
                {
                    bg.color = Color.white;
                    var ph = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    ph.transform.SetParent(t.transform, false);
                    var pi = ph.GetComponent<Image>(); pi.sprite = art; pi.preserveAspect = true; pi.raycastTarget = false;
                    MenuTheme.Anchor((RectTransform)ph.transform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
                }
                else
                {
                    bg.color = def != null ? TypeColorDeck(def.Type) : new Color(0.15f, 0.13f, 0.2f, 1f);
                    var nm = MenuTheme.Label(t.transform, def != null ? def.Nombre : HistoriaName(id), 10, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                    MenuTheme.Anchor((RectTransform)nm.transform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                }
                var ol = t.AddComponent<Outline>(); ol.effectColor = MenuTheme.Gold; ol.effectDistance = new Vector2(2f, -2f);
                ol.enabled = (id == current);
                outlines.Add(ol);
                var b = t.GetComponent<Button>(); b.targetGraphic = bg;
                b.onClick.AddListener(() => { foreach (var o in outlines) o.enabled = false; ol.enabled = true; onSelect(id); });
            }

            Add(historiaId, null);                                                     // carta HISTORIA
            if (_catalog != null)
                foreach (var c in _catalog.Cards.Values) if (c.Type == CardType.Dia) Add(c.Id, c); // cartas DÍA
            foreach (var id in deckUniqueIds)
                if (_catalog != null && _catalog.Cards.TryGetValue(id, out var d)) Add(id, d); // cartas del mazo
        }

        /// <summary>Modal "GUARDAR MAZO": nombre + resumen + selección de portada + guardar.
        /// Aparece sobre el armador.</summary>
        private void ShowSaveDeckDialog(string historiaId, Dictionary<string, int> counts)
        {
            if (counts.Count == 0) return; // no guardar mazo vacío

            CloseModal();
            var overlay = MenuTheme.Panel(_root, "SaveDeck");
            overlay.transform.SetAsLastSibling();
            _modal = overlay.gameObject;
            var dim = MenuTheme.Rect(overlay, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseModal());

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(overlay, false);
            panel.sprite = MenuGraphics.Rounded(64, 16); panel.type = Image.Type.Sliced;
            panel.color = new Color(0.06f, 0.06f, 0.14f, 0.98f);
            panel.gameObject.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(660f, 540f);

            var title = MenuTheme.Label(panel.transform, "GUARDAR MAZO", 24, MenuTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(0f, -12f));

            var nameLbl = MenuTheme.Label(panel.transform, "Nombre del mazo:", 15, new Color(0.85f, 0.82f, 0.7f), TextAnchor.UpperLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)nameLbl.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -92f), new Vector2(-24f, -66f));

            // input de nombre
            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(panel.transform, false);
            var iimg = inputGo.GetComponent<Image>(); iimg.color = new Color(0.02f, 0.03f, 0.08f, 1f);
            MenuTheme.Anchor((RectTransform)inputGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -134f), new Vector2(-24f, -98f));
            var field = inputGo.GetComponent<InputField>();
            var itext = MenuTheme.Label(inputGo.transform, "", 16, new Color(0.95f, 0.92f, 0.8f), TextAnchor.MiddleLeft);
            MenuTheme.Anchor((RectTransform)itext.transform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            field.textComponent = itext; field.targetGraphic = iimg; field.text = "Mi Mazo";

            // divisor
            var div = MenuTheme.Rect(panel.transform, "Div", new Color(0.5f, 0.42f, 0.2f, 0.5f));
            MenuTheme.Anchor((RectTransform)div.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -152f), new Vector2(-24f, -150f));

            // historia + resumen por tipo
            var histLbl = MenuTheme.Label(panel.transform, "Historia: " + HistoriaName(historiaId), 16, MenuTheme.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)histLbl.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -182f), new Vector2(-24f, -158f));

            var byType = new Dictionary<CardType, int>();
            foreach (var kv in counts)
                if (_catalog != null && _catalog.Cards.TryGetValue(kv.Key, out var d))
                    { byType.TryGetValue(d.Type, out var q); byType[d.Type] = q + kv.Value; }
            var parts = new List<string>();
            foreach (CardType t in new[] { CardType.Tierra, CardType.SerHumano, CardType.SerDivino, CardType.SerAnimal, CardType.Concepto })
                if (byType.TryGetValue(t, out var n) && n > 0) parts.Add(TypeLabel(t) + " x" + n);
            var resumen = MenuTheme.Label(panel.transform, string.Join("   ·   ", parts), 14, new Color(0.85f, 0.82f, 0.72f), TextAnchor.UpperLeft);
            MenuTheme.Anchor((RectTransform)resumen.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -206f), new Vector2(-24f, -184f));

            // selección de carta insignia
            var insLbl = MenuTheme.Label(panel.transform, "Portada:", 15, new Color(0.85f, 0.82f, 0.7f), TextAnchor.UpperLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)insLbl.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -236f), new Vector2(-24f, -212f));

            var insContent = MakeScroll(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 84f), new Vector2(-24f, -242f), true);
            var ig = insContent.gameObject.AddComponent<GridLayoutGroup>();
            ig.cellSize = new Vector2(84f, 104f); ig.spacing = new Vector2(8f, 8f);
            ig.constraint = GridLayoutGroup.Constraint.FixedColumnCount; ig.constraintCount = 6;
            ig.childAlignment = TextAnchor.UpperLeft;
            insContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            string insignia = historiaId; // portada por defecto: la carta HISTORIA
            BuildPortadaTiles(insContent, historiaId, counts.Keys, insignia, id => insignia = id);

            // GUARDAR
            var guardar = ChipButton(panel.transform, "GUARDAR MAZO");
            var grt = (RectTransform)guardar.transform;
            grt.anchorMin = new Vector2(0.5f, 0f); grt.anchorMax = new Vector2(0.5f, 0f); grt.pivot = new Vector2(0.5f, 0f);
            grt.sizeDelta = new Vector2(300f, 52f); grt.anchoredPosition = new Vector2(0f, 20f);
            guardar.onClick.AddListener(() =>
            {
                var cartas = new List<string>();
                foreach (var kv in counts) for (int k = 0; k < kv.Value; k++) cartas.Add(kv.Key);
                string nombre = string.IsNullOrWhiteSpace(field.text) ? "Mi Mazo" : field.text.Trim();
                var mazos = PlayerData.Mazos();
                mazos.Add(new DeckEntry { nombre = nombre, historiaId = historiaId, cartas = cartas, insignia = insignia ?? "" });
                PlayerData.SaveMazos(mazos);
                CloseModal();
                if (_stack.Count > 0) _stack.RemoveAt(_stack.Count - 1);                         // quita DeckBuilder
                if (_stack.Count > 0 && _stack[_stack.Count - 1] == Screen.ChooseHistoria) _stack.RemoveAt(_stack.Count - 1);
                Show(_stack.Count > 0 ? _stack[_stack.Count - 1] : Screen.MisMazos);
            });
        }

        // ---- helpers del constructor ----

        private static int TypeOrder(CardType t) => t switch
        {
            CardType.Tierra => 0, CardType.SerHumano => 1, CardType.SerDivino => 2,
            CardType.SerAnimal => 3, CardType.Concepto => 4, CardType.Dia => 5, _ => 6
        };

        private static string TypeLabel(CardType t) => t switch
        {
            CardType.Tierra => "TIERRA", CardType.SerHumano => "SER-HUMANO", CardType.SerDivino => "SER-DIVINO",
            CardType.SerAnimal => "SER-ANIMAL", CardType.Concepto => "CONCEPTO", CardType.Dia => "DÍA", _ => ""
        };

        private static Color TypeColorDeck(CardType t) => t switch
        {
            CardType.Tierra => new Color(0.13f, 0.22f, 0.10f, 0.95f),
            CardType.SerHumano => new Color(0.10f, 0.14f, 0.32f, 0.95f),
            CardType.SerDivino => new Color(0.17f, 0.13f, 0.34f, 0.95f),
            CardType.SerAnimal => new Color(0.10f, 0.24f, 0.24f, 0.95f),
            CardType.Concepto => new Color(0.24f, 0.12f, 0.28f, 0.95f),
            CardType.Dia => new Color(0.28f, 0.22f, 0.10f, 0.95f),
            _ => new Color(0.15f, 0.15f, 0.18f, 0.95f)
        };

        /// <summary>Copias poseídas de una carta (adquiridas por sobres, sin tope). En esta versión: 3 de cada una.</summary>
        private static int CopiasPropias(string cardId) => 3;

        private static string CostText(CardDefinition c) => c.Type switch
        {
            CardType.Tierra => (c.Fd ?? 0) + " FD",
            CardType.SerHumano or CardType.SerDivino or CardType.SerAnimal => (c.Coste ?? 0) + "FD·" + (c.Dur ?? 0) + "T",
            _ => (c.Coste ?? 0) + " FD"
        };

        /// <summary>Nombres de las 5 piezas de cada historia (para el filtro HISTORIA), normalizados.</summary>
        private static readonly Dictionary<string, string[]> HistoriaPiezasRaw = new()
        {
            { "h1", new[] { "El Jardín del Edén", "El Árbol del Fruto Prohibido", "Adán", "Eva", "La Serpiente" } },
            { "h2", new[] { "El Monte Ararat", "Egipto", "Noé", "Sem", "Jafet" } },
            { "h3", new[] { "Hebrón/Mambré", "Canaán", "Abraham", "Sara", "Melquisedec" } },
            { "h4", new[] { "Sodoma", "Gomorra", "Abraham", "Lot", "Los Ángeles de Sodoma" } },
            { "h5", new[] { "El Jardín del Edén", "La Tierra de Nod", "Caín", "Abel", "La Maldición de la Tierra" } },
            { "h6", new[] { "Betel", "Canaán", "Jacob/Israel", "Esaú", "Los Ángeles de la Escalera" } },
            { "h7", new[] { "Egipto", "La Tierra de Gosén", "José", "El Faraón", "Benjamín" } },
        };

        private static HashSet<string> HistoriaPiezas(string historiaId)
        {
            if (historiaId == null || !HistoriaPiezasRaw.TryGetValue(historiaId, out var arr)) return null;
            var set = new HashSet<string>();
            foreach (var n in arr) set.Add(NormName(n));
            return set;
        }

        private static string NormName(string s)
        {
            var d = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(d.Length);
            foreach (var ch in d)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>ScrollRect vertical con viewport enmascarado; devuelve el RectTransform de contenido
        /// (anclado arriba, ancho estirado). Añade una barra deslizable a la derecha.</summary>
        private RectTransform MakeScroll(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, bool vertical)
        {
            var go = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // viewport transparente
            MenuTheme.Anchor((RectTransform)go.transform, aMin, aMax, oMin, oMax);
            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = vertical; sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 28f; sr.viewport = (RectTransform)go.transform;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(go.transform, false);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f); content.offsetMin = Vector2.zero; content.offsetMax = new Vector2(-4f, 0f);
            sr.content = content;

            // barra deslizable (muy fina, casi invisible salvo el asa)
            var barGo = new GameObject("VBar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barGo.transform.SetParent(go.transform, false);
            barGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // fondo transparente
            var brt = (RectTransform)barGo.transform;
            brt.anchorMin = new Vector2(1f, 0f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(1f, 0.5f);
            brt.offsetMin = new Vector2(-3f, 0f); brt.offsetMax = new Vector2(0f, 0f);
            var slide = new GameObject("Sliding", typeof(RectTransform)).GetComponent<RectTransform>();
            slide.SetParent(barGo.transform, false); MenuTheme.Stretch(slide);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(slide, false);
            handle.GetComponent<Image>().color = new Color(0.55f, 0.46f, 0.22f, 0.7f);
            var hrt = (RectTransform)handle.transform; // estira el asa al ancho (fino) de la barra
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero; hrt.sizeDelta = Vector2.zero;
            var sb = barGo.GetComponent<Scrollbar>();
            sb.handleRect = hrt; sb.direction = Scrollbar.Direction.BottomToTop;
            sr.verticalScrollbar = sb; sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        // fichas/etiquetas de filtro
        /// <summary>Imita proceduralmente el botón del menú principal: marco dorado metálico +
        /// interior oscuro + brillo + texto oro. Devuelve el Image del marco (para SetChipOn).</summary>
        private Image BuildMenuChip(GameObject go, string text)
        {
            var frame = go.GetComponent<Image>();
            frame.sprite = MenuGraphics.Rounded(48, 12); frame.type = Image.Type.Sliced; frame.color = Color.white;
            frame.gameObject.AddComponent<MetallicGoldGradient>(); // marco oro metálico

            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(go.transform, false);
            var inner = innerGo.GetComponent<Image>();
            inner.sprite = MenuGraphics.Rounded(48, 10); inner.type = Image.Type.Sliced;
            inner.color = new Color(0.03f, 0.03f, 0.05f, 1f); inner.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)innerGo.transform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            var glossGo = new GameObject("Gloss", typeof(RectTransform), typeof(Image));
            glossGo.transform.SetParent(go.transform, false);
            var g = glossGo.GetComponent<Image>();
            g.sprite = MenuGraphics.VGradient(new Color(1f, 0.92f, 0.65f, 0.22f), new Color(1f, 1f, 1f, 0f));
            g.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)glossGo.transform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var lbl = MenuTheme.Label(go.transform, text, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            lbl.raycastTarget = false; MenuTheme.Stretch((RectTransform)lbl.transform);
            lbl.gameObject.AddComponent<MetallicGoldGradient>(); // texto oro metálico
            return frame;
        }

        private Image MakeChip(Transform parent, string text, float x, float w, bool on, System.Action onClick)
        {
            var go = new GameObject("Chip_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(w, 36f);
            var frame = BuildMenuChip(go, text);
            var btn = go.GetComponent<Button>(); btn.targetGraphic = frame;
            var cols = btn.colors; cols.highlightedColor = new Color(1.15f, 1.1f, 0.95f, 1f); btn.colors = cols;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            SetChipOn(frame, on);
            return frame;
        }

        private static void SetChipOn(Image frame, bool on)
        {
            // marco oro metálico brillante al seleccionar; atenuado si no.
            frame.color = on ? Color.white : new Color(0.42f, 0.42f, 0.45f, 1f);
            var ol = frame.GetComponent<Outline>();
            if (ol == null) ol = frame.gameObject.AddComponent<Outline>();
            ol.effectColor = on ? MenuTheme.Gold : new Color(0f, 0f, 0f, 0f);
            ol.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>Botón con el mismo diseño que los chips del filtro (rounded + borde dorado).</summary>
        private Button ChipButton(Transform parent, string text)
        {
            var go = new GameObject("CBtn_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var frame = BuildMenuChip(go, text);
            var btn = go.GetComponent<Button>(); btn.targetGraphic = frame;
            var cols = btn.colors; cols.highlightedColor = new Color(1.15f, 1.1f, 0.95f, 1f); btn.colors = cols;
            SetChipOn(frame, true); // botón de acción: siempre con aspecto activo
            return btn;
        }

        private void MakeTag(Transform parent, string text, ref float x)
        {
            var lbl = MenuTheme.Label(parent, text, 14, MenuTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            var rt = (RectTransform)lbl.transform;
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(30f, 28f);
            x += 34f;
        }

        private void AddStepLabel(Transform parent, string head, string body)
        {
            var box = new GameObject("Step", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(parent, false);
            box.sizeDelta = new Vector2(500f, 80f);
            var le = box.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 500f; le.preferredHeight = 80f;
            MenuTheme.Rect(box, "bg", MenuTheme.PanelBg);
            var h = MenuTheme.Label(box, head, 20, MenuTheme.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)h.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -32f), new Vector2(-12f, -6f));
            var b = MenuTheme.Label(box, body, 15, new Color(0.88f, 0.84f, 0.7f), TextAnchor.UpperLeft);
            MenuTheme.Anchor((RectTransform)b.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 8f), new Vector2(-12f, -36f));
        }

        private void SaveSampleDeck()
        {
            var mazos = PlayerData.Mazos();
            mazos.Add(new DeckEntry { nombre = "Mazo " + (mazos.Count + 1), historiaId = "h1" });
            PlayerData.SaveMazos(mazos);
            Show(Screen.MisMazos);
            _stack[_stack.Count - 1] = Screen.MisMazos;
        }

        // --- TIENDA ---

        private RectTransform BuildTienda()
        {
            var screen = NewScreen("Tienda", "tienda/fondo_tienda", MenuTheme.DarkBg);
            if (MenuAssets.Sprite("tienda/fondo_tienda") == null) MenuTheme.Rect(screen, "bg2", new Color(0.08f, 0.06f, 0.1f, 1f));
            Title(screen, "TIENDA");
            BackButton(screen);
            TopBar(screen, withCoins: true);
            var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
            grid.transform.SetParent(screen, false);
            MenuTheme.Anchor((RectTransform)grid.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-420f, 40f), new Vector2(420f, -100f));
            grid.cellSize = new Vector2(200f, 240f); grid.spacing = new Vector2(20f, 20f);
            grid.childAlignment = TextAnchor.UpperCenter;
            string[] cuadros = { "tienda/CUADRO 1", "tienda/CADRO 2", "tienda/CUADRO 3", "tienda/CUADRO 6" };
            int[] precios = { 100, 250, 500, 1000 };
            for (int i = 0; i < cuadros.Length; i++)
            {
                int precio = precios[i];
                var cell = MenuTheme.Panel(grid.transform, "Item");
                ((RectTransform)cell.transform).sizeDelta = new Vector2(200f, 240f);
                var pic = MenuAssets.Sprite(cuadros[i]);
                if (pic != null) MenuTheme.Picture(cell, "pic", pic);
                else MenuTheme.Rect(cell, "pic", MenuTheme.PanelBg);
                var buy = MenuTheme.TextButton(cell, precio + " ◈", 18, () => TryBuy(precio), 160f, 40f);
                MenuTheme.Anchor((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-80f, 8f), new Vector2(80f, 48f));
            }
            return screen;
        }

        private void TryBuy(int precio)
        {
            if (PlayerData.Monedas >= precio) { PlayerData.Monedas -= precio; Show(Screen.Tienda); }
        }

        // --- TOMOS ---

        private RectTransform BuildTomos()
        {
            var screen = NewScreen("Tomos", "tomos/fondo_apertura", MenuTheme.DarkBg);
            AddVideoBackground(screen, "Menu/tomos/fondo_tomos"); // fondo animado en bucle (sobre el estático de respaldo)
            PlayerData.Tomos = 10; // TESTING: siempre 10 tomos al entrar (quitar cuando se pruebe con Tienda)
            BackButton(screen); // sin TopBar/monedas ni título: el fondo del podio ya es el marco
            // atajo a la Tienda (arriba-derecha, espejo de VOLVER): comprar más tomos
            var tiendaBtn = MenuTheme.TextButton(screen, "TIENDA", 14, () => Push(Screen.Tienda), 120f, 40f, thicken: false);
            MenuTheme.Anchor((RectTransform)tiendaBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-156f, -70f), new Vector2(-36f, -30f));
            // monedas disponibles debajo del botón de Tienda (TESTING: 10000)
            PlayerData.Monedas = 10000;
            var coinIcon = MenuTheme.Picture(screen, "CoinIcon", MenuAssets.Sprite("Moneda"));
            MenuTheme.Anchor((RectTransform)coinIcon.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -108f), new Vector2(-118f, -76f));
            var coinLbl = MenuTheme.Label(screen, PlayerData.Monedas.ToString(), 18, MenuTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            MenuTheme.GoldMetalText(coinLbl);
            MenuTheme.Anchor((RectTransform)coinLbl.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-112f, -108f), new Vector2(-36f, -76f));

            // libro animado por FRAMES (el verde ya viene recortado/transparente en los PNG)
            var book = new GameObject("Book", typeof(RectTransform), typeof(Image));
            book.transform.SetParent(screen, false);
            var bimg = book.GetComponent<Image>();
            bimg.raycastTarget = false; bimg.preserveAspect = true;
            MenuTheme.Anchor((RectTransform)book.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-319f, -204f), new Vector2(319f, 241f)); // aspecto ~1.43 (con margen para apertura/vuelta)
            var fb = book.AddComponent<Flipbook>();
            fb.frames = LoadTomoFrames();
            fb.fps = 24f;
            fb.ShowFrame(0); // idle: portada cerrada (mismo encuadre que la animación)

            // --- ABRIR TOMO (botón ancho, centrado, encima del selector) ---
            TomoCarousel tc = null;              // referencia para el botón (se asigna abajo)
            System.Action hideBottom = null;     // oculta botón+selector al abrir (se asigna abajo)
            System.Action restoreBottom = null;  // los vuelve a mostrar al cerrar (sin reconstruir la pantalla → el fondo en video no se reinicia)
            System.Action refreshAvail = null;   // ajusta visibilidad según haya tomos o no (se asigna abajo)
            var abrir = MenuTheme.TextButton(screen, "✦  ABRIR TOMO  ✦", 20, () => { if (tc != null && !tc.SelectedUnlocked) return; hideBottom?.Invoke(); OpenTomo(fb, restoreBottom); }, 380f, 56f);
            abrir.interactable = PlayerData.Tomos > 0;
            MenuTheme.Anchor((RectTransform)abrir.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-190f, 206f), new Vector2(190f, 262f));

            // --- SELECTOR (capas): fondo -> contenido -> marco. Fondo y marco comparten el MISMO
            // rect para que sus bordes coincidan (sin rectángulo oscuro asomando fuera del marco).
            var selRect = (min: new Vector2(-390f, 8f), max: new Vector2(390f, 190f));

            // Capa 1: fondo oscuro, INSET dentro del marco (el oro no llena el borde del rect: hay
            // margen transparente), para que la filigrana tape los bordes del fondo y no sobresalga.
            var selFondo = MenuTheme.Picture(screen, "SelFondo", MenuAssets.Sprite("tomos/sel_fondo"), preserveAspect: false);
            selFondo.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)selFondo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-372f, 24f), new Vector2(372f, 174f));

            // Capa 2: carrusel infinito deslizable (contenido; el del centro es el seleccionado)
            var viewport = new GameObject("Selector", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            viewport.SetParent(screen, false);
            MenuTheme.Anchor(viewport, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-372f, 18f), new Vector2(372f, 180f));
            var vpImg = viewport.GetComponent<Image>(); vpImg.color = new Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true; // capta el arrastre

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); MenuTheme.Stretch(content);

            tc = viewport.gameObject.AddComponent<TomoCarousel>();

            // Capa 3: marco filigrana dorado (3 ranuras, centro resaltado), al frente, mismo rect
            var marco = MenuTheme.Picture(screen, "SelMarco", MenuAssets.Sprite("tomos/sel_marco"), preserveAspect: false);
            marco.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)marco.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), selRect.min, selRect.max);

            // cantidad de tomos en la esquina inferior derecha de la ranura central
            var badge = MenuTheme.Label(marco.transform, PlayerData.Tomos.ToString(), 24, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            badge.raycastTarget = false; badge.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            var brt = (RectTransform)badge.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(54f, 32f); brt.anchoredPosition = new Vector2(93f, -50f);

            // ocultar/mostrar botón + selector al abrir/cerrar el tomo (SIN reconstruir la pantalla,
            // para que el fondo en video siga su ciclo sin reiniciarse)
            hideBottom = () =>
            {
                abrir.gameObject.SetActive(false);
                selFondo.gameObject.SetActive(false);
                marco.gameObject.SetActive(false);
                viewport.gameObject.SetActive(false);
            };
            restoreBottom = () =>
            {
                fb.ShowFrame(0); // libro cerrado (idle)
                selFondo.gameObject.SetActive(true);
                marco.gameObject.SetActive(true);
                viewport.gameObject.SetActive(true);
                refreshAvail?.Invoke(); // decide libro/botón/miniatura/contador según queden tomos
            };

            var items = new List<TomoCarousel.Item>
            {
                new TomoCarousel.Item { sprite = MenuAssets.Sprite("tomos/frame_000"), unlocked = true },
                new TomoCarousel.Item { sprite = null, unlocked = false, lockedText = "Próximamente" },
                new TomoCarousel.Item { sprite = null, unlocked = false, lockedText = "Próximamente" },
            };
            tc.onSelect = (idx) =>
            {
                bool avail = items[idx].unlocked && PlayerData.Tomos > 0;
                badge.text = avail ? PlayerData.Tomos.ToString() : "";
                abrir.interactable = avail;
            };
            tc.Build(content, 271f, items);

            // sin tomos: desaparece el libro central, el botón y la miniatura+contador (la barra queda)
            refreshAvail = () =>
            {
                bool has = PlayerData.Tomos > 0;
                book.SetActive(has);
                abrir.gameObject.SetActive(has);
                abrir.interactable = has && (tc == null || tc.SelectedUnlocked);
                badge.text = has ? PlayerData.Tomos.ToString() : "";
                tc.SetThumbVisible(0, has); // oculta la imagen del tomo Genesis en el slot central
            };
            refreshAvail();
            _onShow[Screen.Tomos] = () => { PlayerData.Tomos = 10; PlayerData.Monedas = 10000; coinLbl.text = PlayerData.Monedas.ToString(); fb.ShowFrame(0); refreshAvail(); }; // al reusar del caché (TESTING)
            return screen;
        }

        private Sprite[] LoadTomoFrames()
        {
            var list = new List<Sprite>();
            for (int i = 0; ; i++) // frames keyed a 24fps (recortados: apertura + vuelta + cierre)
            {
                var s = MenuAssets.Sprite("tomos/anim/f_" + i.ToString("000"));
                if (s == null) break;
                list.Add(s);
            }
            return list.ToArray(); // list[0] = f_000 (portada), 0-48 abre, 49-85 vuelta, 86-122 cierra
        }

        // Índices en el array recortado (24fps): 0-48 abre, 49-85 vuelta de página, 86-122 cierra.
        private const int TomoOpenEnd = 48;    // libro totalmente abierto (reposo)
        private const int TomoFlipStart = 49;  // inicio de la vuelta de página real
        private const int TomoFlipEnd = 85;    // fin de la vuelta (vuelve a abierto)
        private const int TomoCloseStart = 86; // inicio del cierre (desde abierto)
        private const int TomoCloseEnd = 122;  // portada cerrada

        /// <summary>Abre un tomo: reproduce la apertura del libro por frames y luego muestra 5 cartas
        /// como páginas (carta a la izquierda, texto a la derecha), pasando de página al tocar; al
        /// terminar cierra el libro y refresca la pantalla.</summary>
        private void OpenTomo(Flipbook fb, System.Action onClose)
        {
            if (PlayerData.Tomos <= 0) return; // esta pantalla solo ABRE tomos (se compran en la Tienda)
            EnsureCatalog();
            var pool = new List<CardDefinition>();
            if (_catalog != null)
                foreach (var c in _catalog.Cards.Values)
                    if (c.Type != CardType.Historia) pool.Add(c);
            if (pool.Count == 0) return;

            PlayerData.Tomos -= 1;
            var picks = new List<CardDefinition>();
            for (int i = 0; i < 5; i++) picks.Add(pool[Random.Range(0, pool.Count)]);

            var screen = (RectTransform)fb.transform.parent;
            // capa transparente a pantalla completa: capta el toque (pasar página) y bloquea botones.
            // Es lo más alto de la jerarquía, así que el libro (debajo) se ve a través y las páginas
            // (hijas de esta capa) se dibujan por encima del libro.
            var overlay = MenuTheme.Rect(screen, "TomoOverlay", new Color(0f, 0f, 0f, 0f));
            overlay.raycastTarget = true;
            var pc = overlay.gameObject.AddComponent<PointerClicks>();
            overlay.transform.SetAsLastSibling();

            // área de páginas alineada al RECUADRO REAL del libro (dentro del frame el libro ocupa
            // solo el centro ~0.13-0.87 x / 0.05-0.95 y, no todo el rect), para que las fracciones
            // de página caigan sobre las hojas.
            var pages = new GameObject("Pages", typeof(RectTransform)).GetComponent<RectTransform>();
            pages.SetParent(overlay.transform, false);
            MenuTheme.Anchor(pages, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-313f, -196f), new Vector2(306f, 222f));

            bool canAnim = fb.frames != null && fb.frames.Length > TomoCloseEnd;
            if (canAnim)
                fb.Play(0, TomoOpenEnd, false, () => ShowTomoSpread(fb, overlay.gameObject, pages, pc, picks, 0, onClose));
            else
                ShowTomoSpread(fb, overlay.gameObject, pages, pc, picks, 0, onClose);
        }

        /// <summary>Dibuja la carta índice <paramref name="idx"/> como una página abierta del tomo y
        /// arma el toque para pasar a la siguiente (o cerrar tras la última).</summary>
        private void ShowTomoSpread(Flipbook fb, GameObject overlay, RectTransform pages, PointerClicks pc, List<CardDefinition> picks, int idx, System.Action onClose)
        {
            fb.ShowFrame(TomoOpenEnd); // reposo: libro abierto
            for (int i = pages.childCount - 1; i >= 0; i--) Destroy(pages.GetChild(i).gameObject);

            var c = picks[idx];

            // --- PÁGINA IZQUIERDA: carta (placeholder; arte real pendiente) ---
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(pages, false);
            var ci = card.GetComponent<Image>();
            ci.sprite = MenuGraphics.Rounded(32, 10); ci.type = Image.Type.Sliced; ci.color = TypeColorDeck(c.Type);
            ci.raycastTarget = false;
            card.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            SetFrac((RectTransform)card.transform, 0.11f, 0.16f, 0.41f, 0.78f); // zona plana pág. izq
            ShiftX((RectTransform)card.transform, 20f); // 20px a la derecha

            var tl = MenuTheme.Label(card.transform, TypeLabel(c.Type), 11, new Color(0.9f, 0.87f, 0.75f), TextAnchor.UpperLeft, FontStyle.Bold);
            tl.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)tl.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -26f), new Vector2(-10f, -6f));
            var nm = MenuTheme.Label(card.transform, c.Nombre, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            nm.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)nm.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 30f), new Vector2(-8f, -30f));
            var cost = MenuTheme.Label(card.transform, CostText(c), 14, new Color(0.95f, 0.9f, 0.7f), TextAnchor.LowerCenter, FontStyle.Bold);
            cost.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)cost.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 12f), new Vector2(-8f, 36f));

            // --- PÁGINA DERECHA: texto en tinta oscura sobre la página (placeholder de verso) ---
            var rTitle = MenuTheme.Label(pages, c.Nombre, 15, new Color(0.28f, 0.2f, 0.08f), TextAnchor.UpperCenter, FontStyle.Bold);
            rTitle.raycastTarget = false;
            SetFrac((RectTransform)rTitle.transform, 0.57f, 0.60f, 0.90f, 0.73f); // zona plana pág. der
            ShiftX((RectTransform)rTitle.transform, -20f); // 20px a la izquierda
            var body = c.Efecto ?? c.Condicion ?? c.AlEntrar ?? "«Texto bíblico»";
            var rText = MenuTheme.Label(pages, body, 12, new Color(0.22f, 0.15f, 0.06f), TextAnchor.UpperCenter);
            rText.raycastTarget = false;
            SetFrac((RectTransform)rText.transform, 0.57f, 0.24f, 0.90f, 0.57f);
            ShiftX((RectTransform)rText.transform, -20f); // 20px a la izquierda

            // contador (arriba, sobre el lomo) y pista (abajo, entre páginas)
            var counter = MenuTheme.Label(pages, $"{idx + 1} / {picks.Count}", 18, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            counter.raycastTarget = false;
            SetFrac((RectTransform)counter.transform, 0.40f, 0.90f, 0.60f, 1.0f);
            var hint = MenuTheme.Label(pages, idx + 1 < picks.Count ? "Izq: pasar · Izq en carta: volver · Der: ampliar" : "Der: ampliar · Toca para cerrar", 13, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            hint.raycastTarget = false;
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            SetFrac((RectTransform)hint.transform, 0.20f, 0.0f, 0.80f, 0.085f);

            bool canAnim = fb.frames != null && fb.frames.Length > TomoCloseEnd;

            // pasar de página / cerrar (click izq fuera de la carta)
            System.Action next = () =>
            {
                if (fb.IsPlaying) return; // ignora clicks mientras la animación no termina
                for (int i = pages.childCount - 1; i >= 0; i--) Destroy(pages.GetChild(i).gameObject);
                if (idx + 1 < picks.Count)
                {
                    if (canAnim) fb.Play(TomoFlipStart, TomoFlipEnd, false, () => ShowTomoSpread(fb, overlay, pages, pc, picks, idx + 1, onClose));
                    else ShowTomoSpread(fb, overlay, pages, pc, picks, idx + 1, onClose);
                }
                else
                {
                    System.Action done = () =>
                    {
                        Destroy(overlay);
                        onClose?.Invoke(); // restaura botón+selector sin reconstruir (el video no se reinicia)
                    };
                    if (canAnim) fb.Play(TomoCloseStart, TomoCloseEnd, false, done);
                    else done();
                }
            };
            // carta anterior (click izq sobre la página/carta izquierda)
            System.Action prev = () =>
            {
                if (fb.IsPlaying || idx <= 0) return;
                for (int i = pages.childCount - 1; i >= 0; i--) Destroy(pages.GetChild(i).gameObject);
                if (canAnim) fb.Play(TomoFlipStart, TomoFlipEnd, false, () => ShowTomoSpread(fb, overlay, pages, pc, picks, idx - 1, onClose));
                else ShowTomoSpread(fb, overlay, pages, pc, picks, idx - 1, onClose);
            };
            System.Action zoom = () => ShowCardZoom(overlay.transform, c);

            // zona de la página izquierda: click izq = anterior; click der = ampliar
            var leftZone = new GameObject("LeftZone", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            leftZone.transform.SetParent(pages, false);
            leftZone.color = new Color(0f, 0f, 0f, 0f); leftZone.raycastTarget = true;
            SetFrac((RectTransform)leftZone.transform, 0.05f, 0.10f, 0.49f, 0.92f);
            var lpc = leftZone.gameObject.AddComponent<PointerClicks>();
            lpc.onLeft = prev; lpc.onRight = zoom;

            pc.onLeft = next; pc.onRight = zoom;
        }

        /// <summary>Muestra la carta índice ampliada en un modal; se cierra al tocar.</summary>
        private void ShowCardZoom(Transform parent, CardDefinition c)
        {
            var dim = MenuTheme.Rect(parent, "CardZoom", new Color(0f, 0f, 0f, 0.82f));
            dim.raycastTarget = true;
            dim.transform.SetAsLastSibling();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => Destroy(dim.gameObject));

            var card = new GameObject("BigCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(dim.transform, false);
            var ci = card.GetComponent<Image>();
            ci.sprite = MenuGraphics.Rounded(48, 14); ci.type = Image.Type.Sliced; ci.color = TypeColorDeck(c.Type);
            ci.raycastTarget = false;
            card.AddComponent<Outline>().effectColor = MenuTheme.Gold;
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(360f, 500f);

            var tl = MenuTheme.Label(card.transform, TypeLabel(c.Type), 16, new Color(0.9f, 0.87f, 0.75f), TextAnchor.UpperLeft, FontStyle.Bold);
            tl.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)tl.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -40f), new Vector2(-18f, -12f));
            var nm = MenuTheme.Label(card.transform, c.Nombre, 28, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            nm.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)nm.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -110f), new Vector2(-14f, -46f));
            var cost = MenuTheme.Label(card.transform, CostText(c), 20, new Color(0.95f, 0.9f, 0.7f), TextAnchor.LowerCenter, FontStyle.Bold);
            cost.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)cost.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 16f), new Vector2(-14f, 52f));
            var body = c.Efecto ?? c.Condicion ?? c.AlEntrar ?? "";
            var eff = MenuTheme.Label(card.transform, body, 16, new Color(0.96f, 0.94f, 0.86f), TextAnchor.MiddleCenter);
            eff.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)eff.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 60f), new Vector2(-20f, -120f));
        }

        /// <summary>Ancla un RectTransform por fracciones del padre (esquinas inferior-izq y superior-der).</summary>
        private static void ShiftX(RectTransform t, float dx)
        {
            t.offsetMin += new Vector2(dx, 0f);
            t.offsetMax += new Vector2(dx, 0f);
        }

        private Sprite _glowSprite;
        /// <summary>Resplandor radial suave (blanco con alfa que decae del centro al borde), generado
        /// una sola vez. Se tiñe con Image.color.</summary>
        private Sprite GlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 centro .. 1 borde
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a; // caída suave (halo más brillante)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _glowSprite;
        }

        private static void SetFrac(RectTransform t, float minX, float minY, float maxX, float maxY)
        {
            t.anchorMin = new Vector2(minX, minY); t.anchorMax = new Vector2(maxX, maxY);
            t.offsetMin = Vector2.zero; t.offsetMax = Vector2.zero;
        }

        // --- OPCIONES ---

        private RectTransform BuildOpciones()
        {
            var screen = NewScreen("Opciones", "fondo_constructor", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.55f));
            Title(screen, "OPCIONES");
            BackButton(screen);
            var list = MenuTheme.VBox(screen, 14f, 0, TextAnchor.UpperCenter);
            MenuTheme.Anchor((RectTransform)list.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-220f, 80f), new Vector2(220f, -100f));
            MenuTheme.TextButton(list.transform, "Pantalla completa", 18, () => Screen_ToggleFullscreen(), 420f, 50f)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            MenuTheme.TextButton(list.transform, "Reiniciar monedas (500)", 18, () => { PlayerData.Monedas = 500; }, 420f, 50f)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            MenuTheme.TextButton(list.transform, "Borrar mazos", 18, () => PlayerData.SaveMazos(new List<DeckEntry>()), 420f, 50f)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            return screen;
        }

        private static void Screen_ToggleFullscreen() => UnityEngine.Screen.fullScreen = !UnityEngine.Screen.fullScreen;

        // --- MENÚ OPCIONES (modal sobre el menú principal) ---

        private static readonly (int w, int h)[] ResList = { (1280, 720), (1600, 900), (1920, 1080), (2560, 1440) };
        private static readonly FullScreenMode[] ModeList = { FullScreenMode.Windowed, FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow };

        private void ShowOpcionesModal(bool inGame = false)
        {
            CloseModal();
            System.Action close = inGame ? (System.Action)CloseInGameOptions : CloseModal;
            var overlay = MenuTheme.Panel(_root, "OpcionesModal");
            overlay.transform.SetAsLastSibling();
            _modal = overlay.gameObject;
            var dim = MenuTheme.Rect(overlay, "Dim", new Color(0f, 0f, 0f, 0.55f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => close());

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(overlay, false);
            panel.sprite = MenuGraphics.Rounded(64, 16); panel.type = Image.Type.Sliced;
            panel.color = new Color(0.06f, 0.06f, 0.14f, 0.98f);
            panel.gameObject.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(600f, 600f);

            var title = MenuTheme.Label(panel.transform, "OPCIONES", 24, MenuTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(0f, -12f));

            // cerrar (X) arriba-derecha
            var xBtn = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            xBtn.transform.SetParent(panel.transform, false);
            var ximg = xBtn.GetComponent<Image>(); ximg.sprite = MenuGraphics.Rounded(32, 8); ximg.type = Image.Type.Sliced; ximg.color = new Color(0.12f, 0.12f, 0.2f, 0.9f);
            var xrt = (RectTransform)xBtn.transform; xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 1f); xrt.pivot = new Vector2(1f, 1f);
            xrt.sizeDelta = new Vector2(34f, 34f); xrt.anchoredPosition = new Vector2(-12f, -12f);
            var xl = MenuTheme.Label(xBtn.transform, "✕", 18, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold); xl.raycastTarget = false; MenuTheme.Stretch((RectTransform)xl.transform);
            xBtn.GetComponent<Button>().onClick.AddListener(() => close());

            // contenido con scroll
            var content = MakeScroll(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 20f), new Vector2(-20f, -62f), true);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6f; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false; vl.childControlWidth = true; vl.childControlHeight = true;
            vl.padding = new RectOffset(4, 14, 4, 4);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // --- GRÁFICOS ---
            OptHeader(content, "GRÁFICOS");
            if (!Application.isMobilePlatform) // resolución/ventana son de PC; en móvil no aplican
            {
                var resOpts = new string[ResList.Length];
                for (int i = 0; i < ResList.Length; i++) resOpts[i] = ResList[i].w + " × " + ResList[i].h;
                int curRes = 0; for (int i = 0; i < ResList.Length; i++) if (ResList[i].w == UnityEngine.Screen.width) curRes = i;
                OptSelector(content, "Resolución", resOpts, curRes, i => UnityEngine.Screen.SetResolution(ResList[i].w, ResList[i].h, UnityEngine.Screen.fullScreenMode));

                int curMode = 0; for (int i = 0; i < ModeList.Length; i++) if (ModeList[i] == UnityEngine.Screen.fullScreenMode) curMode = i;
                OptSelector(content, "Pantalla", new[] { "Modo ventana", "Pantalla completa", "Sin bordes" }, curMode, i => UnityEngine.Screen.fullScreenMode = ModeList[i]);
            }

            OptSelector(content, "Calidad gráfica", QualitySettings.names, QualitySettings.GetQualityLevel(), i => QualitySettings.SetQualityLevel(i, true));

            OptToggle(content, "Vsync", QualitySettings.vSyncCount > 0, on => QualitySettings.vSyncCount = on ? 1 : 0);

            // --- SONIDO ---
            OptHeader(content, "SONIDO");
            OptSlider(content, "Volumen Master", AudioListener.volume, v => AudioListener.volume = v);
            OptToggle(content, "Música", PlayerPrefs.GetInt("opt_mus", 1) == 1, on => { PlayerPrefs.SetInt("opt_mus", on ? 1 : 0); PlayerPrefs.Save(); });
            OptToggle(content, "Efectos (SFX)", PlayerPrefs.GetInt("opt_sfx", 1) == 1, on => { PlayerPrefs.SetInt("opt_sfx", on ? 1 : 0); PlayerPrefs.Save(); });

            // --- OTROS ---
            OptHeader(content, "OTROS");
            if (inGame) OptButton(content, "Abandonar Partida", AbandonMatch);
            else OptButton(content, "Cerrar Sesión", () => { /* sin sistema de cuentas todavía */ });
            OptButton(content, "Salir del Juego", Quit);
        }

        // fila con etiqueta a la izquierda; el control se ancla a la derecha.
        private RectTransform OptRow(Transform parent, string label, float height = 44f)
        {
            var row = new GameObject("Row_" + label, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var lbl = MenuTheme.Label(row, label, 16, new Color(0.9f, 0.86f, 0.72f), TextAnchor.MiddleLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)lbl.transform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(4f, 0f), new Vector2(0f, 0f));
            return row;
        }

        private void OptHeader(Transform parent, string text)
        {
            var row = new GameObject("H_" + text, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            var lbl = MenuTheme.Label(row, text, 15, MenuTheme.Gold, TextAnchor.LowerLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)lbl.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), new Vector2(-4f, -2f));
            var div = MenuTheme.Rect(row, "Div", new Color(0.5f, 0.42f, 0.2f, 0.4f));
            MenuTheme.Anchor((RectTransform)div.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 0f), new Vector2(-4f, 1f));
        }

        private void OptSelector(Transform parent, string label, string[] options, int current, System.Action<int> onChange)
        {
            var row = OptRow(parent, label);
            var go = new GameObject("Sel", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(row, false);
            var img = go.GetComponent<Image>(); img.sprite = MenuGraphics.Rounded(32, 8); img.type = Image.Type.Sliced; img.color = new Color(0.02f, 0.03f, 0.08f, 1f);
            go.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f); rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(190f, 32f); rt.anchoredPosition = new Vector2(-2f, 0f);
            int idx = Mathf.Clamp(current, 0, Mathf.Max(0, options.Length - 1));
            var val = MenuTheme.Label(go.transform, options.Length > 0 ? options[idx] : "", 14, new Color(0.95f, 0.92f, 0.8f), TextAnchor.MiddleLeft);
            val.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)val.transform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-22f, 0f));
            var caret = MenuTheme.Label(go.transform, "▾", 14, MenuTheme.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
            caret.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)caret.transform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-8f, 0f));
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (options.Length == 0) return;
                idx = (idx + 1) % options.Length;
                val.text = options[idx];
                onChange?.Invoke(idx);
            });
        }

        private void OptToggle(Transform parent, string label, bool on, System.Action<bool> onChange)
        {
            var row = OptRow(parent, label);
            var go = new GameObject("Chk", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(row, false);
            var img = go.GetComponent<Image>(); img.sprite = MenuGraphics.Rounded(24, 6); img.type = Image.Type.Sliced;
            go.AddComponent<Outline>().effectColor = MenuTheme.GoldDim;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f); rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(26f, 26f); rt.anchoredPosition = new Vector2(-6f, 0f);
            var check = MenuTheme.Label(go.transform, "✓", 18, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            check.raycastTarget = false; MenuTheme.Stretch((RectTransform)check.transform);
            bool state = on;
            void Paint() { img.color = state ? new Color(0.22f, 0.18f, 0.06f, 1f) : new Color(0.02f, 0.03f, 0.08f, 1f); check.enabled = state; }
            Paint();
            go.GetComponent<Button>().onClick.AddListener(() => { state = !state; Paint(); onChange?.Invoke(state); });
        }

        private void OptSlider(Transform parent, string label, float value, System.Action<float> onChange)
        {
            var row = OptRow(parent, label);
            var sgo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sgo.transform.SetParent(row, false);
            var srt = (RectTransform)sgo.transform; srt.anchorMin = srt.anchorMax = new Vector2(1f, 0.5f); srt.pivot = new Vector2(1f, 0.5f);
            srt.sizeDelta = new Vector2(190f, 20f); srt.anchoredPosition = new Vector2(-52f, 0f);
            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(sgo.transform, false);
            var timg = track.GetComponent<Image>(); timg.sprite = MenuGraphics.Rounded(16, 6); timg.type = Image.Type.Sliced; timg.color = new Color(0.15f, 0.15f, 0.22f, 1f);
            MenuTheme.Anchor((RectTransform)track.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
            var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(sgo.transform, false); MenuTheme.Anchor(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea, false);
            var fimg = fill.GetComponent<Image>(); fimg.sprite = MenuGraphics.Rounded(16, 6); fimg.type = Image.Type.Sliced; fimg.color = MenuTheme.MetalGold;
            var frt = (RectTransform)fill.transform; frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(0f, 1f); frt.sizeDelta = new Vector2(10f, 0f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(sgo.transform, false);
            var himg = handle.GetComponent<Image>(); himg.sprite = MenuGraphics.Rounded(20, 10); himg.color = MenuTheme.Gold;
            ((RectTransform)handle.transform).sizeDelta = new Vector2(14f, 14f);
            var sl = sgo.GetComponent<Slider>();
            sl.fillRect = frt; sl.handleRect = (RectTransform)handle.transform; sl.targetGraphic = himg;
            sl.minValue = 0f; sl.maxValue = 1f; sl.value = value;
            var valLbl = MenuTheme.Label(row, value.ToString("0.00"), 14, new Color(0.9f, 0.86f, 0.72f), TextAnchor.MiddleRight);
            valLbl.raycastTarget = false;
            MenuTheme.Anchor((RectTransform)valLbl.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-44f, 0f), new Vector2(-6f, 0f));
            sl.onValueChanged.AddListener(v => { valLbl.text = v.ToString("0.00"); onChange?.Invoke(v); });
        }

        private void OptButton(Transform parent, string label, System.Action onClick)
        {
            var row = new GameObject("B_" + label, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            var btn = MenuTheme.TextButton(row, label, 16, onClick, 520f, 46f);
            MenuTheme.Anchor((RectTransform)btn.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(4f, -23f), new Vector2(-4f, 23f));
        }

        // --- lanzar campo / salir ---

        private void LaunchGame()
        {
            // El campo ya está en la escena, solo desactivado: ocúltase el menú y se enciende.
            if (_board != null)
            {
                _canvas.gameObject.SetActive(false);
                _board.enabled = true; // dispara su Start → construye la partida
                _inMatch = true;
                return;
            }
            if (Application.CanStreamedLevelBeLoaded("Main")) SceneManager.LoadScene("Main");
            else Debug.LogWarning("Sin campo ni escena 'Main'.");
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --- pausa/Opciones durante la partida (Esc) ---

        private void Update()
        {
            if (_inMatch && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_modal == null) OpenInGameOptions();
                else CloseInGameOptions();
            }
        }

        /// <summary>Engranaje en la esquina superior-derecha durante la partida (overlay IMGUI, por
        /// encima del campo). Abre el menú de Opciones.</summary>
        private void OnGUI()
        {
            if (!_inMatch || _modal != null) return;
            if (_gearTex == null) { var g = MenuGraphics.Gear(80, 8); if (g != null) _gearTex = g.texture; }
            var rect = new Rect(UnityEngine.Screen.width - 58f, 14f, 44f, 44f);
            var prev = GUI.color; GUI.color = MenuTheme.Gold;
            bool clicked = _gearTex != null ? GUI.Button(rect, _gearTex, GUIStyle.none) : GUI.Button(rect, "⚙");
            GUI.color = prev;
            if (clicked) OpenInGameOptions();
        }

        private void OpenInGameOptions()
        {
            if (_board != null) _board.enabled = false;   // pausa el campo (deja de dibujar/actualizar)
            _canvas.gameObject.SetActive(true);
            if (_current != null) _current.SetActive(false); // oculta la pantalla de menú vieja del fondo
            ShowOpcionesModal(inGame: true);
        }

        private void CloseInGameOptions()
        {
            CloseModal();
            _canvas.gameObject.SetActive(false);
            if (_board != null) _board.enabled = true;    // reanuda la partida
        }

        private void AbandonMatch()
        {
            CloseModal();
            _inMatch = false;
            if (_board != null) _board.enabled = false;
            _canvas.gameObject.SetActive(true);
            _stack.Clear();
            Push(Screen.MainMenu);
        }
    }
}
