// Tests for Story CLK-01: Click Loop — Yield Resolver
// Type: Unit (EditMode)
//
// AC-CLK-01: Per-click yield = (BaseYield/MaxHP × damage) × yieldMult × comboMult × critMult
// AC-CLK-02: Destruction yield = BaseYield × yieldMult × comboMult × critMult
// AC-CLK-03: ClickYieldMultiplier stat scales yield
// AC-CLK-04: No crit (critMult=1) → same as non-crit formula

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.ClickLoop;

namespace EndlessEngine.Tests.Unit.ClickLoopSystem
{
    [TestFixture]
    public class ClickYieldResolverTests
    {
        private ClickTargetConfigSO _config;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
#endif
            _config = ScriptableObject.CreateInstance<ClickTargetConfigSO>();
            _config.TargetId          = "test_coin";
            _config.MaxHP             = 10f;
            _config.DamagePerClick    = 1f;
            _config.BaseYield         = 10f;
            _config.AwardYieldPerClick = true;
            _config.RespawnSeconds    = 3f;
            _config.ComboContribution = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) Object.DestroyImmediate(_config);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
#endif
        }

        // ── AC-CLK-01 ─────────────────────────────────────────────────────────────

        [Test]
        [Description("AC-CLK-01: Per-click yield = BaseYield/MaxHP × damage with no upgrades")]
        public void ResolveClickYield_NoUpgrades_ReturnsCorrectBase()
        {
            float expected = (_config.BaseYield / _config.MaxHP) * 1f;
            float result   = ClickYieldResolver.ResolveClickYield(_config, damageApplied: 1f, comboMultiplier: 1f);
            Assert.AreEqual(expected, result, 0.001f);
        }

        [Test]
        [Description("AC-CLK-01: Full-damage per-click yield equals BaseYield")]
        public void ResolveClickYield_FullDamage_ReturnsBaseYield()
        {
            float result = ClickYieldResolver.ResolveClickYield(_config, _config.MaxHP, 1f);
            Assert.AreEqual(_config.BaseYield, result, 0.001f);
        }

        [Test]
        [Description("AC-CLK-01: Crit multiplier scales per-click yield")]
        public void ResolveClickYield_WithCrit_ScalesYield()
        {
            float base_ = (_config.BaseYield / _config.MaxHP) * 1f;
            float result = ClickYieldResolver.ResolveClickYield(_config, 1f, 1f, critMultiplier: 3f);
            Assert.AreEqual(base_ * 3f, result, 0.001f);
        }

        // ── AC-CLK-02 ─────────────────────────────────────────────────────────────

        [Test]
        [Description("AC-CLK-02: Destruction yield = BaseYield with no upgrades")]
        public void ResolveDestructionYield_NoUpgrades_ReturnsBaseYield()
        {
            float result = ClickYieldResolver.ResolveDestructionYield(_config, comboMultiplier: 1f);
            Assert.AreEqual(_config.BaseYield, result, 0.001f);
        }

        [Test]
        [Description("AC-CLK-02: Combo scales destruction yield")]
        public void ResolveDestructionYield_WithCombo_ScalesCorrectly()
        {
            float result = ClickYieldResolver.ResolveDestructionYield(_config, comboMultiplier: 2f);
            Assert.AreEqual(_config.BaseYield * 2f, result, 0.001f);
        }

        // ── AC-CLK-03 ─────────────────────────────────────────────────────────────

        [Test]
        [Description("AC-CLK-03: ClickYieldMultiplier +0.5 → yield ×1.5")]
        public void ResolveClickYield_YieldMultiplierStat_ScalesYield()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ApplyUpgradeEffect(
                StatType.ClickYieldMultiplier, 0.5f, EffectType.AdditivePercent);
#endif
            float expected = (_config.BaseYield / _config.MaxHP) * 1f * 1.5f;
            float result   = ClickYieldResolver.ResolveClickYield(_config, 1f, 1f);
            Assert.AreEqual(expected, result, 0.001f);
        }

        // ── AC-CLK-04 ─────────────────────────────────────────────────────────────

        [Test]
        [Description("AC-CLK-04: critMultiplier=1 gives same result as no-crit call")]
        public void ResolveClickYield_CritMultiplierOne_SameAsNoCrit()
        {
            float noCrit   = ClickYieldResolver.ResolveClickYield(_config, 1f, 1f);
            float withCrit = ClickYieldResolver.ResolveClickYield(_config, 1f, 1f, critMultiplier: 1f);
            Assert.AreEqual(noCrit, withCrit, 0.001f);
        }
    }
}
