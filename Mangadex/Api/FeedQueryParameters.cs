using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Api
{
    public class FeedQueryParameters
    {
        public Guid MangaId { get; set; }
        public int Limit { get; set; }
        public int Offset { get; set; }
        public IList<LanguageCode> TranslatedLanguage { get; set; }
    }
}
