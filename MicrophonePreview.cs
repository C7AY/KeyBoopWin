using System;
using NAudio.Wave;

namespace KeyBoopWin
{
    public class MicrophonePreview : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _bufferedWaveProvider;
        private SpeechRecognizer? _speechRecognizer;

        public event EventHandler<float>? AudioLevelChanged;

        public void StartPreview(SpeechRecognizer? speechRecognizer = null)
        {
            _speechRecognizer = speechRecognizer;

            // ⚡ СБРАСЫВАЕМ ОБУЧЕНИЕ ШУМУ ПРИ ЗАПУСКЕ ТЕСТА
            if (_speechRecognizer != null)
            {
                _speechRecognizer.NoiseSuppressor.Reset();
            }

            _waveIn = new WaveInEvent();
            _waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
            _waveIn.DataAvailable += OnDataAvailable;

            _bufferedWaveProvider = new BufferedWaveProvider(_waveIn.WaveFormat);
            _bufferedWaveProvider.BufferDuration = TimeSpan.FromSeconds(2);

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_bufferedWaveProvider);

            _waveIn.StartRecording();
            _waveOut.Play();
        }

        public void StopPreview()
        {
            _waveIn?.StopRecording();
            _waveOut?.Stop();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            short[] samples = new short[e.BytesRecorded / 2];
            Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

            //  ПРИМЕНЯЕМ ТОТ ЖЕ АЛГОРИТМ ШУМОПОДАВЛЕНИЯ
            if (_speechRecognizer != null)
            {
                samples = _speechRecognizer.NoiseSuppressor.Process(samples);
            }

            // УСИЛЕНИЕ
            float gain = _speechRecognizer?.GetMicrophoneGain() ?? 1.0f;
            if (gain != 1.0f)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = samples[i] / 32768f;
                    float amplified = sample * gain;
                    amplified = Math.Max(-1.0f, Math.Min(1.0f, amplified));
                    samples[i] = (short)(amplified * 32768);
                }
            }

            byte[] processedBuffer = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, processedBuffer, 0, processedBuffer.Length);

            _bufferedWaveProvider?.AddSamples(processedBuffer, 0, processedBuffer.Length);

            float max = CalculateAudioLevel(processedBuffer);
            AudioLevelChanged?.Invoke(this, max);
        }

        private float CalculateAudioLevel(byte[] buffer)
        {
            float max = 0;
            for (int i = 0; i < buffer.Length; i += 2)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                float sample32 = sample / 32768f;
                if (Math.Abs(sample32) > max) max = Math.Abs(sample32);
            }
            return max;
        }

        public void Dispose()
        {
            StopPreview();
            _waveIn?.Dispose();
            _waveOut?.Dispose();
        }
    }
}