using System.Collections.Generic;

namespace Game.Core.Model
{
    public sealed class GameState
    {
        public PlayerState[] Players { get; } = { new PlayerState(0), new PlayerState(1) };
        public int ActivePlayer { get; set; }
        public int FirstPlayer { get; set; }
        public Phase Phase { get; set; } = Phase.Preludio;
        public int TurnNumber { get; set; } = 1;
        public ulong Seed { get; set; }

        public int? Winner { get; set; }
        public VictoryId? WinReason { get; set; }
        public bool IsOver => Winner.HasValue;

        public List<string> Log { get; } = new();

        public PlayerState Active => Players[ActivePlayer];
        public PlayerState Opponent => Players[1 - ActivePlayer];
        public PlayerState Other(int id) => Players[1 - id];

        public void Emit(string msg) => Log.Add($"T{TurnNumber} P{ActivePlayer} [{Phase}] {msg}");

        public void DeclareWinner(int player, VictoryId reason)
        {
            if (Winner.HasValue) return;
            Winner = player;
            WinReason = reason;
            Emit($"VICTORIA {reason} -> jugador {player}");
        }
    }
}
