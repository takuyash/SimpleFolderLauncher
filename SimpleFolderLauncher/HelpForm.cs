using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace StylishLauncherINI
{
    public class HelpForm : Form
    {

        private const string GitHubRepoUrl = "https://github.com/takuyash/SimpleFolderLauncher";
        private const string HelpUrl = "https://takuyash.github.io/SimplefolderlauncherSite/docs.html";
        private const string LicenseUrl = "https://github.com/takuyash/SimpleFolderLauncher/blob/main/LICENSE";
        private Label _updateLabel;

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

            _updateLabel = new Label()
            {
                AutoSize = true,
                ForeColor = Color.DarkRed,
                Margin = new Padding(0, 15, 0, 0),
                Visible = false
            };

            panel.Controls.Add(_updateLabel);

            // 非同期でチェック
            this.Load += async (s, e) => await CheckForUpdateAsync(panel);
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

        private async Task CheckForUpdateAsync(Control parent)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SimpleFolderLauncher");

                var json = await client.GetStringAsync(
                    "https://api.github.com/repos/takuyash/SimpleFolderLauncher/releases/latest");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var tag = root.GetProperty("tag_name").GetString(); // v1.2.3
                var url = root.GetProperty("html_url").GetString();

                if (string.IsNullOrEmpty(tag)) return;

                var latest = new Version(tag.TrimStart('v', 'V'));
                var current = Assembly.GetExecutingAssembly().GetName().Version;

                if (current != null && latest > current)
                {
                    var link = new LinkLabel()
                    {
                        Text = $"新しいバージョンがあります（v{latest}）",
                        AutoSize = true,
                        LinkColor = Color.DarkRed
                    };

                    link.LinkClicked += (s, e) =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    };

                    parent.Controls.Add(link);
                }
            }
            catch
            {
                // 失敗しても何もしない
            }
        }

    }
}
