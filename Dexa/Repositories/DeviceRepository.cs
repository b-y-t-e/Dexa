using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Else.PhoneMirror.ViewModels;

namespace Else.PhoneMirror.Repositories
{
    public static class DeviceRepository
    {
        private static readonly string DevicesFilePath = "devices.json";
        private static List<Device> _devices;

        static DeviceRepository()
        {
            _devices = LoadFromFile();
        }

        public static List<Device> GetDevices()
        {
            if (_devices == null)
                _devices = LoadFromFile();

            return _devices;
        }

        public static void Add(Device device)
        {
            if (!_devices.Any(d => d.Name == device.Name))
            {
                _devices.Add(device);
                SaveToFile();
            }
        }

        public static void Remove(string deviceName)
        {
            var device = _devices.FirstOrDefault(d => d.Name == deviceName);
            if (device != null)
            {
                _devices.Remove(device);
                SaveToFile();
            }
        }

        public static void UpdateHardwareData(Device device)
        {
            var existingDevice = _devices.FirstOrDefault(d => d.Name == device.Name);
            if (existingDevice != null)
            {
                existingDevice.IsEmulator = device.IsEmulator;
                existingDevice.IsNetworkVisible = device.IsNetworkVisible;
                existingDevice.IsRemoteConnection = device.IsRemoteConnection;
                existingDevice.CanBeRemoteConnected = device.CanBeRemoteConnected;
                existingDevice.IpAddress = device.IpAddress;
            }
            else
            {
                _devices.Add(device);
            }

            SaveToFile();
        }

        public static void UpdateRunData(Device? device)
        {
            if (device == null)
                return;

            var existingDevice = _devices.FirstOrDefault(d => d.Name == device.Name);
            if (existingDevice != null)
            {
                existingDevice.IsRunning = device.IsRunning;
                existingDevice.DeviceWindow = device.DeviceWindow;
                existingDevice.DeviceMedia = device.DeviceMedia;
            }

            SaveToFile();
        }

        public static void SaveToFile()
        {
            try
            {
                lock (_devices)
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(_devices,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true
                        });
                    File.WriteAllText(DevicesFilePath, json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas zapisywania urządzeń: {ex.Message}");
            }
        }

        private static List<Device> LoadFromFile()
        {
            try
            {
                if (File.Exists(DevicesFilePath))
                {
                    string json = File.ReadAllText(DevicesFilePath);
                    return System.Text.Json.JsonSerializer.Deserialize<List<Device>>(json) ?? new List<Device>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas wczytywania urządzeń: {ex.Message}");
            }

            return new List<Device>();
        }
    }
}
