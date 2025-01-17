﻿using Microsoft.Extensions.Logging;
using PacketDotNet;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using VpnHood.Common;
using VpnHood.Tunneling;
using VpnHood.Logging;
using VpnHood.Tunneling.Messages;
using VpnHood.Client.Device;
using System.Collections.Generic;

namespace VpnHood.Client
{
    public class VpnHoodClient : IDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        private ILogger _logger => VhLogger.Instance;
        private readonly IPacketCapture _packetCapture;
        private readonly bool _leavePacketCaptureOpen;
        private TcpProxyHost _tcpProxyHost;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly int _minTcpDatagramChannelCount;
        private bool _disposed;
        private bool _isManagaingDatagramChannels;
        private DateTime _lastIntervalCheckTime = DateTime.MinValue;
        private DateTime? _lastConnectionErrorTime = null;

        internal Nat Nat { get; }
        internal Tunnel Tunnel { get; private set; }
        public int Timeout { get; set; }
        public string Token { get; }
        public Guid ClientId { get; }
        public int SessionId { get; private set; }
        public byte[] SessionKey { get; private set; }
        public ClientProfile ActiveClientProfile;
        public string[] ServerEndPoints;
        public string ServerId { get; private set; }
        public bool Connected { get; private set; }
        public IPAddress TcpProxyLoopbackAddress { get; }
        public IPAddress DnsAddress { get; set; }
        public event EventHandler StateChanged;
        public SessionStatus SessionStatus { get; private set; }
        public string Version { get; }
        public long ReceiveSpeed => Tunnel?.ReceiveSpeed ?? 0;
        public long ReceivedByteCount => Tunnel?.ReceivedByteCount ?? 0;
        public long SendSpeed => Tunnel?.SendSpeed ?? 0;
        public long SentByteCount => Tunnel?.SentByteCount ?? 0;
        public bool UseUdpChannel { get; set; }

        public VpnHoodClient(IPacketCapture packetCapture, Guid clientId, ClientProfile activeClientProfile, string token, ClientOptions options)
        {
            _packetCapture = packetCapture ?? throw new ArgumentNullException(nameof(packetCapture));
            _leavePacketCaptureOpen = options.LeavePacketCaptureOpen;
            _minTcpDatagramChannelCount = options.MinTcpDatagramChannelCount;
            Token = token ?? throw new ArgumentNullException(nameof(token));
            DnsAddress = options.DnsAddress ?? throw new ArgumentNullException(nameof(options.DnsAddress));
            TcpProxyLoopbackAddress = options.TcpProxyLoopbackAddress ?? throw new ArgumentNullException(nameof(options.TcpProxyLoopbackAddress));
            ClientId = clientId;
            ActiveClientProfile = activeClientProfile;
            Timeout = options.Timeout;
            Version = options.Version;
            UseUdpChannel = options.UseUdpChannel;
            Nat = new Nat(true);

            packetCapture.OnStopped += PacketCature_OnStopped;
        }

