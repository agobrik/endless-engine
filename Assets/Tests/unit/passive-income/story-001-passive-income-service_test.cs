// Tests for PassiveIncomeService
// Type: Logic (Unit/EditMode)
//
// Verifies:
//   (1) No generators → no income on tick
//   (2) 1 generator (1/sec), tick dt=1 → 1 gold added
//   (3) In-run modifier halves income (ActiveRunPassiveModifier=0.5)
//   (4) TotalPassiveEarned accumulates across ticks
//   (5) Zero-yield tick does not crash

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.PassiveIncome
{
    public class PassiveIncomeServiceTests
    {
        private EndlessEngine.Generator.GeneratorSystem _generators;
        private EconomyService _economy;
        private PassiveIncomeService _passive;
        private GameFlowStateMachine _gameFlow;
        private GeneratorConfigSO _cfg;

        [SetUp]
        public void SetUp()
        {
            // ConfigRegistry
            var econConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econConfig.ResourceHardCap = 1_000_000L;
            econConfig.StartingGold    = 0L;

            var runConfig = ScriptableObject.CreateInstance<RunConfigSO>();
            runConfig.ActiveRunPassiveModifier = 0.5f;

            ConfigRegistry.InjectForTesting(economy: econConfig, run: runConfig);

            // Economy
            var econGO = new GameObject("Economy");
            _economy = econGO.AddComponent<EconomyService>();
            _economy.Initialize(null, null);
            // SchemaVersion=1 prevents new-game branch; CurrentResources=0 for clean slate
            _economy.OnAfterLoad(new SaveData { CurrentResources = 0L, SchemaVersion = 1 });

            // Generator config: 1 gold/sec
            _cfg = ScriptableObject.CreateInstance<GeneratorConfigSO>();
            _cfg.GeneratorId        = "passive_gen";
            _cfg.BaseYieldPerSecond = 1f;
            _cfg.BaseCost           = 10;
            _cfg.CostScalingFactor  = 1f;
            _cfg.MaxCount           = -1;

            // GeneratorSystem
            var sysGO = new GameObject("GeneratorSystem");
            _generators = sysGO.AddComponent<EndlessEngine.Generator.GeneratorSystem>();
            _generators.Initialize(new GeneratorConfigSO[] { _cfg }, _economy, null);

            // GameFlowStateMachine
            var flowGO = new GameObject("GameFlow");
            _gameFlow = flowGO.AddComponent<GameFlowStateMachine>();

            // PassiveIncomeService
            var passiveGO = new GameObject("PassiveIncome");
            _passive = passiveGO.AddComponent<PassiveIncomeService>();
            _passive.Initialize(_generators, _economy, _gameFlow);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _passive.SubscribeForTesting();
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_passive != null) _passive.UnsubscribeForTesting();
#endif
            ConfigRegistry.ClearForTesting();
            Flow.TickEngine.ClearSubscribersForTesting();
            EndlessEngine.Generator.GeneratorSystem.ClearSubscribersForTesting();
            GameFlowStateMachine.ClearSubscribersForTesting();

            Object.DestroyImmediate(_economy.gameObject);
            Object.DestroyImmediate(_generators.gameObject);
            Object.DestroyImmediate(_gameFlow.gameObject);
            Object.DestroyImmediate(_passive.gameObject);
            Object.DestroyImmediate(_cfg);
        }

        [Test]
        public void NoGenerators_TickProducesNoIncome()
        {
            // No generators purchased → yield = 0
            Flow.TickEngine.FireTickForTesting(1f);
            Assert.AreEqual(0L, _economy.CurrentResources);
        }

        [Test]
        public void OneGenerator_TickDt1_Adds1Gold()
        {
            _generators.SetCountForTesting("passive_gen", 1); // 1 gen × 1/sec = 1/sec
            Flow.TickEngine.FireTickForTesting(1f);
            Assert.AreEqual(1L, _economy.CurrentResources);
        }

        [Test]
        public void TwoGenerators_TickDt1_Adds2Gold()
        {
            _generators.SetCountForTesting("passive_gen", 2);
            Flow.TickEngine.FireTickForTesting(1f);
            Assert.AreEqual(2L, _economy.CurrentResources);
        }

        [Test]
        public void InRun_PassiveModifier_HalvesIncome()
        {
            _generators.SetCountForTesting("passive_gen", 2); // 2/sec
            _gameFlow.StartRun(); // GameFlow → InRun (ActiveRunPassiveModifier = 0.5)
            Flow.TickEngine.FireTickForTesting(1f);
            // Expected: (long)(2 * 0.5 * 1) = 1
            Assert.AreEqual(1L, _economy.CurrentResources);
        }

        [Test]
        public void TotalPassiveEarned_AccumulatesAcrossTicks()
        {
            _generators.SetCountForTesting("passive_gen", 1);
            Flow.TickEngine.FireTickForTesting(1f);
            Flow.TickEngine.FireTickForTesting(1f);
            Flow.TickEngine.FireTickForTesting(1f);
            Assert.AreEqual(3.0, _passive.TotalPassiveEarned.ToDouble(), 1e-9);
        }

        [Test]
        public void ZeroYieldTick_DoesNotCrash()
        {
            // No generators, dt=0 — should be a no-op
            Assert.DoesNotThrow(() => Flow.TickEngine.FireTickForTesting(0f));
        }
    }
}
