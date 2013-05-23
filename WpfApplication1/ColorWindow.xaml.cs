using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.IO;
using Microsoft.Speech.Recognition;
using Microsoft.Speech.AudioFormat;
using System.Windows.Threading;

namespace WpfApplication1
{

    public partial class ColorWindow : Window
    {
        KinectSensor kinect;
        public ColorWindow(KinectSensor sensor) : this()
        {
            kinect = sensor;
        }
        public ColorWindow()
        {
            InitializeComponent();
            Loaded += ColorWindow_Loaded;
            Unloaded += ColorWindow_Unloaded;
        }
        void ColorWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (kinect != null)
            {
                kinect.AudioSource.Stop();
                speechRecognizer.RecognizeAsyncCancel();
                speechRecognizer.RecognizeAsyncStop();

                kinect.ColorStream.Disable();
                kinect.ColorFrameReady -= myKinect_ColorFrameReady;
                kinect.Stop();
            }
        }
        private WriteableBitmap _ColorImageBitmap;
        private Int32Rect _ColorImageBitmapRect;
        private int _ColorImageStride;

        private SpeechRecognitionEngine speechRecognizer;
        DispatcherTimer ready4sTimer;
        void ColorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (kinect != null)
            {
                #region 彩色影像相關物件初始化
                ColorImageStream colorStream = kinect.ColorStream;

                colorStream.Enable();
                kinect.ColorFrameReady += myKinect_ColorFrameReady;

                _ColorImageBitmap = new WriteableBitmap(colorStream.FrameWidth,colorStream.FrameHeight, 96, 96,
                                                        PixelFormats.Bgr32, null);
                _ColorImageBitmapRect = new Int32Rect(0, 0, colorStream.FrameWidth,colorStream.FrameHeight);
                _ColorImageStride = colorStream.FrameWidth * colorStream.FrameBytesPerPixel;
                ColorData.Source = _ColorImageBitmap;
                #endregion
                kinect.Start();

                speechRecognizer = CreateSpeechRecognizer();
                if (speechRecognizer != null)
                {
                    ready4sTimer = new DispatcherTimer();
                    ready4sTimer.Tick += ReadyTimerTick;
                    ready4sTimer.Interval = new TimeSpan(0, 0, 4);
                    ready4sTimer.Start();
                }              
            }
        }

        private void ReadyTimerTick(object sender, EventArgs e)
        {
            StartVoiceRecognition();
            ready4sTimer.Stop();
            ready4sTimer = null;
        }
        private void StartVoiceRecognition()
        {
            response.Text = "開始接受語音輸入";
            var audioSource = kinect.AudioSource;
            audioSource.EchoCancellationMode = EchoCancellationMode.None;
            audioSource.AutomaticGainControlEnabled = false;
            var kinectStream = audioSource.Start();

            Stream s = kinectStream;
            speechRecognizer.SetInputToAudioStream(
                        s, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));

            speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);
        }

        private SpeechRecognitionEngine CreateSpeechRecognizer()
        {
            var ri = GetKinectRecognizer();
            Console.WriteLine("\n" + ri.Name);
            var sre = new SpeechRecognitionEngine(ri.Id);
            MyGrammars(sre, ri);
            sre.SpeechRecognized += SreSpeechRecognized;
            sre.SpeechHypothesized += SreSpeechHypothesized;
            sre.SpeechRecognitionRejected += SreSpeechRecognitionRejected;
            return sre;
        }

        private static RecognizerInfo GetKinectRecognizer()
        {
            Func<RecognizerInfo, bool> matchingFunc = r =>
            {
                string value;
                r.AdditionalInfo.TryGetValue("Kinect", out value);
                return "True".Equals(value, StringComparison.InvariantCultureIgnoreCase) &&
                                                                 "en-US".Equals(r.Culture.Name,
                  StringComparison.InvariantCultureIgnoreCase);
            };
            return SpeechRecognitionEngine.InstalledRecognizers().Where(matchingFunc).FirstOrDefault();
        }

        private void MyGrammars(SpeechRecognitionEngine sre, RecognizerInfo reg)
        {
            var dir = new Choices();
            dir.Add("up");
            dir.Add("down");
            dir.Add("take");

            var gb = new GrammarBuilder();
            gb.Culture = reg.Culture;
            gb.Append(dir);

            var g = new Grammar(gb);
            sre.LoadGrammar(g);
        }

        void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            response.Text = "辨識失敗:" + e.Result.Text;
        }
        void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            response.Text = "辨識中:" + e.Result.Text;
        }

        void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            response.Text = "辨識成功:" + e.Result.Text + "  , 可靠度: " + e.Result.Confidence;
            if (e.Result.Confidence > 0.7)
                Action(e.Result.Text);
        }

        void Action(string command)
        {
            if (command == "up")
                kinect.ElevationAngle = kinect.ElevationAngle + 5;
            else if (command == "down")
                kinect.ElevationAngle = kinect.ElevationAngle - 5;
            else if (command == "take")
                ColorData_MouseDown(null, null);
        }

        void myKinect_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame frame = e.OpenColorImageFrame())
            {
                if(frame == null)
                    return ;
                
                byte[] pixelData = new byte[frame.PixelDataLength];
                frame.CopyPixelDataTo(pixelData);
                _ColorImageBitmap.WritePixels(_ColorImageBitmapRect, pixelData,_ColorImageStride, 0);
            }
        }

        private void ColorData_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string filename = NewFileName();
            SaveToFile(filename);
        }

        private int i = 0;
        public string NewFileName()
        {
            i++;

            string mypicsfolder =
                           Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            string fileName = mypicsfolder + "\\pic_" + i + ".png";

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            return fileName;

        }
        public void SaveToFile(string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.CreateNew))
            {
                BitmapSource image = (BitmapSource)ColorData.Source;
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(fs);
            }
        }

    }
}
