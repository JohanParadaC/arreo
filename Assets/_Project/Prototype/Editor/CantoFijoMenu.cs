using System.IO;
using Arreo.Voice;
using UnityEditor;
using UnityEngine;

namespace Arreo.Prototype.Editor
{
    /// <summary>
    /// Arreo > Voice > Use My Recorded Canto As The Fixed Canto: copies the canto recorded with
    /// "Grabar mi canto" (this computer's profile) over the game's fixed canto, the one players who
    /// never recorded theirs sing. The file keeps its name, so every reference to it stays.
    /// </summary>
    public static class CantoFijoMenu
    {
        [MenuItem("Arreo/Voice/Use My Recorded Canto As The Fixed Canto")]
        public static void UseRecordedCanto()
        {
            var store = new VoiceProfileStore(Path.Combine(Application.persistentDataPath, "profile"));
            if (!store.TryLoadCanto(out byte[] wav))
            {
                EditorUtility.DisplayDialog("Canto fijo", $"No recorded canto at {store.CantoPath}. Record one first: Play, then \"Grabar mi canto\".", "OK");
                return;
            }
            File.WriteAllBytes(ProtoSabanaBuilder.FixedCantoPath, wav);
            AssetDatabase.ImportAsset(ProtoSabanaBuilder.FixedCantoPath);
            Debug.Log($"[CantoFijoMenu] {store.CantoPath} is now the fixed canto ({ProtoSabanaBuilder.FixedCantoPath}).");
        }
    }
}
