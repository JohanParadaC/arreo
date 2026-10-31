using System;
using System.IO;
using UnityEngine;

namespace Arreo.Voice
{
    /// <summary>
    /// This computer's player profile for the canto (rule 4): the recorded canto and the voice
    /// preferences, in <c>Application.persistentDataPath/profile</c> or the folder given with
    /// <c>-arreoProfileDir</c>. One per scene, reachable through <see cref="Instance"/> because the
    /// networked player prefab that sends and plays the canto can't hold a reference to a scene object.
    /// </summary>
    public sealed class VoiceProfile : MonoBehaviour
    {
        private const string FolderArgument = "-arreoProfileDir";

        private VoiceProfileStore _store;
        private VoicePrefs _prefs;

        public static VoiceProfile Instance { get; private set; }

        public string Folder => Store.Folder;

        public bool HasOwnCanto => Store.HasCanto;

        /// <summary>Don't play the other players' cantos here; the herd still hears them on the host.</summary>
        public bool MuteOtherCantos
        {
            get => Prefs.muteOtherCantos;
            set
            {
                if (Prefs.muteOtherCantos == value)
                    return;
                Prefs.muteOtherCantos = value;
                Store.SavePrefs(Prefs);
            }
        }

        /// <summary>Microphone to record with; empty = the system's default.</summary>
        public string Microphone
        {
            get => Prefs.microphone;
            set
            {
                value ??= "";
                if (Prefs.microphone == value)
                    return;
                Prefs.microphone = value;
                Store.SavePrefs(Prefs);
            }
        }

        private VoiceProfileStore Store => _store ??= new VoiceProfileStore(DefaultFolder());
        private VoicePrefs Prefs => _prefs ??= Store.LoadPrefs();

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Uses another profile folder from now on (tests use a temporary one, never the player's).</summary>
        public void UseFolder(string folder)
        {
            _store = new VoiceProfileStore(folder);
            _prefs = null;
        }

        /// <summary>This player's own canto as a WAV, or null if they never recorded one.</summary>
        public byte[] LoadOwnCanto() => Store.TryLoadCanto(out byte[] wav) ? wav : null;

        public void SaveOwnCanto(byte[] wav) => Store.SaveCanto(wav);

        /// <summary>Back to the game's fixed canto.</summary>
        public void DeleteOwnCanto() => Store.DeleteCanto();

        private static string DefaultFolder()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, FolderArgument);
            if (index >= 0 && index + 1 < args.Length)
                return args[index + 1];
            return Path.Combine(Application.persistentDataPath, "profile");
        }
    }
}
