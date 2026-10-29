using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// On the host: gives every player who joins their own horse, owned by them, laid out side
    /// by side with <see cref="SpawnLayout"/>.
    /// </summary>
    public sealed class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private Vector3 _origin = Vector3.zero;
        [Tooltip("Distance between players' horses when they appear (m).")]
        [SerializeField, Min(1f)] private float _spacing = 4f;

        private int _spawned;

        // Start, not OnEnable: the NetworkManager creates its SceneManager in its own Awake.
        private void Start() => _networkManager.SceneManager.OnClientLoadedStartScenes += SpawnFor;

        private void OnDestroy()
        {
            if (_networkManager != null && _networkManager.SceneManager != null)
                _networkManager.SceneManager.OnClientLoadedStartScenes -= SpawnFor;
        }

        private void SpawnFor(NetworkConnection connection, bool asServer)
        {
            if (!asServer)
                return;

            Vector3 position = SpawnLayout.PositionFor(_spawned++, _origin, _spacing);
            NetworkObject rider = _networkManager.GetPooledInstantiated(_playerPrefab, position, Quaternion.identity, true);
            _networkManager.ServerManager.Spawn(rider, connection);
        }
    }
}
