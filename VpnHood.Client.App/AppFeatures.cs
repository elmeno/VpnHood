using System;
using System.Text.Json.Serialization;

namespace VpnHood.Client.App
{
    public class AppFeatures
    {
        [JsonPropertyName("version")]
        public string Version => typeof(VpnHoodApp).Assembly.GetName().Version.ToString(3);
        [JsonPropertyName("isExcludeApplicationsSupported")]
        public bool IsExcludeApplicationsSupported { get; set; }
        [JsonPropertyName("isIncludeApplicationsSupported")]
        public bool IsIncludeApplicationsSupported { get; set; }
        [JsonPropertyName("isExcludeNetworksSupported")]
        public bool IsExcludeNetworksSupported { get; set; }
        [JsonPropertyName("isIncludeNetworksSupported")]
        public bool IsIncludeNetworksSupported { get; set; }
    }
}
