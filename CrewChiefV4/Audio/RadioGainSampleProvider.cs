using System;
using NAudio.Wave;

namespace CrewChiefV4.Audio
{
    internal sealed class RadioGainSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly Func<float> gain;
        private float current;

        public RadioGainSampleProvider(ISampleProvider source, Func<float> gain)
        {
            this.source = source;
            this.gain = gain;
            current = Clamp(gain());
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
            float target = Clamp(gain());
            int transition = current == target ? 0 : Math.Min(read, Math.Max(1, WaveFormat.SampleRate * WaveFormat.Channels / 100));
            float start = current;
            for (int i = 0; i < transition; i++)
                buffer[offset + i] = Limit(buffer[offset + i] * (start + (target - start) * ((i + 1f) / transition)));
            for (int i = transition; i < read; i++)
                buffer[offset + i] = Limit(buffer[offset + i] * target);
            current = target;
            return read;
        }
    }
}
