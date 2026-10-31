using System;
using Arreo.Voice;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    /// <summary>A recorded take becomes a canto (rule 4: WAV mono 16 kHz, 5-15 s), and the host checks what it receives.</summary>
    public class CantoRulesTests
    {
        private const int Rate = 16000;

        private static CantoRules CreateRules() => new CantoRules(sampleRate: Rate, minSeconds: 5f, maxSeconds: 15f,
            targetPeak: 0.9f, maxGain: 8f, silencePeak: 0.02f);

        private static float[] Tone(float seconds, int rate = Rate, int channels = 1, float amplitude = 0.5f, float hz = 220f)
        {
            int frames = (int)(seconds * rate);
            var samples = new float[frames * channels];
            for (int f = 0; f < frames; f++)
                for (int c = 0; c < channels; c++)
                    samples[f * channels + c] = amplitude * (float)Math.Sin(2.0 * Math.PI * hz * f / rate);
            return samples;
        }

        private static float[] Decode(byte[] wav)
        {
            Assert.That(WavCodec.TryDecode(wav, out float[] samples, out int rate, out string problem), Is.True, problem);
            Assert.That(rate, Is.EqualTo(Rate));
            return samples;
        }

        private static float Peak(float[] samples)
        {
            float peak = 0f;
            foreach (float s in samples)
                peak = Math.Max(peak, Math.Abs(s));
            return peak;
        }

        [Test]
        public void TakeShorterThanFiveSeconds_IsTooShort()
        {
            CantoTake take = CreateRules().FromRecording(Tone(4f), channels: 1, sampleRate: Rate, recordedFrames: 4 * Rate);

            Assert.That(take.Status, Is.EqualTo(CantoTakeStatus.TooShort));
            Assert.That(take.Wav, Is.Null);
        }

        [Test]
        public void TakeBetweenFiveAndFifteenSeconds_BecomesA16kMonoWav()
        {
            CantoTake take = CreateRules().FromRecording(Tone(8f), channels: 1, sampleRate: Rate, recordedFrames: 8 * Rate);

            Assert.That(take.Status, Is.EqualTo(CantoTakeStatus.Ok));
            Assert.That(take.Seconds, Is.EqualTo(8f).Within(0.001f));
            Assert.That(Decode(take.Wav), Has.Length.EqualTo(8 * Rate));
        }

        [Test]
        public void OnlyTheRecordedPartOfTheMicrophoneBufferIsUsed()
        {
            // The microphone buffer is as long as the longest canto; the player stopped at 6 s.
            CantoTake take = CreateRules().FromRecording(Tone(16f), channels: 1, sampleRate: Rate, recordedFrames: 6 * Rate);

            Assert.That(Decode(take.Wav), Has.Length.EqualTo(6 * Rate));
        }

        [Test]
        public void TakeLongerThanFifteenSeconds_IsCutAtFifteen()
        {
            CantoTake take = CreateRules().FromRecording(Tone(20f), channels: 1, sampleRate: Rate, recordedFrames: 20 * Rate);

            Assert.That(take.Status, Is.EqualTo(CantoTakeStatus.Ok));
            Assert.That(take.Seconds, Is.EqualTo(15f).Within(0.001f));
            Assert.That(Decode(take.Wav), Has.Length.EqualTo(15 * Rate));
        }

        [Test]
        public void MicrophoneAt44kHz_IsResampledTo16k()
        {
            const int micRate = 44100;
            CantoTake take = CreateRules().FromRecording(Tone(6f, micRate, hz: 200f), channels: 1, sampleRate: micRate, recordedFrames: 6 * micRate);

            float[] samples = Decode(take.Wav);
            Assert.That(samples.Length, Is.EqualTo(6 * Rate).Within(1));
            // Same pitch after resampling: a 200 Hz tone crosses zero upwards 200 times a second.
            int crossings = 0;
            for (int i = 1; i < samples.Length; i++)
                if (samples[i - 1] < 0f && samples[i] >= 0f)
                    crossings++;
            Assert.That(crossings, Is.EqualTo(6 * 200).Within(3));
        }

        [Test]
        public void StereoMicrophone_IsMixedToMono()
        {
            CantoTake take = CreateRules().FromRecording(Tone(6f, channels: 2), channels: 2, sampleRate: Rate, recordedFrames: 6 * Rate);

            Assert.That(Decode(take.Wav), Has.Length.EqualTo(6 * Rate));
        }

        [Test]
        public void QuietTake_IsRaisedToTheTargetPeak()
        {
            CantoTake take = CreateRules().FromRecording(Tone(6f, amplitude: 0.3f), channels: 1, sampleRate: Rate, recordedFrames: 6 * Rate);

            Assert.That(take.Gain, Is.EqualTo(3f).Within(0.01f));
            Assert.That(Peak(Decode(take.Wav)), Is.EqualTo(0.9f).Within(0.01f));
        }

        [Test]
        public void VeryQuietTake_IsRaisedNoMoreThanTheMaximumGain()
        {
            CantoTake take = CreateRules().FromRecording(Tone(6f, amplitude: 0.05f), channels: 1, sampleRate: Rate, recordedFrames: 6 * Rate);

            Assert.That(take.Gain, Is.EqualTo(8f).Within(0.001f));
            Assert.That(Peak(Decode(take.Wav)), Is.EqualTo(0.4f).Within(0.01f));
        }

        [Test]
        public void LoudTake_IsNotTurnedDown()
        {
            CantoTake take = CreateRules().FromRecording(Tone(6f, amplitude: 0.95f), channels: 1, sampleRate: Rate, recordedFrames: 6 * Rate);

            Assert.That(take.Gain, Is.EqualTo(1f));
        }

        [Test]
        public void SilentTake_IsRejected()
        {
            CantoTake take = CreateRules().FromRecording(Tone(6f, amplitude: 0.01f), channels: 1, sampleRate: Rate, recordedFrames: 6 * Rate);

            Assert.That(take.Status, Is.EqualTo(CantoTakeStatus.Silent));
            Assert.That(take.Wav, Is.Null);
        }

        [Test]
        public void Check_AcceptsAGoodCanto()
        {
            byte[] wav = WavCodec.Encode(Tone(9f), Rate);

            Assert.That(CreateRules().Check(wav, out float[] samples, out string problem), Is.True, problem);
            Assert.That(samples, Has.Length.EqualTo(9 * Rate));
        }

        [Test]
        public void Check_RejectsWhatIsNotACantoOfThisGame()
        {
            CantoRules rules = CreateRules();

            Assert.That(rules.Check(WavCodec.Encode(Tone(9f, 22050), 22050), out _, out string rate), Is.False, "other sample rate");
            Assert.That(rate, Is.Not.Empty);
            Assert.That(rules.Check(WavCodec.Encode(Tone(20f), Rate), out _, out _), Is.False, "too long");
            Assert.That(rules.Check(WavCodec.Encode(Tone(2f), Rate), out _, out _), Is.False, "too short");
            Assert.That(rules.Check(new byte[] { 1, 2, 3 }, out _, out _), Is.False, "not a wav");
        }

        [Test]
        public void CheckCompressed_AcceptsAGoodCanto_AndItSoundsLikeTheOriginal()
        {
            float[] original = Tone(9f);

            Assert.That(CreateRules().CheckCompressed(ImaAdpcm.Encode(original, Rate), out float[] samples, out string problem), Is.True, problem);
            Assert.That(samples, Has.Length.EqualTo(original.Length));
            for (int i = 0; i < samples.Length; i += 97)
                Assert.That(samples[i], Is.EqualTo(original[i]).Within(0.05f), $"sample {i}");
        }

        [Test]
        public void CheckCompressed_RejectsWhatIsNotACantoOfThisGame()
        {
            CantoRules rules = CreateRules();

            Assert.That(rules.CheckCompressed(ImaAdpcm.Encode(Tone(9f, 22050), 22050), out _, out string rate), Is.False, "other sample rate");
            Assert.That(rate, Is.Not.Empty);
            Assert.That(rules.CheckCompressed(ImaAdpcm.Encode(Tone(20f), Rate), out _, out _), Is.False, "too long");
            Assert.That(rules.CheckCompressed(ImaAdpcm.Encode(Tone(2f), Rate), out _, out _), Is.False, "too short");
            Assert.That(rules.CheckCompressed(WavCodec.Encode(Tone(9f), Rate), out _, out _), Is.False, "an uncompressed WAV");
            Assert.That(rules.CheckCompressed(new byte[] { 1, 2, 3 }, out _, out _), Is.False, "garbage");
        }

        [Test]
        public void MaxCompressedBytes_FitsTheLongestCanto_InAboutAQuarterOfTheWav()
        {
            CantoRules rules = CreateRules();
            byte[] longest = ImaAdpcm.Encode(new float[15 * Rate], Rate);

            Assert.That(rules.MaxCompressedBytes, Is.GreaterThanOrEqualTo(longest.Length));
            Assert.That(rules.MaxCompressedBytes, Is.LessThan(rules.MaxWavBytes / 3.5f));
        }

        [Test]
        public void MaxWavBytes_FitsTheLongestCanto()
        {
            CantoRules rules = CreateRules();
            byte[] longest = WavCodec.Encode(new float[15 * Rate], Rate);

            Assert.That(rules.MaxWavBytes, Is.GreaterThanOrEqualTo(longest.Length));
            Assert.That(rules.MaxWavBytes, Is.LessThan(longest.Length + Rate));
        }
    }
}
