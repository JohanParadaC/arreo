using Arreo.Horse;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 2 prototype): debug readout of speed and stamina, plus cursor lock
    /// for the orbit camera. Replace with the real UI later.
    /// </summary>
    public sealed class ProtoHud : MonoBehaviour
    {
        [SerializeField] private HorseController _horse;
        [SerializeField] private ProtoDrive _drive;
        [SerializeField] private ProtoGate _gate;

        private GUIStyle _style;

        private void Start() => LockCursor(true);

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                LockCursor(false);
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                LockCursor(true);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnGUI()
        {
            HorseLocomotion locomotion = _horse != null ? _horse.Locomotion : null;
            if (locomotion == null)
                return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };
            HorseStamina stamina = locomotion.Stamina;
            string gait = locomotion.IsGalloping ? "GALOPE" : stamina.IsExhausted ? "AGOTADO" : "paso";

            GUI.Box(new Rect(10, 10, 280, 110), GUIContent.none);
            GUI.Label(new Rect(20, 15, 260, 25), $"{locomotion.Speed * 3.6f:0} km/h  ({gait})", _style);
            GUI.Label(new Rect(20, 42, 260, 25), "Aguante", _style);
            GUI.color = stamina.IsExhausted ? Color.red : Color.green;
            GUI.DrawTexture(new Rect(100, 47, 180 * stamina.Normalized, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 72, 270, 45), "WASD/stick: riendas · Shift: galopar\nRatón: cámara · Esc: soltar cursor");

            DrawDrive();
        }

        private void DrawDrive()
        {
            var drive = _drive != null ? _drive.Drive : null;
            if (drive == null)
                return;

            int minutes = (int)(drive.ElapsedSeconds / 60f);
            float seconds = drive.ElapsedSeconds % 60f;
            string clock = $"{minutes}:{seconds:00.0}";

            GUI.Box(new Rect(10, 125, 280, 85), GUIContent.none);
            GUI.Label(new Rect(20, 130, 260, 25), $"Reses en el corral: {drive.InCorral}/{drive.Total}", _style);
            string status = drive.IsComplete ? $"¡Arreo completo en {clock}!" : drive.HasStarted ? $"Tiempo: {clock}" : "Tiempo: arranca al moverte";
            GUI.Label(new Rect(20, 155, 260, 25), status, _style);
            if (_gate != null && _gate.RiderInReach)
                GUI.Label(new Rect(20, 182, 260, 25), $"E: {(_gate.IsOpen ? "cerrar" : "abrir")} la puerta");
        }
    }
}
