using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios.GeneralConf
{
    public static class Common
    {
        public static string SanitizeFileName(string input)
        {
            //input = input.Replace('“', '\"'); // fancy quotes are ok for files but plain quotes aren't
            //input = input.Replace('”', '\"');
            input = input.Replace('—', '-'); //em dash  to hyphen
            input = input.Replace('–', '-'); //en dash
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }

            return input;
        }
        public static string SanitizeFolderName(string input)
        {
            //input = input.Replace('“', '\"'); // fancy quotes are ok for files but plain quotes aren't
            //input = input.Replace('”', '\"');
            input = input.Replace('—', '-'); //em dash to hyphen
            input = input.Replace('–', '-'); //en dash
            foreach (char c in Path.GetInvalidPathChars())
            {
                input = input.Replace(c, '_');
            }

            return input;
        }
    }
}
