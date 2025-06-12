using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Api
{
    public abstract class CollectionResponse<T> : MangadexResponse
    {
        [JsonProperty("data")]
        public IList<T> Data { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; }

        [JsonProperty("offset")]
        public int Offset { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }
    }
}
