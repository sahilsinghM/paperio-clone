using System.Collections.Generic;
using UnityEngine;
using PaperIO.Core;
using PaperIO.Player;

namespace PaperIO.AI
{
    /// <summary>
    /// Finite-state-machine AI that simulates a human Paper.io player.
    /// Mirrors the behaviour in the original src/ai.js.
    ///
    /// States:
    ///   EXPAND       — move outward to claim new territory.
    ///   RETURN_HOME  — return to own territory to close the trail safely.
    ///   CHASE_TRAIL  — opportunistically cut an enemy trail.
    ///   AVOID_ENEMY  — steer away from a nearby threat.
    ///
    /// BotController inherits from PlayerBase so it shares the movement and
    /// trail/territory integration.
    /// </summary>
    public class BotController : PlayerBase
    {
        // ── State machine ──────────────────────────────────────────────────────
        public enum BotState { Expand, ReturnHome, ChaseTrail, AvoidEnemy }
        public BotState CurrentState { get; private set; } = BotState.Expand;

        // ── AI parameters ──────────────────────────────────────────────────────
        [Header("AI Tuning")]
        [Tooltip("Minimum cells to expand away from own border when choosing a target.")]
        public int expandMinDistance = 5;

        [Tooltip("Maximum cells to expand away from own border when choosing a target.")]
        public int expandMaxDistance = 30;

        [Tooltip("Bot will chase an enemy trail only when its own trail is shorter than this.")]
        public int chaseMaxOwnTrail = 50;

        [Tooltip("Radius (cells) in which the bot searches for enemy trails to cut.")]
        public int chaseSearchRadius = 20;

        // ── Decision timer ─────────────────────────────────────────────────────
        private float _decisionTimer;
        private float _nextDecisionInterval;

        // ── Navigation ─────────────────────────────────────────────────────────
        private Vector2 _targetPos;           // World-space XZ target.
        private bool    _hasTarget;
        private const float TargetReachedDist = 1.5f;

        // ── Angle tracking for smooth turns ────────────────────────────────────
        private float _desiredAngle;
        private const float AngleHysteresis = 0.05f; // radians; avoids jitter.

        // ─────────────────────────────────────────────────────────────────────
        protected override void Awake()
        {
            base.Awake();
        }

        // ─────────────────────────────────────────────────────────────────────
        #region PlayerBase overrides

        protected override void ComputeTurnInput(float dt)
        {
            // Re-evaluate state on timer.
            _decisionTimer -= dt;
            if (_decisionTimer <= 0f)
            {
                _nextDecisionInterval = _config.botDecisionInterval
                    + Random.value * _config.botDecisionVariance;
                _decisionTimer = _nextDecisionInterval;
                EvaluateState();
            }

            // Steer toward the desired angle.
            SteerTowardTarget(dt);
        }

