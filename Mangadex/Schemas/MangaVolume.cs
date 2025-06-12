using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Schemas
{
    public class MangaVolume
    {
        [JsonProperty("volume")]
        public string Volume { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("chapters")]
        public IDictionary<string, MangaChapter> Chapters { get; set; }
    }
}
