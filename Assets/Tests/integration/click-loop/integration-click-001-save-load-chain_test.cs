// Integration Tests: Click Loop — Save/Load + Statistics chain
// Type: Integration (EditMode)
//
// INT-CLK-01: OnBeforeSave writes destroyed target state to SaveData.ClickLoopState
// INT-CLK-02: OnAfterLoad restores respawn timer from SaveData
// INT-CLK-03: Gold award increments TotalGoldEarned (persisted)
// INT-CLK-04: TotalTargetsDestroyed increments after a target is destroyed

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.Economy;
using EndlessEngine.ClickLoop;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Statistics;

namespace EndlessEngine.Tests.Integration.ClickLoopSystem
{
    [TestFixture]
    public class ClickLoopSaveLoadChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private GameObject       _servicGo;
        private ClickLoopService _service;

        private GameObject          _targetGo;
        private ClickTarget         _target;
        private ClickTargetConfigSO _targetConfig;
        private ClickLoopConfigSO   _loopConfig;

        private EconomyService    _economy;
        private StatisticsService _statistics;

        [SetUp]
        public void SetUp()
        {
            ClickTargetRegistry.Clear();
            UpgradeApplicationSystem.ResetForTesting();

            var econConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econConfig.ResourceHardCap = 1_000_000L;
            ConfigRegistry.InjectForTesting(economy: econConfig);

            _targetConfig = ScriptableObject.CreateInstance<ClickTargetConfigSO>();
            _targetConfig.TargetId          = "test_coin";
            _targetConfig.MaxHP             = 1f;
            _targetConfig.DamagePerClick    = 1f;
            _targetConfig.BaseYield         = 10f;
            _targetConfig.AwardYieldPerClick = false;
            _targetConfig.RespawnSeconds    = 999f;
            _targetConfig.ComboContribution = 1f;

            _loopConfig = ScriptableObject.CreateInstance<ClickLoopConfigSO>();
            _loopConfig.ComboDecayDelay    = 1.5f;
            _loopConfig.ComboDecayRate     = 8f;
            _loopConfig.MaxComboMultiplier = 8f;
            _loopConfig.ComboPointsPerStep = 5f;
            _loopConfig.BaseCritChance     = 0f; // no crit randomness in tests
            _loopConfig.BaseCritMultiplier = 3f;
            _loopConfig.BaseAutoClickRate  = 0f;
            _loopConfig.OfflineCapHours    = 8f;
            _loopConfig.OfflineEfficiency  = 0.25f;

            var econGo = new GameObject("Economy");
            _economy   = econGo.AddComponent<EconomyService>();
            _economy.Initialize(upgradeTreeQuery: null, saveNotifier: null);

            var statsGo  = new GameObject("Statistics");
            _statistics  = statsGo.AddComponent<StatisticsService>();
            _statistics.Initialize(System.Array.Empty<StatDefinitionSO>());

            _servicGo = new GameObject("ClickLoopService");
            _service  = _servicGo.AddComponent<ClickLoopService>();
            _service.Initialize(_loopConfig, _economy, input: null,
                targetLayer: default, _statistics);

            _targetGo = new GameObject("ClickTarget_Test");
            _targetGo.SetActive(false);
            _targetGo.AddComponent<BoxCollider2D>();
            _target = _targetGo.AddComponent<ClickTarget>();
            SetField(_target, "_config", _targetConfig);
            _targetGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            SafeDestroy(_servicGo);
            SafeDestroy(_targetGo);
            SafeDestroy(GameObject.Find("Economy"));
            SafeDestroy(GameObject.Find("Statistics"));
            Object.DestroyImmediate(_targetConfig);
            Object.DestroyImmediate(_loopConfig);
            ClickTargetRegistry.Clear();
            UpgradeApplicationSystem.ResetForTesting();
        }

        [Test]
        [Description("INT-CLK-01: OnBeforeSave writes respawning target to ClickLoopState")]
        public void OnBeforeSave_DestroyedTarget_WritesRespawnEntry()
        {
            _target.ApplyDamage(_targetConfig.MaxHP);
            Assert.IsTrue(_target.IsRespawning, "Pre-condition: target must be respawning");

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            Assert.AreEqual(1, save.ClickLoopState.TargetStates.Count);
            var entry = System.Linq.Enumerable.First(save.ClickLoopState.TargetStates.Values);
            Assert.IsTrue(entry.IsRespawning);
            Assert.Greater(entry.RespawnSecondsRemaining, 0f);
        }

        [Test]
        [Description("INT-CLK-02: OnAfterLoad restores respawn state from SaveData")]
        public void OnAfterLoad_WithRespawnEntry_RestoresState()
        {
            var save = new SaveData();
            save.EnsureDefaults();
            save.ClickLoopState.TargetStates["test_coin_0"] = new ClickTargetSaveEntry
            {
                IsRespawning = true, RespawnSecondsRemaining = 2f
            };
            _service.OnAfterLoad(save);

            Assert.IsFalse(_target.IsAlive);
            Assert.IsTrue(_target.IsRespawning);
        }

        [Test]
        [Description("INT-CLK-03: SimulateClick on alive target awards gold and persists TotalGoldEarned")]
        public void SimulateClick_AwardsGoldAndPersists()
        {
            _service.SimulateClickOnTarget(_target);

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            Assert.Greater(save.ClickLoopState.TotalGoldEarned, 0L);
        }

        [Test]
        [Description("INT-CLK-04: SimulateClick depleting target increments TotalTargetsDestroyed")]
        public void SimulateClick_KillTarget_IncrementsTotalDestroyed()
        {
            _service.SimulateClickOnTarget(_target); // MaxHP=1, DamagePerClick=1 → killed

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            Assert.AreEqual(1, save.ClickLoopState.TotalTargetsDestroyed);
        }

        private static void SetField(object t, string n, object v)
        {
            var f = t.GetType().GetField(n,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(t, v);
        }

        private static void SafeDestroy(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

#endif
    }
}
