using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StylishLauncherINI
{
    static class Program
    {
        // modifier keys
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LauncherForm launcher = new LauncherForm();
            launcher.Hide(); // 初期は非表示

            MessageWindow messageWindow = new MessageWindow();

            // ホットキー起動
            // Ctrl + Shift + I
            RegisterHotKey(messageWindow.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, (int)Keys.I);

            messageWindow.HotKeyPressed += (s, e) =>
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

            UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        }

        // ホットキー受信用 Invisible Window
        private class MessageWindow : NativeWindow
        {
            public event EventHandler HotKeyPressed;

            public MessageWindow()
            {
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    HotKeyPressed?.Invoke(this, EventArgs.Empty);
                }
                base.WndProc(ref m);
            }
        }
    }
}
