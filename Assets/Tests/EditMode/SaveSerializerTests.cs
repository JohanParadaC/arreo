using Arreo.Core;
using NUnit.Framework;
using UnityEngine;

namespace Arreo.Tests.EditMode
{
    public class SaveSerializerTests
    {
        private const float Tolerance = 0.0001f;

        private static SaveData CreateSave()
        {
            var save = new SaveData();
            save.clock.day = 3;
            save.clock.hour = 14.5f;
            save.money.hato = 7_000_000;
            // D75: room for each player's own wallet, even while money is shared.
            save.money.players.Add(new PlayerMoneySave { player = "player2", balance = 250_000 });
            save.cows.Add(new CowSave { inHerd = true, position = new Vector3(45.5f, 0.1f, 55.25f), yaw = 90f });
            save.cows.Add(new CowSave { inHerd = false, position = new Vector3(-100f, 0f, 90f), yaw = 270f });
            save.players.Add(new PlayerSave { id = "player1", position = new Vector3(10f, 0f, -20f), yaw = 30f });
            save.players.Add(new PlayerSave { id = "player2", position = new Vector3(-5f, 1f, 8f), yaw = 180f });
            return save;
        }

        [Test]
        public void WriteThenRead_KeepsEverything()
        {
            SaveLoadResult result = SaveSerializer.Read(SaveSerializer.Write(CreateSave()));

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Loaded));
            SaveData save = result.Data;
            Assert.That(save.clock.day, Is.EqualTo(3));
            Assert.That(save.clock.hour, Is.EqualTo(14.5f).Within(Tolerance));
            Assert.That(save.money.mode, Is.EqualTo(MoneySave.Shared));
            Assert.That(save.money.hato, Is.EqualTo(7_000_000));
            Assert.That(save.money.players, Has.Count.EqualTo(1));
            Assert.That(save.money.players[0].player, Is.EqualTo("player2"));
            Assert.That(save.money.players[0].balance, Is.EqualTo(250_000));
            Assert.That(save.cows, Has.Count.EqualTo(2));
            Assert.That(save.cows[0].inHerd, Is.True);
            Assert.That(save.cows[1].inHerd, Is.False, "a sold cow must stay sold");
            Assert.That(Vector3.Distance(save.cows[0].position, new Vector3(45.5f, 0.1f, 55.25f)), Is.LessThan(Tolerance));
            Assert.That(save.cows[1].yaw, Is.EqualTo(270f).Within(Tolerance));
            Assert.That(save.players, Has.Count.EqualTo(2));
            Assert.That(save.players[1].id, Is.EqualTo("player2"));
            Assert.That(Vector3.Distance(save.players[1].position, new Vector3(-5f, 1f, 8f)), Is.LessThan(Tolerance));
            Assert.That(save.players[1].yaw, Is.EqualTo(180f).Within(Tolerance));
        }

        [Test]
        public void Write_StampsTheCurrentVersion()
        {
            SaveData save = CreateSave();
            save.version = 0;

            SaveLoadResult result = SaveSerializer.Read(SaveSerializer.Write(save));

            Assert.That(result.FileVersion, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(result.Data.version, Is.EqualTo(SaveData.CurrentVersion));
        }

        [Test]
        public void Read_Version1_MovesItsFlatMoneyIntoTheHatoWallet()
        {
            // Version 1 kept the hato's money as one number, with no room for per-player wallets.
            const string version1 = "{\"version\":1,\"clock\":{\"day\":4,\"hour\":9.5},\"money\":1500000," +
                "\"cows\":[{\"inHerd\":false,\"position\":{\"x\":1.0,\"y\":0.0,\"z\":2.0},\"yaw\":90.0}]," +
                "\"players\":[{\"id\":\"player1\",\"position\":{\"x\":5.0,\"y\":0.0,\"z\":6.0},\"yaw\":45.0}]}";

            SaveLoadResult result = SaveSerializer.Read(version1);

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Loaded), result.Problem);
            Assert.That(result.FileVersion, Is.EqualTo(1));
            SaveData save = result.Data;
            Assert.That(save.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(save.money.mode, Is.EqualTo(MoneySave.Shared));
            Assert.That(save.money.hato, Is.EqualTo(1_500_000));
            Assert.That(save.money.players, Is.Empty);
            Assert.That(save.clock.day, Is.EqualTo(4));
            Assert.That(save.clock.hour, Is.EqualTo(9.5f).Within(Tolerance));
            Assert.That(save.cows, Has.Count.EqualTo(1));
            Assert.That(save.cows[0].inHerd, Is.False);
            Assert.That(save.players[0].id, Is.EqualTo("player1"));
            Assert.That(save.players[0].position.z, Is.EqualTo(6f).Within(Tolerance));
        }

        [TestCase("esto no es un guardado")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Read_NotASave_IsCorrupt(string text)
        {
            SaveLoadResult result = SaveSerializer.Read(text);

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(result.Data, Is.Null);
            Assert.That(result.Problem, Is.Not.Empty);
        }

        [Test]
        public void Read_FileCutOffHalfway_IsCorrupt()
        {
            string json = SaveSerializer.Write(CreateSave());

            Assert.That(SaveSerializer.Read(json.Substring(0, json.Length / 2)).Status, Is.EqualTo(SaveLoadStatus.Corrupt));
        }

        [Test]
        public void Read_JsonWithoutAVersion_IsCorrupt()
        {
            Assert.That(SaveSerializer.Read("{}").Status, Is.EqualTo(SaveLoadStatus.Corrupt));
        }

        [Test]
        public void Read_ANewerVersionThanThisGameKnows_IsUnknownVersion()
        {
            SaveLoadResult result = SaveSerializer.Read("{\"version\":99,\"clock\":{\"day\":1,\"hour\":0.0}}");

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.UnknownVersion));
            Assert.That(result.FileVersion, Is.EqualTo(99));
            Assert.That(result.Data, Is.Null);
        }

        [Test]
        public void Read_ImpossibleValues_IsCorrupt()
        {
            SaveData badDay = CreateSave();
            badDay.clock.day = 0;
            SaveData badHour = CreateSave();
            badHour.clock.hour = 25f;
            SaveData debt = CreateSave();
            debt.money.hato = -1;

            Assert.That(SaveSerializer.Read(SaveSerializer.Write(badDay)).Status, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(SaveSerializer.Read(SaveSerializer.Write(badHour)).Status, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(SaveSerializer.Read(SaveSerializer.Write(debt)).Status, Is.EqualTo(SaveLoadStatus.Corrupt));
        }
    }
}
