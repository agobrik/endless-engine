// Integration Tests — Sprint 23 — S23-07
// Test chain: SkillTreeService.TryUnlock → GetAllActiveEffects returns correct effects,
// caller applies via EndlessEngine.Core.UpgradeApplicationSystem.ApplyUpgradeEffect → GetEffectiveStat reflects bonus.
// Save/load preserves unlocked nodes and applied stat changes.
//
// Design note: SkillTreeService returns raw SkillEffect list — the caller
// (Bootstrap or a skill integration adapter) is responsible for mapping
// SkillEffect → EndlessEngine.Core.UpgradeApplicationSystem.ApplyUpgradeEffect. This test
// verifies both halves of the contract.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.FullSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Tests.Integration.FullSystem
{
    [TestFixture]
    public class SkillTreeStatChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private SkillTreeService  _skillTree;
        private SaveService       _saveService;
        private SkillTreeConfigSO _treeConfig;
        private SkillNodeConfigSO _damageNode;

        [SetUp]
        public void SetUp()
        {
            // Player config needed by UpgradeApplicationSystem.GetBaseStat(StatType.Damage)
            var playerConfig = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            ConfigRegistry.InjectForTesting(player: playerConfig);

            // Damage bonus skill node: +50% damage (StatMultiplier)
            _damageNode = ScriptableObject.CreateInstance<SkillNodeConfigSO>();
            _damageNode.NodeId      = "power_boost";
            _damageNode.DisplayName = "Power Boost";
            _damageNode.PointCost   = 1;
            _damageNode.PrerequisiteIds = new System.Collections.Generic.List<string>();
            _damageNode.Effects     = new List<SkillEffect>
            {
                new SkillEffect
                {
                    Type     = SkillEffectType.StatMultiplier,
                    TargetId = "Damage",
                    Value    = 0.5f   // +50% additive multiplier
                }
            };

            _treeConfig = ScriptableObject.CreateInstance<SkillTreeConfigSO>();
            _treeConfig.TreeId      = "combat";
            _treeConfig.DisplayName = "Combat Tree";
            _treeConfig.Nodes       = new[] { _damageNode };

            var savGo    = new GameObject("Save");
            _saveService = savGo.AddComponent<SaveService>();

            var stGo     = new GameObject("SkillTree");
            _skillTree   = stGo.AddComponent<SkillTreeService>();
            _skillTree.Initialize(new[] { _treeConfig }, startingPoints: 3);

            var sd = new SaveData();
            sd.EnsureDefaults();
            sd.SkillPoints = 3; // preserve startingPoints after OnAfterLoad overwrites _skillPoints
            _skillTree.OnAfterLoad(sd);

            EndlessEngine.Core.UpgradeApplicationSystem.ResetForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            ConfigRegistry.ClearForTesting();
            if (_skillTree   != null) Object.DestroyImmediate(_skillTree.gameObject);
            if (_saveService != null) Object.DestroyImmediate(_saveService.gameObject);
            if (_treeConfig  != null) Object.DestroyImmediate(_treeConfig);
            if (_damageNode  != null) Object.DestroyImmediate(_damageNode);

            SkillTreeService.ClearSubscribersForTesting();
            EndlessEngine.Core.UpgradeApplicationSystem.ResetForTesting();
        }

        [Test]
        public void TryUnlock_ThenGetActiveEffects_ReturnsSkillEffect()
        {
            bool unlocked = _skillTree.TryUnlock("combat", "power_boost");
            Assert.IsTrue(unlocked, "Unlock should succeed with sufficient points");

            var effects = _skillTree.GetAllActiveEffects();
            Assert.AreEqual(1, effects.Count, "One effect from power_boost");
            Assert.AreEqual(SkillEffectType.StatMultiplier, effects[0].Type);
            Assert.AreEqual("Damage", effects[0].TargetId);
            Assert.AreEqual(0.5f, effects[0].Value, 0.001f);
        }

        [Test]
        public void UnlockedEffect_AppliedToUpgradeSystem_IncreasesEffectiveStat()
        {
            // Baseline
            float baseDamage = EndlessEngine.Core.UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            _skillTree.TryUnlock("combat", "power_boost");

            // Caller maps SkillEffect → UpgradeApplicationSystem (as Bootstrap would)
            var effects = _skillTree.GetAllActiveEffects();
            foreach (var fx in effects)
            {
                if (System.Enum.TryParse<StatType>(fx.TargetId, out var stat))
                {
                    EndlessEngine.Core.UpgradeApplicationSystem.ApplyUpgradeEffect(
                        stat,
                        fx.Value,
                        fx.Type == SkillEffectType.StatMultiplier
                            ? EndlessEngine.Core.EffectType.AdditivePercent
                            : EndlessEngine.Core.EffectType.AdditiveFlat);
                }
            }

            float boostedDamage = EndlessEngine.Core.UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);
            Assert.Greater(boostedDamage, baseDamage,
                "Damage stat should increase after applying power_boost effect");
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesUnlockedNodes()
        {
            _skillTree.TryUnlock("combat", "power_boost");
            Assert.IsTrue(_skillTree.IsUnlocked("combat", "power_boost"));

            // Save
            var sd = new SaveData();
            sd.EnsureDefaults();
            _skillTree.OnBeforeSave(sd);

            // Fresh instance
            var st2Go = new GameObject("SkillTree2");
            var st2   = st2Go.AddComponent<SkillTreeService>();
            st2.Initialize(new[] { _treeConfig }, startingPoints: 0);
            st2.OnAfterLoad(sd);

            Assert.IsTrue(st2.IsUnlocked("combat", "power_boost"),
                "Unlocked node must persist after save/load");

            Object.DestroyImmediate(st2Go);
        }

#endif
    }
}
