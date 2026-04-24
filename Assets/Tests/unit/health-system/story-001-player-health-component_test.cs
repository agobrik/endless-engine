// Tests for Story S1-08: Health System — PlayerHealthComponent
// Type: Logic (Unit/EditMode)
// Story: production/epics/health-system/story-001-player-health-component.md
//
// These tests verify:
//   (1) AC-HLT-01: Init from config sets MaxHP=CurrentHP=BaseMaxHP; OnPlayerHPChanged fires
//   (2) AC-HLT-02: Damage reduces HP and fires OnPlayerHPChanged
//   (3) AC-HLT-03: Death fires OnEntityDied exactly once; double-damage same frame does not re-fire
//   (4) AC-HLT-04: I-frame flag is readable; damage system blocks hit when IsInvincible=true
//   (5) AC-HLT-07: DeathTransitionDelay elapses → OnPlayerEnteredIdleRecovery fires
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.HealthSystem

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Health;

namespace EndlessEngine.Tests.Unit.HealthSystemTests
{
    /// <summary>
    /// Unit tests for PlayerHealthComponent (S1-08 / Story 001).
    /// </summary>
    [TestFixture]
    public class PlayerHealthComponentTests
    {
        private PlayerHealthComponent _health;
        private PlayerBaseStatConfigSO _playerConfig;

        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _playerConfig = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            _playerConfig.BaseMaxHP                   = 200f;
            _playerConfig.InvincibilityFramesDuration = 1f;
            _playerConfig.DeathTransitionDelaySeconds  = 2f;
            _playerConfig.BaseCritChance               = 0f;
            _playerConfig.BaseCritMultiplier           = 2f;
            ConfigRegistry.InjectForTesting(player: _playerConfig);

            var go = new GameObject("PlayerHealthTest");
            _health = go.AddComponent<PlayerHealthComponent>();
            _health.InitialiseFromConfigForTesting(); // ensure config is applied even if Awake ran before inject
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PlayerHealthComponent.ClearStaticSubscribersForTesting();
            DamageSystem.ClearSubscribersForTesting();
            if (_health != null)
                UnityEngine.Object.DestroyImmediate(_health.gameObject);
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── AC-HLT-01: Init ───────────────────────────────────────────────────────

        [Test]
        [Description("AC-HLT-01: Awake initializes MaxHP and CurrentHP from config.")]
        public void Awake_InitializesHPFromConfig()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual(200f, _health.MaxHP,     0.001f, "MaxHP must equal BaseMaxHP from config");
            Assert.AreEqual(200f, _health.CurrentHP, 0.001f, "CurrentHP must equal MaxHP on init");
#endif
        }

        [Test]
        [Description("AC-HLT-01: Awake fires OnPlayerHPChanged with (MaxHP, MaxHP).")]
        public void Awake_FiresOnPlayerHPChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Subscribe before creating a new component to capture the Awake event
            float capturedCurrent = -1f;
            float capturedMax     = -1f;
            PlayerHealthComponent.OnPlayerHPChanged += (cur, max) =>
            {
                capturedCurrent = cur;
                capturedMax     = max;
            };

            // Create a fresh component to trigger Awake
            var go2 = new GameObject("PlayerHealthInit");
            var hp2 = go2.AddComponent<PlayerHealthComponent>();
            hp2.InitialiseFromConfigForTesting();

            Assert.AreEqual(200f, capturedCurrent, 0.001f, "OnPlayerHPChanged current must be MaxHP on init");
            Assert.AreEqual(200f, capturedMax,     0.001f, "OnPlayerHPChanged max must be MaxHP on init");

