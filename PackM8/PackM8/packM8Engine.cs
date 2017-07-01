using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Ports;
using System.Timers;

namespace PackM8
{
    public delegate void packM8EngineEventHandler(object sender, EventArgs e);
    public delegate void packM8ChannelEventHandler(object sender, EventArgs e, int i);

    public struct PacketInfo
    {
        public string PLU { get; set; }
        public string PPK { get; set; }

        public PacketInfo(string _plu, string _ppk)
        {
            PLU = _plu;
            PPK = _ppk;
        }
    }

    public class PackM8Engine
    {
        private int NumChannels;
        private bool lookupLoaded;
        private int descriptionIndex;
        private int piecesIndex;
        private List<Timer> scenTimer;

        public List<Queue<PacketInfo>> DataHold { get; set; }

        public List<InFeed> Infeed { get; set; }
        public List<OutFeed> Outfeed { get; set; }
        public DataTable LookupTable { get; set; }
        public string LookUpErrorMessage { get; set; }
        public bool LookupLoaded { get { return lookupLoaded; } }
        public List<string> InfeedMessage { get; set; }
        public List<string> OutfeedMessage { get; set; }
        public List<string> DisplayMessage { get; set; }

        public settingsJSONutils AppSettings { get; set; }

        public string Message { get; set; }

        public event packM8EngineEventHandler MessageUpdated;
        public event packM8ChannelEventHandler DisplayUpdated;

        protected virtual void OnMessageUpdated(EventArgs e) { MessageUpdated?.Invoke(this, e); }
        protected virtual void OnChannelUpdated(EventArgs e, int channel) { DisplayUpdated?.Invoke(this, e, channel); }

        public PackM8Engine(settingsJSONutils settings)
        {
            AppSettings = settings;
            NumChannels = AppSettings.GetSettingInteger("NumberOfChannels", 5);
            InfeedMessage = new List<string>();
            OutfeedMessage = new List<string>();
            DisplayMessage = new List<string>();

            InitializeFeeds(); 
            LookUpErrorMessage = AppSettings.GetSettingString("LookupErrorMessage", "Product not found");
            String lookupFile = AppSettings.GetSettingString("LookupFile", ".");
            

            if (!LoadLookupFile(lookupFile))
            {
                if (Message == String.Empty)
                    Message = "Failed loading " + lookupFile;
            }
            else
                Message = "database loaded on " + DateTime.Now.ToString();
        }

        public void InitializeFeeds()
        {
            SerialSettings comSettings;

            Infeed    = new List<InFeed>();
            Outfeed   = new List<OutFeed>();
            scenTimer = new List<Timer>();
            DataHold  = new List<Queue<PacketInfo>>();

            int timerLag = 0;
            for (int i = 1; i <= NumChannels; i++)
            {
                String index = i.ToString();
                String section = "InFeed" + index;
                try
                {
                    timerLag = AppSettings.GetSettingInteger("ScenarioFollowUpSec", 15) * 1000;
                    AppLogger.Log(LogLevel.INFO, "Initializing Infeeds...");
                    comSettings = new SerialSettings()
                    {
                        PortName = AppSettings.GetSettingString("COMPort", "COM" + index, section),
                        BaudRate = AppSettings.GetSettingInteger("Baudrate", 9600, section),
                        DataBits = AppSettings.GetSettingInteger("Databits", 8, section),
                        Parity = (Parity)(Enum.Parse(typeof(Parity), AppSettings.GetSettingString("Parity", "None", section))),
                        StopBits = (StopBits)AppSettings.GetSettingInteger("StopBits", 1, section)
                    };
                    InFeed tmpInfeed = new InFeed(comSettings)
                    {
                        Index = i-1,
                        Header = StringUtils.ParseIntoASCII(AppSettings.GetSettingString("Header", "", section)),
                        Footer = StringUtils.ParseIntoASCII(AppSettings.GetSettingString("Footer", "", section)),
                        MessageFormat = AppSettings.GetSettingString("MessageFormat", "", section),
                        PLULength = AppSettings.GetSettingInteger("PLULength", 6, section),
                        PPKLength = AppSettings.GetSettingInteger("PPKLength", 5, section)
                    };
                    Infeed.Add(tmpInfeed);
                    InfeedMessage.Add("");

                    Timer tmpTimer = new Timer(timerLag);
                    tmpTimer.Elapsed += OnTimerElapsed;
                    tmpTimer.AutoReset = false;
                    scenTimer.Add(tmpTimer);

                    Queue<PacketInfo> tmpDataHold = new Queue<PacketInfo>();
                    tmpDataHold.Enqueue(new PacketInfo("", ""));
                    DataHold.Add(tmpDataHold);

                    Infeed[i-1].DataUpdated += new InfeedEventHandler(InfeedDataUpdatedListener);
                    Infeed[i-1].DataReceived += new InfeedEventHandler(InfeedDataReceivedListener);
                    Infeed[i-1].RawDataReceived += new InfeedEventHandler(PlainSerialDataReceivedListener);

                    AppLogger.Log(LogLevel.INFO, "Initializing Outfeeds...");
                    String nuIndex = (i + NumChannels).ToString();
                    section = "OutFeed" + index;
                    comSettings = new SerialSettings()
                    {
                        PortName = AppSettings.GetSettingString("COMPort", "COM" + nuIndex, section),
                        BaudRate = AppSettings.GetSettingInteger("Baudrate", 9600, section),
                        DataBits = AppSettings.GetSettingInteger("Databits", 8, section),
                        Parity = (Parity)(Enum.Parse(typeof(Parity), AppSettings.GetSettingString("Parity", "None", section))),
                        StopBits = (StopBits)AppSettings.GetSettingInteger("StopBits", 1, section),                    
                    };
                    OutFeed tmpOutfeed = new OutFeed(comSettings)
                    {
                        Header = StringUtils.ParseIntoASCII(AppSettings.GetSettingString("Header", "", section)),
                        Footer = StringUtils.ParseIntoASCII(AppSettings.GetSettingString("Footer", "", section)),
                        InputPLULength = Infeed[i - 1].PLULength,
                        InputPPKLength = Infeed[i - 1].PPKLength,
                        OutputPLULength = AppSettings.GetSettingInteger("PLULength", Infeed[i - 1].PLULength, section),
                        OutputPPKLength = AppSettings.GetSettingInteger("PPKLength", Infeed[i - 1].PPKLength - 1, section)
                    };
                    for (int x = 1; x <= 2; x++)
                    {
                        String subsection = "Format" + x.ToString();
                        MessageFormat tmpMsgFmt = new MessageFormat(
                            StringUtils.ParseIntoASCII(AppSettings.GetSettingString("PayloadHeader", "", section, subsection)),
                            StringUtils.ParseIntoASCII(AppSettings.GetSettingString("PayloadFooter", "", section, subsection)),
                            StringUtils.ParseIntoASCII(AppSettings.GetSettingString("QuantityTag", "", section, subsection)),
                            AppSettings.GetSettingInteger("QuantityLength", 2, section)
                            );
                        tmpOutfeed.OutputMessage.Add(tmpMsgFmt);
                    }
                    
                    Outfeed.Add(tmpOutfeed);
                    OutfeedMessage.Add("");
                    DisplayMessage.Add("");
                }
                catch (Exception e)
                {
                    AppLogger.Log(LogLevel.ERROR, String.Format("Failed setting serial {0}{1} settings: {2}.", section, index, e.Message));
                }
            }         
        }


