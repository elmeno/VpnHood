using System;
using System.Linq;
using VpnHood.Common;

namespace VpnHood.Client
{
    public class ClientProfile
    {
        public string Name { get; set; }
        public string CountryCode { get; set; }
        public Guid ClientProfileId { get; set; }
        public Guid TokenId { get; set; }
        public Token Token { get; set; }
        public string[] ServerEndPoints { get; set; }
        public byte[] Secret { get; set; }
        public string DnsName { get; set; }
        public bool IsValidDns { get; set; }
        public byte[] CertificateHash { get; set; }


        public string ServerEndPoint { get => ServerEndPoints.FirstOrDefault(); set => ServerEndPoints = new string[] { value }; }
    }
}
