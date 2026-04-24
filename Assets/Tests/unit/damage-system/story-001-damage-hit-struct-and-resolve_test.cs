// Tests for Story S1-07: Damage System — DamageHit Struct, ResolveDamage, and Event Bus
// Type: Logic (Unit/EditMode)
// Story: production/epics/damage-system/story-001-damage-hit-struct-and-resolve.md
//
// These tests verify:
//   (1) AC-DMG-01: Normal player hit with no crit → FinalDamage=baseDamage, IsCrit=false
//   (2) AC-DMG-02: CritChance=1.0, CritMultiplier=2.0 → FinalDamage=200, IsCrit=true
//   (3) AC-DMG-03: rawDamage=0 → FinalDamage=1 (minimum floor), warning logged
//   (4) AC-DMG-06: Player i-frames active + enemy attack → OnDamageBlocked, not OnDamageResolved
//   (5) AC-DMG-07: AttackerType=Enemy → IsCrit=false always
//   (6) Struct verification: DamageHit is a value type
//   (7) FinalDamage is long type
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.DamageSystem

using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using EndlessEngine.Config;
using EndlessEngine.Damage;

namespace EndlessEngine.Tests.Unit.DamageSystemTests
{
    /// <summary>
    /// Unit tests for DamageSystem.ResolveDamage and DamageHit struct (S1-07 / Story 001).
    /// </summary>
    [TestFixture]
    public class DamageHitAndResolveTests
    {
        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var player = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            player.BaseCritChance     = 0f;   // default: no crit
            player.BaseCritMultiplier = 2f;
            ConfigRegistry.InjectForTesting(player: player);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DamageSystem.ClearSubscribersForTesting();
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── AC-DMG-01: Normal player hit ──────────────────────────────────────────

        [Test]
        [Description("AC-DMG-01: Normal player hit with CritChance=0 fires OnDamageResolved with FinalDamage=100, IsCrit=false.")]
        public void ResolveDamage_NoCrit_FinalDamageEqualBase()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DamageHit? received = null;
            DamageSystem.OnDamageResolved += hit => received = hit;

            DamageSystem.ResolveDamage(
                rawDamage:          100f,
                attacker:           AttackerType.Player,
                damageType:         DamageType.Attack,
                targetId:           1,
                hitPos:             Vector2.zero,
                isPlayerInvincible: false);

            Assert.IsNotNull(received, "OnDamageResolved must fire for a normal hit");
            Assert.AreEqual(100L, received.Value.FinalDamage,
                "FinalDamage must equal base damage when no crit");
            Assert.IsFalse(received.Value.IsCrit,
                "IsCrit must be false when CritChance=0");
#endif
        }

        [Test]
        [Description("AC-DMG-01: OnDamageResolved fires exactly once per ResolveDamage call.")]
        public void ResolveDamage_NormalHit_EventFiresExactlyOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int fireCount = 0;
            DamageSystem.OnDamageResolved += _ => fireCount++;

            DamageSystem.ResolveDamage(50f, AttackerType.Player, DamageType.Attack,
                                        1, Vector2.zero, false);

            Assert.AreEqual(1, fireCount, "OnDamageResolved must fire exactly once per call");
#endif
        }

        // ── AC-DMG-02: Always-crit ────────────────────────────────────────────────

        [Test]
        [Description("AC-DMG-02: CritChance=1.0 and CritMultiplier=2.0 yields FinalDamage=200, IsCrit=true.")]
        public void ResolveDamage_AlwaysCrit_FinalDamageDoubled()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var player = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            player.BaseCritChance     = 1f; // always crit
            player.BaseCritMultiplier = 2f;
            ConfigRegistry.InjectForTesting(player: player);

            DamageHit? received = null;
            DamageSystem.OnDamageResolved += hit => received = hit;

            DamageSystem.ResolveDamage(100f, AttackerType.Player, DamageType.Attack,
                                        1, Vector2.zero, false);

            Assert.IsNotNull(received);
            Assert.AreEqual(200L, received.Value.FinalDamage,
                "FinalDamage must be rawDamage * CritMultiplier when always-crit");
            Assert.IsTrue(received.Value.IsCrit,
                "IsCrit must be true when CritChance=1.0");
#endif
        }

        // ── AC-DMG-03: Minimum damage floor ──────────────────────────────────────

        [Test]
        [Description("AC-DMG-03: rawDamage=0 yields FinalDamage=1 (minimum floor).")]
        public void ResolveDamage_ZeroRawDamage_FinalDamageIsOne()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DamageHit? received = null;
            DamageSystem.OnDamageResolved += hit => received = hit;

            LogAssert.Expect(LogType.Warning, new Regex("zero/negative rawDamage"));

            DamageSystem.ResolveDamage(0f, AttackerType.Player, DamageType.Attack,
                                        1, Vector2.zero, false);

            Assert.IsNotNull(received);
            Assert.AreEqual(1L, received.Value.FinalDamage,
                "FinalDamage must be at least 1 even with rawDamage=0");
