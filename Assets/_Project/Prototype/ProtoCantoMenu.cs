using System.Collections.Generic;
using Arreo.Voice;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 10 prototype): "Grabar mi canto". Pick a microphone, record 5 to 15 s,
    /// listen to the take and keep it or try again; it's saved in this computer's profile and sent
    /// to the players of the next game this player joins. Without a recording the game sings its
    /// fixed canto. Opened from <see cref="ProtoNetworkMenu"/>; replace with the real UI later.
    /// </summary>
    public sealed class ProtoCantoMenu : MonoBehaviour
    {
        public const string PrivacyNotice =
            "Tu canto se guarda solo en este PC. Se comparte con los jugadores de cada partida en la que entres.";

        [SerializeField] private CantoRecorder _recorder;
        [SerializeField] private VoiceProfile _profile;

        private enum Step { Closed, Main, Recording, Review }

        private Step _step = Step.Closed;
        private CantoTake _take;
        private string _message = "";
        private byte[] _ownCanto;
        private GUIStyle _title;
        private GUIStyle _wrap;

        public bool IsOpen => _step != Step.Closed;

        public void Open()
        {
            _step = Step.Main;
            _message = "";
            _ownCanto = _profile.LoadOwnCanto();
        }

        private void Close()
        {
            _recorder.Cancel();
            _recorder.StopPreview();
            _take = null;
            _step = Step.Closed;
        }

        private void Update()
        {
            // At 15 s the take is as long as a canto can be: go straight to listening to it.
            if (_step == Step.Recording && _recorder.ReachedLongest)
                FinishRecording();
        }

        private void OnGUI()
        {
            if (_step == Step.Closed)
                return;
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

            var panel = new Rect(Screen.width / 2f - 260, Screen.height / 2f - 200, 520, 400);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x, panel.y + 10, panel.width, 36), "Mi canto", _title);
            var area = new Rect(panel.x + 30, panel.y + 60, panel.width - 60, 270);
            switch (_step)
            {
                case Step.Main:
                    DrawMain(area);
                    break;
                case Step.Recording:
                    DrawRecording(area);
                    break;
                case Step.Review:
                    DrawReview(area);
                    break;
            }
            GUI.Label(new Rect(panel.x + 30, panel.yMax - 60, panel.width - 60, 50), PrivacyNotice, _wrap);
        }

        private void DrawMain(Rect area)
        {
            string status = _ownCanto != null
                ? $"Tienes tu canto grabado ({Seconds(_ownCanto):0.0} s). Suena cuando mantienes V (Y en el control)."
                : "No has grabado tu canto: mientras tanto suena el canto fijo del juego.";
            GUI.Label(new Rect(area.x, area.y, area.width, 40), status, _wrap);

            IReadOnlyList<string> devices = _recorder.Devices;
            float y = area.y + 50;
            if (devices.Count == 0)
            {
                GUI.Label(new Rect(area.x, y, area.width, 40),
                    "No hay ningún micrófono. Conecta uno, o revisa en Windows que las apps de escritorio puedan usarlo.", _wrap);
            }
            else
            {
                int current = Mathf.Max(0, IndexOf(devices, _profile.Microphone));
                GUI.Label(new Rect(area.x, y, 90, 25), "Micrófono:");
                if (devices.Count > 1 && GUI.Button(new Rect(area.x + 90, y, 30, 25), "<"))
                    current = (current + devices.Count - 1) % devices.Count;
                GUI.Label(new Rect(area.x + 125, y, area.width - 165, 25), devices[current]);
                if (devices.Count > 1 && GUI.Button(new Rect(area.xMax - 30, y, 30, 25), ">"))
                    current = (current + 1) % devices.Count;
                _profile.Microphone = devices[current];
            }

            y += 45;
            GUI.enabled = devices.Count > 0;
            if (GUI.Button(new Rect(area.x, y, area.width, 40), _ownCanto != null ? "Grabar otra vez (de 5 a 15 s)" : "Grabar (de 5 a 15 s)"))
                StartRecording();
            GUI.enabled = true;
            y += 48;
            if (_ownCanto != null)
            {
                float half = (area.width - 10) / 2f;
                if (GUI.Button(new Rect(area.x, y, half, 36), _recorder.IsPreviewing ? "Parar" : "Escuchar mi canto"))
                    TogglePreview(_ownCanto);
                if (GUI.Button(new Rect(area.x + half + 10, y, half, 36), "Borrar (usar el fijo)"))
                {
                    _recorder.StopPreview();
                    _profile.DeleteOwnCanto();
                    _ownCanto = null;
                    _message = "Borrado: sonará el canto fijo del juego.";
                }
            }
            y += 44;
            GUI.Label(new Rect(area.x, y, area.width, 25), _message);
            if (GUI.Button(new Rect(area.x, area.yMax - 36, area.width, 36), "Volver"))
                Close();
        }

        private void DrawRecording(Rect area)
        {
            float seconds = _recorder.RecordedSeconds;
            float max = _recorder.Settings.MaxSeconds;
            GUI.Label(new Rect(area.x, area.y, area.width, 25), $"Grabando… {seconds:0.0} s (de 5 a {max:0} s). ¡Canta!");
            GUI.Box(new Rect(area.x, area.y + 35, area.width, 18), GUIContent.none);
            GUI.color = new Color(0.9f, 0.3f, 0.2f);
            GUI.DrawTexture(new Rect(area.x, area.y + 35, area.width * Mathf.Clamp01(seconds / max), 18), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(Color.green, Color.red, _recorder.Level);
            GUI.DrawTexture(new Rect(area.x, area.y + 60, area.width * Mathf.Clamp01(_recorder.Level), 10), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(area.x, area.y + 72, area.width, 20), "volumen del micrófono");

            GUI.enabled = _recorder.CanStop;
            if (GUI.Button(new Rect(area.x, area.y + 110, area.width, 44), _recorder.CanStop ? "Parar" : "Parar (desde los 5 s)"))
                FinishRecording();
            GUI.enabled = true;
            if (GUI.Button(new Rect(area.x, area.yMax - 36, area.width, 36), "Cancelar"))
            {
                _recorder.Cancel();
                _step = Step.Main;
            }
        }

        private void DrawReview(Rect area)
        {
            float half = (area.width - 10) / 2f;
            if (_take.Status != CantoTakeStatus.Ok)
            {
                string why = _take.Status == CantoTakeStatus.Silent
                    ? "No se oyó nada. Revisa que el micrófono no esté silenciado y acércalo."
                    : $"Quedó muy corto ({_take.Seconds:0.0} s): el canto dura de 5 a 15 s.";
                GUI.Label(new Rect(area.x, area.y, area.width, 50), why, _wrap);
                if (GUI.Button(new Rect(area.x, area.y + 70, area.width, 44), "Repetir"))
                    StartRecording();
                if (GUI.Button(new Rect(area.x, area.yMax - 36, area.width, 36), "Volver"))
                    _step = Step.Main;
                return;
            }

            GUI.Label(new Rect(area.x, area.y, area.width, 50), $"Toma de {_take.Seconds:0.0} s. Escúchala y decide si la guardas.", _wrap);
            if (GUI.Button(new Rect(area.x, area.y + 55, area.width, 40), _recorder.IsPreviewing ? "Parar" : "Escuchar"))
                TogglePreview(_take.Wav);
            if (GUI.Button(new Rect(area.x, area.y + 105, half, 44), "Aceptar"))
            {
                _recorder.StopPreview();
                _profile.SaveOwnCanto(_take.Wav);
                _ownCanto = _take.Wav;
                _take = null;
                _message = "Guardado. Se enviará a los jugadores de tu próxima partida.";
                _step = Step.Main;
            }
            if (GUI.Button(new Rect(area.x + half + 10, area.y + 105, half, 44), "Repetir"))
                StartRecording();
            if (GUI.Button(new Rect(area.x, area.yMax - 36, area.width, 36), "Descartar"))
            {
                _recorder.StopPreview();
                _take = null;
                _step = Step.Main;
            }
        }

        private void StartRecording()
        {
            _take = null;
            if (_recorder.Begin(_profile.Microphone))
                _step = Step.Recording;
            else
            {
                _message = "No se pudo abrir el micrófono.";
                _step = Step.Main;
            }
        }

        private void FinishRecording()
        {
            _take = _recorder.Finish();
            _step = Step.Review;
        }

        private void TogglePreview(byte[] wav)
        {
            if (_recorder.IsPreviewing)
                _recorder.StopPreview();
            else
                _recorder.Preview(wav);
        }

        // Our own WAVs: a 44-byte header, then 2 bytes per sample.
        private float Seconds(byte[] wav) => (wav.Length - WavCodec.HeaderBytes) / 2f / _recorder.Settings.SampleRate;

        private static int IndexOf(IReadOnlyList<string> devices, string name)
        {
            for (int i = 0; i < devices.Count; i++)
                if (devices[i] == name)
                    return i;
            return -1;
        }
    }
}
