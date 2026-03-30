using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using MonitorSwap.Models;
using MonitorSwap.Native;

namespace MonitorSwap.Services
{
    internal sealed class HotkeyManager : IDisposable
    {
        private readonly Dictionary<int, RotationDirection> _registeredHotkeys;
        private readonly HotkeyWindow _window;
        private bool _disposed;

        public HotkeyManager()
        {
            _registeredHotkeys = new Dictionary<int, RotationDirection>();
            _window = new HotkeyWindow();
            _window.HotkeyReceived += OnHotkeyReceived;
        }

        public event EventHandler<RotationDirection> HotkeyPressed;

        public void RegisterHotkeys(AppSettings settings)
        {
            EnsureNotDisposed();
            UnregisterAll();

            RegisterHotkey(1, settings.RotateLeftHotkey, RotationDirection.Left);
            RegisterHotkey(2, settings.RotateRightHotkey, RotationDirection.Right);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            UnregisterAll();
            _window.HotkeyReceived -= OnHotkeyReceived;
            _window.DestroyHandle();
            _disposed = true;
        }

        private void RegisterHotkey(int id, HotkeyBinding binding, RotationDirection direction)
        {
            if (binding == null || !binding.IsValid)
            {
                throw new InvalidOperationException("단축키 구성이 올바르지 않습니다.");
            }

            var success = NativeMethods.RegisterHotKey(
                _window.Handle,
                id,
                binding.ToNativeModifiers(),
                (uint)(binding.KeyCode & Keys.KeyCode));

            if (!success)
            {
                throw new Win32Exception("단축키 등록에 실패했습니다: " + binding.ToDisplayString());
            }

            _registeredHotkeys[id] = direction;
        }

        private void UnregisterAll()
        {
            foreach (var id in _registeredHotkeys.Keys)
            {
                NativeMethods.UnregisterHotKey(_window.Handle, id);
            }

            _registeredHotkeys.Clear();
        }

        private void OnHotkeyReceived(object sender, int id)
        {
            RotationDirection direction;
            if (_registeredHotkeys.TryGetValue(id, out direction))
            {
                var handler = HotkeyPressed;
                if (handler != null)
                {
                    handler(this, direction);
                }
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("HotkeyManager");
            }
        }

        private sealed class HotkeyWindow : NativeWindow
        {
            public HotkeyWindow()
            {
                var parameters = new CreateParams
                {
                    Caption = "MonitorSwapHotkeyWindow",
                    Parent = new IntPtr(NativeMethods.HwndMessage)
                };

                CreateHandle(parameters);
            }

            public event EventHandler<int> HotkeyReceived;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WmHotKey)
                {
                    var handler = HotkeyReceived;
                    if (handler != null)
                    {
                        handler(this, m.WParam.ToInt32());
                    }
                    return;
                }

                base.WndProc(ref m);
            }
        }
    }
}
