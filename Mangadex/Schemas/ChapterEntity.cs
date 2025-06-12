using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Mangadex.Schemas
{
    public class ChapterEntity : MangadexEntity
    {
        [JsonProperty("attributes")]
        public ChapterAttributes Attributes { get; set; }

        [JsonProperty("relationships")]
        public IList<MangadexEntity> Relationships { get; set; }
    }
}
