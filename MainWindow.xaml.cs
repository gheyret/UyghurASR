using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace UyghurASR
{
    public partial class MainWindow : Window
    {
        private UyghurSpeechRecognizer _recognizer;
        private AudioRecorder _audioRecorder = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && IsAudioFile(files[0]))
                {
                    e.Effects = DragDropEffects.All;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && IsAudioFile(files[0]))
                {
                    textBoxFilePath.Text = files[0];
                    tabControlAudio.SelectedIndex = 0;
                    LoadAudioFile(files[0]);
                }
            }
            e.Handled = true;
        }

        private bool IsAudioFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension == ".mp3" || extension == ".wav" || extension == ".wma" ||
                   extension == ".aac" || extension == ".mp4" || extension == ".mpeg" ||
                   extension == ".ogg" || extension == ".aiff";
        }

        private void buttonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "Select Audio File",
                Filter = "Audio Files|*.mp3;*.wav;*.wma;*.aac;*.mp4;*.mpeg;*.ogg;*.aiff|" +
                        "MP3 Files|*.mp3|" +
                        "WAV Files|*.wav|" +
                        "WMA Files|*.wma|" +
                        "AAC Files|*.aac|" +
                        "M4A Files|*.mp4|" +
                        "MPEG Files|*.mpeg|" +
                        "OGG Files|*.ogg|" +
                        "AIFF Files|*.aiff|" +
                        "All Files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                textBoxFilePath.Text = openFileDialog.FileName;
                LoadAudioFile(openFileDialog.FileName);
            }
        }

        private async void LoadAudioFile(string filePath)
        {
            buttonRecognizeFile.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            statusLabel.Text = "Höjjetni oqup aldin bir terep qiliwatidu, bir'az saqlang.";

            string msg = await Task.Run(() => _recognizer.Preload(filePath));
            if (msg.Contains("<Tonu>"))
            {
                double duration = _recognizer.Uzunluqi / 22050.0;
                TimeSpan finalTime = TimeSpan.FromSeconds(duration);
                var uz = $"{finalTime.Hours}:{finalTime.Minutes:D2}:{finalTime.Seconds:D2}";
                statusLabel.Text = $"{msg}\nUzunluqi : {uz}";

                Mouse.OverrideCursor = null;
                buttonRecognizeFile.IsEnabled = true;
                buttonRecognizeFile.Style = (Style)FindResource("ModernButtonStyle");
                textBoxResults.Text = "Tonush netijiliri mushu yerde körsitilidu...";
                //textBoxResults.Background = Brushes.White;
            }
            else
            {
                statusLabel.Text = msg;
                Mouse.OverrideCursor = null;
                buttonRecognizeFile.IsEnabled = false;
                buttonRecognizeFile.Background = Brushes.LightGray;
                textBoxResults.Text = "";
                //textBoxResults.Background = Brushes.White;
            }
        }

        private async void buttonRecognizeFile_Click(object sender, RoutedEventArgs e)
        {
            buttonRecognizeFile.IsEnabled = false;
            buttonBrowse.IsEnabled = false;
            var st = (Style)FindResource("SecondaryButtonStyle");
            buttonRecognizeFile.Style = st;
            buttonBrowse.Style = st;

            tabMic.IsEnabled = false;
            tabUzuksiz.IsEnabled = false;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                string tonushNetijisi = await Task.Run(() => _recognizer.Recognize());
                textBoxResults.Text = tonushNetijisi;
                //textBoxResults.Background = Brushes.LightGreen;
                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing audio file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                buttonRecognizeFile.IsEnabled = true;
                buttonBrowse.IsEnabled = true;
                tabMic.IsEnabled = true;
                tabUzuksiz.IsEnabled = true;
                st = (Style)FindResource("ModernButtonStyle");
                buttonRecognizeFile.Style = st;
                buttonBrowse.Style = st;
            }
        }

        private async void buttonStartRecording_Click(object sender, RoutedEventArgs e)
        {
            tabHojjet.IsEnabled = false;
            tabUzuksiz.IsEnabled = false;

            buttonStartRecording.IsEnabled = false;
            buttonStopRecording.IsEnabled = true;

            buttonStartRecording.Style = (Style)FindResource("SecondaryButtonStyle"); ;
            buttonStopRecording.Style = (Style)FindResource("ModernButtonStyle"); ;

            textBoxResults.Text = "";
            await Task.Run(() => _audioRecorder.Start());
        }

        private async void buttonStopRecording_Click(object sender, RoutedEventArgs e)
        {
            _audioRecorder.Stop();
            float[] buf = _audioRecorder.GetRecordedAudio();
            string utext = await Task.Run(() => _recognizer.Recognize(buf));
            textBoxResults.Text += utext + Environment.NewLine;
            buttonStartRecording.IsEnabled = true;
            buttonStopRecording.IsEnabled = false;

            buttonStartRecording.Style = (Style)FindResource("ModernButtonStyle"); ;
            buttonStopRecording.Style = (Style)FindResource("SecondaryButtonStyle"); ;

            tabHojjet.IsEnabled = true;
            tabUzuksiz.IsEnabled = true;
        }

        private async void OnAudioDataReceived(float[] audioBuf)
        {
            string utext = await Task.Run(() => _recognizer.Recognize(audioBuf));
            textBoxResults.Dispatcher.Invoke(() =>
            {
                textBoxResults.Text += utext + Environment.NewLine;
                textBoxResults.ScrollToEnd();
            });
        }

        private void buttonStartRealTime_Click(object sender, RoutedEventArgs e)
        {
            buttonStartRealTime.IsEnabled = false;
            buttonStopRealTime.IsEnabled = true;
            buttonStartRealTime.Style = (Style)FindResource("SecondaryButtonStyle");
            buttonStopRealTime.Style = (Style)FindResource("ModernButtonStyle");

            textBoxResults.Text = "";
            tabHojjet.IsEnabled = false;
            tabMic.IsEnabled = false;
            _audioRecorder.AudioBufferReady -= OnAudioDataReceived;
            _audioRecorder.AudioBufferReady += OnAudioDataReceived;
            _audioRecorder.Start(true);
        }

        private void buttonStopRealTime_Click(object sender, RoutedEventArgs e)
        {
            _audioRecorder.Stop();
            buttonStartRealTime.IsEnabled = true;
            buttonStopRealTime.IsEnabled = false;
            buttonStartRealTime.Style = (Style)FindResource("ModernButtonStyle");
            buttonStopRealTime.Style = (Style)FindResource("SecondaryButtonStyle");

            tabHojjet.IsEnabled = true;
            tabMic.IsEnabled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _audioRecorder.Stop();
            _audioRecorder?.Dispose();
            _recognizer?.Dispose();
            base.OnClosed(e);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            _recognizer = new UyghurSpeechRecognizer();
            _audioRecorder = new AudioRecorder();
            tabHojjet.IsEnabled = true;
            tabMic.IsEnabled = true;
            tabUzuksiz.IsEnabled = true;
            base.OnContentRendered(e);
        }

        private void buttonCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(textBoxResults.Text))
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetText(textBoxResults.Text);
                    MessageBox.Show("Tékist chaplash taxtisigha köchürüldi. Bashqa yerge chapliyalaysiz.", "Muweppeqiyetlik Boldi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Köchürüshte xataliq körüldi. Xataliq uchuri: {ex.Message}", "Xataliq", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Köchüridighan tékist yoq", "Uchur", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            textBoxResults.Clear();
            textBoxResults.Text = "";
        }
    }
}
