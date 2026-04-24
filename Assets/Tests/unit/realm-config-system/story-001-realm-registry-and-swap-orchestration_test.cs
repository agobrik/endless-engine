// Tests for Story S2-05: Realm Config System — Registry, Unlock State, Swap Orchestration
// Type: Logic (Unit/EditMode)
// Story: production/epics/realm-config-system/story-001-realm-registry-and-swap-orchestration.md
//
// Acceptance Criteria: AC-RCS-01, AC-RCS-02, AC-RCS-03, AC-CFG-11
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.RealmConfigSystem

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using EndlessEngine.Config;
using EndlessEngine.Realm;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.RealmConfigSystem
{
    /// <summary>
    /// Unit tests for RealmConfigSystem registry, unlock state, and swap orchestration (S2-05).
    /// Validates AC-RCS-01, AC-RCS-02, AC-RCS-03, AC-CFG-11.
    /// </summary>
    [TestFixture]
    public class RealmRegistryAndSwapTests
    {
        private global::EndlessEngine.Realm.RealmConfigSystem _rcs;
        private RealmRegistrySO _registry;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _rcs = new GameObject("RCS_Test").AddComponent<global::EndlessEngine.Realm.RealmConfigSystem>();
            _rcs.ResetForTesting();

            var playerConfig  = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            var economyConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            ConfigRegistry.InjectForTesting(player: playerConfig, economy: economyConfig);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_rcs != null)
                UnityEngine.Object.DestroyImmediate(_rcs.gameObject);
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private RealmRegistrySO MakeRegistry(params (string slug, bool isDefault, int prestigeThreshold)[] realms)
        {
            var registry = ScriptableObject.CreateInstance<RealmRegistrySO>();
            registry.Realms = new RealmEntry[realms.Length];
            for (int i = 0; i < realms.Length; i++)
            {
                registry.Realms[i] = new RealmEntry
                {
                    Slug                     = realms[i].slug,
                    DisplayName              = realms[i].slug,
                    IsDefaultRealm           = realms[i].isDefault,
                    UnlockPrestigeThreshold  = realms[i].prestigeThreshold,
                    Pack                     = null,
                };
            }
            return registry;
        }

        // ── AC-RCS-01: Default realm always available ─────────────────────────────

        [Test]
        [Description("AC-RCS-01: New save with no unlocked slugs → default realm appears unlocked.")]
        public void GetAvailableRealms_NewSave_DefaultRealmUnlocked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var registry = MakeRegistry(("base_realm", true, 0));
            var saveData = new SaveData { UnlockedRealmSlugs = null };

            _rcs.InjectForTesting(registry, saveData, isNewGame: true);

            // Act
            var realms = _rcs.GetAvailableRealms();

            // Assert
            Assert.AreEqual(1, realms.Length, "AC-RCS-01: Should have 1 realm in result");
            Assert.AreEqual("base_realm", realms[0].Slug, "AC-RCS-01: Default realm slug must be 'base_realm'");
            Assert.IsTrue(realms[0].IsUnlocked, "AC-RCS-01: Default realm must be unlocked on new save");
#endif
        }

        [Test]
        [Description("AC-RCS-01: Empty UnlockedRealmSlugs list → default realm still available.")]
        public void GetAvailableRealms_EmptyUnlockedList_DefaultRealmAvailable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var registry = MakeRegistry(("base_realm", true, 0), ("realm_b", false, 3));
            var saveData = new SaveData { UnlockedRealmSlugs = new List<string>() };

            _rcs.InjectForTesting(registry, saveData);

            // Act
            var realms = _rcs.GetAvailableRealms();

            // Assert: only base_realm is unlocked
            Assert.IsTrue(realms[0].IsUnlocked || realms[1].IsUnlocked,
                "AC-RCS-01: At least the default realm must be unlocked");
            bool baseRealmUnlocked = System.Array.Exists(realms, r => r.Slug == "base_realm" && r.IsUnlocked);
            Assert.IsTrue(baseRealmUnlocked, "Default realm (base_realm) must be unlocked");
            bool realmBUnlocked = System.Array.Exists(realms, r => r.Slug == "realm_b" && r.IsUnlocked);
            Assert.IsFalse(realmBUnlocked, "realm_b must remain locked on new save");
