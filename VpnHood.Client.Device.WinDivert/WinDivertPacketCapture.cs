﻿using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap.WinDivert;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using VpnHood.Logging;

namespace VpnHood.Client.Device.WinDivert
{
    public class WinDivertPacketCapture : IPacketCapture
    {
        private IPNetwork[] _excludeNetworks;
        private IPNetwork[] _includeNetworks;
        private WinDivertHeader _lastCaptureHeader;

        protected readonly SharpPcap.WinDivert.WinDivertDevice _device;
        public event EventHandler<PacketCaptureArrivalEventArgs> OnPacketArrivalFromInbound;
        public event EventHandler OnStopped;

        public bool Started => _device.Started;

        private static void SetWinDivertDllFolder()
        {
            var dllFolderName = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            var assemblyFolder = Path.GetDirectoryName(typeof(WinDivertDevice).Assembly.Location);
            var dllFolder = Path.Combine(assemblyFolder, dllFolderName);

            // extract WinDivert
            // I got sick trying to add it to nuget ad anative library in (x86/x64) folder, OOF!
            if (!File.Exists(Path.Combine(dllFolder, "WinDivert.dll")))
            {
                using var memStream = new MemoryStream(Resource.WinDivertLibZip);
                var tempLibFolder = Path.Combine(Path.GetTempPath(), "VpnHood-WinDivertDevice");
                dllFolder = Path.Combine(tempLibFolder, dllFolderName);
                // extract if file does not exists
                if (!File.Exists(Path.Combine(dllFolder, "WinDivert.dll")))
                {
                    using var zipArchive = new ZipArchive(memStream);
                    zipArchive.ExtractToDirectory(tempLibFolder, true);
                }
            }

            // set dll folder
            var path = Environment.GetEnvironmentVariable("PATH");
            if (path.IndexOf(dllFolder + ";") == -1)
                Environment.SetEnvironmentVariable("PATH", dllFolder + ";" + path);
        }

        private readonly EventWaitHandle _newPacketEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        public WinDivertPacketCapture()
        {
            SetWinDivertDllFolder();

            // initialize devices
            _device = new SharpPcap.WinDivert.WinDivertDevice { Flags = 0 };
            _device.OnPacketArrival += Device_OnPacketArrival;
        }

        private void Device_OnPacketArrival(object sender, SharpPcap.PacketCapture e)
        {
            var rawPacket = e.GetPacket();
            var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var ipPacket = packet.Extract<IPPacket>();
            _lastCaptureHeader = (WinDivertHeader)e.Header;
            ProcessPacket(ipPacket);
        }

        protected virtual void ProcessPacket(IPPacket ipPacket)
        {
            try
            {
                OnPacketArrivalFromInbound?.Invoke(this, new PacketCaptureArrivalEventArgs(new[] { ipPacket }, this));
            }
            catch (Exception ex)
            {
                VhLogger.Instance.Log(LogLevel.Error, $"Error in processing packet {ipPacket}! Error: {ex}");
            }
        }

        public void Dispose()
        {
            StopCapture();
            _device.Dispose();
        }

        public void SendPacketToInbound(IPPacket[] ipPackets)
        {
            foreach (var ipPacket in ipPackets)
                SendPacket(ipPacket, false);
        }

        protected void SendPacket(IPPacket ipPacket, bool outbound)
        {
            // send by a device
            _lastCaptureHeader.Flags = outbound ? WinDivertPacketFlags.Outbound : 0;
            _device.SendPacket(ipPacket.Bytes, _lastCaptureHeader);
        }

        public IPAddress[] RouteAddresses { get; set; }

        public bool IsExcludeNetworksSupported => true;
        public bool IsIncludeNetworksSupported => true;

        public IPNetwork[] ExcludeNetworks
        {
            get => _excludeNetworks;
            set
            {
                if (Started)
                    throw new InvalidOperationException($"Can't set {nameof(ExcludeNetworks)} when {nameof(WinDivertPacketCapture)} is started!");
                _excludeNetworks = value;
            }
        }

        public IPNetwork[] IncludeNetworks
        {
            get => _includeNetworks;
            set
            {
                if (Started) throw new InvalidOperationException($"Can't set {nameof(IncludeNetworks)} when {nameof(WinDivertPacketCapture)} is started!");
                _includeNetworks = value;
            }
        }

        #region Applications Filter
        public bool IsExcludeApplicationsSupported => false;
        public bool IsIncludeApplicationsSupported => false;
        public string[] ExcludeApplications { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public string[] IncludeApplications { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public bool IsMtuSupported => false;
        public int Mtu { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        #endregion

        public void StartCapture()
        {
            if (Started)
                throw new InvalidOperationException("Device has been already started!");

            // add outbound; filter loopback
            var filter = "ip and outbound and !loopback";

            if (IncludeNetworks != null && IncludeNetworks.Length > 0)
            {
                var phrases = IncludeNetworks.Select(x => $"(ip.DstAddr>={x.FirstAddress} and ip.DstAddr<={x.LastAddress})").ToArray();
                var phrase = string.Join(" or ", phrases);
                filter += $" and (udp.DstPort==53 or ({phrase}))";
            }
            if (ExcludeNetworks != null && ExcludeNetworks.Length > 0)
            {
                var phrases = ExcludeNetworks.Select(x => $"(ip.DstAddr<{x.FirstAddress} or ip.DstAddr>{x.LastAddress})");
                var phrase = string.Join(" and ", phrases);
                filter += $" and (udp.DstPort==53 or ({phrase}))";
            }

            try
            {
                _device.Filter = filter;
                _device.Open(new SharpPcap.DeviceConfiguration());
                _device.StartCapture();
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new Exception("Access denied! Could not open WinDivert driver! Make sure the app is running with admin privilege.", ex);
                throw;
            }

        }

        public void StopCapture()
        {
            if (!Started)
                return;

            _device.StopCapture();
            OnStopped?.Invoke(this, EventArgs.Empty);
        }

        public void ProtectSocket(System.Net.Sockets.Socket socket)
        {
        }
    }
}
