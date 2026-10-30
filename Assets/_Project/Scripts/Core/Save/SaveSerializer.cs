using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Core
{
    /// <summary>
    /// Saved game to and from JSON. Reading looks at the version first: an older file is migrated
    /// to the current shape, a newer or unknown one is refused, and anything that isn't a readable
    /// save (not JSON, cut off, impossible values) comes back as corrupt instead of throwing.
    /// </summary>
    public static class SaveSerializer
    {
        public static string Write(SaveData data)
        {
            data.version = SaveData.CurrentVersion;
            return JsonUtility.ToJson(data, prettyPrint: true);
        }

        public static SaveLoadResult Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Corrupt(0, "the file is empty");

            VersionProbe probe;
            try
            {
                probe = JsonUtility.FromJson<VersionProbe>(json);
            }
            catch (Exception e)
            {
                return Corrupt(0, $"it is not valid JSON ({e.Message})");
            }
            if (probe == null || probe.version <= 0)
                return Corrupt(0, "it has no version number, so it is not a save");
            if (probe.version > SaveData.CurrentVersion)
                return new SaveLoadResult(SaveLoadStatus.UnknownVersion, fileVersion: probe.version,
                    problem: $"it was saved by version {probe.version} and this game reads up to {SaveData.CurrentVersion}");

            SaveData data;
            try
            {
                data = probe.version == 1 ? FromVersion1(JsonUtility.FromJson<SaveDataV1>(json)) : JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                return Corrupt(probe.version, $"it could not be read ({e.Message})");
            }

            string problem = Validate(data);
            if (problem != null)
                return Corrupt(probe.version, problem);
            data.version = SaveData.CurrentVersion;
            return new SaveLoadResult(SaveLoadStatus.Loaded, data, probe.version);
        }

        private static SaveLoadResult Corrupt(int version, string problem) =>
            new SaveLoadResult(SaveLoadStatus.Corrupt, fileVersion: version, problem: problem);

        // Version 1 → 2: the single money number becomes the hato's wallet (D75), with no player wallets.
        private static SaveData FromVersion1(SaveDataV1 old) => new SaveData
        {
            clock = old.clock,
            money = new MoneySave { mode = MoneySave.Shared, hato = old.money },
            cows = old.cows,
            players = old.players,
        };

        // Returns what's wrong, or null if the game can be loaded as it is.
        private static string Validate(SaveData data)
        {
            if (data == null || data.clock == null || data.money == null)
                return "parts of the game are missing";
            data.cows ??= new List<CowSave>();
            data.players ??= new List<PlayerSave>();
            data.money.players ??= new List<PlayerMoneySave>();

            if (data.clock.day < 1)
                return $"day {data.clock.day} doesn't exist (days start at 1)";
            if (!(data.clock.hour >= 0f && data.clock.hour < GameClock.HoursPerDay))
                return $"hour {data.clock.hour} is not between 0 and 24";
            if (data.money.mode != MoneySave.Shared && data.money.mode != MoneySave.PerPlayer)
                return $"unknown money mode '{data.money.mode}'";
            if (data.money.hato < 0)
                return "the hato's money is negative";
            foreach (PlayerMoneySave wallet in data.money.players)
                if (wallet == null || wallet.balance < 0)
                    return "a player's money is negative";
            foreach (CowSave cow in data.cows)
                if (cow == null || !IsFinite(cow.position) || !float.IsFinite(cow.yaw))
                    return "a cow has no valid position";
            foreach (PlayerSave player in data.players)
                if (player == null || string.IsNullOrEmpty(player.id) || !IsFinite(player.position))
                    return "a player has no id or no valid position";
            return null;
        }

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        [Serializable]
        private sealed class VersionProbe
        {
            public int version;
        }

        /// <summary>The version 1 shape, kept only so old saves can be migrated.</summary>
        [Serializable]
        private sealed class SaveDataV1
        {
            public int version;
            public ClockSave clock = new ClockSave();
            public long money;
            public List<CowSave> cows = new List<CowSave>();
            public List<PlayerSave> players = new List<PlayerSave>();
        }
    }
}
