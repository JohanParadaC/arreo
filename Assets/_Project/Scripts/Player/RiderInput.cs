using Arreo.Horse;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Player
{
    /// <summary>
    /// Turns the local player's input into a <see cref="HorseIntent"/>, and says whether the
    /// player is holding the button that sings the canto (V, or Y on a gamepad).
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
        [Tooltip("Button held to sing the canto.")]
        [SerializeField] private InputActionReference _sing;

        private bool _slowWalkOn;

        public bool SlowWalkOn => _slowWalkOn;

        /// <summary>The player is holding the canto button and is in the saddle.</summary>
        public bool SingHeld { get; private set; }

        private void OnEnable()
        {
            _move.action.Enable();
            _gallop.action.Enable();
            _slowWalk.action.Enable();
            _sing?.action.Enable();
        }

        private void OnDisable() => SingHeld = false;

        private void Update()
        {
            if (_mount != null && !_mount.HasControl)
            {
                SingHeld = false;
                return;
            }

            if (_slowWalk.action.WasPressedThisFrame())
                _slowWalkOn = !_slowWalkOn;
            SingHeld = _sing != null && _sing.action.IsPressed();

            Vector2 move = _move.action.ReadValue<Vector2>();
            _horse.SetIntent(new HorseIntent(move.y, move.x, _gallop.action.IsPressed(), _slowWalkOn));
        }
    }
}
