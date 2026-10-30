using Arreo.Net;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class PlaybackClockTests
    {
        private const float Tolerance = 0.0001f;
        private const float Frame = 1f / 60f;

        [Test]
        public void FirstAdvance_StartsAtTheTarget()
        {
            var clock = new PlaybackClock(maxSlew: 0.1f, snapDistance: 0.25f);

            Assert.That(clock.Advance(Frame, target: 12f), Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void TargetMovingWithRealTime_IsFollowedExactly()
        {
            var clock = new PlaybackClock(0.1f, 0.25f);
            float target = 5f;
            clock.Advance(Frame, target);
            for (int i = 0; i < 120; i++)
            {
                target += Frame;
                Assert.That(clock.Advance(Frame, target), Is.EqualTo(target).Within(Tolerance));
            }
        }

        [Test]
        public void TargetStepsAhead_ClockSpeedsUpGentlyInsteadOfJumping()
        {
            var clock = new PlaybackClock(maxSlew: 0.1f, snapDistance: 0.25f);
            float target = 5f;
            float time = clock.Advance(Frame, target);

            // The estimate of the host's clock suddenly moves 30 ms ahead.
            target += 0.03f;
            for (int i = 0; i < 60; i++)
            {
                target += Frame;
                float next = clock.Advance(Frame, target);
                Assert.That(next - time, Is.LessThanOrEqualTo(Frame * 1.1f + Tolerance), "the clock jumped");
                Assert.That(next - time, Is.GreaterThanOrEqualTo(Frame - Tolerance), "the clock slowed down while behind");
                time = next;
            }
            Assert.That(time, Is.EqualTo(target).Within(0.002f), "the clock did not catch up within a second");
        }

        [Test]
        public void TargetStepsBack_ClockSlowsDownButNeverRunsBackwards()
        {
            var clock = new PlaybackClock(0.1f, 0.25f);
            float target = 5f;
            float time = clock.Advance(Frame, target);

            target -= 0.03f;
            for (int i = 0; i < 60; i++)
            {
                target += Frame;
                float next = clock.Advance(Frame, target);
                Assert.That(next - time, Is.GreaterThanOrEqualTo(Frame * 0.9f - Tolerance), "the clock jumped back");
                time = next;
            }
            Assert.That(time, Is.EqualTo(target).Within(0.002f));
        }

        [Test]
        public void TargetFarAway_SnapsToIt()
        {
            var clock = new PlaybackClock(0.1f, snapDistance: 0.25f);
            clock.Advance(Frame, 5f);

            Assert.That(clock.Advance(Frame, 9f), Is.EqualTo(9f).Within(Tolerance));
        }
    }
}
