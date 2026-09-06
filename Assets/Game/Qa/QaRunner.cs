using System.Collections.Concurrent;
using System.Collections.Generic;
using Game.Core.Effects;
using Game.Runtime.View;
using UnityEngine;

namespace Game.Qa
{
    /// <summary>
    /// El sistema de pruebas en marcha: escucha las resoluciones de efectos, apunta cada jugada con
    /// la foto del campo y pregunta por los efectos que aún no tienen veredicto definitivo.
    ///
    /// Se crea solo al arrancar el juego (ver <see cref="QaBootstrap"/>), dibuja con IMGUI en su
    /// propio objeto persistente y no toca ninguna escena: por eso borrar esta carpeta no deja
    /// componentes rotos guardados.
    /// </summary>
    public sealed class QaRunner : MonoBehaviour
    {
        // El motor corre los comandos en otro hilo: el evento llega fuera del hilo principal.
        private readonly ConcurrentQueue<EffectPlay> _incoming = new();

        // Cola de preguntas (sin repetir clave) y la que está en pantalla.
        private readonly List<QaEffectId> _queue = new();
        private readonly HashSet<string> _queued = new();
        private QaEffectId? _asking;
        private string _askBoard = "";

        private bool _writingProblem;
        private string _problemText = "";
        private Vector2 _scroll;

        private void OnEnable() => EffectLog.Fired += OnEffectFired;
        private void OnDisable() => EffectLog.Fired -= OnEffectFired;

        /// <summary>Llega desde el hilo del motor: solo encolar, nada de API de Unity aquí.</summary>
        private void OnEffectFired(EffectPlay play) => _incoming.Enqueue(play);

        private void Update()
        {
            while (_incoming.TryDequeue(out var play)) Process(play);
        }

        private void Process(EffectPlay play)
        {
            var d = play.Card.Def;
            string board = SafeBoard();

            // Se apunta CADA efecto resuelto, se pregunte o no: el fallo se ve un par de jugadas
            // más tarde de lo que se causó.
            QaStore.AddPlay($"P{play.OwnerId} · {d.Nombre} ({d.Id}) · {QaEffectId.Etiqueta(play.Trigger)}", board);

            if (!play.HadHandler) return;      // la carta no promete nada aquí: nada que comprobar
            if (QaBootstrap.Resolver == null) return;

            var ef = new QaEffectId(d.Id, play.Trigger, d.Nombre, QaEffects.TextFor(d, play.Trigger));
            var previo = QaStore.Find(ef.Key);
            if (previo != null && previo.IsFinal()) return;   // ya juzgado: callado

            if (_queued.Add(ef.Key))
            {
                _queue.Add(ef);
                if (_asking == null) _askBoard = board;       // foto del momento de preguntar
            }
        }

        private static string SafeBoard()
        {
            try { return MatchSnapshot.Describe?.Invoke() ?? "(sin partida)"; }
            catch { return "(no se pudo leer el campo)"; }
        }

        private void OnGUI()
        {
            if (_asking == null)
            {
                if (_queue.Count == 0) return;
                _asking = _queue[0];
                _queue.RemoveAt(0);
                _writingProblem = false;
                _problemText = "";
            }

            var ef = _asking.Value;

            // Abajo a la derecha, sin tapar el panel de fase ni el detalle de carta.
            const float w = 430f, h = 250f;
            float x = Screen.width - w - 14f;
            float y = Screen.height - h - 14f;

            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);

            GUILayout.Label($"PRUEBA DE EFECTO — {ef.CardName} ({ef.CardId})",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true });
            GUILayout.Label(ef.Label, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

            // El texto del efecto va DENTRO: sin él hay que ir a buscar la carta, y entonces no se contesta.
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(70f));
            GUILayout.Label(ef.Text, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndScrollView();

            if (!_writingProblem)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Sí, funciona", GUILayout.Height(34))) Answer(QaVerdict.Ok, "");
                if (GUILayout.Button("No funciona", GUILayout.Height(34))) _writingProblem = true;
                if (GUILayout.Button("Pendiente", GUILayout.Height(34))) Answer(QaVerdict.Pending, "");
                GUILayout.EndHorizontal();
                GUILayout.Label($"En cola: {_queue.Count}", new GUIStyle(GUI.skin.label) { fontSize = 10 });
            }
            else
            {
                GUILayout.Label("¿Qué hace mal?");
                _problemText = GUILayout.TextField(_problemText ?? "", GUILayout.Height(24));
                GUILayout.BeginHorizontal();
                // Si se cierra a medias no se guarda nada: mejor volver a preguntar que un "mal" sin motivo.
                if (GUILayout.Button("Cancelar", GUILayout.Height(30))) _writingProblem = false;
                GUI.enabled = !string.IsNullOrWhiteSpace(_problemText);
                if (GUILayout.Button("Aceptar", GUILayout.Height(30))) Answer(QaVerdict.Problem, _problemText);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        private void Answer(QaVerdict verdict, string problema)
        {
            var ef = _asking.Value;
            QaStore.SetVerdict(new QaEntry
            {
                Key = ef.Key,
                CardId = ef.CardId,
                CardName = ef.CardName,
                Trigger = ef.Trigger.ToString(),
                EffectText = ef.Text,
                Board = _askBoard,
            }, verdict, problema);

            // Pending vuelve a preguntar la próxima vez que se juegue: fuera de la lista de "ya vistos".
            if (verdict == QaVerdict.Pending) _queued.Remove(ef.Key);

            _asking = null;
            _writingProblem = false;
            _problemText = "";
            QaBootstrap.InvalidateCaches();
        }
    }
}
