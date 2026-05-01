using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Flow
{
    /// <summary>
    /// Owns the top-level game flow: Menu → InRun → PostRun → Menu.
    /// Does not own UI — raises events that UI layers subscribe to.
    ///
    /// Single source of truth for current state. All systems check
    /// GameFlowStateMachine.CurrentState before acting.
    /// </summary>
    public class GameFlowStateMachine : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires whenever state transitions. Parameters: (from, to).</summary>
        public static event Action<GameFlowState, GameFlowState> OnStateChanged;

        /// <summary>Fires when transitioning to Menu state.</summary>
        public static event Action OnEnteredMenu;

        /// <summary>Fires when transitioning to InRun state.</summary>
        public static event Action OnEnteredRun;

        /// <summary>Fires when transitioning to PostRun state.</summary>
        public static event Action OnEnteredPostRun;

        // ── State ─────────────────────────────────────────────────────────────────

        private GameFlowState _state = GameFlowState.Menu;

        /// <summary>Current game flow state.</summary>
        public GameFlowState CurrentState => _state;

        public bool IsInMenu   => _state == GameFlowState.Menu;
        public bool IsInRun    => _state == GameFlowState.InRun;
        public bool IsPostRun  => _state == GameFlowState.PostRun;

        // ── Transitions ───────────────────────────────────────────────────────────

        /// <summary>Transition from Menu → InRun. Call when player presses "Start Run".</summary>
        public void StartRun()
        {
            if (_state != GameFlowState.Menu)
            {
                Debug.LogWarning($"[GameFlow] StartRun called from state {_state} — ignored.");
                return;
            }
            Transition(GameFlowState.InRun);
        }

        /// <summary>Transition from InRun → PostRun. Called by RunSessionManager when timer ends.</summary>
        public void EndRun()
        {
            if (_state != GameFlowState.InRun)
            {
                Debug.LogWarning($"[GameFlow] EndRun called from state {_state} — ignored.");
                return;
            }
            Transition(GameFlowState.PostRun);
        }

        /// <summary>Transition from PostRun → Menu. Called when player dismisses post-run screen.</summary>
        public void ReturnToMenu()
        {
            if (_state != GameFlowState.PostRun)
            {
                Debug.LogWarning($"[GameFlow] ReturnToMenu called from state {_state} — ignored.");
                return;
            }
            Transition(GameFlowState.Menu);
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void Transition(GameFlowState next)
        {
            GameFlowState prev = _state;
            _state = next;

            Debug.Log($"[GameFlow] {prev} → {next}");

            OnStateChanged?.Invoke(prev, next);

            switch (next)
            {
                case GameFlowState.Menu:    OnEnteredMenu?.Invoke();    break;
                case GameFlowState.InRun:   OnEnteredRun?.Invoke();     break;
                case GameFlowState.PostRun: OnEnteredPostRun?.Invoke(); break;
            }
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Force-sets state for testing without firing transition events.</summary>
        public void SetStateForTesting(GameFlowState state) => _state = state;

        /// <summary>Clears all static subscribers. Call in test TearDown.</summary>
        public static void ClearSubscribersForTesting()
        {
            OnStateChanged  = null;
            OnEnteredMenu   = null;
            OnEnteredRun    = null;
            OnEnteredPostRun = null;
        }
#endif
    }
}