#endif
        }

        // ── AC-RCS-02: Unlock on prestige event ──────────────────────────────────

        [Test]
        [Description("AC-RCS-02: OnRealmUnlocked fires for realm_b → GetAvailableRealms shows realm_b unlocked.")]
        public void FireRealmUnlocked_AddsSlugToUnlockedList()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var registry = MakeRegistry(("base_realm", true, 0), ("realm_b", false, 3));
            var saveData = new SaveData { UnlockedRealmSlugs = null };
            _rcs.InjectForTesting(registry, saveData);

            // Pre-condition: realm_b locked
            var before = _rcs.GetAvailableRealms();
            bool realmBBefore = System.Array.Exists(before, r => r.Slug == "realm_b" && r.IsUnlocked);
            Assert.IsFalse(realmBBefore, "Precondition: realm_b must start locked");

            // Act: fire unlock event
            _rcs.FireRealmUnlockedForTesting("realm_b");

            // Assert: realm_b now unlocked
            var after = _rcs.GetAvailableRealms();
            bool realmBAfter = System.Array.Exists(after, r => r.Slug == "realm_b" && r.IsUnlocked);
            Assert.IsTrue(realmBAfter, "AC-RCS-02: realm_b must be unlocked after OnRealmUnlocked fires");
#endif
        }

        [Test]
        [Description("AC-RCS-02: Duplicate unlock event → no duplicate in unlocked list.")]
        public void FireRealmUnlocked_DuplicateEvent_NoDuplicateInList()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var registry = MakeRegistry(("base_realm", true, 0), ("realm_b", false, 3));
            _rcs.InjectForTesting(registry, new SaveData());

            // Act: fire unlock twice
            _rcs.FireRealmUnlockedForTesting("realm_b");
            _rcs.FireRealmUnlockedForTesting("realm_b");

            // Assert: realm_b appears once (IsUnlocked=true, only one entry)
            var realms = _rcs.GetAvailableRealms();
            int count = 0;
            foreach (var r in realms)
                if (r.Slug == "realm_b" && r.IsUnlocked) count++;
            Assert.AreEqual(1, count, "Duplicate unlock events must not create duplicate entries");
#endif
        }

        // ── AC-RCS-03: Swap calls BeginRealmSwapAsync ─────────────────────────────

        [Test]
        [Description("AC-RCS-03: SelectRealmAsync with unlocked realm calls BeginRealmSwapAsync.")]
        public async Task SelectRealmAsync_UnlockedRealm_CallsBeginRealmSwapAsync()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: realm_b unlocked; create a pack SO for the swap
            var pack = ScriptableObject.CreateInstance<RealmPackSO>();
            pack.RealmSlug = "realm_b";

            var registry = ScriptableObject.CreateInstance<RealmRegistrySO>();
            registry.Realms = new[]
            {
                new RealmEntry { Slug = "base_realm", IsDefaultRealm = true,  Pack = null  },
                new RealmEntry { Slug = "realm_b",    IsDefaultRealm = false, Pack = pack  },
            };

            _rcs.InjectForTesting(registry, new SaveData());
            _rcs.FireRealmUnlockedForTesting("realm_b");

            // Capture OnRealmSwapped event
            bool realmSwapped = false;
            ConfigRegistry.OnRealmSwapped += () => realmSwapped = true;

            // Act
            await _rcs.SelectRealmAsync("realm_b");

            ConfigRegistry.OnRealmSwapped -= () => realmSwapped = true;

            // Assert: swap completed (OnRealmSwapped fired)
            Assert.IsTrue(realmSwapped,
                "AC-RCS-03: ConfigRegistry.OnRealmSwapped must fire after SelectRealmAsync completes");
