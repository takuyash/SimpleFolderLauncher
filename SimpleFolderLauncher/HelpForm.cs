using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace StylishLauncherINI
{
    public class HelpForm : Form
    {

        private const string GitHubRepoUrl = "https://github.com/takuyash/SimpleFolderLauncher";
        private const string HelpUrl = "https://takuyash.github.io/SimplefolderlauncherSite/docs.html";
        private const string LicenseUrl = "https://github.com/takuyash/SimpleFolderLauncher/blob/main/LICENSE";

        public HelpForm()
        {
            this.Text = "ヘルプ / バージョン情報";
            this.Size = new Size(420, 260);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var panel = new FlowLayoutPanel()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(15),
                AutoScroll = true
            };
            this.Controls.Add(panel);

            // アプリ名
            panel.Controls.Add(new Label()
            {
                Text = "SimpleFolderLauncher",
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
                AutoSize = true
            });

            // バージョン
            panel.Controls.Add(new Label()
            {
                Text = $"Version: {GetVersion()}",
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 15)
            });

            panel.Controls.Add(CreateLink("GitHub リポジトリ", GitHubRepoUrl));
            panel.Controls.Add(CreateLink("ヘルプ / 使い方", HelpUrl));
            panel.Controls.Add(CreateLink("ライセンス", LicenseUrl));
        }

        private string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        }

        private Control CreateLink(string text, string url)
        {
            var link = new LinkLabel()
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 5)
            };

            link.LinkClicked += (s, e) =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            };

            return link;
        }
    }
}
