using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Api
{
    public abstract class EntityResponse<T> : MangadexResponse
    {
        [JsonProperty("data")]
        public T Data { get; set; }
    }
}
