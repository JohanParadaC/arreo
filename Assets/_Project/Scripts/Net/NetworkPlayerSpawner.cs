using Arreo.Core;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// On the host: gives every player who joins their own horse, owned by them, laid out side
    /// by side with <see cref="SpawnLayout"/>, or where they were if the game was continued.
    /// Players are named by the order they join (<see cref="PlayerPlaces.IdForJoin"/>).
    /// </summary>
    public sealed class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private NetworkObject _playerPrefab;
        [Tooltip("Where continued games keep the players' places. Optional.")]
        [SerializeField] private NetworkSave _save;
        [SerializeField] private Vector3 _origin = Vector3.zero;
        [Tooltip("Distance between players' horses when they appear (m).")]
        [SerializeField, Min(1f)] private float _spacing = 4f;

        private int _spawned;

        // Start, not OnEnable: the NetworkManager creates its SceneManager in its own Awake.
        private void Start()
        {
            _networkManager.SceneManager.OnClientLoadedStartScenes += SpawnFor;
            _networkManager.ServerManager.OnServerConnectionState += _ => _spawned = 0;
        }

        private void OnDestroy()
        {
            if (_networkManager != null && _networkManager.SceneManager != null)
                _networkManager.SceneManager.OnClientLoadedStartScenes -= SpawnFor;
        }

        private void SpawnFor(NetworkConnection connection, bool asServer)
        {
            if (!asServer)
                return;

            int joinIndex = _spawned++;
            string playerId = PlayerPlaces.IdForJoin(joinIndex);
            Vector3 position = SpawnLayout.PositionFor(joinIndex, _origin, _spacing);
            float yaw = 0f;
            if (_save != null && _save.TryGetPlace(playerId, out Vector3 savedPosition, out float savedYaw))
            {
                position = savedPosition;
                yaw = savedYaw;
            }

            NetworkObject rider = _networkManager.GetPooledInstantiated(_playerPrefab, position, Quaternion.Euler(0f, yaw, 0f), true);
            rider.GetComponent<NetworkRider>().ServerPlayerId = playerId;
            _networkManager.ServerManager.Spawn(rider, connection);
        }
    }
}
