using Arreo.Net;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class PlayerRoleTests
    {
        [Test]
        public void HostsOwnHorse_DoesEverything()
        {
            var role = PlayerRole.For(isOwner: true, isServer: true);

            Assert.That(role.ReadsInput, Is.True);
            Assert.That(role.SimulatesMovement, Is.True);
            Assert.That(role.IsInterpolatedCopy, Is.False);
            Assert.That(role.FollowedByCamera, Is.True);
            Assert.That(role.RunsHostSystems, Is.True);
        }

        [Test]
        public void ClientsOwnHorse_MovesItButLeavesHerdAndRopeToTheHost()
        {
            var role = PlayerRole.For(isOwner: true, isServer: false);

            Assert.That(role.ReadsInput, Is.True);
            Assert.That(role.SimulatesMovement, Is.True);
            Assert.That(role.IsInterpolatedCopy, Is.False);
            Assert.That(role.FollowedByCamera, Is.True);
            Assert.That(role.RunsHostSystems, Is.False);
        }

        [Test]
        public void SomeoneElsesHorse_IsAnInterpolatedCopy_OnHostAndClients()
        {
            foreach (bool isServer in new[] { true, false })
            {
                var role = PlayerRole.For(isOwner: false, isServer: isServer);

                Assert.That(role.ReadsInput, Is.False);
                Assert.That(role.SimulatesMovement, Is.False);
                Assert.That(role.IsInterpolatedCopy, Is.True);
                Assert.That(role.FollowedByCamera, Is.False);
                Assert.That(role.RunsHostSystems, Is.False);
            }
        }

        [Test]
        public void OnlyTheLocalRiderIsTaggedForTheCameraToIgnore()
        {
            Assert.That(PlayerRole.For(isOwner: true, isServer: false).CameraIgnores, Is.True);
            Assert.That(PlayerRole.For(isOwner: false, isServer: true).CameraIgnores, Is.False);
        }
    }
}
