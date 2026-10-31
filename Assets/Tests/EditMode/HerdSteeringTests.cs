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

        [Test]
        public void SeveralRiders_TheNearOneScaresLikeItWouldAlone()
        {
            var positions = new[] { Vector3.zero };
            var velocities = new[] { Vector3.zero };
            var near = new Vector3(0f, 0f, -5f);
            var riders = new[] { new RiderSample(new Vector3(300f, 0f, 300f), 4f), new RiderSample(near, 4f) };

            Vector3 withBoth = CreateSteering().DesiredVelocity(0, positions, velocities, riders);
            Vector3 alone = CreateSteering().DesiredVelocity(0, positions, velocities, near, 4f);

            Assert.That(Vector3.Distance(withBoth, alone), Is.LessThan(0.001f));
        }

        [Test]
        public void TwoRidersOnOppositeSides_SqueezeTheCowBetweenThem()
        {
            var riders = new[] { new RiderSample(new Vector3(-5f, 0f, 0f), 4f), new RiderSample(new Vector3(5f, 0f, -5f), 4f) };

            Vector3 velocity = CreateSteering().DesiredVelocity(0, new[] { Vector3.zero }, new[] { Vector3.zero }, riders);

            // Pushed away from both: the left rider pushes +x, the right-and-behind one pushes -x and +z.
            Assert.That(velocity.z, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(velocity.x), Is.LessThan(Mathf.Abs(velocity.z)));
        }

        [Test]
        public void NoRiders_LeavesTheCowCalm()
        {
            Vector3 velocity = CreateSteering().DesiredVelocity(0, new[] { Vector3.zero }, new[] { Vector3.zero }, new RiderSample[0]);

            Assert.That(velocity, Is.EqualTo(Vector3.zero));
        }

        // The cabrestero's canto (rule 4): fear x0.25 and cohesion x2.5 for a cow that fully hears it.
        private static HerdSteering CreateSteeringWithCanto() => new HerdSteering(
            separationRadius: 2.5f, neighborRadius: 12f, separationStrength: 3f, alignmentWeight: 0.6f, cohesionStrength: 0.15f,
            fleeSpeed: 6f, panicSpeed: 8f,
            new RiderFear(fearRadius: 15f, gallopRadiusMultiplier: 2f, stillIntensity: 0.5f,
                gallopReferenceSpeed: 12f, panicStartSpeed: 6f, flightZoneRadius: 8f),
            flightZoneSpeed: 2.2f,
            new CantoCalm(radius: 30f, fullRadius: 15f, fearReduction: 0.75f, cohesionBoost: 1.5f, riseSeconds: 2f, fadeSeconds: 4f,
                stampedeShortening: 0.5f, regroupRadiusBoost: 1.5f));

        [Test]
        public void Canto_CalmCowFindsHerdMatesBeyondTheUsualRadius()
        {
            // The herd mate is 20 m away: past the 12 m neighbor radius, inside 12 x 2.5 = 30 m.
            var positions = new[] { Vector3.zero, new Vector3(20f, 0f, 0f) };
            var velocities = new[] { Vector3.zero, Vector3.zero };
            HerdSteering steering = CreateSteeringWithCanto();

            float apart = steering.DesiredVelocity(0, positions, velocities, new RiderSample[0], cantoCalm: 0f).x;
            float together = steering.DesiredVelocity(0, positions, velocities, new RiderSample[0], cantoCalm: 1f).x;

            Assert.That(apart, Is.EqualTo(0f));
            Assert.That(together, Is.GreaterThan(1f));
        }

        [Test]
        public void Canto_ScatteredHerdRegroupsFaster()
        {
            // A herd strung out after a barajustada, each cow 14 m from the next (past the 12 m neighbor
            // radius, so without the canto none of them feels the others), left alone for 5 s.
            float scattered = SpreadAfter(cantoCalm: 0f, seconds: 5f);
            float calmed = SpreadAfter(cantoCalm: 1f, seconds: 5f);

            Assert.That(calmed, Is.LessThan(scattered * 0.6f), $"spread {calmed:0.0} m with the canto, {scattered:0.0} m without");
        }

        // Average distance (m) of each cow to the herd's center after moving at its desired velocity.
        private static float SpreadAfter(float cantoCalm, float seconds)
        {
            const float step = 0.1f;
            HerdSteering steering = CreateSteeringWithCanto();
            var positions = new Vector3[6];
            var velocities = new Vector3[6];
            var desired = new Vector3[6];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = new Vector3(i * 14f, 0f, 0f);
            for (float t = 0f; t < seconds; t += step)
            {
                for (int i = 0; i < positions.Length; i++)
                    desired[i] = steering.DesiredVelocity(i, positions, velocities, new RiderSample[0], cantoCalm);
                for (int i = 0; i < positions.Length; i++)
                {
                    velocities[i] = desired[i];
                    positions[i] += desired[i] * step;
                }
            }
            Vector3 center = Vector3.zero;
            foreach (Vector3 position in positions)
                center += position / positions.Length;
            float spread = 0f;
            foreach (Vector3 position in positions)
                spread += Vector3.Distance(position, center) / positions.Length;
            return spread;
        }

        [Test]
        public void Canto_GallopingRiderScaresACalmCowFarLess()
        {
            // Rider at 12 m, past the flight zone, so all of the flight is fear.
            var riders = new[] { new RiderSample(new Vector3(0f, 0f, -12f), GallopingRider, singing: true) };
            HerdSteering steering = CreateSteeringWithCanto();

            float scared = steering.DesiredVelocity(0, new[] { Vector3.zero }, new[] { Vector3.zero }, riders, cantoCalm: 0f).magnitude;
            float calmed = steering.DesiredVelocity(0, new[] { Vector3.zero }, new[] { Vector3.zero }, riders, cantoCalm: 1f).magnitude;

            Assert.That(scared, Is.EqualTo(6f * 0.6f).Within(0.001f), "fear (1 - 12/30) x flee speed 6");
            Assert.That(calmed, Is.EqualTo(scared * 0.25f).Within(0.001f));
        }

        [Test]
        public void Canto_CalmCowIsPulledHarderTowardItsHerdMates()
        {
            // No rider near: the x part is pure cohesion, 10 m * 0.15 = 1.5 m/s, x2.5 with the canto.
            var positions = new[] { Vector3.zero, new Vector3(10f, 0f, 0f) };
            var velocities = new[] { Vector3.zero, Vector3.zero };
            HerdSteering steering = CreateSteeringWithCanto();

            float apart = steering.DesiredVelocity(0, positions, velocities, new RiderSample[0], cantoCalm: 0f).x;
            float together = steering.DesiredVelocity(0, positions, velocities, new RiderSample[0], cantoCalm: 1f).x;

            Assert.That(apart, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(together, Is.EqualTo(3.75f).Within(0.001f));
        }

        [Test]
        public void Canto_GallopingSingerScattersTheHerdLess()
        {
            // Herd mate along +x, rider along -z: panic fades cohesion, and the canto takes panic away.
            var positions = new[] { Vector3.zero, new Vector3(10f, 0f, 0f) };
            var velocities = new[] { Vector3.zero, Vector3.zero };
            var riders = new[] { new RiderSample(new Vector3(0f, 0f, -5f), GallopingRider, singing: true) };
            HerdSteering steering = CreateSteeringWithCanto();

            float towardHerdScared = steering.DesiredVelocity(0, positions, velocities, riders, cantoCalm: 0f).x;
            float towardHerdCalmed = steering.DesiredVelocity(0, positions, velocities, riders, cantoCalm: 1f).x;

            Assert.That(towardHerdCalmed, Is.GreaterThan(towardHerdScared * 2f));
        }

        [Test]
        public void Canto_NotHeard_ChangesNothing()
        {
            var positions = new[] { Vector3.zero, new Vector3(6f, 0f, 2f) };
            var velocities = new[] { Vector3.zero, new Vector3(1f, 0f, 1f) };
            var riders = new[] { new RiderSample(new Vector3(0f, 0f, -7f), GallopingRider) };

            Vector3 without = CreateSteering().DesiredVelocity(0, positions, velocities, riders);
            Vector3 unheard = CreateSteeringWithCanto().DesiredVelocity(0, positions, velocities, riders, cantoCalm: 0f);

            Assert.That(Vector3.Distance(without, unheard), Is.LessThan(0.0001f));
        }
    }
}
