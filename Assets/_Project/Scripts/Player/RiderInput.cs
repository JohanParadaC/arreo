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
        [Tooltip("While the rider is down they have no control.")]
        [SerializeField] private RiderMount _mount;
        [Tooltip("Vector2: y = forward/back, x = steer.")]
        [SerializeField] private InputActionReference _move;
        [Tooltip("Button held to gallop.")]
        [SerializeField] private InputActionReference _gallop;
        [Tooltip("Button that toggles the slow driving walk (arreo) on and off.")]
        [SerializeField] private InputActionReference _slowWalk;

        private bool _slowWalkOn;

        public bool SlowWalkOn => _slowWalkOn;

        private void OnEnable()
        {
            _move.action.Enable();
            _gallop.action.Enable();
            _slowWalk.action.Enable();
        }

        private void Update()
        {
            if (_mount != null && !_mount.HasControl)
                return;

            if (_slowWalk.action.WasPressedThisFrame())
                _slowWalkOn = !_slowWalkOn;

            Vector2 move = _move.action.ReadValue<Vector2>();
            _horse.SetIntent(new HorseIntent(move.y, move.x, _gallop.action.IsPressed(), _slowWalkOn));
        }
    }
}
