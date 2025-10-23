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

        private static HotkeyHelper? _globalInstance;
        private static Action? _globalOnHotKeyPressed;
        
        private readonly Window? _window;
        private HwndSource? _source;
        private Action? _onHotKeyPressed;
        private bool _isGlobalHotkeyRegistered = false;
        private bool _isLocalHotkeyRegistered = false;

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
        /// 构造函数，用于创建全局热键助手（不依赖特定窗口）
        /// Constructor, for creating global hotkey helper (not dependent on specific window)
        /// </summary>
        public HotkeyHelper()
        {
            // 创建一个隐藏的窗口来处理全局热键消息
            var parameters = new HwndSourceParameters("HotkeyWindow")
            {
                WindowStyle = 0x800000, // WS_EX_NOACTIVATE
                ExtendedWindowStyle = 0x08000000, // WS_EX_TOOLWINDOW
                Width = 0,
                Height = 0,
                ParentWindow = IntPtr.Zero
            };
            
            _source = new HwndSource(parameters);
            _source.AddHook(new HwndSourceHook(HwndHook));
        }

        /// <summary>
        /// 注册热键
        /// Register hotkey
        /// </summary>
        /// <param name="modifier">修饰键</param>
        /// <param name="key">按键</param>
        /// <param name="onHotKeyPressed">热键按下时的回调函数</param>
        public bool Register(ModifierKeys modifier, Key key, Action onHotKeyPressed)
        {
            _onHotKeyPressed = onHotKeyPressed;
            _isLocalHotkeyRegistered = false;
            
            IntPtr handle;
            if (_window != null)
            {
                handle = new WindowInteropHelper(_window).Handle;
                if (_source == null)
                {
                    _source = HwndSource.FromHwnd(handle);
                    _source.AddHook(new HwndSourceHook(HwndHook));
                }
            }
            else if (_source != null)
            {
                handle = _source.Handle;
            }
            else
            {
                LoggingHelper.Log("No valid window handle available for hotkey registration");
                return false;
            }

            // 先尝试取消注册已有的热键，避免冲突
            UnregisterHotKey(handle, HOTKEY_ID);
            
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            uint modifiers = (uint)modifier;
            
            if (!RegisterHotKey(handle, HOTKEY_ID, modifiers, virtualKey))
            {
                // 获取错误代码
                int errorCode = Marshal.GetLastWin32Error();
                LoggingHelper.Log($"Failed to register local hotkey. Error code: {errorCode}");
                return false;
            }
            
            _isLocalHotkeyRegistered = true;
            LoggingHelper.Log($"Local hotkey registered successfully: {modifier} + {key}");
            return true;
        }

        /// <summary>
        /// 注册全局热键（静态方法）
        /// Register global hotkey (static method)
        /// </summary>
        /// <param name="modifier">修饰键</param>
        /// <param name="key">按键</param>
        /// <param name="onHotKeyPressed">热键按下时的回调函数</param>
        public static bool RegisterGlobalHotkey(ModifierKeys modifier, Key key, Action onHotKeyPressed)
        {
            if (_globalInstance == null)
            {
                _globalInstance = new HotkeyHelper();
            }
            
            _globalOnHotKeyPressed = onHotKeyPressed;
            _globalInstance._isGlobalHotkeyRegistered = false;
            
            if (_globalInstance._source != null)
            {
                IntPtr handle = _globalInstance._source.Handle;
                
                // 先尝试取消注册已有的热键，避免冲突
                UnregisterHotKey(handle, HOTKEY_ID);
                
                uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                uint modifiers = (uint)modifier;
                
                if (!RegisterHotKey(handle, HOTKEY_ID, modifiers, virtualKey))
                {
                    // 获取错误代码
                    int errorCode = Marshal.GetLastWin32Error();
                    LoggingHelper.Log($"Failed to register global hotkey. Error code: {errorCode}");
                    return false;
                }
                
                _globalInstance._isGlobalHotkeyRegistered = true;
                LoggingHelper.Log($"Global hotkey registered successfully: {modifier} + {key}");
                return true;
            }
            
            LoggingHelper.Log("No valid window handle available for global hotkey registration");
            return false;
        }

        /// <summary>
        /// 取消注册热键
        /// Unregister hotkey
        /// </summary>
        public void Unregister()
        {
            if (!_isLocalHotkeyRegistered) return;
            
            IntPtr handle;
            if (_window != null)
            {
                handle = new WindowInteropHelper(_window).Handle;
            }
            else if (_source != null)
            {
                handle = _source.Handle;
            }
            else
            {
                return;
            }
            
            UnregisterHotKey(handle, HOTKEY_ID);
            _isLocalHotkeyRegistered = false;
            _source?.RemoveHook(new HwndSourceHook(HwndHook));
        }

        /// <summary>
        /// 取消注册全局热键
        /// Unregister global hotkey
        /// </summary>
        public static void UnregisterGlobalHotkey()
        {
            if (_globalInstance != null && _globalInstance._isGlobalHotkeyRegistered && _globalInstance._source != null)
            {
                UnregisterHotKey(_globalInstance._source.Handle, HOTKEY_ID);
                _globalInstance._isGlobalHotkeyRegistered = false;
                _globalInstance._source.RemoveHook(new HwndSourceHook(_globalInstance.HwndHook));
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _onHotKeyPressed?.Invoke();
                _globalOnHotKeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _source?.Dispose();
            GC.SuppressFinalize(this);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}