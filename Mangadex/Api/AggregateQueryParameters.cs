using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Api
{
    public class AggregateQueryParameters
    {
        public Guid MangaId { get; set; }
        public IList<LanguageCode> TranslatedLanguage { get; set; }
    }
}
