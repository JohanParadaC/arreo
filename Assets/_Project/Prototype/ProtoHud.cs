using Arreo.Economy;
using Arreo.Horse;
using Arreo.Net;
using Arreo.Player;
using Arreo.Rope;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (weeks 2-7 prototype): debug readout of speed and stamina, the corral, the rope,
    /// the fair and the hato's money, plus cursor lock for the orbit camera. Replace with the real UI later.
    /// </summary>
    public sealed class ProtoHud : MonoBehaviour
    {
        [SerializeField] private HorseController _horse;
        [SerializeField] private NetworkWorld _world;
        [SerializeField] private ProtoFair _fair;
        [SerializeField] private NetworkEconomy _economy;
        [SerializeField] private NetworkClock _clock;
        [SerializeField] private NetworkSave _save;
        [SerializeField] private LassoInput _lasso;
        [SerializeField] private RiderMount _mount;
        [SerializeField] private RiderInput _riderInput;

        private IRopeSnapshotSource _rope;

        private GUIStyle _style;
        private GUIStyle _banner;

        /// <summary>Points the HUD at this machine's rider once it spawns, and captures the cursor.</summary>
        public void Bind(HorseController horse, LassoInput lasso, RiderMount mount, RiderInput riderInput, IRopeSnapshotSource rope)
        {
            _horse = horse;
            _lasso = lasso;
            _mount = mount;
            _riderInput = riderInput;
            _rope = rope;
            LockCursor(true);
        }

        private void Update()
        {
            // Before joining, the cursor stays free for the network menu.
            if (_horse == null)
                return;
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
            bool driving = locomotion.IsDriving || (_riderInput != null && _riderInput.SlowWalkOn);
            string gait = locomotion.IsGalloping ? "GALOPE" : stamina.IsExhausted ? "AGOTADO" : driving ? "arreo" : "paso";

            GUI.Box(new Rect(10, 10, 300, 125), GUIContent.none);
            GUI.Label(new Rect(20, 15, 280, 25), $"{locomotion.Speed * 3.6f:0} km/h  ({gait})", _style);
            GUI.Label(new Rect(20, 42, 280, 25), "Aguante", _style);
            GUI.color = stamina.IsExhausted ? Color.red : Color.green;
            GUI.DrawTexture(new Rect(100, 47, 190 * stamina.Normalized, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 70, 290, 62),
                "WASD: riendas · Shift: galopar · C: arreo on/off\nClic der: apuntar · Clic izq: lanzar · Q: soltar\nB: trueno · R: reponer · F5/F9: guardar/cargar");

            DrawDrive();
            DrawRope();
            DrawMoneyAndFair();
            DrawCrosshairAndBanner();
        }

        private void DrawMoneyAndFair()
        {
            // The host keeps the money and counts the fair pen; every machine shows the same numbers.
            if (_economy == null || !_economy.IsSpawned)
                return;

            var box = new Rect(Screen.width - 330, 40, 320, 110);
            GUI.Box(box, GUIContent.none);
            if (_clock != null && _clock.IsSpawned)
            {
                float hour = _clock.Hour;
                GUI.Label(new Rect(box.x + 10, box.y + 5, 300, 25), $"Día {_clock.Day} · {(int)hour:00}:{(int)(hour % 1f * 60f):00}", _style);
            }
            GUI.Label(new Rect(box.x + 10, box.y + 30, 300, 25), $"Plata del hato: {Money.Format(_economy.HatoBalance)}", _style);
            DrawSaveNotice();
            if (_fair == null || !_fair.IsSpawned)
                return;
            GUI.Label(new Rect(box.x + 10, box.y + 57, 300, 25),
                $"Feria: {Head(_fair.InPen)} en el corral · {Money.Format(_fair.PricePerHead)} c/u");
            if (_fair.LocalRiderAtSalePoint)
                GUI.Label(new Rect(box.x + 10, box.y + 80, 300, 25),
                    _fair.InPen > 0 ? $"E: vender {Head(_fair.InPen)} por {Money.Format(_fair.InPen * _fair.PricePerHead)}" : "Mete reses al corral para vender");

            if (_fair.LastSaleTime >= 0f && Time.time - _fair.LastSaleTime < 4f)
            {
                _banner ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                GUI.color = new Color(0.5f, 1f, 0.4f);
                string sold = _fair.LastSaleHead == 1 ? "¡Vendida 1 res!" : $"¡Vendidas {_fair.LastSaleHead} reses!";
                GUI.Label(new Rect(0f, 150f, Screen.width, 50f), $"{sold} +{Money.Format(_fair.LastSaleTotal)}", _banner);
                GUI.color = Color.white;
            }
        }

        private static string Head(int count) => count == 1 ? "1 res" : $"{count} reses";

        // Only the host saves, so only it shows what saving said (a broken save explains itself there).
        private void DrawSaveNotice()
        {
            if (_save == null || _save.NoticeTime < 0f || Time.time - _save.NoticeTime > 8f)
                return;
            var area = new Rect(Screen.width - 530, 155, 520, 60);
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x + 10, area.y + 5, area.width - 20, area.height - 10), _save.Notice);
        }

        private void DrawRope()
        {
            if (_rope == null || !_rope.IsConfigured)
                return;

            RopeSnapshot rope = _rope.Snapshot;
            string state = rope.State switch
            {
                RopeState.Coiled => "enrollada",
                RopeState.Throwing => "lanzando…",
                RopeState.Lassoed => "enlazada",
                RopeState.Taut => "TENSA",
                RopeState.Released => "suelta",
                RopeState.Broken => "¡REVENTÓ!",
                _ => "",
            };
            float tension01 = rope.Tension01;

            GUI.Box(new Rect(10, 230, 300, 80), GUIContent.none);
            GUI.Label(new Rect(20, 235, 280, 25), $"Soga: {state}  {tension01 * 100f:0} %", _style);
            GUI.color = Color.Lerp(Color.green, Color.red, tension01);
            GUI.DrawTexture(new Rect(20, 263, 280 * Mathf.Clamp01(tension01), 12), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.3f, 0.1f);
            GUI.DrawTexture(new Rect(20, 280, 280 * rope.BreakProgress, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 288, 280, 20), "tensión · se revienta");
        }

        private void DrawCrosshairAndBanner()
        {
            if (_lasso != null && _lasso.IsAiming)
            {
                float cx = Screen.width / 2f;
                float cy = Screen.height / 2f;
                GUI.DrawTexture(new Rect(cx - 8f, cy - 1f, 16f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1f, cy - 8f, 2f, 16f), Texture2D.whiteTexture);
            }

            _banner ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (_world != null && _world.StampedeIntensity > 0f)
            {
                GUI.color = new Color(1f, 0.85f, 0.2f, Mathf.Clamp01(_world.StampedeIntensity * 2f));
                GUI.Label(new Rect(0f, 40f, Screen.width, 50f), "¡BARAJUSTADA!", _banner);
            }
            if (_mount != null && _mount.State != RiderState.Mounted)
            {
                GUI.color = new Color(1f, 0.4f, 0.3f);
                GUI.Label(new Rect(0f, 95f, Screen.width, 50f),
                    _mount.State == RiderState.Fallen ? "¡AL SUELO!" : "volviendo a montar…", _banner);
            }
            GUI.color = Color.white;
        }

        private void DrawDrive()
        {
            // The host counts the corral and runs the clock; every machine shows the same numbers.
            if (_world == null || !_world.HasDrive)
                return;

            int minutes = (int)(_world.Elapsed / 60f);
            float seconds = _world.Elapsed % 60f;
            string clock = $"{minutes}:{seconds:00}";

            GUI.Box(new Rect(10, 140, 300, 85), GUIContent.none);
            GUI.Label(new Rect(20, 145, 280, 25), $"Reses en el corral: {_world.InCorral}/{_world.Total}", _style);
            string status = _world.Complete ? $"¡Arreo completo en {clock}!" : _world.Started ? $"Tiempo: {clock}" : "Tiempo: arranca al moverse";
            GUI.Label(new Rect(20, 170, 280, 25), status, _style);
            if (_world.LocalRiderNearGate)
                GUI.Label(new Rect(20, 197, 280, 25), $"E: {(_world.GateOpen ? "cerrar" : "abrir")} la puerta");
        }
    }
}
