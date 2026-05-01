using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Stats;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Prestige
{
    [TestFixture]
    public class PrestigeStateManagerTests
    {
        private PrestigeStateManager _prestige;
        private EconomyService       _economy;
        private PrestigeConfigSO     _config;

        [SetUp]
        public void SetUp()
        {
            BigNumberFactory.Configure(NumberBackend.DoubleNumber);
            PrestigeStateManager.ClearStaticEventsForTesting();
            EconomyService.ClearSubscribersForTesting();

            var go = new GameObject("PrestigeStateManager");
            _prestige = go.AddComponent<PrestigeStateManager>();

            var ecoGo = new GameObject("EconomyService");
            _economy = ecoGo.AddComponent<EconomyService>();
            _economy.InjectStateForTesting(0.0, 1_000_000.0, 0.0);

            _config = ScriptableObject.CreateInstance<PrestigeConfigSO>();
            _config.MinWaveForPrestige       = 0;
            _config.MaxPrestigeCount         = 0;
            _config.MinGoldToPrestige        = 0;
            _config.BaseMultiplierPerPrestige = 1.5f;
            _config.MaxPermanentMultiplier   = 999f;

            _prestige.InjectConfigForTesting(_config);
        }

        [TearDown]
        public void TearDown()
        {
            PrestigeStateManager.ClearStaticEventsForTesting();
            EconomyService.ClearSubscribersForTesting();
            Object.DestroyImmediate(_prestige.gameObject);
            Object.DestroyImmediate(_economy.gameObject);
        }

        // ── CanPrestige gate ──────────────────────────────────────────────────────

        [Test]
        public void CanPrestige_True_WhenNoConstraints()
        {
            Assert.IsTrue(_prestige.CanPrestige);
        }

        [Test]
        public void CanPrestige_False_WhenPrestigeInProgress()
        {
            _prestige.SetPrestigeInProgressForTesting(true);
            Assert.IsFalse(_prestige.CanPrestige);
        }

        [Test]
        public void CanPrestige_False_WhenWaveBelowMinimum()
        {
            _config.MinWaveForPrestige = 10;
            _prestige.SetWaveNumberForTesting(5);
            Assert.IsFalse(_prestige.CanPrestige);
        }

        [Test]
        public void CanPrestige_True_WhenWaveAtMinimum()
        {
            _config.MinWaveForPrestige = 5;
            _prestige.SetWaveNumberForTesting(5);
            Assert.IsTrue(_prestige.CanPrestige);
        }

        [Test]
        public void CanPrestige_False_WhenMaxPrestigeCountReached()
        {
            _config.MaxPrestigeCount = 3;
            _prestige.SetPrestigeCountForTesting(3);
            Assert.IsFalse(_prestige.CanPrestige);
        }

        [Test]
        public void CanPrestige_True_WhenPrestigeCountBelowMax()
        {
            _config.MaxPrestigeCount = 5;
            _prestige.SetPrestigeCountForTesting(4);
            Assert.IsTrue(_prestige.CanPrestige);
        }

        // ── TryPrestige (synchronous test overload) ────────────────────────────────

        [Test]
        public void TryPrestige_IncreasesPrestigeCount()
        {
            _economy.InjectStateForTesting(1000.0, 1_000_000.0, 0.0);

            bool result = _prestige.TryPrestige(_economy);

            Assert.IsTrue(result);
            Assert.AreEqual(1, _prestige.PrestigeCount);
        }

        [Test]
        public void TryPrestige_InsufficientGold_ReturnsFalse()
        {
            _config.MinGoldToPrestige = 500;
            _economy.InjectStateForTesting(100.0, 1_000_000.0, 0.0);

            bool result = _prestige.TryPrestige(_economy);

            Assert.IsFalse(result);
            Assert.AreEqual(0, _prestige.PrestigeCount);
        }

        [Test]
        public void TryPrestige_FiresOnPrestigeComplete_WithCountAndMultiplier()
        {
            _economy.InjectStateForTesting(1000.0, 1_000_000.0, 0.0);

            int firedCount = 0;
            float firedMult = 0f;
            PrestigeStateManager.OnPrestigeComplete += (c, m) => { firedCount = c; firedMult = m; };

            _prestige.TryPrestige(_economy);

            Assert.AreEqual(1, firedCount);
            Assert.AreEqual(Mathf.Pow(1.5f, 1), firedMult, 0.001f);
        }

        [Test]
        public void TryPrestige_InProgressGuard_PreventsConcurrentPrestige()
        {
            _economy.InjectStateForTesting(1000.0, 1_000_000.0, 0.0);
            _prestige.SetPrestigeInProgressForTesting(true);

            bool result = _prestige.TryPrestige(_economy);

            Assert.IsFalse(result);
        }

        // ── GetPermanentMultiplier ────────────────────────────────────────────────

        [Test]
        public void GetPermanentMultiplier_ReturnsOne_AtPrestigeZero()
        {
            // 1.5^0 = 1
            Assert.AreEqual(1.0f, _prestige.GetPermanentMultiplier(), 0.001f);
        }

        [Test]
        public void GetPermanentMultiplier_IncreasesWithPrestigeCount()
        {
            _prestige.SetPrestigeCountForTesting(3);
            float expected = Mathf.Pow(1.5f, 3);
            Assert.AreEqual(expected, _prestige.GetPermanentMultiplier(), 0.001f);
        }

        [Test]
        public void GetPermanentMultiplier_ClampsAtMaxPermanentMultiplier()
        {
            _config.MaxPermanentMultiplier = 5.0f;
            _prestige.SetPrestigeCountForTesting(100);
            Assert.AreEqual(5.0f, _prestige.GetPermanentMultiplier(), 0.001f);
        }

        // ── Save / Load roundtrip ─────────────────────────────────────────────────

        [Test]
        public void OnBeforeSave_WritesPrestigeCount()
        {
            _prestige.SetPrestigeCountForTesting(7);

            var save = new SaveData();
            save.EnsureDefaults();
            _prestige.OnBeforeSave(save);

            Assert.AreEqual(7, save.PrestigeCount);
        }

        [Test]
        public void OnAfterLoad_RestoresPrestigeCount()
        {
            var save = new SaveData();
            save.EnsureDefaults();
            save.PrestigeCount = 4;

            _prestige.OnAfterLoad(save);

            Assert.AreEqual(4, _prestige.PrestigeCount);
        }

        [Test]
        public void OnAfterLoad_PrestigeCountZero_DoesNotCallSetPermanentMultiplier()
        {
            var save = new SaveData();
            save.EnsureDefaults();
            save.PrestigeCount = 0;

            Assert.DoesNotThrow(() => _prestige.OnAfterLoad(save),
                "OnAfterLoad with PrestigeCount=0 must not throw.");
        }

        // ── IModifierSource ───────────────────────────────────────────────────────

        [Test]
        public void GetModifier_IdleYieldRate_ReturnsMultiplierModifier()
        {
            _prestige.SetPrestigeCountForTesting(2);
            float expectedMult = Mathf.Pow(1.5f, 2);

            var mod = _prestige.GetModifier(StatType.IdleYieldRate);

            Assert.IsFalse(mod.IsNone, "IdleYieldRate modifier must not be None after 2 prestiges.");
            Assert.AreEqual(expectedMult, mod.Multiplicative, 0.001f);
        }

        [Test]
        public void GetModifier_UnrelatedStat_ReturnsNone()
        {
            var mod = _prestige.GetModifier(StatType.CritChance);
            Assert.IsTrue(mod.IsNone, "Unrelated stat must return Modifier.None.");
        }
    }
}
