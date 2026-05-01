// Tests for Sprint 8 — S8-04: ConversionService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Basic convert: balance consumed + produced
//   - Insufficient balance → fails
//   - Cooldown: second call within window fails; after window succeeds
//   - Bulk convert: multiple executions in one call
//   - Gold (primary currency) as input or output
//   - Unknown recipe → fails
//   - OnConverted / OnConversionFailed events
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ConversionSystem

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.ConversionSystem
{
    [TestFixture]
    public class ConversionServiceTests
    {
        private ConversionService _service;
        private EndlessEngine.Economy.CurrencyService   _currencyService;
        private EconomyService    _economy;

        private ConversionDatabaseSO _database;
        private ConversionRecipeSO   _goldToGems;
        private ConversionRecipeSO   _gemsToGold;
        private ConversionRecipeSO   _bulkRecipe;
        private ConversionRecipeSO   _cooldownRecipe;

        private CurrencyDatabaseSO   _currencyDb;
        private CurrencyConfigSO     _gemConfig;
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Economy config
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            // Currency config
            _gemConfig               = ScriptableObject.CreateInstance<CurrencyConfigSO>();
            _gemConfig.CurrencyId    = "gems";
            _gemConfig.HardCap       = 100_000;
            _gemConfig.StartingAmount = 0;

            _currencyDb            = ScriptableObject.CreateInstance<CurrencyDatabaseSO>();
            _currencyDb.Currencies = new[] { _gemConfig };

            // Recipes
            _goldToGems = ScriptableObject.CreateInstance<ConversionRecipeSO>();
            _goldToGems.RecipeId        = "gold_to_gems";
            _goldToGems.InputCurrencyId  = "gold";
            _goldToGems.InputAmount      = 100;
            _goldToGems.OutputCurrencyId = "gems";
            _goldToGems.OutputAmount     = 1;
            _goldToGems.CooldownSeconds  = 0f;
            _goldToGems.AllowBulk        = false;

            _gemsToGold = ScriptableObject.CreateInstance<ConversionRecipeSO>();
            _gemsToGold.RecipeId        = "gems_to_gold";
            _gemsToGold.InputCurrencyId  = "gems";
            _gemsToGold.InputAmount      = 1;
            _gemsToGold.OutputCurrencyId = "gold";
            _gemsToGold.OutputAmount     = 80;
            _gemsToGold.CooldownSeconds  = 0f;
            _gemsToGold.AllowBulk        = false;

            _bulkRecipe = ScriptableObject.CreateInstance<ConversionRecipeSO>();
            _bulkRecipe.RecipeId        = "bulk_gold_to_gems";
            _bulkRecipe.InputCurrencyId  = "gold";
            _bulkRecipe.InputAmount      = 10;
            _bulkRecipe.OutputCurrencyId = "gems";
            _bulkRecipe.OutputAmount     = 1;
            _bulkRecipe.CooldownSeconds  = 0f;
            _bulkRecipe.AllowBulk        = true;
            _bulkRecipe.MaxBulkCount     = 0; // unlimited by config

            _cooldownRecipe = ScriptableObject.CreateInstance<ConversionRecipeSO>();
            _cooldownRecipe.RecipeId        = "cooldown_recipe";
            _cooldownRecipe.InputCurrencyId  = "gold";
            _cooldownRecipe.InputAmount      = 10;
            _cooldownRecipe.OutputCurrencyId = "gems";
            _cooldownRecipe.OutputAmount     = 1;
            _cooldownRecipe.CooldownSeconds  = 60f;
            _cooldownRecipe.AllowBulk        = false;

            _database         = ScriptableObject.CreateInstance<ConversionDatabaseSO>();
            _database.Recipes = new[] { _goldToGems, _gemsToGold, _bulkRecipe, _cooldownRecipe };

            // Services
            var go = new GameObject("ConversionTest");

            _economy = go.AddComponent<EconomyService>();
            _economy.Initialize(upgradeTreeQuery: null, saveNotifier: null);
            var saveData = new SaveData { CurrentResources = 10_000L };
            _economy.OnAfterLoad(saveData);

            _currencyService = go.AddComponent<EndlessEngine.Economy.CurrencyService>();
            _currencyService.Initialize(_currencyDb);
            var currSave = new SaveData();
            currSave.EnsureDefaults();
            _currencyService.OnAfterLoad(currSave);
            _currencyService.InjectBalanceForTesting("gems", 50); // start with 50 gems

            _service = go.AddComponent<ConversionService>();
            _service.Initialize(_database, _economy, _currencyService);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConversionService.ClearSubscribersForTesting();
            EndlessEngine.Economy.CurrencyService.ClearSubscribersForTesting();

            if (_service != null) Object.DestroyImmediate(_service.gameObject);
            if (_database != null)      Object.DestroyImmediate(_database);
            if (_goldToGems != null)    Object.DestroyImmediate(_goldToGems);
            if (_gemsToGold != null)    Object.DestroyImmediate(_gemsToGold);
            if (_bulkRecipe != null)    Object.DestroyImmediate(_bulkRecipe);
            if (_cooldownRecipe != null)Object.DestroyImmediate(_cooldownRecipe);
            if (_currencyDb != null)    Object.DestroyImmediate(_currencyDb);
            if (_gemConfig != null)     Object.DestroyImmediate(_gemConfig);
            if (_econConfig != null)    Object.DestroyImmediate(_econConfig);

            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
#endif
        }

        // ── Basic conversion ──────────────────────────────────────────────────────

        [Test]
        public void TryConvert_GoldToGems_ConsumesGoldProducesGems()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            double goldBefore = _economy.CurrentResources;
            double gemsBefore = _currencyService.GetBalance("gems");

            bool result = _service.TryConvert("gold_to_gems");

            Assert.IsTrue(result);
            Assert.AreEqual(goldBefore - 100, _economy.CurrentResources, "100 gold should be consumed");
            Assert.AreEqual(gemsBefore + 1,   _currencyService.GetBalance("gems"), 0.001, "1 gem should be produced");
#endif
        }

        [Test]
        public void TryConvert_GemsToGold_ConsumesGemsProducesGold()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            double gemsBefore = _currencyService.GetBalance("gems");
            double goldBefore   = _economy.CurrentResources;

            bool result = _service.TryConvert("gems_to_gold");

            Assert.IsTrue(result);
            Assert.AreEqual(gemsBefore - 1, _currencyService.GetBalance("gems"), 0.001);
            Assert.AreEqual(goldBefore + 80, _economy.CurrentResources);
