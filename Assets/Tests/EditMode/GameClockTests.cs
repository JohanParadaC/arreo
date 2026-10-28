using System;
using System.Collections.Generic;
using Arreo.Core;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class GameClockTests
    {
        // 240 real seconds per game day = 10 real seconds per game hour.
        private const float SecondsPerDay = 240f;
        private const float Tolerance = 0.0001f;

        [Test]
        public void NewClock_DefaultsToDayOneAtMidnight()
        {
            var clock = new GameClock(SecondsPerDay);

            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.Hour, Is.EqualTo(0f));
        }

        [Test]
        public void NewClock_StartsAtGivenDayAndHour()
        {
            var clock = new GameClock(SecondsPerDay, startDay: 3, startHour: 6f);

            Assert.That(clock.Day, Is.EqualTo(3));
            Assert.That(clock.Hour, Is.EqualTo(6f).Within(Tolerance));
        }

        [Test]
        public void Advance_MovesHourProportionallyToRealTime()
        {
            var clock = new GameClock(SecondsPerDay, startHour: 6f);

            clock.Advance(10f);

            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.Hour, Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void Advance_ManySmallSteps_MatchesElapsedTime()
        {
            var clock = new GameClock(SecondsPerDay);

            // Half a game day at 60 fps.
            for (int frame = 0; frame < 120 * 60; frame++)
                clock.Advance(1f / 60f);

            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.Hour, Is.EqualTo(12f).Within(0.01f));
        }

        [Test]
        public void Advance_PastMidnight_WrapsHourAndStartsNextDay()
        {
            var clock = new GameClock(SecondsPerDay, startHour: 23f);

            clock.Advance(20f);

            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.Hour, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Advance_WithinSameDay_DoesNotRaiseDayStarted()
        {
            var clock = new GameClock(SecondsPerDay, startHour: 6f);
            var startedDays = new List<int>();
            clock.DayStarted += startedDays.Add;

            clock.Advance(100f);

            Assert.That(startedDays, Is.Empty);
        }

        [Test]
        public void Advance_SeveralDaysAtOnce_RaisesDayStartedForEachDay()
        {
            var clock = new GameClock(SecondsPerDay, startHour: 6f);
            var startedDays = new List<int>();
            clock.DayStarted += startedDays.Add;

            clock.Advance(SecondsPerDay * 3f);

            Assert.That(startedDays, Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(clock.Hour, Is.EqualTo(6f).Within(Tolerance));
        }

        [Test]
        public void DayProgress_IsHourAsFractionOfDay()
        {
            var clock = new GameClock(SecondsPerDay, startHour: 18f);

            Assert.That(clock.DayProgress, Is.EqualTo(0.75f).Within(Tolerance));
        }

        [Test]
        public void Advance_NegativeTime_Throws()
        {
            var clock = new GameClock(SecondsPerDay);

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1f));
        }

        [TestCase(0f)]
        [TestCase(-10f)]
        public void Constructor_NonPositiveDayLength_Throws(float secondsPerDay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(secondsPerDay));
        }

        [TestCase(-1f)]
        [TestCase(24f)]
        public void Constructor_HourOutsideDay_Throws(float startHour)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(SecondsPerDay, startHour: startHour));
        }

        [Test]
        public void Constructor_DayBeforeFirst_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(SecondsPerDay, startDay: 0));
        }
    }
}
