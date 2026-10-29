using Arreo.Net;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class SpawnLayoutTests
    {
        private static readonly Vector3 Origin = new Vector3(0f, 0f, 0f);

        [Test]
        public void FirstPlayer_SpawnsAtTheOrigin()
        {
            Assert.That(SpawnLayout.PositionFor(0, Origin, spacing: 4f), Is.EqualTo(Origin));
        }

        [Test]
        public void NextPlayers_AlternateRightAndLeft()
        {
            Assert.That(SpawnLayout.PositionFor(1, Origin, 4f), Is.EqualTo(new Vector3(4f, 0f, 0f)));
            Assert.That(SpawnLayout.PositionFor(2, Origin, 4f), Is.EqualTo(new Vector3(-4f, 0f, 0f)));
            Assert.That(SpawnLayout.PositionFor(3, Origin, 4f), Is.EqualTo(new Vector3(8f, 0f, 0f)));
            Assert.That(SpawnLayout.PositionFor(4, Origin, 4f), Is.EqualTo(new Vector3(-8f, 0f, 0f)));
        }

        [Test]
        public void FivePlayers_NeverShareASpot()
        {
            var spots = new Vector3[5];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = SpawnLayout.PositionFor(i, new Vector3(10f, 0f, 5f), 4f);

            for (int a = 0; a < spots.Length; a++)
            for (int b = a + 1; b < spots.Length; b++)
                Assert.That(Vector3.Distance(spots[a], spots[b]), Is.GreaterThanOrEqualTo(4f));
        }
    }
}
