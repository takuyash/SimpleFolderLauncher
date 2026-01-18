using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StylishLauncherINI
{
    static class Program
    {
        // --- Win32 API Definitions ---
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101; // 追加
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105; // 追加
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;

        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_CTRL_SHIFT_I = 9001;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // --- Fields ---
        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static DateTime _lastShiftTime = DateTime.MinValue;
        private const int DOUBLE_PRESS_MS = 300;
        private static LauncherForm _launcher;

        // 【重要】長押し判定用のフラグ
        private static bool _isShiftPressed = false;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            var ini = IniHelper.ReadIni(iniPath);
            string rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";

            _launcher = new LauncherForm(rootPath);
            IntPtr forceHandle = _launcher.Handle;
            _launcher.Hide();

            _hookID = SetHook(_proc);

            MessageWindow messageWindow = new MessageWindow();
            RegisterHotKey(messageWindow.Handle, HOTKEY_ID_CTRL_SHIFT_I, MOD_CONTROL | MOD_SHIFT, (int)Keys.I);
            messageWindow.LauncherRequested += ToggleLauncher;

            Application.Run();

            UnhookWindowsHookEx(_hookID);
            UnregisterHotKey(messageWindow.Handle, HOTKEY_ID_CTRL_SHIFT_I);
        }

        private static void ToggleLauncher(object sender, EventArgs e)
        {
            if (_launcher.Visible) _launcher.Hide();
            else
            {
                _launcher.Show();
                _launcher.Activate();
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // --- 1. キーが離された時の処理 ---
                if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    if (vkCode == VK_LSHIFT || vkCode == VK_RSHIFT)
                    {
                        _isShiftPressed = false; // 押し下げ状態を解除
                    }
                }

                // --- 2. キーが押された時の処理 ---
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    if (vkCode == VK_LSHIFT || vkCode == VK_RSHIFT)
                    {
                        // 既に押されている（長押し中）なら無視
                        if (_isShiftPressed)
                        {
                            return CallNextHookEx(_hookID, nCode, wParam, lParam);
                        }

                        _isShiftPressed = true; // 押し下げ状態を記録

                        var now = DateTime.Now;
                        if ((now - _lastShiftTime).TotalMilliseconds <= DOUBLE_PRESS_MS)
                        {
                            if (_launcher != null && _launcher.IsHandleCreated && !_launcher.IsDisposed)
                            {
                                _launcher.BeginInvoke(new Action(() => ToggleLauncher(null, null)));
                            }
                            _lastShiftTime = DateTime.MinValue; // 成功したらリセット
                        }
                        else
                        {
                            _lastShiftTime = now;
                        }
                    }
                    else
                    {
                        // Shift以外のキーが押されたらリセット
                        _lastShiftTime = DateTime.MinValue;
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private class MessageWindow : NativeWindow
        {
            public event EventHandler LauncherRequested;
            public MessageWindow() { CreateHandle(new CreateParams()); }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID_CTRL_SHIFT_I)
                {
                    LauncherRequested?.Invoke(this, EventArgs.Empty);
                }
                base.WndProc(ref m);
            }
        }
    }
}