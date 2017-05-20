using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
        private static string versionInfo = "1.0.0.0";
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
            if (RunningLogLength < 10) RunningLogLength = 10; // Arbitrary default

            lineInput0.Content = ini.GetSettingString("InFeedName", "InFeed1", "InFeed1");
            lineInput1.Content = ini.GetSettingString("InFeedName", "InFeed1", "InFeed2");
            lineInput2.Content = ini.GetSettingString("InFeedName", "InFeed1", "InFeed3");
            lineInput3.Content = ini.GetSettingString("InFeedName", "InFeed1", "InFeed4");

            lineOutput0.Content = ini.GetSettingString("OutFeedName", "OutFeed1", "OutFeed1");
            lineOutput1.Content = ini.GetSettingString("OutFeedName", "OutFeed1", "OutFeed1");
            lineOutput2.Content = ini.GetSettingString("OutFeedName", "OutFeed1", "OutFeed1");
            lineOutput3.Content = ini.GetSettingString("OutFeedName", "OutFeed1", "OutFeed1");

            /*lineInputCxn0.Content = ini.Infeed1IPAddress + ":" + ini.Infeed1Port.ToString();
            lineInputCxn1.Content = ini.Infeed2IPAddress + ":" + ini.Infeed2Port.ToString();
            lineInputCxn2.Content = ini.Infeed3IPAddress + ":" + ini.Infeed3Port.ToString();
            lineInputCxn3.Content = ini.Infeed4IPAddress + ":" + ini.Infeed4Port.ToString();

            lineOutputCxn0.Content = ini.Outfeed1IPAddress + ":" + ini.Outfeed1Port.ToString();
            lineOutputCxn1.Content = ini.Outfeed2IPAddress + ":" + ini.Outfeed2Port.ToString();
            lineOutputCxn2.Content = ini.Outfeed3IPAddress + ":" + ini.Outfeed3Port.ToString();
            lineOutputCxn3.Content = ini.Outfeed4IPAddress + ":" + ini.Outfeed4Port.ToString();*/

            packM8Engine = new PackM8Engine();
            packM8Engine.AppSettings = ini;

            /*packM8Engine.Infeed[0].TcpConnected += new TcpEventHandler(InfeedConnectedListener0);
            packM8Engine.Infeed[1].TcpConnected += new TcpEventHandler(InfeedConnectedListener1);
            packM8Engine.Infeed[2].TcpConnected += new TcpEventHandler(InfeedConnectedListener2);
            packM8Engine.Infeed[3].TcpConnected += new TcpEventHandler(InfeedConnectedListener3);

            packM8Engine.Infeed[0].TcpDisconnected += new TcpEventHandler(InfeedDisconnectedListener0);
            packM8Engine.Infeed[1].TcpDisconnected += new TcpEventHandler(InfeedDisconnectedListener1);
            packM8Engine.Infeed[2].TcpDisconnected += new TcpEventHandler(InfeedDisconnectedListener2);
            packM8Engine.Infeed[3].TcpDisconnected += new TcpEventHandler(InfeedDisconnectedListener3);

            packM8Engine.Infeed[0].GeneralUseEvent += new TcpEventHandler(InputReceivedListener0);
            packM8Engine.Infeed[1].GeneralUseEvent += new TcpEventHandler(InputReceivedListener1);
            packM8Engine.Infeed[2].GeneralUseEvent += new TcpEventHandler(InputReceivedListener2);
            packM8Engine.Infeed[3].GeneralUseEvent += new TcpEventHandler(InputReceivedListener3);

            packM8Engine.Outfeed[0].TcpConnected += new TcpEventHandler(OutfeedConnectedListener0);
            packM8Engine.Outfeed[1].TcpConnected += new TcpEventHandler(OutfeedConnectedListener1);
            packM8Engine.Outfeed[2].TcpConnected += new TcpEventHandler(OutfeedConnectedListener2);
            packM8Engine.Outfeed[3].TcpConnected += new TcpEventHandler(OutfeedConnectedListener3);

            packM8Engine.Outfeed[0].TcpDisconnected += new TcpEventHandler(OutfeedDisconnectedListener0);
            packM8Engine.Outfeed[1].TcpDisconnected += new TcpEventHandler(OutfeedDisconnectedListener1);
            packM8Engine.Outfeed[2].TcpDisconnected += new TcpEventHandler(OutfeedDisconnectedListener2);
            packM8Engine.Outfeed[3].TcpDisconnected += new TcpEventHandler(OutfeedDisconnectedListener3);

            packM8Engine.Outfeed[0].GeneralUseEvent += new TcpEventHandler(OutputReceivedListener0);
            packM8Engine.Outfeed[1].GeneralUseEvent += new TcpEventHandler(OutputReceivedListener1);
            packM8Engine.Outfeed[2].GeneralUseEvent += new TcpEventHandler(OutputReceivedListener2);
            packM8Engine.Outfeed[3].GeneralUseEvent += new TcpEventHandler(OutputReceivedListener3);*/

            MessageArea.Content = packM8Engine.Message;
            // for future messages
            packM8Engine.MessageUpdated += new packM8EngineEventHandler(ModelMessageListener);

            for (int i = 0; i < NumChannels; i++)
            {
                // We don't know yet if we're connected.
                // If we are, the listeners later on would update
                // the status indicators
                UpdateInfeedLabel(i, AlertLevel.WARNING);
                UpdateOutfeedLabel(i, AlertLevel.WARNING);
            }

            ShowRunningLogs(packM8Engine.RecipeFileLoaded);

            // For debugging purposes only -------------------------------------------
            /*bool swTrigEnabled = Properties.Settings.Default.EnableSWTrigger;
            System.Windows.Visibility amIVisible = swTrigEnabled ?
                                                   System.Windows.Visibility.Visible :
                                                   System.Windows.Visibility.Hidden;
            AcquireButton.Visibility = amIVisible;
            ChannelChoice.Visibility = amIVisible;
            if (swTrigEnabled)
            {
                for (int i = 0; i < NumChannels; i++)
                    ChannelChoice.Items.Add(i);
                ChannelChoice.SelectedIndex = 0;
            }*/
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

        private void UpdateLogDisplay(int channel, string entry)
        {
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

            if (entry.Contains("BAD PRODUCT") ||
                entry.Contains("NO READ") ||
                entry.Contains("MULTIPLE BARCODES") ||
                entry.Contains("Infeed ERROR") ||
                entry.Contains("Outfeed ERROR"))
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
                    runningLog3.Items.Insert(0, listEntry);
                    if (runningLog4.Items.Count > RunningLogLength) runningLog4.Items.RemoveAt(RunningLogLength);
                    break;
                default:
                    AppLogger.Log(LogLevel.ERROR, "Invalid channel: " + channel.ToString());
                    break;
            }
        }

        private void UpdateInfeedLabel(int channel, AlertLevel lvl)
        {
            if (!this.Dispatcher.HasShutdownFinished)
            {
                if (this.Dispatcher.CheckAccess()) SetInfeedLabelIndicator(channel, lvl);
                else this.Dispatcher.Invoke((Action)(() => { SetInfeedLabelIndicator(channel, lvl); }));
            }
        }

        private void SetInfeedLabelIndicator(int channel, AlertLevel lvl)
        {
            // default (Normal)
            var weight = FontWeights.Normal;
            var color = Brushes.Black;

            switch (lvl)
            {
                case AlertLevel.WARNING:
                    weight = FontWeights.Normal;
                    color = Brushes.Orange;
                    break;
                case AlertLevel.ERROR:
                    weight = FontWeights.Bold;
                    color = Brushes.Red;
                    break;
            }

            switch (channel)
            {
                case 4:
                    lineInput4.FontWeight = lineInputCxn3.FontWeight = weight;
                    lineInput4.Foreground = lineInputCxn3.Foreground = color;
                    break;
                case 3:
                    lineInput3.FontWeight = lineInputCxn3.FontWeight = weight;
                    lineInput3.Foreground = lineInputCxn3.Foreground = color;
                    break;
                case 2:
                    lineInput2.FontWeight = lineInputCxn2.FontWeight = weight;
                    lineInput2.Foreground = lineInputCxn2.Foreground = color;
                    break;
                case 1:
                    lineInput1.FontWeight = lineInputCxn1.FontWeight = weight;
                    lineInput1.Foreground = lineInputCxn1.Foreground = color;
                    break;
                default:
                    lineInput0.FontWeight = lineInputCxn0.FontWeight = weight;
                    lineInput0.Foreground = lineInputCxn0.Foreground = color;
                    break;
            }
        }

        private void UpdateOutfeedLabel(int channel, AlertLevel lvl)
        {
            if (!this.Dispatcher.HasShutdownFinished)
            {
                if (this.Dispatcher.CheckAccess()) SetOutfeedLabelIndicator(channel, lvl);
                else this.Dispatcher.Invoke((Action)(() => { SetOutfeedLabelIndicator(channel, lvl); }));
            }
        }

        private void SetOutfeedLabelIndicator(int channel, AlertLevel lvl)
        {
            // default (Normal)
            var weight = FontWeights.Normal;
            var color = Brushes.Black;

            switch (lvl)
            {
                case AlertLevel.WARNING:
                    weight = FontWeights.Normal;
                    color = Brushes.Orange;
                    break;
                case AlertLevel.ERROR:
                    weight = FontWeights.ExtraBold;
                    color = Brushes.Red;
                    break;
            }

            switch (channel)
            {
                case 4:
                    lineOutput3.FontWeight = lineOutputCxn4.FontWeight = weight;
                    lineOutput3.Foreground = lineOutputCxn4.Foreground = color;
                    break;
                case 3:
                    lineOutput3.FontWeight = lineOutputCxn3.FontWeight = weight;
                    lineOutput3.Foreground = lineOutputCxn3.Foreground = color;
                    break;
                case 2:
                    lineOutput2.FontWeight = lineOutputCxn2.FontWeight = weight;
                    lineOutput2.Foreground = lineOutputCxn2.Foreground = color;
                    break;
                case 1:
                    lineOutput1.FontWeight = lineOutputCxn1.FontWeight = weight;
                    lineOutput1.Foreground = lineOutputCxn1.Foreground = color;
                    break;
                default:
                    lineOutput0.FontWeight = lineOutputCxn0.FontWeight = weight;
                    lineOutput0.Foreground = lineOutputCxn0.Foreground = color;
                    break;
            }
        }

        //---------------------------------------------------------------------
        private void CommonInfeedConnectedListener(object sender, int channel)
        {
            AppLogger.Log(LogLevel.INFO, channel.ToString() + ": Infeed connected");
            UpdateInfeedLabel(channel, AlertLevel.NORMAL);
        }

        private void InfeedConnectedListener0(object sender, EventArgs e)
        { CommonInfeedConnectedListener(sender, 0); }

        private void InfeedConnectedListener1(object sender, EventArgs e)
        { CommonInfeedConnectedListener(sender, 1); }

        private void InfeedConnectedListener2(object sender, EventArgs e)
        { CommonInfeedConnectedListener(sender, 2); }

        private void InfeedConnectedListener3(object sender, EventArgs e)
        { CommonInfeedConnectedListener(sender, 3); }

        private void InfeedConnectedListener4(object sender, EventArgs e)
        { CommonInfeedConnectedListener(sender, 4); }

        //-----------------------------------------------------------------------
        private void CommonInfeedDisconnectedListener(object sender, int channel)
        {
            AppLogger.Log(LogLevel.INFO, channel.ToString() + ": Infeed disconnected");
            UpdateInfeedLabel(channel, AlertLevel.ERROR);
            UpdateLogDisplay(channel, DateTime.Now.ToString() + "   Infeed ERROR");
        }

        private void InfeedDisconnectedListener0(object sender, EventArgs e)
        { CommonInfeedDisconnectedListener(sender, 0); }

        private void InfeedDisconnectedListener1(object sender, EventArgs e)
        { CommonInfeedDisconnectedListener(sender, 1); }

        private void InfeedDisconnectedListener2(object sender, EventArgs e)
        { CommonInfeedDisconnectedListener(sender, 2); }

        private void InfeedDisconnectedListener3(object sender, EventArgs e)
        { CommonInfeedDisconnectedListener(sender, 3); }

        private void InfeedDisconnectedListener4(object sender, EventArgs e)
        { CommonInfeedDisconnectedListener(sender, 4); }

        //-------------------------------------------------------------------
        private void CommonInputReceivedListener(object sender, int channel)
        { UpdateLogDisplay(channel, packM8Engine.InfeedInputString[channel]); }

        private void InputReceivedListener0(object sender, EventArgs e)
        { CommonInputReceivedListener(sender, 0); }

        private void InputReceivedListener1(object sender, EventArgs e)
        { CommonInputReceivedListener(sender, 1); }

        private void InputReceivedListener2(object sender, EventArgs e)
        { CommonInputReceivedListener(sender, 2); }

        private void InputReceivedListener3(object sender, EventArgs e)
        { CommonInputReceivedListener(sender, 3); }

        private void InputReceivedListener4(object sender, EventArgs e)
        { CommonInputReceivedListener(sender, 4); }

        //-------------------------------------------------------------------
        private void CommonOutfeedConnectedListener(object sender, int channel)
        {
            AppLogger.Log(LogLevel.INFO, channel.ToString() + ": Outfeed connected");
            UpdateOutfeedLabel(channel, AlertLevel.NORMAL);
        }

        private void OutfeedConnectedListener0(object sender, EventArgs e)
        { CommonOutfeedConnectedListener(sender, 0); }

        private void OutfeedConnectedListener1(object sender, EventArgs e)
        { CommonOutfeedConnectedListener(sender, 1); }

        private void OutfeedConnectedListener2(object sender, EventArgs e)
        { CommonOutfeedConnectedListener(sender, 2); }

        private void OutfeedConnectedListener3(object sender, EventArgs e)
        { CommonOutfeedConnectedListener(sender, 3); }

        private void OutfeedConnectedListener4(object sender, EventArgs e)
        { CommonOutfeedConnectedListener(sender, 4); }

        //-----------------------------------------------------------------------
        private void CommonOutfeedDisconnectedListener(object sender, int channel)
        {
            AppLogger.Log(LogLevel.INFO, channel.ToString() + ": Outfeed disconnected");
            UpdateOutfeedLabel(channel, AlertLevel.ERROR);
            UpdateLogDisplay(channel, DateTime.Now.ToString() + "   Outfeed ERROR");
        }

        private void OutfeedDisconnectedListener0(object sender, EventArgs e)
        { CommonOutfeedConnectedListener(sender, 0); }

        private void OutfeedDisconnectedListener1(object sender, EventArgs e)
        { CommonOutfeedDisconnectedListener(sender, 1); }

        private void OutfeedDisconnectedListener2(object sender, EventArgs e)
        { CommonOutfeedDisconnectedListener(sender, 2); }

        private void OutfeedDisconnectedListener3(object sender, EventArgs e)
        { CommonOutfeedDisconnectedListener(sender, 3); }

        private void OutfeedDisconnectedListener4(object sender, EventArgs e)
        { CommonOutfeedDisconnectedListener(sender, 4); }

        //-------------------------------------------------------------------
        private void CommonOutputReceivedListener(object sender, int channel)
        { UpdateLogDisplay(channel, packM8Engine.DisplayOutputString[channel]); }

        private void OutputReceivedListener0(object sender, EventArgs e)
        { CommonOutputReceivedListener(sender, 0); }

        private void OutputReceivedListener1(object sender, EventArgs e)
        { CommonOutputReceivedListener(sender, 1); }

        private void OutputReceivedListener2(object sender, EventArgs e)
        { CommonOutputReceivedListener(sender, 2); }

        private void OutputReceivedListener3(object sender, EventArgs e)
        { CommonOutputReceivedListener(sender, 3); }

        private void OutputReceivedListener4(object sender, EventArgs e)
        { CommonOutputReceivedListener(sender, 4); }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            for (int i = 0; i < NumChannels; i++)
            {
                packM8Engine.Infeed[i].StopListening();
                packM8Engine.Outfeed[i].StopListening();
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
            /*if (!packM8Engine.LoadRecipeTable(Properties.Settings.Default.DatabasePath))
                MessageArea.Content = packM8Engine.Message;
            else
                MessageArea.Content = "database refreshed on " + DateTime.Now.ToString();
            ShowRunningLogs(packM8Engine.RecipeFileLoaded);*/
        }

        private void ModelMessageListener(object sender, EventArgs e)
        {
            UpdateMessageArea();
        }

        // For debugging-mode only
        private void AcquireButton_Click(object sender, RoutedEventArgs e)
        { packM8Engine.SendTrigger(ChannelChoice.SelectedIndex); }

    }

}