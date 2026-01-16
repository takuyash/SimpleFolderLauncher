using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StylishLauncherINI
{
    static class Program
    {
        // modifier keys
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int WM_HOTKEY = 0x0312;

        private const int HOTKEY_ID_DOUBLE_SHIFT = 9000;
        private const int HOTKEY_ID_CTRL_SHIFT_I = 9001;

        // ダブルShift判定時間(ms)
        private const int DOUBLE_PRESS_MS = 300;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ini読み込み
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            var ini = IniHelper.ReadIni(iniPath);
            string rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";

            LauncherForm launcher = new LauncherForm(rootPath);
            launcher.Hide();

            MessageWindow messageWindow = new MessageWindow(DOUBLE_PRESS_MS);

            // Shift × 2
            RegisterHotKey(
                messageWindow.Handle,
                HOTKEY_ID_DOUBLE_SHIFT,
                MOD_SHIFT,
                (int)Keys.ShiftKey
            );

            // Ctrl + Shift + I
            RegisterHotKey(
                messageWindow.Handle,
                HOTKEY_ID_CTRL_SHIFT_I,
                MOD_CONTROL | MOD_SHIFT,
                (int)Keys.I
            );

            messageWindow.LauncherRequested += (s, e) =>
            {
                if (launcher.Visible)
                {
                    launcher.Hide();
                }
                else
                {
                    launcher.Show();
                    launcher.Activate();
                }
            };

            Application.Run();

            UnregisterHotKey(messageWindow.Handle, HOTKEY_ID_DOUBLE_SHIFT);
            UnregisterHotKey(messageWindow.Handle, HOTKEY_ID_CTRL_SHIFT_I);
        }

        /// <summary>
        /// ホットキー受信用 Invisible Window
        /// </summary>
        private class MessageWindow : NativeWindow
        {
            public event EventHandler LauncherRequested;

            private DateTime lastShiftTime = DateTime.MinValue;
            private readonly int thresholdMs;

            public MessageWindow(int thresholdMs)
            {
                this.thresholdMs = thresholdMs;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    int id = m.WParam.ToInt32();

                    // Ctrl + Shift + i → 即起動
                    if (id == HOTKEY_ID_CTRL_SHIFT_I)
                    {
                        LauncherRequested?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    // Shift × 2
                    if (id == HOTKEY_ID_DOUBLE_SHIFT)
                    {
                        var now = DateTime.Now;

                        if ((now - lastShiftTime).TotalMilliseconds <= thresholdMs)
                        {
                            LauncherRequested?.Invoke(this, EventArgs.Empty);
                            lastShiftTime = DateTime.MinValue;
                        }
                        else
                        {
                            lastShiftTime = now;
                        }
                    }
                }

                base.WndProc(ref m);
            }
        }
    }
}
