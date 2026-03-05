using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PaperIO.Player;
using PaperIO.AI;
using PaperIO.Systems;
using PaperIO.Map;
using PaperIO.UI;

namespace PaperIO.Core
{
    /// <summary>
    /// Central singleton that owns the game state machine and coordinates all systems.
    /// Attach to a "GameManager" GameObject in the scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── Inspector references ───────────────────────────────────────────────
        [Header("Config")]
        public GameConfig config;

        [Header("Systems")]
        public TerritorySystem territorySystem;
        public TrailSystem trailSystem;
        public CollisionSystem collisionSystem;

        [Header("Scene Objects")]
        public MapManager mapManager;
        public BotSpawner botSpawner;
        public UIManager uiManager;
        public CameraController cameraController;
        public PlayerController playerController;

        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject botPrefab;

        // ── State machine ──────────────────────────────────────────────────────
        public enum GameState { Start, Playing, Dead, Respawning }
        public GameState State { get; private set; } = GameState.Start;

        // ── Player registry ────────────────────────────────────────────────────
        // All active players (human + bots) keyed by playerId.
        private readonly Dictionary<int, PlayerBase> _players = new();
        private int _nextPlayerId = 1;

        // ── Events ─────────────────────────────────────────────────────────────
        /// <summary>Fired when a player dies. Args: killerId, victimId.</summary>
        public event Action<int, int> OnPlayerKilled;

        /// <summary>Fired after territory is claimed. Arg: playerId that captured.</summary>
        public event Action<int> OnTerritoryCapture;

        // ── Kill feed ──────────────────────────────────────────────────────────
        public List<KillFeedEntry> KillFeed { get; } = new();

        // ── Physics accumulator ────────────────────────────────────────────────
        private float _tickAccumulator;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (config == null)
                Debug.LogError("[GameManager] GameConfig not assigned!");
        }

        private void Start()
        {
            territorySystem.Initialize(config.gridSize);
            mapManager.Initialize(config.gridSize);
            uiManager.Initialize();
            SetState(GameState.Start);
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            // Accumulate time and run fixed-rate physics ticks.
            _tickAccumulator += Time.deltaTime;
            while (_tickAccumulator >= config.physicsTickInterval)
            {
                _tickAccumulator -= config.physicsTickInterval;
                PhysicsTick(config.physicsTickInterval);
            }

            // Per-frame rendering updates.
            trailSystem.UpdateVisuals();
            uiManager.Tick(Time.deltaTime);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region State machine

        public void SetState(GameState newState)
        {
            State = newState;
            switch (newState)
            {
                case GameState.Start:
                    uiManager.ShowStartScreen();
                    break;

                case GameState.Playing:
                    SpawnHumanPlayer();
                    botSpawner.SpawnInitialBots();
                    break;

                case GameState.Dead:
                    uiManager.ShowDeathScreen(playerController);
                    StartCoroutine(WaitForRespawnInput());
                    break;

                case GameState.Respawning:
                    StartCoroutine(RespawnHuman());
                    break;
            }
        }

        private IEnumerator WaitForRespawnInput()
        {
            yield return new WaitForSeconds(config.humanRespawnDelay);
            // UIManager will call GameManager.Instance.RequestRespawn() when the
            // player presses the respawn button.
        }

        /// <summary>Called by UIManager when the player presses Play Again.</summary>
        public void RequestRespawn()
        {
            if (State == GameState.Dead)
                SetState(GameState.Respawning);
        }

        private IEnumerator RespawnHuman()
        {
            yield return new WaitForSeconds(0.1f);
            SpawnHumanPlayer();
            SetState(GameState.Playing);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Player management

        public int AllocatePlayerId() => _nextPlayerId++;

        public void RegisterPlayer(PlayerBase player)
        {
            _players[player.PlayerId] = player;
        }

        public void UnregisterPlayer(int playerId)
        {
            _players.Remove(playerId);
        }

        public bool TryGetPlayer(int playerId, out PlayerBase player)
            => _players.TryGetValue(playerId, out player);

        public IEnumerable<PlayerBase> AllPlayers => _players.Values;

        private void SpawnHumanPlayer()
        {
            // Reuse the existing PlayerController GameObject placed in the scene.
            Vector2Int spawnCell = territorySystem.RandomEmptyCell();
            playerController.InitPlayer(
                AllocatePlayerId(),
                config.playerColors[0],
                "You",
                spawnCell
            );
            RegisterPlayer(playerController);
            cameraController.SetTarget(playerController.transform);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Physics tick

        private void PhysicsTick(float dt)
        {
            foreach (var player in _players.Values)
            {
                if (!player.IsAlive) continue;
                player.PhysicsTick(dt);
            }

            // Collision detection runs after all movement has been applied.
            collisionSystem.CheckAll(_players.Values);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Kill & territory events

        /// <summary>
        /// Called by CollisionSystem when a player is killed.
        /// killerId == 0 means boundary death (no killer).
        /// </summary>
        public void NotifyPlayerKilled(int victimId, int killerId)
        {
            if (!_players.TryGetValue(victimId, out var victim)) return;

            string killerName = "boundary";
            if (killerId != 0 && _players.TryGetValue(killerId, out var killer))
            {
                killer.AddKill();
                killerName = killer.PlayerName;

                // Transfer victim territory to killer.
                territorySystem.TransferTerritory(victimId, killerId);
            }
            else
            {
                // Clear victim territory back to neutral.
                territorySystem.ClearTerritory(victimId);
            }

            // Remove victim trail.
            trailSystem.ClearTrail(victimId);

            // Record kill feed.
            KillFeed.Add(new KillFeedEntry(killerName, victim.PlayerName, Time.time));
            uiManager.RefreshKillFeed(KillFeed);

            victim.Kill();

            OnPlayerKilled?.Invoke(killerId, victimId);

            // Human player died.
            if (victimId == playerController.PlayerId)
                SetState(GameState.Dead);
            else
                botSpawner.ScheduleBotRespawn(victimId);
        }

        /// <summary>Called by TerritorySystem after a successful area capture.</summary>
        public void NotifyTerritoryCapture(int playerId)
        {
            OnTerritoryCapture?.Invoke(playerId);
        }

        #endregion
    }

    // ── Data classes ───────────────────────────────────────────────────────────
    [Serializable]
    public class KillFeedEntry
    {
        public string killerName;
        public string victimName;
        public float timestamp;

        public KillFeedEntry(string killer, string victim, float time)
        {
            killerName = killer;
            victimName = victim;
            timestamp  = time;
        }
    }
}
