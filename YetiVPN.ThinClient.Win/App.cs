using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Forms;
using VpnHood.Client.App;
using VpnHood.Logging;
using YetiVPN.Client.App.UI;
using YetiVPN.ThinClient.Win;

namespace YetiVPN.ThinClient.Win
{

    public class VpnHoodAppStub : IDisposable
    {
        public bool IsIdle => ConnectionState == AppConnectionState.None;
        private AppConnectionState _ConnectionState = AppConnectionState.None;
        public AppState State { get; set; }
        public AppSettings Settings { get; set; }
        private AppConnectionState ConnectionState
        {
            get
            {
                return _ConnectionState;
            }
            set
            {
                _ConnectionState = value;
            }
        }
        public VpnHoodAppStub()
        {

        }
        public void Connect(bool byUser = false)
        {

        }
        public void Disconnect(bool byUser = false)
        {

        }
        public void Dispose()
        {
            // throw new NotImplementedException();
        }
    }

    public class VpnHoodAppUIStub : IDisposable
    {
        public string Url { get; private set; }

        public VpnHoodAppUIStub()
        {
            Url = $"http://127.0.0.1:6714";
        }

        public void Dispose()
        {
            // throw new NotImplementedException();
        }
    }


    internal class App : ApplicationContext
    {
        private bool _disposed = false;
        private readonly Mutex _mutex = new(false, typeof(Program).FullName);
        private NotifyIcon _notifyIcon;
        // private VpnHoodApp _app;
        private VpnHoodAppStub _app;
        // private VpnHoodAppUI _appUI;
        private VpnHoodAppUIStub _appUI;
        private WebViewWindow _webViewWindow;
        private FileSystemWatcher _fileSystemWatcher;
        private System.Windows.Forms.Timer _uiTimer;
        private DateTime? _updater_lastCheckTime;
        string appDataPath;
        public int CheckIntervalMinutes { get; set; } = 1 * (6 * 60); // 6 hours
                                                                      // private enum ServiceCustomCommands { StopWorker = 128, RestartWorker, CheckWorker }; // between 128 and 255
        private LoadAppPayload payload = new LoadAppPayload();
        private LoadAppResult serviceStatus;


        public App()
        {
            payload.withState = true;
            payload.withSettings = true;
            payload.withClientProfileItems = true;
            payload.withFeatures = false;

            var apiCheckTimer = new System.Timers.Timer();
            apiCheckTimer.Interval = 200;
            apiCheckTimer.Elapsed += LoadAppReq;
            apiCheckTimer.AutoReset = true;
            apiCheckTimer.Enabled = true;
        }

        public void Start(string[] args)
        {
            var openWindow = !args.Any(x => x.Equals("/nowindow", StringComparison.OrdinalIgnoreCase));
            var autoConnect = args.Any(x => x.Equals("/autoconnect", StringComparison.OrdinalIgnoreCase));
            var logToConsole = true;

            VhLogger.Instance = VhLogger.CreateConsoleLogger();

            // Report current Version
            // Replace dot in version to prevent anonymouizer treat it as ip.
            VhLogger.Instance.LogInformation($"{typeof(App).Assembly.ToString().Replace('.', ',')}");
            appDataPath = new AppOptions().AppDataPath; // we use defaultPath
            var appCommandFilePath = Path.Combine(appDataPath, "appCommand.txt");

            // Make single instance
            // if you like to wait a few seconds in case that the instance is just shutting down
            if (!_mutex.WaitOne(TimeSpan.FromSeconds(0), false))
            {
                // open main window if app is already running and user run the app again
                if (openWindow)
                    File.WriteAllText(appCommandFilePath, "OpenMainWindow");
                VhLogger.Instance.LogInformation($"{nameof(App)} is already running!");
                return;
            }


            // init app
            //   _app = VpnHoodApp.Init(new WinAppProvider(), new AppOptions() { LogToConsole = logToConsole });
            _app = new VpnHoodAppStub();
            // _appUI =  VpnHoodAppUI.Init(new MemoryStream(Resource.SPA));
            _appUI = new VpnHoodAppUIStub();

            // auto connect
            // if (autoConnect && _app.UserSettings.DefaultClientProfileId != null &&
            //     _app.ClientProfileStore.ClientProfileItems.Any(x => x.ClientProfile.ClientProfileId == _app.UserSettings.DefaultClientProfileId))
            //      _app.Connect(_app.UserSettings.DefaultClientProfileId.Value).GetAwaiter();

            // create notification icon
            InitNotifyIcon();

            // Create webview if installed
            if (WebViewWindow.IsInstalled)
                _webViewWindow = new WebViewWindow(_appUI.Url, Path.Combine(appDataPath, "Temp"));

            CheckForUpdate();

            // MainWindow
            if (openWindow)
                OpenMainWindow();

            // Init command watcher for external command
            InitCommnadWatcher(appCommandFilePath);

            //Ui Timer
            InitUiTimer();



            // Message Loop
            Application.Run(this);
        }

