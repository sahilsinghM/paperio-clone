using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using PaperIO.Core;

namespace PaperIO.Systems
{
    /// <summary>
    /// Owns the 400×400 territory grid, handles flood-fill area capture, and
    /// renders territory as a single colour-mapped Texture2D on a ground plane.
    ///
    /// Algorithm (mirrors src/logic.js claimTerritory):
    ///   1. Rasterise the closed trail onto the grid as boundary cells.
    ///   2. BFS flood-fill from all four map edges to find "outside" cells.
    ///   3. Any cell NOT reached by the flood fill (and not already owned by
    ///      the player) is enclosed and gets claimed.
    ///   4. Trigger a brief brightness animation on newly claimed cells.
    ///
    /// Attach to a "TerritorySystem" GameObject in the scene.
    /// </summary>
    public class TerritorySystem : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Territory Rendering")]
        [Tooltip("The plane mesh that displays territory colours. Scale it to cover the grid.")]
        public MeshRenderer territoryPlane;

        [Tooltip("Shader used for the territory texture plane. Unlit/Transparent works well.")]
        public Shader territoryShader;

        // ── Grid ───────────────────────────────────────────────────────────────
        private int    _gridSize;
        private int[]  _grid;        // Stores owner playerId (0 = neutral).

        // ── Texture ────────────────────────────────────────────────────────────
        private Texture2D _texture;
        private Color32[] _pixels;   // Direct pixel buffer for fast updates.
        private bool      _textureDirty;
        private Material  _territoryMaterial;

        // ── Fill animations ────────────────────────────────────────────────────
        // Maps cell index → normalised animation progress [0,1].
        private readonly Dictionary<int, float> _fillAnims = new();
        private GameConfig _config;

        // ── BFS work buffers (reused to avoid GC) ─────────────────────────────
        private readonly Queue<int>  _bfsQueue    = new();
        private readonly HashSet<int> _visited    = new();

        // ─────────────────────────────────────────────────────────────────────
        #region Initialization

