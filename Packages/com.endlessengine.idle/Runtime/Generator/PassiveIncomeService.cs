using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Flow;

namespace EndlessEngine.Generator
{
    /// <summary>
    /// Subscribes to TickEngine.OnTick and converts generator yield into gold.
    /// Applies run-state modifiers from RunConfigSO:
    ///   - In menu (not in run): full passive income
    ///   - During a run: income * ActiveRunPassiveModifier (default 0.5 — active combat already earns)
    ///
    /// Passive income is zero when no generators are owned.
    /// This service owns only income production — it does NOT manage generator state.
    /// </summary>
    public class PassiveIncomeService : MonoBehaviour
    {
        // ── Dependencies ──────────────────────────────────────────────────────────

        private GeneratorSystem        _generators;
        private EconomyService         _economy;
        private GameFlowStateMachine   _gameFlow;

        // ── State ─────────────────────────────────────────────────────────────────

        /// <summary>Total gold earned from passive income since last reset.</summary>
        public long TotalPassiveEarned { get; private set; }

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependencies. Call from Bootstrap after all systems are initialized.
        /// </summary>
        public void Initialize(
            GeneratorSystem      generators,
            EconomyService       economy,
            GameFlowStateMachine gameFlow)
        {
            _generators = generators;
            _economy    = economy;
            _gameFlow   = gameFlow;
        }

        private void OnEnable()
        {
            TickEngine.OnTick += HandleTick;
        }

        private void OnDisable()
        {
            TickEngine.OnTick -= HandleTick;
        }

        // ── Tick Handler ──────────────────────────────────────────────────────────

        private void HandleTick(float effectiveDt)
        {
            if (_generators == null || _economy == null) return;

            float baseYield = _generators.CalculateTotalYield(); // gold/sec
            if (baseYield <= 0f) return;

            float modifier = GetRunModifier();
            long  income   = (long)(baseYield * modifier * effectiveDt);

            if (income <= 0) return;

            _economy.AddResources(income);
            TotalPassiveEarned += income;
        }

        private float GetRunModifier()
        {
            if (_gameFlow == null || !_gameFlow.IsInRun) return 1f;

            RunConfigSO cfg = null;
            try { cfg = ConfigRegistry.Run; } catch { }
            return cfg != null ? cfg.ActiveRunPassiveModifier : 0.5f;
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ResetTotalsForTesting() => TotalPassiveEarned = 0L;

        /// <summary>
        /// Manually subscribes to TickEngine.OnTick.
        /// Call after Initialize() in EditMode tests where OnEnable does not fire.
        /// </summary>
        public void SubscribeForTesting()
        {
            TickEngine.OnTick += HandleTick;
        }

        /// <summary>Unsubscribes from TickEngine.OnTick. Call in test TearDown.</summary>
        public void UnsubscribeForTesting()
        {
            TickEngine.OnTick -= HandleTick;
        }
#endif
    }
}
