using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;

namespace PackM8
{
    public delegate void packM8EngineEventHandler(object sender, EventArgs e);

    public struct PacketInfo
    {
        public string PLU { get; set; }
        public string PPK { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }

        public PacketInfo(string _plu, string _ppk, string _description, int _quantity)
        {
            PLU = _plu;
            PPK = _ppk;
            Quantity = _quantity;
            Description = _description;
        }
    }

    public class PackM8Engine
    {
        private int NumChannels;
        private bool lookupLoaded;
        private int descriptionIndex;
        private int piecesIndex;

        public Queue<PacketInfo> DataHold { get; set; }

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

        protected virtual void OnMessageUpdated(EventArgs e) { MessageUpdated?.Invoke(this, e); }

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

            DataHold = new Queue<PacketInfo>();
        }

        public void InitializeFeeds()
        {
            SerialSettings comSettings;
            Infeed = new List<InFeed>();
            Outfeed = new List<OutFeed>();
            for (int i = 1; i <= NumChannels; i++)
            {
                String index = i.ToString();
                String section = "InFeed" + index;
                try
                {
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

                    Infeed[i-1].DataUpdated += new InfeedEventHandler(InfeedDataUpdatedListener);
                    Infeed[i-1].DataReceived += new InfeedEventHandler(InfeedDataReceivedListener);

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
                        OutputPPKLength = AppSettings.GetSettingInteger("PPKLength", Infeed[i - 1].PPKLength - 1, section),
                        ErrorPLU = AppSettings.GetSettingString("ErrorPLU", "xxxxx", section),
                        ErrorPPK = AppSettings.GetSettingString("ErrorPPK", "yyy.yy ", section)
                    };
                    for (int x = 1; x <= 2; x++)
                    {
                        String subsection = "Format" + x.ToString();
                        MessageFormat tmpMsgFmt = new MessageFormat(
                            AppSettings.GetSettingString("PayloadHeader", "", section, subsection),
                            AppSettings.GetSettingString("PayloadFooter", "", section, subsection),
                            AppSettings.GetSettingString("QuantityTag", "", section, subsection),
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
                    //Outfeed[i].Port.StartListening(); // CHIMICHANGA
                }
                catch (Exception e)
                {
                    AppLogger.Log(LogLevel.ERROR, String.Format("Failed starting channel: {0}{2}.", (i+1).ToString(), e.Message));
                }
            }
        }

        public void SendTrigger(int channel)
        {
            //AppLogger.Log(LogLevel.INFO, "Sending software trigger to Infeed " + channel.ToString());
            //Infeed[channel].Send(Properties.Settings.Default.DebugSWTrigger);
        }

        private void InfeedDataUpdatedListener(object sender, EventArgs e, int index)
        {
            string infeedData = Infeed[index].InFeedData;
            AppLogger.Log(LogLevel.INFO, String.Format("Infeed {0} updated with {1}", (index+1).ToString(), infeedData));
            int x = infeedData.IndexOf(",") + 1; 
            string plu = infeedData.Substring(0, Infeed[index].PLULength);
            string ppk = infeedData.Substring(x, Infeed[index].PPKLength-1);
            string description = String.Empty;
            int quantity = 0;
            if (LookUpPack(index, plu, ref description, ref quantity))
            {
                DataHold.Enqueue(new PacketInfo(plu, ppk, description, quantity));
            }
            else
            {
                //esult = Outfeed[channel].CreateErrorOutputMessage(LookUpErrorMessage, 1);
            }
            /*
             * When product 2 comes in and differs from product 1 locked in send scenario 1. That message should contain 
             * product 1 payload . I can only assume that message in scenario 1 cause the screen to blink as no other
             * messages are sent . Scenario 2 is product 2 the new product that differed . This is sent 15 secs later . 
             * This message is held on the screen . You can see scenario 1 and 2 are different so I assume one allows blinking .
             */
        }

        private void InfeedDataReceivedListener(object sender, EventArgs e, int index)
        {
           AppLogger.Log(LogLevel.INFO, String.Format("Infeed {0} received data: {1}", index.ToString(), Infeed[index].ReceivedData));
        }

        private void ProcessBarcode(int cIdx, string payload)
        {
            /*string barcode = String.Empty;
            string msg = String.Empty;

            AppLogger.Log(LogLevel.INFO, String.Format("Infeed[{0}] received {1}", cIdx.ToString(), payload));

            if (!lookupLoaded)
            {
                msg = String.Format("Received {0} on channel {1} but no recipe file; clearing buffer instead",
                        payload, cIdx.ToString());
                AppLogger.Log(LogLevel.ERROR, msg);
                ClearOutfeedBuffer(cIdx);
                // at this point, it would be a good idea to just stop the line
                // but we don't have control over that
            }

            if ((new String('?', payload.Length)) == payload)
            {
                msg = "NO READ";
                ClearOutfeedBuffer(cIdx);
            }
            else if ((new String('!', payload.Length)) == payload)
            {
                msg = "MULTIPLE BARCODES OR CHECK BOX SPACING";
                ClearOutfeedBuffer(cIdx);
            }
            else
            {
                if (payload.Length < heartbeat.Length)
                {
                    msg = String.Format("Infeed {0} ERROR: Unknown Infeed message: {1}",
                        cIdx.ToString(), payload);
                    AppLogger.Log(LogLevel.ERROR, msg);
                }
                else if (payload.Substring(0, heartbeat.Length).ToUpper() == heartbeat)
                {
                    tickOverTimer(cIdx);
                    // TBD: send a response in the future
                }
                else
                    msg = barcode = payload;
            }

            if (msg != String.Empty)
            {
                InfeedInputString[cIdx] = DateTime.Now.ToString() + spacer + msg;
                Infeed[cIdx].TriggerGeneralUseEvent();
                tickOverTimer(cIdx);
            }

            if (barcode != String.Empty)
            {            
                DoRecipeLookup(cIdx, barcode);
            }*/
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


        public string GetOutfeedFormat(string curFormatString)
        {
            String result = String.Empty;
            var tmp = StringUtils.ParseIntoASCII(curFormatString);
            tmp = tmp.Replace("%%B/B DATE%%", "dd MMM yy");
            result = tmp.Replace("%%TIME%%", "HH:mm");
            return result;
        }

        private void ClearOutfeedBuffer(int channel)
        {
            //Outfeed[channel].Send(OutfeedFormat.Replace("B/B dd MMM yy HH:mm", String.Empty));
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
