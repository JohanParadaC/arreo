using System.Linq;
using Arreo.Core;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class PlayerPlacesTests
    {
        private static PlayerSave Saved(string id, float x) => new PlayerSave { id = id, position = new Vector3(x, 0f, 0f), yaw = x };

        [Test]
        public void Loaded_PlayersAreFoundById()
        {
            var places = new PlayerPlaces(new[] { Saved("player1", 10f), Saved("player2", 20f) });

            Assert.That(places.TryGet("player2", out Vector3 position, out float yaw), Is.True);
            Assert.That(position.x, Is.EqualTo(20f));
            Assert.That(yaw, Is.EqualTo(20f));
            Assert.That(places.TryGet("player3", out _, out _), Is.False);
        }

        [Test]
        public void Set_ReplacesWhereThePlayerWas()
        {
            var places = new PlayerPlaces(new[] { Saved("player1", 10f) });

            places.Set("player1", new Vector3(-5f, 1f, 2f), 45f);

            Assert.That(places.TryGet("player1", out Vector3 position, out float yaw), Is.True);
            Assert.That(position, Is.EqualTo(new Vector3(-5f, 1f, 2f)));
            Assert.That(yaw, Is.EqualTo(45f));
        }

        [Test]
        public void ToSave_KeepsPlayersWhoDidNotJoinThisTime()
        {
            // player2 played last time but not today: their horse stays where they left it.
            var places = new PlayerPlaces(new[] { Saved("player1", 10f), Saved("player2", 20f) });
            places.Set("player1", new Vector3(1f, 0f, 0f), 0f);
            places.Set("player3", new Vector3(3f, 0f, 0f), 0f);

            var saved = places.ToSave();

            Assert.That(saved.Select(p => p.id), Is.EqualTo(new[] { "player1", "player2", "player3" }));
            Assert.That(saved[0].position.x, Is.EqualTo(1f));
            Assert.That(saved[1].position.x, Is.EqualTo(20f));
        }

        [Test]
        public void NewGame_HasNobody()
        {
            Assert.That(new PlayerPlaces().ToSave(), Is.Empty);
        }

        [Test]
        public void JoinOrder_NamesTheHostPlayerOne()
        {
            Assert.That(PlayerPlaces.IdForJoin(0), Is.EqualTo("player1"));
            Assert.That(PlayerPlaces.IdForJoin(4), Is.EqualTo("player5"));
        }
    }
}
