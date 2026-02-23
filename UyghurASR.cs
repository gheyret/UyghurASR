using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;
using System.Text;

namespace UyghurASR
{
    /// <summary>
    /// Configuration settings for audio processing.
    /// </summary>
    /// <summary>
    /// Calculates Mel spectrograms from audio data.
    /// </summary>
    public class AudioLoader
    {
        public float[] _audioBuffer = null;
        /// <summary>
        /// Generates a Mel spectrogram from an audio signal.
        /// </summary>
        /// 
        private int _sample_rate;
        public AudioLoader(int sample_rate = 22050)
        {
            _sample_rate = sample_rate;
            MediaFoundationApi.Startup();
        }
        /// </summary>
        public void LoadAndPreprocess(string filePath)
        {
            _audioBuffer = null;
            _audioBuffer = LoadAudio(filePath);
        }


        public float[] LoadAudio(string audioFilePath)
        {
            ISampleProvider sampleProvider;
            WaveStream reader;

            string extension = Path.GetExtension(audioFilePath).ToLower();
            switch (extension)
            {
                case ".wav":
                case ".aiff":
                case ".aif":
                    reader = new AudioFileReader(audioFilePath);
                    break;
                case ".ogg":
                    reader = new OpusWaveReader(audioFilePath);
                    break;
                default:
                    reader = new MediaFoundationReader(audioFilePath);
                    break;
            }

            var originalFormat = reader.WaveFormat;
            sampleProvider = reader.ToSampleProvider();

            if (originalFormat.Channels > 1)
            {
                sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
            }

            if (originalFormat.SampleRate != _sample_rate)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, _sample_rate);
            }

            var buffer = new List<float>();
            var readBuffer = new float[sampleProvider.WaveFormat.SampleRate * sampleProvider.WaveFormat.Channels];
            int samplesRead;

            while ((samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                buffer.AddRange(readBuffer.Take(samplesRead));
            }

            reader.Dispose();
            return buffer.ToArray();
        }
    }

    /// <summary>
    /// Performs inference using an ONNX model for speech recognition.
    /// </summary>
    public class UyghurVocabulary
    {
        private const string UyghurLatin = "abcdefghijklmnopqrstuvwxyz éöü'";
        private const string PadToken = "<pad>";
        private const string SosToken = "<sos>";
        private const string EosToken = "<eos>";

        private readonly List<string> _vocabulary;
        private readonly Dictionary<string, int> _vocabToIndex;

        public UyghurVocabulary()
        {
            _vocabulary = new List<string> { PadToken, SosToken, EosToken };
            _vocabulary.AddRange(UyghurLatin.Select(c => c.ToString()));
            _vocabToIndex = _vocabulary.Select((vocab, index) => (vocab, index)).ToDictionary(x => x.vocab, x => x.index);
        }

        public int PadIndex => _vocabToIndex[PadToken];
        public int SosIndex => _vocabToIndex[SosToken];
        public int EosIndex => _vocabToIndex[EosToken];

        /// <summary>
        /// Converts an index to its corresponding vocabulary token.
        /// </summary>
        public string IndexToVocab(int index) => index >= 0 && index < _vocabulary.Count ? _vocabulary[index] : throw new ArgumentOutOfRangeException(nameof(index));
    }
    public class UyghurSpeechRecognizer : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly UyghurVocabulary _vocabulary;
        private readonly AudioLoader _loader;
        public UyghurSpeechRecognizer()
        {
            var modelpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uyghur_asr.onnx");
            _session = new InferenceSession(modelpath);
            _vocabulary = new UyghurVocabulary();
            _loader = new AudioLoader();
        }

        public int Uzunluqi
        {
            get { return _loader._audioBuffer.Length; }
        }

        public string Preload(string audioFilePath)
        {
            try
            {
                _loader.LoadAndPreprocess(audioFilePath);
                return "Höjjetni aldin bir terep qilish tamamlandi. <Tonu> topchisini bassingizla bolidu.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Preload error: {ex.Message}");
                return "Höjjetni aldin bir terep qilishta xataliq körüldi. Awaz höjjiti sel uzundek qilidu.";
            }
        }

        public string Recognize()
        {
            return Recognize(_loader._audioBuffer);
        }


        public string Recognize(float[] audioBuffer)
        {
            try
            {
                System.Diagnostics.Stopwatch st = new System.Diagnostics.Stopwatch();
                st.Restart();
                var inputTensor = new DenseTensor<float>(audioBuffer, new int[] { audioBuffer.Length });
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", inputTensor) };
                var results = _session.Run(inputs);
                var ustr = DecodeResults(results.First().AsTensor<float>());
                //Console.WriteLine($"Serip Qilghan Waqit: {st.ElapsedMilliseconds}");
                st.Stop();
                inputs.Clear();
                return ustr;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recognition error: {ex.Message}");
                return string.Empty;
            }
        }

        private string DecodeResults(Tensor<float> outputTensor)
        {
            var prediction = new StringBuilder();
            int? lastChar = null;
            int timeSteps = outputTensor.Dimensions[2];
            int vocabSize = outputTensor.Dimensions[1];

            for (int i = 0; i < timeSteps; i++)
            {
                int maxIndex = FindArgMax(outputTensor, vocabSize, i);
                if (maxIndex != _vocabulary.PadIndex && maxIndex != lastChar)
                {
                    prediction.Append(_vocabulary.IndexToVocab(maxIndex));
                }
                lastChar = maxIndex;
            }

            return prediction.ToString();
        }

        private int FindArgMax(Tensor<float> tensor, int vocabSize, int timeStep)
        {
            int maxIndex = 0;
            float maxValue = tensor[0, 0, timeStep];

            for (int j = 1; j < vocabSize; j++)
            {
                float value = tensor[0, j, timeStep];
                if (value > maxValue)
                {
                    maxValue = value;
                    maxIndex = j;
                }
            }

            return maxIndex;
        }

        public void Dispose() => _session?.Dispose();
    }
}
