using System;
using System.IO;
using System.Text;
using Arreo.Voice;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class WavCodecTests
    {
        private const int Rate = 16000;
        // One step of 16-bit PCM, plus float rounding.
        private const float Step = 1f / 32767f + 1e-6f;

        private static float[] Sine(int count, float hz = 440f, float amplitude = 0.5f)
        {
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = amplitude * (float)Math.Sin(2.0 * Math.PI * hz * i / Rate);
            return samples;
        }

        [Test]
        public void Encode_WritesA16BitMonoPcmHeader()
        {
            byte[] wav = WavCodec.Encode(new float[Rate], Rate);

            Assert.That(Encoding.ASCII.GetString(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(BitConverter.ToInt32(wav, 4), Is.EqualTo(wav.Length - 8));
            Assert.That(Encoding.ASCII.GetString(wav, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(Encoding.ASCII.GetString(wav, 12, 4), Is.EqualTo("fmt "));
            Assert.That(BitConverter.ToInt16(wav, 20), Is.EqualTo(1), "PCM");
            Assert.That(BitConverter.ToInt16(wav, 22), Is.EqualTo(1), "mono");
            Assert.That(BitConverter.ToInt32(wav, 24), Is.EqualTo(Rate));
            Assert.That(BitConverter.ToInt32(wav, 28), Is.EqualTo(Rate * 2), "bytes per second");
            Assert.That(BitConverter.ToInt16(wav, 32), Is.EqualTo(2), "block align");
            Assert.That(BitConverter.ToInt16(wav, 34), Is.EqualTo(16), "bits");
            Assert.That(Encoding.ASCII.GetString(wav, 36, 4), Is.EqualTo("data"));
            Assert.That(BitConverter.ToInt32(wav, 40), Is.EqualTo(Rate * 2));
            Assert.That(wav.Length, Is.EqualTo(WavCodec.HeaderBytes + Rate * 2));
        }

        [Test]
        public void EncodeThenDecode_GivesBackTheSamples()
        {
            float[] samples = Sine(Rate / 2);

            bool decoded = WavCodec.TryDecode(WavCodec.Encode(samples, Rate), out float[] back, out int rate, out string problem);

            Assert.That(decoded, Is.True, problem);
            Assert.That(rate, Is.EqualTo(Rate));
            Assert.That(back.Length, Is.EqualTo(samples.Length));
            for (int i = 0; i < samples.Length; i++)
                Assert.That(back[i], Is.EqualTo(samples[i]).Within(Step), $"sample {i}");
        }

        [Test]
        public void Encode_ClampsSamplesLouderThanFullScale()
        {
            WavCodec.TryDecode(WavCodec.Encode(new[] { 1.5f, -2f }, Rate), out float[] back, out _, out _);

            Assert.That(back[0], Is.EqualTo(1f).Within(Step));
            Assert.That(back[1], Is.EqualTo(-1f).Within(Step));
        }

        [Test]
        public void Decode_SomethingThatIsNotAWav_FailsWithAReason()
        {
            byte[] garbage = Encoding.ASCII.GetBytes("this is not a wav file at all, just some text bytes");

            Assert.That(WavCodec.TryDecode(garbage, out _, out _, out string problem), Is.False);
            Assert.That(problem, Is.Not.Empty);
            Assert.That(WavCodec.TryDecode(null, out _, out _, out _), Is.False);
            Assert.That(WavCodec.TryDecode(new byte[0], out _, out _, out _), Is.False);
        }

        [Test]
        public void Decode_FileCutShort_Fails()
        {
            byte[] wav = WavCodec.Encode(Sine(1000), Rate);

            Assert.That(WavCodec.TryDecode(Cut(wav, 30), out _, out _, out _), Is.False, "header cut");
            Assert.That(WavCodec.TryDecode(Cut(wav, wav.Length - 100), out _, out _, out string problem), Is.False, "data cut");
            Assert.That(problem, Is.Not.Empty);
        }

        [Test]
        public void Decode_Stereo_IsMixedToMono()
        {
            // Left full up, right silent: the mono mix is half.
            var data = new byte[8];
            WriteSample(data, 0, 32767);
            WriteSample(data, 2, 0);
            WriteSample(data, 4, -32767);
            WriteSample(data, 6, 0);

            bool decoded = WavCodec.TryDecode(TestWav(format: 1, channels: 2, bits: 16, data), out float[] back, out _, out string problem);

            Assert.That(decoded, Is.True, problem);
            Assert.That(back, Has.Length.EqualTo(2));
            Assert.That(back[0], Is.EqualTo(0.5f).Within(Step));
            Assert.That(back[1], Is.EqualTo(-0.5f).Within(Step));
        }

        [Test]
        public void Decode_SkipsChunksItDoesNotNeed()
        {
            var data = new byte[4];
            WriteSample(data, 0, 16384);
            WriteSample(data, 2, -16384);

            bool decoded = WavCodec.TryDecode(TestWav(format: 1, channels: 1, bits: 16, data, withListChunk: true), out float[] back, out _, out string problem);

            Assert.That(decoded, Is.True, problem);
            Assert.That(back[0], Is.EqualTo(0.5f).Within(Step));
            Assert.That(back[1], Is.EqualTo(-0.5f).Within(Step));
        }

        [Test]
        public void Decode_FormatsOtherThan16BitPcm_AreRejected()
        {
            Assert.That(WavCodec.TryDecode(TestWav(format: 1, channels: 1, bits: 8, new byte[4]), out _, out _, out _), Is.False, "8-bit");
            Assert.That(WavCodec.TryDecode(TestWav(format: 3, channels: 1, bits: 32, new byte[8]), out _, out _, out _), Is.False, "float");
            Assert.That(WavCodec.TryDecode(TestWav(format: 1, channels: 6, bits: 16, new byte[12]), out _, out _, out _), Is.False, "6 channels");
        }

        private static byte[] Cut(byte[] bytes, int length)
        {
            var cut = new byte[length];
            Array.Copy(bytes, cut, length);
            return cut;
        }

        private static void WriteSample(byte[] data, int offset, short value)
        {
            data[offset] = (byte)(value & 0xff);
            data[offset + 1] = (byte)((value >> 8) & 0xff);
        }

        private static byte[] TestWav(short format, short channels, short bits, byte[] data, bool withListChunk = false)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            byte[] list = Encoding.ASCII.GetBytes("INFOISFT\u0004\0\0\0test");
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(0);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write(format);
            writer.Write(channels);
            writer.Write(Rate);
            writer.Write(Rate * channels * bits / 8);
            writer.Write((short)(channels * bits / 8));
            writer.Write(bits);
            if (withListChunk)
            {
                writer.Write(Encoding.ASCII.GetBytes("LIST"));
                writer.Write(list.Length);
                writer.Write(list);
            }
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(data.Length);
            writer.Write(data);
            writer.Flush();
            byte[] bytes = stream.ToArray();
            BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
            return bytes;
        }
    }
}
