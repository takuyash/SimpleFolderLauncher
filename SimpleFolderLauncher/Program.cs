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
        private const int WM_SYSKEYDOWN = 0x0104;
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
        private static LowLevelKeyboardProc _proc = HookCallback; // GC対策で静的変数に保持
        private static DateTime _lastShiftTime = DateTime.MinValue;
        private const int DOUBLE_PRESS_MS = 300;
        private static LauncherForm _launcher;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ini読み込み
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            var ini = IniHelper.ReadIni(iniPath);
            string rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";

            // --- Mainメソッド内 ---
            _launcher = new LauncherForm(rootPath);

            // ハンドルを強制的に作成させる
            IntPtr forceHandle = _launcher.Handle;

            _launcher.Hide();

            // 1. Shiftキー監視用のフックを開始
            _hookID = SetHook(_proc);

            // 2. Ctrl + Shift + I は引き続きホットキーで登録（これは文字入力と被らないためOK）
            MessageWindow messageWindow = new MessageWindow();
            RegisterHotKey(messageWindow.Handle, HOTKEY_ID_CTRL_SHIFT_I, MOD_CONTROL | MOD_SHIFT, (int)Keys.I);

            messageWindow.LauncherRequested += ToggleLauncher;

            Application.Run();

            // 終了処理
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
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == VK_LSHIFT || vkCode == VK_RSHIFT)
                {
                    var now = DateTime.Now;
                    if ((now - _lastShiftTime).TotalMilliseconds <= DOUBLE_PRESS_MS)
                    {
                        // ハンドルが作成されているか確認してから実行
                        if (_launcher != null && _launcher.IsHandleCreated && !_launcher.IsDisposed)
                        {
                            _launcher.BeginInvoke(new Action(() => ToggleLauncher(null, null)));
                        }
                        _lastShiftTime = DateTime.MinValue;
                    }
                    else
                    {
                        _lastShiftTime = now;
                    }
                }
                else
                {
                    _lastShiftTime = DateTime.MinValue;
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