        private void InitUiTimer()
        {
            _uiTimer = new System.Windows.Forms.Timer
            {
                Interval = 100,
                Enabled = true
            };
            _uiTimer.Tick += (sender, e) => UpdateNotifyIconText();
            _uiTimer.Start();
        }

        private void UpdateNotifyIconText()
        {
            if (_app.State == null)
            {
                _notifyIcon.Text = $"{AppUIResource.AppName}";
                return;
            }
            var stateName = _app.State.ConnectionState == AppConnectionState.None ? "Disconnected" : _app.State.ConnectionState.ToString();
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = $"{AppUIResource.AppName} - {stateName}";
                if (_app.State.IsIdle || _app.State.ConnectionState == AppConnectionState.None) _notifyIcon.Icon = Resource.VpnDisconnectedIcon;
                else if (_app.State.ConnectionState == AppConnectionState.Connected) _notifyIcon.Icon = Resource.VpnConnectedIcon;
                else _notifyIcon.Icon = Resource.VpnConnectingIcon;


            }
        }

        private void CheckForUpdate()
        {
            // read last check
            var lastCheckFilePath = Path.Combine(appDataPath, "lastCheckUpdate");
            if (_updater_lastCheckTime == null)
            {
                _updater_lastCheckTime = DateTime.MinValue;
                if (File.Exists(lastCheckFilePath))
                    try { _updater_lastCheckTime = JsonSerializer.Deserialize<DateTime>(File.ReadAllText(lastCheckFilePath)); } catch { }
            }

            // check last update time
            if ((DateTime.Now - _updater_lastCheckTime).Value.TotalMinutes < CheckIntervalMinutes)
                return;

            // set checktime before chking filename
            _updater_lastCheckTime = DateTime.Now;

            // launch updater if exists
            var updaterFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "updater.exe");
            if (!File.Exists(updaterFilePath))
            {
                VhLogger.Instance.LogWarning($"Could not find updater: {updaterFilePath}");
                return;
            }

