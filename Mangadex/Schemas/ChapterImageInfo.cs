using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Schemas
{
    public class ChapterImageInfo
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("data")]
        public IList<string> Data { get; set; }

        [JsonProperty("dataSaver")]
        public IList<string> DataSaver { get; set; }
    }
}
