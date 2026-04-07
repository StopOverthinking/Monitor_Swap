using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MonitorSwap.Models;
using MonitorSwap.Native;

namespace MonitorSwap.Services
{
    internal sealed class WindowRotationService
    {
        private readonly MonitorDisplayService _monitorDisplayService = new MonitorDisplayService();

        private static readonly HashSet<string> ExcludedWindowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman",
            "WorkerW",
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "NotifyIconOverflowWindow"
        };

        private static readonly IntPtr ShellWindowHandle = NativeMethods.GetShellWindow();

        public RotationResult Rotate(RotationDirection direction, AppSettings settings)
        {
            settings.EnsureDefaults();

            var includedScreens = GetIncludedScreens(settings);
            if (includedScreens.Count < 2)
            {
                return RotationResult.Failed("Select at least two monitors to rotate windows.");
            }

            var windows = CaptureWindows(includedScreens, settings.IncludeMinimizedWindows).ToList();
            if (windows.Count == 0)
            {
                return RotationResult.Failed("No eligible windows were found on the selected monitors.");
            }

            var sourceToTarget = BuildScreenRotationMap(includedScreens, direction);
            var movedCount = 0;
            foreach (var window in windows.OrderBy(window => window.ZOrder))
            {
                Screen targetScreen;
                if (!sourceToTarget.TryGetValue(window.SourceScreen.DeviceName, out targetScreen))
                {
                    continue;
                }

                if (MoveWindow(window, targetScreen))
                {
                    window.WasMoved = true;
                    window.TargetScreen = targetScreen;
                    movedCount++;
                }
            }

            if (settings.PreserveWindowOrder)
            {
                RestoreWindowOrder(windows);
            }

            return RotationResult.Succeeded(
                string.Format(
                    "{0} window(s) rotated {1}.",
                    movedCount,
                    direction == RotationDirection.Left ? "left" : "right"));
        }

        private List<Screen> GetIncludedScreens(AppSettings settings)
        {
            return _monitorDisplayService
                .ResolveSelectedMonitors(settings.IncludedMonitorIds, settings.IncludedMonitorDeviceNames)
                .Select(monitor => monitor.Screen)
                .OrderBy(screen => screen.Bounds.Left)
                .ThenBy(screen => screen.Bounds.Top)
                .ToList();
        }

        private static IDictionary<string, Screen> BuildScreenRotationMap(IReadOnlyList<Screen> screens, RotationDirection direction)
        {
            var map = new Dictionary<string, Screen>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < screens.Count; index++)
            {
                var targetIndex = direction == RotationDirection.Right
                    ? (index + 1) % screens.Count
                    : (index - 1 + screens.Count) % screens.Count;
                map[screens[index].DeviceName] = screens[targetIndex];
            }

