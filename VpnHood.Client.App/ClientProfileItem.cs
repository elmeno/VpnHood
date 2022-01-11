using System;
using System.Text.Json.Serialization;
using VpnHood.Common;

namespace VpnHood.Client.App
{
    public class ClientProfileItem
    {
        [JsonPropertyName("id")]
        public Guid Id => ClientProfile.ClientProfileId;
        [JsonPropertyName("clientProfile")]
        public ClientProfile ClientProfile { get; set; }
        [JsonPropertyName("token")]
        public Token Token { get; set; }
    }

}