#endif
        }

        [Test]
        [Description("AC-DMG-03: Negative rawDamage also floors to 1 and logs a warning.")]
        public void ResolveDamage_NegativeRawDamage_FinalDamageIsOne()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DamageHit? received = null;
            DamageSystem.OnDamageResolved += hit => received = hit;

            LogAssert.Expect(LogType.Warning, new Regex("zero/negative rawDamage"));

            DamageSystem.ResolveDamage(-50f, AttackerType.Player, DamageType.Attack,
                                        1, Vector2.zero, false);

            Assert.IsNotNull(received);
            Assert.AreEqual(1L, received.Value.FinalDamage,
                "FinalDamage must be 1 for negative rawDamage");
#endif
        }

        // ── AC-DMG-06: I-frames block enemy attacks ───────────────────────────────

        [Test]
        [Description("AC-DMG-06: Enemy attack during player i-frames fires OnDamageBlocked, not OnDamageResolved.")]
        public void ResolveDamage_EnemyDuringInvincibility_FiresOnDamageBlocked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool resolvedFired = false;
            bool blockedFired  = false;
            DamageSystem.OnDamageResolved += _ => resolvedFired = true;
            DamageSystem.OnDamageBlocked  += _ => blockedFired  = true;

            DamageSystem.ResolveDamage(50f, AttackerType.Enemy, DamageType.Attack,
                                        1, Vector2.zero, isPlayerInvincible: true);

            Assert.IsTrue(blockedFired,   "OnDamageBlocked must fire when player is invincible");
            Assert.IsFalse(resolvedFired, "OnDamageResolved must NOT fire during i-frames");
#endif
        }

        [Test]
        [Description("AC-DMG-06: Player attack ignores i-frame flag (i-frames only apply to enemy attackers).")]
        public void ResolveDamage_PlayerDuringInvincibility_StillResolvesNormally()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool resolvedFired = false;
            DamageSystem.OnDamageResolved += _ => resolvedFired = true;

            // Player-to-player scenario (shouldn't happen in gameplay but tests guard correctness)
            DamageSystem.ResolveDamage(50f, AttackerType.Player, DamageType.Attack,
                                        1, Vector2.zero, isPlayerInvincible: true);

            Assert.IsTrue(resolvedFired,
                "OnDamageResolved must still fire for player-sourced attacks regardless of i-frame flag");
#endif
        }

        // ── AC-DMG-07: Enemy never crits ─────────────────────────────────────────

        [Test]
        [Description("AC-DMG-07: Enemy attacker IsCrit is always false, even if CritChance=1.0 in config.")]
        public void ResolveDamage_EnemyAttacker_IsCritAlwaysFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Even with CritChance=1.0 configured, enemies must not crit
            var player = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            player.BaseCritChance     = 1f;
            player.BaseCritMultiplier = 2f;
            ConfigRegistry.InjectForTesting(player: player);

            DamageHit? received = null;
            DamageSystem.OnDamageResolved += hit => received = hit;

            DamageSystem.ResolveDamage(100f, AttackerType.Enemy, DamageType.Attack,
                                        1, Vector2.zero, isPlayerInvincible: false);

            Assert.IsNotNull(received);
            Assert.IsFalse(received.Value.IsCrit,
                "Enemy attacks must never set IsCrit=true, regardless of CritChance config");
            Assert.AreEqual(100L, received.Value.FinalDamage,
                "Enemy FinalDamage must not be multiplied by crit multiplier");
#endif
        }

        // ── Struct and type verification ──────────────────────────────────────────

        [Test]
        [Description("DamageHit is a struct (value type), not a class.")]
        public void DamageHit_IsValueType()
        {
            Assert.IsTrue(typeof(DamageHit).IsValueType,
                "DamageHit must be a value type (struct) for zero-allocation event passing");
        }

        [Test]
        [Description("DamageHit.FinalDamage is of type long.")]
        public void DamageHit_FinalDamage_IsLong()
        {
            var field = typeof(DamageHit).GetField("FinalDamage");
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(long), field.FieldType,
                "FinalDamage must be of type long for large damage numbers");
        }

        [Test]
        [Description("DamageHit fields are populated correctly by constructor.")]
        public void DamageHit_Constructor_SetsAllFields()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var pos = new Vector2(3f, 4f);
            var hit = new DamageHit(AttackerType.Player, 50f, true, 100L,
                                     DamageType.Attack, 42, pos);

            Assert.AreEqual(AttackerType.Player, hit.AttackerType);
            Assert.AreEqual(50f,              hit.BaseDamage,   0.001f);
            Assert.IsTrue(hit.IsCrit);
            Assert.AreEqual(100L,             hit.FinalDamage);
            Assert.AreEqual(DamageType.Attack, hit.DamageType);
            Assert.AreEqual(42,               hit.TargetID);
            Assert.AreEqual(pos,              hit.HitPosition);
#endif
        }
    }
}
