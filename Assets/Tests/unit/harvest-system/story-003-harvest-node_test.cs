// Tests for Story HAR-03: Harvest System — HarvestNode HP & Depletion
// Type: Unit (EditMode)
//
// Acceptance Criteria:
//   AC-HAR-11: ApplyDamage reduces CurrentHP by the damage amount
//   AC-HAR-12: ApplyDamage returns clamped actual damage (cannot over-damage)
//   AC-HAR-13: Node is not alive (IsAlive=false) when HP reaches 0
//   AC-HAR-14: OnDepleted fires exactly once when HP reaches 0
//   AC-HAR-15: Dead node returns 0 from ApplyDamage and ignores calls
//   AC-HAR-16: HarvestNodeRegistry contains the node after Awake
//   AC-HAR-17: HarvestNodeRegistry removes the node after OnDestroy
//
// NOTE: We create the GameObject inactive so Awake doesn't fire before
//       we can set _config via reflection. Then SetActive(true) triggers Awake.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.HarvestSystem

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Harvest;

namespace EndlessEngine.Tests.Unit.HarvestSystem
{
    [TestFixture]
    public class HarvestNodeTests
    {
        private GameObject          _go;
        private HarvestNode         _node;
        private HarvestNodeConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            HarvestNodeRegistry.Clear();

            _config = ScriptableObject.CreateInstance<HarvestNodeConfigSO>();
            _config.NodeId           = "test_mineral";
            _config.MaxHP            = 10f;
            _config.DamagePerTick    = 1f;
            _config.BaseYield        = 5f;
            _config.RespawnSeconds   = 999f;
            _config.ComboContribution = 1f;

            // Create inactive so Awake defers until SetActive(true)
            _go = new GameObject("HarvestNode_Test");
            _go.SetActive(false);
            _go.AddComponent<BoxCollider2D>();
            _node = _go.AddComponent<HarvestNode>();

            // Inject config before Awake fires
            SetPrivateField(_node, "_config", _config);

            // Now trigger Awake
            _go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
            if (_config != null)
                Object.DestroyImmediate(_config);
            HarvestNodeRegistry.Clear();
        }

        // ── AC-HAR-11: Damage reduces HP ─────────────────────────────────────────

        [Test]
        [Description("AC-HAR-11: ApplyDamage reduces CurrentHP by the damage amount")]
        public void ApplyDamage_ReducesHP()
        {
            float before = _node.CurrentHP;
            _node.ApplyDamage(3f);
            Assert.AreEqual(before - 3f, _node.CurrentHP, 0.001f,
                "CurrentHP must decrease by the damage amount");
        }

        // ── AC-HAR-12: Clamped actual damage ─────────────────────────────────────

        [Test]
        [Description("AC-HAR-12: ApplyDamage returns actual damage clamped to remaining HP")]
        public void ApplyDamage_OverDamage_ReturnsClampedActual()
        {
            float actual = _node.ApplyDamage(999f);
            Assert.AreEqual(_config.MaxHP, actual, 0.001f,
                "ApplyDamage must return at most the remaining HP, not the raw input damage");
        }

        // ── AC-HAR-13: Node dies when HP = 0 ─────────────────────────────────────

        [Test]
        [Description("AC-HAR-13: IsAlive is false after HP reaches 0")]
        public void ApplyDamage_ExactLethal_NodeNotAlive()
        {
            _node.ApplyDamage(_config.MaxHP);
            Assert.IsFalse(_node.IsAlive, "Node must not be alive after HP reaches 0");
        }

        // ── AC-HAR-14: OnDepleted fires once ─────────────────────────────────────

        [Test]
        [Description("AC-HAR-14: OnDepleted fires exactly once when HP reaches 0")]
        public void ApplyDamage_KillNode_FiresOnDepletedOnce()
        {
            int depleteCount = 0;
            _node.OnDepleted += _ => depleteCount++;

            _node.ApplyDamage(_config.MaxHP);

            Assert.AreEqual(1, depleteCount,
                "OnDepleted must fire exactly once when node HP reaches 0");
        }

        [Test]
        [Description("AC-HAR-14: OnDepleted does NOT fire for non-lethal damage")]
        public void ApplyDamage_NonLethal_DoesNotFireOnDepleted()
        {
            int depleteCount = 0;
            _node.OnDepleted += _ => depleteCount++;

            _node.ApplyDamage(_config.MaxHP * 0.5f);

            Assert.AreEqual(0, depleteCount,
                "OnDepleted must not fire for non-lethal damage");
        }

        // ── AC-HAR-15: Dead node ignores damage ──────────────────────────────────

        [Test]
        [Description("AC-HAR-15: ApplyDamage on dead node returns 0")]
        public void ApplyDamage_DeadNode_ReturnsZero()
        {
            _node.ApplyDamage(_config.MaxHP); // kill it
            float damage = _node.ApplyDamage(5f);
            Assert.AreEqual(0f, damage, 0.001f,
                "Dead node must return 0 from ApplyDamage and ignore the call");
        }

        // ── AC-HAR-16: Registry registration ─────────────────────────────────────

        [Test]
        [Description("AC-HAR-16: HarvestNodeRegistry contains the node after Awake")]
        public void Registry_ContainsNodeAfterAwake()
        {
            bool found = false;
            foreach (var n in HarvestNodeRegistry.All)
                if (n == _node) { found = true; break; }

            Assert.IsTrue(found, "HarvestNodeRegistry must contain the node after Awake");
        }

        // ── AC-HAR-17: Registry de-registration ──────────────────────────────────

        [Test]
        [Description("AC-HAR-17: HarvestNodeRegistry removes the node after it is destroyed")]
        public void Registry_RemovesNodeAfterDestroy()
        {
            Object.DestroyImmediate(_go);
            _go = null;

            Assert.AreEqual(0, HarvestNodeRegistry.All.Count,
                "HarvestNodeRegistry must not contain the node after it is destroyed");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
