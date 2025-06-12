using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebCrawler.Mangadex.Schemas;

namespace WebCrawler.Mangadex.Converters
{
    public class EntityTypeConverter : JsonConverter<MangadexEntityType>
    {
        public override void WriteJson(JsonWriter writer, MangadexEntityType value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override MangadexEntityType ReadJson(JsonReader reader, Type objectType, MangadexEntityType existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string stringVal = reader.Value as string;
            if (string.IsNullOrEmpty(stringVal))
            {
                return MangadexEntityType.Unknown;
            }

            if (string.Equals(stringVal, "chapter", StringComparison.OrdinalIgnoreCase))
            {
                return MangadexEntityType.Chapter;
            }
            else if (string.Equals(stringVal, "manga", StringComparison.OrdinalIgnoreCase))
            {
                return MangadexEntityType.Manga;
            }
            else if (string.Equals(stringVal, "scanlation_group", StringComparison.OrdinalIgnoreCase))
            {
                return MangadexEntityType.ScanlationGroup;
            }
            else if (string.Equals(stringVal, "user", StringComparison.OrdinalIgnoreCase))
            {
                return MangadexEntityType.User;
            }

            return MangadexEntityType.Unknown;
        }
    }
}
