// Tests for Story CLK-03: Click Loop — ClickTarget HP & Destruction
// Type: Unit (EditMode)
//
// AC-CLK-10: ApplyDamage reduces CurrentHP
// AC-CLK-11: ApplyDamage returns clamped actual damage
// AC-CLK-12: IsAlive = false when HP reaches 0
// AC-CLK-13: OnDestroyed fires once on depletion
// AC-CLK-14: Dead target returns 0 from ApplyDamage
// AC-CLK-15: Registry registers and deregisters correctly

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.ClickLoop;

namespace EndlessEngine.Tests.Unit.ClickLoopSystem
{
    [TestFixture]
    public class ClickTargetTests
    {
        private GameObject          _go;
        private ClickTarget         _target;
        private ClickTargetConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            ClickTargetRegistry.Clear();

            _config = ScriptableObject.CreateInstance<ClickTargetConfigSO>();
            _config.TargetId          = "test_coin";
            _config.MaxHP             = 10f;
            _config.DamagePerClick    = 1f;
            _config.BaseYield         = 10f;
            _config.RespawnSeconds    = 999f;
            _config.ComboContribution = 1f;

            _go = new GameObject("ClickTarget_Test");
            _go.SetActive(false);
            _go.AddComponent<BoxCollider2D>();
            _target = _go.AddComponent<ClickTarget>();
            SetField(_target, "_config", _config);
            _go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_config != null) Object.DestroyImmediate(_config);
            ClickTargetRegistry.Clear();
        }

        [Test]
        [Description("AC-CLK-10: ApplyDamage reduces CurrentHP")]
        public void ApplyDamage_ReducesHP()
        {
            _target.ApplyDamage(3f);
            Assert.AreEqual(7f, _target.CurrentHP, 0.001f);
        }

        [Test]
        [Description("AC-CLK-11: ApplyDamage returns clamped actual damage")]
        public void ApplyDamage_OverDamage_ReturnsClamped()
        {
            float actual = _target.ApplyDamage(999f);
            Assert.AreEqual(_config.MaxHP, actual, 0.001f);
        }

        [Test]
        [Description("AC-CLK-12: IsAlive = false when HP reaches 0")]
        public void ApplyDamage_Lethal_NotAlive()
        {
            _target.ApplyDamage(_config.MaxHP);
            Assert.IsFalse(_target.IsAlive);
        }

        [Test]
        [Description("AC-CLK-13: OnDestroyed fires exactly once")]
        public void ApplyDamage_Kill_FiresOnDestroyedOnce()
        {
            int count = 0;
            _target.OnDestroyed += _ => count++;
            _target.ApplyDamage(_config.MaxHP);
            Assert.AreEqual(1, count);
        }

        [Test]
        [Description("AC-CLK-13: OnDestroyed does not fire for non-lethal damage")]
        public void ApplyDamage_NonLethal_NoEvent()
        {
            int count = 0;
            _target.OnDestroyed += _ => count++;
            _target.ApplyDamage(_config.MaxHP * 0.5f);
            Assert.AreEqual(0, count);
        }

        [Test]
        [Description("AC-CLK-14: Dead target returns 0 from ApplyDamage")]
        public void ApplyDamage_DeadTarget_ReturnsZero()
        {
            _target.ApplyDamage(_config.MaxHP);
            Assert.AreEqual(0f, _target.ApplyDamage(5f), 0.001f);
        }

        [Test]
        [Description("AC-CLK-15: Registry contains target after Awake")]
        public void Registry_ContainsTargetAfterAwake()
        {
            bool found = false;
            foreach (var t in ClickTargetRegistry.All)
                if (t == _target) { found = true; break; }
            Assert.IsTrue(found);
        }

        [Test]
        [Description("AC-CLK-15: Registry removes target after destroy")]
        public void Registry_RemovesTargetAfterDestroy()
        {
            Object.DestroyImmediate(_go);
            _go = null;
            Assert.AreEqual(0, ClickTargetRegistry.All.Count);
        }

        private static void SetField(object t, string name, object val)
        {
            var f = t.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(t, val);
        }
    }
}
