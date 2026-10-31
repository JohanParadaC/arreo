using System;
using System.IO;
using UnityEngine;

namespace Arreo.Voice
{
    /// <summary>This player's voice preferences, kept on this computer only (JSON, versioned like the save).</summary>
    [Serializable]
    public sealed class VoicePrefs
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        /// <summary>Don't play the other players' cantos on this computer (the herd still hears them).</summary>
        public bool muteOtherCantos;
        /// <summary>Microphone to record with; empty = the system's default.</summary>
        public string microphone = "";
    }

    /// <summary>
    /// The local player profile's voice files (rule 4): the recorded canto (<c>canto.wav</c>) and
    /// the voice preferences (<c>voice.json</c>). Nothing here leaves this computer; the game only
    /// sends the canto to the players of a game it joins.
    /// </summary>
    public sealed class VoiceProfileStore
    {
        public const string CantoFileName = "canto.wav";
        public const string PrefsFileName = "voice.json";

        public VoiceProfileStore(string folder)
        {
            Folder = folder;
        }

        public string Folder { get; }
        public string CantoPath => Path.Combine(Folder, CantoFileName);
        private string PrefsPath => Path.Combine(Folder, PrefsFileName);

        public bool HasCanto => File.Exists(CantoPath);

        public bool TryLoadCanto(out byte[] wav)
        {
            wav = null;
            if (!HasCanto)
                return false;
            try
            {
                wav = File.ReadAllBytes(CantoPath);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public void SaveCanto(byte[] wav) => WriteAtomically(CantoPath, wav);

        public void DeleteCanto()
        {
            if (HasCanto)
                File.Delete(CantoPath);
        }

        /// <summary>The saved preferences, or the defaults if there are none or the file is unreadable.</summary>
        public VoicePrefs LoadPrefs()
        {
            try
            {
                if (File.Exists(PrefsPath))
                {
                    VoicePrefs prefs = JsonUtility.FromJson<VoicePrefs>(File.ReadAllText(PrefsPath));
                    if (prefs != null)
                    {
                        prefs.microphone ??= "";
                        return prefs;
                    }
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException)
            {
                Debug.LogWarning($"[VoiceProfileStore] Ignoring unreadable {PrefsPath}: {exception.Message}");
            }
            return new VoicePrefs();
        }

        public void SavePrefs(VoicePrefs prefs)
        {
            prefs.version = VoicePrefs.CurrentVersion;
            WriteAtomically(PrefsPath, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(prefs, prettyPrint: true)));
        }

        // Written next to the old file and swapped in, so a crash mid-write never leaves half a file.
        private void WriteAtomically(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Folder);
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
    }
}
