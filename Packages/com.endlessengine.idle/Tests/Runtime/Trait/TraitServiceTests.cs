using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Stats;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Trait;
using StatType = EndlessEngine.Config.StatType;

namespace EndlessEngine.Tests.Trait
{
    [TestFixture]
    public class TraitServiceTests
    {
        private TraitService         _service;
        private PrestigeStateManager _prestige;
        private SaveService          _saveService;

        [SetUp]
        public void SetUp()
        {
            TraitService.ClearSubscribersForTesting();
            PrestigeStateManager.ClearStaticEventsForTesting();

            var go = new GameObject("TraitService");
            _service = go.AddComponent<TraitService>();

            var prestGo = new GameObject("PrestigeStateManager");
            _prestige = prestGo.AddComponent<PrestigeStateManager>();

            var saveGo = new GameObject("SaveService");
            _saveService = saveGo.AddComponent<SaveService>();
            _saveService.InjectForTesting(new SaveData());
        }

        [TearDown]
        public void TearDown()
        {
            TraitService.ClearSubscribersForTesting();
            PrestigeStateManager.ClearStaticEventsForTesting();
            _saveService.ResetForTesting();
            Object.DestroyImmediate(_service.gameObject);
            Object.DestroyImmediate(_prestige.gameObject);
            Object.DestroyImmediate(_saveService.gameObject);
        }

        private TraitConfigSO MakeTrait(string id, int tier = 0, int prestigeRequired = 0, List<string> exclusiveWith = null)
        {
            var t = ScriptableObject.CreateInstance<TraitConfigSO>();
            t.TraitId          = id;
            t.DisplayName      = id;
            t.Tier             = tier;
            t.PrestigeRequired = prestigeRequired;
            t.ExclusiveWith    = exclusiveWith ?? new List<string>();
            t.Effects          = new List<SkillEffect>();
            return t;
        }

        private TraitConfigSO MakeTraitWithEffect(string id, StatType stat, SkillEffectType type, float value)
        {
            var t = MakeTrait(id);
            t.Effects = new List<SkillEffect>
            {
                new SkillEffect
                {
                    TargetId = stat.ToString(),
                    Type     = type,
                    Value    = value
                }
            };
            return t;
        }

        // ── ChooseTrait ───────────────────────────────────────────────────────────

        [Test]
        public void ChooseTrait_WithPendingSelection_AddsToChosenIds()
        {
            var t = MakeTrait("swift");
            _service.Initialize(new[] { t }, _prestige, _saveService);

            // Simulate prestige firing to create pending selection
            PrestigeStateManager.FirePrestigeCompleteForTesting(1, 1.5f);

            bool result = _service.ChooseTrait("swift");

            Assert.IsTrue(result);
            Assert.IsTrue(_service.IsChosen("swift"));
        }

        [Test]
        public void ChooseTrait_WithoutPendingSelection_ReturnsFalse()
        {
            var t = MakeTrait("swift");
            _service.Initialize(new[] { t }, _prestige, _saveService);

            bool result = _service.ChooseTrait("swift");

            Assert.IsFalse(result, "ChooseTrait must fail when there is no pending selection.");
        }

        [Test]
        public void ChooseTrait_UnknownId_ReturnsFalse()
        {
            _service.Initialize(System.Array.Empty<TraitConfigSO>(), _prestige, _saveService);
            PrestigeStateManager.FirePrestigeCompleteForTesting(1, 1.5f);

            bool result = _service.ChooseTrait("nonexistent");

            Assert.IsFalse(result);
        }

        [Test]
        public void ChooseTrait_ClearsPendingSelection()
        {
            var t = MakeTrait("swift");
            _service.Initialize(new[] { t }, _prestige, _saveService);
            PrestigeStateManager.FirePrestigeCompleteForTesting(1, 1.5f);

            _service.ChooseTrait("swift");

            Assert.IsFalse(_service.HasPendingSelection, "Pending selection must be cleared after a choice.");
        }

        [Test]
        public void ChooseTrait_FiresOnTraitChosen()
        {
            var t = MakeTrait("swift");
            _service.Initialize(new[] { t }, _prestige, _saveService);
            PrestigeStateManager.FirePrestigeCompleteForTesting(1, 1.5f);

            TraitConfigSO chosen = null;
            TraitService.OnTraitChosen += c => chosen = c;

            _service.ChooseTrait("swift");

            Assert.IsNotNull(chosen);
            Assert.AreEqual("swift", chosen.TraitId);
        }

