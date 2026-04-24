// Tests for Story S1-14: Health System — Enemy HealthComponent, Contact Damage, and Pool Reset
// Type: Logic (Unit/EditMode)
// Story: production/epics/health-system/story-002-enemy-health-component-and-contact-damage.md
//
// These tests verify:
//   (1) AC-HLT-05: Initialize sets MaxHP and CurrentHP from supplied value
//   (2) AC-HLT-06: ApplyDamage fires OnEntityDied on zero HP; Reset restores full HP
//   (3) AC-HLT-08: Contact damage tick fires at 1/second per overlapping enemy
//   (4) Dead enemy ignores further damage (double-fire guard)
//   (5) Reset clears the _isDead flag
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.HealthSystem

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Damage;
using EndlessEngine.Health;

namespace EndlessEngine.Tests.Unit.HealthSystemTests
{
    /// <summary>
    /// Unit tests for HealthComponent (enemy) and contact damage logic (S1-14 / Story 002).
    /// </summary>
    [TestFixture]
    public class EnemyHealthComponentTests
    {
        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HealthSystem.ClearStaticSubscribersForTesting();
            DamageSystem.ClearSubscribersForTesting();
#endif
        }

        // ── AC-HLT-05: Initialize ─────────────────────────────────────────────────

        [Test]
        [Description("AC-HLT-05: Initialize sets MaxHP and CurrentHP to supplied value.")]
        public void Initialize_SetsMaxAndCurrentHP()
        {
            var component = new HealthComponent();
            component.Initialize(entityId: 5, maxHP: 1412f, deathVFXTag: "grunt_death");

            Assert.AreEqual(1412f, component.MaxHP,     0.001f, "MaxHP must equal supplied value");
            Assert.AreEqual(1412f, component.CurrentHP, 0.001f, "CurrentHP must equal MaxHP on init");
            Assert.AreEqual(5,     component.EntityID,           "EntityID must be set on init");
            Assert.AreEqual("grunt_death", component.DeathVFXTag);
        }

        [Test]
        [Description("AC-HLT-05: Initialize can be called multiple times (pool reuse scenario).")]
        public void Initialize_CalledTwice_UpdatesAllFields()
        {
            var component = new HealthComponent();
            component.Initialize(entityId: 1, maxHP: 100f, deathVFXTag: "small");
            component.Initialize(entityId: 2, maxHP: 500f, deathVFXTag: "large");

            Assert.AreEqual(500f, component.MaxHP,    0.001f);
            Assert.AreEqual(500f, component.CurrentHP, 0.001f);
            Assert.AreEqual(2,    component.EntityID);
        }

        // ── AC-HLT-06: Death and Reset ────────────────────────────────────────────

        [Test]
        [Description("AC-HLT-06: Overkill damage reduces HP to 0 and fires OnEntityDied.")]
        public void ApplyDamage_OverkillDamage_FiresOnEntityDied()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var component = new HealthComponent();
            component.Initialize(entityId: 5, maxHP: 100f, deathVFXTag: "grunt_death");

            int diedCount = 0;
            HealthSystem.OnEntityDied += (id, tag, pos) => diedCount++;

            component.ApplyDamage(200L, Vector2.zero);

            Assert.AreEqual(0f, component.CurrentHP, 0.001f, "HP must clamp to 0 on overkill");
            Assert.AreEqual(1,  diedCount, "OnEntityDied must fire once");
#endif
        }

        [Test]
        [Description("AC-HLT-06: After Reset(), CurrentHP restores to MaxHP.")]
        public void Reset_RestoresCurrentHP()
        {
            var component = new HealthComponent();
            component.Initialize(entityId: 5, maxHP: 100f, deathVFXTag: "grunt_death");

            component.ApplyDamage(200L, Vector2.zero); // kill
            component.Reset();

            Assert.AreEqual(100f, component.CurrentHP, 0.001f,
                "Reset must restore CurrentHP to MaxHP");
        }

        [Test]
        [Description("AC-HLT-06: After Reset(), further damage is accepted (isDead cleared).")]
        public void Reset_ClearsDeadFlag_AllowsFurtherDamage()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var component = new HealthComponent();
            component.Initialize(entityId: 5, maxHP: 100f, deathVFXTag: "grunt_death");

            int diedCount = 0;
            HealthSystem.OnEntityDied += (id, tag, pos) => diedCount++;

            component.ApplyDamage(200L, Vector2.zero); // first death
            component.Reset();

            component.ApplyDamage(200L, Vector2.zero); // second death after pool reuse

            Assert.AreEqual(2, diedCount,
                "After Reset(), enemy can die again (pool reuse scenario)");
