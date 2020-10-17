﻿using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.OS;
using Android.Net;
using Android.Content;
using Android.Widget;
using Android.Views;
using VpnHood.Client.App.UI;
using Android.Webkit;

namespace VpnHood.Client.App.Android
{
    [Activity(Label = "VpnHoodApp",
        Icon = "@mipmap/ic_launcher",
        Theme = "@android:style/Theme.DeviceDefault.NoActionBar",
        MainLauncher = true, AlwaysRetainTaskState = true, LaunchMode = LaunchMode.SingleInstance,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.LayoutDirection | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.FontScale | ConfigChanges.Locale | ConfigChanges.Navigation | ConfigChanges.UiMode)]
    public class MainActivity : Activity
    {
        class MyWebViewClient : WebViewClient
        {
            private readonly MainActivity _mainActivity;
            public MyWebViewClient(MainActivity mainActivity) { _mainActivity = mainActivity;}
            public override bool ShouldOverrideUrlLoading(WebView webView, IWebResourceRequest request)
            {
                return ShouldOverrideUrlLoading(webView, request);
            }

            public override void OnPageFinished(WebView view, string url)
            {
                base.OnPageFinished(view, url);
                _mainActivity.SetContentView(_mainActivity._webView);
            }

            public override void OnPageCommitVisible(WebView view, string url) => base.OnPageCommitVisible(view, url);
        }

        private WebView _webView;
        private VpnHoodAppUI _appUi;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // initialize web view
            InitSplashScreen();

            _appUi = new VpnHoodAppUI();
            InitWebUI();
        }

        protected override void OnDestroy()
        {
            _appUi.Dispose();
            _appUi = null;
            base.OnDestroy();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private void InitSplashScreen()
        {
            var imageView = new ImageView(this);
            imageView.SetImageResource(Resource.Mipmap.ic_launcher);
            imageView.LayoutParameters = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            imageView.SetScaleType(ImageView.ScaleType.CenterInside);
            //imageView.SetBackgroundColor(Android.Graphics.Color.Blue);
            SetContentView(imageView);
        }

        private void InitWebUI()
        {
            if (!_appUi.Started)
                _appUi.Start().GetAwaiter();

            _webView = new WebView(this);
            _webView.SetWebViewClient(new MyWebViewClient(this));
            _webView.Settings.JavaScriptEnabled = true;
            _webView.Settings.DomStorageEnabled = true;
            _webView.Settings.SetSupportMultipleWindows(true);
            _webView.SetLayerType(LayerType.Hardware, null);
#if DEBUG
            WebView.SetWebContentsDebuggingEnabled(true);
#endif
            _webView.LoadUrl($"{_appUi.Url}?nocache={_appUi.SpaHash}");
        }

        

        public override void OnBackPressed()
        {
            if (_webView.CanGoBack())
                _webView.GoBack();
            else
                base.OnBackPressed();
        }
    }
}