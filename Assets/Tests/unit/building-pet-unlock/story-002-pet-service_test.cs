// Tests for Sprint 17 — S17-02: PetService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - TryEquip: success, fires OnPetEquipped
//   - TryEquip: PetNotFound fails
//   - Unequip: clears equipped, fires OnPetUnequipped
//   - TryLevelUp: increments level, deducts cost, fires OnPetLeveledUp
//   - TryLevelUp: AlreadyMaxLevel fails
//   - TryLevelUp: InsufficientFunds fails
//   - TryEvolve: evolves to new form, carries over level, fires OnPetEvolved
//   - TryEvolve: NoEvolution fails for max-tier pets
//   - TryEvolve: LevelRequirementNotMet fails
//   - GetActiveEffects: returns base effects for equipped pet at level 1
//   - GetActiveEffects: returns bonus effects at higher levels
//   - GetActiveEffects: returns empty when no pet equipped
//   - Save/Load round-trip
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.BuildingPetUnlock

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Pet;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.BuildingPetUnlock
{
    [TestFixture]
    public class PetServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private PetService     _service;
        private EconomyService _economy;
        private PetConfigSO    _foxPet;
        private PetConfigSO    _foxEvolvedPet;

        private readonly List<PetConfigSO>                    _equippedEvents   = new List<PetConfigSO>();
        private readonly List<int>                            _unequippedEvents = new List<int>(); // count
        private readonly List<(PetConfigSO, int)>             _leveledEvents    = new List<(PetConfigSO, int)>();
        private readonly List<(PetConfigSO, PetConfigSO)>     _evolvedEvents    = new List<(PetConfigSO, PetConfigSO)>();
        private readonly List<string>                          _failedEvents     = new List<string>();
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            PetService.ClearSubscribersForTesting();
            PetService.OnPetEquipped   += p => _equippedEvents.Add(p);
            PetService.OnPetUnequipped += () => _unequippedEvents.Add(1);
            PetService.OnPetLeveledUp  += (p, l) => _leveledEvents.Add((p, l));
            PetService.OnPetEvolved    += (f, t) => _evolvedEvents.Add((f, t));
            PetService.OnActionFailed  += r => _failedEvents.Add(r);
            _equippedEvents.Clear(); _unequippedEvents.Clear(); _leveledEvents.Clear();
            _evolvedEvents.Clear(); _failedEvents.Clear();

            // Economy
            var ecoGo = new GameObject("Economy");
            _economy  = ecoGo.AddComponent<EconomyService>();
            _economy.Initialize(null, new GameObject("Save").AddComponent<SaveService>());
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 500;
            _economy.OnAfterLoad(sd);

            // Pets
            _foxPet = ScriptableObject.CreateInstance<PetConfigSO>();
            _foxPet.PetId         = "fox";
            _foxPet.MaxLevel      = 3;
            _foxPet.LevelUpCosts  = new long[] { 100, 200 };
            _foxPet.EvolveAtLevel = 3;
            _foxPet.EvolvesToPetId = "fox_elder";
            _foxPet.EvolutionCost  = 300;
            _foxPet.BaseEffects = new List<SkillEffect>
            {
                new SkillEffect { Type = SkillEffectType.StatMultiplier, TargetId ="income_rate", Value =1.1f }
            };
            _foxPet.LevelBonuses = new List<PetLevelBonus>
            {
                new PetLevelBonus { AdditionalEffects = new List<SkillEffect>
                    { new SkillEffect { Type = SkillEffectType.StatMultiplier, TargetId ="income_rate", Value =0.05f } } },
                new PetLevelBonus { AdditionalEffects = new List<SkillEffect>
                    { new SkillEffect { Type = SkillEffectType.StatMultiplier, TargetId ="income_rate", Value =0.05f } } },
            };

            _foxEvolvedPet = ScriptableObject.CreateInstance<PetConfigSO>();
            _foxEvolvedPet.PetId         = "fox_elder";
            _foxEvolvedPet.MaxLevel      = 5;
            _foxEvolvedPet.LevelUpCosts  = new long[] { 300, 400, 500, 600 };
            _foxEvolvedPet.EvolvesToPetId = string.Empty;

            var go   = new GameObject("PetService");
            _service = go.AddComponent<PetService>();
            _service.Initialize(new[] { _foxPet, _foxEvolvedPet }, _economy);
        }

        [TearDown]
        public void TearDown()
        {
            PetService.ClearSubscribersForTesting();
            if (_service       != null) Object.DestroyImmediate(_service.gameObject);
            if (_economy       != null) Object.DestroyImmediate(_economy.gameObject);
            if (_foxPet        != null) Object.DestroyImmediate(_foxPet);
            if (_foxEvolvedPet != null) Object.DestroyImmediate(_foxEvolvedPet);
            if (_econConfig    != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        // ── TryEquip ─────────────────────────────────────────────────────────────

        [Test]
        public void TryEquip_Success_FiresEvent()
        {
            bool result = _service.TryEquip("fox");
            Assert.IsTrue(result);
            Assert.IsTrue(_service.IsEquipped("fox"));
            Assert.AreEqual(1, _equippedEvents.Count);
            Assert.AreEqual(_foxPet, _equippedEvents[0]);
        }

        [Test]
        public void TryEquip_PetNotFound_Fails()
        {
            bool result = _service.TryEquip("dragon");
            Assert.IsFalse(result);
            Assert.AreEqual(1, _failedEvents.Count);
            Assert.AreEqual("PetNotFound", _failedEvents[0]);
        }

        // ── Unequip ───────────────────────────────────────────────────────────────

        [Test]
        public void Unequip_ClearsEquipped_FiresEvent()
        {
            _service.TryEquip("fox");
            _service.Unequip();
            Assert.IsFalse(_service.IsEquipped("fox"));
            Assert.AreEqual(1, _unequippedEvents.Count);
        }

        // ── TryLevelUp ────────────────────────────────────────────────────────────

        [Test]
        public void TryLevelUp_IncrementsLevel_DeductsCost()
        {
            bool result = _service.TryLevelUp("fox");
            Assert.IsTrue(result);
            Assert.AreEqual(2, _service.GetLevel("fox"));
            Assert.AreEqual(400, _economy.CurrentResources, "500 - 100 = 400");
            Assert.AreEqual(1, _leveledEvents.Count);
            Assert.AreEqual(2, _leveledEvents[0].Item2);
        }

        [Test]
        public void TryLevelUp_AlreadyMaxLevel_Fails()
        {
            _service.TryLevelUp("fox"); // 1→2
            _service.TryLevelUp("fox"); // 2→3
            bool result = _service.TryLevelUp("fox"); // already max
            Assert.IsFalse(result);
            Assert.AreEqual("AlreadyMaxLevel", _failedEvents[0]);
        }

        [Test]
        public void TryLevelUp_InsufficientFunds_Fails()
        {
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 50;
            _economy.OnAfterLoad(sd);
            bool result = _service.TryLevelUp("fox"); // costs 100
            Assert.IsFalse(result);
            Assert.AreEqual("InsufficientFunds", _failedEvents[0]);
        }

        // ── TryEvolve ─────────────────────────────────────────────────────────────

        [Test]
        public void TryEvolve_EvolvesToNewForm()
        {
            // Level up to max (3) then evolve
            _service.TryLevelUp("fox"); // 1→2 (cost 100)
            _service.TryLevelUp("fox"); // 2→3 (cost 200)
            // Resources left: 500 - 100 - 200 = 200; evolution costs 300 → need more
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 1000;
            _economy.OnAfterLoad(sd);

            bool result = _service.TryEvolve("fox");
            Assert.IsTrue(result);
            Assert.AreEqual(1, _evolvedEvents.Count);
            Assert.AreEqual(_foxPet, _evolvedEvents[0].Item1);
            Assert.AreEqual(_foxEvolvedPet, _evolvedEvents[0].Item2);
            Assert.AreEqual(3, _service.GetLevel("fox_elder"), "Level carried over");
        }

        [Test]
        public void TryEvolve_NoEvolution_Fails()
        {
            bool result = _service.TryEvolve("fox_elder"); // no EvolvesToPetId
            Assert.IsFalse(result);
            Assert.AreEqual("NoEvolution", _failedEvents[0]);
        }

        [Test]
        public void TryEvolve_LevelRequirementNotMet_Fails()
        {
            bool result = _service.TryEvolve("fox"); // level 1, requires 3
            Assert.IsFalse(result);
            Assert.AreEqual("LevelRequirementNotMet", _failedEvents[0]);
        }

        // ── GetActiveEffects ──────────────────────────────────────────────────────

        [Test]
        public void GetActiveEffects_ReturnsBaseEffectsAtLevel1()
        {
            _service.TryEquip("fox");
            var effects = _service.GetActiveEffects();
            Assert.AreEqual(1, effects.Count, "Only base effect at level 1");
        }

        [Test]
        public void GetActiveEffects_IncludesBonusEffectsAtLevel2()
        {
            _service.TryEquip("fox");
            _service.TryLevelUp("fox"); // → level 2
            var effects = _service.GetActiveEffects();
            Assert.AreEqual(2, effects.Count, "Base + 1 level bonus");
        }

        [Test]
        public void GetActiveEffects_EmptyWhenNoPetEquipped()
        {
            var effects = _service.GetActiveEffects();
            Assert.AreEqual(0, effects.Count);
        }

        // ── Save/Load round-trip ──────────────────────────────────────────────────

        [Test]
        public void SaveLoad_RoundTrip_RestoresLevelAndEquipped()
        {
            _service.TryEquip("fox");
            _service.TryLevelUp("fox");

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _service.OnBeforeSave(saveData);

            var go2      = new GameObject("PetService2");
            var service2 = go2.AddComponent<PetService>();
            service2.Initialize(new[] { _foxPet, _foxEvolvedPet }, _economy);
            service2.OnAfterLoad(saveData);

            Assert.AreEqual(2, service2.GetLevel("fox"));
            Assert.IsTrue(service2.IsEquipped("fox"));

            Object.DestroyImmediate(go2);
        }

#endif
    }
}