            return map;
        }

        private static IEnumerable<CapturedWindow> CaptureWindows(IReadOnlyList<Screen> includedScreens, bool includeMinimized)
        {
            var screensByName = includedScreens.ToDictionary(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase);
            var zOrder = 0;
            var windows = new List<CapturedWindow>();

            NativeMethods.EnumWindows(
                delegate(IntPtr hWnd, IntPtr _)
                {
                    if (!ShouldIncludeWindow(hWnd, includeMinimized))
                    {
                        return true;
                    }

                    CapturedWindow snapshot;
                    if (!TryCreateSnapshot(hWnd, zOrder++, screensByName, out snapshot))
                    {
                        return true;
                    }

                    windows.Add(snapshot);
                    return true;
                },
                IntPtr.Zero);

            return windows;
        }

        private static bool ShouldIncludeWindow(IntPtr hWnd, bool includeMinimized)
        {
            if (hWnd == IntPtr.Zero || hWnd == ShellWindowHandle || IsCurrentProcessWindow(hWnd))
            {
                return false;
            }

            var isMinimized = NativeMethods.IsIconic(hWnd);
            if (!NativeMethods.IsWindowVisible(hWnd) && !(includeMinimized && isMinimized))
            {
                return false;
            }

            if (!includeMinimized && isMinimized)
            {
                return false;
            }

            var style = (uint)NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GwlStyle).ToInt64();
            if ((style & NativeMethods.WsChild) != 0)
            {
                return false;
            }

            var exStyle = (uint)NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GwlExStyle).ToInt64();
            if ((exStyle & NativeMethods.WsExToolWindow) != 0)
            {
                return false;
            }

            var className = GetClassName(hWnd);
            if (ExcludedWindowClasses.Contains(className))
            {
                return false;
            }

            if (IsCloaked(hWnd))
            {
                return false;
            }

            var owner = NativeMethods.GetWindow(hWnd, NativeMethods.GwOwner);
            if (owner != IntPtr.Zero && (exStyle & NativeMethods.WsExAppWindow) == 0)
            {
                RECT rect;
                RECT ownerRect;
                if (NativeMethods.GetWindowRect(hWnd, out rect) &&
                    NativeMethods.GetWindowRect(owner, out ownerRect))
                {
                    var currentRect = NativeMethods.ToRectangle(rect);
                    var ownerRectangle = NativeMethods.ToRectangle(ownerRect);
                    if (currentRect == ownerRectangle)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryCreateSnapshot(IntPtr hWnd, int zOrder, IDictionary<string, Screen> screensByName, out CapturedWindow snapshot)
        {
            snapshot = null;

            var placement = CreateWindowPlacement();
            if (!NativeMethods.GetWindowPlacement(hWnd, ref placement))
            {
                return false;
            }

            Rectangle windowRectangle;
            if (!TryGetEffectiveRectangle(hWnd, placement, out windowRectangle))
            {
                return false;
            }

            var sourceScreen = Screen.FromRectangle(windowRectangle);
            Screen includedScreen;
            if (!screensByName.TryGetValue(sourceScreen.DeviceName, out includedScreen))
            {
                return false;
            }

            var restoreRectangle = NativeMethods.ToRectangle(placement.rcNormalPosition);
            if (restoreRectangle.Width <= 0 || restoreRectangle.Height <= 0)
            {
                restoreRectangle = windowRectangle;
            }

            var exStyle = (uint)NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GwlExStyle).ToInt64();

            snapshot = new CapturedWindow
            {
                Handle = hWnd,
                SourceScreen = includedScreen,
                WindowRectangle = windowRectangle,
                RestoreRectangle = restoreRectangle,
                Placement = placement,
                ZOrder = zOrder,
                IsMinimized = placement.showCmd == NativeMethods.SwShowMinimized || NativeMethods.IsIconic(hWnd),
                IsMaximized = placement.showCmd == NativeMethods.SwShowMaximized || NativeMethods.IsZoomed(hWnd),
                IsFullScreen = IsFullScreenWindow(windowRectangle, includedScreen.Bounds),
                IsTopMost = (exStyle & NativeMethods.WsExTopMost) != 0
            };
            return true;
        }

        private static bool TryGetEffectiveRectangle(IntPtr hWnd, WINDOWPLACEMENT placement, out Rectangle rectangle)
        {
            if (NativeMethods.IsIconic(hWnd))
            {
                rectangle = NativeMethods.ToRectangle(placement.rcNormalPosition);
                return rectangle.Width > 0 && rectangle.Height > 0;
            }

            RECT dwmRect;
            if (NativeMethods.DwmGetWindowAttribute(
                    hWnd,
                    NativeMethods.DwmaExtendedFrameBounds,
                    out dwmRect,
                    System.Runtime.InteropServices.Marshal.SizeOf(typeof(RECT))) == 0)
            {
                rectangle = NativeMethods.ToRectangle(dwmRect);
                return rectangle.Width > 0 && rectangle.Height > 0;
            }

            RECT rect;
            if (NativeMethods.GetWindowRect(hWnd, out rect))
            {
                rectangle = NativeMethods.ToRectangle(rect);
                return rectangle.Width > 0 && rectangle.Height > 0;
            }

            rectangle = Rectangle.Empty;
            return false;
        }

        private static WINDOWPLACEMENT CreateWindowPlacement()
        {
            return new WINDOWPLACEMENT
            {
                length = System.Runtime.InteropServices.Marshal.SizeOf(typeof(WINDOWPLACEMENT))
            };
        }

        private static bool MoveWindow(CapturedWindow window, Screen targetScreen)
        {
            var normalTargetRectangle = TranslateRectangle(window.RestoreRectangle, window.SourceScreen.WorkingArea, targetScreen.WorkingArea);
            if (normalTargetRectangle.Width <= 0 || normalTargetRectangle.Height <= 0)
            {
                return false;
            }

            var placement = window.Placement;
            placement.length = System.Runtime.InteropServices.Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            placement.rcNormalPosition = NativeMethods.FromRectangle(normalTargetRectangle);
            if (window.IsMinimized)
            {
                return NativeMethods.SetWindowPlacement(window.Handle, ref placement);
            }

            var flags = NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder;

            if (window.IsFullScreen)
            {
                return NativeMethods.SetWindowPos(
                    window.Handle,
                    NativeMethods.HwndTop,
                    targetScreen.Bounds.Left,
                    targetScreen.Bounds.Top,
                    targetScreen.Bounds.Width,
                    targetScreen.Bounds.Height,
                    flags);
            }

            if (window.IsMaximized)
            {
                return MoveMaximizedWindow(window.Handle, placement, normalTargetRectangle, flags);
            }

            return NativeMethods.SetWindowPos(
                window.Handle,
                NativeMethods.HwndTop,
                normalTargetRectangle.Left,
                normalTargetRectangle.Top,
                normalTargetRectangle.Width,
                normalTargetRectangle.Height,
                flags);
        }

        private static bool MoveMaximizedWindow(IntPtr hWnd, WINDOWPLACEMENT placement, Rectangle targetRectangle, uint flags)
        {
            placement.showCmd = NativeMethods.SwShowNormal;
            placement.rcNormalPosition = NativeMethods.FromRectangle(targetRectangle);

            if (!NativeMethods.SetWindowPlacement(hWnd, ref placement))
            {
                return false;
            }

            NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);

            if (!NativeMethods.SetWindowPos(
                    hWnd,
                    NativeMethods.HwndTop,
                    targetRectangle.Left,
                    targetRectangle.Top,
                    targetRectangle.Width,
                    targetRectangle.Height,
                    flags))
            {
                return false;
            }

            NativeMethods.ShowWindow(hWnd, NativeMethods.SwShowMaximized);
            return true;
        }

        private static void RestoreWindowOrder(IReadOnlyList<CapturedWindow> windows)
        {
            var movedGroups = windows
                .Where(window => window.WasMoved && !window.IsMinimized && window.TargetScreen != null)
                .GroupBy(window => window.TargetScreen.DeviceName, StringComparer.OrdinalIgnoreCase);

            foreach (var group in movedGroups)
            {
                RestoreWindowOrderForBand(group.Where(window => !window.IsTopMost).OrderBy(window => window.ZOrder).ToList(), false);
                RestoreWindowOrderForBand(group.Where(window => window.IsTopMost).OrderBy(window => window.ZOrder).ToList(), true);
            }
        }

        private static void RestoreWindowOrderForBand(IReadOnlyList<CapturedWindow> windows, bool topMost)
        {
            if (windows.Count < 2)
            {
                return;
            }

            IntPtr insertAfter = topMost ? NativeMethods.HwndTopMost : NativeMethods.HwndTop;
            foreach (var window in windows)
            {
                if (!NativeMethods.IsWindow(window.Handle))
                {
                    continue;
                }

                NativeMethods.SetWindowPos(
                    window.Handle,
                    insertAfter,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SwpNoMove |
                    NativeMethods.SwpNoSize |
                    NativeMethods.SwpNoActivate |
                    NativeMethods.SwpNoOwnerZOrder);

                insertAfter = window.Handle;
            }
        }

        private static Rectangle TranslateRectangle(Rectangle sourceRectangle, Rectangle sourceArea, Rectangle targetArea)
        {
            var sourceWidth = Math.Max(1, sourceArea.Width);
            var sourceHeight = Math.Max(1, sourceArea.Height);

            var widthRatio = Math.Max(0.15, Math.Min(1.0, sourceRectangle.Width / (double)sourceWidth));
            var heightRatio = Math.Max(0.15, Math.Min(1.0, sourceRectangle.Height / (double)sourceHeight));
            var xRatio = (sourceRectangle.Left - sourceArea.Left) / (double)sourceWidth;
            var yRatio = (sourceRectangle.Top - sourceArea.Top) / (double)sourceHeight;

            var targetWidth = Math.Min(targetArea.Width, Math.Max(120, (int)Math.Round(targetArea.Width * widthRatio)));
            var targetHeight = Math.Min(targetArea.Height, Math.Max(120, (int)Math.Round(targetArea.Height * heightRatio)));
            var maxXOffset = Math.Max(0, targetArea.Width - targetWidth);
            var maxYOffset = Math.Max(0, targetArea.Height - targetHeight);

            var targetX = targetArea.Left + Clamp((int)Math.Round(xRatio * targetArea.Width), 0, maxXOffset);
            var targetY = targetArea.Top + Clamp((int)Math.Round(yRatio * targetArea.Height), 0, maxYOffset);

            return new Rectangle(targetX, targetY, targetWidth, targetHeight);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        private static bool IsCloaked(IntPtr hWnd)
        {
            int cloaked;
            return NativeMethods.DwmGetWindowAttribute(
                       hWnd,
                       NativeMethods.DwmaCloaked,
                       out cloaked,
                       sizeof(int)) == 0 &&
                   cloaked != 0;
        }

        private static string GetClassName(IntPtr hWnd)
        {
            var buffer = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        private static bool IsFullScreenWindow(Rectangle rectangle, Rectangle screenBounds)
        {
            const int tolerance = 2;
            return Math.Abs(rectangle.Left - screenBounds.Left) <= tolerance &&
                   Math.Abs(rectangle.Top - screenBounds.Top) <= tolerance &&
                   Math.Abs(rectangle.Right - screenBounds.Right) <= tolerance &&
                   Math.Abs(rectangle.Bottom - screenBounds.Bottom) <= tolerance;
        }

        private static bool IsCurrentProcessWindow(IntPtr hWnd)
        {
            uint processId;
            NativeMethods.GetWindowThreadProcessId(hWnd, out processId);
            return processId == (uint)Process.GetCurrentProcess().Id;
        }

        private sealed class CapturedWindow
        {
            public IntPtr Handle { get; set; }

            public Screen SourceScreen { get; set; }

            public Rectangle WindowRectangle { get; set; }

            public Rectangle RestoreRectangle { get; set; }

            public WINDOWPLACEMENT Placement { get; set; }

            public int ZOrder { get; set; }

            public bool IsMinimized { get; set; }

            public bool IsMaximized { get; set; }

            public bool IsFullScreen { get; set; }

            public bool IsTopMost { get; set; }

            public bool WasMoved { get; set; }

            public Screen TargetScreen { get; set; }
        }
    }

    internal sealed class RotationResult
    {
        public bool Success { get; private set; }

        public string Message { get; private set; }

        public static RotationResult Succeeded(string message)
        {
            return new RotationResult { Success = true, Message = message };
        }

        public static RotationResult Failed(string message)
        {
            return new RotationResult { Success = false, Message = message };
        }
    }

    internal enum RotationDirection
    {
        Left,
        Right
    }
}
