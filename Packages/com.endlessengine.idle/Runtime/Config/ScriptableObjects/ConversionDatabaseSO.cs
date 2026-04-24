using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Registry of all conversion recipes available in the game.
    /// ConversionService reads this on Initialize.
    /// </summary>
    [CreateAssetMenu(fileName = "ConversionDatabase", menuName = "Endless Engine/Config/Conversion Database")]
    public class ConversionDatabaseSO : ScriptableObject
    {
        public ConversionRecipeSO[] Recipes = new ConversionRecipeSO[0];

        /// <summary>Returns the recipe with the given id, or null.</summary>
        public ConversionRecipeSO GetById(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return null;
            foreach (var r in Recipes)
                if (r != null && r.RecipeId == recipeId)
                    return r;
            return null;
        }
    }
}
