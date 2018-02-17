using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
                    waveform.Invoke((MethodInvoker)(() => waveform.Series[0].Points.DataBindY(waveValues)));
                    UpdateVolumeMeter(Math.Abs(waveValues[waveValues.Length / 2]));
                };
            waveIn.StartRecording();

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
    }
}
