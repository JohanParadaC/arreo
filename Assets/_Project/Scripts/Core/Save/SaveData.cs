using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Core
{
    /// <summary>
    /// A saved game as it goes into the JSON file (rule 5: the host saves, with a version number).
    /// Public camelCase fields because they are the JSON keys (JsonUtility). Systems copy their state
    /// in and out of it; nothing here knows about them. Bump <see cref="CurrentVersion"/> whenever
    /// the shape changes and teach <see cref="SaveSerializer"/> to migrate the old one.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>2: money became a set of wallets (D75). 1: money was a single number.</summary>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        /// <summary>When it was written (UTC, ISO 8601), for the menu and for people reading the file.</summary>
        public string savedAtUtc = "";
        public ClockSave clock = new ClockSave();
        public MoneySave money = new MoneySave();
        /// <summary>Every cow of the active herd, by index (its network id).</summary>
        public List<CowSave> cows = new List<CowSave>();
        public List<PlayerSave> players = new List<PlayerSave>();
    }

    [Serializable]
    public sealed class ClockSave
    {
        public int day = 1;
        public float hour;
    }

    /// <summary>D75: the hato's wallet, and room for each player's when money is split.</summary>
    [Serializable]
    public sealed class MoneySave
    {
        public const string Shared = "Shared";
        public const string PerPlayer = "PerPlayer";

        public string mode = Shared;
        public long hato;
        /// <summary>Empty while money is shared.</summary>
        public List<PlayerMoneySave> players = new List<PlayerMoneySave>();
    }

    [Serializable]
    public sealed class PlayerMoneySave
    {
        public string player = "";
        public long balance;
    }

    [Serializable]
    public sealed class CowSave
    {
        /// <summary>False once sold or lost (D29): it stays gone after loading.</summary>
        public bool inHerd = true;
        public Vector3 position;
        public float yaw;
    }

    [Serializable]
    public sealed class PlayerSave
    {
        /// <summary>Who this is. For now the join order ("player1" is the host); Steam ids later.</summary>
        public string id = "";
        public Vector3 position;
        public float yaw;
    }
}
