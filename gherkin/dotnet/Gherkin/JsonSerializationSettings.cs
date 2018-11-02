using Newtonsoft.Json;

namespace Gherkin
{
    public class JsonSerializationSettings
    {
        public static JsonSerializerSettings CreateJsonSerializerSettings()
        {
            var jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.Formatting = Formatting.None;
            jsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            jsonSerializerSettings.ContractResolver = new FeatureAstJsonContractResolver();
            return jsonSerializerSettings;
        }
    }
}