        public void StartEngine()
        {
            for (int i = 0; i < NumChannels; i++)
            {
                try
                {
                    Infeed[i].Port.StartListening();
                    Outfeed[i].Port.StartListening(); 
                }
                catch (Exception e)
                {
                    AppLogger.Log(LogLevel.ERROR, String.Format("Failed starting channel: {0}{2}.", (i+1).ToString(), e.Message));
                }
            }
        }

        private void LookUpAndSend(int index, int scenario)
        {
            string description = String.Empty;
            string outputMsg = String.Empty;
            int quantity = 0;

            PacketInfo onHold = DataHold[index].Peek();
            if (scenario == 1)
                onHold = DataHold[index].Dequeue();
            if (LookUpPack(index, onHold.PLU, ref description, ref quantity))
                outputMsg = Outfeed[index].CreateOutputMessage(onHold.PLU,
                                    onHold.PPK,
                                    quantity,
                                    description,
                                    scenario);
            else
                outputMsg = Outfeed[index].CreateErrorOutputMessage(onHold.PLU,
                                    onHold.PPK,
                                    LookUpErrorMessage, 1);
            Outfeed[index].SendOutputMessage();
            DisplayMessage[index] = String.Format("Outfeed {0}: send {1}", (index + 1).ToString(), StringUtils.StringifyControlChars(outputMsg));
            OnChannelUpdated(new EventArgs(), index);
            AppLogger.Log(LogLevel.INFO, DisplayMessage[index]);
        }

        private void InfeedDataUpdatedListener(object sender, EventArgs e, int index)
        {
            string infeedData = Infeed[index].InFeedData;
            if (scenTimer[index].Enabled)
            {
                InfeedMessage[index] = String.Format("Infeed {0} still active; ignoring {1}", (index + 1).ToString(), infeedData);
                AppLogger.Log(LogLevel.INFO, InfeedMessage[index]);
            }
            else
            {
                InfeedMessage[index] = String.Format("Infeed {0} updated with {1}", (index + 1).ToString(), StringUtils.StringifyControlChars(infeedData));
                AppLogger.Log(LogLevel.INFO, InfeedMessage[index]);
                int x = infeedData.IndexOf(",") + 1;
                string plu = infeedData.Substring(0, Infeed[index].PLULength);
                string ppk = infeedData.Substring(x, Infeed[index].PPKLength - 1);
                DataHold[index].Enqueue(new PacketInfo(plu, ppk));

                if (DataHold[index].Count > 0)
                {
                    Infeed[index].SendScenario2 = true;
                    scenTimer[index].Start();
                    LookUpAndSend(index, 1);
                }
            }
        }

