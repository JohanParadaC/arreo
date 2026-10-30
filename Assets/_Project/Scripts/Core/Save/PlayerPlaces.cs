using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arreo.Core
{
    /// <summary>
    /// Where every player of a game last was, by player id: what the save had, updated as they
    /// ride. A player who didn't join this session keeps their saved place for next time.
    /// </summary>
    public sealed class PlayerPlaces
    {
        private readonly Dictionary<string, PlayerSave> _places = new Dictionary<string, PlayerSave>();

        public PlayerPlaces(IEnumerable<PlayerSave> loaded = null)
        {
            if (loaded == null)
                return;
            foreach (PlayerSave player in loaded)
                Set(player.id, player.position, player.yaw);
        }

        /// <summary>
        /// A player's id from the order they joined in (0 = the host's own rider). A placeholder
        /// until players have a lasting identity (Steam ids, week 12).
        /// </summary>
        public static string IdForJoin(int joinIndex) => $"player{joinIndex + 1}";

        public void Set(string id, Vector3 position, float yaw) =>
            _places[id] = new PlayerSave { id = id, position = position, yaw = yaw };

        public bool TryGet(string id, out Vector3 position, out float yaw)
        {
            bool found = _places.TryGetValue(id, out PlayerSave place);
            position = found ? place.position : Vector3.zero;
            yaw = found ? place.yaw : 0f;
            return found;
        }

        /// <summary>Everyone, in id order, as the save file holds them.</summary>
        public List<PlayerSave> ToSave() => _places.Values
            .OrderBy(place => place.id, System.StringComparer.Ordinal)
            .Select(place => new PlayerSave { id = place.id, position = place.position, yaw = place.yaw })
            .ToList();
    }
}
