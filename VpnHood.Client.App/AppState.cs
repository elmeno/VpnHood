using System;
using System.Text.Json.Serialization;
using VpnHood.Tunneling.Messages;
using VpnHood.Client.Diagnosing;

namespace VpnHood.Client.App
{
    public class AppState
    {
        [JsonPropertyName("connectionState")]
        public AppConnectionState ConnectionState { get; set; }
        [JsonPropertyName("lastError")]
        public string LastError { get; set; }
        [JsonPropertyName("sessionStatusCode")]
        public ResponseCode? SessionStatusCode { get; set; }
        [JsonPropertyName("activeClientProfileId")]
        public Guid? ActiveClientProfileId { get; internal set; }
        [JsonPropertyName("defaultClientProfileId")]
        public Guid? DefaultClientProfileId { get; set; }
        [JsonPropertyName("isIdle")]
        public bool IsIdle { get; set; }
        [JsonPropertyName("logExists")]
        public bool LogExists { get; set; }
        [JsonPropertyName("lastActiveClientProfileId")]
        public Guid? LastActiveClientProfileId { get; set; }
        [JsonPropertyName("hasDiagnoseStarted")]
        public bool HasDiagnoseStarted { get; set; }
        [JsonPropertyName("hasDisconnectedByUser")]
        public bool HasDisconnectedByUser { get; set; }
        [JsonPropertyName("hasProblemDetected")]
        public bool HasProblemDetected { get; set; }
        [JsonPropertyName("sessionStatus")]
        public SessionStatus SessionStatus { get; set; }
        [JsonPropertyName("sendSpeed")]
        public long SendSpeed { get; set; }
        [JsonPropertyName("sentByteCount")]
        public long SentByteCount { get; set; }
        [JsonPropertyName("receiveSpeed")]
        public long ReceiveSpeed { get; set; }
        [JsonPropertyName("recievedByteCount")]
        public long RecievedByteCount { get; set; }
        [JsonPropertyName("openUrl")]
        public string OpenUrl { get; set; }
    }
}
