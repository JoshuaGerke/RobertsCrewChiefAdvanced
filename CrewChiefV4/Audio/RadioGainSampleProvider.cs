using System;
using NAudio.Wave;

namespace CrewChiefV4.Audio
{
    internal sealed class RadioGainSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly Func<float> gain;
        private readonly int channels, rampFrames;
        private float current, target, rampStart;
        private int rampSample;

        public RadioGainSampleProvider(ISampleProvider source, Func<float> gain)
        {
            this.source = source;
            this.gain = gain;
            channels = source.WaveFormat.Channels;
            rampFrames = Math.Max(1, source.WaveFormat.SampleRate / 100);
            current = target = Clamp(gain());
            rampSample = rampFrames * channels;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        internal static float Clamp(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0f, Math.Min(1f, value));

        internal static float Limit(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0;
            const float knee = .9f;
            float peak = Math.Abs(value);
            if (peak <= knee) return value;
            float excess = peak - knee;
            float limited = knee + .1f * (excess / (excess + .1f));
            return value < 0 ? -limited : limited;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            float requested = Clamp(gain());
            if (requested != target)
            {
                rampStart = current;
                target = requested;
                rampSample = 0;
            }
            int rampEnd = rampFrames * channels;
            int transition = Math.Min(read, rampEnd - rampSample);
            for (int i = 0; i < transition; i++, rampSample++)
            {
                int frame = rampSample / channels + 1;
                current = rampStart + (target - rampStart) * ((float)frame / rampFrames);
                buffer[offset + i] = Limit(buffer[offset + i] * current);
            }
            for (int i = transition; i < read; i++)
                buffer[offset + i] = Limit(buffer[offset + i] * target);
            return read;
        }
    }
}
