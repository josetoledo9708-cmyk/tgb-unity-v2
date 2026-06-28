using Game.Core.Model;

namespace Game.Core.Engine
{
    public sealed partial class GameEngine
    {
        // Implementación real en M3. Declarados aquí para que fases/comandos compilen en M2.
        private void CheckVictoryAtEntrega(PlayerState p) { /* M3 */ }

        // M2: sin gating de condición (M3 implementa la evaluación real).
        private bool DiaConditionMet(PlayerState p, int diaNum) => true;

        // M2: no-op. M3 declara Victoria I al activar el día 7.
        private void OnDiaActivated(PlayerState p, int diaNum) { /* M3 */ }
    }
}
