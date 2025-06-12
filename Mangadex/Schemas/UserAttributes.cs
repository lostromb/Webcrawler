using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Schemas
{
    public class UserAttributes
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("roles")]
        public IList<string> Roles { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }
    }
}
