using Google.Cloud.Speech.V1;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Timer = System.Timers.Timer;

namespace BopIt
{
    public partial class MainForm : Form
    {
        private Timer progressReset = new Timer();
        private Series wave = new Series();

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs args)
        {
            var waveIn = new NAudio.Wave.WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new NAudio.Wave.WaveFormat(16000, 1);
            waveIn.DataAvailable +=
                (object waveSender, NAudio.Wave.WaveInEventArgs e) =>
                {
                    //Dynamic waveform buffer offset, i want to fucking die btw
                    short[] waveValues = new short[(int)(e.BytesRecorded * .55)];
                    for (int i = 0 ; i < e.BytesRecorded; i += 2)
                        waveValues[(i / 2) + ((waveValues.Length - e.BytesRecorded / 2) / 2)] = (short)(BitConverter.ToInt16(e.Buffer, i) / 50);
                    try
                    {
                        waveform.Invoke((MethodInvoker)(() => waveform.Series[0].Points.DataBindY(waveValues)));
                        UpdateVolumeMeter(Math.Abs(waveValues[waveValues.Length / 2]));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                };
            waveIn.StartRecording();



            new Thread((ThreadStart)delegate
            {
                while (true)
                    StreamingMicRecognizeAsync(10).Wait();
            }).Start();

            //Volume meter de-incrimenter
            #region
            progressReset.Interval = 50;
            progressReset.Elapsed += (o, arg) =>
            {
                if (progressBar.Value > 0)
                    progressBar.Invoke((MethodInvoker)(() => progressBar.Value--));

            };
            progressReset.Start();
            #endregion

            wave.ChartType = SeriesChartType.Area;
            wave.Color = Color.Blue;
        }

        private void UpdateVolumeMeter(int data)
        {
            if(data < progressBar.Maximum && data > progressBar.Value)
                progressBar.Invoke((MethodInvoker)(() => progressBar.Value = data));
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            progressReset.Dispose();
        }

        private string currentSentance = String.Empty;
        private string lastTrigger = String.Empty;
        async Task<object> StreamingMicRecognizeAsync(int seconds)
        {
            if (NAudio.Wave.WaveIn.DeviceCount < 1)
            {
                Console.WriteLine("No Mic");
                return -1;
            }
            var speech = SpeechClient.Create();
            var streamingCall = speech.StreamingRecognize();

            // Write the initial request with the config.
            await streamingCall.WriteAsync(
                new StreamingRecognizeRequest()
                {
                    StreamingConfig = new StreamingRecognitionConfig()
                    {
                        Config = new RecognitionConfig()
                        {
                            Encoding =
                            RecognitionConfig.Types.AudioEncoding.Linear16,
                            SampleRateHertz = 16000,
                            LanguageCode = "en",
                        },
                        InterimResults = true,
                    }
                });
            Task printResponses = Task.Run(async () =>
            {
                while (await streamingCall.ResponseStream.MoveNext(
                    default(CancellationToken)))
                {
                    foreach (var result in streamingCall.ResponseStream
                        .Current.Results)
                    {
                        var sentanceResult = result.Alternatives.Last().Transcript;
                        var newWordList = sentanceResult.Split(' ');
                        var currentWordList = currentSentance.Split(' ');

                        if (newWordList.Length > currentWordList.Length ||
                            newWordList.First() != currentWordList.First())
                        {
                            currentSentance = sentanceResult;
                            textBox1.Invoke((MethodInvoker)(() => textBox1.AppendText(newWordList.Last() + Environment.NewLine)));
                            if ( newWordList.Last() != lastTrigger)
                            {
                                textBox1.Invoke((MethodInvoker)(() => textBox1.AppendText("TRIGGER: " + newWordList.Last() + Environment.NewLine)));
                                lastTrigger = newWordList.Last();
                            }
                            Console.WriteLine(newWordList.Last());
                        }
                    }
                }
            });

            object writeLock = new object();
            bool writeMore = true;
            var waveIn = new NAudio.Wave.WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new NAudio.Wave.WaveFormat(16000, 1);
            waveIn.DataAvailable +=
                (object sender, NAudio.Wave.WaveInEventArgs args) =>
                {
                    lock (writeLock)
                    {
                        if (!writeMore) return;
                        streamingCall.WriteAsync(
                            new StreamingRecognizeRequest()
                            {
                                AudioContent = Google.Protobuf.ByteString
                                    .CopyFrom(args.Buffer, 0, args.BytesRecorded)
                            }).Wait();
                    }
                };
            waveIn.StartRecording();
            Console.WriteLine("Reconnecting stream");
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            // Stop recording and shut down.
            waveIn.StopRecording();
            lock (writeLock) writeMore = false;
            await streamingCall.WriteCompleteAsync();
            await printResponses;
            return 0;
        }

        private static bool IsTrigger(string word)
        {
            foreach (var trigger in File.ReadAllLines("beeMovie.txt"))
                if (word == trigger)
                    return true;
            return false;
        }
    }
}
