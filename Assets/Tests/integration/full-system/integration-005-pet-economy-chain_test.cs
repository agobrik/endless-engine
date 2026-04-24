// Integration Tests — Sprint 22 — S22-03
// Test chain: PetService.TryEquip → GetActiveEffects → passive bonuses available,
// evolution carries level, save/load preserves equipped state.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.FullSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Pet;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Integration.FullSystem
{
    [TestFixture]
    public class PetEconomyChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private PetService     _petService;
        private EconomyService _economy;
        private SaveService    _saveService;
        private PetConfigSO    _petConfig;
        private PetConfigSO    _evolvedConfig;

        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            var ecoGo    = new GameObject("Economy");
            _economy     = ecoGo.AddComponent<EconomyService>();
            var savGo    = new GameObject("Save");
            _saveService = savGo.AddComponent<SaveService>();
            _economy.Initialize(null, _saveService);

            // Base pet
            _petConfig = ScriptableObject.CreateInstance<PetConfigSO>();
            _petConfig.PetId      = "slime";
            _petConfig.DisplayName = "Slime";
            _petConfig.MaxLevel   = 3;
            _petConfig.LevelUpCosts  = new long[] { 50, 100 };
            _petConfig.BaseEffects   = new List<SkillEffect>
            {
                new SkillEffect { Type = SkillEffectType.StatMultiplier, TargetId = "damage_bonus", Value = 5f }
            };
            _petConfig.LevelBonuses  = new List<PetLevelBonus>();
            _petConfig.EvolveAtLevel = 3;
            _petConfig.EvolvesToPetId = "king_slime";
            _petConfig.EvolutionCost  = 200;

            // Evolved pet
            _evolvedConfig = ScriptableObject.CreateInstance<PetConfigSO>();
            _evolvedConfig.PetId      = "king_slime";
            _evolvedConfig.DisplayName = "King Slime";
            _evolvedConfig.MaxLevel   = 5;
            _evolvedConfig.LevelUpCosts  = new long[] { 100, 150, 200, 250 };
            _evolvedConfig.BaseEffects   = new List<SkillEffect>
            {
                new SkillEffect { Type = SkillEffectType.StatMultiplier, TargetId = "damage_bonus", Value = 15f }
            };
            _evolvedConfig.LevelBonuses  = new List<PetLevelBonus>();
            _evolvedConfig.EvolveAtLevel = 0;

            var petGo    = new GameObject("PetService");
            _petService  = petGo.AddComponent<PetService>();
            _petService.Initialize(new[] { _petConfig, _evolvedConfig }, _economy);

            var sd = new SaveData();
            sd.EnsureDefaults();
            _economy.OnAfterLoad(sd);
            _petService.OnAfterLoad(sd);
        }

        [TearDown]
        public void TearDown()
        {
            if (_petService    != null) Object.DestroyImmediate(_petService.gameObject);
            if (_economy       != null) Object.DestroyImmediate(_economy.gameObject);
            if (_saveService   != null) Object.DestroyImmediate(_saveService.gameObject);
            if (_petConfig     != null) Object.DestroyImmediate(_petConfig);
            if (_evolvedConfig != null) Object.DestroyImmediate(_evolvedConfig);
            if (_econConfig    != null) Object.DestroyImmediate(_econConfig);
            PetService.ClearSubscribersForTesting();
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        [Test]
        public void Equip_ThenGetActiveEffects_ReturnsBaseEffects()
        {
            _petService.TryEquip("slime");
            Assert.IsTrue(_petService.IsEquipped("slime"));

            var effects = _petService.GetActiveEffects();
            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual("damage_bonus", effects[0].TargetId);
            Assert.AreEqual(5f, effects[0].Value, 0.001f);
        }

        [Test]
        public void LevelUp_ConsumesGold_AndIncreasesLevel()
        {
            _economy.AddResources(200);
            _petService.TryEquip("slime");

            bool leveled = _petService.TryLevelUp("slime");
            Assert.IsTrue(leveled);
            Assert.AreEqual(2, _petService.GetLevel("slime"));
            Assert.AreEqual(150, _economy.CurrentResources, "50 gold consumed for level up");
        }

        [Test]
        public void Evolve_CarriesLevel_ToEvolvedForm()
        {
            _economy.AddResources(1000);
            // Level up to max (level 3)
            _petService.TryEquip("slime");
            _petService.TryLevelUp("slime"); // 1→2
            _petService.TryLevelUp("slime"); // 2→3

            Assert.AreEqual(3, _petService.GetLevel("slime"));

            bool evolved = _petService.TryEvolve("slime");
            Assert.IsTrue(evolved, "Evolve should succeed at level 3");
            Assert.AreEqual(3, _petService.GetLevel("king_slime"), "Level carries over");
            Assert.IsTrue(_petService.IsEquipped("king_slime"), "Equipped pet swaps to evolved form");
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesEquippedPet()
        {
            _petService.TryEquip("slime");
            Assert.IsTrue(_petService.IsEquipped("slime"));

            var sd = new SaveData();
            sd.EnsureDefaults();
            _petService.OnBeforeSave(sd);

            var pet2Go  = new GameObject("PetService2");
            var pet2    = pet2Go.AddComponent<PetService>();
            var eco2Go  = new GameObject("Economy2");
            var eco2    = eco2Go.AddComponent<EconomyService>();
            var sav2Go  = new GameObject("Save2");
            var sav2    = sav2Go.AddComponent<SaveService>();
            eco2.Initialize(null, sav2);
            pet2.Initialize(new[] { _petConfig, _evolvedConfig }, eco2);
            pet2.OnAfterLoad(sd);

            Assert.IsTrue(pet2.IsEquipped("slime"), "Equipped pet persisted after save/load");

            Object.DestroyImmediate(pet2Go);
            Object.DestroyImmediate(eco2Go);
            Object.DestroyImmediate(sav2Go);
        }

#endif
    }
}
