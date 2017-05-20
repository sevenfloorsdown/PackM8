using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;

namespace PackM8
{
    public delegate void packM8EngineEventHandler(object sender, EventArgs e);

    class PackM8Engine
    {
        private const int NumChannels = 5;

        public SerialPortManager[] Infeed { get; set; }
        public SerialPortManager[] Outfeed { get; set; }
        public DataTable RecipeTable { get; set; }
        public DateTime[] BestBeforeDate { get; set; }

        public string[] InfeedInputString { get; set; }
        public string[] OutfeedOutputString { get; set; }
        public string[] DisplayOutputString { get; set; }
        public settingsJSONutils AppSettings { get; set; }

        public string Message { get; set; }
        public bool RecipeFileLoaded { get { return recipeIsOK; } }
        public event packM8EngineEventHandler MessageUpdated;

        private Timer InfeedTimeoutTimer0;
        private Timer InfeedTimeoutTimer1;
        private Timer InfeedTimeoutTimer2;
        private Timer InfeedTimeoutTimer3;

        private bool recipeIsOK;
        private int prodName1Index;
        private int prodName2Index;
        private int barcodeIndex;
        private int weeksIndex;
        private int bestBeforeDayInt;
        private string OutfeedFormat;
        private const string spacer = "   ";
        private string heartbeat;

        protected virtual void OnMessageUpdated(EventArgs e) { if (MessageUpdated != null) MessageUpdated(this, e); }

