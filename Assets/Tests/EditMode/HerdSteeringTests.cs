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
                gallopReferenceSpeed: 12f, panicStartSpeed: 6f, flightZoneRadius: 8f),
            flightZoneSpeed: 2.2f);

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

        [Test]
        public void StillRiderInsideFlightZone_CowWalksAwayAtFlightZoneSpeed()
        {
            // At 6 m fear alone would give 1.8 m/s; the flight zone lifts it to 2.2.
            Vector3 velocity = CreateSteering().DesiredVelocity(0, new[] { Vector3.zero }, new[] { Vector3.zero },
                new Vector3(0f, 0f, -6f), riderSpeed: 0f);

            Assert.That(velocity.magnitude, Is.EqualTo(2.2f).Within(0.001f));
            Assert.That(velocity.z, Is.GreaterThan(0f));
        }

        [Test]
        public void StillRiderOutsideFlightZone_OnlyFearMovesTheCow()
        {
            // At 10 m: fear (1 - 10/15) * 0.5 = 1/6, times flee speed 6 = 1 m/s.
            Vector3 velocity = CreateSteering().DesiredVelocity(0, new[] { Vector3.zero }, new[] { Vector3.zero },
                new Vector3(0f, 0f, -10f), riderSpeed: 0f);

            Assert.That(velocity.magnitude, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SlowRiderInsideFlightZone_DoesNotBreakCohesion()
        {
            // Herd mate along +x, rider along -z: the x part is pure cohesion, 10 m * 0.15 = 1.5 m/s.
            var positions = new[] { Vector3.zero, new Vector3(10f, 0f, 0f) };
            var velocities = new[] { Vector3.zero, Vector3.zero };

            Vector3 velocity = CreateSteering().DesiredVelocity(0, positions, velocities, new Vector3(0f, 0f, -6f), riderSpeed: 2f);

            Assert.That(velocity.x, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void StampedeVelocity_RunsAwayFromTheThunderAtPanicSpeed()
        {
            Vector3 velocity = CreateSteering().StampedeVelocity(0, new[] { new Vector3(0f, 0f, 10f) }, Vector3.zero, intensity: 1f);

            Assert.That(velocity.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(velocity.z, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void StampedeVelocity_FadingIntensity_IsSlower()
        {
            Vector3 velocity = CreateSteering().StampedeVelocity(0, new[] { new Vector3(0f, 0f, 10f) }, Vector3.zero, intensity: 0.5f);

            Assert.That(velocity.magnitude, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void StampedeVelocity_NoIntensity_IsZero()
        {
            Vector3 velocity = CreateSteering().StampedeVelocity(0, new[] { new Vector3(0f, 0f, 10f) }, Vector3.zero, intensity: 0f);

            Assert.That(velocity, Is.EqualTo(Vector3.zero));
        }
    }
}
