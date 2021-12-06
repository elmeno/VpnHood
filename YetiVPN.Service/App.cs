using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using VpnHood.Client.App.UI;
using System.IO;
using VpnHood.Client.App;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace YetiVPN.Service
{
    class App : BackgroundService
    {
        private bool _disposed = false;
        private readonly Mutex _mutex = new(false, typeof(Program).FullName);

        private VpnHoodApp _app;
        private VpnHoodAppUI _appUI;
        private FileSystemWatcher _fileSystemWatcher;

        private readonly ILogger<App> _logger;

        public App(ILogger<App> logger)
        {
            _logger = logger;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Report current Version
            // Replace dot in version to prevent anonymouizer treat it as ip.
            _logger.LogInformation("Service started");
            _logger.LogInformation($"{typeof(App).Assembly.ToString().Replace('.', ',')}");
            var appDataPath = new AppOptions().AppDataPath; // we use defaultPath
            var appCommandFilePath = Path.Combine(appDataPath, "appCommand.txt");
            _logger.LogInformation($"appDataPath {appDataPath}");

            // Make single instance
            // if you like to wait a few seconds in case that the instance is just shutting down
            if (!_mutex.WaitOne(TimeSpan.FromSeconds(0), false))
            {
                // open main window if app is already running and user run the app again
                _logger.LogInformation($"{nameof(App)} is already running!");
                //return;
            }

            // configuring Windows Firewall
            try
            {
                OpenLocalFirewall(appDataPath);
            }
            catch { };

            // init app
            _app = VpnHoodApp.Init(new WinAppProvider(), new AppOptions() { 
                //LogToConsole = logToConsole 
            });
            _appUI = VpnHoodAppUI.Init(new MemoryStream(Resource.SPA));

            // auto connect

            // Init command watcher for external command
            InitCommnadWatcher(appCommandFilePath);

            var tcs = new TaskCompletionSource<bool>();
            stoppingToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            await tcs.Task;
        }


        private void InitCommnadWatcher(string path)
        {
            _fileSystemWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(path),
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = Path.GetFileName(path),
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _fileSystemWatcher.Changed += (sender, e) =>
            {
                try
                {
                    Thread.Sleep(100);
                    var cmd = File.ReadAllText(e.FullPath);
                    if (cmd == "OpenMainWindow") {
                        // OpenMainWindow();
                    }
                }
                catch { }
            };
        }

        private static string FindExePath(string exe)
        {
            exe = Environment.ExpandEnvironmentVariables(exe);
            if (File.Exists(exe))
                return Path.GetFullPath(exe);

            if (Path.GetDirectoryName(exe) == string.Empty)
            {
                foreach (string test in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                {
                    string path = test.Trim();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path = Path.Combine(path, exe)))
                        return Path.GetFullPath(path);
                }
            }
            throw new FileNotFoundException(new FileNotFoundException().Message, exe);
        }

        private void OpenLocalFirewall(string appDataPath)
        {
            var lastFirewallConfig = Path.Combine(appDataPath, "lastFirewallConfig");
            var lastExeFile = File.Exists(lastFirewallConfig) ? File.ReadAllText(lastFirewallConfig) : null;
            var curExePath = Path.ChangeExtension(typeof(App).Assembly.Location, "exe");
            if (lastExeFile == curExePath)
                return;

            _logger.LogInformation($"Configuring Windows Defender Firewall...");
            var ruleName = "YETI VPN";

            //dotnet exe
            var exePath = FindExePath("dotnet.exe");
            ProcessStartNoWindow("netsh", $"advfirewall firewall delete rule name=\"{ruleName}\" dir=in").WaitForExit();
            ProcessStartNoWindow("netsh", $"advfirewall firewall add rule  name=\"{ruleName}\" program=\"{exePath}\" protocol=TCP localport=any action=allow profile=private dir=in").WaitForExit();
            ProcessStartNoWindow("netsh", $"advfirewall firewall add rule  name=\"{ruleName}\" program=\"{exePath}\" protocol=UDP localport=any action=allow profile=private dir=in").WaitForExit();

            // vpnhood exe
            exePath = curExePath;
            if (File.Exists(exePath))
            {
                ProcessStartNoWindow("netsh", $"advfirewall firewall add rule  name=\"{ruleName}\" program=\"{exePath}\" protocol=TCP localport=any action=allow profile=private dir=in").WaitForExit();
                ProcessStartNoWindow("netsh", $"advfirewall firewall add rule  name=\"{ruleName}\" program=\"{exePath}\" protocol=UDP localport=any action=allow profile=private dir=in").WaitForExit();
            }

            // save firewall modified
            File.WriteAllText(lastFirewallConfig, curExePath);
        }

        private static Process ProcessStartNoWindow(string filename, string argument)
        {
            var processStart = new ProcessStartInfo(filename, argument)
            {
                CreateNoWindow = true
            };

            return Process.Start(processStart);
        }
    }
}
