﻿using System;

namespace VpnHood.Server
{
    public class ClientIdentity
    {
        public string Token { get; set; }
        public string UserToken { get; set; }
        public Guid ClientId { get; set; }
        public string ClientIp { get; set; }
        public string ClientVersion { get; set; }
    }
}
