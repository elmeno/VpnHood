﻿using System;

namespace VpnHood.Tunneling.Messages
{
    public class HelloRequest 
    {
        public string ClientVersion { get; set; }
        public string Token { get; set; }
        public Guid ClientId { get; set; }
        public bool UseUdpChannel { get; set; }
    }
}
