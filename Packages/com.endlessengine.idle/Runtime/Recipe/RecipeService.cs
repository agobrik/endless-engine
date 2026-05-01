using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Stats;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Recipe
{
    /// <summary>
    /// Engine-level crafting service. Validates ingredient availability and costs,
    /// consumes ingredients, and produces output items.
    ///
    /// Bootstrap wiring:
    ///   recipeService.Initialize(configs, inventoryService, economyService);
    ///   recipeService.Initialize(configs, inventoryService, economyService, currencyService);
    ///
    /// Unlock gating: recipes with UnlockedByDefault=false must be unlocked via
    ///   recipeService.UnlockRecipe(recipeId) before they appear in CanCraft()/Craft().
    /// </summary>
    public class RecipeService : MonoBehaviour, IModifierSource
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires after a successful craft. (recipeId, outputItemId, quantity)</summary>
        public static event Action<string, string, int> OnCraftCompleted;

        /// <summary>Fires when a craft attempt fails. (recipeId, reason)</summary>
        public static event Action<string, CraftFailReason> OnCraftFailed;

        // ── State ─────────────────────────────────────────────────────────────────

        private RecipeConfigSO[] _configs;
        private InventoryService _inventory;
        private EconomyService   _economy;
        private CurrencyService  _currency;

        private readonly Dictionary<string, RecipeConfigSO> _lookup   = new Dictionary<string, RecipeConfigSO>();
        private readonly HashSet<string>                     _unlocked = new HashSet<string>();

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(
            RecipeConfigSO[] configs,
            InventoryService inventory,
            EconomyService   economy,
            CurrencyService  currency = null)
        {
            _configs   = configs ?? Array.Empty<RecipeConfigSO>();
            _inventory = inventory;
            _economy   = economy;
            _currency  = currency;

            _lookup.Clear();
            _unlocked.Clear();

            foreach (var c in _configs)
            {
                if (c == null || string.IsNullOrEmpty(c.RecipeId)) continue;
                _lookup[c.RecipeId] = c;
                if (c.UnlockedByDefault) _unlocked.Add(c.RecipeId);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Unlocks a locked recipe. No-op if already unlocked.</summary>
        public void UnlockRecipe(string recipeId) => _unlocked.Add(recipeId);

        /// <summary>True if the recipe exists, is unlocked, and all ingredients + costs are met.</summary>
        public bool CanCraft(string recipeId)
            => GetFailReason(recipeId) == CraftFailReason.None;

        /// <summary>
        /// Attempts to craft the recipe. Returns true on success.
        /// Fires OnCraftCompleted or OnCraftFailed.
        /// </summary>
        public bool Craft(string recipeId)
        {
            var reason = GetFailReason(recipeId);
            if (reason != CraftFailReason.None)
            {
                OnCraftFailed?.Invoke(recipeId, reason);
                return false;
            }

            var config = _lookup[recipeId];

            // Deduct ingredients
            if (config.ConsumeIngredients)
                foreach (var ing in config.Ingredients)
                    _inventory.Remove(ing.ItemId, ing.Quantity);

            // Deduct gold cost
            if (config.GoldCost > 0)
                _economy.DeductResources(config.GoldCost);

            // Deduct currency cost
            if (!string.IsNullOrEmpty(config.CurrencyCostId) && config.CurrencyCostAmount > 0)
                _currency?.TrySpend(config.CurrencyCostId, config.CurrencyCostAmount);

            // Add output
            _inventory.Add(config.OutputItemId, config.OutputQuantity);

            OnCraftCompleted?.Invoke(recipeId, config.OutputItemId, config.OutputQuantity);
            Debug.Log($"[RecipeService] Crafted '{recipeId}' → {config.OutputQuantity}× {config.OutputItemId}");
            return true;
        }

        /// <summary>Returns all unlocked recipes that can currently be crafted.</summary>
        public IEnumerable<RecipeConfigSO> GetAvailableRecipes()
        {
            foreach (var id in _unlocked)
                if (_lookup.TryGetValue(id, out var c) && CanCraft(id))
                    yield return c;
        }

        /// <summary>Returns all unlocked recipes regardless of whether they can be crafted.</summary>
        public IEnumerable<RecipeConfigSO> GetUnlockedRecipes()
        {
            foreach (var id in _unlocked)
                if (_lookup.TryGetValue(id, out var c))
                    yield return c;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private CraftFailReason GetFailReason(string recipeId)
        {
            if (!_lookup.TryGetValue(recipeId, out var config))   return CraftFailReason.NotFound;
            if (!_unlocked.Contains(recipeId))                    return CraftFailReason.Locked;
            if (_inventory == null)                               return CraftFailReason.NoInventory;

            foreach (var ing in config.Ingredients)
                if (_inventory.GetCount(ing.ItemId) < ing.Quantity)
                    return CraftFailReason.InsufficientIngredients;

            if (config.GoldCost > 0 && (_economy == null || _economy.CurrentResources < config.GoldCost))
                return CraftFailReason.InsufficientGold;

            if (!string.IsNullOrEmpty(config.CurrencyCostId) && config.CurrencyCostAmount > 0)
                if (_currency == null || _currency.GetBalance(config.CurrencyCostId) < config.CurrencyCostAmount)
                    return CraftFailReason.InsufficientCurrency;

            return CraftFailReason.None;
        }

        // ── IModifierSource ───────────────────────────────────────────────────────
        // Recipe outputs are items in inventory; stat effects from equipped/held items
        // are resolved by InventoryService once item effects are designed.

        public string SourceId => "recipe";

        public virtual Modifier GetModifier(StatType stat) => Modifier.None;

        private void OnDestroy() => ClearSubscribersForTesting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnCraftCompleted = null;
            OnCraftFailed    = null;
        }
        public bool IsUnlockedForTesting(string id) => _unlocked.Contains(id);
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }

    public enum CraftFailReason
    {
        None,
        NotFound,
        Locked,
        NoInventory,
        InsufficientIngredients,
        InsufficientGold,
        InsufficientCurrency,
    }
}
