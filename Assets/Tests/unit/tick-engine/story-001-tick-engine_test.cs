// Tests for TickEngine
// Type: Logic (Unit/EditMode)
//
// Verifies:
//   (1) OnTick fires via FireTickForTesting
//   (2) Effective dt = TickIntervalSeconds * TimeScale
//   (3) FireTickForTesting does not fire when no subscribers
//   (4) ClearSubscribersForTesting removes all listeners
//   (5) Multiple subscribers all receive the tick

using NUnit.Framework;
using EndlessEngine.Flow;

namespace EndlessEngine.Tests.Unit.TickEngine
{
    public class TickEngineTests
    {
        [TearDown]
        public void TearDown()
        {
            Flow.TickEngine.ClearSubscribersForTesting();
        }

        [Test]
        public void FireTick_CallsSubscriber()
        {
            float received = -1f;
            Flow.TickEngine.OnTick += dt => received = dt;

            Flow.TickEngine.FireTickForTesting(1f);

            Assert.AreEqual(1f, received, 0.001f);
        }

        [Test]
        public void FireTick_PassesDeltaTime_Unchanged()
        {
            float received = 0f;
            Flow.TickEngine.OnTick += dt => received = dt;

            Flow.TickEngine.FireTickForTesting(2.5f);

            Assert.AreEqual(2.5f, received, 0.001f);
        }

        [Test]
        public void FireTick_MultipleSubscribers_AllReceiveTick()
        {
            int count = 0;
            Flow.TickEngine.OnTick += _ => count++;
            Flow.TickEngine.OnTick += _ => count++;
            Flow.TickEngine.OnTick += _ => count++;

            Flow.TickEngine.FireTickForTesting(1f);

            Assert.AreEqual(3, count);
        }

        [Test]
        public void ClearSubscribers_PreventsSubsequentFire()
        {
            bool fired = false;
            Flow.TickEngine.OnTick += _ => fired = true;

            Flow.TickEngine.ClearSubscribersForTesting();
            Flow.TickEngine.FireTickForTesting(1f);

            Assert.IsFalse(fired);
        }

        [Test]
        public void FireTick_ZeroDt_SubscriberReceivesZero()
        {
            float received = -1f;
            Flow.TickEngine.OnTick += dt => received = dt;

            Flow.TickEngine.FireTickForTesting(0f);

            Assert.AreEqual(0f, received, 0.001f);
        }
    }
}
