using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MonitorSwap.Models;
using MonitorSwap.Native;

namespace MonitorSwap.Services
{
    internal sealed class WindowRotationService
    {
        private const int MoveValidationAttempts = 6;
        private const int MoveValidationDelayMs = 40;
        private const int FullscreenBreakMargin = 24;
        private readonly MonitorDisplayService _monitorDisplayService = new MonitorDisplayService();
        private readonly RotationTraceService _traceService = new RotationTraceService();

        private static readonly HashSet<string> ExcludedWindowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman",
            "WorkerW",
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "NotifyIconOverflowWindow"
        };

        private static readonly HashSet<string> ChromiumProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "brave",
            "chrome",
            "msedge",
            "msedgewebview2",
            "opera",
            "vivaldi"
        };

        private static readonly IntPtr ShellWindowHandle = NativeMethods.GetShellWindow();

        public RotationResult Rotate(RotationDirection direction, AppSettings settings)
        {
            settings.EnsureDefaults();

            using (var trace = _traceService.BeginSession(settings, direction))
            {
                trace.Write(
                    "rotate-request direction={0} includeMinimized={1} preserveOrder={2} browserCompatibility={3} skipBrowserFullscreen={4}",
                    direction,
                    settings.IncludeMinimizedWindows,
                    settings.PreserveWindowOrder,
                    settings.EnableBrowserCompatibilityMode,
                    settings.SkipBrowserFullscreenWindows);

                var includedScreens = GetIncludedScreens(settings);
                trace.Write(
                    "included-screens count={0} details={1}",
                    includedScreens.Count,
                    string.Join(" | ", includedScreens.Select(DescribeScreen)));

                if (includedScreens.Count < 2)
                {
                    return RotationResult.Failed("Select at least two monitors to rotate windows.");
                }

                var windows = CaptureWindows(includedScreens, settings.IncludeMinimizedWindows, trace).ToList();
                trace.Write("captured-window-count={0}", windows.Count);
                if (windows.Count == 0)
                {
                    return RotationResult.Failed("No eligible windows were found on the selected monitors.");
                }

                var sourceToTarget = BuildScreenRotationMap(includedScreens, direction);
                var movedCount = 0;
                var skippedCount = 0;
                var failedCount = 0;

                foreach (var window in windows.OrderBy(window => window.ZOrder))
                {
                    Screen targetScreen;
                    if (!sourceToTarget.TryGetValue(window.SourceScreen.DeviceName, out targetScreen))
                    {
                        trace.Write("skip-no-target {0}", DescribeWindow(window));
                        continue;
                    }

                    if (settings.SkipBrowserFullscreenWindows && window.IsChromiumWindow && window.IsFullScreen)
                    {
                        skippedCount++;
                        trace.Write(
                            "skip-browser-fullscreen handle=0x{0:X} process={1} title=\"{2}\" source={3} target={4}",
                            window.Handle.ToInt64(),
                            window.ProcessName,
                            Truncate(window.WindowTitle, 80),
                            window.SourceScreen.DeviceName,
                            targetScreen.DeviceName);
                        continue;
                    }

                    string failureReason;
                    if (MoveWindow(window, targetScreen, settings, trace, out failureReason))
                    {
                        window.WasMoved = true;
                        window.TargetScreen = targetScreen;
                        movedCount++;
                        continue;
                    }

                    failedCount++;
                    trace.Write(
                        "move-failed handle=0x{0:X} process={1} class={2} source={3} target={4} reason={5}",
                        window.Handle.ToInt64(),
                        window.ProcessName,
                        window.ClassName,
                        window.SourceScreen.DeviceName,
                        targetScreen.DeviceName,
                        failureReason);
                }

                if (settings.PreserveWindowOrder)
                {
                    RestoreWindowOrder(windows, trace);
                }

                RefreshSurfaceBackedWindows(windows, trace);

                var message = BuildResultMessage(direction, movedCount, skippedCount, failedCount);
                trace.Write("rotate-result moved={0} skipped={1} failed={2} message=\"{3}\"", movedCount, skippedCount, failedCount, message);

                if (movedCount > 0)
                {
                    return RotationResult.Succeeded(message, skippedCount > 0 || failedCount > 0, skippedCount > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info);
                }

                if (skippedCount > 0)
                {
                    return RotationResult.Failed(message, true, ToolTipIcon.Warning);
                }

                return RotationResult.Failed(message, failedCount > 0, ToolTipIcon.Warning);
            }
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

        private static IEnumerable<CapturedWindow> CaptureWindows(
            IReadOnlyList<Screen> includedScreens,
            bool includeMinimized,
            RotationTraceService.RotationTraceSession trace)
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
                    trace.Write("captured {0}", DescribeWindow(snapshot));
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

        private static bool TryCreateSnapshot(
            IntPtr hWnd,
            int zOrder,
            IDictionary<string, Screen> screensByName,
            out CapturedWindow snapshot)
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
            var className = GetClassName(hWnd);
            uint processId;
            NativeMethods.GetWindowThreadProcessId(hWnd, out processId);
            var processName = TryGetProcessName(processId);
            var windowTitle = GetWindowTitle(hWnd);

            snapshot = new CapturedWindow
            {
                Handle = hWnd,
                ProcessId = processId,
                ProcessName = processName,
                WindowTitle = windowTitle,
                SourceScreen = includedScreen,
                WindowRectangle = windowRectangle,
                RestoreRectangle = restoreRectangle,
                Placement = placement,
                ZOrder = zOrder,
                IsMinimized = placement.showCmd == NativeMethods.SwShowMinimized || NativeMethods.IsIconic(hWnd),
                IsMaximized = placement.showCmd == NativeMethods.SwShowMaximized || NativeMethods.IsZoomed(hWnd),
                IsFullScreen = IsFullScreenWindow(windowRectangle, includedScreen.Bounds),
                IsTopMost = (exStyle & NativeMethods.WsExTopMost) != 0,
                ClassName = className,
                IsChromiumWindow = IsChromiumWindow(className, processName)
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
                    Marshal.SizeOf(typeof(RECT))) == 0)
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
                length = Marshal.SizeOf(typeof(WINDOWPLACEMENT))
            };
        }

        private static bool MoveWindow(
            CapturedWindow window,
            Screen targetScreen,
            AppSettings settings,
            RotationTraceService.RotationTraceSession trace,
            out string failureReason)
        {
            failureReason = null;

            var normalTargetRectangle = TranslateRectangle(window.RestoreRectangle, window.SourceScreen.WorkingArea, targetScreen.WorkingArea);
            if (normalTargetRectangle.Width <= 0 || normalTargetRectangle.Height <= 0)
            {
                failureReason = "target rectangle was invalid";
                return false;
            }

            var placement = window.Placement;
            placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            placement.rcNormalPosition = NativeMethods.FromRectangle(normalTargetRectangle);
            if (window.IsMinimized)
            {
                return SetWindowPlacementWithLogging(window.Handle, ref placement, trace, "move-minimized", out failureReason);
            }

            var useBrowserCompatibilityMode = settings.EnableBrowserCompatibilityMode && window.IsChromiumWindow;
            var flags = BuildMoveFlags(window, !useBrowserCompatibilityMode);

            trace.Write(
                "move-begin handle=0x{0:X} process={1} class={2} title=\"{3}\" src={4} dst={5} fullscreen={6} maximized={7} compatibility={8}",
                window.Handle.ToInt64(),
                window.ProcessName,
                window.ClassName,
                Truncate(window.WindowTitle, 80),
                window.SourceScreen.DeviceName,
                targetScreen.DeviceName,
                window.IsFullScreen,
                window.IsMaximized,
                useBrowserCompatibilityMode);

            if (useBrowserCompatibilityMode && window.IsFullScreen)
            {
                return MoveChromiumFullScreenWindow(window, targetScreen, placement, normalTargetRectangle, trace, out failureReason);
            }

            // Maximize semantics should win for standard desktop windows like Explorer.
            // Chromium fullscreen is handled above before we get here.
            if (window.IsMaximized)
            {
                return MoveMaximizedWindow(window, placement, normalTargetRectangle, trace, out failureReason);
            }

            if (window.IsFullScreen)
            {
                if (!SetWindowPosWithLogging(
                        window.Handle,
                        NativeMethods.HwndTop,
                        targetScreen.Bounds,
                        flags,
                        trace,
                        "move-fullscreen",
                        out failureReason))
                {
                    return false;
                }

                if (!ValidateWindowReachedTarget(window, targetScreen, targetScreen.Bounds, true, trace, "validate-fullscreen"))
                {
                    failureReason = "fullscreen validation failed";
                    return false;
                }

                return true;
            }

            if (RequiresSurfaceReset(window))
            {
                return MoveSurfaceBackedWindow(window.Handle, placement, normalTargetRectangle, flags, trace, "move-surface", out failureReason);
            }

            if (!SetWindowPosWithLogging(
                    window.Handle,
                    NativeMethods.HwndTop,
                    normalTargetRectangle,
                    flags,
                    trace,
                    "move-standard",
                    out failureReason))
            {
                return false;
            }

            if (!ValidateWindowReachedTarget(window, targetScreen, normalTargetRectangle, false, trace, "validate-standard"))
            {
                failureReason = "standard validation failed";
                return false;
            }

            return true;
        }

        private static bool MoveChromiumFullScreenWindow(
            CapturedWindow window,
            Screen targetScreen,
            WINDOWPLACEMENT placement,
            Rectangle normalTargetRectangle,
            RotationTraceService.RotationTraceSession trace,
            out string failureReason)
        {
            failureReason = null;
            var hWnd = window.Handle;
            var syncFlags = BuildMoveFlags(window, false);
            var breakoutRectangle = GetFullscreenBreakoutRectangle(window);

            placement.showCmd = NativeMethods.SwShowNormal;
            placement.rcNormalPosition = NativeMethods.FromRectangle(breakoutRectangle);

            if (!SetWindowPlacementWithLogging(hWnd, ref placement, trace, "compat-fullscreen-placement-breakout", out failureReason))
            {
                return false;
            }

            ShowWindowWithLogging(hWnd, NativeMethods.SwRestore, trace, "compat-fullscreen-restore");

            if (!SetWindowPosWithLogging(hWnd, NativeMethods.HwndTop, breakoutRectangle, syncFlags, trace, "compat-fullscreen-breakout", out failureReason))
            {
                return false;
            }

            PulseRedraw(hWnd, trace, "compat-fullscreen-breakout");
            Thread.Sleep(MoveValidationDelayMs);

            placement.rcNormalPosition = NativeMethods.FromRectangle(normalTargetRectangle);
            if (!SetWindowPlacementWithLogging(hWnd, ref placement, trace, "compat-fullscreen-target-placement", out failureReason))
            {
                return false;
            }

            if (!SetWindowPosWithLogging(hWnd, NativeMethods.HwndTop, normalTargetRectangle, syncFlags, trace, "compat-fullscreen-stage-move", out failureReason))
            {
                return false;
            }

            PulseRedraw(hWnd, trace, "compat-fullscreen-stage-move");
            Thread.Sleep(MoveValidationDelayMs);

            if (!SetWindowPosWithLogging(hWnd, NativeMethods.HwndTop, targetScreen.Bounds, syncFlags, trace, "compat-fullscreen-final-bounds", out failureReason))
            {
                return false;
            }

            PulseRedraw(hWnd, trace, "compat-fullscreen-final-bounds");

            if (ValidateWindowReachedTarget(window, targetScreen, targetScreen.Bounds, true, trace, "validate-compat-fullscreen"))
            {
                return true;
            }

            trace.Write("compat-fullscreen-validation-failed; entering fallback handle=0x{0:X}", hWnd.ToInt64());
            return AttemptChromiumCompatibilityFallback(window, targetScreen, placement, normalTargetRectangle, trace, out failureReason);
        }

        private static bool MoveMaximizedWindow(
            CapturedWindow window,
            WINDOWPLACEMENT placement,
            Rectangle targetRectangle,
            RotationTraceService.RotationTraceSession trace,
            out string failureReason)
        {
            failureReason = null;
            var hWnd = window.Handle;
            var syncFlags = BuildMoveFlags(window, false);

            placement.showCmd = NativeMethods.SwShowNormal;
            placement.rcNormalPosition = NativeMethods.FromRectangle(targetRectangle);

            if (!SetWindowPlacementWithLogging(hWnd, ref placement, trace, "move-maximized-placement-normal", out failureReason))
            {
                return false;
            }

            ShowWindowWithLogging(hWnd, NativeMethods.SwRestore, trace, "move-maximized-restore");

            if (!SetWindowPosWithLogging(hWnd, NativeMethods.HwndTop, targetRectangle, syncFlags, trace, "move-maximized-pos", out failureReason))
            {
                return false;
            }

            placement.showCmd = NativeMethods.SwShowMaximized;
            placement.rcNormalPosition = NativeMethods.FromRectangle(targetRectangle);
            if (!SetWindowPlacementWithLogging(hWnd, ref placement, trace, "move-maximized-placement-maximized", out failureReason))
            {
                return false;
            }

            ShowWindowWithLogging(hWnd, NativeMethods.SwShowMaximized, trace, "move-maximized-show");

            if (RequiresSurfaceReset(window))
            {
                PulseRedraw(hWnd, trace, "move-maximized-redraw");
            }

            if (!ValidateWindowReachedTarget(window, Screen.FromRectangle(targetRectangle), targetRectangle, false, trace, "validate-maximized"))
            {
                failureReason = "maximized validation failed";
                return false;
            }

            return true;
        }

        private static bool MoveSurfaceBackedWindow(
            IntPtr hWnd,
            WINDOWPLACEMENT placement,
            Rectangle targetRectangle,
            uint flags,
            RotationTraceService.RotationTraceSession trace,
            string stagePrefix,
            out string failureReason)
        {
            failureReason = null;

            placement.showCmd = NativeMethods.SwShowNormal;
            placement.rcNormalPosition = NativeMethods.FromRectangle(targetRectangle);
            if (!SetWindowPlacementWithLogging(hWnd, ref placement, trace, stagePrefix + "-placement", out failureReason))
            {
                return false;
            }

            if (!SetWindowPosWithLogging(hWnd, NativeMethods.HwndTop, targetRectangle, flags, trace, stagePrefix + "-pos", out failureReason))
            {
                return false;
            }

            PulseRedraw(hWnd, trace, stagePrefix + "-redraw");
            return true;
        }

        private static bool AttemptChromiumCompatibilityFallback(
            CapturedWindow window,
            Screen targetScreen,
            WINDOWPLACEMENT placement,
            Rectangle normalTargetRectangle,
            RotationTraceService.RotationTraceSession trace,
            out string failureReason)
        {
            failureReason = null;
            var hWnd = window.Handle;
            var syncFlags = BuildMoveFlags(window, false);
            var targetWorkingArea = targetScreen.WorkingArea;

            placement.showCmd = NativeMethods.SwShowNormal;
            placement.rcNormalPosition = NativeMethods.FromRectangle(targetWorkingArea);
            if (!SetWindowPlacementWithLogging(hWnd, ref placement, trace, "compat-fallback-placement-working-area", out failureReason))
            {
                return false;
            }

            ShowWindowWithLogging(hWnd, NativeMethods.SwRestore, trace, "compat-fallback-restore");

            if (!SetWindowPosWithLogging(hWnd, NativeMethods.HwndTop, targetWorkingArea, syncFlags, trace, "compat-fallback-working-area", out failureReason))
            {
                return false;
            }

            PulseRedraw(hWnd, trace, "compat-fallback-working-area");
            Thread.Sleep(MoveValidationDelayMs);

            if (!SetWindowPosWithLogging(hWnd, NativeMethods.HwndTop, targetScreen.Bounds, syncFlags, trace, "compat-fallback-target-bounds", out failureReason))
            {
                return false;
            }

            PulseRedraw(hWnd, trace, "compat-fallback-target-bounds");

            if (ValidateWindowReachedTarget(window, targetScreen, targetScreen.Bounds, true, trace, "validate-compat-fallback-bounds"))
            {
                return true;
            }

            if (!SetWindowPosWithLogging(hWnd, NativeMethods.HwndTop, normalTargetRectangle, syncFlags, trace, "compat-fallback-normal-target", out failureReason))
            {
                return false;
            }

            PulseRedraw(hWnd, trace, "compat-fallback-normal-target");

            if (ValidateWindowReachedTarget(window, targetScreen, normalTargetRectangle, false, trace, "validate-compat-fallback-normal"))
            {
                return true;
            }

            failureReason = "compatibility fallback validation failed";
            return false;
        }

        private static void RefreshSurfaceBackedWindows(IEnumerable<CapturedWindow> windows, RotationTraceService.RotationTraceSession trace)
        {
            var movedWindows = windows
                .Where(window => window.WasMoved && !window.IsMinimized && RequiresSurfaceReset(window))
                .ToList();

            foreach (var window in movedWindows)
            {
                if (!NativeMethods.IsWindow(window.Handle))
                {
                    continue;
                }

                string ignoredFailure;
                SetWindowPosWithLogging(
                    window.Handle,
                    NativeMethods.HwndTop,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SwpNoMove |
                    NativeMethods.SwpNoSize |
                    NativeMethods.SwpNoZOrder |
                    NativeMethods.SwpNoActivate |
                    NativeMethods.SwpNoOwnerZOrder |
                    NativeMethods.SwpFrameChanged,
                    trace,
                    "refresh-surface-frame",
                    out ignoredFailure);

                PulseRedraw(window.Handle, trace, "refresh-surface-redraw");
            }
        }

        private static void RestoreWindowOrder(IReadOnlyList<CapturedWindow> windows, RotationTraceService.RotationTraceSession trace)
        {
            var movedGroups = windows
                .Where(window => window.WasMoved && !window.IsMinimized && window.TargetScreen != null)
                .GroupBy(window => window.TargetScreen.DeviceName, StringComparer.OrdinalIgnoreCase);

            foreach (var group in movedGroups)
            {
                RestoreWindowOrderForBand(group.Where(window => !window.IsTopMost).OrderBy(window => window.ZOrder).ToList(), false, trace);
                RestoreWindowOrderForBand(group.Where(window => window.IsTopMost).OrderBy(window => window.ZOrder).ToList(), true, trace);
            }
        }

        private static void RestoreWindowOrderForBand(
            IReadOnlyList<CapturedWindow> windows,
            bool topMost,
            RotationTraceService.RotationTraceSession trace)
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

                string ignoredFailure;
                SetWindowPosWithLogging(
                    window.Handle,
                    insertAfter,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SwpNoMove |
                    NativeMethods.SwpNoSize |
                    NativeMethods.SwpNoActivate |
                    NativeMethods.SwpNoOwnerZOrder,
                    trace,
                    topMost ? "restore-order-topmost" : "restore-order-standard",
                    out ignoredFailure);

                insertAfter = window.Handle;
            }
        }

        private static uint BuildMoveFlags(CapturedWindow window, bool allowAsync)
        {
            var flags = NativeMethods.SwpNoActivate |
                        NativeMethods.SwpNoOwnerZOrder |
                        NativeMethods.SwpNoZOrder;
            if (allowAsync)
            {
                flags |= NativeMethods.SwpAsyncWindowPos;
            }

            if (RequiresSurfaceReset(window))
            {
                flags |= NativeMethods.SwpNoCopyBits | NativeMethods.SwpFrameChanged;
            }

            return flags;
        }

        private static Rectangle GetFullscreenBreakoutRectangle(CapturedWindow window)
        {
            var workingArea = window.SourceScreen.WorkingArea;
            if (workingArea.Width <= FullscreenBreakMargin * 2 || workingArea.Height <= FullscreenBreakMargin * 2)
            {
                return workingArea;
            }

            return Rectangle.FromLTRB(
                workingArea.Left + FullscreenBreakMargin,
                workingArea.Top + FullscreenBreakMargin,
                workingArea.Right - FullscreenBreakMargin,
                workingArea.Bottom - FullscreenBreakMargin);
        }

        private static bool ValidateWindowReachedTarget(
            CapturedWindow window,
            Screen targetScreen,
            Rectangle expectedRectangle,
            bool requireBoundsMatch,
            RotationTraceService.RotationTraceSession trace,
            string stage)
        {
            for (var attempt = 1; attempt <= MoveValidationAttempts; attempt++)
            {
                Rectangle actualRectangle;
                if (!TryReadCurrentWindowRectangle(window.Handle, out actualRectangle))
                {
                    trace.Write("validate stage={0} attempt={1} rect=unavailable", stage, attempt);
                    Thread.Sleep(MoveValidationDelayMs);
                    continue;
                }

                var actualScreen = Screen.FromRectangle(actualRectangle);
                var onTargetScreen = string.Equals(actualScreen.DeviceName, targetScreen.DeviceName, StringComparison.OrdinalIgnoreCase);
                var boundsMatch = !requireBoundsMatch || AreRectanglesClose(actualRectangle, expectedRectangle, 12);

                trace.Write(
                    "validate stage={0} attempt={1} rect={2} actualScreen={3} expectedScreen={4} onTarget={5} boundsMatch={6}",
                    stage,
                    attempt,
                    FormatRectangle(actualRectangle),
                    actualScreen.DeviceName,
                    targetScreen.DeviceName,
                    onTargetScreen,
                    boundsMatch);

                if (onTargetScreen && boundsMatch)
                {
                    return true;
                }

                Thread.Sleep(MoveValidationDelayMs);
            }

            return false;
        }

        private static bool TryReadCurrentWindowRectangle(IntPtr hWnd, out Rectangle rectangle)
        {
            var placement = CreateWindowPlacement();
            if (!NativeMethods.GetWindowPlacement(hWnd, ref placement))
            {
                rectangle = Rectangle.Empty;
                return false;
            }

            return TryGetEffectiveRectangle(hWnd, placement, out rectangle);
        }

        private static bool AreRectanglesClose(Rectangle actual, Rectangle expected, int tolerance)
        {
            return Math.Abs(actual.Left - expected.Left) <= tolerance &&
                   Math.Abs(actual.Top - expected.Top) <= tolerance &&
                   Math.Abs(actual.Right - expected.Right) <= tolerance &&
                   Math.Abs(actual.Bottom - expected.Bottom) <= tolerance;
        }

        private static void PulseRedraw(IntPtr hWnd, RotationTraceService.RotationTraceSession trace, string stage)
        {
            RedrawWindowWithLogging(
                hWnd,
                NativeMethods.RdwInvalidate |
                NativeMethods.RdwErase |
                NativeMethods.RdwAllChildren |
                NativeMethods.RdwFrame |
                NativeMethods.RdwUpdateNow,
                trace,
                stage);
            DwmFlushWithLogging(trace, stage);
        }

        private static bool SetWindowPosWithLogging(
            IntPtr hWnd,
            IntPtr insertAfter,
            Rectangle rectangle,
            uint flags,
            RotationTraceService.RotationTraceSession trace,
            string stage,
            out string failureReason)
        {
            return SetWindowPosWithLogging(
                hWnd,
                insertAfter,
                rectangle.Left,
                rectangle.Top,
                rectangle.Width,
                rectangle.Height,
                flags,
                trace,
                stage,
                out failureReason);
        }

        private static bool SetWindowPosWithLogging(
            IntPtr hWnd,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags,
            RotationTraceService.RotationTraceSession trace,
            string stage,
            out string failureReason)
        {
            var success = NativeMethods.SetWindowPos(hWnd, insertAfter, x, y, width, height, flags);
            var error = success ? 0 : Marshal.GetLastWin32Error();
            trace.Write(
                "set-window-pos stage={0} handle=0x{1:X} success={2} error={3} x={4} y={5} w={6} h={7} flags=0x{8:X}",
                stage,
                hWnd.ToInt64(),
                success,
                error,
                x,
                y,
                width,
                height,
                flags);

            failureReason = success ? null : string.Format("SetWindowPos failed with error {0} during {1}", error, stage);
            return success;
        }

        private static bool SetWindowPlacementWithLogging(
            IntPtr hWnd,
            ref WINDOWPLACEMENT placement,
            RotationTraceService.RotationTraceSession trace,
            string stage,
            out string failureReason)
        {
            var success = NativeMethods.SetWindowPlacement(hWnd, ref placement);
            var error = success ? 0 : Marshal.GetLastWin32Error();
            trace.Write(
                "set-window-placement stage={0} handle=0x{1:X} success={2} error={3} showCmd={4} rect={5}",
                stage,
                hWnd.ToInt64(),
                success,
                error,
                placement.showCmd,
                FormatRectangle(NativeMethods.ToRectangle(placement.rcNormalPosition)));

            failureReason = success ? null : string.Format("SetWindowPlacement failed with error {0} during {1}", error, stage);
            return success;
        }

        private static void ShowWindowWithLogging(
            IntPtr hWnd,
            int command,
            RotationTraceService.RotationTraceSession trace,
            string stage)
        {
            var result = NativeMethods.ShowWindow(hWnd, command);
            trace.Write(
                "show-window stage={0} handle=0x{1:X} command={2} result={3}",
                stage,
                hWnd.ToInt64(),
                command,
                result);
        }

        private static void RedrawWindowWithLogging(
            IntPtr hWnd,
            uint flags,
            RotationTraceService.RotationTraceSession trace,
            string stage)
        {
            var success = NativeMethods.RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, flags);
            var error = success ? 0 : Marshal.GetLastWin32Error();
            trace.Write(
                "redraw-window stage={0} handle=0x{1:X} success={2} error={3} flags=0x{4:X}",
                stage,
                hWnd.ToInt64(),
                success,
                error,
                flags);
        }

        private static void DwmFlushWithLogging(RotationTraceService.RotationTraceSession trace, string stage)
        {
            var result = NativeMethods.DwmFlush();
            trace.Write("dwm-flush stage={0} result={1}", stage, result);
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

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            var buffer = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        private static string TryGetProcessName(uint processId)
        {
            if (processId == 0)
            {
                return string.Empty;
            }

            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsChromiumWindow(string className, string processName)
        {
            return (!string.IsNullOrWhiteSpace(className) &&
                    className.StartsWith("Chrome_WidgetWin_", StringComparison.OrdinalIgnoreCase)) ||
                   ChromiumProcessNames.Contains(processName ?? string.Empty);
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

        private static bool RequiresSurfaceReset(CapturedWindow window)
        {
            if (window == null || string.IsNullOrWhiteSpace(window.ClassName))
            {
                return false;
            }

            return window.ClassName.StartsWith("HwndWrapper[", StringComparison.OrdinalIgnoreCase) ||
                   window.ClassName.StartsWith("Chrome_WidgetWin_", StringComparison.OrdinalIgnoreCase);
        }

        private static string DescribeScreen(Screen screen)
        {
            return string.Format(
                "{0}:{1}",
                screen.DeviceName,
                FormatRectangle(screen.Bounds));
        }

        private static string DescribeWindow(CapturedWindow window)
        {
            return string.Format(
                "handle=0x{0:X} pid={1} process={2} class={3} fullscreen={4} maximized={5} rect={6} screen={7} title=\"{8}\"",
                window.Handle.ToInt64(),
                window.ProcessId,
                string.IsNullOrWhiteSpace(window.ProcessName) ? "<unknown>" : window.ProcessName,
                string.IsNullOrWhiteSpace(window.ClassName) ? "<none>" : window.ClassName,
                window.IsFullScreen,
                window.IsMaximized,
                FormatRectangle(window.WindowRectangle),
                window.SourceScreen.DeviceName,
                Truncate(window.WindowTitle, 80));
        }

        private static string FormatRectangle(Rectangle rectangle)
        {
            return string.Format(
                "{0},{1},{2},{3}",
                rectangle.Left,
                rectangle.Top,
                rectangle.Width,
                rectangle.Height);
        }

        private static string BuildResultMessage(RotationDirection direction, int movedCount, int skippedCount, int failedCount)
        {
            var directionLabel = direction == RotationDirection.Left ? "left" : "right";
            var builder = new StringBuilder();
            builder.AppendFormat("{0} window(s) rotated {1}.", movedCount, directionLabel);

            if (skippedCount > 0)
            {
                builder.AppendFormat(" Skipped {0} browser fullscreen window(s).", skippedCount);
            }

            if (failedCount > 0)
            {
                builder.AppendFormat(" Failed to move {0} window(s).", failedCount);
            }

            return builder.ToString();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private sealed class CapturedWindow
        {
            public IntPtr Handle { get; set; }

            public uint ProcessId { get; set; }

            public string ProcessName { get; set; }

            public string WindowTitle { get; set; }

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

            public string ClassName { get; set; }

            public bool IsChromiumWindow { get; set; }
        }
    }

    internal sealed class RotationResult
    {
        public bool Success { get; private set; }

        public string Message { get; private set; }

        public bool ShouldNotifyUser { get; private set; }

        public ToolTipIcon NotificationIcon { get; private set; }

        public static RotationResult Succeeded(string message, bool shouldNotifyUser = false, ToolTipIcon notificationIcon = ToolTipIcon.Info)
        {
            return new RotationResult
            {
                Success = true,
                Message = message,
                ShouldNotifyUser = shouldNotifyUser,
                NotificationIcon = notificationIcon
            };
        }

        public static RotationResult Failed(string message, bool shouldNotifyUser = true, ToolTipIcon notificationIcon = ToolTipIcon.Warning)
        {
            return new RotationResult
            {
                Success = false,
                Message = message,
                ShouldNotifyUser = shouldNotifyUser,
                NotificationIcon = notificationIcon
            };
        }
    }

    internal enum RotationDirection
    {
        Left,
        Right
    }
}
