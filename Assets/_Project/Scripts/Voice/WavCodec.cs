using System;
using System.Text;

namespace Arreo.Voice
{
    /// <summary>
    /// Reads and writes the canto's file format (rule 4): a WAV with 16-bit PCM samples. Writes
    /// mono; reads mono or stereo (mixed down to mono) and skips chunks it doesn't need, so a WAV
    /// from another program loads too. Samples are floats from -1 to 1, like an AudioClip's.
    /// </summary>
    public static class WavCodec
    {
        /// <summary>Size of the header <see cref="Encode"/> writes before the samples.</summary>
        public const int HeaderBytes = 44;

        private const short Pcm = 1;
        private const short Bits = 16;

        public static byte[] Encode(float[] samples, int sampleRate)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Must be positive.");

            int dataBytes = samples.Length * 2;
            var wav = new byte[HeaderBytes + dataBytes];
            WriteTag(wav, 0, "RIFF");
            WriteInt(wav, 4, wav.Length - 8);
            WriteTag(wav, 8, "WAVE");
            WriteTag(wav, 12, "fmt ");
            WriteInt(wav, 16, 16);
            WriteShort(wav, 20, Pcm);
            WriteShort(wav, 22, 1);
            WriteInt(wav, 24, sampleRate);
            WriteInt(wav, 28, sampleRate * 2);
            WriteShort(wav, 32, 2);
            WriteShort(wav, 34, Bits);
            WriteTag(wav, 36, "data");
            WriteInt(wav, 40, dataBytes);
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = samples[i] < -1f ? -1f : samples[i] > 1f ? 1f : samples[i];
                WriteShort(wav, HeaderBytes + i * 2, (short)Math.Round(clamped * 32767f));
            }
            return wav;
        }

        /// <summary>
        /// Decodes a 16-bit PCM WAV, mono or stereo. False (with a reason) for anything else,
        /// including a file cut short: a canto received over the network is never trusted as is.
        /// </summary>
        public static bool TryDecode(byte[] wav, out float[] samples, out int sampleRate, out string problem)
        {
            samples = null;
            sampleRate = 0;
            if (wav == null || wav.Length < 12 || Tag(wav, 0) != "RIFF" || Tag(wav, 8) != "WAVE")
            {
                problem = "not a WAV file";
                return false;
            }

            int channels = 0;
            bool hasFormat = false;
            int offset = 12;
            while (offset + 8 <= wav.Length)
            {
                string id = Tag(wav, offset);
                int size = ReadInt(wav, offset + 4);
                int body = offset + 8;
                if (size < 0 || body + (long)size > wav.Length)
                {
                    problem = $"the '{id.Trim()}' chunk is cut short";
                    return false;
                }

                if (id == "fmt ")
                {
                    if (size < 16)
                    {
                        problem = "the format chunk is too small";
                        return false;
                    }
                    short format = ReadShort(wav, body);
                    channels = ReadShort(wav, body + 2);
                    sampleRate = ReadInt(wav, body + 4);
                    short bits = ReadShort(wav, body + 14);
                    if (format != Pcm || bits != Bits)
                    {
                        problem = $"only 16-bit PCM is supported (format {format}, {bits} bits)";
                        return false;
                    }
                    if (channels != 1 && channels != 2)
                    {
                        problem = $"only mono or stereo is supported ({channels} channels)";
                        return false;
                    }
                    if (sampleRate <= 0)
                    {
                        problem = $"bad sample rate {sampleRate}";
                        return false;
                    }
                    hasFormat = true;
                }
                else if (id == "data")
                {
                    if (!hasFormat)
                    {
                        problem = "the samples come before the format";
                        return false;
                    }
                    samples = ReadSamples(wav, body, size, channels);
                    problem = "";
                    return true;
                }
                // Chunks are padded to an even size.
                offset = body + size + (size & 1);
            }

            problem = hasFormat ? "no samples in the file" : "no format chunk in the file";
            return false;
        }

        private static float[] ReadSamples(byte[] wav, int offset, int size, int channels)
        {
            int frames = size / (2 * channels);
            var samples = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += ReadShort(wav, offset + (f * channels + c) * 2) / 32767f;
                samples[f] = sum / channels;
            }
            return samples;
        }

        private static string Tag(byte[] bytes, int offset) => Encoding.ASCII.GetString(bytes, offset, 4);

        private static void WriteTag(byte[] bytes, int offset, string tag) => Encoding.ASCII.GetBytes(tag, 0, 4, bytes, offset);

        private static int ReadInt(byte[] bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;

        private static short ReadShort(byte[] bytes, int offset) => (short)(bytes[offset] | bytes[offset + 1] << 8);

        private static void WriteInt(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteShort(byte[] bytes, int offset, short value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }
    }
}
