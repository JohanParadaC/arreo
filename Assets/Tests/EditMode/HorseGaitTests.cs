using System;
using Arreo.Horse;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class HorseGaitTests
    {
        private const float Tolerance = 0.0001f;

        private static HorseGait CreateGait() => new HorseGait(
            walkSpeed: 4f,
            gallopSpeed: 12f,
            backSpeed: 1.5f,
            acceleration: 4f,
            coastDeceleration: 2f,
            braking: 8f,
            turnRateStanding: 120f,
            turnRateAtGallop: 40f,
            driveSpeed: 2f);

        [Test]
        public void TargetSpeed_FullThrottleWalking_IsWalkSpeed()
        {
            Assert.That(CreateGait().TargetSpeed(1f, galloping: false), Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void TargetSpeed_FullThrottleGalloping_IsGallopSpeed()
        {
            Assert.That(CreateGait().TargetSpeed(1f, galloping: true), Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void TargetSpeed_HalfThrottle_IsHalfWalkSpeed()
        {
            Assert.That(CreateGait().TargetSpeed(0.5f, galloping: false), Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void TargetSpeed_NegativeThrottle_BacksUpSlowly()
        {
            Assert.That(CreateGait().TargetSpeed(-1f, galloping: true), Is.EqualTo(-1.5f).Within(Tolerance));
        }

        [Test]
        public void NextSpeed_SpeedingUp_IsLimitedByAcceleration()
        {
            Assert.That(CreateGait().NextSpeed(0f, 12f, braking: false, deltaTime: 1f), Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void NextSpeed_NearTarget_DoesNotOvershoot()
        {
            Assert.That(CreateGait().NextSpeed(11f, 12f, braking: false, deltaTime: 1f), Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void NextSpeed_ThrottleReleased_CoastsDownWithInertia()
        {
            Assert.That(CreateGait().NextSpeed(10f, 0f, braking: false, deltaTime: 1f), Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void NextSpeed_Braking_SlowsFasterThanCoasting()
        {
            Assert.That(CreateGait().NextSpeed(10f, 0f, braking: true, deltaTime: 1f), Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void NextSpeed_ReversingFromStandstill_UsesAcceleration()
        {
            Assert.That(CreateGait().NextSpeed(0f, -1.5f, braking: true, deltaTime: 0.25f), Is.EqualTo(-1f).Within(Tolerance));
        }

        [Test]
        public void TurnRate_Standing_IsStandingRate()
        {
            Assert.That(CreateGait().TurnRate(0f), Is.EqualTo(120f).Within(Tolerance));
        }

        [Test]
        public void TurnRate_AtGallopSpeed_IsGallopRate()
        {
            Assert.That(CreateGait().TurnRate(12f), Is.EqualTo(40f).Within(Tolerance));
        }

        [Test]
        public void TurnRate_HalfwayToGallop_IsHalfwayBetweenRates()
        {
            Assert.That(CreateGait().TurnRate(6f), Is.EqualTo(80f).Within(Tolerance));
        }

        [Test]
        public void TurnRate_BackingUp_DependsOnSpeedMagnitude()
        {
            var gait = CreateGait();

            Assert.That(gait.TurnRate(-1.5f), Is.EqualTo(gait.TurnRate(1.5f)).Within(Tolerance));
        }

        [Test]
        public void Constructor_GallopNotFasterThanWalk_Throws()
        {
            Assert.Throws<ArgumentException>(() => new HorseGait(4f, 4f, 1.5f, 4f, 2f, 8f, 120f, 40f, 2f));
        }

        [Test]
        public void TargetSpeed_FullThrottleDriving_IsDriveSpeed()
        {
            Assert.That(CreateGait().TargetSpeed(1f, galloping: false, driving: true), Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void TargetSpeed_DrivingAndGalloping_Gallops()
        {
            Assert.That(CreateGait().TargetSpeed(1f, galloping: true, driving: true), Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void Constructor_DriveFasterThanWalk_Throws()
        {
            Assert.Throws<ArgumentException>(() => new HorseGait(4f, 12f, 1.5f, 4f, 2f, 8f, 120f, 40f, 5f));
        }
    }
}
