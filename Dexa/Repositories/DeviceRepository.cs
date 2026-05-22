using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Else.PhoneMirror.ViewModels;

namespace Else.PhoneMirror.Repositories
{
    public static class DeviceRepository
    {
        private static readonly string DevicesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Dexa",
            "devices.json");

        // K2: single lock for all access to _devices
        private static readonly object _lock = new object();
        private static List<Device> _devices;

        static DeviceRepository()
        {
            _devices = LoadFromFile();
        }

        public static List<Device> GetDevices()
        {
            lock (_lock)
                return _devices.ToList();
        }

        public static void Add(Device device)
        {
            lock (_lock)
            {
                if (!_devices.Any(d => d.Name == device.Name))
                    _devices.Add(device);
            }
            SaveToFile();
        }

        public static void Remove(string deviceName)
        {
            lock (_lock)
            {
                var device = _devices.FirstOrDefault(d => d.Name == deviceName);
                if (device != null)
                    _devices.Remove(device);
            }
            SaveToFile();
        }

        public static void Update(Device device)
        {
            lock (_lock)
            {
                var existing = _devices.FirstOrDefault(d => d.Name == device.Name);
                if (existing != null)
                {
                    existing.HardwareName = device.HardwareName;
                    existing.IsEmulator = device.IsEmulator;
                    existing.IsAvailable = device.IsAvailable;
                    existing.IsNetworkVisible = device.IsNetworkVisible;
                    existing.IsRemoteConnection = device.IsRemoteConnection;
                    existing.CanBeRemoteConnected = device.CanBeRemoteConnected;
                    existing.IpAddress = device.IpAddress;
                    existing.FriendlyName = device.FriendlyName;
                }
                else
                {
                    _devices.Add(device);
                }
            }
            SaveToFile();
        }

        public static void UpdateRunData(Device? device)
        {
            if (device == null)
                return;

            lock (_lock)
            {
                var existing = _devices.FirstOrDefault(d => d.Name == device.Name);
                if (existing != null)
                {
                    existing.IsRunning = device.IsRunning;
                    existing.DeviceWindow = device.DeviceWindow;
                    existing.DeviceMedia = device.DeviceMedia;
                    existing.LastUsage = device.LastUsage;
                }
            }
            SaveToFile();
        }

        public static void SaveToFile()
        {
            try
            {
                List<Device> snapshot;
                lock (_lock)
                    snapshot = _devices.ToList();

                Directory.CreateDirectory(Path.GetDirectoryName(DevicesFilePath)!);
                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DevicesFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd podczas zapisywania urządzeń: {ex.Message}");
            }
        }

        private static List<Device> LoadFromFile()
        {
            try
            {
                if (File.Exists(DevicesFilePath))
                {
                    string json = File.ReadAllText(DevicesFilePath);
                    return JsonSerializer.Deserialize<List<Device>>(json) ?? new List<Device>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd podczas wczytywania urządzeń: {ex.Message}");
            }

            return new List<Device>();
        }
    }
}
