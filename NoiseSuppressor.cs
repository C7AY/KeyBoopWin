using System;

namespace KeyBoopWin
{
    public class NoiseSuppressor
    {
        private float[] _noiseProfile = new float[1024];
        private int _samplesProcessed = 0;
        private const int LearningSamples = 32000; // 2 секунды при 16kHz

        public bool IsEnabled { get; set; } = true;
        public bool IsLearning { get; private set; } = false;
        public int LearningProgress { get; private set; } = 0;

        public event EventHandler<int>? LearningProgressChanged;
        public event EventHandler? LearningCompleted;

        public void Reset()
        {
            _samplesProcessed = 0;
            LearningProgress = 0;
            IsLearning = false;
        }

        public short[] Process(short[] samples)
        {
            if (!IsEnabled)
            {
                return samples;
            }

            const int frameSize = 1024;
            short[] output = new short[samples.Length];

            // ФАЗА ОБУЧЕНИЯ (первые 2 секунды)
            if (_samplesProcessed < LearningSamples)
            {
                IsLearning = true;

                for (int i = 0; i < samples.Length && _samplesProcessed < LearningSamples; i++)
                {
                    int idx = _samplesProcessed % frameSize;
                    _noiseProfile[idx] = Math.Abs(samples[i] / 32768f);
                    output[i] = samples[i]; // Пока передаём как есть
                    _samplesProcessed++;
                }

                LearningProgress = Math.Min(100, (_samplesProcessed * 100) / LearningSamples);
                LearningProgressChanged?.Invoke(this, LearningProgress);

                if (_samplesProcessed >= LearningSamples)
                {
                    IsLearning = false;
                    LearningCompleted?.Invoke(this, EventArgs.Empty);
                    System.Diagnostics.Debug.WriteLine("✅ Обучение шумоподавлению завершено");
                }

                return output;
            }

            // ФАЗА ПОДАВЛЕНИЯ ШУМА
            float noiseThreshold = 0.0f;
            for (int i = 0; i < frameSize; i++)
            {
                noiseThreshold += _noiseProfile[i];
            }
            noiseThreshold = (noiseThreshold / frameSize) * 1.5f;

            for (int i = 0; i < samples.Length; i++)
            {
                float sample = samples[i] / 32768f;
                float signalPower = Math.Abs(sample);

                if (signalPower < noiseThreshold)
                {
                    // Приглушаем шум на 50% (мягко)
                    output[i] = (short)(sample * 0.5f * 32768);
                }
                else
                {
                    // Голос — пропускаем без изменений
                    output[i] = samples[i];
                }
            }

            return output;
        }
    }
}