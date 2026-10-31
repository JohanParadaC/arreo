using System;

namespace Arreo.Voice
{
    public enum CantoTakeStatus
    {
        Ok,
        /// <summary>Shorter than the shortest canto (5 s).</summary>
        TooShort,
        /// <summary>Nothing was heard: the microphone is muted or too far away.</summary>
        Silent,
    }

    /// <summary>A recording turned into a canto, or why it can't be one.</summary>
    public sealed class CantoTake
    {
        public CantoTake(CantoTakeStatus status, byte[] wav, float seconds, float gain)
        {
            Status = status;
            Wav = wav;
            Seconds = seconds;
            Gain = gain;
        }

        public CantoTakeStatus Status { get; }

        /// <summary>The canto as a WAV (mono, at the rules' sample rate); null unless Ok.</summary>
        public byte[] Wav { get; }

        public float Seconds { get; }

        /// <summary>How much the take was turned up so every canto plays about as loud.</summary>
        public float Gain { get; }
    }

    /// <summary>
    /// What a canto is (rule 4): mono, 16 kHz, 5 to 15 seconds. Turns a microphone recording into
    /// one (mixed to mono, resampled, cut at the longest length, turned up to a common loudness)
    /// and checks a canto that arrives from another player before it's played.
    /// </summary>
    public sealed class CantoRules
    {
        // A canto sent by another game may round its length a little differently.
        private const float LengthTolerance = 0.25f;

        private readonly float _minSeconds;
        private readonly float _maxSeconds;
        private readonly float _targetPeak;
        private readonly float _maxGain;
        private readonly float _silencePeak;

        /// <param name="targetPeak">Loudest sample after turning the take up (0-1).</param>
        /// <param name="maxGain">Most a quiet take is turned up, so background noise isn't blown up into a roar.</param>
        /// <param name="silencePeak">A take whose loudest sample is below this has no voice in it.</param>
        public CantoRules(int sampleRate, float minSeconds, float maxSeconds, float targetPeak, float maxGain, float silencePeak)
        {
            SampleRate = sampleRate;
            _minSeconds = minSeconds;
            _maxSeconds = maxSeconds;
            _targetPeak = targetPeak;
            _maxGain = maxGain;
            _silencePeak = silencePeak;
        }

        public int SampleRate { get; }

        /// <summary>Size of the longest canto accepted from the network, as a WAV.</summary>
        public int MaxWavBytes => WavCodec.HeaderBytes + (int)Math.Ceiling((_maxSeconds + LengthTolerance) * SampleRate) * 2;

        /// <param name="data">The microphone clip's samples (interleaved if more than one channel).</param>
        /// <param name="recordedFrames">How much of the clip was recorded before the player stopped.</param>
        public CantoTake FromRecording(float[] data, int channels, int sampleRate, int recordedFrames)
        {
            int frames = Math.Min(recordedFrames, data.Length / channels);
            frames = Math.Min(frames, (int)Math.Round(_maxSeconds * sampleRate));
            float seconds = (float)frames / sampleRate;
            if (seconds < _minSeconds)
                return new CantoTake(CantoTakeStatus.TooShort, null, seconds, 1f);

            float[] mono = Downmix(data, channels, frames);
            float[] samples = sampleRate == SampleRate ? mono : Resample(mono, sampleRate, SampleRate);
            float peak = Peak(samples);
            if (peak < _silencePeak)
                return new CantoTake(CantoTakeStatus.Silent, null, seconds, 1f);

            float gain = Math.Min(_maxGain, Math.Max(1f, _targetPeak / peak));
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= gain;
            return new CantoTake(CantoTakeStatus.Ok, WavCodec.Encode(samples, SampleRate), (float)samples.Length / SampleRate, gain);
        }

        /// <summary>Whether <paramref name="wav"/> is a canto this game plays, and its samples if so.</summary>
        public bool Check(byte[] wav, out float[] samples, out string problem)
        {
            if (!WavCodec.TryDecode(wav, out samples, out int rate, out problem))
                return false;
            float seconds = (float)samples.Length / rate;
            if (rate != SampleRate)
                problem = $"sampled at {rate} Hz, not {SampleRate}";
            else if (seconds < _minSeconds - LengthTolerance || seconds > _maxSeconds + LengthTolerance)
                problem = $"{seconds:0.0} s long (a canto lasts {_minSeconds:0} to {_maxSeconds:0} s)";
            else
                return true;
            samples = null;
            return false;
        }

        private static float[] Downmix(float[] data, int channels, int frames)
        {
            var mono = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += data[f * channels + c];
                mono[f] = sum / channels;
            }
            return mono;
        }

        // Linear interpolation is plenty for a voice going down to 16 kHz in a game.
        private static float[] Resample(float[] samples, int fromRate, int toRate)
        {
            int count = (int)Math.Round((double)samples.Length * toRate / fromRate);
            var result = new float[count];
            double step = (double)fromRate / toRate;
            for (int i = 0; i < count; i++)
            {
                double at = i * step;
                int left = Math.Min((int)at, samples.Length - 1);
                int right = Math.Min(left + 1, samples.Length - 1);
                float t = (float)(at - left);
                result[i] = samples[left] + (samples[right] - samples[left]) * t;
            }
            return result;
        }

        private static float Peak(float[] samples)
        {
            float peak = 0f;
            foreach (float sample in samples)
                peak = Math.Max(peak, Math.Abs(sample));
            return peak;
        }
    }
}
