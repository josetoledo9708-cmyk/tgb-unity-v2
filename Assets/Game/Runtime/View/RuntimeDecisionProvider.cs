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
        public CardInstance? Result;
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
    /// resuelve por clic. ChooseYesNo/ChooseOption usan un valor por defecto por ahora.
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

        public bool ChooseYesNo(GameState s, string prompt) => true;
        public int ChooseOption(GameState s, IReadOnlyList<string> options, string prompt) => 0;

        /// <summary>Llamado por la UI (hilo principal) para resolver la decisión.</summary>
        public void Resolve(DecisionRequest req, CardInstance? result)
        {
            req.Result = result;
            req.Done.Set();
        }
    }
}