        // ── Exclusivity ───────────────────────────────────────────────────────────

        [Test]
        public void ChooseTrait_ExclusiveWithChosen_ReturnsFalse()
        {
            var fire = MakeTrait("fire", exclusiveWith: new List<string> { "ice" });
            var ice  = MakeTrait("ice",  exclusiveWith: new List<string> { "fire" });
            _service.Initialize(new[] { fire, ice }, _prestige, _saveService);

            _service.ForceChooseForTesting("fire");

            PrestigeStateManager.FirePrestigeCompleteForTesting(2, 2.25f);

            bool result = _service.ChooseTrait("ice");
            Assert.IsFalse(result, "Cannot choose trait that is exclusive with an already-chosen trait.");
        }

        [Test]
        public void ExclusiveTrait_NotInSelectionPool_WhenConflictAlreadyChosen()
        {
            var fire    = MakeTrait("fire",    exclusiveWith: new List<string> { "ice" });
            var ice     = MakeTrait("ice",     exclusiveWith: new List<string> { "fire" });
            var neutral = MakeTrait("neutral");
            _service.Initialize(new[] { fire, ice, neutral }, _prestige, _saveService);
            _service.ForceChooseForTesting("fire");

            TraitConfigSO[] pool = null;
            TraitService.OnTraitSelectionAvailable += p => pool = p;

            PrestigeStateManager.FirePrestigeCompleteForTesting(2, 2.25f);

            Assert.IsNotNull(pool);
            foreach (var t in pool)
                Assert.AreNotEqual("ice", t.TraitId, "ice must not appear in pool when fire is chosen.");
        }

        // ── PrestigeRequired gate ─────────────────────────────────────────────────

        [Test]
        public void Trait_RequiringHigherPrestige_NotInPool()
        {
            var basic    = MakeTrait("basic",    prestigeRequired: 1);
            var advanced = MakeTrait("advanced", prestigeRequired: 5);
            _service.Initialize(new[] { basic, advanced }, _prestige, _saveService);

            TraitConfigSO[] pool = null;
            TraitService.OnTraitSelectionAvailable += p => pool = p;

            PrestigeStateManager.FirePrestigeCompleteForTesting(1, 1.5f);

            Assert.IsNotNull(pool);
            foreach (var t in pool)
                Assert.AreNotEqual("advanced", t.TraitId, "advanced (prestige≥5) must not appear at prestige 1.");
        }

        [Test]
        public void AlreadyChosen_Trait_NotInPool()
        {
            var t = MakeTrait("swift");
            _service.Initialize(new[] { t }, _prestige, _saveService);
            _service.ForceChooseForTesting("swift");

            TraitConfigSO[] pool = null;
            TraitService.OnTraitSelectionAvailable += p => pool = p;

            PrestigeStateManager.FirePrestigeCompleteForTesting(2, 2.25f);

            if (pool != null)
                foreach (var item in pool)
                    Assert.AreNotEqual("swift", item.TraitId, "Already chosen trait must not appear in pool again.");
        }

        // ── Pool size ─────────────────────────────────────────────────────────────

        [Test]
        public void SelectionPool_ContainsAtMostThreeTraits()
        {
            var traits = new TraitConfigSO[10];
            for (int i = 0; i < 10; i++)
                traits[i] = MakeTrait($"trait_{i}");
            _service.Initialize(traits, _prestige, _saveService);

            TraitConfigSO[] pool = null;
            TraitService.OnTraitSelectionAvailable += p => pool = p;

            PrestigeStateManager.FirePrestigeCompleteForTesting(1, 1.5f);

            Assert.IsNotNull(pool);
            Assert.LessOrEqual(pool.Length, 3, "Selection pool must have at most 3 traits.");
        }

        [Test]
        public void SelectionPool_Empty_WhenNoEligibleTraits()
        {
            var advanced = MakeTrait("advanced", prestigeRequired: 99);
            _service.Initialize(new[] { advanced }, _prestige, _saveService);

            bool eventFired = false;
            TraitService.OnTraitSelectionAvailable += _ => eventFired = true;

            PrestigeStateManager.FirePrestigeCompleteForTesting(1, 1.5f);

            Assert.IsFalse(eventFired, "OnTraitSelectionAvailable must not fire when pool is empty.");
        }

        // ── IModifierSource ───────────────────────────────────────────────────────

