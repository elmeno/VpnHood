using System;

namespace VpnHood.Server
{
    public class Access
    {
        public string AccessId { get; set; }
        public DateTime? ExpirationTime { get; set; }
        public int MaxClientCount { get; set; }
        public long MaxTrafficByteCount { get; set; }
        public long SentTrafficByteCount { get; set; }
        public long ReceivedTrafficByteCount { get; set; }
        public string Message { get; set; }
        public AccessStatusCode StatusCode { get; set; }
    }
}