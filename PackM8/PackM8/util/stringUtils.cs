
using System;

namespace PackM8
{
    /**
     * Translate some strings into hex bytes to send later on
     **/
    public class StringUtils
    {
        public static string TranslateAsASCII(string input)
        {
            string hxt = input.ToUpper();
            string txt = hxt;
            if (txt == "SOH") hxt = "\x01";
            if (txt == "STX") hxt = "\x02";
            if (txt == "ETX") hxt = "\x03";
            if (txt == "EOT") hxt = "\x04";
            if (txt == "ENQ") hxt = "\x05";
            if (txt == "ACK") hxt = "\x06";
            if (txt == "LF") hxt = "\x0a";
            if (txt == "VT") hxt = "\x0b";
            if (txt == "FF") hxt = "\x0c";
            if (txt == "CR") hxt = "\x0d";
            if (txt == "SO") hxt = "\x0e";
            if (txt == "SI") hxt = "\x0f";
            if (txt == "DC1") hxt = "\x11";
            if (txt == "DC2") hxt = "\x12";
            if (txt == "DC3") hxt = "\x13";
            if (txt == "DC4") hxt = "\x14";
            if (txt == "NAK") hxt = "\x15";
            if (txt == "SYN") hxt = "\x16";
            return hxt;
        }

        public static string ParseIntoASCII(string input)
        {
            string text = "";
            try
            {
                foreach (string x in (input.ToUpper()).Split('[', ']'))
                {
                    if (x != String.Empty)
                        text += TranslateAsASCII(x);
                }
            }
            catch (ArgumentOutOfRangeException e)
            {
                text = input; ;
            }
            return text;
        }
    }
}
