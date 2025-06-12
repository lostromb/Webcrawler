using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios.GeneralConf
{
    public class Conference
    {
        public ConferencePhase Phase { get; set; }
        public int Year { get; set; }

        public Conference(ConferencePhase phase, int year)
        {
            Phase = phase;
            Year = year;
        }

        public override string ToString()
        {
            return Phase.ToString() + " " + Year.ToString();
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
