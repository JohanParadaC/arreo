using System;
using System.Text;

namespace Arreo.Voice
{
    /// <summary>
    /// The canto as it travels over the network: IMA ADPCM, 4 bits a sample instead of 16, so a
    /// 15 s canto is about 120 KB instead of 480 KB. Plain C#, no plugins. The audio is cut into
    /// blocks of <see cref="BlockSamples"/> samples; each block starts with its first sample as is,
    /// so a block never depends on the ones before it.
    /// Layout: "IMA4", sample rate (int32), sample count (int32), then the blocks; each block is the
    /// first sample (int16), the step index (byte), a zero byte, and one 4-bit code per later sample,
    /// low nibble first.
    /// </summary>
    public static class ImaAdpcm
    {
        public const int HeaderBytes = 12;
        public const int BlockBytes = 256;
        // 4 header bytes, then 252 bytes of 4-bit codes for the samples after the first.
        public const int BlockSamples = 1 + (BlockBytes - BlockHeaderBytes) * 2;

        private const int BlockHeaderBytes = 4;
        private const string Magic = "IMA4";

        private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

        private static readonly int[] StepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97,
            107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
            876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871,
            5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623,
            27086, 29794, 32767,
        };

        /// <summary>Size of <paramref name="sampleCount"/> samples once compressed.</summary>
        public static int EncodedBytes(int sampleCount)
        {
            int fullBlocks = sampleCount / BlockSamples;
            int rest = sampleCount % BlockSamples;
            return HeaderBytes + fullBlocks * BlockBytes + (rest > 0 ? BlockHeaderBytes + rest / 2 : 0);
        }

        public static byte[] Encode(float[] samples, int sampleRate)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Must be positive.");

            var data = new byte[EncodedBytes(samples.Length)];
            Encoding.ASCII.GetBytes(Magic, 0, 4, data, 0);
            WriteInt(data, 4, sampleRate);
            WriteInt(data, 8, samples.Length);

            int index = 0;
            int offset = HeaderBytes;
            for (int start = 0; start < samples.Length; start += BlockSamples)
            {
                int count = Math.Min(BlockSamples, samples.Length - start);
                int predicted = ToPcm(samples[start]);
                data[offset] = (byte)predicted;
                data[offset + 1] = (byte)(predicted >> 8);
                data[offset + 2] = (byte)index;
                int codes = offset + BlockHeaderBytes;
                for (int i = 1; i < count; i++)
                {
                    int code = EncodeSample(ToPcm(samples[start + i]), ref predicted, ref index);
                    int at = codes + (i - 1) / 2;
                    data[at] = (i - 1) % 2 == 0 ? (byte)code : (byte)(data[at] | code << 4);
                }
                offset += BlockHeaderBytes + count / 2;
            }
            return data;
        }

        /// <summary>
        /// Decodes a compressed canto. False (with a reason) for anything else, including a size that
        /// doesn't match its sample count: it came from another machine, so nothing is trusted as is.
        /// </summary>
        public static bool TryDecode(byte[] data, out float[] samples, out int sampleRate, out string problem)
        {
            samples = null;
            sampleRate = 0;
            if (data == null || data.Length < HeaderBytes || Encoding.ASCII.GetString(data, 0, 4) != Magic)
            {
                problem = "not a compressed canto";
                return false;
            }
            sampleRate = ReadInt(data, 4);
            int count = ReadInt(data, 8);
            if (sampleRate <= 0 || count <= 0)
            {
                problem = $"bad header ({sampleRate} Hz, {count} samples)";
                return false;
            }
            // Checked before allocating: the count can't ask for more samples than the bytes hold.
            if (count > (data.Length - HeaderBytes) * 2 || data.Length != EncodedBytes(count))
            {
                problem = $"{data.Length} bytes don't hold {count} samples";
                return false;
            }

            samples = new float[count];
            int offset = HeaderBytes;
            for (int start = 0; start < count; start += BlockSamples)
            {
                int blockCount = Math.Min(BlockSamples, count - start);
                int predicted = (short)(data[offset] | data[offset + 1] << 8);
                int index = data[offset + 2];
                if (index >= StepTable.Length)
                {
                    samples = null;
                    problem = $"bad step {index} in the block at byte {offset}";
                    return false;
                }
                samples[start] = predicted / 32767f;
                int codes = offset + BlockHeaderBytes;
                for (int i = 1; i < blockCount; i++)
                {
                    int packed = data[codes + (i - 1) / 2];
                    int code = (i - 1) % 2 == 0 ? packed & 0x0f : packed >> 4;
                    samples[start + i] = DecodeSample(code, ref predicted, ref index) / 32767f;
                }
                offset += BlockHeaderBytes + blockCount / 2;
            }
            problem = "";
            return true;
        }

        // Picks the 4-bit code that best follows the sample, and moves the predictor exactly as the decoder will.
        private static int EncodeSample(int sample, ref int predicted, ref int index)
        {
            int step = StepTable[index];
            int difference = sample - predicted;
            int code = 0;
            if (difference < 0)
            {
                code = 8;
                difference = -difference;
            }
            if (difference >= step)
            {
                code |= 4;
                difference -= step;
            }
            if (difference >= step >> 1)
            {
                code |= 2;
                difference -= step >> 1;
            }
            if (difference >= step >> 2)
                code |= 1;
            DecodeSample(code, ref predicted, ref index);
            return code;
        }

        private static int DecodeSample(int code, ref int predicted, ref int index)
        {
            int step = StepTable[index];
            int delta = step >> 3;
            if ((code & 4) != 0)
                delta += step;
            if ((code & 2) != 0)
                delta += step >> 1;
            if ((code & 1) != 0)
                delta += step >> 2;
            predicted += (code & 8) != 0 ? -delta : delta;
            predicted = Math.Max(-32767, Math.Min(32767, predicted));
            index = Math.Max(0, Math.Min(StepTable.Length - 1, index + IndexTable[code]));
            return predicted;
        }

        private static int ToPcm(float sample) => (int)Math.Round(Math.Max(-1f, Math.Min(1f, sample)) * 32767f);

        private static int ReadInt(byte[] bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;

        private static void WriteInt(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
