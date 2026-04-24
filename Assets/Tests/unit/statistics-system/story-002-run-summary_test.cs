// Tests for Sprint 13 — S13-05: RunSummaryData
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Factory method creates correct RunSummaryData
//   - DurationSeconds computed correctly
//   - All fields populated from Create()
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.StatisticsSystem

using System;
using NUnit.Framework;
using EndlessEngine.Statistics;

namespace EndlessEngine.Tests.Unit.StatisticsSystem
{
    [TestFixture]
    public class RunSummaryDataTests
    {
        [Test]
        public void Create_PopulatesAllFields()
        {
            var start = new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc);
            var end   = new DateTime(2026, 4, 23, 10, 2, 30, DateTimeKind.Utc);

            var summary = RunSummaryData.Create(
                startTime:            start,
                endTime:              end,
                goldEarned:           12345L,
                killCount:            88,
                maxWave:              12,
                prestigeCountAtStart: 3,
                prestigePerformed:    true,
                upgradesAccepted:     5,
                cascadeMultiplier:    2.25f,
                finalIncomeRate:      1500f);

            Assert.AreEqual(start,   summary.StartTime);
            Assert.AreEqual(end,     summary.EndTime);
            Assert.AreEqual(12345L,  summary.GoldEarned);
            Assert.AreEqual(88,      summary.KillCount);
            Assert.AreEqual(12,      summary.MaxWave);
            Assert.AreEqual(3,       summary.PrestigeCountAtStart);
            Assert.IsTrue(summary.PrestigePerformed);
            Assert.AreEqual(5,       summary.UpgradesAccepted);
            Assert.AreEqual(2.25f,   summary.CascadeMultiplier, 0.001f);
            Assert.AreEqual(1500f,   summary.FinalIncomeRate, 0.001f);
        }

        [Test]
        public void DurationSeconds_ComputedFromStartAndEnd()
        {
            var start = new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc);
            var end   = new DateTime(2026, 4, 23, 10, 1, 30, DateTimeKind.Utc); // 90 seconds later

            var summary = RunSummaryData.Create(start, end, 0, 0, 1, 0, false, 0, 1f, 0f);

            Assert.AreEqual(90f, summary.DurationSeconds, 0.1f);
        }

        [Test]
        public void DurationSeconds_ZeroWhenSameTimestamp()
        {
            var now = new DateTime(2026, 4, 23, 12, 0, 0, DateTimeKind.Utc);
            var summary = RunSummaryData.Create(now, now, 0, 0, 1, 0, false, 0, 1f, 0f);
            Assert.AreEqual(0f, summary.DurationSeconds, 0.001f);
        }
    }
}
