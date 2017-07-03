
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PackM8
{
    /**
     * Translate some strings into hex bytes to send later on
     **/
    public class StringUtils
    {
        private static Dictionary<string, string> ControlChars = new Dictionary<string, string>
        {
            {"NUL", "\x00"},
            {"SOH", "\x01"},
            {"STX", "\x02"},
            {"ETX", "\x03"},
            {"EOT", "\x04"},
            {"ENQ", "\x05"},
            {"ACK", "\x06"},
            {"BEL", "\x07"},
            {"BS",  "\x08"},
            {"TAB", "\x09"},
            {"LF",  "\x0a"},
            {"VT",  "\x0b"},
            {"FF",  "\x0c"},
            {"CR",  "\x0d"},
            {"SO",  "\x0e"},
            {"SI",  "\x0f"},
            {"DLE", "\x10"},
            {"DC1", "\x11"},
            {"DC2", "\x12"},
            {"DC3", "\x13"},
            {"DC4", "\x14"},
            {"NAK", "\x15"},
            {"SYN", "\x16"}
        };

        public static string TranslateAsASCII(string input)
        {
            string hxt = input.ToUpper();
            string txt = hxt;
            if (ControlChars.ContainsKey(txt))
                hxt = ControlChars[txt];
            return hxt;
        }

        public static string StringifyControlChars(String input)
        {
            List<string> chars = new List<string>( ControlChars.Keys);
            try
            {
                string hxt = Regex.Replace(input, @"\p{Cc}",
                    a => string.Format("[{0}]", chars[(byte)a.Value[0]]));
                return hxt;
            }
            catch (ArgumentOutOfRangeException e)
            {
                return Regex.Replace(input, @"\p{Cc}",
                    a => string.Format("[{0:X2}]", (byte)a.Value[0]));
            }
            catch (Exception e)
            {
                return input;
            }
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
                text = input; 
            }
            return text;
        }
    }
}
