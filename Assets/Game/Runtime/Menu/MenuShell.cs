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
    public sealed class MenuShell : MonoBehaviour
    {
        public enum Screen { MainMenu, Historias, ContraIA, Multijugador, MisMazos, SelectDeck, ChooseHistoria, DeckBuilder, Misiones, Tienda, Tomos, Opciones }

        private Canvas _canvas;
        private RectTransform _root;      // contenedor de la pantalla activa
        private GameObject _current;
        private GameObject _modal; // overlay opcional (opciones de mazo, renombrar) encima de la pantalla activa
        private readonly List<Screen> _stack = new();
        private Game.Runtime.View.HotseatView _board;
        private CardCatalog _catalog;      // catálogo de cartas, para previews reales en los modales
        private CardArtLibrary _cardArt;   // arte de carta, misma carpeta que usa el campo
        private string _chosenHistoriaId;  // carta Historia elegida al crear una nueva historia/mazo

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
            CloseModal();
            if (_current != null) Destroy(_current);
            _current = Build(s).gameObject;
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
            gearBtn.onClick.AddListener(() => Push(Screen.Opciones));
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

            // --- Tomos = libro ornamentado a la derecha-centro ---
            var tomos = FloatingIcon(screen, "Tomes", "TOMOS", () => Push(Screen.Tomos));
            MenuTheme.Anchor((RectTransform)tomos.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-200f, -110f), new Vector2(-40f, 110f));
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
            Title(screen, "ELIGE TU MAZO");
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

            var vbox = MenuTheme.VBox(screen, 20f, 0, TextAnchor.MiddleCenter);
            MenuTheme.Anchor((RectTransform)vbox.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-580f, 90f), new Vector2(580f, -110f));
            var row1 = MenuTheme.HBox(vbox.transform, 20f, 0, TextAnchor.MiddleCenter);
            var row2 = MenuTheme.HBox(vbox.transform, 20f, 0, TextAnchor.MiddleCenter);

            const float cellW = 264f, cellH = 189f;
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

            // SIGUIENTE (abajo-derecha): crea la historia con la carta elegida y vuelve a Mis Historias.
            siguiente = MenuTheme.TextButton(screen, "SIGUIENTE", 16, () =>
            {
                if (_chosenHistoriaId == null) return;
                var mazos = PlayerData.Mazos();
                mazos.Add(new DeckEntry { nombre = "Historia " + (mazos.Count + 1), historiaId = _chosenHistoriaId });
                PlayerData.SaveMazos(mazos);
                Show(Screen.MisMazos);
                _stack[_stack.Count - 1] = Screen.MisMazos;
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
            MenuTheme.Anchor((RectTransform)img.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-402f, -60f), new Vector2(30f, 324f));
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
            MenuTheme.Rect(overlay, "Dim", new Color(0f, 0f, 0f, 0.6f));

            const float panelW = 760f, panelH = 520f;
            ModalPanel(overlay, panelW, panelH, out var inner);

            // Carta representativa (real, con arte): el DÍA de esa historia, elegido de forma
            // determinística por mazo (mismo mazo siempre muestra la misma carta).
            int diaNum = 1 + System.Math.Abs((m.nombre + "|" + m.historiaId).GetHashCode()) % 7;
            if (_catalog != null && _catalog.TryGet("dia" + diaNum, out var diaDef))
                BuildCardPreview(overlay, diaDef, panelW, panelH);

            var title = MenuTheme.Label(inner, m.nombre, 30, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -66f), new Vector2(0f, -12f));

            var sub = MenuTheme.Label(inner, $"Historia: {HistoriaName(m.historiaId)}\n{m.cartas.Count} cartas", 17, new Color(0.85f, 0.82f, 0.72f));
            MenuTheme.Anchor((RectTransform)sub.transform, new Vector2(0f, 0.62f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero);

            var list = MenuTheme.VBox(inner, 12f, 0, TextAnchor.UpperCenter);
            MenuTheme.Anchor((RectTransform)list.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.6f), new Vector2(-220f, 24f), new Vector2(220f, -8f));
            AddModalOption(list.transform, "EDITAR CARTAS", MenuTheme.Gold, () => { CloseModal(); Push(Screen.DeckBuilder); }, spriteKey: "minimenu/editar_cartas");
            AddModalOption(list.transform, "CAMBIAR PORTADA", MenuTheme.Gold, () => { /* pendiente: sin sistema de portada personalizada aún */ CloseModal(); }, spriteKey: "minimenu/cambiar_portada");
            AddModalOption(list.transform, "RENOMBRAR", MenuTheme.Gold, () => ShowRenameDialog(index), spriteKey: "minimenu/renombrar");
            AddModalOption(list.transform, "BORRAR", new Color(0.85f, 0.28f, 0.28f), () => { DeleteMazo(index); CloseModal(); RefreshMisMazos(); }, spriteKey: "minimenu/borrar");
            AddModalOption(list.transform, "CERRAR", new Color(0.8f, 0.78f, 0.7f), CloseModal, spriteKey: "minimenu/cerrar");
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
            MenuTheme.Rect(overlay, "Dim", new Color(0f, 0f, 0f, 0.6f));

            var marcoSprite = MenuAssets.Sprite("minimenu/renombrar_marco");
            RectTransform inner;
            RectTransform inputArea;
            RectTransform btnArea;

            if (marcoSprite != null)
            {
                // El marco ya trae el título "RENOMBRAR HISTORIA" y el recuadro del campo baked.
                var go = new GameObject("Marco", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(overlay, false);
                var img = go.GetComponent<Image>();
                img.sprite = marcoSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                const float w = 560f;
                float h = w * marcoSprite.rect.height / marcoSprite.rect.width;
                MenuTheme.Anchor((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-w / 2f, -h / 2f + 40f), new Vector2(w / 2f, h / 2f + 40f));
                inner = (RectTransform)go.transform;
                inputArea = inner; // el campo se ancla dentro del recuadro baked (posición aproximada)
                btnArea = inner;
            }
            else
            {
                ModalPanel(overlay, 440f, 210f, out inner);
                var title = MenuTheme.Label(inner, "RENOMBRAR MAZO", 20, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                MenuTheme.Anchor((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, -8f));
                inputArea = inner;
                btnArea = inner;
            }

            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(inputArea, false);
            var inputImg = inputGo.GetComponent<Image>();
            inputImg.color = marcoSprite != null ? new Color(0f, 0f, 0f, 0.001f) : new Color(0.12f, 0.11f, 0.08f, 1f); // sobre el recuadro baked, casi invisible
            MenuTheme.Anchor((RectTransform)inputGo.transform,
                marcoSprite != null ? new Vector2(0.12f, 0.30f) : new Vector2(0.08f, 0.42f),
                marcoSprite != null ? new Vector2(0.88f, 0.46f) : new Vector2(0.92f, 0.68f),
                Vector2.zero, Vector2.zero);
            var field = inputGo.GetComponent<InputField>();
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(inputGo.transform, false);
            var txt = textGo.GetComponent<Text>();
            txt.font = MenuAssets.Font();
            txt.color = MenuTheme.Gold;
            txt.fontSize = 18;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.supportRichText = false;
            MenuTheme.Anchor((RectTransform)textGo.transform, Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            field.textComponent = txt;
            field.text = m.nombre;
            field.characterLimit = 24;

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

            var save = AddModalOption(btnArea, "GUARDAR", MenuTheme.Gold, DoSave, width: 180f, spriteKey: "minimenu/btn_aceptar");
            MenuTheme.Anchor((RectTransform)save.transform, new Vector2(0.5f, 0.06f), new Vector2(0.5f, 0.06f), new Vector2(-190f, 0f), new Vector2(-10f, 44f));

            var cancel = AddModalOption(btnArea, "CANCELAR", new Color(0.8f, 0.78f, 0.7f), CloseModal, width: 180f, spriteKey: "minimenu/cancelar");
            MenuTheme.Anchor((RectTransform)cancel.transform, new Vector2(0.5f, 0.06f), new Vector2(0.5f, 0.06f), new Vector2(10f, 0f), new Vector2(190f, 44f));
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
        private void BuildCardPreview(RectTransform overlay, CardDefinition def, float panelWidth, float panelHeight)
        {
            const float w = 230f, h = 340f;
            float cx = -(panelWidth / 2f) + 40f; // se superpone al borde izquierdo del panel

            var box = new GameObject("CardPreview", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(overlay, false);
            var bimg = box.GetComponent<Image>();
            bimg.sprite = MenuGraphics.Rounded(64, 10);
            bimg.type = Image.Type.Sliced;
            bimg.color = MenuTheme.MetalGold;
            var brt = (RectTransform)box.transform;
            MenuTheme.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx - w / 2f, -h / 2f), new Vector2(cx + w / 2f, h / 2f));
            // Sibling por defecto (último = encima): visible sobre Dim y superpuesto al panel.

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
            var sprite = spriteKey != null ? MenuAssets.Sprite(spriteKey) : null;
            var go = new GameObject("Opt_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, 48f);
            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cols = btn.colors;
            cols.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            btn.colors = cols;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            go.AddComponent<HoverScale>();
            go.AddComponent<LayoutElement>().preferredHeight = 48f;

            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                return btn;
            }

            img.color = new Color(0.04f, 0.03f, 0.02f, 0.92f);
            var txt = MenuTheme.Label(rt, label, 18, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Stretch((RectTransform)txt.transform);
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
            // Carta HISTORIA elegida como representación del mazo; si no hay, Hoja/color.
            var cartaSprite = MenuAssets.Sprite("cartas_historia/carta_" + hid);
            Image cover;
            if (cartaSprite != null)
            {
                cover = MenuTheme.Picture(inner, "Cover", cartaSprite, preserveAspect: false);
                cover.color = Color.white;
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
            MenuTheme.Anchor((RectTransform)cover.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(2f, -110f), new Vector2(-2f, -2f));

            var badge = MenuTheme.Rect(inner, "Badge", MenuTheme.MetalGold);
            badge.sprite = MenuGraphics.Rounded(32, 16);
            var brt = (RectTransform)badge.transform;
            brt.sizeDelta = new Vector2(28f, 28f);
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-20f, -20f);
            var idxLabel = MenuTheme.Label(badge.transform, string.IsNullOrEmpty(hid) ? "?" : hid.Replace("h", ""), 14, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Stretch((RectTransform)idxLabel.transform);

            var histName = MenuTheme.Label(inner, HistoriaName(m.historiaId), 12, new Color(0.85f, 0.82f, 0.7f), TextAnchor.MiddleCenter);
            MenuTheme.Anchor((RectTransform)histName.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 80f), new Vector2(-6f, 112f));

            var countLbl = MenuTheme.Label(inner, $"{m.cartas.Count} cartas", 11, new Color(0.7f, 0.66f, 0.55f), TextAnchor.MiddleCenter);
            MenuTheme.Anchor((RectTransform)countLbl.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 62f), new Vector2(-6f, 80f));

            var mazoLbl = MenuTheme.Label(inner, "MAZO", 13, new Color(0.8f, 0.76f, 0.6f), TextAnchor.MiddleCenter);
            MenuTheme.Anchor((RectTransform)mazoLbl.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 34f), new Vector2(-6f, 58f));

            var nameLbl = MenuTheme.Label(inner, m.nombre.ToUpperInvariant(), 18, MenuTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuTheme.Anchor((RectTransform)nameLbl.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 4f), new Vector2(-6f, 34f));

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
