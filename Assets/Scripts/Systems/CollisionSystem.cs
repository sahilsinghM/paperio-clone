using System.Collections.Generic;
using UnityEngine;
using PaperIO.Core;
using PaperIO.Player;

namespace PaperIO.Systems
{
    /// <summary>
    /// Detects collisions between players and trails each physics tick.
    ///
    /// Rules (mirrors src/logic.js):
    ///   • A player touching any other player's trail → that player is killed,
    ///     and the colliding player gains credit.
    ///   • A player touching their OWN trail is ignored (with a self-collision
    ///     buffer so the trail tip doesn't immediately kill the player).
    ///
    /// Performance: uses a spatial hash grid (cell size = collisionGridCell) so
    /// trail lookups are O(1) instead of O(n×m).  This keeps the game smooth
    /// at 60 FPS on mobile even with 10 players all trailing.
    ///
    /// Attach to a "CollisionSystem" GameObject in the scene.
    /// </summary>
    public class CollisionSystem : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Spatial Grid")]
        [Tooltip("Size of each spatial hash cell in world units. Should be ~2× collision threshold.")]
        public float spatialCellSize = 2f;

        // ── Runtime ────────────────────────────────────────────────────────────
        private GameConfig  _config;
        private TrailSystem _trailSystem;

        // Spatial hash: maps cell key → list of (ownerId, segmentIndex) pairs.
        private readonly Dictionary<long, List<(int ownerId, int segIdx)>> _spatialHash = new();

        // Reused each tick.
        private readonly List<(int victim, int killer)> _deathQueue = new();

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _config      = GameManager.Instance.config;
            _trailSystem = GameManager.Instance.trailSystem;
        }

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Run a full collision pass.  Called by GameManager after all players
        /// have been moved for this physics tick.
        /// </summary>
        public void CheckAll(IEnumerable<PlayerBase> players)
        {
            _spatialHash.Clear();
            _deathQueue.Clear();

            // 1. Build the spatial hash from all active trails.
            BuildSpatialHash(players);

            // 2. Check each player's position against the spatial hash.
            foreach (var player in players)
            {
                if (!player.IsAlive) continue;
                CheckPlayerCollisions(player);
            }

            // 3. Process deaths outside the enumeration to avoid mutation issues.
            foreach (var (victim, killer) in _deathQueue)
                GameManager.Instance.NotifyPlayerKilled(victim, killer);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Spatial hash construction

        private void BuildSpatialHash(IEnumerable<PlayerBase> players)
        {
            foreach (var player in players)
            {
                if (!player.IsAlive) continue;

                IReadOnlyList<Vector2> pts = _trailSystem.GetTrailPoints(player.PlayerId);
                int count = pts.Count;

                for (int i = 0; i < count - 1; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[i + 1];

                    // Insert segment into all spatial cells it overlaps.
                    InsertSegment(a, b, player.PlayerId, i);
                }
            }
        }

        private void InsertSegment(Vector2 a, Vector2 b, int ownerId, int segIdx)
        {
            // Find the bounding box of the segment.
            float minX = Mathf.Min(a.x, b.x) - _config.collisionThreshold;
            float maxX = Mathf.Max(a.x, b.x) + _config.collisionThreshold;
            float minZ = Mathf.Min(a.y, b.y) - _config.collisionThreshold;
            float maxZ = Mathf.Max(a.y, b.y) + _config.collisionThreshold;

            int cx0 = Mathf.FloorToInt(minX / spatialCellSize);
            int cx1 = Mathf.FloorToInt(maxX / spatialCellSize);
            int cz0 = Mathf.FloorToInt(minZ / spatialCellSize);
            int cz1 = Mathf.FloorToInt(maxZ / spatialCellSize);

            for (int cx = cx0; cx <= cx1; cx++)
            for (int cz = cz0; cz <= cz1; cz++)
            {
                long key = ((long)cx << 32) | (uint)cz;
                if (!_spatialHash.TryGetValue(key, out var list))
                {
                    list = new List<(int, int)>();
                    _spatialHash[key] = list;
                }
                list.Add((ownerId, segIdx));
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Per-player collision check

        private void CheckPlayerCollisions(PlayerBase player)
        {
            Vector2 pos = player.GridPosition2D;

            int cellX = Mathf.FloorToInt(pos.x / spatialCellSize);
            int cellZ = Mathf.FloorToInt(pos.y / spatialCellSize);

            IReadOnlyList<Vector2> ownTrail = _trailSystem.GetTrailPoints(player.PlayerId);
            int ownTrailLen = ownTrail.Count;

            // Search the 3×3 neighbourhood of cells around the player.
            for (int nx = cellX - 1; nx <= cellX + 1; nx++)
            for (int nz = cellZ - 1; nz <= cellZ + 1; nz++)
            {
                long key = ((long)nx << 32) | (uint)nz;
                if (!_spatialHash.TryGetValue(key, out var segments)) continue;

                foreach (var (ownerId, segIdx) in segments)
                {
                    // Skip own trail with self-collision buffer at the tip.
                    if (ownerId == player.PlayerId)
                    {
                        int buffer = _config.selfCollisionBuffer;
                        if (ownTrailLen - segIdx <= buffer) continue;
                    }

                    IReadOnlyList<Vector2> trail = _trailSystem.GetTrailPoints(ownerId);
                    if (segIdx + 1 >= trail.Count) continue;

                    Vector2 a = trail[segIdx];
                    Vector2 b = trail[segIdx + 1];

                    if (PointToSegmentDistSq(pos, a, b) <= _config.collisionThreshold * _config.collisionThreshold)
                    {
                        if (ownerId == player.PlayerId)
                        {
                            // Self-hit → player dies, no killer.
                            EnqueueDeath(player.PlayerId, 0);
                        }
                        else
                        {
                            // Hit enemy trail → enemy dies, this player is the killer.
                            EnqueueDeath(ownerId, player.PlayerId);
                        }
                        return; // One collision per player per tick is enough.
                    }
                }
            }
        }

        private void EnqueueDeath(int victimId, int killerId)
        {
            // Avoid duplicate entries (a player can only die once per tick).
            foreach (var entry in _deathQueue)
                if (entry.victim == victimId) return;
            _deathQueue.Add((victimId, killerId));
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Math utilities

        /// <summary>
        /// Returns the squared distance from point P to segment AB.
        /// Equivalent to the JS perpendicular distance check in logic.js.
        /// </summary>
        private static float PointToSegmentDistSq(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float   lenSq = ab.sqrMagnitude;

            if (lenSq < 1e-6f) return (p - a).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 closest = a + t * ab;
            return (p - closest).sqrMagnitude;
        }

        #endregion
    }
}
