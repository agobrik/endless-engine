using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Realm
{
    /// <summary>
    /// Manages realm unlock state and coordinates realm swap orchestration.
    ///
    /// On load: reads UnlockedRealmSlugs from SaveData; validates against registry;
    ///          unknown slugs are removed with a LogWarning.
    ///
    /// On OnRealmUnlocked: adds slug to available list.
    ///
    /// SelectRealmAsync: validates unlock → calls ConfigRegistry.BeginRealmSwapAsync().
    ///   Must not be called during GameState.Combat (Debug.Assert in Editor).
    ///
    /// ArenaBounds (from RealmIdentityConfigSO): consumed only after OnConfigsLoaded or OnRealmSwapped.
    ///
    /// ADR: ADR-0003 — Config Registry Static Service Locator
    /// ADR: ADR-0001 — Addressables Config Loading
    /// </summary>
    public class RealmConfigSystem : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private readonly List<string> _unlockedSlugs = new();
        private RealmRegistrySO        _registry;

        [SerializeField]
        private SaveService _saveService;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_saveService != null)
                _saveService.OnSaveLoaded += HandleSaveLoaded;

            PrestigeStateManager.OnRealmUnlocked += HandleRealmUnlocked;
        }

        private void OnDisable()
        {
            if (_saveService != null)
                _saveService.OnSaveLoaded -= HandleSaveLoaded;

            PrestigeStateManager.OnRealmUnlocked -= HandleRealmUnlocked;
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void HandleSaveLoaded(SaveData saveData, bool isNewGame)
        {
            if (_registry == null)
                _registry = ConfigRegistry.RealmRegistry;
            if (_registry == null)
            {
                Debug.LogWarning("[RealmConfigSystem] No RealmRegistry in ConfigRegistry. Using empty realm list.");
                return;
            }

            _unlockedSlugs.Clear();

            // Validate saved slugs against registry
            if (saveData.UnlockedRealmSlugs != null)
            {
                foreach (var slug in saveData.UnlockedRealmSlugs)
                {
                    if (_registry.HasRealm(slug))
                        _unlockedSlugs.Add(slug);
                    else
                        Debug.LogWarning($"[RealmConfigSystem] Unknown realm slug in save: '{slug}' — skipping.");
                }
            }

            // Default realm is always available
            var defaultRealm = _registry.GetDefaultRealm();
            if (defaultRealm != null && !_unlockedSlugs.Contains(defaultRealm.Slug))
                _unlockedSlugs.Add(defaultRealm.Slug);
        }

        private void HandleRealmUnlocked(string slug)
        {
            if (!_unlockedSlugs.Contains(slug))
                _unlockedSlugs.Add(slug);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns display data for all known realms, with IsUnlocked reflecting current state.
        /// </summary>
        public RealmDisplayData[] GetAvailableRealms()
        {
            if (_registry == null) return new RealmDisplayData[0];

            var result = new RealmDisplayData[_registry.AllRealms.Count];
            for (int i = 0; i < _registry.AllRealms.Count; i++)
            {
                var r = _registry.AllRealms[i];
                result[i] = new RealmDisplayData
                {
                    Slug                     = r.Slug,
                    DisplayName              = r.DisplayName,
                    PreviewImage             = r.PreviewImage,
                    IsUnlocked               = _unlockedSlugs.Contains(r.Slug),
                    UnlockPrestigeRequired   = r.UnlockPrestigeThreshold,
                };
            }
            return result;
        }

        /// <summary>
        /// Attempts to swap to the realm identified by <paramref name="slug"/>.
        /// No-op if the realm is locked.
        /// </summary>
        public async Task SelectRealmAsync(string slug)
        {
            if (!_unlockedSlugs.Contains(slug))
            {
                Debug.LogWarning($"[RealmConfigSystem] Attempted swap to locked realm '{slug}' — ignoring.");
                return;
            }

            // Forbidden during combat
            Debug.Assert(
                !IsInCombat(),
                "[RealmConfigSystem] SelectRealmAsync called during combat — realm swap is forbidden during combat.");

            var pack = _registry?.GetPack(slug);
            if (pack == null)
            {
                Debug.LogWarning($"[RealmConfigSystem] No RealmPackSO found for slug '{slug}'.");
                return;
            }

            await ConfigRegistry.BeginRealmSwapAsync(pack);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static bool IsInCombat()
        {
            // Placeholder — GameStateManager not yet implemented.
            // When GameStateManager is available, replace with:
            //   return GameStateManager.CurrentState == GameState.Combat;
            return false;
        }

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Directly injects a registry and unlocked slugs for unit testing
        /// without requiring MonoBehaviour event wiring.
        /// </summary>
        public void InjectForTesting(RealmRegistrySO registry, SaveData saveData = null, bool isNewGame = false)
        {
            _registry = registry;
            var data = saveData ?? new SaveData();
            HandleSaveLoaded(data, isNewGame);
        }

        /// <summary>Fires the realm unlocked handler directly for testing.</summary>
        public void FireRealmUnlockedForTesting(string slug) => HandleRealmUnlocked(slug);

        /// <summary>Clears unlock state for test isolation.</summary>
        public void ResetForTesting()
        {
            _unlockedSlugs.Clear();
            _registry = null;
        }
#endif
    }

    // ── Value types ───────────────────────────────────────────────────────────────

    /// <summary>Display data for a single realm entry in the realm selector UI.</summary>
    public struct RealmDisplayData
    {
        public string Slug;
        public string DisplayName;
        public Sprite PreviewImage;
        public bool   IsUnlocked;
        public int    UnlockPrestigeRequired;
    }
}
