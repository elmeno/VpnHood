using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VpnHood.Common;
using VpnHood.Logging;

namespace VpnHood.Client.App
{
  public class AppSettings
  {
        [JsonPropertyName("settingsFilePath")]
        public string SettingsFilePath { get; private set; }
        [JsonPropertyName("userSettings")]
        public AppUserSettings UserSettings { get; set; } = new AppUserSettings();
        [JsonPropertyName("clientId")]
        public Guid ClientId { get; set; } = Guid.NewGuid();
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";
    public event EventHandler OnSaved;

    public void Save()
    {
      var json = JsonSerializer.Serialize(this);
      File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
      OnSaved?.Invoke(this, EventArgs.Empty);
    }

    public AppSettings Load(string settingsFilePath)
    {
      try
      {
        var json = File.ReadAllText(settingsFilePath, Encoding.UTF8);
        var ret = JsonSerializer.Deserialize<AppSettings>(json);
        ret.SettingsFilePath = settingsFilePath;
        return ret;
      }
      catch
      {
        var ret = new AppSettings
        {
          SettingsFilePath = settingsFilePath
        };
        ret.Save();
        AppInitRequest(ret);
        return ret;
      }
    }

    public class ClientIdPayload
    {
      public Guid ClientId { get; set; }
      public string Action { get; set; }
    }
    
    public async void AppInitRequest(AppSettings appSettings)
    {
      VhLogger.Instance.LogInformation($"Perform AppInitRequest");
      try
      {
        var payload = new ClientIdPayload();
        payload.ClientId = appSettings.ClientId;
        payload.Action = "install";

        var json = JsonSerializer.Serialize(payload);
        var data = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");

        var url = "https://yetivp.com/evstatus";
        using var client = new HttpClient();

        var response = await client.PostAsync(url, data);

        string result = response.Content.ReadAsStringAsync().Result;
        VhLogger.Instance.LogInformation($"AppInitRequest Response: {result}");
      }
      catch (Exception ex)
      {
        VhLogger.Instance.LogInformation($"AppInitRequest, Error: {ex.Message}");
        return;
      }
    }
  }
}
