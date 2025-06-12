using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Schemas
{
    public class UserEntity : MangadexEntity
    {
        [JsonProperty("attributes")]
        public UserAttributes Attributes { get; set; }
    }
}