        public PackM8Engine()
        {
            /*heartbeat = AppSettings.InfeedHeartBeat;

            socketTransactorType clientOrServerInfeed =
                (AppSettings.InfeedSocketType.ToUpper() == "CLIENT") ?
                socketTransactorType.client : socketTransactorType.server;

            socketTransactorType clientOrServerOutfeed =
                (AppSettings.OutfeedSocketType.ToUpper() == "CLIENT") ?
                socketTransactorType.client : socketTransactorType.server;

            bool InfeedUseBin = (AppSettings.InfeedTCPDataType.ToUpper() == "BINARY" ||
                                  AppSettings.InfeedTCPDataType.ToUpper() == "HEX") ?
                                  true : false;
            bool OutfeedUseBin = (AppSettings.InfeedTCPDataType.ToUpper() == "BINARY" ||
                                  AppSettings.InfeedTCPDataType.ToUpper() == "HEX") ?
                                  true : false;*/

            InfeedInputString = new string[NumChannels];
            OutfeedOutputString = new string[NumChannels];
            DisplayOutputString = new string[NumChannels];

            BestBeforeDate = new DateTime[NumChannels];

            string[] someIPs;
            /*if (clientOrServerInfeed == socketTransactorType.server)
            {
                someIPs = new string[] {
                    AppSettings.OwnIPAddressInfeedSide,
                    AppSettings.OwnIPAddressInfeedSide,
                    AppSettings.OwnIPAddressInfeedSide,
                    AppSettings.OwnIPAddressInfeedSide};
            }
            else
            {
                someIPs = new string[] {
                    AppSettings.Infeed1IPAddress,
                    AppSettings.Infeed2IPAddress,
                    AppSettings.Infeed3IPAddress,
                    AppSettings.Infeed4IPAddress};
            }

            AppLogger.Log(LogLevel.INFO, "Initializing Infeeds...");
            Infeed = new TcpConnection[] {
                new TcpConnection(clientOrServerInfeed, someIPs[0], AppSettings.Infeed1Port, InfeedUseBin ),
                new TcpConnection(clientOrServerInfeed, someIPs[1], AppSettings.Infeed2Port, InfeedUseBin ),
                new TcpConnection(clientOrServerInfeed, someIPs[2], AppSettings.Infeed3Port, InfeedUseBin ),
                new TcpConnection(clientOrServerInfeed, someIPs[3], AppSettings.Infeed4Port, InfeedUseBin )
            };

            if (clientOrServerOutfeed == socketTransactorType.server)
            {
                someIPs = new string[] {
                    AppSettings.OwnIPAddressOutfeedSide,
                    AppSettings.OwnIPAddressOutfeedSide,
                    AppSettings.OwnIPAddressOutfeedSide,
                    AppSettings.OwnIPAddressOutfeedSide};
            }
            else
            {
                someIPs = new string[] {
                    AppSettings.Outfeed1IPAddress,
                    AppSettings.Outfeed2IPAddress,
                    AppSettings.Outfeed3IPAddress,
                    AppSettings.Outfeed4IPAddress};
            }

            AppLogger.Log(LogLevel.INFO, "Initializing Outfeeds...");
            Outfeed = new TcpConnection[] {
                new TcpConnection(clientOrServerOutfeed, someIPs[0], AppSettings.Outfeed1Port, OutfeedUseBin ),
                new TcpConnection(clientOrServerOutfeed, someIPs[1], AppSettings.Outfeed2Port, OutfeedUseBin ),
                new TcpConnection(clientOrServerOutfeed, someIPs[2], AppSettings.Outfeed3Port, OutfeedUseBin ),
                new TcpConnection(clientOrServerOutfeed, someIPs[3], AppSettings.Outfeed4Port, OutfeedUseBin )
            };

            for (uint i = 0; i < NumChannels; i++)
            {
                Infeed[i].Header = StringUtils.ParseIntoASCII(AppSettings.InfeedTCPHeader);
                Infeed[i].Footer = StringUtils.ParseIntoASCII(AppSettings.InfeedTCPFooter);
                Infeed[i].ConnectionName = i.ToString(); // we'll use this as reverse-lookup key later on

                Outfeed[i].Header = StringUtils.ParseIntoASCII(AppSettings.OutfeedTCPHeader);
                Outfeed[i].Footer = StringUtils.ParseIntoASCII(AppSettings.OutfeedTCPFooter);
                Outfeed[i].ConnectionName = i.ToString(); // we'll use this as reverse-lookup key later on

                // Wire up events; the listener use the ConnectionName which should contain
                // the index that will let us know which channel is which (like a reverse lookup)
                Infeed[i].TcpConnected += new TcpEventHandler(InfeedConnectedListener);
                Infeed[i].TcpDisconnected += new TcpEventHandler(InfeedDisconnectedListener);
                Infeed[i].DataReceived += new TcpEventHandler(InfeedDataReceivedListener);

                Outfeed[i].TcpConnected += new TcpEventHandler(OutfeedConnectedListener);
                Outfeed[i].TcpDisconnected += new TcpEventHandler(OutfeedDisconnectedListener);
                Outfeed[i].DataReceived += new TcpEventHandler(OutfeedDataReceivedListener);

                InfeedInputString[i] = String.Empty;
                OutfeedOutputString[i] = String.Empty;
                DisplayOutputString[i] = String.Empty;
            }

            int timeout = Convert.ToInt32(AppSettings.InfeedHeartBeatFreqInS) * 1000;
            InfeedTimeoutTimer0 = new Timer(timeout);
            InfeedTimeoutTimer0.Elapsed += OnTimedEvent0;
            InfeedTimeoutTimer1 = new Timer(timeout);
            InfeedTimeoutTimer1.Elapsed += OnTimedEvent1;
            InfeedTimeoutTimer2 = new Timer(timeout);
            InfeedTimeoutTimer2.Elapsed += OnTimedEvent2;
            InfeedTimeoutTimer3 = new Timer(timeout);
            InfeedTimeoutTimer3.Elapsed += OnTimedEvent3;

            if (!LoadRecipeTable(AppSettings.DatabasePath))
            {
                if (Message == String.Empty)
                    Message = "Failed loading " + AppSettings.DatabasePath;
            }
            else
                Message = "database loaded on " + DateTime.Now.ToString();

            prodName1Index = AppSettings.RecipeProdName1Index;
            prodName2Index = AppSettings.RecipeProdName2Index;
            barcodeIndex = AppSettings.RecipeBarcodeIndex;
            weeksIndex = AppSettings.RecipeWeeksIndex;

            try { bestBeforeDayInt = (int)(DayOfWeek)Enum.Parse(typeof(DayOfWeek), AppSettings.BestBeforeDay.ToUpper()); }
            catch (Exception e) { bestBeforeDayInt = (int)DayOfWeek.Saturday; }

            // I don't want to keep parsing the string we need to send to the Outfeed
            OutfeedFormat = GetOutfeedFormat(AppSettings.OutfeedOutputString);*/
        }


