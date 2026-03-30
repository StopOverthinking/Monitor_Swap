using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MonitorSwap.Native;

namespace MonitorSwap.Services
{
    internal sealed class MonitorDisplayService
    {
        public IReadOnlyDictionary<string, string> GetFriendlyNames()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in QueryFriendlyNames())
            {
                var deviceName = entry.Key;
                var friendlyName = entry.Value;
                if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(friendlyName))
                {
                    continue;
                }

                if (!result.ContainsKey(deviceName) || ShouldReplace(result[deviceName], friendlyName))
                {
                    result[deviceName] = friendlyName;
                }
            }

            return result;
        }

        private static IEnumerable<KeyValuePair<string, string>> QueryFriendlyNames()
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                uint pathCount;
                uint modeCount;
                var status = NativeMethods.GetDisplayConfigBufferSizes(NativeMethods.QdcOnlyActivePaths, out pathCount, out modeCount);
                if (status != NativeMethods.ErrorSuccess)
                {
                    yield break;
                }

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                status = NativeMethods.QueryDisplayConfig(
                    NativeMethods.QdcOnlyActivePaths,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modes,
                    IntPtr.Zero);

                if (status == NativeMethods.ErrorInsufficientBuffer)
                {
                    continue;
                }

                if (status != NativeMethods.ErrorSuccess)
                {
                    yield break;
                }

                for (var i = 0; i < pathCount; i++)
                {
                    var path = paths[i];
                    var sourceName = GetSourceName(path);
                    var friendlyName = GetTargetFriendlyName(path);
                    if (!string.IsNullOrWhiteSpace(sourceName) && !string.IsNullOrWhiteSpace(friendlyName))
                    {
                        yield return new KeyValuePair<string, string>(sourceName, friendlyName);
                    }
                }

                yield break;
            }
        }

        private static string GetSourceName(DISPLAYCONFIG_PATH_INFO path)
        {
            var request = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = CreateHeader(
                    DISPLAYCONFIG_DEVICE_INFO_TYPE.GetSourceName,
                    (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME)),
                    path.sourceInfo.adapterId,
                    path.sourceInfo.id)
            };

            return NativeMethods.DisplayConfigGetDeviceInfo(ref request) == NativeMethods.ErrorSuccess
                ? Sanitize(request.viewGdiDeviceName)
                : null;
        }

        private static string GetTargetFriendlyName(DISPLAYCONFIG_PATH_INFO path)
        {
            var request = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = CreateHeader(
                    DISPLAYCONFIG_DEVICE_INFO_TYPE.GetTargetName,
                    (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME)),
                    path.targetInfo.adapterId,
                    path.targetInfo.id)
            };

            return NativeMethods.DisplayConfigGetDeviceInfo(ref request) == NativeMethods.ErrorSuccess
                ? Sanitize(request.monitorFriendlyDeviceName)
                : null;
        }

        private static DISPLAYCONFIG_DEVICE_INFO_HEADER CreateHeader(DISPLAYCONFIG_DEVICE_INFO_TYPE type, uint size, LUID adapterId, uint id)
        {
            return new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = type,
                size = size,
                adapterId = adapterId,
                id = id
            };
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('\0');
        }

        private static bool ShouldReplace(string currentValue, string candidate)
        {
            return IsGenericName(currentValue) && !IsGenericName(candidate);
        }

        private static bool IsGenericName(string value)
        {
            return string.Equals(value, "Generic PnP Monitor", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PnP-Monitor (Standard)", StringComparison.OrdinalIgnoreCase);
        }
    }
}