        public void Initialize(int gridSize)
        {
            _gridSize = gridSize;
            _config   = GameManager.Instance.config;
            _grid     = new int[gridSize * gridSize];

            // Build the territory texture and material.
            _texture = new Texture2D(gridSize, gridSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
                name       = "TerritoryTexture"
            };
            _pixels  = new Color32[gridSize * gridSize];

            // All pixels start transparent (neutral ground shows through).
            System.Array.Clear(_pixels, 0, _pixels.Length);
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            // Apply to the territory plane.
            if (territoryPlane != null)
            {
                var shader = territoryShader ?? Shader.Find("Sprites/Default");
                _territoryMaterial = new Material(shader);
                _territoryMaterial.mainTexture = _texture;
                territoryPlane.sharedMaterial  = _territoryMaterial;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Unity lifecycle

        private void Update()
        {
            TickFillAnimations();

            if (_textureDirty)
            {
                _texture.SetPixels32(_pixels);
                _texture.Apply(false);
                _textureDirty = false;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>Returns the owner player ID at grid cell (x, z). 0 = neutral.</summary>
        public int GetOwner(int x, int z)
        {
            if (!InBounds(x, z)) return 0;
            return _grid[Idx(x, z)];
        }

        /// <summary>
        /// Stamps a (2*radius+1)^2 square of home territory for the player at
        /// the given centre cell.
        /// </summary>
        public void StampHome(int playerId, Vector2Int centre, int radius)
        {
            Color playerColor = GetPlayerColor(playerId);
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = centre.x + dx;
                int z = centre.y + dz;
                if (!InBounds(x, z)) continue;
                int idx = Idx(x, z);
                _grid[idx] = playerId;
                _pixels[idx] = ToColor32(playerColor, 0.9f);
            }
            _textureDirty = true;
        }

        /// <summary>
        /// Performs the flood-fill capture algorithm when a player closes their
        /// trail.  Called by PlayerBase.UpdateTrail via TrailSystem.
        /// </summary>
        public void ClaimTerritory(int playerId, List<Vector2> trailPoints)
        {
            if (trailPoints == null || trailPoints.Count < 2) return;

            // 1. Rasterise trail onto a temporary boundary layer.
            HashSet<int> boundary = RasteriseTrail(trailPoints);

            // 2. Mark trail cells as player-owned (the perimeter).
            foreach (int idx in boundary)
            {
                _grid[idx] = playerId;
            }

            // 3. Flood-fill from all four edges to mark "outside" cells.
            HashSet<int> outside = FloodFillOutside(boundary);

            // 4. Every non-outside, non-player cell that is inside is now claimed.
            Color playerColor = GetPlayerColor(playerId);
            List<int> newCells = new();

            for (int z = 0; z < _gridSize; z++)
            for (int x = 0; x < _gridSize; x++)
            {
                int idx = Idx(x, z);
                if (outside.Contains(idx)) continue;
                if (_grid[idx] == playerId) continue;

                _grid[idx]    = playerId;
                newCells.Add(idx);
            }

            // 5. Animate newly claimed cells and update texture for boundaries.
            foreach (int idx in boundary)
            {
                _fillAnims[idx] = 0f; // start animation.
                _pixels[idx]    = ToColor32(playerColor, 0.9f);
            }
            foreach (int idx in newCells)
            {
                _fillAnims[idx] = 0f;
                _pixels[idx]    = ToColor32(playerColor, 0.9f);
            }

            _textureDirty = true;

            GameManager.Instance.NotifyTerritoryCapture(playerId);
        }

        /// <summary>Transfer all cells owned by victim to the killer.</summary>
        public void TransferTerritory(int victimId, int killerId)
        {
            Color killerColor = GetPlayerColor(killerId);
            for (int i = 0; i < _grid.Length; i++)
            {
                if (_grid[i] != victimId) continue;
                _grid[i]    = killerId;
                _pixels[i]  = ToColor32(killerColor, 0.9f);
                _fillAnims[i] = 0f;
            }
            _textureDirty = true;
        }

        /// <summary>Clear all territory belonging to this player (on death with no killer).</summary>
        public void ClearTerritory(int playerId)
        {
            for (int i = 0; i < _grid.Length; i++)
            {
                if (_grid[i] != playerId) continue;
                _grid[i]   = 0;
                _pixels[i] = new Color32(0, 0, 0, 0);
            }
            _textureDirty = true;
        }

        /// <summary>Count how many cells are owned by a given player.</summary>
        public int GetTerritoryCount(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _grid.Length; i++)
                if (_grid[i] == playerId) count++;
            return count;
        }

        /// <summary>Territory percentage for this player (0–100).</summary>
        public float GetTerritoryPercent(int playerId)
            => GetTerritoryCount(playerId) * 100f / (_gridSize * _gridSize);

        /// <summary>Find the closest owned cell to the given grid position (for bot retreat).</summary>
        public Vector2Int FindNearestOwnedCell(int playerId, Vector2Int from)
        {
            int    bestDist = int.MaxValue;
            Vector2Int best = from;

            for (int z = 0; z < _gridSize; z++)
            for (int x = 0; x < _gridSize; x++)
            {
                if (_grid[Idx(x, z)] != playerId) continue;
                int dist = Mathf.Abs(x - from.x) + Mathf.Abs(z - from.y);
                if (dist < bestDist) { bestDist = dist; best = new Vector2Int(x, z); }
            }
            return best;
        }

        /// <summary>Find a random cell that is not owned by any player.</summary>
        public Vector2Int RandomEmptyCell()
        {
            int gs      = _gridSize;
            int attempts = 200;
            while (attempts-- > 0)
            {
                int x = Random.Range(gs / 10, gs * 9 / 10);
                int z = Random.Range(gs / 10, gs * 9 / 10);
                if (_grid[Idx(x, z)] == 0)
                    return new Vector2Int(x, z);
            }
            return new Vector2Int(gs / 2, gs / 2);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Trail rasterisation (Bresenham)

        /// <summary>
        /// Converts the list of floating-point trail positions into a set of
        /// integer grid cell indices using Bresenham's line algorithm.
        /// </summary>
        private HashSet<int> RasteriseTrail(List<Vector2> trailPoints)
        {
            var result = new HashSet<int>();
            for (int i = 0; i < trailPoints.Count - 1; i++)
                DrawLine(trailPoints[i], trailPoints[i + 1], result);
            return result;
        }

        private void DrawLine(Vector2 a, Vector2 b, HashSet<int> output)
        {
            int x0 = Mathf.RoundToInt(a.x), z0 = Mathf.RoundToInt(a.y);
            int x1 = Mathf.RoundToInt(b.x), z1 = Mathf.RoundToInt(b.y);

            int dx = Mathf.Abs(x1 - x0), dz = Mathf.Abs(z1 - z0);
            int sx = x0 < x1 ? 1 : -1,   sz = z0 < z1 ? 1 : -1;
            int err = dx - dz;

            while (true)
            {
                if (InBounds(x0, z0)) output.Add(Idx(x0, z0));
                if (x0 == x1 && z0 == z1) break;

                int e2 = err * 2;
                if (e2 > -dz) { err -= dz; x0 += sx; }
                if (e2 <  dx) { err += dx; z0 += sz; }
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Flood fill

        /// <summary>
        /// BFS from all four map edges; returns the set of cell indices reachable
        /// without crossing boundary cells.  These are "outside" the loop.
        /// </summary>
        private HashSet<int> FloodFillOutside(HashSet<int> boundary)
        {
            _bfsQueue.Clear();
            _visited.Clear();

            // Seed the queue with all edge cells that are not boundary.
            int gs = _gridSize;
            for (int i = 0; i < gs; i++)
            {
                TryEnqueue(Idx(i,    0),    boundary);
                TryEnqueue(Idx(i,    gs-1), boundary);
                TryEnqueue(Idx(0,    i),    boundary);
                TryEnqueue(Idx(gs-1, i),    boundary);
            }

            int[] dx = { 1, -1, 0,  0 };
            int[] dz = { 0,  0, 1, -1 };

            while (_bfsQueue.Count > 0)
            {
                int idx = _bfsQueue.Dequeue();
                int x   = idx % _gridSize;
                int z   = idx / _gridSize;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dx[d], nz = z + dz[d];
                    if (!InBounds(nx, nz)) continue;
                    TryEnqueue(Idx(nx, nz), boundary);
                }
            }

            return _visited;
        }

        private void TryEnqueue(int idx, HashSet<int> boundary)
        {
            if (_visited.Contains(idx) || boundary.Contains(idx)) return;
            _visited.Add(idx);
            _bfsQueue.Enqueue(idx);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Fill animation

        private void TickFillAnimations()
        {
            if (_fillAnims.Count == 0) return;

            float dt   = Time.deltaTime;
            float rate = 1f / _config.fillAnimDuration;

            var toRemove = new List<int>();

            foreach (var kv in _fillAnims)
            {
                int   idx      = kv.Key;
                float progress = kv.Value + dt * rate;

                if (progress >= 1f)
                {
                    // Animation finished — write final colour.
                    int owner = _grid[idx];
                    _pixels[idx] = owner == 0
                        ? new Color32(0, 0, 0, 0)
                        : ToColor32(GetPlayerColor(owner), 0.9f);
                    toRemove.Add(idx);
                }
                else
                {
                    _fillAnims[idx] = progress;
                    // Flash bright then settle.
                    int owner = _grid[idx];
                    if (owner != 0)
                    {
                        float bright = 1f + _config.fillAnimBrightness * (1f - progress);
                        Color bc     = GetPlayerColor(owner) * bright;
                        _pixels[idx] = ToColor32(bc, 0.95f);
                    }
                }
                _textureDirty = true;
            }

            foreach (int idx in toRemove)
                _fillAnims.Remove(idx);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Helpers

        private int Idx(int x, int z) => z * _gridSize + x;

        private bool InBounds(int x, int z)
            => x >= 0 && x < _gridSize && z >= 0 && z < _gridSize;

        private static Color32 ToColor32(Color c, float alpha)
            => new Color32(
                (byte)(Mathf.Clamp01(c.r) * 255),
                (byte)(Mathf.Clamp01(c.g) * 255),
                (byte)(Mathf.Clamp01(c.b) * 255),
                (byte)(alpha * 255)
            );

        private Color GetPlayerColor(int playerId)
        {
            if (playerId == 0) return Color.clear;
            if (!GameManager.Instance.TryGetPlayer(playerId, out var player))
                return Color.gray;
            return player.PlayerColor;
        }

        #endregion
    }
}
