using Arreo.Herd;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class BoidRulesTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Separation_NeighborTooClose_PushesAway()
        {
            var positions = new[] { Vector3.zero, new Vector3(1f, 0f, 0f) };

            Vector3 push = BoidRules.Separation(0, positions, radius: 2f);

            Assert.That(push.x, Is.LessThan(0f));
            Assert.That(push.z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Separation_CloserNeighbor_PushesHarder()
        {
            var near = new[] { Vector3.zero, new Vector3(0.5f, 0f, 0f) };
            var far = new[] { Vector3.zero, new Vector3(1.5f, 0f, 0f) };

            float nearPush = BoidRules.Separation(0, near, radius: 2f).magnitude;
            float farPush = BoidRules.Separation(0, far, radius: 2f).magnitude;

            Assert.That(nearPush, Is.GreaterThan(farPush));
        }

        [Test]
        public void Separation_NeighborOutsideRadius_DoesNothing()
        {
            var positions = new[] { Vector3.zero, new Vector3(3f, 0f, 0f) };

            Assert.That(BoidRules.Separation(0, positions, radius: 2f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Separation_NeighborsOnBothSides_CancelOut()
        {
            var positions = new[] { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f) };

            Assert.That(BoidRules.Separation(0, positions, radius: 2f).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Separation_IgnoresHeightDifference()
        {
            var positions = new[] { Vector3.zero, new Vector3(1f, 5f, 0f) };

            Vector3 push = BoidRules.Separation(0, positions, radius: 2f);

            Assert.That(push.y, Is.EqualTo(0f));
            Assert.That(push.x, Is.LessThan(0f));
        }

        [Test]
        public void Alignment_ReturnsAverageNeighborVelocity()
        {
            var positions = new[] { Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 2f) };
            var velocities = new[] { Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 4f) };

            Vector3 alignment = BoidRules.Alignment(0, positions, velocities, radius: 5f);

            Assert.That(alignment.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(alignment.z, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void Alignment_IgnoresOwnVelocityAndFarNeighbors()
        {
            var positions = new[] { Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(20f, 0f, 0f) };
            var velocities = new[] { new Vector3(9f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(5f, 0f, 0f) };

            Vector3 alignment = BoidRules.Alignment(0, positions, velocities, radius: 5f);

            Assert.That(alignment.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(alignment.z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Alignment_NoNeighbors_IsZero()
        {
            var positions = new[] { Vector3.zero };
            var velocities = new[] { new Vector3(3f, 0f, 0f) };

            Assert.That(BoidRules.Alignment(0, positions, velocities, radius: 5f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Cohesion_PointsToNeighborsCenter()
        {
            var positions = new[] { Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 2f) };

            Vector3 cohesion = BoidRules.Cohesion(0, positions, radius: 10f);

            Assert.That(cohesion.x, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(cohesion.z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Cohesion_Alone_IsZero()
        {
            var positions = new[] { Vector3.zero, new Vector3(30f, 0f, 0f) };

            Assert.That(BoidRules.Cohesion(0, positions, radius: 10f), Is.EqualTo(Vector3.zero));
        }
    }
}
