using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PackM8
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    enum AlertLevel
    {
        NORMAL = 0,
        WARNING = 1,
        ERROR = 2
    };

    public partial class MainWindow : Window
    {

        private static Mutex mutex = new Mutex(true, "1659aff2-7d2c-48f5-8557-a4efd694d16d");
        private static string versionInfo = "1.0.0.2a";
        private static string displayName = "PackM8";
        private static string showInfo = displayName + " v: " + versionInfo;

        private PackM8Engine packM8Engine;
        private static int NumChannels = 5;
        private static int RunningLogLength;
        private static settingsJSONutils ini;

        public MainWindow()
        {
            // subsequent additional instances just quit running
            if (!mutex.WaitOne(TimeSpan.Zero, true)) Environment.Exit(0);

            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();
            string filePath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + "\\" + args[1];

            if (!File.Exists(filePath))
            {
                string msg = System.Reflection.Assembly.GetEntryAssembly().GetName().Name + " settings.json not found";
                MessageBox.Show(msg);
                Environment.Exit(0);
            }

            ini = new settingsJSONutils(filePath);

            AppLogger.Start(ini.GetSettingString("Root", "."),
                            ini.GetSettingString("LogFile", System.Reflection.Assembly.GetEntryAssembly().GetName().Name, "Logging"),
                            ini.GetSettingString("DateTimeFormat", "dd/MM/yyyy hh:mm:ss.fff", "Logging"));
            AppLogger.CurrentLevel = AppLogger.ConvertToLogLevel(ini.GetSettingString("LogLevel", "Verbose", "Logging"));

            int n = showInfo.Length;
            string filler = new string('*', n);
            this.Title = showInfo;

            RunningLogLength = ini.GetSettingInteger("RunningLogLength", 5);
            NumChannels = ini.GetSettingInteger("NumberOfChannels", 5);
            if (RunningLogLength < 10) RunningLogLength = 10; // Arbitrary default

            packM8Engine = new PackM8Engine(ini);
            packM8Engine.StartEngine();

            for (int i = 1; i <= NumChannels; i++)
            {
                try
                {
                    switch (i)
                    {
                        case 1:
                            lineInput0.Content = ini.GetSettingString("InFeedName", "InFeed1", "InFeed1");
                            lineOutput0.Content = ini.GetSettingString("OutFeedName", "OutFeed1", "OutFeed1");
                            lineInputCxn0.Content = ini.GetSettingString("COMPort", "COM1", "InFeed1");
                            lineOutputCxn0.Content = ini.GetSettingString("COMPort", "COM6", "OutFeed1");
                            packM8Engine.Infeed[0].DataUpdated += new InfeedEventHandler(InputReceivedListener);
                            packM8Engine.Infeed[0].DataReceived += new InfeedEventHandler(InputReceivedListener);
                            break;
                        case 2:
                            lineInput1.Content = ini.GetSettingString("InFeedName", "InFeed2", "InFeed2");
                            lineOutput1.Content = ini.GetSettingString("OutFeedName", "OutFeed2", "OutFeed2");
                            lineInputCxn1.Content = ini.GetSettingString("COMPort", "COM2", "InFeed2");
                            lineOutputCxn1.Content = ini.GetSettingString("COMPort", "COM7", "OutFeed2");
                            packM8Engine.Infeed[1].DataUpdated += new InfeedEventHandler(InputReceivedListener);
                            packM8Engine.Infeed[1].DataReceived += new InfeedEventHandler(InputReceivedListener);
                            break;
                        case 3:
                            lineInput2.Content = ini.GetSettingString("InFeedName", "InFeed3", "InFeed3");
                            lineOutput2.Content = ini.GetSettingString("OutFeedName", "OutFeed3", "OutFeed3");
                            lineInputCxn2.Content = ini.GetSettingString("COMPort", "COM3", "InFeed3");
                            lineOutputCxn2.Content = ini.GetSettingString("COMPort", "COM8", "OutFeed3");
                            packM8Engine.Infeed[2].DataUpdated += new InfeedEventHandler(InputReceivedListener);
                            packM8Engine.Infeed[2].DataReceived += new InfeedEventHandler(InputReceivedListener);
                            break;
                        case 4:
                            lineInput3.Content = ini.GetSettingString("InFeedName", "InFeed4", "InFeed4");
                            lineOutput3.Content = ini.GetSettingString("OutFeedName", "OutFeed4", "OutFeed4");
                            lineInputCxn3.Content = ini.GetSettingString("COMPort", "COM4", "InFeed4");
                            lineOutputCxn3.Content = ini.GetSettingString("COMPort", "COM9", "OutFeed4");
                            packM8Engine.Infeed[3].DataUpdated += new InfeedEventHandler(InputReceivedListener);
                            packM8Engine.Infeed[3].DataReceived += new InfeedEventHandler(InputReceivedListener);
                            break;
                        default:
                            lineInput4.Content = ini.GetSettingString("InFeedName", "InFeed5", "InFeed5");
                            lineOutput4.Content = ini.GetSettingString("OutFeedName", "OutFeed5", "OutFeed5");
                            lineInputCxn4.Content = ini.GetSettingString("COMPort", "COM5", "InFeed5");
                            lineOutputCxn4.Content = ini.GetSettingString("COMPort", "COM10", "OutFeed5");
                            packM8Engine.Infeed[4].DataUpdated += new InfeedEventHandler(InputReceivedListener);
                            packM8Engine.Infeed[4].DataReceived += new InfeedEventHandler(InputReceivedListener);
                            break;
                    }
                }
                catch (Exception e)
                {
                    AppLogger.Log(LogLevel.ERROR, String.Format("Incorrect number of channel set: {1} {2}.", NumChannels.ToString(), e.Message));
                }
            }
            
            MessageArea.Content = packM8Engine.Message;
            // for future messages
            packM8Engine.DisplayUpdated += new packM8ChannelEventHandler(DisplayMessageListener);
            packM8Engine.MessageUpdated += new packM8EngineEventHandler(ModelMessageListener);

            ShowRunningLogs(packM8Engine.LookupLoaded);
        }

        private void ShowRunningLogs(bool value)
        {
            System.Windows.Visibility amIVisible = value ?
                                                   System.Windows.Visibility.Visible :
                                                   System.Windows.Visibility.Hidden;
            runningLog0.Visibility = amIVisible;
            runningLog4.Visibility = runningLog3.Visibility
                                   = runningLog2.Visibility
                                   = runningLog1.Visibility
                                   = runningLog0.Visibility;
        }

        private void DisplayMessageListener(object sender, EventArgs e, int channel)
        {
            string entry = packM8Engine.DisplayMessage[channel];
            if (!this.Dispatcher.HasShutdownFinished)
            {
                if (this.Dispatcher.CheckAccess()) SetLogEntry(channel, entry);
                else this.Dispatcher.Invoke((Action)(() => { SetLogEntry(channel, entry); }));
            }
        }

        private void SetLogEntry(int channel, string entry)
        {
            // default (Normal)
            var weight = FontWeights.Normal;
            var color = Brushes.Black;

            if (entry.Contains("ERROR"))
            {
                weight = FontWeights.Bold;
                color = Brushes.Red;
            }

            ListViewItem listEntry = new ListViewItem();
            listEntry.Content = entry;
            listEntry.FontWeight = weight;
            listEntry.Foreground = color;

            switch (channel)
            {
                case 0:
                    runningLog0.Items.Insert(0, listEntry);
                    if (runningLog0.Items.Count > RunningLogLength) runningLog0.Items.RemoveAt(RunningLogLength);
                    break;
                case 1:
                    runningLog1.Items.Insert(0, listEntry);
                    if (runningLog1.Items.Count > RunningLogLength) runningLog1.Items.RemoveAt(RunningLogLength);
                    break;
                case 2:
                    runningLog2.Items.Insert(0, listEntry);
                    if (runningLog2.Items.Count > RunningLogLength) runningLog2.Items.RemoveAt(RunningLogLength);
                    break;
                case 3:
                    runningLog3.Items.Insert(0, listEntry);
                    if (runningLog3.Items.Count > RunningLogLength) runningLog3.Items.RemoveAt(RunningLogLength);
                    break;
                case 4:
                    runningLog4.Items.Insert(0, listEntry);
                    if (runningLog4.Items.Count > RunningLogLength) runningLog4.Items.RemoveAt(RunningLogLength);
                    break;
                default:
                    AppLogger.Log(LogLevel.ERROR, "Invalid channel: " + channel.ToString());
                    break;
            }
        }

        private void InputReceivedListener(object sender, EventArgs e, int channel)
        { 
            if (!this.Dispatcher.HasShutdownFinished)
            {
                if (this.Dispatcher.CheckAccess()) SetLogEntry(channel, packM8Engine.InfeedMessage[channel]);
                else this.Dispatcher.Invoke((Action)(() => { SetLogEntry(channel, packM8Engine.InfeedMessage[channel]); }));
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            for (int i = 0; i < NumChannels; i++)
            {
                packM8Engine.Infeed[i].Port.StopListening();
                packM8Engine.Outfeed[i].Port.StopListening();
            }
            AppLogger.Log(LogLevel.INFO, "Closing window");
            AppLogger.Stop();
        }

        private void UpdateMessageArea()
        {
            if (!this.Dispatcher.HasShutdownFinished)
            {
                if (this.Dispatcher.CheckAccess()) MessageArea.Content = packM8Engine.Message;
                else this.Dispatcher.Invoke((Action)(() => { MessageArea.Content = packM8Engine.Message; }));
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!packM8Engine.LoadLookupFile(ini.GetSettingString("LookupFile", "")))
                MessageArea.Content = packM8Engine.Message;
            else
                MessageArea.Content = "database refreshed on " + DateTime.Now.ToString();
            ShowRunningLogs(packM8Engine.LookupLoaded);
        }

        private void ModelMessageListener(object sender, EventArgs e)
        {
            UpdateMessageArea();
        }

        // For debugging-mode only
        private void AcquireButton_Click(object sender, RoutedEventArgs e)
        { }

    }

}