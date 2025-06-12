using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Schemas
{
    public class MangaChapter
    {
        [JsonProperty("chapter")]
        public string Chapter { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("others")]
        public IList<Guid> Others { get; set; }
    }
}
