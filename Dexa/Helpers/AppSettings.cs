using System;
using System.IO;
using Newtonsoft.Json;

namespace Dexa.Helpers
{
    public class AppSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "Dexa", 
            "settings.json");

        public bool IsKeyboardEnabled { get; set; } = true;
        public bool IsScreenOffEnabled { get; set; } = true;
        public bool IsAspectRatioUnlocked { get; set; } = false;
        public bool IsAudioEnabled { get; set; } = true;

        private static AppSettings? _cached;
        private static DateTime _cachedWriteTime = DateTime.MinValue;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var writeTime = File.GetLastWriteTimeUtc(SettingsFilePath);
                    if (_cached != null && writeTime == _cachedWriteTime)
                        return _cached;

                    var json = File.ReadAllText(SettingsFilePath);
                    _cached = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                    _cachedWriteTime = writeTime;
                    return _cached;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return _cached ?? new AppSettings();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                // Log error or handle as needed
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}