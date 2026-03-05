using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PaperIO.Core;
using PaperIO.Player;

namespace PaperIO.UI
{
    /// <summary>
    /// Manages all HUD elements: start screen, death screen, leaderboard,
    /// territory score, trail danger gauge, kill feed, and minimap.
    ///
    /// Expects a UI Canvas in the scene with the child GameObjects wired in
    /// the Inspector.  Mirrors all HUD elements from src/ui.js.
    ///
    /// Attach to a "UIManager" GameObject in the scene.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ── Screens ────────────────────────────────────────────────────────────
        [Header("Screens")]
        public GameObject startScreen;
        public GameObject gameHUD;
        public GameObject deathScreen;

        // ── Start screen ───────────────────────────────────────────────────────
        [Header("Start Screen")]
        public Button    startButton;
        public TMP_InputField playerNameInput;

        // ── Death screen ───────────────────────────────────────────────────────
        [Header("Death Screen")]
        public TMP_Text  deathKillsText;
        public TMP_Text  deathTerritoryText;
        public Button    respawnButton;

        // ── Leaderboard ────────────────────────────────────────────────────────
        [Header("Leaderboard")]
        [Tooltip("Container that holds leaderboard row children.")]
        public Transform leaderboardContainer;

        [Tooltip("Prefab for a single leaderboard row.")]
        public GameObject leaderboardRowPrefab;

        private readonly List<GameObject> _leaderboardRows = new();

        // ── Score ──────────────────────────────────────────────────────────────
        [Header("Score HUD")]
        public TMP_Text  territoryText;
        public TMP_Text  killsText;
        public TMP_Text  playerNameLabel;

        // ── Danger gauge ───────────────────────────────────────────────────────
        [Header("Danger Gauge")]
        [Tooltip("Image whose fillAmount shows trail danger (0=safe, 1=max).")]
        public Image     dangerFill;

        [Tooltip("Text shown when trail is near the limit.")]
        public TMP_Text  dangerWarningText;

        private static readonly Color GaugeGreen  = new(0.2f, 0.85f, 0.3f);
        private static readonly Color GaugeYellow = new(0.95f, 0.85f, 0.1f);
        private static readonly Color GaugeRed    = new(0.95f, 0.2f, 0.1f);

        // ── Kill feed ──────────────────────────────────────────────────────────
        [Header("Kill Feed")]
        [Tooltip("Container that holds kill feed entry children.")]
        public Transform killFeedContainer;

        [Tooltip("Prefab for a single kill feed entry.")]
        public GameObject killFeedPrefab;

        // Pairs of (gameObject, spawnTime) so entries can be expired cleanly.
        private readonly List<(GameObject go, float spawnTime)> _killFeedEntries = new();

        // ── Minimap ────────────────────────────────────────────────────────────
        [Header("Minimap")]
        [Tooltip("RawImage that displays the minimap texture.")]
        public RawImage  minimapDisplay;

        private Texture2D _minimapTexture;
        private Color32[] _minimapPixels;

        // ── Internal timers ────────────────────────────────────────────────────
        private float _leaderboardTimer;
        private GameConfig _config;

        // ─────────────────────────────────────────────────────────────────────
        #region Initialization

