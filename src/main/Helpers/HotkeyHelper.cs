using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace PigPicPot.Helpers
{
    /// <summary>
    /// 热键助手类，用于注册和管理全局热键
    /// Hotkey helper class, used to register and manage global hotkeys
    /// </summary>
    public class HotkeyHelper : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        private readonly Window _window;
        private HwndSource? _source;
        private Action? _onHotKeyPressed;

        /// <summary>
        /// 构造函数，初始化热键助手
        /// Constructor, initialize hotkey helper
        /// </summary>
        /// <param name="window">关联的窗口</param>
        public HotkeyHelper(Window window)
        {
            _window = window;
            var helper = new WindowInteropHelper(_window);
            helper.EnsureHandle();
        }

        /// <summary>
        /// 注册热键
        /// Register hotkey
        /// </summary>
        /// <param name="modifier">修饰键</param>
        /// <param name="key">按键</param>
        /// <param name="onHotKeyPressed">热键按下时的回调函数</param>
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

        /// <summary>
        /// 取消注册热键
        /// Unregister hotkey
        /// </summary>
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