            PlayerHealthComponent.ClearStaticSubscribersForTesting();
            UnityEngine.Object.DestroyImmediate(go2);
#endif
        }

        // ── AC-HLT-02: Damage reduces HP ─────────────────────────────────────────

        [Test]
        [Description("AC-HLT-02: OnDamageResolved with matching TargetID reduces CurrentHP.")]
        public void DamageResolved_MatchingTarget_ReducesHP()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int entityId = _health.GetEntityIdForTesting();
            var hit = new DamageHit(AttackerType.Enemy, 50f, false, 50L,
                                    DamageType.Attack, entityId, Vector2.zero);

            // Fire event directly
            FireDamageResolved(hit);

            Assert.AreEqual(150f, _health.CurrentHP, 0.001f,
                "CurrentHP must decrease by FinalDamage when TargetID matches");
#endif
        }

        [Test]
        [Description("AC-HLT-02: OnPlayerHPChanged fires with updated values after damage.")]
        public void DamageResolved_MatchingTarget_FiresOnPlayerHPChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float capturedCurrent = -1f;
            float capturedMax     = -1f;
            PlayerHealthComponent.OnPlayerHPChanged += (cur, max) =>
            {
                capturedCurrent = cur;
                capturedMax     = max;
            };

            int entityId = _health.GetEntityIdForTesting();
            FireDamageResolved(new DamageHit(AttackerType.Enemy, 50f, false, 50L,
                                              DamageType.Attack, entityId, Vector2.zero));

            Assert.AreEqual(150f, capturedCurrent, 0.001f);
            Assert.AreEqual(200f, capturedMax,     0.001f);
#endif
        }

        [Test]
        [Description("AC-HLT-02: Damage with non-matching TargetID does not change HP.")]
        public void DamageResolved_DifferentTarget_HPUnchanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var hit = new DamageHit(AttackerType.Enemy, 50f, false, 50L,
                                    DamageType.Attack, targetId: 9999, Vector2.zero);
            FireDamageResolved(hit);

            Assert.AreEqual(200f, _health.CurrentHP, 0.001f,
                "HP must not change for damage targeting a different entity");
#endif
        }

        // ── AC-HLT-03: Death fires once ───────────────────────────────────────────

        [Test]
        [Description("AC-HLT-03: Overkill damage reduces HP to 0 and fires OnEntityDied.")]
        public void DamageResolved_OverkillDamage_HPClampedToZeroAndDied()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int diedCount = 0;
            PlayerHealthComponent.OnEntityDied += (id, tag, pos) => diedCount++;

            int entityId = _health.GetEntityIdForTesting();
            FireDamageResolved(new DamageHit(AttackerType.Enemy, 500f, false, 500L,
                                              DamageType.Attack, entityId, Vector2.zero));

            Assert.AreEqual(0f, _health.CurrentHP, 0.001f, "HP must clamp to 0 on overkill");
            Assert.AreEqual(1,  diedCount, "OnEntityDied must fire exactly once");
#endif
        }

        [Test]
        [Description("AC-HLT-03: Second damage event after death does not re-fire OnEntityDied.")]
        public void DamageResolved_SecondHitAfterDeath_OnEntityDiedFiresOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int diedCount = 0;
            PlayerHealthComponent.OnEntityDied += (id, tag, pos) => diedCount++;

            int entityId = _health.GetEntityIdForTesting();
            // First hit kills
            FireDamageResolved(new DamageHit(AttackerType.Enemy, 500f, false, 500L,
                                              DamageType.Attack, entityId, Vector2.zero));
            // Second hit same frame
            FireDamageResolved(new DamageHit(AttackerType.Enemy, 100f, false, 100L,
                                              DamageType.Attack, entityId, Vector2.zero));

            Assert.AreEqual(1, diedCount,
                "OnEntityDied must not fire more than once even with multiple death-frame hits");
#endif
        }

        // ── AC-HLT-04: I-frame flag ───────────────────────────────────────────────

        [Test]
        [Description("AC-HLT-04: IsInvincible is false at init (no i-frames active).")]
        public void IsInvincible_InitialState_IsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.IsFalse(_health.IsInvincible,
                "IsInvincible must be false at game start");
