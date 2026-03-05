using UnityEngine;

namespace PaperIO.Core
{
    /// <summary>
    /// Smoothly follows the player across the map and adds a screen-shake effect
    /// on death.  Attach to the Main Camera GameObject.
    ///
    /// The camera uses a top-down perspective with a slight tilt so players can
    /// see their trail and incoming enemies.  Height and tilt are driven by
    /// GameConfig.cameraHeight and GameConfig.cameraTilt.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Config (auto-populated from GameManager)")]
        [SerializeField] private GameConfig config;

        // ── Runtime ────────────────────────────────────────────────────────────
        private Transform _target;
        private Vector3   _basePosition;      // Position without shake offset.

        // ── Shake state ────────────────────────────────────────────────────────
        private float  _shakeTimer;
        private float  _shakeIntensity;
        private float  _shakeDuration;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (config == null && GameManager.Instance != null)
                config = GameManager.Instance.config;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // Build the desired world position above the player.
            Vector3 targetGroundPos = new Vector3(
                _target.position.x,
                0f,
                _target.position.z
            );

            // Offset camera behind and above the player based on tilt.
            float tiltRad = config.cameraTilt * Mathf.Deg2Rad;
            float backOffset = config.cameraHeight * Mathf.Tan(tiltRad);
            Vector3 desired = targetGroundPos
                + Vector3.up    * config.cameraHeight
                + Vector3.back  * backOffset;

            // Lerp smoothly toward the desired position.
            _basePosition = Vector3.Lerp(_basePosition, desired, config.cameraLerpFactor);

            // Apply shake offset on top.
            Vector3 shakeOffset = Vector3.zero;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                float progress = _shakeTimer / _shakeDuration;
                float magnitude = _shakeIntensity * progress;

                // Smooth sine-wave shake (no random jitter = better feel on mobile).
                shakeOffset = new Vector3(
                    Mathf.Sin(Time.time * 60f) * magnitude,
                    Mathf.Cos(Time.time * 55f) * magnitude * 0.5f,
                    0f
                );
            }

            transform.position = _basePosition + shakeOffset;

            // Look slightly toward the ground plane based on tilt setting.
            transform.rotation = Quaternion.Euler(90f - config.cameraTilt, 0f, 0f);
        }

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>Set the transform the camera should follow.</summary>
        public void SetTarget(Transform target)
        {
            _target       = target;
            // Snap immediately so there is no initial lerp across the map.
            if (target != null)
                _basePosition = new Vector3(target.position.x, config.cameraHeight, target.position.z - 10f);
        }

        /// <summary>
        /// Trigger a screen shake.  GameManager calls this on player death.
        /// </summary>
        public void TriggerShake(float intensity = -1f, float duration = -1f)
        {
            _shakeIntensity = intensity < 0f ? config.shakeIntensity : intensity;
            _shakeDuration  = duration  < 0f ? config.shakeDuration  : duration;
            _shakeTimer     = _shakeDuration;
        }

        #endregion
    }
}