        public override void Kill()
        {
            base.Kill();
            CurrentState = BotState.Expand;
            _hasTarget   = false;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region State transitions

        private void EvaluateState()
        {
            int trailLen = _trail.GetTrailLength(PlayerId);

            // Hard retreat when trail is near the limit.
            if (trailLen >= _config.trailLimit - 50)
            {
                TransitionTo(BotState.ReturnHome);
                return;
            }

            switch (CurrentState)
            {
                case BotState.Expand:
                    if (trailLen >= _config.trailWarning)
                    {
                        TransitionTo(BotState.ReturnHome);
                        return;
                    }
                    // Opportunistic: if a nearby enemy trail exists and we're safe.
                    if (trailLen < chaseMaxOwnTrail && TryFindEnemyTrailTarget(out Vector2 trailTarget))
                    {
                        _targetPos = trailTarget;
                        _hasTarget = true;
                        TransitionTo(BotState.ChaseTrail);
                        return;
                    }
                    // Choose a new expansion target if we've reached the old one.
                    if (!_hasTarget || Vector2.Distance(GridPosition2D, _targetPos) < TargetReachedDist)
                        ChooseExpansionTarget();
                    break;

                case BotState.ReturnHome:
                    // Returned home when on own territory with no trail.
                    if (IsOnOwnTerritory && _trail.GetTrailLength(PlayerId) == 0)
                        TransitionTo(BotState.Expand);
                    else
                        ChooseReturnTarget();
                    break;

                case BotState.ChaseTrail:
                    if (trailLen >= _config.trailWarning)
                    {
                        TransitionTo(BotState.ReturnHome);
                        return;
                    }
                    if (!HasValidTarget())
                        TransitionTo(BotState.Expand);
                    break;

                case BotState.AvoidEnemy:
                    TransitionTo(BotState.Expand);
                    break;
            }

            // Always avoid map edges.
            AvoidMapBoundary();
        }

        private void TransitionTo(BotState next)
        {
            CurrentState = next;
            _hasTarget   = false;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Target selection

        /// <summary>Pick a random unowned cell in the expand radius.</summary>
        private void ChooseExpansionTarget()
        {
            int cx = GridX, cz = GridZ;
            int attempts = 20;
            while (attempts-- > 0)
            {
                int dist = Random.Range(expandMinDistance, expandMaxDistance + 1);
                float ang = Random.value * Mathf.PI * 2f;
                int tx = Mathf.Clamp(cx + Mathf.RoundToInt(Mathf.Cos(ang) * dist), 1, _config.gridSize - 2);
                int tz = Mathf.Clamp(cz + Mathf.RoundToInt(Mathf.Sin(ang) * dist), 1, _config.gridSize - 2);

                if (_territory.GetOwner(tx, tz) != PlayerId)
                {
                    _targetPos = new Vector2(tx + 0.5f, tz + 0.5f);
                    _hasTarget = true;
                    return;
                }
            }
            // Fallback: pick a cell toward map centre.
            float centre = _config.gridSize * 0.5f;
            _targetPos = new Vector2(
                Mathf.Lerp(transform.position.x, centre, 0.3f),
                Mathf.Lerp(transform.position.z, centre, 0.3f)
            );
            _hasTarget = true;
        }

        /// <summary>Find the nearest owned cell to retreat to.</summary>
        private void ChooseReturnTarget()
        {
            Vector2Int nearest = _territory.FindNearestOwnedCell(PlayerId, new Vector2Int(GridX, GridZ));
            _targetPos = new Vector2(nearest.x + 0.5f, nearest.y + 0.5f);
            _hasTarget = true;
        }

        /// <summary>
        /// Look for an enemy trail point within chaseSearchRadius and aim for it.
        /// Returns false if no target is found.
        /// </summary>
        private bool TryFindEnemyTrailTarget(out Vector2 target)
        {
            target = default;
            Vector2 pos = GridPosition2D;

            float bestDist = chaseSearchRadius;
            bool  found    = false;

            foreach (var player in GameManager.Instance.AllPlayers)
            {
                if (player.PlayerId == PlayerId || !player.IsAlive) continue;

                IReadOnlyList<Vector2> pts = _trail.GetTrailPoints(player.PlayerId);
                foreach (var pt in pts)
                {
                    float d = Vector2.Distance(pos, pt);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        target   = pt;
                        found    = true;
                    }
                }
            }
            return found;
        }

        private bool HasValidTarget()
        {
            if (!_hasTarget) return false;
            return Vector2.Distance(GridPosition2D, _targetPos) >= TargetReachedDist;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Steering

        private void SteerTowardTarget(float dt)
        {
            if (!_hasTarget) return;

            Vector2 toTarget = _targetPos - GridPosition2D;
            _desiredAngle = Mathf.Atan2(toTarget.y, toTarget.x);

            // Compute signed angular delta.
            float delta = Mathf.DeltaAngle(_angle * Mathf.Rad2Deg, _desiredAngle * Mathf.Rad2Deg)
                          * Mathf.Deg2Rad;

            if (Mathf.Abs(delta) < AngleHysteresis)
                _turnInput = 0f;
            else
                _turnInput = Mathf.Sign(delta);
        }

        /// <summary>Override target to steer toward map centre when near edges.</summary>
        private void AvoidMapBoundary()
        {
            float margin = 15f;
            float gs = _config.gridSize;
            float x  = transform.position.x;
            float z  = transform.position.z;

            bool nearEdge = x < margin || x > gs - margin || z < margin || z > gs - margin;
            if (!nearEdge) return;

            float centre = gs * 0.5f;
            _targetPos = new Vector2(centre, centre);
            _hasTarget = true;
        }

        #endregion
    }
}
