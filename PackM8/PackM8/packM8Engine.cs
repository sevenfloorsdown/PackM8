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

    class PackM8Engine
    {
        private int NumChannels;
        private bool lookupLoaded;

        public List<InFeed> Infeed { get; set; }
        public List<OutFeed> Outfeed { get; set; }
        public DataTable LookupTable { get; set; }
        public DateTime[] BestBeforeDate { get; set; }
        public bool LookupLoaded { get { return lookupLoaded; } }

        public settingsJSONutils AppSettings { get; set; }

        public string Message { get; set; }

        public event packM8EngineEventHandler MessageUpdated;

        protected virtual void OnMessageUpdated(EventArgs e) { if (MessageUpdated != null) MessageUpdated(this, e); }

        public PackM8Engine(settingsJSONutils settings)
        {
            AppSettings = settings;
            NumChannels = AppSettings.GetSettingInteger("NumberOfChannels", 5);
            InitializeFeeds();

            String lookupFile = AppSettings.GetSettingString("LookupFile", ".");

            if (!LoadLookupFile(lookupFile))
            {
                if (Message == String.Empty)
                    Message = "Failed loading " + lookupFile;
            }
            else
                Message = "database loaded on " + DateTime.Now.ToString();

            /*prodName1Index = AppSettings.RecipeProdName1Index;
            prodName2Index = AppSettings.RecipeProdName2Index;
            barcodeIndex = AppSettings.RecipeBarcodeIndex;
            weeksIndex = AppSettings.RecipeWeeksIndex;

            try { bestBeforeDayInt = (int)(DayOfWeek)Enum.Parse(typeof(DayOfWeek), AppSettings.BestBeforeDay.ToUpper()); }
            catch (Exception e) { bestBeforeDayInt = (int)DayOfWeek.Saturday; }

            // I don't want to keep parsing the string we need to send to the Outfeed
            OutfeedFormat = GetOutfeedFormat(AppSettings.OutfeedOutputString);*/
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

                    Infeed[i-1].DataUpdated += new InfeedEventHandler(InfeedDataReceivedListener);

                    AppLogger.Log(LogLevel.INFO, "Initializing Outfeeds...");
                    String nuIndex = (i + NumChannels).ToString();
                    section = "OutFeed" + index;
                    comSettings = new SerialSettings()
                    {
                        PortName = AppSettings.GetSettingString("COMPort", "COM" + nuIndex, section),
                        BaudRate = AppSettings.GetSettingInteger("Baudrate", 9600, section),
                        DataBits = AppSettings.GetSettingInteger("Databits", 8, section),
                        Parity = (Parity)(Enum.Parse(typeof(Parity), AppSettings.GetSettingString("Parity", "None", section))),
                        StopBits = (StopBits)AppSettings.GetSettingInteger("StopBits", 1, section)
                    };
                    OutFeed tmpOutfeed = new OutFeed(comSettings)
                    {
                        Header = StringUtils.ParseIntoASCII(AppSettings.GetSettingString("Header", "", section)),
                        Footer = StringUtils.ParseIntoASCII(AppSettings.GetSettingString("Footer", "", section))
                        // additional fields
                    };
                    Outfeed.Add(tmpOutfeed);
                    //    if (i == 1)
                    //        LineInFeeds[j].Port.NewSerialDataReceived += new EventHandler<SerialDataEventArgs>(LineInFeed1NewDataReceived);
                    //    if (i == 2)
                    //        LineInFeeds[j].Port.NewSerialDataReceived += new EventHandler<SerialDataEventArgs>(LineInFeed2NewDataReceived);
                    //               else  // OutFeed
                    //  LineOutFeed.Port.NewSerialDataReceived += new EventHandler<SerialDataEventArgs>(LineOutFeedNewDataReceived);
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

        public void SendTrigger(int channel)
        {
            //AppLogger.Log(LogLevel.INFO, "Sending software trigger to Infeed " + channel.ToString());
            //Infeed[channel].Send(Properties.Settings.Default.DebugSWTrigger);
        }

        private void InfeedDataUpdatedListener(object sender, EventArgs e, int index)
        {
            AppLogger.Log(LogLevel.INFO, String.Format("Infeed {0} updated with {1}", (index+1).ToString(), Infeed[index].InFeedData));
            // pass to output formatter, update display
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

            if (!recipeIsOK)
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
            AppLogger.Log(LogLevel.INFO, "Loading lookup file " + csvFilePath);
            try
            {
                LookupTable = new DataTable("Recipe");
                using (StreamReader sr = new StreamReader(csvFilePath))
                {
                    while (!sr.EndOfStream)
                    {
                        string[] rows = sr.ReadLine().Split(',');
                        if (LookupTable.Columns.Count == 0)
                            for (int i = 0; i < rows.Length; i++) LookupTable.Columns.Add();
                        DataRow dr = LookupTable.NewRow();
                        for (int i = 0; i < rows.Length; i++) dr[i] = rows[i];
                        LookupTable.Rows.Add(dr);
                    }
                }
                // the barcodes will be the keys
                // TODO: Add code that will catch if there are non-unique barcodes
                LookupTable.PrimaryKey = new DataColumn[] { LookupTable.Columns[2] };

                AppLogger.Log(LogLevel.VERBOSE, "Done loading lookup File ");
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
                Message = String.Format("Error reading lookup file {0}: {1}", csvFilePath, e.Message);
                AppLogger.Log(LogLevel.ERROR, Message);
                OnMessageUpdated(EventArgs.Empty);
                result = false;
            }

            lookupLoaded = result;
            return result;
        }


        public void DoLookup(int channel, string barcode)
        {
           /* string result = String.Empty;
            string productName = String.Empty;
            const string badProd = "BAD PRODUCT: ";
            string tmp = String.Empty;

            DataRow foundEntry = RecipeTable.Rows.Find(barcode);
            DateTime dt = DateTime.Now;
            if (foundEntry == null)
            {
                tmp = barcode + " doesn't have a recipe entry";
                AppLogger.Log(LogLevel.ERROR, logText(channel, tmp));
                DisplayOutputString[channel] = dt.ToString("MM/dd/yyyy hh:mm:ss.fff tt") + spacer + badProd + tmp;
            }
            else
            {
                productName = foundEntry[prodName1Index] + " " + foundEntry[prodName2Index];
                try
                {
                    BestBeforeDate[channel] = GetBestBeforeDate(dt, Convert.ToInt32(foundEntry[weeksIndex]));
                    OutfeedOutputString[channel] = BestBeforeDate[channel].ToString(OutfeedFormat).ToUpper();
                    DisplayOutputString[channel] = dt.ToString() + spacer +
                                                  Regex.Replace(OutfeedOutputString[channel], @"[\x00-\x1F]", string.Empty) +
                                                  spacer + productName;
                }
                catch (Exception e)
                {
                    tmp = "Couldn't get BB date; check recipe file";
                    AppLogger.Log(LogLevel.ERROR, e.Message + " " + tmp);
                    DisplayOutputString[channel] = dt.ToString("MM/dd/yyyy hh:mm:ss.fff tt") + spacer + badProd + tmp;
                }
            }

            // TODO: Figger out what the Outfeed sends back and do sumtin' about it
            if (DisplayOutputString[channel].Contains(badProd))
                ClearOutfeedBuffer(channel);
            else
                Outfeed[channel].Send(OutfeedOutputString[channel]);
            Outfeed[channel].TriggerGeneralUseEvent();*/
        }
    }
}
