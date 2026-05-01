using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Recipe;

namespace EndlessEngine.Tests.Recipe
{
    [TestFixture]
    public class RecipeServiceTests
    {
        private GameObject       _root;
        private RecipeService    _service;
        private InventoryService _inventory;
        private EconomyService   _economy;

        private List<(string recipeId, string outputId, int qty)> _craftLog;
        private List<(string recipeId, CraftFailReason reason)>   _failLog;

        [SetUp]
        public void SetUp()
        {
            RecipeService.ClearSubscribersForTesting();
            BigNumberFactory.Configure(NumberBackend.DoubleNumber);

            _root      = new GameObject("RecipeServiceRoot");
            _service   = _root.AddComponent<RecipeService>();
            _inventory = _root.AddComponent<InventoryService>();
            _economy   = _root.AddComponent<EconomyService>();

            _inventory.Initialize(null, maxSlots: 50);
            _economy.InjectStateForTesting(1000.0, 1_000_000.0, 0.0);

            _craftLog = new List<(string, string, int)>();
            _failLog  = new List<(string, CraftFailReason)>();

            RecipeService.OnCraftCompleted += (id, outId, qty) => _craftLog.Add((id, outId, qty));
            RecipeService.OnCraftFailed    += (id, reason)     => _failLog.Add((id, reason));
        }

        [TearDown]
        public void TearDown()
        {
            RecipeService.ClearSubscribersForTesting();
            EconomyService.ClearSubscribersForTesting();
            Object.DestroyImmediate(_root);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static RecipeConfigSO MakeRecipe(
            string recipeId,
            string outputId,
            int outputQty = 1,
            double goldCost = 0,
            bool unlockedByDefault = true,
            params (string itemId, int qty)[] ingredients)
        {
            var so = ScriptableObject.CreateInstance<RecipeConfigSO>();
            so.RecipeId          = recipeId;
            so.OutputItemId      = outputId;
            so.OutputQuantity    = outputQty;
            so.GoldCost          = goldCost;
            so.ConsumeIngredients = true;
            so.UnlockedByDefault = unlockedByDefault;
            so.Ingredients       = new List<RecipeIngredient>();
            foreach (var (itemId, qty) in ingredients)
                so.Ingredients.Add(new RecipeIngredient { ItemId = itemId, Quantity = qty });
            return so;
        }

        // ── Basic crafting ────────────────────────────────────────────────────────

        [Test]
        public void Craft_WithSufficientIngredients_Succeeds()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 0, true, ("iron", 2));
            _service.Initialize(new[] { recipe }, _inventory, _economy);
            _inventory.Add("iron", 5);

            bool result = _service.Craft("r1");

            Assert.IsTrue(result);
            Assert.AreEqual(1, _craftLog.Count);
            Assert.AreEqual("r1",   _craftLog[0].recipeId);
            Assert.AreEqual("sword", _craftLog[0].outputId);
            Assert.AreEqual(1,      _craftLog[0].qty);
        }

