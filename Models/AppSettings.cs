using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Network_Pinger.Models
{
    // Handles loading and saving application state
    public class AppSettings
    {
        // UI / connection defaults
        public bool IsDarkMode { get; set; }
        public string TargetAddress { get; set; } = "8.8.8.8";
        public string Interval { get; set; } = "1";
        public string PingCount { get; set; } = "4";

        // Threshold defaults (ms)
        public string NormalThreshold { get; set; } = "100";
        public string WarningThreshold { get; set; } = "200";
        public string TimeOutThreshold { get; set; } = "500";

        // Ping option defaults
        public string BufferSize { get; set; } = "32";
        public string Timeout { get; set; } = "4000";
        public bool DontFragment { get; set; }
        public bool PlaySound { get; set; }

        // Color defaults
        public string SuccessColor { get; set; } = "#28A745";
        public string WarningColor { get; set; } = "#FFC107";
        public string ErrorColor { get; set; } = "#DC3545";

        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Network Pinger");
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        // Load from disk; return defaults on missing file or error
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                    return Serializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFilePath)) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Load error: {ex.Message}");
            }
            return new AppSettings();
        }

        // Persist current settings to disk
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                File.WriteAllText(SettingsFilePath, Serializer.Serialize(this));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Save error: {ex.Message}");
            }
        }
    }
}