using System.Collections.Generic;
using UnityEngine;
using PaperIO.Core;

namespace PaperIO.Systems
{
    /// <summary>
    /// Global trail manager.  Stores all players' trail point lists and owns
    /// the LineRenderer pairs (core + glow) that visualise them.
    ///
    /// One TrailSystem serves all players; it manages a LineRenderer pool to
    /// avoid allocations during play.  Mirrors the trail behaviour in src/trail.js.
    ///
    /// Attach to a "TrailSystem" GameObject in the scene.
    /// </summary>
    public class TrailSystem : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Trail Materials")]
        [Tooltip("Material for the opaque core trail line (solid colour).")]
        public Material trailCoreMaterial;

        [Tooltip("Material for the semi-transparent glow trail line.")]
        public Material trailGlowMaterial;

        // ── Per-player data ────────────────────────────────────────────────────
        private class TrailData
        {
            public List<Vector2> points = new();    // Grid-space XZ positions.
            public LineRenderer  coreRenderer;
            public LineRenderer  glowRenderer;
            public Color         color;
            public bool          dirty;             // Renderer needs position update.
        }

        private readonly Dictionary<int, TrailData> _trails = new();
        private GameConfig _config;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _config = GameManager.Instance.config;
        }

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Register a player so the system creates LineRenderers for them.
        /// Called by PlayerBase.InitPlayer.
        /// </summary>
        public void RegisterPlayer(int playerId, Color color)
        {
            if (_trails.ContainsKey(playerId)) return;

            var data = new TrailData { color = color };
            data.coreRenderer = CreateLineRenderer($"Trail_Core_{playerId}", color, _config.trailCoreWidth, 1f);
            data.glowRenderer = CreateLineRenderer($"Trail_Glow_{playerId}", BrightenColor(color, 1.8f), _config.trailGlowWidth, _config.trailGlowAlpha);
            _trails[playerId] = data;
        }

        /// <summary>Unregister a player and destroy their LineRenderers.</summary>
        public void UnregisterPlayer(int playerId)
        {
            if (!_trails.TryGetValue(playerId, out var data)) return;
            if (data.coreRenderer != null) Destroy(data.coreRenderer.gameObject);
            if (data.glowRenderer != null) Destroy(data.glowRenderer.gameObject);
            _trails.Remove(playerId);
        }

        /// <summary>
        /// Append a grid-space point to a player's trail.
        /// Only records the point if it is far enough from the last one
        /// (trailSampleDistance) to avoid bloat.
        /// </summary>
        public void RecordPoint(int playerId, Vector2 pos)
        {
            if (!_trails.TryGetValue(playerId, out var data)) return;

            var pts = data.points;
            if (pts.Count > 0)
            {
                float distSq = (pos - pts[pts.Count - 1]).sqrMagnitude;
                if (distSq < _config.trailSampleDistance * _config.trailSampleDistance)
                    return;
            }

            pts.Add(pos);
            data.dirty = true;
        }

        /// <summary>
        /// Called when a player returns to their own territory.
        /// Returns the trail points for territory capture, then clears the trail.
        /// </summary>
        public void CloseTrail(int playerId, out List<Vector2> closedPoints)
        {
            closedPoints = new List<Vector2>();
            if (!_trails.TryGetValue(playerId, out var data)) return;

            closedPoints.AddRange(data.points);
            data.points.Clear();
            data.dirty = true;
        }

        /// <summary>Discard the trail without capturing territory (on death).</summary>
        public void ClearTrail(int playerId)
        {
            if (!_trails.TryGetValue(playerId, out var data)) return;
            data.points.Clear();
            data.dirty = true;
        }

        /// <summary>Returns the current number of points in a player's trail.</summary>
        public int GetTrailLength(int playerId)
        {
            return _trails.TryGetValue(playerId, out var data) ? data.points.Count : 0;
        }

        /// <summary>Read-only access to a player's trail points (for bot AI).</summary>
        public IReadOnlyList<Vector2> GetTrailPoints(int playerId)
        {
            return _trails.TryGetValue(playerId, out var data)
                ? data.points
                : System.Array.Empty<Vector2>();
        }

        /// <summary>
        /// Sync LineRenderer positions for all dirty trails.
        /// Called once per visual frame by GameManager.Update.
        /// </summary>
        public void UpdateVisuals()
        {
            foreach (var kv in _trails)
            {
                var data = kv.Value;
                if (!data.dirty) continue;

                int count = data.points.Count;
                SyncRenderer(data.coreRenderer, data.points, count);
                SyncRenderer(data.glowRenderer, data.points, count);
                data.dirty = false;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Renderer helpers

        private static void SyncRenderer(LineRenderer lr, List<Vector2> pts, int count)
        {
            if (lr == null) return;

            lr.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                // Trail rendered at y = 0.28 (above ground, below player).
                lr.SetPosition(i, new Vector3(pts[i].x, 0.28f, pts[i].y));
            }
        }

        private LineRenderer CreateLineRenderer(string goName, Color color, float width, float alpha)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount     = 0;
            lr.startWidth        = width;
            lr.endWidth          = width;
            lr.useWorldSpace     = true;
            lr.numCapVertices    = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
            lr.generateLightingData = false;

            // Apply colour.
            Color c = color;
            c.a = alpha;

            // Use provided material or fall back to a simple unlit one.
            Material mat = trailCoreMaterial != null
                ? new Material(trailCoreMaterial)
                : new Material(Shader.Find("Sprites/Default"));
            mat.color    = c;
            lr.material  = mat;

            lr.startColor = c;
            lr.endColor   = c;

            return lr;
        }

        private static Color BrightenColor(Color c, float factor)
            => new Color(
                Mathf.Clamp01(c.r * factor),
                Mathf.Clamp01(c.g * factor),
                Mathf.Clamp01(c.b * factor),
                c.a
            );

        #endregion
    }
}
