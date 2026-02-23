using Concentus;
using Concentus.Oggfile;
using NAudio.Wave;
using System.IO;

namespace UyghurASR
{
    public class OpusWaveReader : WaveStream
    {
        private readonly WaveFormat _waveFormat;
        private long _position;
        private bool _disposed;
        private byte[] _audioBuffer;
        private int _bufferLength;

        public OpusWaveReader(string fileName)
        {
            // Opusの標準サンプリングレートは48kHz
            _waveFormat = new WaveFormat(48000, 16, 1);
            _position = 0;
            DecodeAllAudio(fileName);
        }

        private void DecodeAllAudio(String opusFileName)
        {
            using (FileStream fileIn = new FileStream(opusFileName, FileMode.Open))
            {
                // Opusは内部的に常に48000Hzで処理されます
                OpusOggReadStream oggStream = new OpusOggReadStream(OpusCodecFactory.CreateDecoder(48000, 1), fileIn);

                List<short> pcmBuffer = new List<short>();

                // 3. パケットを一つずつデコードして読み込む
                while (oggStream.HasNextPacket)
                {
                    short[] packet = oggStream.DecodeNextPacket();
                    if (packet != null)
                    {
                        pcmBuffer.AddRange(packet);
                    }
                }
                System.Diagnostics.Debug.WriteLine($"デコード完了: {pcmBuffer.Count / 1.0 / 48000.0:F2}秒分を読み込みました。");
                _audioBuffer = new byte[pcmBuffer.Count * 2];
                _bufferLength = _audioBuffer.Length;
                Buffer.BlockCopy(pcmBuffer.ToArray(), 0, _audioBuffer, 0, _audioBuffer.Length);
            }
        }

        public override WaveFormat WaveFormat => _waveFormat;
        public override long Length => _bufferLength;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _bufferLength)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusWaveReader));

            int bytesToRead = (int)Math.Min(count, _bufferLength - _position);

            if (bytesToRead <= 0)
                return 0;

            Array.Copy(_audioBuffer, (int)_position, buffer, offset, bytesToRead);
            _position += bytesToRead;

            return bytesToRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _audioBuffer = null;
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}