        private ClientState _state = ClientState.None;
        public ClientState State
        {
            get => _state;
            private set
            {
                if (_state == value) return;
                _state = value;
                _logger.LogInformation($"Client is {State}");
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void PacketCature_OnStopped(object sender, EventArgs e)
        {
            _logger.LogInformation("Device has been stopped!");
            Dispose();
        }

        private IPEndPoint _serverEndPoint;
        public IPEndPoint ServerTcpEndPoint
        {
            get
            {
                if (_serverEndPoint != null)
                    return _serverEndPoint;

                var random = new Random();
                if (ActiveClientProfile.IsValidDns)
                {
                    try
                    {
                        _logger.LogInformation($"Resolving IP from host name: {VhLogger.FormatDns(ActiveClientProfile.DnsName)}...");
                        var hostEntry = Dns.GetHostEntry(ActiveClientProfile.DnsName);
                        if (hostEntry.AddressList.Length > 0)
                        {
                            var index = random.Next(0, hostEntry.AddressList.Length);
                            var ip = hostEntry.AddressList[index];
                            var serverEndPoint = Util.ParseIpEndPoint(ActiveClientProfile.ServerEndPoint);

                            _logger.LogInformation($"{hostEntry.AddressList.Length} IP founds. {ip}:{serverEndPoint.Port} has been Selected!");
                            _serverEndPoint = new IPEndPoint(ip, serverEndPoint.Port);
                            return _serverEndPoint;
                        }
                    }
                    catch {};
                }
                else
                {
                    //_logger.LogInformation($"Extracting host from the token. Host: {VhLogger.FormatDns(ServerEndPoint)}");
                    var index = random.Next(0, ActiveClientProfile.ServerEndPoints.Length);
                    _serverEndPoint = Util.ParseIpEndPoint(ActiveClientProfile.ServerEndPoints[index]);
                    return _serverEndPoint;
                }

                throw new Exception("Could not resolve Server Address!");
            }
        }

        public async Task Connect()
        {
            _ = _logger.BeginScope("Client");
            if (_disposed) throw new ObjectDisposedException(nameof(VpnHoodClient));

            if (State != ClientState.None)
                throw new Exception("Connection is already in progress!");

            // Replace dot in version to prevent anonymous make treat it as ip.
            _logger.LogInformation($"Client is connecting. Version: {Version}");

            // Starting
            State = ClientState.Connecting;
            SessionStatus = new SessionStatus();
            _cancellationTokenSource = new CancellationTokenSource();

            // Connect
            try
            {
                // Preparing tunnel
                Tunnel = new Tunnel();
                Tunnel.OnPacketReceived += Tunnel_OnPacketReceived;

                // Establish first connection and create a session
                await Task.Run(() => ConnectInternal(_cancellationTokenSource.Token));

                // create Tcp Proxy Host
                _logger.LogTrace($"Creating {VhLogger.FormatTypeName<TcpProxyHost>()}...");
                _tcpProxyHost = new TcpProxyHost(this, _packetCapture, TcpProxyLoopbackAddress);
                var _ = _tcpProxyHost.StartListening();

                // Preparing device
                if (!_packetCapture.Started)
                {
                    // Exclude serverEp
                    if (_packetCapture.IsExcludeNetworksSupported)
                        _packetCapture.ExcludeNetworks = _packetCapture.ExcludeNetworks != null
                            ? _packetCapture.ExcludeNetworks.Concat(new IPNetwork[] { new IPNetwork(ServerTcpEndPoint.Address) }).ToArray()
                            : new IPNetwork[] { new IPNetwork(ServerTcpEndPoint.Address) }.ToArray();

                    _packetCapture.OnPacketArrivalFromInbound += PacketCapture_OnPacketArrivalFromInbound;
                    _packetCapture.StartCapture();
                }

                State = ClientState.Connected;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error! {ex}");
                Dispose();
                throw;
            }
        }

        private void IntervalCheck()
        {
            // check is in progress
            lock (this)
            {
                if ((DateTime.Now - _lastIntervalCheckTime).TotalSeconds < 5)
                    return;
                _lastIntervalCheckTime = DateTime.Now;
            }


            var _ = ManageDatagramChannels(_cancellationTokenSource.Token);
        }

        // WARNING: Performance Critical!
        private void Tunnel_OnPacketReceived(object sender, ChannelPacketArrivalEventArgs e)
        {
            // manage DNS reply
            foreach (var ipPacket in e.IpPackets)
                UpdateDnsRequest(ipPacket, false);

            // forward packet to device
            _packetCapture.SendPacketToInbound(e.IpPackets);
        }

        // WARNING: Performance Critical!
        private void PacketCapture_OnPacketArrivalFromInbound(object sender, PacketCaptureArrivalEventArgs e)
        {
            try
            {
                IntervalCheck();

                var ipPackets = new List<IPPacket>(); //todo cache
                foreach (var arivalPacket in e.ArivalPackets)
                {
                    var ipPacket = arivalPacket.IpPacket;
                    if (_cancellationTokenSource.IsCancellationRequested) return;
                    if (arivalPacket.IsHandled || ipPacket.Version != IPVersion.IPv4)
                        continue;

                    // tunnel only Udp and Icmp packets
                    if (ipPacket.Protocol == PacketDotNet.ProtocolType.Udp || ipPacket.Protocol == PacketDotNet.ProtocolType.Icmp)
                    {
                        UpdateDnsRequest(ipPacket, true);
                        arivalPacket.IsHandled = true;
                        ipPackets.Add(ipPacket);
                    }
                }

                if (ipPackets.Count > 0)
                    Tunnel.SendPacket(ipPackets.ToArray());

            }
            catch (Exception ex)
            {
                VhLogger.Instance.LogError($"{VhLogger.FormatTypeName(this)}: Error in processing packet! Error: {ex}");
            }
        }

        private void UpdateDnsRequest(IPPacket ipPacket, bool outgoing)
        {
            if (ipPacket.Protocol != PacketDotNet.ProtocolType.Udp) return;

            // manage DNS outgoing packet if requested DNS is not VPN DNS
            if (outgoing && !ipPacket.DestinationAddress.Equals(DnsAddress))
            {
                var udpPacket = ipPacket.Extract<UdpPacket>();
                if (udpPacket == null) return;

                if (udpPacket.DestinationPort == 53) //53 is DNS port
                {
                    _logger.Log(LogLevel.Information, GeneralEventId.Dns, $"DNS request from {VhLogger.Format(ipPacket.SourceAddress)}:{udpPacket.SourcePort} to {VhLogger.Format(ipPacket.DestinationAddress)}, Map to: {VhLogger.Format(DnsAddress)}");

                    udpPacket.SourcePort = Nat.GetOrAdd(ipPacket).NatId;
                    udpPacket.UpdateUdpChecksum();
                    udpPacket.UpdateCalculatedValues();
                    ipPacket.DestinationAddress = DnsAddress;
                    ((IPv4Packet)ipPacket).UpdateIPChecksum();
                    ipPacket.UpdateCalculatedValues();
                }
            }

            // manage DNS incomming packet from VPN DNS
            else if (!outgoing && ipPacket.SourceAddress.Equals(DnsAddress))
            {
                var udpPacket = ipPacket.Extract<UdpPacket>();
                if (udpPacket == null) return;

                var natItem = (NatItemEx)Nat.Resolve(PacketDotNet.ProtocolType.Udp, udpPacket.DestinationPort);
                if (natItem != null)
                {
                    _logger.Log(LogLevel.Information, GeneralEventId.Dns, $"DNS reply to {VhLogger.Format(natItem.DestinationAddress)}:{natItem.DestinationPort}");
                    ipPacket.SourceAddress = natItem.DestinationAddress;
                    udpPacket.DestinationPort = natItem.SourcePort;
                    udpPacket.UpdateUdpChecksum();
                    udpPacket.UpdateCalculatedValues();
                    ((IPv4Packet)ipPacket).UpdateIPChecksum();
                    ipPacket.UpdateCalculatedValues();
                }
            }
        }

        /// <returns>true if managing is in progress</returns>
        private async Task ManageDatagramChannels(CancellationToken cancellationToken)
        {
            // check is in progress
            lock (this)
            {
                if (_isManagaingDatagramChannels)
                    return;
                _isManagaingDatagramChannels = true;
            }

            try
            {
                // make sure only one UdpChannel exists for DatagramChannels if UseUdpChannel is on
                if (UseUdpChannel)
                {
                    // check current channels
                    if (Tunnel.DatagramChannels.Length == 1 && Tunnel.DatagramChannels[0] is UdpChannel)
                        return;

                    // remove all other datagram channel
                    foreach (var channel in Tunnel.DatagramChannels)
                        Tunnel.RemoveChannel(channel);

                    // request udpChannel
                    using var tcpStream = await GetSslConnectionToServer(GeneralEventId.DatagramChannel, cancellationToken);
                    var response = await SendRequest<UdpChannelResponse>(tcpStream.Stream, RequestCode.UdpChannel,
                        new UdpChannelRequest { SessionId = SessionId, SessionKey = SessionKey },
                        cancellationToken);

                    if (response.UdpPort != 0)
                        AddUdpChannel(response.UdpPort, response.UdpKey);
                    else
                        UseUdpChannel = false;
                }

                // don't use else; UseUdpChannel may be changed if server doet not assign the channel
                if (!UseUdpChannel)
                {
                    // remove UDP datagram channels
                    foreach (var channel in Tunnel.DatagramChannels.Where(x => x is UdpChannel))
                        Tunnel.RemoveChannel(channel);

                    // make sure there is enough DatagramChannel
                    var curDatagramChannelCount = Tunnel.DatagramChannels.Length;
                    if (curDatagramChannelCount >= _minTcpDatagramChannelCount)
                        return;

                    // creating DatagramChannels
                    List<Task> tasks = new();
                    for (var i = curDatagramChannelCount; i < _minTcpDatagramChannelCount; i++)
                        tasks.Add(AddTcpDatagramChannel(cancellationToken));

                    await Task.WhenAll(tasks).ContinueWith(x =>
                    {
                        if (x.IsFaulted)
                            _logger.LogError($"Couldn't add a {VhLogger.FormatTypeName<TcpDatagramChannel>()}!", x.Exception);
                        _isManagaingDatagramChannels = false;

                        // Close session
                        if (SessionStatus.ResponseCode != ResponseCode.Ok)
                            Dispose(); //todo: reset packet!
                    });
                }
            }
            finally
            {
                _isManagaingDatagramChannels = false;
            }
        }

        private void AddUdpChannel(int udpPort, byte[] udpKey)
        {
            if (udpPort == 0) throw new ArgumentException(nameof(udpPort));
            if (udpKey == null || udpKey.Length == 0) throw new ArgumentNullException(nameof(udpKey));

            var udpEndPoint = new IPEndPoint(ServerTcpEndPoint.Address, udpPort);
            _logger.LogInformation(GeneralEventId.DatagramChannel, $"Creating {VhLogger.FormatTypeName<UdpChannel>()}... ServerEp: {udpEndPoint}");
            var udpClient = new UdpClient();
            _packetCapture.ProtectSocket(udpClient.Client);
            udpClient.Connect(udpEndPoint);
            var udpChannel = new UdpChannel(true, udpClient, SessionId, udpKey);
            Tunnel.AddChannel(udpChannel);
        }

        internal async Task<TcpClientStream> GetSslConnectionToServer(EventId eventId, CancellationToken cancellationToken)
        {
            var tcpClient = new TcpClient() { NoDelay = true };
            try
            {
                // create tcpConnection
                _packetCapture.ProtectSocket(tcpClient.Client);

                // Client.Timeout does not affect in ConnectAsync
                _logger.LogTrace(eventId, $"Connecting to Server: {VhLogger.Format(ServerTcpEndPoint)}...");
                await Util.TcpClient_ConnectAsync(tcpClient, ServerTcpEndPoint.Address, ServerTcpEndPoint.Port, Timeout, cancellationToken);

                // start TLS
                var stream = new SslStream(tcpClient.GetStream(), true, UserCertificateValidationCallback);

                // Establish a TLS connection
                _logger.LogTrace(eventId, $"TLS Authenticating. HostName: {VhLogger.FormatDns(ActiveClientProfile.DnsName)}...");
                await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = ActiveClientProfile.DnsName }, cancellationToken);

                _lastConnectionErrorTime = null;
                return new TcpClientStream(tcpClient, stream);
            }
            catch (Exception ex)
            {
                // clean up TcpClient
                tcpClient?.Dispose();
                if (State == ClientState.Connected)
                    State = ClientState.Connecting;

                // set _lastConnectionErrorTime
                if (_lastConnectionErrorTime == null)
                    _lastConnectionErrorTime = DateTime.Now;

                // dispose client after long waiting socket error
                if ((DateTime.Now - _lastConnectionErrorTime.Value).TotalMilliseconds > Timeout)
                {
                    SessionStatus.ResponseCode = ResponseCode.GeneralError;
                    SessionStatus.ErrorMessage = ex.Message;
                    Dispose();
                }

                throw;
            }
        }

