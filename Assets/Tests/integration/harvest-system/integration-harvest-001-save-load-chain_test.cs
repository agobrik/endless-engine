// Integration Tests: Harvest System — Save/Load + Statistics chain
// Type: Integration (EditMode)
//
// Covers:
//   INT-HAR-01: OnBeforeSave writes depleted node state to SaveData.HarvestState
//   INT-HAR-02: OnAfterLoad restores depleted node respawn timer from SaveData
//   INT-HAR-03: Yield award increments TotalGoldEarned (persisted via OnBeforeSave)
//   INT-HAR-04: Statistics counters (total gold, total nodes) update after harvest tick
//   INT-HAR-05: BestComboMultiplier peak is tracked and persisted
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.HarvestSystem

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.Economy;
using EndlessEngine.Harvest;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Statistics;

namespace EndlessEngine.Tests.Integration.HarvestSystem
{
    [TestFixture]
    public class HarvestSaveLoadChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        // ── Scene objects ─────────────────────────────────────────────────────────

        private GameObject          _managerGo;
        private HarvestLoopService  _loopService;

        private GameObject          _cursorGo;
        private HarvestCursor       _cursor;

        private GameObject          _nodeGo;
        private HarvestNode         _node;

        // ── Config ────────────────────────────────────────────────────────────────

        private HarvestNodeConfigSO  _nodeConfig;
        private HarvestAreaConfigSO  _areaConfig;

        // ── Services ──────────────────────────────────────────────────────────────

        private EconomyService   _economy;
        private StatisticsService _statistics;

        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            HarvestNodeRegistry.Clear();
            UpgradeApplicationSystem.ResetForTesting();

            // Economy config
            var econConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econConfig.ResourceHardCap = 1_000_000L;
            ConfigRegistry.InjectForTesting(economy: econConfig);

            // Configs
            _nodeConfig = ScriptableObject.CreateInstance<HarvestNodeConfigSO>();
            _nodeConfig.NodeId           = "test_tree";
            _nodeConfig.MaxHP            = 2f;
            _nodeConfig.DamagePerTick    = 2f;   // kills in one tick
            _nodeConfig.BaseYield        = 10f;
            _nodeConfig.AwardYieldPerTick = true;
            _nodeConfig.RespawnSeconds   = 5f;
            _nodeConfig.ComboContribution = 1f;

            _areaConfig = ScriptableObject.CreateInstance<HarvestAreaConfigSO>();
            _areaConfig.BaseRadius                   = 99f; // huge — always overlapping
            _areaConfig.BaseTickInterval             = 0.25f;
            _areaConfig.ComboDecayDelay              = 2f;
            _areaConfig.ComboDecayRate               = 5f;
            _areaConfig.MaxComboMultiplier           = 5f;
            _areaConfig.ComboPointsPerMultiplierStep = 10f;
            _areaConfig.OfflineCapHours              = 8f;
            _areaConfig.OfflineEfficiency            = 0.3f;

            // Economy service
            var econGo = new GameObject("Economy");
            _economy   = econGo.AddComponent<EconomyService>();
            _economy.Initialize(upgradeTreeQuery: null, saveNotifier: null);

            // Statistics service (no StatDefinitionSOs — calls silently ignored for unknown IDs)
            var statsGo  = new GameObject("Statistics");
            _statistics  = statsGo.AddComponent<StatisticsService>();
            _statistics.Initialize(System.Array.Empty<StatDefinitionSO>());

            // HarvestCursor stub — we use a MockHarvestCursor (inner class below)
            _cursorGo = new GameObject("HarvestCursor");
            _cursor   = _cursorGo.AddComponent<MockHarvestCursor>();
            ((MockHarvestCursor)_cursor).SetAreaConfig(_areaConfig);

            // HarvestLoopService
            _managerGo   = new GameObject("HarvestManager");
            _loopService = _managerGo.AddComponent<HarvestLoopService>();
            _loopService.Initialize(_cursor, _areaConfig, _economy, _statistics);