        public void SendTrigger(int channel)
        {
            //AppLogger.Log(LogLevel.INFO, "Sending software trigger to Infeed " + channel.ToString());
            //Infeed[channel].Send(Properties.Settings.Default.DebugSWTrigger);
        }


        /*public TcpConnection ExtractConnection(object anObject)
        {
            if (anObject is TcpConnection) return (TcpConnection)anObject;
            return null;
        }*/

        private void InfeedConnectedListener(object sender, EventArgs e)
        {
            /*TcpConnection current = ExtractConnection(sender);
            if (current == null) return;
            AppLogger.Log(LogLevel.INFO, "Infeed " + current.ConnectionName + " connected");
            int idx = Convert.ToInt32(current.ConnectionName);
            switch (idx)
            {
                case 0:
                    InfeedTimeoutTimer0.AutoReset = true;
                    InfeedTimeoutTimer0.Enabled = true;
                    break;
                case 1:
                    InfeedTimeoutTimer1.AutoReset = true;
                    InfeedTimeoutTimer1.Enabled = true;
                    break;
                case 2:
                    InfeedTimeoutTimer2.AutoReset = true;
                    InfeedTimeoutTimer2.Enabled = true;
                    break;
                case 3:
                    InfeedTimeoutTimer3.AutoReset = true;
                    InfeedTimeoutTimer3.Enabled = true;
                    break;
                default:
                    AppLogger.Log(LogLevel.ERROR, "Invalid channel: " + current.ConnectionName);
                    break;
            }*/
        }

        private void CommonTimedOutProcedure(int channel)
        {
            /*AppLogger.Log(LogLevel.ERROR, "Infeed " + channel.ToString() + " timed out");
            InfeedInputString[channel] = DateTime.Now.ToString() + spacer + "Infeed ERROR: Connection timeout";
            Infeed[channel].TriggerGeneralUseEvent();
            ClearOutfeedBuffer(channel);*/
        }

        private void OnTimedEvent0(Object source, ElapsedEventArgs e)
        { CommonTimedOutProcedure(0); InfeedTimeoutTimer0.Stop(); }
        private void OnTimedEvent1(Object source, ElapsedEventArgs e)
        { CommonTimedOutProcedure(1); InfeedTimeoutTimer1.Stop(); }
        private void OnTimedEvent2(Object source, ElapsedEventArgs e)
        { CommonTimedOutProcedure(2); InfeedTimeoutTimer2.Stop(); }
        private void OnTimedEvent3(Object source, ElapsedEventArgs e)
        { CommonTimedOutProcedure(3); InfeedTimeoutTimer3.Stop(); }

        private void InfeedDisconnectedListener(object sender, EventArgs e)
        {
            /*TcpConnection current = ExtractConnection(sender);
            if (current == null) return;
            AppLogger.Log(LogLevel.INFO, "Infeed " + current.ConnectionName + " disconnected");
            ClearOutfeedBuffer(Convert.ToInt32(current.ConnectionName));*/
        }

        private void tickOverTimer(int channel)
        {
            switch (channel)
            {
                case 0:
                    InfeedTimeoutTimer0.Stop();
                    InfeedTimeoutTimer0.Start();
                    break;
                case 1:
                    InfeedTimeoutTimer1.Stop();
                    InfeedTimeoutTimer1.Start();
                    break;
                case 2:
                    InfeedTimeoutTimer2.Stop();
                    InfeedTimeoutTimer2.Start();
                    break;
                case 3:
                    InfeedTimeoutTimer3.Stop();
                    InfeedTimeoutTimer3.Start();
                    break;
            }
        }

