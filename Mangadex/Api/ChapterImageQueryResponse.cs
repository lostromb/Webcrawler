using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebCrawler.Mangadex.Schemas;

namespace WebCrawler.Mangadex.Api
{
    public class ChapterImageQueryResponse : MangadexResponse
    {
        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; }

        [JsonProperty("chapter")]
        public ChapterImageInfo Chapter { get; set; }
    }
}
