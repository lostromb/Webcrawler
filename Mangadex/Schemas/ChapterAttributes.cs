using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Schemas
{
    public class ChapterAttributes
    {
        [JsonProperty("volume")]
        public string Volume { get; set; }

        [JsonProperty("chapter")]
        public string Chapter { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("translatedLanguage")]
        public LanguageCode TranslatedLanguage { get; set; }

        [JsonProperty("externalUrl")]
        public string ExternalUrl { get; set; }

        [JsonProperty("publishAt")]
        public DateTimeOffset? PublishAt { get; set; }

        [JsonProperty("readableAt")]
        public DateTimeOffset? ReadableAt { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("pages")]
        public int Pages { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }
    }
}