        private void InfeedDataReceivedListener(object sender, EventArgs e)
        {
            /*TcpConnection current = ExtractConnection(sender);
            if (current == null) return;
            int cIdx = Convert.ToInt32(current.ConnectionName);

            ProcessBarcode(cIdx, current.Response);*/
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


        private void OutfeedConnectedListener(object sender, EventArgs e)
        {
            /*TcpConnection current = ExtractConnection(sender);
            if (current == null) return;
            int channel = Convert.ToInt32(current.ConnectionName);
            AppLogger.Log(LogLevel.INFO, "Outfeed " + current.ConnectionName + " connected");*/
        }


        private void OutfeedDisconnectedListener(object sender, EventArgs e)
        {
            /*TcpConnection current = ExtractConnection(sender);
            if (current == null) return;
            AppLogger.Log(LogLevel.INFO, "Outfeed " + current.ConnectionName + " disconnected");*/
        }


        private void OutfeedDataReceivedListener(object sender, EventArgs e)
        {
            /*TcpConnection current = ExtractConnection(sender);
            if (current == null) return;
            AppLogger.Log(LogLevel.INFO, String.Format("Sent to Outfeed [{0}] received {1}", current.ConnectionName, current.Response));*/
        }

        ~PackM8Engine()
        {
            for (uint i = 0; i < NumChannels; i++)
            {
                Infeed[i].StopListening();
                Outfeed[i].StopListening();
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

        private string logText(int channel, string msg) { return String.Format("Channel[{0}]: {1}", channel.ToString(), msg); }

        public bool LoadRecipeTable(string csvFilePath)
        {
            bool result = false;
            AppLogger.Log(LogLevel.INFO, "Loading Recipe File " + csvFilePath);
            try
            {
                RecipeTable = new DataTable("Recipe");
                using (StreamReader sr = new StreamReader(csvFilePath))
                {
                    while (!sr.EndOfStream)
                    {
                        string[] rows = sr.ReadLine().Split(',');
                        if (RecipeTable.Columns.Count == 0)
                            for (int i = 0; i < rows.Length; i++) RecipeTable.Columns.Add();
                        DataRow dr = RecipeTable.NewRow();
                        for (int i = 0; i < rows.Length; i++) dr[i] = rows[i];
                        RecipeTable.Rows.Add(dr);
                    }
                }
                // the barcodes will be the keys
                // TODO: Add code that will catch if there are non-unique barcodes
                RecipeTable.PrimaryKey = new DataColumn[] { RecipeTable.Columns[2] };

                AppLogger.Log(LogLevel.VERBOSE, "Done loading Recipe File ");
                result = true;
            }
            catch (DataException e)
            {
                Message = String.Format("Error initializing recipe file {0}: {1}", csvFilePath, e.Message);
                AppLogger.Log(LogLevel.ERROR, Message);
                OnMessageUpdated(EventArgs.Empty);
                result = false;
            }

            catch (Exception e)
            {
                Message = String.Format("Error reading recipe file {0}: {1}", csvFilePath, e.Message);
                AppLogger.Log(LogLevel.ERROR, Message);
                OnMessageUpdated(EventArgs.Empty);
                result = false;
            }

            recipeIsOK = result;
            return result;
        }

        // ----------------------------------
        // THIS IS THE HEART OF THE SOFTWARE
        // ----------------------------------
        public DateTime GetBestBeforeDate(DateTime refTime, int numWeeks)
        {
            // Best before date is always the nearest forward best before day (default: Saturday)
            int curDayOfWeek = (int)refTime.DayOfWeek;
            int daysToBB = bestBeforeDayInt - curDayOfWeek;
            if (bestBeforeDayInt < curDayOfWeek) daysToBB += 7;
            return refTime.AddDays(numWeeks * 7 + daysToBB);
        }


        public void DoRecipeLookup(int channel, string barcode)
        {
            string result = String.Empty;
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
            /*if (DisplayOutputString[channel].Contains(badProd))
                ClearOutfeedBuffer(channel);
            else
                Outfeed[channel].Send(OutfeedOutputString[channel]);
            Outfeed[channel].TriggerGeneralUseEvent();*/
        }
    }
}