#endif
        }

        // ── Insufficient balance ──────────────────────────────────────────────────

        [Test]
        public void TryConvert_InsufficientGold_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Drain gold
            _economy.InjectStateForTesting(currentResources: 50L, hardCap: 1_000_000L, startingGold: 0L);
            bool result = _service.TryConvert("gold_to_gems"); // needs 100
            Assert.IsFalse(result);
            // Balance unchanged
            Assert.AreEqual(50L, _economy.CurrentResources);
#endif
        }

        [Test]
        public void TryConvert_InsufficientGems_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _currencyService.InjectBalanceForTesting("gems", 0);
            bool result = _service.TryConvert("gems_to_gold"); // needs 1 gem
            Assert.IsFalse(result);
#endif
        }

        // ── Unknown recipe ────────────────────────────────────────────────────────

        [Test]
        public void TryConvert_UnknownRecipe_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool result = _service.TryConvert("nonexistent_recipe");
            Assert.IsFalse(result);
#endif
        }

        // ── Cooldown ──────────────────────────────────────────────────────────────

        [Test]
        public void TryConvert_SecondCallWithinCooldown_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool first = _service.TryConvert("cooldown_recipe");
            Assert.IsTrue(first);

            bool second = _service.TryConvert("cooldown_recipe");
            Assert.IsFalse(second, "Second call within cooldown must fail");

            Assert.IsTrue(_service.IsOnCooldown("cooldown_recipe"));
            Assert.Greater(_service.GetCooldownRemaining("cooldown_recipe"), 0f);
#endif
        }

        [Test]
        public void TryConvert_AfterCooldown_Succeeds()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.TryConvert("cooldown_recipe");
            // Simulate cooldown expired by setting it to the past
            _service.SetCooldownForTesting("cooldown_recipe", -1f);
            Assert.IsFalse(_service.IsOnCooldown("cooldown_recipe"));
            bool result = _service.TryConvert("cooldown_recipe");
            Assert.IsTrue(result);
#endif
        }

        // ── Bulk conversion ───────────────────────────────────────────────────────

        [Test]
        public void TryConvert_Bulk_ExecutesMultipleTimes()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            double goldBefore  = _economy.CurrentResources; // 10_000
            double gemsBefore = _currencyService.GetBalance("gems");

            bool result = _service.TryConvert("bulk_gold_to_gems", count: 5);

            Assert.IsTrue(result);
            Assert.AreEqual(goldBefore - 50L, _economy.CurrentResources, "5 × 10 gold = 50 consumed");
            Assert.AreEqual(gemsBefore + 5,   _currencyService.GetBalance("gems"), 0.001, "5 gems produced");
#endif
        }

        [Test]
        public void TryConvert_Bulk_CappedByAvailableBalance()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Only 30 gold available → max 3 executions at 10 each
            _economy.InjectStateForTesting(currentResources: 30L, hardCap: 1_000_000L, startingGold: 0L);
            double gemsBefore = _currencyService.GetBalance("gems");

            bool result = _service.TryConvert("bulk_gold_to_gems", count: 100);

            Assert.IsTrue(result);
            Assert.AreEqual(0L, _economy.CurrentResources);
            Assert.AreEqual(gemsBefore + 3, _currencyService.GetBalance("gems"), 0.001);
#endif
        }

        // ── Events ────────────────────────────────────────────────────────────────

        [Test]
        public void TryConvert_Success_FiresOnConverted()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string capturedId   = null;
            int    capturedRuns = 0;
            ConversionService.OnConverted += (id, runs, _, _) => { capturedId = id; capturedRuns = runs; };

            _service.TryConvert("gold_to_gems");

            Assert.AreEqual("gold_to_gems", capturedId);
            Assert.AreEqual(1, capturedRuns);
#endif
        }

        [Test]
        public void TryConvert_Failure_FiresOnConversionFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConversionFailReason? capturedReason = null;
            ConversionService.OnConversionFailed += (_, reason) => capturedReason = reason;

            _service.TryConvert("nonexistent_recipe");

            Assert.IsNotNull(capturedReason);
            Assert.AreEqual(ConversionFailReason.UnknownRecipe, capturedReason.Value);
#endif
        }
    }
}
