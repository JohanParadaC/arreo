using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// Packs the herd into a snapshot: a 3-byte header (cow count and restock generation), then
    /// 6 bytes per cow (x and z in centimetres as int16, ±327 m; height in decimetres as a byte,
    /// 0-25.5 m; heading in 256 steps) and one bit per cow saying whether it's still in the herd
    /// (a sold cow keeps its index, so network ids don't shift). 50 cows = 310 bytes; at 10
    /// snapshots a second that's about 3 KB/s per client.
    /// </summary>
    public static class HerdSnapshotCodec
    {
        private const int HeaderBytes = 3;
        private const int BytesPerCow = 6;

        /// <param name="generation">Goes up every time the herd is restocked, so clients stop blending with the old herd.</param>
        public static byte[] Encode(IReadOnlyList<Vector3> positions, IReadOnlyList<float> yaws, IReadOnlyList<bool> present, byte generation)
        {
            int count = positions.Count;
            var data = new byte[HeaderBytes + count * BytesPerCow + (count + 7) / 8];
            data[0] = (byte)(count & 0xFF);
            data[1] = (byte)(count >> 8);
            data[2] = generation;

            int offset = HeaderBytes;
            for (int i = 0; i < count; i++)
            {
                WriteShort(data, ref offset, Quantize(positions[i].x, 100f));
                WriteShort(data, ref offset, Quantize(positions[i].z, 100f));
                data[offset++] = (byte)Mathf.Clamp(Mathf.RoundToInt(positions[i].y * 10f), 0, 255);
                data[offset++] = (byte)(Mathf.RoundToInt(Mathf.Repeat(yaws[i], 360f) / 360f * 256f) & 0xFF);
            }
            for (int i = 0; i < count; i++)
                if (present[i])
                    data[offset + i / 8] |= (byte)(1 << (i % 8));
            return data;
        }

        /// <returns>The snapshot's restock generation.</returns>
        public static byte Decode(byte[] data, List<Vector3> positions, List<float> yaws, List<bool> present)
        {
            positions.Clear();
            yaws.Clear();
            present.Clear();
            int count = data[0] | (data[1] << 8);
            int offset = HeaderBytes;
            for (int i = 0; i < count; i++)
            {
                float x = ReadShort(data, ref offset) / 100f;
                float z = ReadShort(data, ref offset) / 100f;
                float y = data[offset++] / 10f;
                positions.Add(new Vector3(x, y, z));
                yaws.Add(data[offset++] * (360f / 256f));
            }
            for (int i = 0; i < count; i++)
                present.Add((data[offset + i / 8] & (1 << (i % 8))) != 0);
            return data[2];
        }

        private static short Quantize(float value, float scale) =>
            (short)Mathf.Clamp(Mathf.RoundToInt(value * scale), -short.MaxValue, short.MaxValue);

        private static void WriteShort(byte[] data, ref int offset, short value)
        {
            data[offset++] = (byte)(value & 0xFF);
            data[offset++] = (byte)((value >> 8) & 0xFF);
        }

        private static short ReadShort(byte[] data, ref int offset)
        {
            short value = (short)(data[offset] | (data[offset + 1] << 8));
            offset += 2;
            return value;
        }
    }
}
