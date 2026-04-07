using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MonitorSwap.Native;

namespace MonitorSwap.Services
{
    internal sealed class MonitorDisplayService
    {
        public IReadOnlyList<MonitorInfo> GetMonitorInfos()
        {
            var metadataByDeviceName = new Dictionary<string, MonitorMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in QueryMonitorMetadata())
            {
                if (string.IsNullOrWhiteSpace(entry.DeviceName))
                {
                    continue;
                }

                MonitorMetadata current;
                if (!metadataByDeviceName.TryGetValue(entry.DeviceName, out current))
                {
                    metadataByDeviceName[entry.DeviceName] = entry;
                    continue;
                }

                if (ShouldUseCandidateFriendlyName(current.FriendlyName, entry.FriendlyName))
                {
                    current.FriendlyName = entry.FriendlyName;
                }

                if (ShouldUseCandidateStableId(current.StableId, entry.StableId, entry.DeviceName))
                {
                    current.StableId = entry.StableId;
                }
            }

            return Screen.AllScreens
                .Select(
                    screen =>
                    {
                        MonitorMetadata metadata;
                        metadataByDeviceName.TryGetValue(screen.DeviceName, out metadata);
                        return new MonitorInfo(
                            screen,
                            metadata != null ? metadata.StableId : screen.DeviceName,
                            metadata != null ? metadata.FriendlyName : null);
                    })
                .OrderBy(info => info.Screen.Bounds.Left)
                .ThenBy(info => info.Screen.Bounds.Top)
                .ToList();
        }

        public List<string> ResolveStableIds(IEnumerable<string> savedMonitorIds, IEnumerable<string> legacyDeviceNames = null)
        {
            var monitors = GetMonitorInfos();
            var selectedTokens = CreateSelectionSet(savedMonitorIds, legacyDeviceNames);
            if (selectedTokens.Count == 0)
            {
                return monitors
                    .Select(monitor => monitor.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return monitors
                .Where(monitor => selectedTokens.Contains(monitor.Id) || selectedTokens.Contains(monitor.DeviceName))
                .Select(monitor => monitor.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<MonitorInfo> ResolveSelectedMonitors(IEnumerable<string> savedMonitorIds, IEnumerable<string> legacyDeviceNames = null)
        {
            var monitors = GetMonitorInfos();
            var selectedTokens = CreateSelectionSet(savedMonitorIds, legacyDeviceNames);
            if (selectedTokens.Count == 0)
            {
                return monitors;
            }

            return monitors
                .Where(monitor => selectedTokens.Contains(monitor.Id) || selectedTokens.Contains(monitor.DeviceName))
                .ToList();
        }

        private static HashSet<string> CreateSelectionSet(IEnumerable<string> savedMonitorIds, IEnumerable<string> legacyDeviceNames)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddSelectionTokens(result, savedMonitorIds);
            AddSelectionTokens(result, legacyDeviceNames);
            return result;
        }

        private static void AddSelectionTokens(ISet<string> selectionSet, IEnumerable<string> tokens)
        {
            if (tokens == null)
            {
                return;
            }

            foreach (var token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    selectionSet.Add(token);
                }
            }
        }

        private static IEnumerable<MonitorMetadata> QueryMonitorMetadata()
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
                    if (!string.IsNullOrWhiteSpace(sourceName))
                    {
                        yield return new MonitorMetadata
                        {
                            DeviceName = sourceName,
                            FriendlyName = friendlyName,
                            StableId = GetTargetStableId(path) ?? sourceName
                        };
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

        private static string GetTargetStableId(DISPLAYCONFIG_PATH_INFO path)
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
                ? Sanitize(request.monitorDevicePath)
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

        private static bool ShouldUseCandidateFriendlyName(string currentValue, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentValue))
            {
                return true;
            }

            return IsGenericName(currentValue) && !IsGenericName(candidate);
        }

        private static bool ShouldUseCandidateStableId(string currentStableId, string candidateStableId, string deviceName)
        {
            if (string.IsNullOrWhiteSpace(candidateStableId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentStableId))
            {
                return true;
            }

            return string.Equals(currentStableId, deviceName, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(candidateStableId, deviceName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGenericName(string value)
        {
            return string.Equals(value, "Generic PnP Monitor", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PnP-Monitor (Standard)", StringComparison.OrdinalIgnoreCase);
        }

        internal sealed class MonitorInfo
        {
            public MonitorInfo(Screen screen, string id, string friendlyName)
            {
                Screen = screen;
                DeviceName = screen.DeviceName;
                Id = string.IsNullOrWhiteSpace(id) ? screen.DeviceName : id;
                FriendlyName = friendlyName;
            }

            public Screen Screen { get; private set; }

            public string DeviceName { get; private set; }

            public string Id { get; private set; }

            public string FriendlyName { get; private set; }
        }

        private sealed class MonitorMetadata
        {
            public string DeviceName { get; set; }

            public string FriendlyName { get; set; }

            public string StableId { get; set; }
        }
    }
}