            // HarvestNode (created inactive so we can inject config before Awake)
            _nodeGo = new GameObject("HarvestNode_Test");
            _nodeGo.SetActive(false);
            _nodeGo.AddComponent<BoxCollider2D>();
            _node = _nodeGo.AddComponent<HarvestNode>();
            SetPrivateField(_node, "_config", _nodeConfig);
            _nodeGo.SetActive(true); // Awake fires here
        }

        [TearDown]
        public void TearDown()
        {
            SafeDestroy(_managerGo);
            SafeDestroy(_cursorGo);
            SafeDestroy(_nodeGo);
            SafeDestroy(GameObject.Find("Economy"));
            SafeDestroy(GameObject.Find("Statistics"));

            Object.DestroyImmediate(_nodeConfig);
            Object.DestroyImmediate(_areaConfig);
            HarvestNodeRegistry.Clear();
            UpgradeApplicationSystem.ResetForTesting();
        }

        // ── INT-HAR-01: OnBeforeSave persists depleted state ──────────────────────

        [Test]
        [Description("INT-HAR-01: OnBeforeSave writes depleted node to HarvestState.NodeStates")]
        public void OnBeforeSave_DepletedNode_WritesRespawnEntry()
        {
            // Deplete the node manually
            _node.ApplyDamage(_nodeConfig.MaxHP);
            Assert.IsFalse(_node.IsAlive, "Pre-condition: node must be depleted");
            Assert.IsTrue(_node.IsRespawning, "Pre-condition: node must be respawning");

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _loopService.OnBeforeSave(saveData);

            Assert.AreEqual(1, saveData.HarvestState.NodeStates.Count,
                "One depleted node must produce one NodeStates entry");

            var entry = System.Linq.Enumerable.First(saveData.HarvestState.NodeStates.Values);
            Assert.IsTrue(entry.IsRespawning, "Entry must mark node as respawning");
            Assert.Greater(entry.RespawnSecondsRemaining, 0f,
                "Remaining respawn seconds must be > 0");
        }

        // ── INT-HAR-02: OnAfterLoad restores respawn timer ────────────────────────

        [Test]
        [Description("INT-HAR-02: OnAfterLoad restores node respawn state from SaveData")]
        public void OnAfterLoad_WithRespawnEntry_RestoresNodeState()
        {
            var saveData = new SaveData();
            saveData.EnsureDefaults();

            // Manually write an entry as if saved mid-respawn (3s remaining)
            saveData.HarvestState.NodeStates["test_tree_0"] = new HarvestNodeSaveEntry
            {
                IsRespawning            = true,
                RespawnSecondsRemaining = 3f,
            };

            _loopService.OnAfterLoad(saveData);

            Assert.IsFalse(_node.IsAlive, "Node must be dead/respawning after load");
            Assert.IsTrue(_node.IsRespawning, "Node must be in respawning state");
        }

        // ── INT-HAR-03: TotalGoldEarned accumulates and is persisted ─────────────

        [Test]
        [Description("INT-HAR-03: Yield ticks increment TotalGoldEarned, which is written to SaveData")]
        public void OnBeforeSave_AfterTick_TotalGoldEarnedPersisted()
        {
            // Register cursor overlap
            ((MockHarvestCursor)_cursor).AddOverlapping(_node);

            // Fire a tick directly
            _loopService.TickForTesting();

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _loopService.OnBeforeSave(saveData);

            Assert.Greater(saveData.HarvestState.TotalGoldEarned, 0L,
                "TotalGoldEarned must be > 0 after at least one yield-awarding tick");
        }

        // ── INT-HAR-04: Statistics counters update ────────────────────────────────

        [Test]
        [Description("INT-HAR-04: TotalNodesHarvested increments in SaveData after a node is depleted")]
        public void OnBeforeSave_AfterNodeDepletion_TotalNodesHarvestedIncremented()
        {
            ((MockHarvestCursor)_cursor).AddOverlapping(_node);

            // Tick once — DamagePerTick == MaxHP so node is depleted in one tick
            _loopService.TickForTesting();

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _loopService.OnBeforeSave(saveData);

            Assert.AreEqual(1, saveData.HarvestState.TotalNodesHarvested,
                "TotalNodesHarvested must be 1 after one node is depleted");
        }

        // ── INT-HAR-05: BestComboMultiplier peak ──────────────────────────────────

        [Test]
        [Description("INT-HAR-05: BestComboMultiplier is updated and persisted when combo rises")]
        public void OnBeforeSave_AfterComboRise_BestComboMultiplierPersisted()
        {
            // Give the combo tracker some points directly via a tick
            ((MockHarvestCursor)_cursor).AddOverlapping(_node);
            _loopService.TickForTesting();

            // Check save
            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _loopService.OnBeforeSave(saveData);

            Assert.GreaterOrEqual(saveData.HarvestState.BestComboMultiplier, 1f,
                "BestComboMultiplier must be at least 1 after any tick");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void SetPrivateField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(target, value);
        }

        private static void SafeDestroy(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        // ── Mock HarvestCursor ────────────────────────────────────────────────────

        /// <summary>
        /// Minimal HarvestCursor subclass for tests — lets us inject overlapping nodes
        /// without needing Physics2D.OverlapCircle to work in EditMode.
        /// </summary>
        private class MockHarvestCursor : HarvestCursor
        {
            private readonly System.Collections.Generic.List<IHarvestNode> _mock
                = new System.Collections.Generic.List<IHarvestNode>();

            public void AddOverlapping(IHarvestNode node) => _mock.Add(node);
            public void ClearOverlapping()               => _mock.Clear();

            public override System.Collections.Generic.IReadOnlyList<IHarvestNode> OverlappingNodes => _mock;
            public override float EffectiveRadius => 99f;

            public void SetAreaConfig(HarvestAreaConfigSO config)
            {
                SetPrivateField(this, "_config", config);
            }

            private static void SetPrivateField(object t, string n, object v)
            {
                var f = t.GetType().GetField(n,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                f?.SetValue(t, v);
            }
        }

#endif
    }
}
