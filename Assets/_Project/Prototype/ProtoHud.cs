using Arreo.Herd;
using Arreo.Horse;
using Arreo.Rope;
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
        [SerializeField] private RopeSimulation _rope;
        [SerializeField] private ActiveHerd _herd;

        private GUIStyle _style;
        private GUIStyle _banner;

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
            string gait = locomotion.IsGalloping ? "GALOPE" : stamina.IsExhausted ? "AGOTADO" : locomotion.IsDriving ? "arreo" : "paso";

            GUI.Box(new Rect(10, 10, 300, 125), GUIContent.none);
            GUI.Label(new Rect(20, 15, 280, 25), $"{locomotion.Speed * 3.6f:0} km/h  ({gait})", _style);
            GUI.Label(new Rect(20, 42, 280, 25), "Aguante", _style);
            GUI.color = stamina.IsExhausted ? Color.red : Color.green;
            GUI.DrawTexture(new Rect(100, 47, 190 * stamina.Normalized, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 70, 290, 62),
                "WASD: riendas · Shift: galopar · Ctrl: arreo\nClic izq: lanzar soga · Clic der: soltarla\nB: trueno · Ratón: cámara · Esc: cursor");

            DrawDrive();
            DrawRope();
            DrawCrosshairAndBanner();
        }

        private void DrawRope()
        {
            if (_rope == null)
                return;

            string state = _rope.State switch
            {
                RopeState.Coiled => "enrollada",
                RopeState.Throwing => "lanzando…",
                RopeState.Lassoed => "enlazada",
                RopeState.Taut => "TENSA",
                RopeState.Released => "suelta",
                RopeState.Broken => "¡REVENTÓ!",
                _ => "",
            };
            float tension01 = _rope.Snapshot.Tension01;

            GUI.Box(new Rect(10, 230, 300, 80), GUIContent.none);
            GUI.Label(new Rect(20, 235, 280, 25), $"Soga: {state}  {_rope.Tension:0} N", _style);
            GUI.color = Color.Lerp(Color.green, Color.red, tension01);
            GUI.DrawTexture(new Rect(20, 263, 280 * Mathf.Clamp01(tension01), 12), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.3f, 0.1f);
            GUI.DrawTexture(new Rect(20, 280, 280 * _rope.BreakProgress, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 288, 280, 20), "tensión · se revienta");
        }

        private void DrawCrosshairAndBanner()
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;
            GUI.DrawTexture(new Rect(cx - 8f, cy - 1f, 16f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 8f, 2f, 16f), Texture2D.whiteTexture);

            if (_herd != null && _herd.StampedeIntensity > 0f)
            {
                _banner ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                GUI.color = new Color(1f, 0.85f, 0.2f, Mathf.Clamp01(_herd.StampedeIntensity * 2f));
                GUI.Label(new Rect(0f, 40f, Screen.width, 50f), "¡BARAJUSTADA!", _banner);
                GUI.color = Color.white;
            }
        }

        private void DrawDrive()
        {
            var drive = _drive != null ? _drive.Drive : null;
            if (drive == null)
                return;

            int minutes = (int)(drive.ElapsedSeconds / 60f);
            float seconds = drive.ElapsedSeconds % 60f;
            string clock = $"{minutes}:{seconds:00.0}";

            GUI.Box(new Rect(10, 140, 300, 85), GUIContent.none);
            GUI.Label(new Rect(20, 145, 280, 25), $"Reses en el corral: {drive.InCorral}/{drive.Total}", _style);
            string status = drive.IsComplete ? $"¡Arreo completo en {clock}!" : drive.HasStarted ? $"Tiempo: {clock}" : "Tiempo: arranca al moverte";
            GUI.Label(new Rect(20, 170, 280, 25), status, _style);
            if (_gate != null && _gate.RiderInReach)
                GUI.Label(new Rect(20, 197, 280, 25), $"E: {(_gate.IsOpen ? "cerrar" : "abrir")} la puerta");
        }
    }
}
