using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>
    /// Cáscara de menús (réplica del Shell+stack de Godot) con Canvas uGUI generado por código.
    /// Cada apartado es una pantalla apilable; el botón Volver hace pop. "Jugar" carga la escena
    /// del campo (Main). Un solo MonoBehaviour construye todo.
    /// </summary>
    public sealed class MenuShell : MonoBehaviour
    {
        public enum Screen { MainMenu, Historias, ContraIA, Multijugador, MisMazos, DeckBuilder, Misiones, Tienda, Tomos, Opciones }

        private Canvas _canvas;
        private RectTransform _root;      // contenedor de la pantalla activa
        private GameObject _current;
        private readonly List<Screen> _stack = new();
        private Game.Runtime.View.HotseatView _board;

        /// <summary>El campo a activar cuando el jugador entra a una partida (se deja desactivado).</summary>
        public void SetBoard(Game.Runtime.View.HotseatView board) => _board = board;

        private void Start()
        {
            EnsureEventSystem();
            BuildCanvas();
            Push(Screen.MainMenu);
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
            if (_current != null) Destroy(_current);
            _current = Build(s).gameObject;
        }

        private RectTransform Build(Screen s) => s switch
        {
            Screen.MainMenu     => BuildMainMenu(),
            Screen.Historias    => BuildListScreen("HISTORIAS", "fondo_historias", HistoriaButtons()),
            Screen.ContraIA     => BuildContraIA(),
            Screen.Multijugador => BuildSimple("MULTIJUGADOR", "Partida LAN 1v1 — próximamente."),
            Screen.MisMazos     => BuildMisMazos(),
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
            var back = MenuTheme.ImageButton(screen, MenuAssets.Sprite("buttons/volver"), Pop, 120f, 48f);
            var rt = (RectTransform)back.transform;
            MenuTheme.Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -64f), new Vector2(136f, -16f));
            if (MenuAssets.Sprite("buttons/volver") == null)
            {
                Destroy(back.gameObject);
                var tb = MenuTheme.TextButton(screen, "VOLVER", 18, Pop, 120f, 46f);
                MenuTheme.Anchor((RectTransform)tb.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -66f), new Vector2(136f, -20f));
            }
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
            MenuTheme.Anchor((RectTransform)pname.transform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(62f, -4f), new Vector2(-6f, -6f));
            var pinfo = MenuTheme.Label(prof.transform, "Nv 1 · 0 amigos", 13, new Color(0.75f, 0.72f, 0.6f), TextAnchor.LowerLeft);
            MenuTheme.Anchor((RectTransform)pinfo.transform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(62f, 6f), new Vector2(-6f, 0f));

            // --- Cluster arriba-derecha: monedas + tutorial + engranaje ---
            var coinIcon = MenuTheme.Picture(screen, "CoinIcon", MenuAssets.Sprite("Moneda"));
            MenuTheme.Anchor((RectTransform)coinIcon.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300f, -56f), new Vector2(-262f, -18f));
            var coinLbl = MenuTheme.Label(screen, PlayerData.Monedas.ToString(), 24, MenuTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)coinLbl.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-256f, -56f), new Vector2(-150f, -18f));
            var tut = MenuTheme.TextButton(screen, "TUTORIAL", 14, LaunchGame, 110f, 38f);
            MenuTheme.Anchor((RectTransform)tut.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-146f, -56f), new Vector2(-52f, -18f));
            var gear = MenuTheme.TextButton(screen, "⚙", 22, () => Push(Screen.Opciones), 40f, 40f);
            MenuTheme.Anchor((RectTransform)gear.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -56f), new Vector2(-8f, -16f));

            // --- Logo centrado ---
            var logo = MenuTheme.Picture(screen, "Logo", MenuAssets.Sprite("logo_tgb"));
            MenuTheme.Anchor((RectTransform)logo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-180f, -250f), new Vector2(180f, -40f));

            // --- 4 botones DISEÑADOS con uGUI (marco dorado + interior oscuro) ---
            var list = MenuTheme.VBox(screen, 12f, 0, TextAnchor.MiddleCenter);
            MenuTheme.Anchor((RectTransform)list.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-175f, -135f), new Vector2(175f, 95f));
            DesignedMenuButton(list.transform, "HISTORIAS", () => Push(Screen.Historias), icon: MenuIcons.Book);
            DesignedMenuButton(list.transform, "MULTIJUGADOR", () => Push(Screen.Multijugador), icon: MenuIcons.People);
            DesignedMenuButton(list.transform, "CONSTRUCTOR DE MAZOS", () => Push(Screen.MisMazos), icon: MenuIcons.Cards);
            DesignedMenuButton(list.transform, "MISIONES Y LOGROS", () => Push(Screen.Misiones), icon: MenuIcons.Medal);

            // --- Tienda = estandarte a la izquierda-centro ---
            var tienda = FloatingIcon(screen, "buttons/btn_tienda", "TIENDA", () => Push(Screen.Tienda));
            MenuTheme.Anchor((RectTransform)tienda.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, -120f), new Vector2(150f, 120f));

            // --- Tomos = libro ornamentado a la derecha-centro ---
            var tomos = FloatingIcon(screen, "Tomes", "TOMOS", () => Push(Screen.Tomos));
            MenuTheme.Anchor((RectTransform)tomos.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-200f, -110f), new Vector2(-40f, 110f));
            return screen;
        }

        private void DesignedMenuButton(Transform parent, string label, System.Action onClick,
                                        float width = 340f, float height = 50f,
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

        // --- CONTRA IA: seleccionar mazo ---

        private RectTransform BuildContraIA()
        {
            var screen = NewScreen("ContraIA", "fondo_constructor", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.5f));
            Title(screen, "ELIGE TU MAZO");
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

        // --- MIS MAZOS ---

        private RectTransform BuildMisMazos()
        {
            var screen = NewScreen("MisMazos", "fondo_constructor", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.5f));
            Title(screen, "MIS MAZOS");
            BackButton(screen);

            var list = MenuTheme.VBox(screen, 12f, 0, TextAnchor.UpperCenter);
            MenuTheme.Anchor((RectTransform)list.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-240f, 40f), new Vector2(240f, -90f));

            foreach (var m in PlayerData.Mazos())
            {
                var b = MenuTheme.TextButton(list.transform, $"{m.nombre}  ({m.cartas.Count})", 18, () => Push(Screen.DeckBuilder), 460f, 50f);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            }
            var nuevo = MenuTheme.TextButton(list.transform, "+ CREAR NUEVO MAZO", 20, () => Push(Screen.DeckBuilder), 460f, 56f);
            nuevo.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            return screen;
        }

        // --- CONSTRUCTOR (resumen 3 pasos) ---

        private RectTransform BuildDeckBuilder()
        {
            var screen = NewScreen("DeckBuilder", "fondo_constructor", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.55f));
            Title(screen, "CONSTRUCTOR DE MAZOS");
            BackButton(screen);
            var steps = MenuTheme.VBox(screen, 14f, 0, TextAnchor.UpperCenter);
            MenuTheme.Anchor((RectTransform)steps.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-260f, 60f), new Vector2(260f, -100f));
            AddStepLabel(steps.transform, "Paso 1", "Elige la HISTORIA (define las 5 piezas y la condición de victoria).");
            AddStepLabel(steps.transform, "Paso 2", "Arma 40 cartas desde la colección (máx 3 copias). Barra de progreso.");
            AddStepLabel(steps.transform, "Paso 3", "Nombra y guarda el mazo. Validación de reglas.");
            var guardar = MenuTheme.TextButton(steps.transform, "GUARDAR MAZO DE PRUEBA", 18, SaveSampleDeck, 460f, 52f);
            guardar.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            return screen;
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
            var screen = NewScreen("Tomos", "Tomes", MenuTheme.DarkBg);
            MenuTheme.Rect(screen, "Dim", new Color(0f, 0f, 0f, 0.5f));
            Title(screen, "TOMOS");
            BackButton(screen);
            TopBar(screen, withCoins: true);
            var t = MenuTheme.Label(screen, "Abre sobres de cartas con tus monedas.", 20, new Color(0.9f, 0.86f, 0.72f));
            MenuTheme.Anchor((RectTransform)t.transform, new Vector2(0.2f, 0.55f), new Vector2(0.8f, 0.7f), Vector2.zero, Vector2.zero);
            var abrir = MenuTheme.TextButton(screen, "ABRIR TOMO (250 ◈)", 20, () => TryBuy(250), 300f, 56f);
            MenuTheme.Anchor((RectTransform)abrir.transform, new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), new Vector2(-150f, -28f), new Vector2(150f, 28f));
            return screen;
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

        // --- lanzar campo / salir ---

        private void LaunchGame()
        {
            // El campo ya está en la escena, solo desactivado: ocúltase el menú y se enciende.
            if (_board != null)
            {
                _canvas.gameObject.SetActive(false);
                _board.enabled = true; // dispara su Start → construye la partida
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
    }
}
