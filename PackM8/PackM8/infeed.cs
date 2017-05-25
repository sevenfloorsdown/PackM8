﻿
using System;
using System.Text.RegularExpressions;

namespace PackM8
{

    public delegate void InfeedEventHandler(object sender, EventArgs e, int index);

    public class InFeed
    {
        private string buffer = string.Empty;
        private Regex inRegex;
        private string pattern;
        public SerialPortManager Port { get; set; }
        public int Index { get; set; }
        public string SwitchValue { get; set; }
        public string InFeedData { get; set; }
        public string ReceivedData { get; set; }
        public string Header { get; set; }
        public string Footer { get; set; }
        public bool CheckMessageFormat { get; set; }
        public int PLULength { get; set; }
        public int PPKLength { get; set; }
        public event InfeedEventHandler DataUpdated = delegate { };
        public event InfeedEventHandler DataReceived = delegate { };

        protected virtual void OnDataUpdated(EventArgs e, int index) { DataUpdated?.Invoke(this, e, index); }
        protected virtual void OnDataReceived(EventArgs e, int index) { DataReceived?.Invoke(this, e, index); }
  
        public string MessageFormat {
            get { return pattern;  }
            set
            {
                pattern = value;
                if (!String.IsNullOrEmpty(value.Trim()))
                    CheckMessageFormat = true;
                inRegex = new Regex(value, RegexOptions.None);
            }
        }

        public InFeed(SerialSettings settings)
        {
            Port = new SerialPortManager(settings.PortName)
            {
                Settings = settings
            };
            Port.NewSerialDataReceived += new EventHandler<SerialDataEventArgs>(SerialDataReceived);
        }

        void SerialDataReceived(object sender, SerialDataEventArgs e)
        {
            // collect data until delimiter comes in         
            ReceivedData = System.Text.Encoding.UTF8.GetString(e.Data);           
            OnDataReceived(EventArgs.Empty, Index);
            if (BufferDataUpdated(ReceivedData))
                OnDataUpdated(EventArgs.Empty, Index);         
        }

        public bool IsInCorrectFormat(string value)
        {
            if (pattern == String.Empty) return true;
            MatchCollection matches = inRegex.Matches(value);
            if (matches.Count != 1) return false;
            return true;
        }

        public bool BufferDataUpdated(string data)
        {
            int s = data.IndexOf(Header);
            int e = data.IndexOf(Footer);
            bool nobuf = buffer == string.Empty;

            if (s > -1)
            {
                if (e > -1 && e > s)
                {
                    string sub = data.Substring(s + 1, e - s - 1);
                    if (CheckMessageFormat && !IsInCorrectFormat(sub)) return false;
                    if (!String.IsNullOrEmpty(InFeedData))
                        if (InFeedData.Equals(sub)) return false;
                    InFeedData = sub;
                    buffer = string.Empty;
                    return true;
                }
                else if (e == -1)
                {
                    buffer = data.Substring(s+1);
                    return false;
                }
            }
            else
            {
                if (!nobuf)
                {
                    if (e > -1)
                    {
                        buffer += data.Substring(0, e);
                        if (CheckMessageFormat && !IsInCorrectFormat(buffer)) return false;
                        if (!String.IsNullOrEmpty(InFeedData))
                            if (InFeedData.Equals(buffer)) return false;
                        InFeedData = buffer;
                        buffer = string.Empty;
                        return true;
                    }
                }
                buffer += data;
                return false;  
            }
            return false;
        }
    }
}