        private void OnTimerElapsed(Object source, ElapsedEventArgs e)
        {
            for (int i = 0; i <Infeed.Count; i++)
            {
                if (Infeed[i].SendScenario2)
                {
                    Infeed[i].SendScenario2 = false;
                    scenTimer[i].Stop();
                    LookUpAndSend(i, 2);                  
                }
            }
        }    
        
        private void PlainSerialDataReceivedListener(object sender, EventArgs e, int index)
        {
            AppLogger.Log(LogLevel.VERBOSE, String.Format("Infeed {0} received raw data: {1}", (index + 1).ToString(), StringUtils.StringifyControlChars(Infeed[index].ReceivedData)));
        }

        private void InfeedDataReceivedListener(object sender, EventArgs e, int index)
        {
           InfeedMessage[index] = String.Format("Infeed {0} received data: {1}", (index+1).ToString(), StringUtils.StringifyControlChars(Infeed[index].ReceivedData));
           AppLogger.Log(LogLevel.INFO, InfeedMessage[index]);
        }

        private void OutfeedDataReceivedListener(object sender, EventArgs e)
        {
            /*TcpConnection current = ExtractConnection(sender);
            if (current == null) return;
            AppLogger.Log(LogLevel.INFO, String.Format("Sent to Outfeed [{0}] received {1}", current.ConnectionName, current.Response));*/
        }

        ~PackM8Engine()
        {
            for (int i = 0; i < NumChannels; i++)
            {
                Infeed[i].Port.StopListening();
                Outfeed[i].Port.StopListening();
            }
        }

        private string LogText(int channel, string msg) { return String.Format("Channel[{0}]: {1}", channel.ToString(), msg); }

        public bool LoadLookupFile(string csvFilePath)
        {
            bool result = false;
            string csvLine = "";
            int LookUpColumnIndex = 0;
            string lookupKey = AppSettings.GetSettingString("LookupKey", "");
            string _desCol = AppSettings.GetSettingString("LookupDescColumn", "");
            string _piecesCol = AppSettings.GetSettingString("LookupPiecesColumn", "");
            AppLogger.Log(LogLevel.INFO, "Loading lookup file " + csvFilePath);
            try
            {
                LookupTable = new DataTable("LookupTable");
                using (StreamReader sr = new StreamReader(csvFilePath))
                {
                    while (!sr.EndOfStream)
                    {
                        csvLine = sr.ReadLine();
                        string[] rows = csvLine.Split(',');
                        if (LookupTable.Columns.Count == 0)
                        {
                            for (int i = 0; i < rows.Length; i++)
                            {
                                LookupTable.Columns.Add();
                                string cur = rows[i].Replace("\"", "");
                                if (cur == lookupKey)
                                    LookUpColumnIndex = i;
                                else if (cur == _desCol)
                                    descriptionIndex = i;
                                else if (cur == _piecesCol)
                                    piecesIndex = i;
                            }
                        }
                        DataRow dr = LookupTable.NewRow();
                        for (int i = 0; i < rows.Length; i++) dr[i] = rows[i].Replace("\"", "");
                        LookupTable.Rows.Add(dr);
                    }
                    if (String.IsNullOrEmpty(_desCol)) descriptionIndex = 1;
                    if (String.IsNullOrEmpty(_piecesCol)) piecesIndex = 2;
                }

                LookupTable.PrimaryKey = new DataColumn[] { LookupTable.Columns[LookUpColumnIndex] };

                AppLogger.Log(LogLevel.VERBOSE, "Done loading lookup File");
                AppLogger.Log(LogLevel.VERBOSE, String.Format("Look Up key is \"{0}\"", lookupKey));
                result = true;
            }
            catch (DataException e)
            {
                Message = String.Format("Error initializing lookup file {0}: {1}", csvFilePath, e.Message);
                AppLogger.Log(LogLevel.ERROR, Message);
                OnMessageUpdated(EventArgs.Empty);
                result = false;
            }

            catch (Exception e)
            {
                Message = String.Format("Error reading lookup file {0}: '{1}', {2}", csvFilePath, csvLine, e.Message);
                AppLogger.Log(LogLevel.ERROR, Message);
                OnMessageUpdated(EventArgs.Empty);
                result = false;
            }

            lookupLoaded = result;
            return result;
        }


        public bool LookUpPack(int channel, string key, ref string description, ref int quantity)
        {
            string result = String.Empty;
            string tmp = String.Empty;

            DataRow foundEntry = LookupTable.Rows.Find(key);
            DateTime dt = DateTime.Now;
            if (foundEntry == null)
            {
                tmp = key + " is not in product list";
                AppLogger.Log(LogLevel.ERROR, LogText(channel, tmp));
                OutfeedMessage[channel] = dt.ToString("MM/dd/yyyy hh:mm:ss.fff tt") + " " + tmp;
                return false;
            }
            else
            {
                description = foundEntry[descriptionIndex].ToString();
                quantity = Convert.ToInt32(foundEntry[piecesIndex]);
                return true;
            }
        }
    }
}
