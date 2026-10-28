using Arreo.Horse;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class HorseLocomotionTests
    {
        private const float Tolerance = 0.001f;
        private const float Step = 0.02f;

        private static readonly HorseIntent Forward = new HorseIntent(throttle: 1f, steer: 0f, gallop: false);
        private static readonly HorseIntent Gallop = new HorseIntent(throttle: 1f, steer: 0f, gallop: true);

        private static HorseLocomotion CreateLocomotion(float heading = 0f, float staminaRecovery = 20f) => new HorseLocomotion(
            new HorseGait(walkSpeed: 4f, gallopSpeed: 12f, backSpeed: 1.5f, acceleration: 4f,
                coastDeceleration: 2f, braking: 8f, turnRateStanding: 120f, turnRateAtGallop: 40f),
            new HorseStamina(max: 100f, drainPerSecond: 10f, recoveryPerSecond: staminaRecovery, recoveryThreshold: 0.35f),
            heading);

        private static void Simulate(HorseLocomotion locomotion, HorseIntent intent, float seconds)
        {
            for (float t = 0f; t < seconds - Step * 0.5f; t += Step)
                locomotion.Tick(intent, Step);
        }

        [Test]
        public void Intent_ClampsToValidRange()
        {
            var intent = new HorseIntent(throttle: 3f, steer: -2f, gallop: true);

            Assert.That(intent.Throttle, Is.EqualTo(1f));
            Assert.That(intent.Steer, Is.EqualTo(-1f));
        }

        [Test]
        public void Tick_NoIntent_StaysStill()
        {
            var locomotion = CreateLocomotion(heading: 90f);

            Simulate(locomotion, HorseIntent.None, 1f);

            Assert.That(locomotion.Speed, Is.EqualTo(0f));
            Assert.That(locomotion.Heading, Is.EqualTo(90f));
        }

        [Test]
        public void Tick_Forward_AcceleratesGradually()
        {
            var locomotion = CreateLocomotion();

            Simulate(locomotion, Forward, 0.5f);

            Assert.That(locomotion.Speed, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void Tick_HoldingForward_SettlesAtWalkSpeed()
        {
            var locomotion = CreateLocomotion();

            Simulate(locomotion, Forward, 3f);

            Assert.That(locomotion.Speed, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(locomotion.IsGalloping, Is.False);
        }

        [Test]
        public void Tick_ThrottleReleased_CoastsDown()
        {
            var locomotion = CreateLocomotion();
            Simulate(locomotion, Forward, 3f);

            Simulate(locomotion, HorseIntent.None, 1f);

            Assert.That(locomotion.Speed, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void Tick_Gallop_ReachesGallopSpeedAndDrainsStamina()
        {
            var locomotion = CreateLocomotion();

            Simulate(locomotion, Gallop, 4f);

            Assert.That(locomotion.Speed, Is.EqualTo(12f).Within(Tolerance));
            Assert.That(locomotion.IsGalloping, Is.True);
            Assert.That(locomotion.Stamina.Current, Is.LessThan(100f));
        }

        [Test]
        public void Tick_WalkingAfterGallop_RecoversStamina()
        {
            var locomotion = CreateLocomotion();
            Simulate(locomotion, Gallop, 4f);
            float afterGallop = locomotion.Stamina.Current;

            Simulate(locomotion, Forward, 2f);

            Assert.That(locomotion.Stamina.Current, Is.GreaterThan(afterGallop));
        }

        [Test]
        public void Tick_GallopWhileExhausted_FallsBackToWalk()
        {
            // Slow recovery so it stays exhausted for the whole test.
            var locomotion = CreateLocomotion(staminaRecovery: 1f);
            Simulate(locomotion, Gallop, 15f);

            Simulate(locomotion, Gallop, 6f);

            Assert.That(locomotion.Stamina.CanGallop, Is.False);
            Assert.That(locomotion.IsGalloping, Is.False);
            Assert.That(locomotion.Speed, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void Tick_SteerWhileStanding_TurnsAtStandingRate()
        {
            var locomotion = CreateLocomotion();

            Simulate(locomotion, new HorseIntent(0f, 1f, false), 0.5f);

            Assert.That(locomotion.Heading, Is.EqualTo(60f).Within(Tolerance));
        }

        [Test]
        public void Tick_SteerAtGallop_TurnsSlowerThanStanding()
        {
            var galloping = CreateLocomotion();
            Simulate(galloping, Gallop, 4f);

            Simulate(galloping, new HorseIntent(1f, 1f, true), 0.5f);

            Assert.That(galloping.Heading, Is.EqualTo(20f).Within(0.1f));
        }

        [Test]
        public void Tick_SteerLeftPastNorth_WrapsHeading()
        {
            var locomotion = CreateLocomotion();

            Simulate(locomotion, new HorseIntent(0f, -1f, false), 0.1f);

            Assert.That(locomotion.Heading, Is.EqualTo(348f).Within(Tolerance));
        }

        [Test]
        public void LimitToMeasuredSpeed_Blocked_DropsSpeed()
        {
            var locomotion = CreateLocomotion();
            Simulate(locomotion, Gallop, 4f);

            locomotion.LimitToMeasuredSpeed(1f);

            Assert.That(locomotion.Speed, Is.EqualTo(1f));
        }

        [Test]
        public void LimitToMeasuredSpeed_SmallLoss_KeepsSpeed()
        {
            var locomotion = CreateLocomotion();
            Simulate(locomotion, Gallop, 4f);

            locomotion.LimitToMeasuredSpeed(10f);

            Assert.That(locomotion.Speed, Is.EqualTo(12f).Within(Tolerance));
        }
    }
}
