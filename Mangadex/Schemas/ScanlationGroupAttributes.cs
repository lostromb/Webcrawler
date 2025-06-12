using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Schemas
{
    public class ScanlationGroupAttributes
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("altNames")]
        public IList<IDictionary<LanguageCode, string>> AltNames { get; set; }

        [JsonProperty("website")]
        public string Website { get; set; }

        [JsonProperty("ircServer")]
        public string IrcServer { get; set; }

        [JsonProperty("ircChannel")]
        public string IrcChannel { get; set; }

        [JsonProperty("discord")]
        public string Discord { get; set; }

        [JsonProperty("contactEmail")]
        public string ContactEmail { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("twitter")]
        public string Twitter { get; set; }

        //[JsonProperty("mangaUpdates")]
        //public string MangaUpdates { get; set; }

        [JsonProperty("focusedLanguages")]
        public IList<LanguageCode> FocusedLanguages { get; set; }

        [JsonProperty("official")]
        public bool Official { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("inactive")]
        public bool Inactive { get; set; }

        //[JsonProperty("publishDelay")]
        //public string PublishDelay { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }
    }
}
