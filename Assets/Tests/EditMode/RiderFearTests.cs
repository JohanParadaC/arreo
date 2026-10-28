using Arreo.Herd;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class RiderFearTests
    {
        private const float Tolerance = 0.0001f;

        private static RiderFear CreateFear() => new RiderFear(
            fearRadius: 15f,
            gallopRadiusMultiplier: 2f,
            stillIntensity: 0.5f,
            gallopReferenceSpeed: 12f,
            panicStartSpeed: 6f);

        [Test]
        public void Fear_StillRiderOutsideRadius_IsZero()
        {
            Assert.That(CreateFear().Fear(distance: 20f, riderSpeed: 0f), Is.EqualTo(0f));
        }

        [Test]
        public void Fear_StillRiderRightNextToCow_IsStillIntensity()
        {
            Assert.That(CreateFear().Fear(distance: 0f, riderSpeed: 0f), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Fear_CloserRider_IsScarier()
        {
            var fear = CreateFear();

            Assert.That(fear.Fear(5f, 4f), Is.GreaterThan(fear.Fear(10f, 4f)));
        }

        [Test]
        public void Fear_GallopingRider_ScaresFromFarther()
        {
            Assert.That(CreateFear().Fear(distance: 20f, riderSpeed: 12f), Is.EqualTo(1f / 3f).Within(Tolerance));
        }

        [Test]
        public void Fear_FasterRiderAtSameDistance_IsScarier()
        {
            var fear = CreateFear();

            Assert.That(fear.Fear(8f, 12f), Is.GreaterThan(fear.Fear(8f, 4f)));
        }

        [Test]
        public void Fear_NeverExceedsOne()
        {
            Assert.That(CreateFear().Fear(distance: 0f, riderSpeed: 30f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Panic_AtWalk_IsZero()
        {
            Assert.That(CreateFear().Panic(fear: 0.8f, riderSpeed: 4f), Is.EqualTo(0f));
        }

        [Test]
        public void Panic_AtFullGallop_EqualsFear()
        {
            Assert.That(CreateFear().Panic(fear: 0.8f, riderSpeed: 12f), Is.EqualTo(0.8f).Within(Tolerance));
        }

        [Test]
        public void Panic_HalfwayToGallop_IsHalfOfFear()
        {
            Assert.That(CreateFear().Panic(fear: 0.8f, riderSpeed: 9f), Is.EqualTo(0.4f).Within(Tolerance));
        }

        [Test]
        public void FleeDirection_PointsAwayFromRiderAlongTheGround()
        {
            Vector3 direction = RiderFear.FleeDirection(cow: Vector3.zero, rider: new Vector3(0f, 2f, -5f));

            Assert.That(direction.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(direction.y, Is.EqualTo(0f));
            Assert.That(direction.z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void FleeDirection_SamePosition_IsZero()
        {
            Assert.That(RiderFear.FleeDirection(Vector3.one, Vector3.one), Is.EqualTo(Vector3.zero));
        }
    }
}
