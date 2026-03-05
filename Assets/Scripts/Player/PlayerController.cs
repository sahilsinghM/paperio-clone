using UnityEngine;
using PaperIO.Core;

namespace PaperIO.Player
{
    /// <summary>
    /// Human-controlled player.  Reads directional input from InputManager and
    /// passes it to the shared PlayerBase movement system.
    ///
    /// Attach to the "Player" GameObject in the scene alongside the visual
    /// components (MeshRenderer for body, MeshRenderer for arrow, Light).
    /// </summary>
    public class PlayerController : PlayerBase
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Player")]
        [Tooltip("Display name shown on the leaderboard and kill feed.")]
        public string defaultPlayerName = "You";

        // ── Cached ─────────────────────────────────────────────────────────────
        private InputManager _input;

        // ─────────────────────────────────────────────────────────────────────
        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            _input = InputManager.Instance;
            if (_input == null)
                Debug.LogError("[PlayerController] InputManager not found in scene.");
        }

        // ─────────────────────────────────────────────────────────────────────
        #region PlayerBase overrides

        protected override void ComputeTurnInput(float dt)
        {
            // Delegate entirely to InputManager; it already normalises to [-1, 1].
            _turnInput = _input != null ? _input.TurnInput : 0f;
        }

        public override void Kill()
        {
            base.Kill();
            // Trigger camera shake on human death.
            GameManager.Instance.cameraController.TriggerShake();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public helpers (called by GameManager)

        /// <summary>
        /// Re-initializes the player for a fresh game or respawn.
        /// GameManager calls this; do not call directly.
        /// </summary>
        public new void InitPlayer(int id, Color color, string name, Vector2Int spawnCell)
        {
            gameObject.SetActive(true);
            base.InitPlayer(id, color, name, spawnCell);
        }

        #endregion
    }
}
