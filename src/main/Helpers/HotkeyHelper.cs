using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace PigPicPot.Helpers
{
    public class HotkeyHelper : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        private readonly Window _window;
        private HwndSource? _source;
        private Action? _onHotKeyPressed;

        public HotkeyHelper(Window window)
        {
            _window = window;
            var helper = new WindowInteropHelper(_window);
            helper.EnsureHandle();
        }

        public void Register(ModifierKeys modifier, Key key, Action onHotKeyPressed)
        {
            _onHotKeyPressed = onHotKeyPressed;
            var handle = new WindowInteropHelper(_window).Handle;
            _source = HwndSource.FromHwnd(handle);
            _source.AddHook(HwndHook);

            if (!RegisterHotKey(handle, HOTKEY_ID, (uint)modifier, (uint)KeyInterop.VirtualKeyFromKey(key)))
            {
                throw new InvalidOperationException("Failed to register hotkey.");
            }
        }

        public void Unregister()
        {
            var handle = new WindowInteropHelper(_window).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);
            _source?.RemoveHook(HwndHook);
            _source = null;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _onHotKeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            GC.SuppressFinalize(this);
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