        [Test]
        public void GetModifier_MultiplierEffect_AggregatesAcrossChosenTraits()
        {
            var t1 = MakeTraitWithEffect("t1", StatType.IdleYieldRate, SkillEffectType.StatMultiplier, 1.5f);
            var t2 = MakeTraitWithEffect("t2", StatType.IdleYieldRate, SkillEffectType.StatMultiplier, 2.0f);
            _service.Initialize(new[] { t1, t2 }, _prestige, _saveService);
            _service.ForceChooseForTesting("t1");
            _service.ForceChooseForTesting("t2");

            var mod = _service.GetModifier(StatType.IdleYieldRate);

            Assert.AreEqual(1.5f * 2.0f, mod.Multiplicative, 0.001f);
        }

        [Test]
        public void GetModifier_AdditiveEffect_Accumulates()
        {
            var t1 = MakeTraitWithEffect("t1", StatType.IdleYieldRate, SkillEffectType.StatAdditive, 10f);
            var t2 = MakeTraitWithEffect("t2", StatType.IdleYieldRate, SkillEffectType.StatAdditive, 20f);
            _service.Initialize(new[] { t1, t2 }, _prestige, _saveService);
            _service.ForceChooseForTesting("t1");
            _service.ForceChooseForTesting("t2");

            var mod = _service.GetModifier(StatType.IdleYieldRate);

            Assert.AreEqual(30.0, mod.Additive, 0.001f);
        }

        [Test]
        public void GetModifier_NoChosenTraits_ReturnsNone()
        {
            _service.Initialize(System.Array.Empty<TraitConfigSO>(), _prestige, _saveService);

            var mod = _service.GetModifier(StatType.IdleYieldRate);

            Assert.IsTrue(mod.IsNone, "No chosen traits must return Modifier.None.");
        }

        [Test]
        public void GetModifier_UnrelatedStat_ReturnsNone()
        {
            var t = MakeTraitWithEffect("t1", StatType.IdleYieldRate, SkillEffectType.StatMultiplier, 2.0f);
            _service.Initialize(new[] { t }, _prestige, _saveService);
            _service.ForceChooseForTesting("t1");

            var mod = _service.GetModifier(StatType.CritChance);

            Assert.IsTrue(mod.IsNone, "Unrelated stat must return Modifier.None.");
        }

        // ── Save / Load ───────────────────────────────────────────────────────────

        [Test]
        public void OnBeforeSave_WritesChosenTraitsWithPrefix()
        {
            var t = MakeTrait("swift");
            _service.Initialize(new[] { t }, _prestige, _saveService);
            _service.ForceChooseForTesting("swift");

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            Assert.IsTrue(save.CompletedMilestones.Contains("trait:swift"),
                "Chosen trait must be written with 'trait:' prefix.");
        }

        [Test]
        public void OnAfterLoad_RestoresChosenTraits()
        {
            var t = MakeTrait("swift");
            _service.Initialize(new[] { t }, _prestige, _saveService);

            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("trait:swift");
            _service.OnAfterLoad(save);

            Assert.IsTrue(_service.IsChosen("swift"), "Trait must be restored from save.");
        }

        [Test]
        public void OnAfterLoad_IgnoresNonTraitEntries()
        {
            _service.Initialize(System.Array.Empty<TraitConfigSO>(), _prestige, _saveService);

            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("quest:daily_1");
            save.CompletedMilestones.Add("bare_milestone");
            _service.OnAfterLoad(save);

            Assert.IsFalse(_service.IsChosen("daily_1"),     "Non-trait entries must be ignored.");
            Assert.IsFalse(_service.IsChosen("bare_milestone"), "Bare milestones must not be loaded as traits.");
        }

        [Test]
        public void SaveLoadRoundtrip_PreservesAllChoices()
        {
            var t1 = MakeTrait("swift");
            var t2 = MakeTrait("tough");
            _service.Initialize(new[] { t1, t2 }, _prestige, _saveService);
            _service.ForceChooseForTesting("swift");
            _service.ForceChooseForTesting("tough");

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            // New service instance simulates load
            var go2       = new GameObject("TraitService2");
            var service2  = go2.AddComponent<TraitService>();
            TraitService.ClearSubscribersForTesting();
            service2.Initialize(new[] { t1, t2 }, _prestige, null);
            service2.OnAfterLoad(save);

            Assert.IsTrue(service2.IsChosen("swift"), "swift must survive save/load roundtrip.");
            Assert.IsTrue(service2.IsChosen("tough"), "tough must survive save/load roundtrip.");

            Object.DestroyImmediate(go2);
        }
    }
}