        public void Initialize()
        {
            _config = GameManager.Instance.config;

            // Wire start button.
            if (startButton != null)
                startButton.onClick.AddListener(OnStartButtonPressed);

            // Wire respawn button.
            if (respawnButton != null)
                respawnButton.onClick.AddListener(() => GameManager.Instance.RequestRespawn());

            // Build minimap texture.
            int ms = _config.minimapSize;
            _minimapTexture = new Texture2D(ms, ms, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp
            };
            _minimapPixels = new Color32[ms * ms];
            if (minimapDisplay != null)
                minimapDisplay.texture = _minimapTexture;

            ShowStartScreen();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Screen management

        public void ShowStartScreen()
        {
            SetScreenActive(startScreen,  true);
            SetScreenActive(gameHUD,      false);
            SetScreenActive(deathScreen,  false);
        }

        public void ShowGameHUD()
        {
            SetScreenActive(startScreen,  false);
            SetScreenActive(gameHUD,      true);
            SetScreenActive(deathScreen,  false);
        }

        public void ShowDeathScreen(PlayerController player)
        {
            SetScreenActive(startScreen,  false);
            SetScreenActive(gameHUD,      false);
            SetScreenActive(deathScreen,  true);

            if (deathKillsText != null)
                deathKillsText.text = $"Kills: {player?.Kills ?? 0}";

            if (deathTerritoryText != null && player != null)
            {
                float pct = GameManager.Instance.territorySystem.GetTerritoryPercent(player.PlayerId);
                deathTerritoryText.text = $"Territory: {pct:F1}%";
            }
        }

        private static void SetScreenActive(GameObject screen, bool active)
        {
            if (screen != null) screen.SetActive(active);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Per-frame tick

        /// <summary>Called every Update by GameManager while the game is running.</summary>
        public void Tick(float dt)
        {
            UpdateScore();
            UpdateDangerGauge();
            UpdateMinimap();
            CleanKillFeed();

            _leaderboardTimer -= dt;
            if (_leaderboardTimer <= 0f)
            {
                _leaderboardTimer = _config.leaderboardRefreshInterval;
                RefreshLeaderboard();
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Score

        private void UpdateScore()
        {
            var player = GameManager.Instance.playerController;
            if (player == null || !player.IsAlive) return;

            if (territoryText != null)
            {
                float pct = GameManager.Instance.territorySystem.GetTerritoryPercent(player.PlayerId);
                territoryText.text = $"{pct:F1}%";
            }

            if (killsText != null)
                killsText.text = $"Kills: {player.Kills}";

            if (playerNameLabel != null)
                playerNameLabel.text = player.PlayerName;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Danger gauge

        private void UpdateDangerGauge()
        {
            var player = GameManager.Instance.playerController;
            if (player == null || dangerFill == null) return;

            int trailLen = GameManager.Instance.trailSystem.GetTrailLength(player.PlayerId);
            float t = (float)trailLen / _config.trailLimit;

            dangerFill.fillAmount = t;
            dangerFill.color = Color.Lerp(
                Color.Lerp(GaugeGreen, GaugeYellow, Mathf.InverseLerp(0f, 0.5f, t)),
                GaugeRed,
                Mathf.InverseLerp(0.5f, 1f, t)
            );

            bool danger = trailLen >= _config.trailWarning;
            if (dangerWarningText != null)
            {
                dangerWarningText.gameObject.SetActive(danger);
                if (danger)
                {
                    float alpha = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                    var c = dangerWarningText.color;
                    c.a = Mathf.Lerp(0.4f, 1f, alpha);
                    dangerWarningText.color = c;
                }
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Leaderboard

        private void RefreshLeaderboard()
        {
            if (leaderboardContainer == null) return;

            // Build sorted entry list.
            var entries = new List<LeaderboardEntry>();
            foreach (var player in GameManager.Instance.AllPlayers)
            {
                if (!player.IsAlive) continue;
                float pct = GameManager.Instance.territorySystem.GetTerritoryPercent(player.PlayerId);
                entries.Add(new LeaderboardEntry(player.PlayerId, player.PlayerName, player.PlayerColor, pct, player.Kills));
            }
            entries.Sort();

            int displayCount = Mathf.Min(entries.Count, 5);

            // Ensure enough row GameObjects exist.
            while (_leaderboardRows.Count < displayCount)
            {
                var row = leaderboardRowPrefab != null
                    ? Instantiate(leaderboardRowPrefab, leaderboardContainer)
                    : CreateDefaultLeaderboardRow(leaderboardContainer);
                _leaderboardRows.Add(row);
            }

            // Update rows.
            for (int i = 0; i < _leaderboardRows.Count; i++)
            {
                bool active = i < displayCount;
                _leaderboardRows[i].SetActive(active);
                if (!active) continue;

                var entry = entries[i];
                var texts = _leaderboardRows[i].GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 2)
                {
                    texts[0].text  = $"{i + 1}. {entry.playerName}";
                    texts[0].color = entry.playerColor;
                    texts[1].text  = $"{entry.territoryPercent:F1}%";
                }
            }
        }

        private static GameObject CreateDefaultLeaderboardRow(Transform parent)
        {
            var go = new GameObject("LeaderboardRow");
            go.transform.SetParent(parent);
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;

            var nameGo  = new GameObject("Name");  nameGo.transform.SetParent(go.transform);
            var pctGo   = new GameObject("Pct");   pctGo.transform.SetParent(go.transform);
            nameGo.AddComponent<TMP_Text>();
            pctGo.AddComponent<TMP_Text>();
            return go;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Kill feed

        public void RefreshKillFeed(List<KillFeedEntry> entries)
        {
            if (killFeedContainer == null || entries.Count == 0) return;
            AddKillFeedEntry(entries[entries.Count - 1]);
        }

        private void AddKillFeedEntry(KillFeedEntry entry)
        {
            if (killFeedContainer == null) return;

            GameObject go = killFeedPrefab != null
                ? Instantiate(killFeedPrefab, killFeedContainer)
                : CreateDefaultKillFeedRow(killFeedContainer);

            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.text  = $"{entry.killerName} eliminated {entry.victimName}";
                txt.color = Color.white;
            }

            _killFeedEntries.Add((go, Time.time));

            // Cap at 5 visible entries; remove oldest.
            while (_killFeedEntries.Count > 5)
            {
                Destroy(_killFeedEntries[0].go);
                _killFeedEntries.RemoveAt(0);
            }
        }

        private static GameObject CreateDefaultKillFeedRow(Transform parent)
        {
            var go = new GameObject("KillFeedEntry");
            go.transform.SetParent(parent);
            go.AddComponent<TMP_Text>();
            return go;
        }

        private void CleanKillFeed()
        {
            float now = Time.time;
            for (int i = _killFeedEntries.Count - 1; i >= 0; i--)
            {
                var (go, spawnTime) = _killFeedEntries[i];
                if (go == null || now - spawnTime > _config.killFeedDuration)
                {
                    if (go != null) Destroy(go);
                    _killFeedEntries.RemoveAt(i);
                }
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Minimap

        private void UpdateMinimap()
        {
            if (minimapDisplay == null || _minimapTexture == null) return;

            int ms = _config.minimapSize;
            int gs = GameManager.Instance.config.gridSize;
            float scale = (float)ms / gs;

            // Clear to dark background.
            System.Array.Clear(_minimapPixels, 0, _minimapPixels.Length);

            // Draw territory from TerritorySystem (sample the grid at minimap resolution).
            var ts = GameManager.Instance.territorySystem;
            for (int mz = 0; mz < ms; mz++)
            for (int mx = 0; mx < ms; mx++)
            {
                int gx = Mathf.Clamp(Mathf.RoundToInt(mx / scale), 0, gs - 1);
                int gz = Mathf.Clamp(Mathf.RoundToInt(mz / scale), 0, gs - 1);
                int owner = ts.GetOwner(gx, gz);
                if (owner != 0 && GameManager.Instance.TryGetPlayer(owner, out var p))
                {
                    Color c = p.PlayerColor * 0.55f;
                    _minimapPixels[mz * ms + mx] = new Color32(
                        (byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 220);
                }
            }

            // Draw player dots.
            foreach (var player in GameManager.Instance.AllPlayers)
            {
                if (!player.IsAlive) continue;
                int mx = Mathf.Clamp(Mathf.RoundToInt(player.transform.position.x * scale), 0, ms - 1);
                int mz = Mathf.Clamp(Mathf.RoundToInt(player.transform.position.z * scale), 0, ms - 1);

                int dotRadius = player == GameManager.Instance.playerController ? 3 : 2;
                DrawMinimapDot(mx, mz, dotRadius, player.PlayerColor, ms);
            }

            _minimapTexture.SetPixels32(_minimapPixels);
            _minimapTexture.Apply(false);
        }

        private void DrawMinimapDot(int cx, int cy, int r, Color color, int ms)
        {
            Color32 c32 = new Color32((byte)(color.r*255),(byte)(color.g*255),(byte)(color.b*255),255);
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || x >= ms || y < 0 || y >= ms) continue;
                _minimapPixels[y * ms + x] = c32;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Button handlers

        private void OnStartButtonPressed()
        {
            string name = playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text)
                ? playerNameInput.text.Trim()
                : "You";

            GameManager.Instance.playerController.defaultPlayerName = name;
            GameManager.Instance.SetState(GameManager.GameState.Playing);
            ShowGameHUD();
        }

        #endregion
    }
}
