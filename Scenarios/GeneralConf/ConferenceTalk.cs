using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios.GeneralConf
{
    public class ConferenceTalk
    {
        public Conference Conference { get; set; }
        public LanguageCode Language { get; set; }
        public string Title { get; set; }
        public string Speaker { get; set; }
        public int SessionIndex { get; set; }
        public int TalkIndex { get; set; }
        public string InternalName { get; set; }
        public Uri PageUrl { get; set; }
        public bool HasVideoThumbnail { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1} ({2} {3})",
                Speaker,
                Title,
                SessionIndex,
                TalkIndex);
        }
    }
}
