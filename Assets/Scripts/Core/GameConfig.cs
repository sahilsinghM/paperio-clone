using UnityEngine;

namespace PaperIO.Core
{
    /// <summary>
    /// ScriptableObject holding all tunable game constants.
    /// Create via Assets > Create > PaperIO > GameConfig.
    /// Mirrors the constants from the original src/config.js.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "PaperIO/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Map")]
        [Tooltip("Number of cells along each axis (400 x 400 grid).")]
        public int gridSize = 400;

        [Header("Movement")]
        [Tooltip("Normal movement speed in world units per second.")]
        public float normalSpeed = 52f;

        [Tooltip("Boosted speed when fully on own territory with no trail.")]
        public float boostSpeed = 72f;

        [Tooltip("Turn rate in radians per second.")]
        public float turnSpeed = 3.5f;

        [Header("Trail")]
        [Tooltip("Maximum trail length (points). Player is killed when exceeded.")]
        public int trailLimit = 800;

        [Tooltip("Trail length at which danger warnings appear.")]
        public int trailWarning = 280;

        [Tooltip("Minimum world-unit distance between consecutive trail samples.")]
        public float trailSampleDistance = 0.6f;

        [Tooltip("Line width of the trail core renderer.")]
        public float trailCoreWidth = 0.08f;

        [Tooltip("Line width of the trail glow renderer (wider, semi-transparent).")]
        public float trailGlowWidth = 0.18f;

        [Tooltip("Opacity of the glow trail layer.")]
        [Range(0f, 1f)]
        public float trailGlowAlpha = 0.22f;

        [Header("Territory")]
        [Tooltip("Half-size of the initial home square (homeSize = 2*radius+1). "
               + "9 means a 9x9 stamp (radius 4).")]
        public int homeRadius = 4;

        [Tooltip("Duration in seconds of the territory fill animation.")]
        public float fillAnimDuration = 0.35f;

        [Tooltip("Brightness multiplier during fill animation peak.")]
        public float fillAnimBrightness = 2.2f;

        [Header("Bots")]
        [Tooltip("Maximum simultaneous bots in the game.")]
        public int maxBots = 9;

        [Tooltip("Seconds before a dead bot respawns.")]
        public float botRespawnDelay = 2f;

        [Tooltip("Seconds before the human player can respawn after death.")]
        public float humanRespawnDelay = 2f;

        [Tooltip("How often (seconds) a bot re-evaluates its AI decision.")]
        public float botDecisionInterval = 0.2f;

        [Tooltip("Random variance added to bot decision interval.")]
        public float botDecisionVariance = 0.1f;

        [Header("Physics")]
        [Tooltip("Physics tick rate in seconds (16 ms = 62.5 ticks/sec).")]
        public float physicsTickInterval = 0.016f;

        [Tooltip("Buffer trail points at the start to skip self-collision.")]
        public int selfCollisionBuffer = 10;

        [Tooltip("Min point-to-segment distance to register a trail hit.")]
        public float collisionThreshold = 0.45f;

        [Header("Camera")]
        [Tooltip("Lerp factor for camera follow smoothing.")]
        [Range(0.01f, 1f)]
        public float cameraLerpFactor = 0.1f;

        [Tooltip("Camera height above the map plane.")]
        public float cameraHeight = 60f;

        [Tooltip("Camera tilt angle in degrees (0 = straight down).")]
        public float cameraTilt = 25f;

        [Header("Death FX")]
        [Tooltip("Screen shake intensity on player death.")]
        public float shakeIntensity = 0.8f;

        [Tooltip("Screen shake duration in seconds.")]
        public float shakeDuration = 0.4f;

        [Header("Colors")]
        [Tooltip("Pool of player colors (10 entries matching the JS palette).")]
        public Color[] playerColors = new Color[]
        {
            new Color(0.22f, 0.71f, 0.96f), // #38b5f5 blue
            new Color(0.95f, 0.35f, 0.35f), // #f35959 red
            new Color(0.30f, 0.85f, 0.45f), // #4cd974 green
            new Color(0.98f, 0.75f, 0.18f), // #fac02e yellow
            new Color(0.68f, 0.35f, 0.96f), // #ae59f5 purple
            new Color(0.98f, 0.55f, 0.18f), // #fa8c2e orange
            new Color(0.20f, 0.85f, 0.85f), // #33d9d9 cyan
            new Color(0.96f, 0.35f, 0.75f), // #f559bf pink
            new Color(0.55f, 0.85f, 0.20f), // #8cd933 lime
            new Color(0.96f, 0.65f, 0.35f), // #f5a659 peach
        };

        [Header("UI")]
        [Tooltip("How often the leaderboard refreshes in seconds.")]
        public float leaderboardRefreshInterval = 1f;

        [Tooltip("How long a kill-feed entry stays visible in seconds.")]
        public float killFeedDuration = 4f;

        [Tooltip("Minimap display size in pixels.")]
        public int minimapSize = 150;
    }
}
