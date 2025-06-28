using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;

namespace Else.PhoneMirror.ViewModels
{
    public static class IpHelper
    {
        // In-memory cache for local IP checks (key: IP string, value: bool result)
        private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(2);

        public static bool IsIpAddress(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var parts = text.Split(':');
            if (parts.Length > 2)
                return false;

            if (!IPAddress.TryParse(parts[0], out _))
                return false;

            if (parts.Length == 2)
            {
                if (!int.TryParse(parts[1], out var port) || port < 0 || port > 65535)
                    return false;
            }

            return true;
        }

        public static bool IsLocalIpAddress(string ipAddress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ipAddress))
                    return false;

                // Return cached result if available
                if (_cache.TryGetValue(ipAddress, out bool isLocalCached))
                {
                    return isLocalCached;
                }

                if (!IPAddress.TryParse(ipAddress, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new ArgumentException("Podaj poprawny adres IPv4.", nameof(ipAddress));
                }

                bool isLocal = false;

                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                             .Where(ni => ni.OperationalStatus == OperationalStatus.Up))
                {
                    var props = nic.GetIPProperties();
                    foreach (var uni in props.UnicastAddresses
                                 .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork))
                    {
                        if (uni.IPv4Mask is null)
                            continue;

                        var localNet = GetNetworkAddress(uni.Address, uni.IPv4Mask);
                        var targetNet = GetNetworkAddress(ip, uni.IPv4Mask);
                        if (localNet.Equals(targetNet))
                        {
                            isLocal = true;
                            break;
                        }
                    }

                    if (isLocal)
                        break;
                }

                // Cache the computed result for the specified duration
                _cache.Set(ipAddress, isLocal, _cacheDuration);

                return isLocal;
            }
            catch (Exception exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Oblicza adres sieciowy (network address) dla podanego IP i maski.
        /// </summary>
        private static IPAddress GetNetworkAddress(IPAddress address, IPAddress mask)
        {
            var ipBytes = address.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            if (ipBytes.Length != maskBytes.Length)
                throw new ArgumentException("Maska i adres różnią się długością.");

            var netBytes = new byte[ipBytes.Length];
            for (int i = 0; i < netBytes.Length; i++)
            {
                netBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }

            return new IPAddress(netBytes);
        }
    }
}
