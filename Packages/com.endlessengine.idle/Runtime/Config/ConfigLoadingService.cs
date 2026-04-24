using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using EndlessEngine.UI;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Boot sequence orchestrator. Lives on the root GameObject of the Boot scene.
    /// Loads GameBootstrapSO from Addressables address "game-bootstrap", resolves
    /// the RealmPackSO, validates all 8 canonical SO types, populates ConfigRegistry,
    /// and fires OnConfigsLoaded.
    ///
    /// [DefaultExecutionOrder(-1000)] guarantees this runs before all other MonoBehaviours.
    /// If any step fails, transitions to ErrorState — the game halts.
    ///
    /// ADR: ADR-0001 — Addressables Config Loading Strategy and Boot Sequence
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ConfigLoadingService : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The single Addressable address used in all C# code.
        /// ADR-0001: only one string address is permitted in the codebase.
        /// </summary>
        private const string BootstrapAddress = "game-bootstrap";

        /// <summary>
        /// Provisional boot-time budget. Refine after first profiling run (OQ-CFG-01).
        /// </summary>
        private const float BootTimeBudgetSeconds = 3.0f;

        // ── State ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retained handle for the loaded GameBootstrapSO asset.
        /// Released only on application quit or realm swap — not during normal session.
        /// </summary>
        private AsyncOperationHandle<GameBootstrapSO> _bootstrapHandle;

        private enum ConfigLoadState { Uninitialized, Loading, Loaded, ErrorState }
        private ConfigLoadState _state = ConfigLoadState.Uninitialized;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private async void Start()
        {
            _state = ConfigLoadState.Loading;
            await LoadAsync();
        }

        private void OnApplicationQuit()
        {
            // Release the retained handle on quit so Addressables can clean up.
            if (_bootstrapHandle.IsValid())
                Addressables.Release(_bootstrapHandle);
        }

        // ── Load Sequence ─────────────────────────────────────────────────────────

        private async Task LoadAsync()
        {
            var timer = Stopwatch.StartNew();

            // ── Step 1: Load GameBootstrapSO from Addressables ────────────────────
            // Unity 6.2+: throws InvalidKeyException on missing address — verified behavior required.
            GameBootstrapSO bootstrap;
            try
            {
                _bootstrapHandle = Addressables.LoadAssetAsync<GameBootstrapSO>(BootstrapAddress);
                bootstrap = await _bootstrapHandle.Task;
            }
            catch (Exception ex)
            {
                EnterErrorState($"Addressables load failed for address '{BootstrapAddress}': {ex.Message}");
                return;
            }

            if (bootstrap == null)
            {
                EnterErrorState($"LoadAssetAsync returned null for address '{BootstrapAddress}'. " +
                                "Verify the address is registered in the Addressables catalog.");
                return;
            }

            // ── Step 2: Resolve RealmPackSO ───────────────────────────────────────
            RealmPackSO pack = bootstrap.ActiveRealmPack;
            if (pack == null)
            {
                EnterErrorState($"GameBootstrapSO.ActiveRealmPack is null. " +
                                "Assign a RealmPackSO in the GameBootstrap asset.");
                return;
            }

            // ── Step 3: Resolve all 8 SO references from RealmPackSO ─────────────
            // All 8 fields are direct serialized Unity object references — they loaded
            // as part of loading RealmPackSO. No additional Addressables calls needed.
            ResolvedConfigs resolved = ResolveFromPack(pack);
            if (resolved == null)
                return; // EnterErrorState called inside ResolveFromPack

            // ── Step 4: Validate ──────────────────────────────────────────────────
            if (!ConfigValidator.Validate(resolved))
            {
                EnterErrorState("Config validation failed — see console for field-level errors. " +
                                $"Realm: {pack.RealmSlug}");
                return;
            }

            // ── Step 5: Populate ConfigRegistry ──────────────────────────────────
            ConfigRegistry.Populate(resolved);
            // Note: ConfigRegistry.Populate() fires OnConfigsLoaded internally.

            // ── Step 6: Log timing ────────────────────────────────────────────────
            timer.Stop();
            float elapsed = (float)timer.Elapsed.TotalSeconds;

            if (elapsed > BootTimeBudgetSeconds)
                Debug.LogWarning($"[ConfigLoadingService] Boot load took {elapsed:F2}s — exceeds budget of {BootTimeBudgetSeconds}s. " +
                                 "Profile on minimum target hardware and revisit OQ-CFG-01.");
            else
                Debug.Log($"[ConfigLoadingService] Boot load complete in {elapsed:F2}s. " +
                          $"Realm: {pack.RealmSlug}");

            _state = ConfigLoadState.Loaded;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads all 8 SO references from the pack's serialized fields.
        /// Returns null (and calls EnterErrorState) if any required field is null.
        /// </summary>
        private ResolvedConfigs ResolveFromPack(RealmPackSO pack)
        {
            string slug = pack.RealmSlug;

            if (pack.EnemyStatConfig == null)
            { EnterErrorState($"[Realm: {slug}] RealmPackSO.EnemyStatConfig is null."); return null; }

            if (pack.WaveConfig == null)
            { EnterErrorState($"[Realm: {slug}] RealmPackSO.WaveConfig is null."); return null; }

            if (pack.EconomyConfig == null)
            { EnterErrorState($"[Realm: {slug}] RealmPackSO.EconomyConfig is null."); return null; }

            if (pack.UpgradeNodeConfigs == null || pack.UpgradeNodeConfigs.Length == 0)
            { EnterErrorState($"[Realm: {slug}] RealmPackSO.UpgradeNodeConfigs is null or empty."); return null; }

            if (pack.PrestigeConfig == null)
            { EnterErrorState($"[Realm: {slug}] RealmPackSO.PrestigeConfig is null."); return null; }

            if (pack.RealmIdentityConfig == null)
            { EnterErrorState($"[Realm: {slug}] RealmPackSO.RealmIdentityConfig is null."); return null; }

            if (pack.PlayerBaseStatConfig == null)
            { EnterErrorState($"[Realm: {slug}] RealmPackSO.PlayerBaseStatConfig is null."); return null; }

            if (pack.SchemaVersion == null)
            { EnterErrorState($"[Realm: {slug}] RealmPackSO.SchemaVersion is null."); return null; }

            return new ResolvedConfigs(
                pack.EnemyStatConfig,
                pack.WaveConfig,
                pack.EconomyConfig,
                pack.UpgradeNodeConfigs,
                pack.PrestigeConfig,
                pack.RealmIdentityConfig,
                pack.PlayerBaseStatConfig,
                pack.SchemaVersion,
                slug
            );
        }

        /// <summary>
        /// Transitions to ErrorState: logs the error and shows the error screen.
        /// Game cannot proceed — player must restart.
        /// </summary>
        private void EnterErrorState(string reason)
        {
            _state = ConfigLoadState.ErrorState;
            Debug.LogError($"[ConfigLoadingService] Boot halted: {reason}");
            ErrorScreenUI.Show(reason);
        }
    }
}
