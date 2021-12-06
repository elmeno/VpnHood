using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VpnHood.Common;
using VpnHood.Logging;

namespace VpnHood.Server.AccessServers
{
    public class RestAccessServer : IAccessServer
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _authHeader;
        public string ValidCertificateThumbprint { get; set; }
        public Uri BaseUri { get; }

        private static string AppFolderPath => Path.GetDirectoryName(typeof(RestAccessServer).Assembly.Location);
        private static string WorkingFolderPath { get; set; } = AppFolderPath;
        public string StoragePath { get; }
        private readonly string _sslCertificatesPassword = null;
        public string CertsFolderPath => Path.Combine(StoragePath, "certificates");
        public string GetCertFilePath(IPEndPoint ipEndPoint) => Path.Combine(CertsFolderPath, ipEndPoint.ToString().Replace(":", "-") + ".pfx");
        public X509Certificate2 DefaultCert { get; }


        private static X509Certificate2 CreateSelfSignedCertificate(string certFilePath, string password)
        {
            // VhLogger.Instance.LogInformation($"Creating Certificate file: {certFilePath}");
            var certificate = CertificateUtil.CreateSelfSigned();
            var buf = certificate.Export(X509ContentType.Pfx, password);
            Directory.CreateDirectory(Path.GetDirectoryName(certFilePath));
            File.WriteAllBytes(certFilePath, buf);
            return new X509Certificate2(certFilePath, password, X509KeyStorageFlags.Exportable);
        }

        public RestAccessServer(string storagePath, Uri baseUri, string authHeader)
        {
            
            StoragePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
            Directory.CreateDirectory(StoragePath);

            var defaultCertFile = Path.Combine(CertsFolderPath, "default.pfx");
            DefaultCert = File.Exists(defaultCertFile)
                ? new X509Certificate2(defaultCertFile, _sslCertificatesPassword)
                : CreateSelfSignedCertificate(defaultCertFile, _sslCertificatesPassword);
            //if (baseUri.Scheme != Uri.UriSchemeHttps)
            //  throw new ArgumentException("baseUri must be https!", nameof(baseUri));

            BaseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
            _authHeader = authHeader ?? throw new ArgumentNullException(nameof(authHeader));

            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = ServerCertificateCustomValidationCallback
            };
            _httpClient = new HttpClient(handler);
        }

        private bool ServerCertificateCustomValidationCallback(HttpRequestMessage httpRequestMessage, X509Certificate2 x509Certificate2, X509Chain x509Chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None || x509Certificate2.Thumbprint.Equals(ValidCertificateThumbprint, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<T> SendRequest<T>(string api, object paramerters, HttpMethod httpMethod, bool useBody)
        {
            var uriBuilder = new UriBuilder(new Uri(BaseUri, api));

            // use query string
            if (useBody)
            {
                var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
                var type = paramerters.GetType();
                foreach (var prop in type.GetProperties())
                {
                    var value = prop.GetValue(paramerters, null)?.ToString();
                    if (value != null)
                        query.Add(prop.Name, value);
                }
                uriBuilder.Query = query.ToString();
            }

            // create request
            var uri = uriBuilder.ToString();
            var requestMessage = new HttpRequestMessage(httpMethod, uri);
            requestMessage.Headers.Add("authorization", _authHeader);
            if (useBody)
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(paramerters), Encoding.UTF8, "application/json");

            // send request
            var res = await _httpClient.SendAsync(requestMessage);
            using var stream = await res.Content.ReadAsStreamAsync();
            var streamReader = new StreamReader(stream);
            var ret = streamReader.ReadToEnd();

            if (res.StatusCode != HttpStatusCode.OK)
                throw new Exception($"Invalid status code from RestAccessServer! Status: {res.StatusCode}, Message: {ret}");

            var jsonSerializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(ret, jsonSerializerOptions);
        }

        public Task<Access> GetAccess(ClientIdentity clientIdentity) =>
            SendRequest<Access>(nameof(GetAccess), clientIdentity, HttpMethod.Post, true);

        public Task<Access> AddUsage(AddUsageParams addUsageParams) =>
            SendRequest<Access>(nameof(AddUsage), addUsageParams, HttpMethod.Post, true);

        // public Task<byte[]> GetSslCertificateData(string serverEndPoint) =>
            // SendRequest<byte[]>(nameof(GetSslCertificateData), new { serverEndPoint }, HttpMethod.Get, false);

        private X509Certificate2 GetSslCertificate(IPEndPoint serverEndPoint, bool returnDefaultIfNotFound)
        {
            var certFilePath = GetCertFilePath(serverEndPoint);
            if (returnDefaultIfNotFound && !File.Exists(certFilePath))
                return DefaultCert;
            return new X509Certificate2(certFilePath, _sslCertificatesPassword, X509KeyStorageFlags.Exportable);
        }

        public Task<byte[]> GetSslCertificateData(string serverEndPoint)
            => Task.FromResult(GetSslCertificate(Util.ParseIpEndPoint(serverEndPoint), true).Export(X509ContentType.Pfx));
    }
}
