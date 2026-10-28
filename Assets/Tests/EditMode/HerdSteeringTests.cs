using Arreo.Herd;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class HerdSteeringTests
    {
        private const float WalkingRider = 4f;
        private const float GallopingRider = 12f;
        private static readonly Vector3 FarAway = new Vector3(500f, 0f, 500f);

        private static HerdSteering CreateSteering() => new HerdSteering(
            separationRadius: 2.5f,
            neighborRadius: 12f,
            separationStrength: 3f,
            alignmentWeight: 0.6f,
            cohesionStrength: 0.15f,
            fleeSpeed: 6f,
            panicSpeed: 8f,
            new RiderFear(fearRadius: 15f, gallopRadiusMultiplier: 2f, stillIntensity: 0.5f,
                gallopReferenceSpeed: 12f, panicStartSpeed: 6f));

        [Test]
        public void LoneCow_NoRiderAround_StaysStill()
        {
            Vector3 velocity = CreateSteering().DesiredVelocity(0, new[] { Vector3.zero }, new[] { Vector3.zero }, FarAway, WalkingRider);

            Assert.That(velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void RiderNearby_CowMovesAwayFromIt()
        {
            Vector3 velocity = CreateSteering().DesiredVelocity(0, new[] { Vector3.zero }, new[] { Vector3.zero },
                new Vector3(0f, 0f, -5f), WalkingRider);

            Assert.That(velocity.z, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(velocity.x), Is.LessThan(0.0001f));
        }

        [Test]
        public void GallopingRider_CowRunsFasterThanFromWalkingRider()
        {
            var steering = CreateSteering();
            var positions = new[] { Vector3.zero };
            var velocities = new[] { Vector3.zero };
            var rider = new Vector3(0f, 0f, -8f);

            float fromWalk = steering.DesiredVelocity(0, positions, velocities, rider, WalkingRider).magnitude;
            float fromGallop = steering.DesiredVelocity(0, positions, velocities, rider, GallopingRider).magnitude;

            Assert.That(fromGallop, Is.GreaterThan(fromWalk));
        }

        [Test]
        public void CalmCow_FollowsHerdMatesThatAreMoving()
        {
            var positions = new[] { Vector3.zero, new Vector3(3f, 0f, 0f) };
            var velocities = new[] { Vector3.zero, new Vector3(0f, 0f, 3f) };

            Vector3 velocity = CreateSteering().DesiredVelocity(0, positions, velocities, FarAway, WalkingRider);

            Assert.That(velocity.z, Is.GreaterThan(0f));
        }

        [Test]
        public void GallopingRider_BreaksHerdCohesion()
        {
            // The herd mate is along +x and the rider along -z, so cohesion and fleeing don't overlap.
            var steering = CreateSteering();
            var positions = new[] { Vector3.zero, new Vector3(10f, 0f, 0f) };
            var velocities = new[] { Vector3.zero, Vector3.zero };
            var rider = new Vector3(0f, 0f, -5f);

            float towardHerdAtWalk = steering.DesiredVelocity(0, positions, velocities, rider, WalkingRider).x;
            float towardHerdAtGallop = steering.DesiredVelocity(0, positions, velocities, rider, GallopingRider).x;

            Assert.That(towardHerdAtGallop, Is.LessThan(towardHerdAtWalk));
        }

        [Test]
        public void CrowdedAndTerrified_NeverExceedsPanicSpeed()
        {
            var positions = new[]
            {
                Vector3.zero, new Vector3(0.2f, 0f, 0f), new Vector3(-0.2f, 0f, 0.1f),
                new Vector3(0f, 0f, 0.3f), new Vector3(0.1f, 0f, -0.2f),
            };
            var velocities = new[] { Vector3.zero, Vector3.right * 8f, Vector3.right * 8f, Vector3.right * 8f, Vector3.right * 8f };

            Vector3 velocity = CreateSteering().DesiredVelocity(0, positions, velocities, new Vector3(-0.5f, 0f, 0f), GallopingRider);

            Assert.That(velocity.magnitude, Is.LessThanOrEqualTo(8f + 0.0001f));
        }

        [Test]
        public void DesiredVelocity_StaysOnTheGround()
        {
            var positions = new[] { Vector3.zero, new Vector3(1f, 2f, 1f) };
            var velocities = new[] { Vector3.zero, new Vector3(1f, 3f, 0f) };

            Vector3 velocity = CreateSteering().DesiredVelocity(0, positions, velocities, new Vector3(0f, 3f, -4f), WalkingRider);

            Assert.That(velocity.y, Is.EqualTo(0f));
        }
    }
}