        [Test]
        public void Craft_ConsumesIngredients()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 0, true, ("iron", 2));
            _service.Initialize(new[] { recipe }, _inventory, _economy);
            _inventory.Add("iron", 3);

            _service.Craft("r1");

            Assert.AreEqual(1, _inventory.GetCount("iron"), "2 iron consumed, 1 should remain.");
        }

        [Test]
        public void Craft_AddsOutputToInventory()
        {
            var recipe = MakeRecipe("r1", "sword", 2, 0, true, ("iron", 1));
            _service.Initialize(new[] { recipe }, _inventory, _economy);
            _inventory.Add("iron", 1);

            _service.Craft("r1");

            Assert.AreEqual(2, _inventory.GetCount("sword"));
        }

        [Test]
        public void Craft_DeductsGoldCost()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 100.0, true);
            _service.Initialize(new[] { recipe }, _inventory, _economy);

            _service.Craft("r1");

            Assert.AreEqual(900.0, _economy.CurrentResources, 0.01);
        }

        // ── Fail cases ────────────────────────────────────────────────────────────

        [Test]
        public void Craft_InsufficientIngredients_FailsAndFiresEvent()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 0, true, ("iron", 5));
            _service.Initialize(new[] { recipe }, _inventory, _economy);
            _inventory.Add("iron", 2);

            bool result = _service.Craft("r1");

            Assert.IsFalse(result);
            Assert.AreEqual(1, _failLog.Count);
            Assert.AreEqual(CraftFailReason.InsufficientIngredients, _failLog[0].reason);
        }

        [Test]
        public void Craft_InsufficientGold_FailsAndFiresEvent()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 5000.0, true);
            _service.Initialize(new[] { recipe }, _inventory, _economy);

            bool result = _service.Craft("r1");

            Assert.IsFalse(result);
            Assert.AreEqual(CraftFailReason.InsufficientGold, _failLog[0].reason);
        }

        [Test]
        public void Craft_UnknownRecipeId_Fails()
        {
            _service.Initialize(new RecipeConfigSO[0], _inventory, _economy);

            bool result = _service.Craft("nonexistent");

            Assert.IsFalse(result);
            Assert.AreEqual(CraftFailReason.NotFound, _failLog[0].reason);
        }

        [Test]
        public void Craft_LockedRecipe_FailsWithLocked()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 0, false); // not unlocked by default
            _service.Initialize(new[] { recipe }, _inventory, _economy);

            bool result = _service.Craft("r1");

            Assert.IsFalse(result);
            Assert.AreEqual(CraftFailReason.Locked, _failLog[0].reason);
        }

        // ── Unlock gating ─────────────────────────────────────────────────────────

        [Test]
        public void UnlockRecipe_AllowsCrafting_AfterUnlock()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 0, false);
            _service.Initialize(new[] { recipe }, _inventory, _economy);

            _service.UnlockRecipe("r1");
            bool result = _service.Craft("r1");

            Assert.IsTrue(result);
        }

        [Test]
        public void IsUnlocked_FalseForLockedByDefault()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 0, false);
            _service.Initialize(new[] { recipe }, _inventory, _economy);

            Assert.IsFalse(_service.IsUnlockedForTesting("r1"));
        }

        [Test]
        public void IsUnlocked_TrueForUnlockedByDefault()
        {
            var recipe = MakeRecipe("r1", "sword");
            _service.Initialize(new[] { recipe }, _inventory, _economy);

            Assert.IsTrue(_service.IsUnlockedForTesting("r1"));
        }

        // ── CanCraft ──────────────────────────────────────────────────────────────

        [Test]
        public void CanCraft_ReturnsFalse_WhenIngredientsShort()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 0, true, ("wood", 3));
            _service.Initialize(new[] { recipe }, _inventory, _economy);
            _inventory.Add("wood", 1);

            Assert.IsFalse(_service.CanCraft("r1"));
        }

        [Test]
        public void CanCraft_ReturnsTrue_WhenAllConditionsMet()
        {
            var recipe = MakeRecipe("r1", "sword", 1, 50.0, true, ("wood", 2));
            _service.Initialize(new[] { recipe }, _inventory, _economy);
            _inventory.Add("wood", 2);

            Assert.IsTrue(_service.CanCraft("r1"));
        }

        // ── GetAvailableRecipes ───────────────────────────────────────────────────

        [Test]
        public void GetAvailableRecipes_OnlyReturnsUnlockedAndCraftable()
        {
            var r1 = MakeRecipe("r1", "sword", 1, 0, true);               // craftable
            var r2 = MakeRecipe("r2", "shield", 1, 0, false);              // locked
            var r3 = MakeRecipe("r3", "helmet", 1, 0, true, ("iron", 99)); // insufficient ingredients
            _service.Initialize(new[] { r1, r2, r3 }, _inventory, _economy);

            var available = new List<RecipeConfigSO>(_service.GetAvailableRecipes());

            Assert.AreEqual(1, available.Count);
            Assert.AreEqual("r1", available[0].RecipeId);
        }

        // ── Multiple ingredients ──────────────────────────────────────────────────

        [Test]
        public void Craft_MultipleIngredients_ConsumesAll()
        {
            var recipe = MakeRecipe("r1", "potion", 1, 0, true,
                ("herb", 2), ("water", 1));
            _service.Initialize(new[] { recipe }, _inventory, _economy);
            _inventory.Add("herb", 3);
            _inventory.Add("water", 2);

            _service.Craft("r1");

            Assert.AreEqual(1, _inventory.GetCount("herb"),  "3-2=1 herb should remain.");
            Assert.AreEqual(1, _inventory.GetCount("water"), "2-1=1 water should remain.");
        }
    }
}
