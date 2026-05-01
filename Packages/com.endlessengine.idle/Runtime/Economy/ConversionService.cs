using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Executes resource conversion recipes (e.g. 100 gold → 1 gem).
    ///
    /// Dependencies:
    ///   - EconomyService  — primary currency (gold) source/sink
    ///   - CurrencyService — secondary currency source/sink
    ///   - ConversionDatabaseSO — recipe definitions
    ///
    /// Cooldown state is ephemeral (not persisted) — resets on session start.
    /// If you need persistent cooldowns, implement ISaveStateProvider and add
    /// ConversionCooldowns to SaveData (Sprint roadmap: ConversionService v2).
    ///
    /// Bootstrap wiring:
    ///   var cs = gameObject.AddComponent&lt;ConversionService&gt;();
    ///   cs.Initialize(conversionDatabase, economyService, currencyService);
    /// </summary>
    public class ConversionService : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires after a successful conversion.
        /// Parameters: (recipeId, executionCount, totalInputConsumed, totalOutputProduced).
        /// </summary>
        public static event Action<string, int, double, double> OnConverted;

        /// <summary>Fires when TryConvert fails. Parameters: (recipeId, reason).</summary>
        public static event Action<string, ConversionFailReason> OnConversionFailed;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private ConversionDatabaseSO _database;
        private EconomyService       _economy;
        private CurrencyService      _currencyService;
        private bool _initialized;

        // ── Cooldown tracking ─────────────────────────────────────────────────────

        /// <summary>Key = RecipeId, Value = next allowed conversion time (Time.unscaledTime).</summary>
        private readonly Dictionary<string, float> _cooldownEnds = new();

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependencies. currencyService may be null if the game has no secondary currencies.
        /// </summary>
        public void Initialize(
            ConversionDatabaseSO database,
            EconomyService       economy,
            CurrencyService      currencyService = null)
        {
            _database        = database;
            _economy         = economy;
            _currencyService = currencyService;
            _initialized     = true;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to execute the recipe once (or <paramref name="count"/> times if AllowBulk).
        /// Returns true and fires <see cref="OnConverted"/> on success.
        /// Returns false and fires <see cref="OnConversionFailed"/> on any failure.
        /// </summary>
        public bool TryConvert(string recipeId, int count = 1)
        {
            if (!_initialized) return Fail(recipeId, ConversionFailReason.NotInitialized);

            var recipe = _database?.GetById(recipeId);
            if (recipe == null) return Fail(recipeId, ConversionFailReason.UnknownRecipe);

            // Bulk guard
            if (count > 1 && !recipe.AllowBulk) count = 1;
            if (count <= 0) return Fail(recipeId, ConversionFailReason.InvalidCount);

            // Cooldown check
            if (recipe.CooldownSeconds > 0 &&
                _cooldownEnds.TryGetValue(recipeId, out float endTime) &&
                Time.unscaledTime < endTime)
                return Fail(recipeId, ConversionFailReason.OnCooldown);

            // Balance check — resolve max runnable count
            double inputBalance  = GetBalance(recipe.InputCurrencyId);
            double outputBalance = GetBalance(recipe.OutputCurrencyId);
            double outputCap     = GetCap(recipe.OutputCurrencyId);

            int maxRuns = recipe.AllowBulk
                ? recipe.MaxExecutions(inputBalance, outputCap, outputBalance)
                : 1;

            int runs = System.Math.Min(count, maxRuns);
            if (runs <= 0) return Fail(recipeId, ConversionFailReason.InsufficientBalance);

            double totalInput  = recipe.InputAmount  * runs;
            double totalOutput = recipe.OutputAmount * runs;

            // Execute: consume input
            if (!ConsumeBalance(recipe.InputCurrencyId, totalInput))
                return Fail(recipeId, ConversionFailReason.InsufficientBalance);

            // Execute: produce output
            ProduceBalance(recipe.OutputCurrencyId, totalOutput);

            // Set cooldown
            if (recipe.CooldownSeconds > 0)
                _cooldownEnds[recipeId] = Time.unscaledTime + recipe.CooldownSeconds;

            OnConverted?.Invoke(recipeId, runs, totalInput, totalOutput);
            return true;
        }

        /// <summary>Returns seconds remaining on cooldown. 0 if not on cooldown.</summary>
        public float GetCooldownRemaining(string recipeId)
        {
            if (_cooldownEnds.TryGetValue(recipeId, out float end))
                return Mathf.Max(0, end - Time.unscaledTime);
            return 0;
        }

        /// <summary>Returns true if the recipe is currently on cooldown.</summary>
        public bool IsOnCooldown(string recipeId) => GetCooldownRemaining(recipeId) > 0;

        /// <summary>Returns the recipe SO, or null if not found.</summary>
        public ConversionRecipeSO GetRecipe(string recipeId) => _database?.GetById(recipeId);

        // ── Balance Helpers ───────────────────────────────────────────────────────

        private const string GoldId = "gold";

        private double GetBalance(string currencyId)
        {
            if (currencyId == GoldId)
                return _economy != null ? (double)_economy.CurrentResources : 0;
            return _currencyService?.GetBalance(currencyId) ?? 0;
        }

        private double GetCap(string currencyId)
        {
            if (currencyId == GoldId)
                return 0; // EconomyService enforces its own cap internally
            var cfg = _currencyService?.GetConfig(currencyId);
            return cfg?.HardCap ?? 0;
        }

        private bool ConsumeBalance(string currencyId, double amount)
        {
            if (currencyId == GoldId)
            {
                long longAmount = (long)amount;
                if (_economy == null || _economy.CurrentResources < longAmount) return false;
                _economy.DeductResources(longAmount);
                return true;
            }
            return _currencyService?.TrySpend(currencyId, amount) ?? false;
        }

        private void ProduceBalance(string currencyId, double amount)
        {
            if (currencyId == GoldId)
                _economy?.AddResources((long)amount);
            else
                _currencyService?.Add(currencyId, amount);
        }

        private bool Fail(string recipeId, ConversionFailReason reason)
        {
            OnConversionFailed?.Invoke(recipeId, reason);
            return false;
        }

        // ── Test support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Directly sets the cooldown end time for testing.</summary>
        public void SetCooldownForTesting(string recipeId, float secondsRemaining)
            => _cooldownEnds[recipeId] = Time.unscaledTime + secondsRemaining;

        /// <summary>Clears all static subscribers.</summary>
        public static void ClearSubscribersForTesting()
        {
            OnConverted        = null;
            OnConversionFailed = null;
        }
#endif
    }

    /// <summary>Reason codes for <see cref="ConversionService.OnConversionFailed"/>.</summary>
    public enum ConversionFailReason
    {
        NotInitialized,
        UnknownRecipe,
        OnCooldown,
        InsufficientBalance,
        InvalidCount,
    }
}
