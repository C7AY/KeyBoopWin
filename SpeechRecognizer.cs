using System;
using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace KeyBoopWin
{
    public class SpeechRecognizer : IDisposable
    {
        private Model? _model;
        private VoskRecognizer? _recognizer;
        private WaveInEvent? _waveIn;
        private bool _isListening = false;

        private float _microphoneGain = 1.0f;

        // ⚡ ИСПОЛЬЗУЕМ ЕДИНЫЙ КЛАСС ШУМОПОДАВЛЕНИЯ
        public NoiseSuppressor NoiseSuppressor { get; private set; } = new NoiseSuppressor();

        public string CurrentLanguage { get; private set; } = "ru";

        public event EventHandler<string>? TextRecognized;
        public event EventHandler<float>? AudioLevelChanged;

        public SpeechRecognizer(string lang = "ru")
        {
            CurrentLanguage = lang;
            Vosk.Vosk.SetLogLevel(-1);
            LoadModel(lang);

            // Шумоподавление включено по умолчанию
            NoiseSuppressor.IsEnabled = true;
        }

        public void SetMicrophoneGain(float gain)
        {
            _microphoneGain = gain;
            System.Diagnostics.Debug.WriteLine($"🔊 Усиление: {gain:F2}x");
        }

        public float GetMicrophoneGain()
        {
            return _microphoneGain;
        }

        public void SetNoiseSuppression(bool enable)
        {
            NoiseSuppressor.IsEnabled = enable;
            NoiseSuppressor.Reset(); // Сбрасываем обучение
            System.Diagnostics.Debug.WriteLine($"🔇 Шумоподавление: {(enable ? "ВКЛ" : "ВЫКЛ")}");
        }

        private void LoadModel(string lang)
        {
            _recognizer?.Dispose();
            _model?.Dispose();

            string modelFolder = lang == "ru" ? "vosk-model-small-ru-0.22" : "vosk-model-small-en-us-0.15";
            string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", modelFolder);

            if (System.IO.Directory.Exists(modelPath))
            {
                _model = new Model(modelPath);
                _recognizer = new VoskRecognizer(_model, 16000.0f);
            }
            else
            {
                throw new Exception($"Модель не найдена: {modelPath}");
            }
        }

        public void ChangeLanguage(string lang)
        {
            bool wasListening = _isListening;
            if (wasListening) StopListening();

            CurrentLanguage = lang;
            LoadModel(lang);

            if (wasListening) StartListening();
        }

        public void StartListening()
        {
            if (_isListening || _model == null) return;
            _isListening = true;
            NoiseSuppressor.Reset(); // Сбрасываем обучение при новой записи

            _waveIn = new WaveInEvent();
            _waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();

            System.Diagnostics.Debug.WriteLine("🎤 Запись началась");
        }

        public void StopListening()
        {
            _isListening = false;
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isListening || _recognizer == null) return;

            short[] samples = new short[e.BytesRecorded / 2];
            Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

            //  ПРИМЕНЯЕМ ЕДИНОЕ ШУМОПОДАВЛЕНИЕ
            samples = NoiseSuppressor.Process(samples);

            // УСИЛЕНИЕ
            float maxLevel = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = samples[i] / 32768f;

                if (_microphoneGain != 1.0f)
                {
                    float amplified = sample * _microphoneGain;
                    amplified = Math.Max(-1.0f, Math.Min(1.0f, amplified));
                    samples[i] = (short)(amplified * 32768);
                    sample = amplified;
                }

                if (Math.Abs(sample) > maxLevel)
                    maxLevel = Math.Abs(sample);
            }

            AudioLevelChanged?.Invoke(this, maxLevel);

            // РАСПОЗНАВАНИЕ
            byte[] processedBuffer = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, processedBuffer, 0, samples.Length * 2);

            if (_recognizer.AcceptWaveform(processedBuffer, processedBuffer.Length))
            {
                string result = _recognizer.Result();
                try
                {
                    using var json = JsonDocument.Parse(result);
                    if (json.RootElement.TryGetProperty("text", out var textElement))
                    {
                        string? text = textElement.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            TextRecognized?.Invoke(this, text);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка парсинга JSON: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            StopListening();
            _recognizer?.Dispose();
            _model?.Dispose();
        }
    }
}