        private bool UserCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None ||
                ActiveClientProfile.CertificateHash == null ||
                ActiveClientProfile.CertificateHash.SequenceEqual(certificate.GetCertHash());
        }

        private async Task ConnectInternal(CancellationToken cancellationToken)
        {
            var tcpClientStream = await GetSslConnectionToServer(GeneralEventId.Hello, cancellationToken);

            // Create the hello Message
            var request = new HelloRequest()
            {
                ClientVersion = typeof(VpnHoodClient).Assembly.GetName().Version.ToString(3),
                ClientId = ClientId,
                Token = Token,
                UseUdpChannel = UseUdpChannel,
            };

            // send the request
            var response = await SendRequest<HelloResponse>(tcpClientStream.Stream, RequestCode.Hello, request, cancellationToken);
            if (response.SessionId == 0)
                throw new Exception($"Invalid SessionId!");

            // get session id
            SessionId = response.SessionId;
            SessionKey = response.SessionKey;
            ServerId = response.ServerId;
            SessionStatus.SuppressedTo = response.SuppressedTo;

            // report Suppressed
            if (response.SuppressedTo == SuppressType.YourSelf)
                _logger.LogWarning($"You suppressed a session of yourself!");
            else if (response.SuppressedTo == SuppressType.Other)
                _logger.LogWarning($"You suppressed a session of another client!");

            // add current stream as a channel
            if (UseUdpChannel && response.UdpPort != 0)
                AddUdpChannel(response.UdpPort, response.UdpKey);

            await ManageDatagramChannels(cancellationToken);

            // done
            _logger.LogInformation(GeneralEventId.Hello, $"Hurray! Client has connected! SessionId: {VhLogger.FormatSessionId(SessionId)}");
            Connected = true;
        }

        private async Task<TcpDatagramChannel> AddTcpDatagramChannel(CancellationToken cancellationToken)
        {
            var tcpClientStream = await GetSslConnectionToServer(GeneralEventId.DatagramChannel, cancellationToken);
            return await AddTcpDatagramChannel(tcpClientStream, cancellationToken);
        }

        private async Task<TcpDatagramChannel> AddTcpDatagramChannel(TcpClientStream tcpClientStream, CancellationToken cancellationToken)
        {
            // Create the Request Message
            var request = new TcpDatagramChannelRequest()
            {
                SessionId = SessionId,
                SessionKey = SessionKey
            };

            // SendRequest
            await SendRequest<BaseResponse>(tcpClientStream.Stream, RequestCode.TcpDatagramChannel, request, cancellationToken);

            // add the new channel
            var channel = new TcpDatagramChannel(tcpClientStream);
            Tunnel.AddChannel(channel);
            return channel;
        }

        internal async Task<T> SendRequest<T>(Stream stream, RequestCode requestCode, object request, CancellationToken cancellationToken) where T : BaseResponse
        {
            // log this request
            var eventId = requestCode switch
            {
                RequestCode.Hello => GeneralEventId.Hello,
                RequestCode.TcpDatagramChannel => GeneralEventId.DatagramChannel,
                RequestCode.TcpProxyChannel => GeneralEventId.StreamChannel,
                _ => GeneralEventId.Tcp
            };
            _logger.LogTrace(eventId, $"Sending a request... RequestCode: {requestCode}");

            // building request
            using var mem = new MemoryStream();
            mem.WriteByte(1);
            mem.WriteByte((byte)requestCode);
            await StreamUtil.WriteJsonAsync(mem, request, cancellationToken);

            // send the request
            await stream.WriteAsync(mem.ToArray());

            // Reading the response
            var response = await StreamUtil.ReadJsonAsync<T>(stream, cancellationToken);
            _logger.LogTrace(eventId, $"Received a response... ResponseCode: {response.ResponseCode}");

            // set SessionStatus
            if (response.AccessUsage != null)
                SessionStatus.AccessUsage = response.AccessUsage;

            // close for any error
            switch (response.ResponseCode)
            {
                case ResponseCode.InvalidSessionId:
                case ResponseCode.SessionClosed:
                case ResponseCode.SessionSuppressedBy:
                case ResponseCode.AccessExpired:
                case ResponseCode.AccessTrafficOverflow:
                    SessionStatus.ResponseCode = response.ResponseCode;
                    SessionStatus.ErrorMessage = response.ErrorMessage;
                    SessionStatus.SuppressedBy = response.SuppressedBy;
                    throw new Exception(response.ErrorMessage);

                // Restore connected state by any ok return
                case ResponseCode.Ok:
                    if (!_disposed)
                        State = ClientState.Connected;
                    return response;

                default:
                    return response;
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_disposed) return;
                _disposed = true;
            }

            if (State == ClientState.None) return;

            _logger.LogInformation("Disconnecting...");
            if (State == ClientState.Connecting || State == ClientState.Connected)
                State = ClientState.Disconnecting;

            _cancellationTokenSource.Cancel();
            _packetCapture.OnPacketArrivalFromInbound -= PacketCapture_OnPacketArrivalFromInbound;

            // log suppressedBy
            if (SessionStatus.SuppressedBy == SuppressType.YourSelf) _logger.LogWarning($"You suppressed by a session of yourself!");
            else if (SessionStatus.SuppressedBy == SuppressType.Other) _logger.LogWarning($"You suppressed a session of another client!");

            _logger.LogTrace($"Disposing {VhLogger.FormatTypeName<TcpProxyHost>()}...");
            _tcpProxyHost?.Dispose();

            _logger.LogTrace($"Disposing {VhLogger.FormatTypeName<Tunnel>()}...");
            Tunnel?.Dispose();

            // shutdown
            _logger.LogInformation("Shutting down...");

            // dispose NAT
            _logger.LogTrace($"Disposing {VhLogger.FormatTypeName(Nat)}...");
            Nat.Dispose();

            // close PacketCapture
            if (!_leavePacketCaptureOpen)
            {
                _logger.LogTrace($"Disposing Captured Device...");
                _packetCapture.Dispose();
            }

            State = ClientState.Disposed;
            _logger.LogInformation("Bye Bye!");
        }
    }
}
