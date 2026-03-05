using System.Collections.Generic;
using UnityEngine;
using PaperIO.Core;
using PaperIO.Systems;

namespace PaperIO.Player
{
    /// <summary>
    /// Shared data and movement logic for both the human player and bots.
    /// Subclasses provide the turn-input: PlayerController reads from InputManager,
    /// BotController runs an AI state machine.
    ///
    /// World space: grid cell (x, y) maps to world position (x, 0, y).
    /// One world unit == one grid cell.
    /// </summary>
    public abstract class PlayerBase : MonoBehaviour
    {
        // ── Identity ───────────────────────────────────────────────────────────
        public int    PlayerId    { get; private set; }
        public Color  PlayerColor { get; private set; }
        public string PlayerName  { get; private set; }

        // ── Status ─────────────────────────────────────────────────────────────
        public bool IsAlive           { get; private set; }
        public bool IsOnOwnTerritory  { get; protected set; }
        public int  Kills             { get; private set; }

        // ── Movement state ─────────────────────────────────────────────────────
        /// <summary>Direction angle in radians (0 = +X, π/2 = +Z).</summary>
        protected float _angle;

        /// <summary>Turn intent in [-1, 1] set by the subclass each physics tick.</summary>
        protected float _turnInput;

        // ── Cached references ──────────────────────────────────────────────────
        protected GameConfig      _config;
        protected TerritorySystem _territory;
        protected TrailSystem     _trail;

        // ── Visual components ──────────────────────────────────────────────────
        [Header("Visual")]
        [Tooltip("Main body mesh renderer (colour is driven by player colour).")]
        public MeshRenderer bodyRenderer;

        [Tooltip("Directional arrow / cone mesh renderer.")]
        public MeshRenderer arrowRenderer;

        [Tooltip("Point light that illuminates the area around this player.")]
        public Light playerLight;

        // ── Runtime helpers ────────────────────────────────────────────────────
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _mpb;

        // Bobbing animation.
        private float _bobPhase;
        private const float BobFrequency = 2.2f;
        private const float BobAmplitude = 0.12f;

        // Danger flash state (used when trail length exceeds warning threshold).
        private float _dangerFlashTimer;
        private bool  _dangerFlashing;

        // ─────────────────────────────────────────────────────────────────────
        #region Initialization

        protected virtual void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            // Assign a random bob phase so all bots don't bob in sync.
            _bobPhase = Random.value * Mathf.PI * 2f;
        }

        /// <summary>
        /// Call this once after instantiation to set the player's identity and
        /// place them on the map.
        /// </summary>
        public virtual void InitPlayer(int id, Color color, string name, Vector2Int spawnCell)
        {
            PlayerId   = id;
            PlayerColor = color;
            PlayerName  = name;

            _config    = GameManager.Instance.config;
            _territory = GameManager.Instance.territorySystem;
            _trail     = GameManager.Instance.trailSystem;

            // Create LineRenderers for this player's trail.
            _trail.RegisterPlayer(PlayerId, PlayerColor);

            // Position on map.
            transform.position = new Vector3(spawnCell.x + 0.5f, 0f, spawnCell.y + 0.5f);
            _angle = Random.value * Mathf.PI * 2f;

            // Stamp home territory (homeRadius * 2 + 1 square).
            _territory.StampHome(PlayerId, spawnCell, _config.homeRadius);

            IsAlive = true;

            ApplyColor(color);
            GameManager.Instance.RegisterPlayer(this);
        }

