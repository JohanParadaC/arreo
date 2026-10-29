using Arreo.Player;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class RiderFallTests
    {
        private const float Tolerance = 0.001f;
        private static readonly Vector3 PullForward = new Vector3(0f, 0f, 1f);

        private static RiderFall CreateFall() => new RiderFall(
            fallTension: 4000f,
            launchSpeed: 6f,
            launchSpeedPerNewton: 0.002f,
            maxLaunchSpeed: 14f,
            launchAngle: 35f,
            getUpSeconds: 2.2f,
            remountSeconds: 0.8f);

        [Test]
        public void New_IsMountedWithControl()
        {
            var fall = CreateFall();

            Assert.That(fall.State, Is.EqualTo(RiderState.Mounted));
            Assert.That(fall.HasControl, Is.True);
        }

        [Test]
        public void RopeSnapsBelowTheThreshold_StaysMounted()
        {
            var fall = CreateFall();

            Assert.That(fall.TryFall(3999f, PullForward, out _), Is.False);
            Assert.That(fall.State, Is.EqualTo(RiderState.Mounted));
        }

        [Test]
        public void RopeSnapsAboveTheThreshold_ThrowsTheRiderOff()
        {
            var fall = CreateFall();

            Assert.That(fall.TryFall(4500f, PullForward, out _), Is.True);
            Assert.That(fall.State, Is.EqualTo(RiderState.Fallen));
            Assert.That(fall.HasControl, Is.False);
        }

        [Test]
        public void Launch_GoesAlongThePullAndUpwards()
        {
            CreateFall().TryFall(4000f, PullForward, out Vector3 launch);

            Vector3 direction = launch.normalized;
            Assert.That(direction.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(direction.y, Is.EqualTo(Mathf.Sin(35f * Mathf.Deg2Rad)).Within(Tolerance));
            Assert.That(direction.z, Is.EqualTo(Mathf.Cos(35f * Mathf.Deg2Rad)).Within(Tolerance));
        }

        [Test]
        public void Launch_IgnoresHowHighTheRopeWas()
        {
            CreateFall().TryFall(4000f, new Vector3(0f, 5f, 1f), out Vector3 launch);

            Assert.That(launch.normalized.y, Is.EqualTo(Mathf.Sin(35f * Mathf.Deg2Rad)).Within(Tolerance));
        }

        [Test]
        public void Launch_HarderPullThrowsFarther_UpToAMaximum()
        {
            CreateFall().TryFall(4000f, PullForward, out Vector3 atThreshold);
            CreateFall().TryFall(6000f, PullForward, out Vector3 harder);
            CreateFall().TryFall(100000f, PullForward, out Vector3 huge);

            Assert.That(atThreshold.magnitude, Is.EqualTo(6f).Within(Tolerance));
            Assert.That(harder.magnitude, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(huge.magnitude, Is.EqualTo(14f).Within(Tolerance));
        }

        [Test]
        public void WhileFallen_CannotFallAgain()
        {
            var fall = CreateFall();
            fall.TryFall(5000f, PullForward, out _);

            Assert.That(fall.TryFall(9000f, PullForward, out _), Is.False);
        }

        [Test]
        public void Fallen_GetsUpThenRemountsAndRegainsControl()
        {
            var fall = CreateFall();
            fall.TryFall(5000f, PullForward, out _);

            fall.Tick(2.1f);
            Assert.That(fall.State, Is.EqualTo(RiderState.Fallen));

            fall.Tick(0.2f);
            Assert.That(fall.State, Is.EqualTo(RiderState.Remounting));
            Assert.That(fall.HasControl, Is.False);

            fall.Tick(0.4f);
            Assert.That(fall.RemountProgress, Is.EqualTo(0.625f).Within(Tolerance));

            fall.Tick(0.5f);
            Assert.That(fall.State, Is.EqualTo(RiderState.Mounted));
            Assert.That(fall.HasControl, Is.True);
        }
    }
}
