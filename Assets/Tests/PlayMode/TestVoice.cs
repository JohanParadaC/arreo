using System;
using System.IO;
using Arreo.Voice;
using Object = UnityEngine.Object;

namespace Arreo.Tests.PlayMode
{
    /// <summary>
    /// Tests that host a game point the voice profile at a temporary folder first: otherwise the
    /// host would send and sing the developer's own recorded canto.
    /// </summary>
    internal static class TestVoice
    {
        public const int SampleRate = 16000;

        public static string NewFolder() => Path.Combine(Path.GetTempPath(), "ArreoTests", "profiles", Path.GetRandomFileName());

        /// <summary>Makes the loaded scene's voice profile use <paramref name="folder"/> (a new, empty temporary one if null); returns it.</summary>
        public static string UseFolder(string folder = null)
        {
            folder ??= NewFolder();
            Object.FindFirstObjectByType<VoiceProfile>().UseFolder(folder);
            return folder;
        }

        /// <summary>Records a test canto (a hummed tone) into the profile at <paramref name="folder"/>; returns its WAV.</summary>
        public static byte[] WriteCanto(string folder, float seconds, float hz)
        {
            var samples = new float[(int)(seconds * SampleRate)];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = 0.5f * (float)Math.Sin(2.0 * Math.PI * hz * i / SampleRate);
            byte[] wav = WavCodec.Encode(samples, SampleRate);
            new VoiceProfileStore(folder).SaveCanto(wav);
            return wav;
        }

        /// <summary>A recorded canto as it travels over the network (IMA ADPCM), to compare with what arrived.</summary>
        public static byte[] Compressed(byte[] wav)
        {
            WavCodec.TryDecode(wav, out float[] samples, out int rate, out _);
            return ImaAdpcm.Encode(samples, rate);
        }

        public static void Delete(string folder)
        {
            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
