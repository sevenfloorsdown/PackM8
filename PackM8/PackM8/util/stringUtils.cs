﻿namespace PackM8
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
            if (txt == "<SOH>") hxt = "\x01";
            if (txt == "<STX>") hxt = "\x02";
            if (txt == "<ETX>") hxt = "\x03";
            if (txt == "<EOT>") hxt = "\x04";
            if (txt == "<ENQ>") hxt = "\x05";
            if (txt == "<ACK>") hxt = "\x06";
            if (txt == "<CR>")  hxt = "\x0d";
            if (txt == "<LF>")  hxt = "\x0a";
            if (txt == "<SYN>") hxt = "\x16";
            return hxt;
        }

        public static string ParseIntoASCII(string input)
        {
            string text = input.ToUpper();
            int a = 0;
            int b = 0;
            int c = text.Length;
            for (int i = a; i < c; i += b)
            {
                string txt = ">";
                a = text.IndexOf("<"); 
                b = text.IndexOf(txt) + 1;
                if ((a + 1 != b) && (a < b) && (a >= 0))
                {
                    string tmp = text.Substring(a, b-a);
                    txt = TranslateAsASCII(tmp);
                    text = text.Replace(tmp, txt);
                }
                b = text.IndexOf(txt) + txt.Length;
                c = text.Length;
            }
            return text;
        }
    }
}
