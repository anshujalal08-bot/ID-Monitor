using System;
using System.IO;
using System.Text.Json;

namespace AutodeskIDMonitor
{
    /// <summary>
    /// Application settings manager - replaces Properties.Settings
    /// </summary>
    public sealed class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutodeskIDMonitor",
            "settings.json"
        );

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static AppSettings? _instance;
        private static readonly object _lock = new();

        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= Load();
                    }
                }
                return _instance;
            }
        }

        // Settings properties
        public string ServerUrl { get; set; } = "http://141.145.153.32:5000";
        public bool IsFirstRun { get; set; } = true;
        public string AuthToken { get; set; } = string.Empty;
        public string SavedUsername { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = false;
        public int HeartbeatIntervalSeconds { get; set; } = 30;
        public int StatusSyncIntervalSeconds { get; set; } = 60;
        public int IdleThresholdMinutes { get; set; } = 5;

        // User info (populated after login)
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;

        public static AppSettings Load()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        _instance = settings;
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            _instance = new AppSettings();
            return _instance;
        }

        public static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_instance ?? new AppSettings(), JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public static void Reset()
        {
            _instance = new AppSettings();
            Save();
        }
    }
}