            try
            {
                VhLogger.Instance.LogInformation("Cheking for new updates...");
                Process.Start(updaterFilePath, "/silent");
            }
            catch (Exception ex)
            {
                VhLogger.Instance.LogError(ex.Message);
            }
            finally
            {
                File.WriteAllText(lastCheckFilePath, JsonSerializer.Serialize(_updater_lastCheckTime));
            }
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
                    if (cmd == "OpenMainWindow")
                        OpenMainWindow();
                }
                catch { }
            };
        }

        public void OpenMainWindow()
        {
            if (_webViewWindow != null)
            {
                _webViewWindow.Show();
                return;
            }

            Process.Start(new ProcessStartInfo()
            {
                FileName = _appUI.Url,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        private void InitNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = Resource.YetiVPNIcon
            };
            _notifyIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    OpenMainWindow();
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add(AppUIResource.Open, null, (sender, e) => OpenMainWindow());

            //menu.Items.Add("-");
            //var menuItem = menu.Items.Add("Connect");
            //menuItem.Name = "connect";
            //menuItem.Click += ConnectMenuItem_Click;

            //menuItem = menu.Items.Add(AppUIResource.Disconnect);
            //menuItem.Name = "disconnect";
            //menuItem.Click += (sender, e) => _app.Disconnect(true);

            menu.Items.Add("-");
            var menurestartItem = menu.Items.Add("Restart Service");
            menurestartItem.Name = "restart";
            menurestartItem.Click += OnRestartService_Click;

            menu.Items.Add("-");
            menu.Items.Add(AppUIResource.Exit, null, (sender, e) => Application.Exit());
            menu.Opening += Menu_Opening;
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.Text = AppUIResource.AppName;
            _notifyIcon.Visible = true;
            UpdateNotifyIconText();
        }

        private void ConnectMenuItem_Click(object sender, EventArgs e)
        {
            if (_app.Settings.UserSettings.DefaultClientProfileId != null)
            {
                try
                {
                    // _app.Connect(_app.Settings.UserSettings.DefaultClientProfileId.Value).GetAwaiter();
                }
                catch
                {
                    OpenMainWindow();
                }
            }
            else
            {
                OpenMainWindow();
            }
        }

        private void OnRestartService_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.UseShellExecute = true;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C net stop \"YETI VPN Service\" & net start \"YETI VPN Service\"";
            startInfo.Verb = "runas";
            process.StartInfo = startInfo;
            process.Start();

        }

        private void Menu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var menu = (ContextMenuStrip)sender;
            //menu.Items["connect"].Enabled = _app.IsIdle;
            //menu.Items["disconnect"].Enabled = !_app.IsIdle && _app.State.ConnectionState != AppConnectionState.Disconnecting;
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
        private static Process ProcessStartNoWindow(string filename, string argument)
        {
            var processStart = new ProcessStartInfo(filename, argument)
            {
                CreateNoWindow = true
            };

            return Process.Start(processStart);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _notifyIcon?.Dispose();
                _appUI?.Dispose();
                _app?.Dispose();
                _fileSystemWatcher?.Dispose();
            }
            _disposed = true;

            // base
            CheckForUpdate();
            base.Dispose(disposing);
        }

        public class LoadAppPayload
        {
            public bool withState { get; set; }
            public bool withSettings { get; set; }
            public bool withClientProfileItems { get; set; }
            public bool withFeatures { get; set; }
            public bool withOpenUrl { get; set; }
        }

        public class LoadAppResult
        {
            [JsonPropertyName("features")]
            public AppFeatures Features { get; set; }
            [JsonPropertyName("settings")]
            public AppSettings Settings { get; set; }
            [JsonPropertyName("state")]
            public AppState State { get; set; }
            [JsonPropertyName("clientProfileItems")]
            public ClientProfileItem[] ClientProfileItems { get; set; }
        }

        public async void LoadAppReq(Object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                var jsonPayload = JsonSerializer.Serialize(payload);
                var jsonReq = new System.Net.Http.StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var url = "http://127.0.0.1:6714/api/loadApp";
                using var client = new HttpClient();

                var response = await client.PostAsync(url, jsonReq);
                var result = response.Content.ReadAsStringAsync().Result;
                var parsedResp = JsonSerializer.Deserialize<LoadAppResult>(result);

                if (serviceStatus?.State != null && parsedResp?.State?.ConnectionState != serviceStatus?.State?.ConnectionState)
                {
                    VhLogger.Instance.LogInformation($"LoadAppReq connectionState: {parsedResp.State.ConnectionState}");
                }

                if (serviceStatus?.State != null && parsedResp?.State?.OpenUrl != null && serviceStatus?.State?.OpenUrl != parsedResp?.State?.OpenUrl)
                {
                    ResetOpenUrl();
                    OpenUrl(parsedResp.State.OpenUrl);
                }

                serviceStatus = parsedResp;
                _app.State = serviceStatus.State;
                _app.Settings = serviceStatus.Settings;

            }
            catch (Exception ex)
            {
                VhLogger.Instance.LogInformation($"LoadAppReq, Error: {ex.Message}");
                if (_app.State != null)
                {
                    _app.State.ConnectionState = AppConnectionState.None;
                }
                return;
            }
        }

        public async void ResetOpenUrl()
        {
            try
            {
                VhLogger.Instance.LogInformation($"Send ResetOpenUrl Request");
                var url = "http://127.0.0.1:6714/api/resetOpenUrl";
                using var client = new HttpClient();

                var jsonPayload = JsonSerializer.Serialize(payload);
                var jsonReq = new System.Net.Http.StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, jsonReq);
                var result = response.Content.ReadAsStringAsync().Result;
                return;
            }
            catch (Exception ex)
            {
                VhLogger.Instance.LogInformation($"ResetOpenUrl, Error: {ex.Message}");
                return;
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
