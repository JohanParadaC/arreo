using Arreo.Horse;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Player
{
    /// <summary>
    /// Turns the local player's input into a <see cref="HorseIntent"/>. From week 5 this intent
    /// goes to the host instead of straight to the horse.
    /// </summary>
    public sealed class RiderInput : MonoBehaviour
    {
        [SerializeField] private HorseController _horse;
        [Tooltip("Vector2: y = forward/back, x = steer.")]
        [SerializeField] private InputActionReference _move;
        [Tooltip("Button held to gallop.")]
        [SerializeField] private InputActionReference _gallop;

        private void OnEnable()
        {
            _move.action.Enable();
            _gallop.action.Enable();
        }

        private void Update()
        {
            Vector2 move = _move.action.ReadValue<Vector2>();
            _horse.SetIntent(new HorseIntent(move.y, move.x, _gallop.action.IsPressed()));
        }
    }
}