#endif
        }

        [Test]
        [Description("AC-HLT-04: IsInvincible is true after a surviving hit.")]
        public void IsInvincible_AfterSurvivingHit_IsTrue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int entityId = _health.GetEntityIdForTesting();
            FireDamageResolved(new DamageHit(AttackerType.Enemy, 10f, false, 10L,
                                              DamageType.Attack, entityId, Vector2.zero));

            Assert.IsTrue(_health.IsInvincible,
                "IsInvincible must be true immediately after a surviving hit");
#endif
        }

        [Test]
        [Description("AC-HLT-04: When IsInvincible=true, DamageSystem routes to OnDamageBlocked.")]
        public void IsInvincible_True_DamageSystemBlocksHit()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _health.SetIframeTimerForTesting(1f); // manually activate i-frames

            bool blockedFired  = false;
            bool resolvedFired = false;
            DamageSystem.OnDamageBlocked  += _ => blockedFired  = true;
            DamageSystem.OnDamageResolved += _ => resolvedFired = true;

            // Caller reads IsInvincible and passes it to ResolveDamage
            DamageSystem.ResolveDamage(50f, AttackerType.Enemy, DamageType.Attack,
                                        _health.GetEntityIdForTesting(), Vector2.zero,
                                        isPlayerInvincible: _health.IsInvincible);

            Assert.IsTrue(blockedFired,   "OnDamageBlocked must fire when player is invincible");
            Assert.IsFalse(resolvedFired, "OnDamageResolved must not fire when player is invincible");
#endif
        }

        // ── AC-HLT-07: Death → Idle Recovery transition ───────────────────────────

        [Test]
        [Description("AC-HLT-07: OnPlayerEnteredIdleRecovery fires after DeathTransitionDelaySeconds.")]
        public void Death_TransitionDelay_FiresIdleRecoveryEvent()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool idleRecoveryFired = false;
            PlayerHealthComponent.OnPlayerEnteredIdleRecovery += () => idleRecoveryFired = true;

            // Kill the player
            int entityId = _health.GetEntityIdForTesting();
            FireDamageResolved(new DamageHit(AttackerType.Enemy, 500f, false, 500L,
                                              DamageType.Attack, entityId, Vector2.zero));

            Assert.IsFalse(idleRecoveryFired, "Idle recovery must not fire immediately on death");

            // Simulate Update with deltaTime = DeathTransitionDelaySeconds (2f)
            _health.SimulateUpdateForTesting(2f);

            Assert.IsTrue(idleRecoveryFired,
                "OnPlayerEnteredIdleRecovery must fire after DeathTransitionDelaySeconds elapses");
#endif
        }

        [Test]
        [Description("AC-HLT-07: Damage after death transition does not affect HP (dead state immune).")]
        public void Death_AfterTransition_DamageIgnored()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int entityId = _health.GetEntityIdForTesting();
            // Kill
            FireDamageResolved(new DamageHit(AttackerType.Enemy, 500f, false, 500L,
                                              DamageType.Attack, entityId, Vector2.zero));
            // Attempt damage while in death state
            FireDamageResolved(new DamageHit(AttackerType.Enemy, 10f, false, 10L,
                                              DamageType.Attack, entityId, Vector2.zero));

            Assert.AreEqual(0f, _health.CurrentHP, 0.001f,
                "HP must remain 0 after additional damage in dead state");
#endif
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires the DamageSystem.OnDamageResolved event directly for testing.
        /// Uses <see cref="DamageSystem.InvokeOnDamageResolvedForTesting"/> so the
        /// exact hit values reach subscribers (PlayerHealthComponent) without the
        /// ResolveDamage crit/i-frame logic interfering.
        /// </summary>
        private static void FireDamageResolved(DamageHit hit)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DamageSystem.InvokeOnDamageResolvedForTesting(hit);
#endif
        }
    }
}
