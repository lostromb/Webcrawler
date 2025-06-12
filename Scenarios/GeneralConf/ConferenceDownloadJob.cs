using Durandal.Common.NLP.Language;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios.GeneralConf
{
    public class ConferenceDownloadJob
    {
        public ConferencePhase Phase;
        public int Year;
        public LanguageCode Language;

        public ConferenceDownloadJob(int year, ConferencePhase phase, LanguageCode language)
        {
            Year = year;
            Phase = phase;
            Language = language.AssertNonNull(nameof(language));
        }

        public Uri PageUrl
        {
            get
            {
                return new Uri(string.Format("https://www.churchofjesuschrist.org/study/general-conference/{0}/{1}?lang={2}",
                    Year,
                    Phase == ConferencePhase.April ? "04" : "10",
                    Language.ToBcp47Alpha3String()));
            }
        }

        public override string ToString()
        {
            return Phase.ToString() + " " + Year.ToString() + " General Conference";
        }

        public override bool Equals(object obj)
        {
            if (obj == null ||
                !(obj is Conference))
            {
                return false;
            }

            Conference other = obj as Conference;
            return Phase == other.Phase &&
                Year == other.Year;
        }

        public override int GetHashCode()
        {
            return Phase.GetHashCode() + Year.GetHashCode();
        }
    }
}
