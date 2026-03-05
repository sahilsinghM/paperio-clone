using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PaperIO.Core;

namespace PaperIO.AI
{
    /// <summary>
    /// Manages the bot population: spawning, respawning after death, and
    /// maintaining bot count at GameConfig.maxBots.
    ///
    /// Attach to a "BotSpawner" GameObject in the scene.
    /// </summary>
    public class BotSpawner : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Prefab")]
        [Tooltip("Prefab with BotController and visual components.")]
        public GameObject botPrefab;

        // ── Bot names pool ─────────────────────────────────────────────────────
        private static readonly string[] BotNames =
        {
            "Alpha", "Blaze", "Cipher", "Dusk", "Echo",
            "Frost", "Ghost", "Hawk", "Iris", "Jinx",
            "Kite", "Luna", "Mach", "Nova", "Orbit",
            "Pixel", "Quest", "Raze", "Storm", "Titan"
        };

        // ── Runtime ────────────────────────────────────────────────────────────
        private GameConfig _config;
        private readonly Dictionary<int, BotController> _bots = new();
        private readonly Queue<int> _pendingRespawn = new();

        // Color index cycling (skip index 0 which is reserved for the human).
        private int _colorIndex = 1;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _config = GameManager.Instance.config;
        }

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>Spawn all initial bots when a game starts.</summary>
        public void SpawnInitialBots()
        {
            for (int i = 0; i < _config.maxBots; i++)
                SpawnBot();
        }

        /// <summary>Called by GameManager when a bot dies. Schedules a respawn.</summary>
        public void OnBotDied(int botPlayerId)
        {
            _pendingRespawn.Enqueue(botPlayerId);
            StartCoroutine(RespawnAfterDelay(botPlayerId));
        }

        /// <summary>
        /// Called by BotSpawner internally (and can be called externally) to
        /// spawn a replacement bot if the count is below max.
        /// </summary>
        public void ScheduleBotRespawn(int victimId) => OnBotDied(victimId);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Spawn logic

        private BotController SpawnBot()
        {
            if (botPrefab == null)
            {
                Debug.LogError("[BotSpawner] botPrefab is not assigned!");
                return null;
            }

            Vector2Int spawnCell = GameManager.Instance.territorySystem.RandomEmptyCell();
            GameObject go = Instantiate(botPrefab, new Vector3(spawnCell.x + 0.5f, 0f, spawnCell.y + 0.5f), Quaternion.identity, transform);

            BotController bot = go.GetComponent<BotController>();
            if (bot == null)
            {
                Debug.LogError("[BotSpawner] botPrefab does not have a BotController component.");
                Destroy(go);
                return null;
            }

            int    id    = GameManager.Instance.AllocatePlayerId();
            Color  color = _config.playerColors[_colorIndex % _config.playerColors.Length];
            string name  = BotNames[Random.Range(0, BotNames.Length)];

            _colorIndex++;

            bot.InitPlayer(id, color, name, spawnCell);
            _bots[id] = bot;
            go.name = $"Bot_{name}_{id}";

            return bot;
        }

        private IEnumerator RespawnAfterDelay(int oldBotId)
        {
            yield return new WaitForSeconds(_config.botRespawnDelay);

            // Remove old entry and clean up all per-player subsystem state.
            if (_bots.TryGetValue(oldBotId, out var oldBot))
            {
                _bots.Remove(oldBotId);
                GameManager.Instance.UnregisterPlayer(oldBotId);
                // Destroy the trail LineRenderers owned by TrailSystem.
                GameManager.Instance.trailSystem.UnregisterPlayer(oldBotId);
                if (oldBot != null)
                    Destroy(oldBot.gameObject);
            }

            // Only respawn if the game is still running.
            if (GameManager.Instance.State == GameManager.GameState.Playing)
                SpawnBot();
        }

        #endregion
    }
}
