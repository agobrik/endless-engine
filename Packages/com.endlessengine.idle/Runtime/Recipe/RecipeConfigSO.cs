using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Recipe
{
    /// <summary>
    /// Defines a crafting recipe: N ingredient stacks → 1 output item.
    ///
    /// Ingredients are matched against InventoryService item counts.
    /// Output is added to inventory on successful craft.
    ///
    /// Optional gold cost and secondary currency cost are also deducted.
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeConfig", menuName = "Endless Engine/Recipe/Recipe Config")]
    public class RecipeConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID. Never change after release.")]
        public string RecipeId;

        public string DisplayName;

        [TextArea(1, 3)] public string Description;

        public Sprite Icon;

        [Header("Ingredients")]
        public List<RecipeIngredient> Ingredients = new List<RecipeIngredient>();

        [Header("Output")]
        public string OutputItemId;
        [Min(1)] public int OutputQuantity = 1;

        [Header("Costs (optional)")]
        [Min(0)] public double GoldCost;
        public string CurrencyCostId;
        [Min(0)] public double CurrencyCostAmount;

        [Header("Behavior")]
        [Tooltip("If true, ingredients are consumed on craft.")]
        public bool ConsumeIngredients = true;

        [Tooltip("If false, recipe is locked until explicitly unlocked via UnlockLogService.")]
        public bool UnlockedByDefault = true;
    }

    [Serializable]
    public class RecipeIngredient
    {
        [Tooltip("ItemConfigSO.ItemId of the required item.")]
        public string ItemId;

        [Min(1)] public int Quantity = 1;
    }
}
