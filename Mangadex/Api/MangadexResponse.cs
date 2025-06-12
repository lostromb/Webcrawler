using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Api
{
    public abstract class MangadexResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("response")]
        public string Response { get; set; }
    }
}
