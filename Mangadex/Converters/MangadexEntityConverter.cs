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
    public class MangadexEntityConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(MangadexEntity);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            JObject rawObject = serializer.Deserialize<JObject>(reader);
            string type = rawObject["type"].Value<string>();
            switch (type)
            {
                case "chapter":
                    return rawObject.ToObject<ChapterEntity>(serializer);
                case "scanlation_group":
                    return rawObject.ToObject<ScanlationGroupEntity>(serializer);
                case "manga":
                    return rawObject.ToObject<MangaEntity>(serializer);
                case "user":
                    return rawObject.ToObject<UserEntity>(serializer);
                default:
                    return rawObject.ToObject<UnknownEntity>(serializer);
            }
        }
    }
}
