using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Registry of all currency types in the game.
    /// CurrencyService reads this on boot to initialize all currency balances.
    ///
    /// Usage: Tools → Endless Engine → Create Currency Database
    ///        (or create manually via Assets → Create → Endless Engine → Config → Currency Database)
    /// </summary>
    [CreateAssetMenu(fileName = "CurrencyDatabase", menuName = "Endless Engine/Config/Currency Database")]
    public class CurrencyDatabaseSO : ScriptableObject
    {
        [Tooltip("All currency definitions. Order determines display order in HUD.")]
        public CurrencyConfigSO[] Currencies = new CurrencyConfigSO[0];

        /// <summary>Returns the config for the given id, or null if not found.</summary>
        public CurrencyConfigSO GetById(string currencyId)
        {
            if (string.IsNullOrEmpty(currencyId)) return null;
            foreach (var c in Currencies)
                if (c != null && c.CurrencyId == currencyId)
                    return c;
            return null;
        }

        /// <summary>Returns true if a currency with the given id exists in this database.</summary>
        public bool Contains(string currencyId) => GetById(currencyId) != null;
    }
}
