using System;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.Economy;
using EndlessEngine.Input;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Statistics;
using EndlessEngine.VFX;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Core active-loop service for click/tap gameplay.
    ///
    /// Pipeline per click:
    ///   1. Detect click via IInputProvider.GetPointerClickedThisFrame()
    ///   2. Raycast to find the ClickTarget under the pointer (Physics2D.OverlapPoint)
    ///   3. Compute damage (base × ClickDamage stat)
    ///   4. ClickYieldResolver computes gold (per-click or on-destruction)
    ///   5. ClickComboTracker accumulates combo points; crit roll applied
    ///   6. EconomyService receives gold; VFX floating number spawned
    ///   7. Events fired: OnTargetClicked, OnTargetDestroyed, OnYieldAwarded, OnComboChanged, OnCrit
    ///
    /// Auto-click: fires synthetic clicks on a timer (upgrade-driven via ClickAutoRate stat).
    ///
    /// Implements ISaveStateProvider: persists target respawn timers + lifetime stats.
    ///
    /// Attach to a persistent manager GameObject.
    /// Call Initialize() from bootstrapper, then saveService.RegisterStateProvider(this).
    /// </summary>
    public class ClickLoopService : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.ClickLoop;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>(target, damageDealt, yieldAwarded, wasCrit)</summary>
        public event Action<IClickTarget, float, float, bool> OnTargetClicked;

        /// <summary>Fired when a target's HP reaches 0.</summary>
        public event Action<IClickTarget> OnTargetDestroyed;

        /// <summary>Total gold awarded this click (after crit + combo).</summary>
        public event Action<float> OnYieldAwarded;

        /// <summary>Combo multiplier changed.</summary>
        public event Action<float> OnComboChanged;

        /// <summary>A critical click occurred. Payload = crit multiplier.</summary>
        public event Action<float> OnCrit;

        // ── Statistics stat IDs ───────────────────────────────────────────────────

        public const string StatIdTotalGold      = "clickloop.total_gold";
        public const string StatIdTotalDestroyed = "clickloop.total_targets_destroyed";
        public const string StatIdBestCombo      = "clickloop.best_combo_multiplier";

        // ── Dependencies ──────────────────────────────────────────────────────────

        private ClickLoopConfigSO _config;
        private EconomyService    _economy;
        private IInputProvider    _input;
        private StatisticsService _statistics;
        private VFXController     _vfx;
        private LayerMask         _targetLayer;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private ClickComboTracker _combo;
        private float             _autoClickAccum;
        private float             _lastComboMul = 1f;

        private long  _totalGoldEarned;
        private int   _totalTargetsDestroyed;
        private float _bestComboMultiplier = 1f;

        public float ComboMultiplier => _combo?.ComboMultiplier ?? 1f;
        public float ComboPoints     => _combo?.ComboPoints     ?? 0f;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(
            ClickLoopConfigSO config,
            EconomyService    economy,
            IInputProvider    input,
            LayerMask         targetLayer,
            StatisticsService statistics = null,
            VFXController     vfx        = null)
        {
            _config      = config;
            _economy     = economy;
            _input       = input;
            _targetLayer = targetLayer;
            _statistics  = statistics;
            _vfx         = vfx;
            _combo       = new ClickComboTracker(config);
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_config == null || _input == null) return;

            float dt = Time.deltaTime;

            _combo.Tick(dt);

            float newMul = _combo.ComboMultiplier;
            if (!Mathf.Approximately(newMul, _lastComboMul))
            {
                _lastComboMul = newMul;
                OnComboChanged?.Invoke(newMul);
                if (newMul > _bestComboMultiplier)
                {
                    _bestComboMultiplier = newMul;
                    _statistics?.SetIfHigher(StatIdBestCombo, newMul);
                }
            }

            // Manual click
            if (_input.GetPointerClickedThisFrame())
                TryClickAtPointer(_input.GetMouseWorldPosition());

            // Auto-click
            float autoRate = _config.BaseAutoClickRate
                + UpgradeApplicationSystem.GetEffectiveStat(StatType.ClickAutoRate);
            if (autoRate > 0f)
            {
                _autoClickAccum += autoRate * dt;
                while (_autoClickAccum >= 1f)
                {
                    _autoClickAccum -= 1f;
                    AutoClick();
                }
            }
        }

        // ── Click processing ──────────────────────────────────────────────────────

        private void TryClickAtPointer(Vector2 worldPos)
        {
            // Find target under pointer
            Collider2D hit = Physics2D.OverlapPoint(worldPos, _targetLayer);
            if (hit == null) return;

            var target = hit.GetComponent<ClickTarget>();
            if (target == null || !target.IsAlive) return;

            ProcessClick(target);
        }

        private void AutoClick()
        {
            // Auto-click hits the first alive registered target
            var all = ClickTargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsAlive)
                {
                    ProcessClick(all[i]);
                    return;
                }
            }
        }

        private void ProcessClick(IClickTarget target)
        {
            float damage  = ComputeDamage(target.Config);
            bool  wasCrit = RollCrit();
            float critMul = wasCrit
                ? _config.BaseCritMultiplier
                  * (1f + UpgradeApplicationSystem.GetEffectiveStat(StatType.ClickCritMultiplier))
                : 1f;

            float applied = target.ApplyDamage(damage);

            _combo.RecordClick(target.Config.ComboContribution);

            float comboMul = _combo.ComboMultiplier;

            float yield;
            if (target.Config.AwardYieldPerClick)
                yield = ClickYieldResolver.ResolveClickYield(target.Config, applied, comboMul, critMul);
            else
                yield = (!target.IsAlive)
                    ? ClickYieldResolver.ResolveDestructionYield(target.Config, comboMul, critMul)
                    : 0f;

            if (wasCrit) OnCrit?.Invoke(critMul);
            OnTargetClicked?.Invoke(target, applied, yield, wasCrit);

            if (!target.IsAlive)
            {
                _totalTargetsDestroyed++;
                _statistics?.Add(StatIdTotalDestroyed, 1);
                OnTargetDestroyed?.Invoke(target);
            }

            if (yield > 0f)
            {
                AwardYield(yield, wasCrit, target.WorldPosition);
                OnYieldAwarded?.Invoke(yield);
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private float ComputeDamage(ClickTargetConfigSO config)
        {
            float bonus = UpgradeApplicationSystem.GetEffectiveStat(StatType.ClickDamage);
            return config.DamagePerClick * (1f + bonus);
        }

        private bool RollCrit()
        {
            float chance = _config.BaseCritChance
                + UpgradeApplicationSystem.GetEffectiveStat(StatType.ClickCritChance);
            return UnityEngine.Random.value < Mathf.Clamp01(chance);
        }

        private void AwardYield(float amount, bool wasCrit, Vector2 worldPos)
        {
            if (_economy == null) return;
            long gold = (long)Mathf.Max(1f, amount);
            _economy.AddResources(gold);
            _totalGoldEarned += gold;
            _statistics?.Add(StatIdTotalGold, gold);

            if (_vfx != null)
                _vfx.SpawnClickNumber(gold, wasCrit, worldPos);
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            var s = saveData.ClickLoopState;
            s.TargetStates.Clear();
            s.TotalGoldEarned       = _totalGoldEarned;
            s.TotalTargetsDestroyed = _totalTargetsDestroyed;
            s.BestComboMultiplier   = _bestComboMultiplier;

            var all = ClickTargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (!t.IsRespawning) continue;
                s.TargetStates[MakeKey(t, i)] = new ClickTargetSaveEntry
                {
                    IsRespawning            = true,
                    RespawnSecondsRemaining = t.RespawnSecondsRemaining,
                };
            }
        }

        public void OnAfterLoad(SaveData saveData)
        {
            var s = saveData.ClickLoopState;
            _totalGoldEarned       = s.TotalGoldEarned;
            _totalTargetsDestroyed = s.TotalTargetsDestroyed;
            _bestComboMultiplier   = Mathf.Max(1f, s.BestComboMultiplier);

            var all = ClickTargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                s.TargetStates.TryGetValue(MakeKey(t, i), out ClickTargetSaveEntry entry);
                t.RestoreFromSave(entry);
            }

            _statistics?.Add(StatIdTotalGold,      _totalGoldEarned);
            _statistics?.Add(StatIdTotalDestroyed,  _totalTargetsDestroyed);
            _statistics?.SetIfHigher(StatIdBestCombo, _bestComboMultiplier);
        }

        // ── Public controls ───────────────────────────────────────────────────────

        public void ResetCombo() => _combo?.Reset();

        // ── Private utility ───────────────────────────────────────────────────────

        private static string MakeKey(ClickTarget t, int idx) => $"{t.TargetId}_{idx}";

        // ── Test support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SimulateClickOnTarget(IClickTarget target) => ProcessClick(target);
        public long  TotalGoldForTesting       => _totalGoldEarned;
        public int   TotalDestroyedForTesting  => _totalTargetsDestroyed;
        public float BestComboForTesting       => _bestComboMultiplier;
#endif
    }
}