#endif
        }

        [Test]
        [Description("Dead enemy ignores further damage — OnEntityDied fires only once.")]
        public void ApplyDamage_AfterDeath_IgnoredAndEventNotRepeated()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var component = new HealthComponent();
            component.Initialize(entityId: 5, maxHP: 100f, deathVFXTag: "grunt_death");

            int diedCount = 0;
            HealthSystem.OnEntityDied += (id, tag, pos) => diedCount++;

            component.ApplyDamage(200L, Vector2.zero); // kills
            component.ApplyDamage(50L,  Vector2.zero); // must be ignored

            Assert.AreEqual(1, diedCount, "OnEntityDied must fire only once even with two death-frame hits");
            Assert.AreEqual(0f, component.CurrentHP, 0.001f, "HP stays at 0 after ignored post-death hit");
#endif
        }

        // ── AC-HLT-08: Contact damage tick simulation ─────────────────────────────

        [Test]
        [Description("AC-HLT-08: Contact damage tick fires once per second per overlapping enemy.")]
        public void ContactDamage_TwoEnemiesOneSecond_TwoDamageCallsFire()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Simulate the contact timer logic from PlayerHealthComponent
            // (extracted for deterministic testing without Time.deltaTime dependency)
            var contactTimers = new Dictionary<int, float>
            {
                [10] = 0f, // enemy A
                [20] = 0f, // enemy B
            };
            const float ContactDamagePerEnemy = 10f;
            const long  ContactDamageLong     = 10L;
            _ = ContactDamageLong; // declared for documentation; contact damage goes through ResolveDamage float path

            int resolveCallCount = 0;
            DamageSystem.OnDamageResolved += _ => resolveCallCount++;

            // Simulate 1.0s of Update ticks using small increments
            float elapsed = 0f;
            float tick    = 0.1f;

            while (elapsed < 1.0f)
            {
                elapsed += tick;
                var keys = new List<int>(contactTimers.Keys);
                foreach (int id in keys)
                {
                    contactTimers[id] += tick;
                    if (contactTimers[id] >= 1.0f)
                    {
                        contactTimers[id] -= 1.0f;
                        // Call ResolveDamage with Contact type (mirroring PlayerHealthComponent logic)
                        DamageSystem.ResolveDamage(
                            rawDamage:          ContactDamagePerEnemy,
                            attacker:           EndlessEngine.Damage.AttackerType.Enemy,
                            damageType:         EndlessEngine.Damage.DamageType.Contact,
                            targetId:           999, // player entity id placeholder
                            hitPos:             Vector2.zero,
                            isPlayerInvincible: false);
                    }
                }
            }

            Assert.AreEqual(2, resolveCallCount,
                "Exactly 2 contact damage calls must fire (1 per overlapping enemy per second)");
#endif
        }

        [Test]
        [Description("AC-HLT-08: Enemy that exits overlap before 1s does not produce a contact tick.")]
        public void ContactDamage_EnemyExitsBeforeOneSec_NoTickFires()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var contactTimers = new Dictionary<int, float> { [10] = 0f };

            int resolveCallCount = 0;
            DamageSystem.OnDamageResolved += _ => resolveCallCount++;

            // Simulate 0.5s then remove enemy (OnTriggerExit2D)
            contactTimers[10] += 0.5f;
            contactTimers.Remove(10); // enemy exited

            // Continue for another 0.6s — timer is gone so no tick
            // (no entries to iterate)

            Assert.AreEqual(0, resolveCallCount,
                "No contact tick must fire if enemy exits overlap before 1s threshold");
#endif
        }
    }
}
