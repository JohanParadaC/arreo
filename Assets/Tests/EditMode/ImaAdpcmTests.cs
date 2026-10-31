using System;
using Arreo.Voice;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    /// <summary>The canto travels compressed (IMA ADPCM): about a quarter of the WAV, and still sounds like it.</summary>
    public class ImaAdpcmTests
    {
        private const int Rate = 16000;

        private static float[] Tone(int count, float hz = 440f, float amplitude = 0.5f)
        {
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = amplitude * (float)Math.Sin(2.0 * Math.PI * hz * i / Rate);
            return samples;
        }

        // Something like a sung vowel: a wavering pitch with harmonics and a little breath.
        private static float[] Voice(int count)
        {
            var noise = new Random(4);
            var samples = new float[count];
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                double hz = 220.0 * (1 + 0.02 * Math.Sin(2 * Math.PI * 5 * i / Rate));
                phase += 2 * Math.PI * hz / Rate;
                samples[i] = (float)(0.4 * Math.Sin(phase) + 0.2 * Math.Sin(2 * phase) + 0.12 * Math.Sin(3 * phase)
                                     + 0.05 * Math.Sin(5 * phase) + 0.02 * (noise.NextDouble() * 2 - 1));
            }
            return samples;
        }

        private static float[] RoundTrip(float[] samples)
        {
            Assert.That(ImaAdpcm.TryDecode(ImaAdpcm.Encode(samples, Rate), out float[] back, out int rate, out string problem), Is.True, problem);
            Assert.That(rate, Is.EqualTo(Rate));
            Assert.That(back, Has.Length.EqualTo(samples.Length));
            return back;
        }

        // Signal-to-noise ratio (dB) of the decoded audio against the original.
        private static double Snr(float[] original, float[] decoded)
        {
            double signal = 0, noise = 0;
            for (int i = 0; i < original.Length; i++)
            {
                signal += original[i] * original[i];
                noise += (original[i] - decoded[i]) * (original[i] - decoded[i]);
            }
            return 10 * Math.Log10(signal / noise);
        }

        [Test]
        public void LongestCanto_IsAboutAQuarterOfItsWav()
        {
            float[] samples = Voice(15 * Rate);

            byte[] compressed = ImaAdpcm.Encode(samples, Rate);
            int wav = WavCodec.Encode(samples, Rate).Length;

            Assert.That(compressed.Length, Is.EqualTo(ImaAdpcm.EncodedBytes(samples.Length)));
            Assert.That(compressed.Length, Is.LessThan(wav * 0.26f));
            Assert.That(compressed.Length, Is.GreaterThan(wav * 0.24f));
        }

        [Test]
        public void Tone_SoundsLikeTheOriginal()
        {
            float[] samples = Tone(Rate);

            Assert.That(Snr(samples, RoundTrip(samples)), Is.GreaterThan(25.0));
        }

        [Test]
        public void SungVowel_SoundsLikeTheOriginal()
        {
            float[] samples = Voice(4 * Rate);

            Assert.That(Snr(samples, RoundTrip(samples)), Is.GreaterThan(20.0));
        }

        [Test]
        public void Silence_StaysSilent()
        {
            foreach (float sample in RoundTrip(new float[3000]))
                Assert.That(Math.Abs(sample), Is.LessThan(0.001f));
        }

        [Test]
        public void FullScale_NeverGoesPastIt()
        {
            var samples = new float[4000];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (i / 40) % 2 == 0 ? 1f : -1f;

            foreach (float sample in RoundTrip(samples))
                Assert.That(sample, Is.InRange(-1f, 1f));
        }

        [Test]
        public void LengthsAroundABlock_AllComeBackWhole()
        {
            foreach (int count in new[] { 1, 2, 3, ImaAdpcm.BlockSamples - 1, ImaAdpcm.BlockSamples, ImaAdpcm.BlockSamples + 1, 3 * ImaAdpcm.BlockSamples + 7 })
            {
                float[] samples = Tone(count);
                byte[] compressed = ImaAdpcm.Encode(samples, Rate);
                Assert.That(compressed.Length, Is.EqualTo(ImaAdpcm.EncodedBytes(count)), $"{count} samples");
                Assert.That(ImaAdpcm.TryDecode(compressed, out float[] back, out _, out string problem), Is.True, $"{count} samples: {problem}");
                Assert.That(back, Has.Length.EqualTo(count));
                // Each block starts with its first sample as is.
                Assert.That(back[0], Is.EqualTo(samples[0]).Within(1f / 32767f + 1e-6f));
            }
        }

        [Test]
        public void SameAudio_SameBytes()
        {
            float[] samples = Voice(Rate);

            Assert.That(ImaAdpcm.Encode(samples, Rate), Is.EqualTo(ImaAdpcm.Encode(samples, Rate)));
        }

        [Test]
        public void Decode_SomethingThatIsNotACompressedCanto_FailsWithAReason()
        {
            Assert.That(ImaAdpcm.TryDecode(null, out _, out _, out _), Is.False);
            Assert.That(ImaAdpcm.TryDecode(new byte[5], out _, out _, out _), Is.False);
            Assert.That(ImaAdpcm.TryDecode(WavCodec.Encode(Tone(1000), Rate), out _, out _, out string problem), Is.False, "a WAV");
            Assert.That(problem, Is.Not.Empty);
        }

        [Test]
        public void Decode_SizeThatDoesNotMatchTheSampleCount_Fails()
        {
            byte[] compressed = ImaAdpcm.Encode(Tone(2000), Rate);

            var cut = new byte[compressed.Length - 10];
            Array.Copy(compressed, cut, cut.Length);
            var longer = new byte[compressed.Length + 10];
            Array.Copy(compressed, longer, compressed.Length);

            Assert.That(ImaAdpcm.TryDecode(cut, out _, out _, out string problem), Is.False, "cut short");
            Assert.That(problem, Is.Not.Empty);
            Assert.That(ImaAdpcm.TryDecode(longer, out _, out _, out _), Is.False, "extra bytes");
        }

        [Test]
        public void Decode_BadStepInABlock_Fails()
        {
            byte[] compressed = ImaAdpcm.Encode(Tone(2000), Rate);
            // The second block's step index (after its first sample), past the last step.
            compressed[ImaAdpcm.HeaderBytes + ImaAdpcm.BlockBytes + 2] = 120;

            Assert.That(ImaAdpcm.TryDecode(compressed, out _, out _, out string problem), Is.False);
            Assert.That(problem, Is.Not.Empty);
        }
    }
}
