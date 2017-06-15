using System;
using System.Collections.Generic;

namespace PackM8
{
    public class MessageFormat
    {
        private int qty;
        private string qtyTagFill;
        public string Header { get; set; }
        public string Footer { get; set; }
        public string QTYTag { get; set; }
        public int QuantityLength { get; set; }
        public string Description { get; set; }
        public string PLU { get; set; }
        public string PPK { get; set; }
        public int QTY
        {
            get { return qty; }
            set
            {
                qty = value;
                string qtyStr = qty.ToString();
                int tmp = QTYTag.IndexOf(": ");
                if (tmp < 0) tmp = 0;
                if (qtyStr.Length < QuantityLength)
                {
                    String fmt = "D" + QuantityLength.ToString();
                    qtyStr = qty.ToString(fmt);
                }
                string tmpTag = QTYTag;
                qtyTagFill = tmpTag.Insert(tmp + 2, qtyStr);
            }
        }

        public MessageFormat(
            string _header,
            string _footer,
            string _qtytag,
            int _qtylen
            )
        {
            Header = _header;
            Footer = _footer;
            QTYTag = _qtytag;
            QuantityLength = _qtylen;
        }

        public string GetFormattedMessage(string _plu,
                                          string _ppk,
                                          int _qty,
                                          string _description,
                                          bool error=false)
        {
            PLU = _plu;
            PPK = _ppk;
            QTY = _qty;
            Description = _description;
            string result = Header + PLU + "|" + PPK + qtyTagFill + Description;
            if (error) result = Header + PLU + "|" + PPK + Description;
            return result;
        }
    }

    public class OutFeed
    {
        private string _outputMessage;
        public SerialPortManager Port { get; set; }
        public string Header { get; set; }
        public string Footer { get; set; }
        public int InputPLULength { get; set; }
        public int InputPPKLength { get; set; }
        public int OutputPLULength { get; set; }
        public int OutputPPKLength { get; set; }

        public List<MessageFormat> OutputMessage { get; set; }
        public OutFeed(SerialSettings settings)
        {
            Port = new SerialPortManager(settings.PortName)
            {
                Settings = settings
            };
            _outputMessage = string.Empty;
            OutputMessage = new List<MessageFormat>();
        }

        public string CreateErrorOutputMessage(string plu, string ppk, string message, int scenario)
        {
            _outputMessage = OutputMessage[scenario].GetFormattedMessage(plu, ppk, 0, message, true);
            return _outputMessage;
        }

        public string CreateOutputMessage(string plu, string ppk, int quantity, string description, int scenario)
        {
            string outputPlu = String.Empty;
            string outputPpk = String.Empty;
            string message = description;
            int scen = scenario - 1;
            try
            {
                int startX = InputPLULength - OutputPLULength;
                if (startX < 0) startX = 0;
                if (startX > 0) outputPlu = new string('0', startX);
                outputPlu = outputPlu + plu.Substring(startX, OutputPLULength);
            }
            catch (Exception e)
            {
                message = String.Format("Mismatch with input and output PLU lengths: {0} vs {1}, {2}",
                    InputPLULength.ToString(), OutputPLULength.ToString(), e.Message);
                _outputMessage = OutputMessage[scen].GetFormattedMessage("xxxxxx", "yyyyy", 0, message, true);
                throw new Exception(message);
            }
            try
            {
                decimal tmpPrice = Convert.ToDecimal(ppk);
                tmpPrice = tmpPrice / 100;
                outputPpk = tmpPrice.ToString("0.00");
                if (outputPpk.Length > OutputPPKLength)
                    outputPpk = outputPpk.Substring(outputPpk.Length - OutputPPKLength, OutputPPKLength);
                outputPpk = outputPpk + " ";
            }
            catch (Exception e)
            {
                message = String.Format("Mismatch with input and output PPK lengths: {0} vs {1}, {2}",
                    InputPPKLength.ToString(), OutputPPKLength.ToString(), e.Message);
                _outputMessage = OutputMessage[scen].GetFormattedMessage("xxxxxx", "yyyyy", 0, message, true);
                throw new Exception(message);
            }
            _outputMessage = OutputMessage[scen].GetFormattedMessage(outputPlu, outputPpk, quantity, message);
            return _outputMessage;
        }

        public void SendMessage(string message)
        {
            Port.WriteLine(Header + message + Footer);
        }

        public void SendOutputMessage()
        {
            Port.WriteLine(Header + _outputMessage + Footer);
        }

    }
}
