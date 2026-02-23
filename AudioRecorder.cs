using NAudio.Wave;

namespace UyghurASR
{
    public class AudioRecorder : IDisposable
    {
        private readonly WaveInEvent _waveIn;

        // 外部（Application）にfloat配列を渡すためのイベント
        public event Action<float[]> AudioBufferReady;

        // --- 設定値 ---
        private const int PreRollMs = 500;
        private bool _realTimeMode = false;
        private List<float> voiceBuffer;
        private readonly object lockObject = new object();

        private Queue<float[]> preRecordingBuffer;
        private int preRecordingBufferSize;
        private bool isVoiceDetected = false;
        private int silenceSampleCount = 0;
        private int silenceSampleThreshold;
        private const int SampleRate = 22050;
        // エネルギー計算用
        private const int EnergyWindowSize = 512;
        private Queue<float> energyWindow;

        // VAD関連の設定
        private float voiceThreshold = 0.01f; // 音声検出閾値（0.0～1.0）
        private int voiceMinDurationMs = 200; // 最小音声長（ミリ秒）
        private int preRecordingMs = 500; // 音声検出前の録音時間（ミリ秒）

        public AudioRecorder(int deviceNumber = 0, float voiceThreshold = 0.01f, int silenceDurationMs = 500)
        {
            this.voiceThreshold = Math.Max(0.0f, Math.Min(1.0f, voiceThreshold));

            // 静音サンプル数の閾値を計算
            silenceSampleThreshold = (SampleRate * silenceDurationMs) / 1000;

            // プリレコーディングバッファサイズを計算
            preRecordingBufferSize = (SampleRate * preRecordingMs) / 1000;

            voiceBuffer = new List<float>();
            preRecordingBuffer = new Queue<float[]>();
            energyWindow = new Queue<float>(EnergyWindowSize);

            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += OnDataAvailable;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            lock (lockObject)
            {                 // byte[] を float[] に変換 (16bit PCM -> -1.0f ~ 1.0f)
                float[] samples = ConvertByteToFloat(e.Buffer, e.BytesRecorded);
                if (_realTimeMode)
                {
                    ProcessAudioData(samples);
                }
                else
                {
                    voiceBuffer.AddRange(samples);
                }
            }
        }

        /// <summary>
        /// NAudioのbyteバッファを認識エンジン用のfloat配列に変換する
        /// </summary>
        private float[] ConvertByteToFloat(byte[] array, int length)
        {
            int samplesCount = length / 2;
            float[] floatBuffer = new float[samplesCount];
            for (int i = 0; i < samplesCount; i++)
            {
                short sample = BitConverter.ToInt16(array, i * 2);
                floatBuffer[i] = sample / 32768f;
            }
            return floatBuffer;
        }

        public void Start(bool realTime = false)
        {
            lock (lockObject)
            {
                voiceBuffer.Clear();
                preRecordingBuffer.Clear();
                energyWindow.Clear();
                isVoiceDetected = false;
                silenceSampleCount = 0;

            }
            _realTimeMode = realTime;
            _waveIn.StartRecording();
        }
        public void Stop()
        {
            _waveIn.StopRecording();
            _realTimeMode = false;
        }
        public float[] GetRecordedAudio()
        {
            lock (lockObject)
            {
                return voiceBuffer.ToArray();
            }
        }

        private void ProcessAudioData(float[] audioData)
        {
            // エネルギー計算
            float energy = CalculateEnergy(audioData);
            bool voiceDetected = energy > voiceThreshold;

            if (!isVoiceDetected && voiceDetected)
            {
                // 音声開始を検出
                StartVoiceSegment();
                isVoiceDetected = true;
                //VoiceActivityChanged?.Invoke(this, true);
            }

            if (isVoiceDetected)
            {
                // 音声データをバッファに追加
                voiceBuffer.AddRange(audioData);

                if (voiceDetected)
                {
                    // 音声が継続中
                    silenceSampleCount = 0;
                }
                else
                {
                    // 静音をカウント
                    silenceSampleCount += audioData.Length;

                    // 十分な静音が続いたら音声セグメント終了
                    if (silenceSampleCount >= silenceSampleThreshold)
                    {
                        CompleteVoiceSegment();
                        isVoiceDetected = false;
                        silenceSampleCount = 0;
                        //VoiceActivityChanged?.Invoke(this, false);
                    }
                }
            }
            else
            {
                // 音声検出前のデータをプリレコーディングバッファに保存
                UpdatePreRecordingBuffer(audioData);
            }
        }

        /// <summary>
        /// 音声セグメントの開始処理
        /// </summary>
        private void StartVoiceSegment()
        {
            voiceBuffer.Clear();
            // プリレコーディングバッファの内容を音声バッファに追加
            foreach (var chunk in preRecordingBuffer)
            {
                voiceBuffer.AddRange(chunk);
            }
            preRecordingBuffer.Clear();
        }

        /// <summary>
        /// 音声セグメントの完了処理
        /// </summary>
        private void CompleteVoiceSegment()
        {
            // 最小音声長のチェック
            int minSamples = (SampleRate * voiceMinDurationMs) / 1000;
            if (voiceBuffer.Count < minSamples)
            {
                voiceBuffer.Clear();
                return;
            }

            // 末尾の静音部分を削除（オプション）
            //TrimSilenceFromEnd();

            if (voiceBuffer.Count > 0)
            {
                float[] segmentData = voiceBuffer.ToArray();
                // イベントを発火
                AudioBufferReady?.Invoke(segmentData);
            }

            voiceBuffer.Clear();
        }

        /// <summary>
        /// プリレコーディングバッファの更新
        /// </summary>
        private void UpdatePreRecordingBuffer(float[] audioData)
        {
            preRecordingBuffer.Enqueue(audioData);

            // バッファサイズを制限
            int totalSamples = preRecordingBuffer.Sum(x => x.Length);
            while (totalSamples > preRecordingBufferSize && preRecordingBuffer.Count > 0)
            {
                var removed = preRecordingBuffer.Dequeue();
                totalSamples -= removed.Length;
            }
        }

        /// <summary>
        /// 音声エネルギーの計算（RMS）
        /// </summary>
        private float CalculateEnergy(float[] audioData)
        {
            if (audioData.Length == 0) return 0;

            float sum = 0;
            foreach (float sample in audioData)
            {
                sum += sample * sample;
            }

            return (float)Math.Sqrt(sum / audioData.Length);
        }

        /// <summary>
        /// 末尾の静音部分を削除
        /// </summary>
        private void TrimSilenceFromEnd()
        {
            if (voiceBuffer.Count == 0) return;

            int trimIndex = voiceBuffer.Count - 1;
            int windowSize = Math.Min(256, voiceBuffer.Count);

            // 末尾から静音部分を探す
            while (trimIndex > voiceBuffer.Count / 2)
            {
                int startIdx = Math.Max(0, trimIndex - windowSize);
                int length = Math.Min(windowSize, trimIndex - startIdx + 1);

                float energy = 0;
                for (int i = startIdx; i < startIdx + length; i++)
                {
                    energy += voiceBuffer[i] * voiceBuffer[i];
                }
                energy = (float)Math.Sqrt(energy / length);

                if (energy > voiceThreshold * 0.5f)
                {
                    break;
                }

                trimIndex -= windowSize / 2;
            }

            if (trimIndex < voiceBuffer.Count - 1)
            {
                voiceBuffer.RemoveRange(trimIndex + 1, voiceBuffer.Count - trimIndex - 1);
            }
        }



        public void Dispose()
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
        }
    }
}
