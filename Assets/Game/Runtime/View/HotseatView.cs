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

        private GameEngine _engine = null!;
        private Material _baseMat = null!;
        private readonly List<CardView> _spawned = new();
        private string _status = "";

        private void Start()
        {
            EnsureSceneRig();
            _baseMat = new Material(FindCardShader());

            string path = Path.Combine(Application.streamingAssetsPath, "catalogo.v3.json");
            if (!File.Exists(path)) { Debug.LogError($"Falta catálogo en {path}"); return; }
            var catalog = UnityCatalogLoader.FromJson(File.ReadAllText(path));

            _engine = new GameEngine(catalog) { Effects = CardEffects.BuildResolver() };
            _engine.StartGame(
                SampleDeckBuilder.Build(catalog, historiaP0, 40),
                SampleDeckBuilder.Build(catalog, historiaP1, 40),
                seed, firstPlayer: 0);

            _status = "Partida iniciada.";
            Rebuild();
        }

        private void Update()
        {
            if (_engine == null || _engine.State.IsOver) return;
            if (Input.GetMouseButtonDown(0) && Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit))
                {
                    var cv = hit.collider.GetComponentInParent<CardView>();
                    if (cv != null) OnCardClicked(cv);
                }
            }
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
            var s = _engine.State;

            for (int p = 0; p < 2; p++)
            {
                var ps = s.Players[p];
                bool hideHand = p != 0; // ocultar la mano del rival

                for (int i = 0; i < ps.Mano.Count; i++)
                    Spawn(ps.Mano.Cards[i], p, BoardLayout.Hand(p, i, ps.Mano.Count), hideHand);

                for (int i = 0; i < ps.Tierras.Count; i++)
                    Spawn(ps.Tierras.Cards[i], p, BoardLayout.Tierra(p, i), false);

                if (ps.Mazo.Top != null)
                    Spawn(ps.Mazo.Top, p, BoardLayout.Mazo(p), true);

                for (int i = 0; i < ps.Seres.Count; i++)
                    Spawn(ps.Seres.Cards[i], p, BoardLayout.Ser(p, i), false);

                if (ps.Concepto.Top != null)
                    Spawn(ps.Concepto.Top, p, BoardLayout.Concepto(p), true);

                if (ps.Historia != null)
                    Spawn(ps.Historia, p, BoardLayout.Historia(p), false);

                var diaTop = ps.PilaDia.Cards.FirstOrDefault(d => PlayerState.DiaNumero(d) == ps.DiaActual);
                if (diaTop != null)
                    Spawn(diaTop, p, BoardLayout.Dia(p), false);
            }
        }

        private void Spawn(CardInstance c, int owner, Vector3 pos, bool faceDown)
        {
            var v = CardView.Create(transform, _baseMat);
            v.transform.position = pos;
            v.Bind(c, owner, faceDown);
            _spawned.Add(v);
        }

        /// <summary>Shader robusto para URP (evita el magenta de "Standard" bajo URP).</summary>
        private static Shader FindCardShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
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
            cam.orthographic = false;
            cam.transform.position = new Vector3(0f, 13f, -13f);
            cam.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        // ---------------- HUD (IMGUI, sin paquetes) ----------------

        private void OnGUI()
        {
            if (_engine == null) return;
            var s = _engine.State;
            GUILayout.BeginArea(new Rect(10, 10, 360, 220), GUI.skin.box);
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
        }
    }
}
