using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebCrawler.Mangadex.Schemas;

namespace WebCrawler.Mangadex.Api
{
    public class AggregateQueryResponse : MangadexResponse
    {
        [Newtonsoft.Json.JsonProperty("volumes")]
        public IDictionary<string, MangaVolume> Volumes { get; set; }
    }
}
