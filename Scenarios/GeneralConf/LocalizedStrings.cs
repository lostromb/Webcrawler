using Durandal.Common.Collections;
using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios.GeneralConf
{
    public class LocalizedStrings
    {
        public static FastConcurrentDictionary<LanguageCode, LocalizedStrings> GLOBAL_STRING_DICT = new FastConcurrentDictionary<LanguageCode, LocalizedStrings>();

        static LocalizedStrings()
        {
            GLOBAL_STRING_DICT.Add(LanguageCode.ENGLISH, new LocalizedStrings()
            {
                NameOfTheChurch = "The Church of Jesus Christ of Latter-Day Saints",
                ConferenceAlbumName = "General Conference",
                AprilConferenceNamePattern = "April {0} General Conference",
                OctoberConferenceNamePattern = "October {0} General Conference",
                CopyrightTextPattern = "© {0} by Intellectual Reserve, Inc. All rights reserved.",
            });
        }

        public string ConferenceName(Conference conf)
        {
            if (conf.Phase == ConferencePhase.April)
            {
                return string.Format(AprilConferenceNamePattern, conf.Year);
            }
            else
            {
                return string.Format(OctoberConferenceNamePattern, conf.Year);
            }
        }

        public string CopyrightText(int year)
        {
            return string.Format(CopyrightTextPattern, year);
        }

        public string NameOfTheChurch { get; set; }
        public string ConferenceAlbumName { get; set; }
        public string CopyrightTextPattern { get; set; }
        public string AprilConferenceNamePattern { get; set; }
        public string OctoberConferenceNamePattern { get; set; }
    }
}