        private void ApplyColor(Color c)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorPropId, c);
                bodyRenderer.SetPropertyBlock(_mpb);
            }
            if (arrowRenderer != null)
            {
                arrowRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorPropId, c);
                arrowRenderer.SetPropertyBlock(_mpb);
            }
            if (playerLight != null)
                playerLight.color = c;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Physics tick (called by GameManager at fixed rate)

        /// <summary>
        /// Run one physics step.  GameManager calls this instead of FixedUpdate
        /// so all players tick at the same deterministic rate.
        /// </summary>
        public virtual void PhysicsTick(float dt)
        {
            if (!IsAlive) return;

            // Let the subclass decide turn input this frame.
            ComputeTurnInput(dt);

            // Determine speed: boost when on own territory with no trail.
            int trailLen = _trail.GetTrailLength(PlayerId);
            bool onOwn   = _territory.GetOwner(GridX, GridZ) == PlayerId;
            IsOnOwnTerritory = onOwn;

            float speed = (onOwn && trailLen == 0) ? _config.boostSpeed : _config.normalSpeed;

            // Rotate direction and advance position.
            _angle += _turnInput * _config.turnSpeed * dt;
            Vector3 dir = new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle));
            Vector3 next = transform.position + dir * speed * dt;

            // Boundary reflection (matches JS behaviour).
            float lo = 0.5f, hi = _config.gridSize - 0.5f;
            if (next.x < lo || next.x > hi) { _angle = Mathf.PI - _angle; next.x = Mathf.Clamp(next.x, lo, hi); }
            if (next.z < lo || next.z > hi) { _angle = -_angle;           next.z = Mathf.Clamp(next.z, lo, hi); }

            transform.position = next;

            // Trail management.
            UpdateTrail(onOwn);
        }

        // ─────────────────────────────────────────────────────────────────────
        private void UpdateTrail(bool onOwnTerritory)
        {
            int trailLen = _trail.GetTrailLength(PlayerId);

            if (onOwnTerritory)
            {
                if (trailLen > 0)
                {
                    // Returned home — close the trail and capture territory.
                    _trail.CloseTrail(PlayerId, out List<Vector2> closedPoints);
                    _territory.ClaimTerritory(PlayerId, closedPoints);
                }
            }
            else
            {
                // Off own territory — record trail.
                _trail.RecordPoint(PlayerId, new Vector2(transform.position.x, transform.position.z));

                // Kill the player if trail exceeds the hard limit.
                if (_trail.GetTrailLength(PlayerId) >= _config.trailLimit)
                    GameManager.Instance.NotifyPlayerKilled(PlayerId, 0);
            }
        }

        /// <summary>Override in subclasses to set _turnInput before movement.</summary>
        protected abstract void ComputeTurnInput(float dt);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Per-frame visuals (Update)

        protected virtual void Update()
        {
            if (!IsAlive) return;

            AnimateBob();
            AnimateDanger();
            RotateArrow();
        }

        private void AnimateBob()
        {
            _bobPhase += BobFrequency * Time.deltaTime;
            float bobY = Mathf.Sin(_bobPhase) * BobAmplitude;
            Vector3 pos = transform.position;
            pos.y = bobY + 0.35f; // centre height
            transform.position = pos;
        }

        private void AnimateDanger()
        {
            bool isDangerous = _trail.GetTrailLength(PlayerId) >= _config.trailWarning;
            if (isDangerous)
            {
                _dangerFlashTimer += Time.deltaTime * 6f;
                float flash = (Mathf.Sin(_dangerFlashTimer) + 1f) * 0.5f;
                Color flashColor = Color.Lerp(PlayerColor, Color.red, flash * 0.8f);
                ApplyColor(flashColor);
                if (playerLight != null)
                    playerLight.color = Color.Lerp(Color.yellow, Color.red, flash);
            }
            else
            {
                _dangerFlashTimer = 0f;
                ApplyColor(PlayerColor);
                if (playerLight != null)
                    playerLight.color = PlayerColor;
            }
        }

        private void RotateArrow()
        {
            if (arrowRenderer == null) return;
            // Rotate the arrow child around Y to match movement direction.
            arrowRenderer.transform.rotation = Quaternion.Euler(0f, -_angle * Mathf.Rad2Deg + 90f, 0f);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Death / stats

        public void AddKill() => Kills++;

        public virtual void Kill()
        {
            IsAlive = false;
            gameObject.SetActive(false);
            _trail.ClearTrail(PlayerId);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Helpers

        public int GridX => Mathf.FloorToInt(transform.position.x);
        public int GridZ => Mathf.FloorToInt(transform.position.z);

        /// <summary>World-space position projected onto the XZ grid plane.</summary>
        public Vector2 GridPosition2D => new(transform.position.x, transform.position.z);

        #endregion
    }
}
