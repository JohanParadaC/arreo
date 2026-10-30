using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// Packs the herd's positions and headings into 6 bytes per cow: x and z in centimetres
    /// (int16, ±327 m), height in decimetres (byte, 0-25.5 m) and heading in 256 steps (byte).
    /// 50 cows = 302 bytes; at 10 snapshots a second that's about 3 KB/s per client.
    /// </summary>
    public static class HerdSnapshotCodec
    {
        private const int BytesPerCow = 6;

        public static byte[] Encode(IReadOnlyList<Vector3> positions, IReadOnlyList<float> yaws)
        {
            int count = positions.Count;
            var data = new byte[2 + count * BytesPerCow];
            data[0] = (byte)(count & 0xFF);
            data[1] = (byte)(count >> 8);

            int offset = 2;
            for (int i = 0; i < count; i++)
            {
                WriteShort(data, ref offset, Quantize(positions[i].x, 100f));
                WriteShort(data, ref offset, Quantize(positions[i].z, 100f));
                data[offset++] = (byte)Mathf.Clamp(Mathf.RoundToInt(positions[i].y * 10f), 0, 255);
                data[offset++] = (byte)(Mathf.RoundToInt(Mathf.Repeat(yaws[i], 360f) / 360f * 256f) & 0xFF);
            }
            return data;
        }

        public static void Decode(byte[] data, List<Vector3> positions, List<float> yaws)
        {
            positions.Clear();
            yaws.Clear();
            int count = data[0] | (data[1] << 8);
            int offset = 2;
            for (int i = 0; i < count; i++)
            {
                float x = ReadShort(data, ref offset) / 100f;
                float z = ReadShort(data, ref offset) / 100f;
                float y = data[offset++] / 10f;
                positions.Add(new Vector3(x, y, z));
                yaws.Add(data[offset++] * (360f / 256f));
            }
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
