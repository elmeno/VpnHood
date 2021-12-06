using VpnHood.Client.App;
using VpnHood.Client.Device;
using VpnHood.Client.Device.WinDivert;

namespace YetiVPN.Service
{
    class WinAppProvider : IAppProvider
    {
        public IDevice Device { get; } = new WinDivertDevice();
    }
}