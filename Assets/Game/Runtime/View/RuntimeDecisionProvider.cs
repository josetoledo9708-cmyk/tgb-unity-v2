using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Core.Effects;
using Game.Core.Model;

namespace Game.Runtime.View
{
    /// <summary>Una decisión pendiente: el motor (en su hilo) espera a que la UI la resuelva.</summary>
    public sealed class DecisionRequest
    {
        public IReadOnlyList<CardInstance> Options { get; }
        public string Prompt { get; }
        public bool Optional { get; }
        public bool IsOrder;                 // true = elegir orden de todas; false = elegir 1
        public bool IsYesNo;                 // true = pregunta de sí/no (sin cartas)
        public CardInstance? Result;         // modo seleccionar
        public List<CardInstance>? ResultOrder; // modo ordenar
        public bool ResultYes;               // modo sí/no
        public readonly ManualResetEventSlim Done = new(false);

        public DecisionRequest(IReadOnlyList<CardInstance> options, string prompt, bool optional)
        {
            Options = options;
            Prompt = prompt;
            Optional = optional;
        }
    }

    /// <summary>
    /// Proveedor de decisiones interactivo: cuando el motor (corriendo en un hilo) pide elegir
    /// una carta, publica un DecisionRequest y BLOQUEA hasta que la UI (hilo principal) lo
    /// resuelve por clic. Lo mismo para las preguntas de sí/no (efectos opcionales).
    /// </summary>
    public sealed class RuntimeDecisionProvider : IDecisionProvider
    {
        private volatile DecisionRequest? _pending;
        public DecisionRequest? Pending => _pending;

        public CardInstance? ChooseCard(GameState s, IReadOnlyList<CardInstance> options,
                                        string prompt, bool optional)
        {
            if (options.Count == 0) return null;
            var req = new DecisionRequest(options.ToList(), prompt, optional);
            _pending = req;
            req.Done.Wait();   // bloquea el hilo del motor hasta que la UI responda
            _pending = null;
            return req.Result;
        }

        /// <summary>Pregunta de sí/no (efectos OPCIONALES, p. ej. Río Tigris). Bloquea el hilo del
        /// motor hasta que el jugador responde en pantalla.</summary>
        public bool ChooseYesNo(GameState s, string prompt)
        {
            var req = new DecisionRequest(System.Array.Empty<CardInstance>(), prompt, optional: true) { IsYesNo = true };
            _pending = req;
            req.Done.Wait();
            _pending = null;
            return req.ResultYes;
        }
        public int ChooseOption(GameState s, IReadOnlyList<string> options, string prompt) => 0;

        public IReadOnlyList<CardInstance> ChooseOrder(GameState s, IReadOnlyList<CardInstance> cards, string prompt)
        {
            if (cards.Count <= 1) return cards;
            var req = new DecisionRequest(cards.ToList(), prompt, optional: false) { IsOrder = true };
            _pending = req;
            req.Done.Wait();
            _pending = null;
            return req.ResultOrder ?? cards;
        }

        /// <summary>Llamado por la UI (hilo principal) para resolver una selección.</summary>
        public void Resolve(DecisionRequest req, CardInstance? result)
        {
            req.Result = result;
            req.Done.Set();
        }

        /// <summary>Resuelve una pregunta de sí/no.</summary>
        public void ResolveYesNo(DecisionRequest req, bool yes)
        {
            req.ResultYes = yes;
            req.Done.Set();
        }

        /// <summary>Resuelve una decisión de orden con la lista ordenada.</summary>
        public void ResolveOrder(DecisionRequest req, List<CardInstance> order)
        {
            req.ResultOrder = order;
            req.Done.Set();
        }
    }
}
