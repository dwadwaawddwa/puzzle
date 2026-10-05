using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PuzzleStudio.Core.Pack
{
    /// <summary>Shared JSON settings for packs, saves and projects.</summary>
    public static class PackJson
    {
        public static readonly JsonSerializerSettings Settings = Create(Formatting.Indented);

        public static JsonSerializerSettings Create(Formatting formatting)
        {
            var s = new JsonSerializerSettings
            {
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                // Replace (not append to) the default list values declared in the data classes.
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                FloatFormatHandling = FloatFormatHandling.DefaultValue,
            };
            s.Converters.Add(new StringEnumConverter());
            return s;
        }

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);
        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
        public static JsonSerializer Serializer() => JsonSerializer.Create(Settings);
    }
}