#endif
        }

        [Test]
        [Description("AC-RCS-03: SelectRealmAsync with locked realm → no swap, warning logged.")]
        public async Task SelectRealmAsync_LockedRealm_NoSwapOccurs()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: realm_b not unlocked
            var registry = MakeRegistry(("base_realm", true, 0), ("realm_b", false, 3));
            _rcs.InjectForTesting(registry, new SaveData());

            bool realmSwapped = false;
            ConfigRegistry.OnRealmSwapped += () => realmSwapped = true;

            LogAssert.Expect(LogType.Warning, new Regex("locked realm"));

            // Act
            await _rcs.SelectRealmAsync("realm_b");

            ConfigRegistry.OnRealmSwapped -= () => realmSwapped = true;

            // Assert
            Assert.IsFalse(realmSwapped,
                "SelectRealmAsync with locked realm must not trigger a swap");
#endif
        }

        // ── AC-CFG-11: Schema version readable after swap ─────────────────────────

        [Test]
        [Description("AC-CFG-11: ConfigRegistry.Schema.CurrentSchemaVersion readable after realm swap.")]
        public async Task AfterRealmSwap_SchemaVersionReadable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: pack with a SchemaVersionSO
            var schema = ScriptableObject.CreateInstance<SchemaVersionSO>();
            schema.CurrentSchemaVersion = 3;

            var pack = ScriptableObject.CreateInstance<RealmPackSO>();
            pack.RealmSlug    = "realm_b";
            pack.SchemaVersion = schema;

            var registry = ScriptableObject.CreateInstance<RealmRegistrySO>();
            registry.Realms = new[]
            {
                new RealmEntry { Slug = "base_realm", IsDefaultRealm = true,  Pack = null },
                new RealmEntry { Slug = "realm_b",    IsDefaultRealm = false, Pack = pack },
            };

            // Inject schema so IsLoaded=true
            ConfigRegistry.InjectForTesting(schema: schema);
            _rcs.InjectForTesting(registry, new SaveData());
            _rcs.FireRealmUnlockedForTesting("realm_b");

            // Act
            await _rcs.SelectRealmAsync("realm_b");

            // Assert: schema readable after swap
            Assert.DoesNotThrow(() =>
            {
                int ver = ConfigRegistry.Schema.CurrentSchemaVersion;
                Assert.AreEqual(3, ver, "AC-CFG-11: CurrentSchemaVersion must match injected value after swap");
            }, "AC-CFG-11: ConfigRegistry.Schema must be readable after realm swap");
#endif
        }

        // ── Ghost slug in save ────────────────────────────────────────────────────

        [Test]
        [Description("Unknown slug in save data is removed with a warning; valid realms unaffected.")]
        public void HandleSaveLoaded_GhostSlugInSave_SkipsWithWarning()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: save has "ghost_realm" not in registry
            var registry = MakeRegistry(("base_realm", true, 0));
            var saveData = new SaveData
            {
                UnlockedRealmSlugs = new List<string> { "base_realm", "ghost_realm" },
            };

            LogAssert.Expect(LogType.Warning, new Regex("ghost_realm"));

            // Act
            _rcs.InjectForTesting(registry, saveData);

            // Assert: valid realm still available, ghost realm not present
            var realms = _rcs.GetAvailableRealms();
            bool baseAvailable  = System.Array.Exists(realms, r => r.Slug == "base_realm" && r.IsUnlocked);
            bool ghostPresent   = System.Array.Exists(realms, r => r.Slug == "ghost_realm");
            Assert.IsTrue(baseAvailable, "Valid realm must still be available after ghost slug is removed");
            Assert.IsFalse(ghostPresent, "Ghost slug must not appear in GetAvailableRealms()");
#endif
        }
    }
}
