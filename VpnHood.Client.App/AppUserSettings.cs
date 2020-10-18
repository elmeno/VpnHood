﻿namespace VpnHood.Client.App
{
    public class AppUserSettings
    {
        public bool LogToFile { get; set; } = false;
        public bool LogVerbose { get; set; } = true;
        public bool DarkMode { get; set; }
        public string CultureName { get; set; } = "en";
    }
}