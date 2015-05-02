using System;
using System.Linq;

namespace LouieBacajT3.Models
{
    public class Player
    {
        public string Name { get; set; }
        public Player Opponent { get; set; }
        public bool IsPlaying { get; set; }
        public bool WaitingForMove { get; set; }
        public bool LookingForOpponent { get; set; }
        public string Symbol { get; set; }
        public string ConnectionId { get; set; }
    }
}