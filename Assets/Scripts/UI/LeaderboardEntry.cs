using System;
using UnityEngine;

namespace PaperIO.UI
{
    /// <summary>Data container for a single leaderboard row.</summary>
    [Serializable]
    public class LeaderboardEntry : IComparable<LeaderboardEntry>
    {
        public int    playerId;
        public string playerName;
        public Color  playerColor;
        public float  territoryPercent;
        public int    kills;

        public LeaderboardEntry(int id, string name, Color color, float pct, int kills)
        {
            playerId         = id;
            playerName       = name;
            playerColor      = color;
            territoryPercent = pct;
            this.kills       = kills;
        }

        /// <summary>Sort descending by territory percentage.</summary>
        public int CompareTo(LeaderboardEntry other)
            => other.territoryPercent.CompareTo(territoryPercent);
    }
}
