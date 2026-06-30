using System;
using Game.Core.AI;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Tests
{
    /// <summary>
    /// Verifica que los mazos de muestra + la IA convergen: en una partida IA-vs-IA, alguien
    /// gana (por su HISTORIA o por DÍA) dentro de un número razonable de turnos.
    /// </summary>
    public static class M8AiTests
    {
        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M8 IA vs IA: la mayoría de partidas terminan con un ganador", () =>
            {
                const int total = 10;
                int resolved = 0, sumPlies = 0;
                var winsByPlayer = new System.Collections.Generic.Dictionary<int, int>();
                for (ulong seed = 1; seed <= total; seed++)
                {
                    var eng = new GameEngine(cat)
                    {
                        Effects = CardEffects.BuildResolver(),
                        Decisions = new AutoDecisionProvider(cat)
                    };
                    eng.StartGame(
                        SampleDeckBuilder.Build(cat, "h1", 40, 3),
                        SampleDeckBuilder.Build(cat, "h2", 40, 3),
                        seed, firstPlayer: 0);

                    int plies = 0;
                    while (!eng.State.IsOver && plies < 400)
                    {
                        SimpleAI.PlayTurn(eng);
                        eng.EndTurn();
                        plies++;
                    }
                    if (eng.State.IsOver)
                    {
                        resolved++; sumPlies += plies;
                        int w = eng.State.Winner!.Value;
                        winsByPlayer.TryGetValue(w, out var n); winsByPlayer[w] = n + 1;
                    }
                }

                int avg = resolved > 0 ? sumPlies / resolved : 0;
                winsByPlayer.TryGetValue(0, out var w0); winsByPlayer.TryGetValue(1, out var w1);
                Console.WriteLine($"      [M8] resueltas={resolved}/{total} (prom. {avg} medios-turnos) ganadas P0={w0} P1={w1}");
                TestRunner.IsTrue(resolved >= total * 7 / 10, $"resueltas={resolved}/{total}");
            });
        }
    }
}
