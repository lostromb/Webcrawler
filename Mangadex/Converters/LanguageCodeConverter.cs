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
    public class LanguageCodeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is LanguageCode)
            {
                writer.WriteValue(((LanguageCode)value).ToBcp47Alpha2String());
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType == typeof(LanguageCode))
            {
                string stringVal = reader.Value as string;
                if (string.IsNullOrEmpty(stringVal))
                {
                    return null;
                }

                return LanguageCode.Parse(stringVal);
            }
            else if (objectType == typeof(IDictionary<LanguageCode, string>) || objectType == typeof(Dictionary<LanguageCode, string>))
            {
                Dictionary<LanguageCode, string> returnVal = new Dictionary<LanguageCode, string>();
                if (reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Unexpected token", reader.Path, 0, 0, null);
                reader.Read();
                while (reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) throw new JsonSerializationException("Unexpected token", reader.Path, 0, 0, null);
                    LanguageCode key = LanguageCode.Parse(reader.Value as string);
                    reader.Read();
                    if (reader.TokenType != JsonToken.String) throw new JsonSerializationException("Unexpected token", reader.Path, 0, 0, null);
                    string value = reader.Value as string;
                    reader.Read();

                    returnVal[key] = value;
                }

                return returnVal;
            }

            throw new JsonSerializationException("Unknown object type " + objectType);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(LanguageCode) ||
                objectType == typeof(IDictionary<LanguageCode, string>) ||
                objectType == typeof(Dictionary<LanguageCode, string>);
        }
    }
}
