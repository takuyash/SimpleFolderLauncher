using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StylishLauncherINI
{
    public static class IniHelper
    {
        public static Dictionary<string, string> ReadIni(string path)
        {
            var result = new Dictionary<string, string>();
            if (!File.Exists(path)) return result;

            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.StartsWith(";") || trimmed.StartsWith("[")) continue;

                var kv = trimmed.Split(new char[] { '=' }, 2);
                if (kv.Length == 2)
                    result[kv[0].Trim()] = kv[1].Trim();
            }
            return result;
        }
    }

    public class LauncherForm : Form
    {
        private TreeView fileTree;
        private ContextMenuStrip nodeContextMenu;
        private List<TreeNode> flatNodeList = new List<TreeNode>();

        // ★ タスクトレイアイコン
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        public LauncherForm()
        {
            this.Text = "SimpleFolderLauncher";
            this.Size = new Size(420, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // ================================  
            // タスクトレイアイコン  
            // ================================
            trayMenu = new ContextMenuStrip();

            trayMenu.Items.Add("開く", null, (s, e) =>
            {
                this.Show();
                this.Activate();
            });

            trayMenu.Items.Add("設定", null, (s, e) =>
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                var settings = new SettingsForm(iniPath);
                settings.ShowDialog();

                // 設定変更後に再読み込み
                ReloadTree();
            });

            trayMenu.Items.Add("終了", null, (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });

            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Visible = true;
            trayIcon.Text = "Launcher";
            trayIcon.ContextMenuStrip = trayMenu;

            trayIcon.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    trayMenu.Show(Cursor.Position);
                }
            };

            // ================================
            // TreeView
            // ================================
            fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                DrawMode = TreeViewDrawMode.OwnerDrawText,
                HideSelection = false,
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.None
            };

            fileTree.DrawNode += FileTree_DrawNode;
            fileTree.NodeMouseDoubleClick += FileTree_NodeMouseDoubleClick;
            fileTree.KeyDown += FileTree_KeyDown;
            fileTree.NodeMouseClick += FileTree_NodeMouseClick;

            this.Controls.Add(fileTree);

            // 右クリックメニュー
            nodeContextMenu = new ContextMenuStrip();
            var copyPathItem = new ToolStripMenuItem("パスをコピー");
            copyPathItem.Click += CopyPathItem_Click;
            nodeContextMenu.Items.Add(copyPathItem);

            // 初回ロード
            ReloadTree();
        }

        // ================================
        // INI を読み込み、ツリーを再構築
        // ================================
        private void ReloadTree()
        {
            fileTree.Nodes.Clear();
            flatNodeList.Clear();

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            var ini = IniHelper.ReadIni(iniPath);
            string rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";

            // --- ★ 修正ポイント：INI がない・空・フォルダなしでも落とさない ---
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                // 何も読み込まない（空表示）
                return;
            }

            // 正常時のみ読み込み
            LoadFolder(rootPath, fileTree.Nodes);
            BuildFlatNodeList(fileTree.Nodes);

            if (fileTree.Nodes.Count > 0)
            {
                fileTree.SelectedNode = fileTree.Nodes[0];
                fileTree.Focus();
            }
        }

        // × で閉じても終了せずタスクトレイへ
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        // ================================
        // 0〜9、A〜Z ショートカット
        // ================================
        private void BuildFlatNodeList(TreeNodeCollection nodes)
        {
            flatNodeList.Clear();
            AddNodesToFlatList(nodes, 0);
        }

        private int AddNodesToFlatList(TreeNodeCollection nodes, int index)
        {
            foreach (TreeNode node in nodes)
            {
                if (File.Exists(node.Tag as string) || Directory.Exists(node.Tag as string))
                {
                    string keyLabel;

                    if (index < 10)
                        keyLabel = $"{index}: ";
                    else if (index < 36)
                        keyLabel = $"{(char)('A' + index - 10)}: ";
                    else
                        keyLabel = "   ";

                    node.Text = keyLabel + node.Text;
                    flatNodeList.Add(node);
                    index++;
                }

                if (node.Nodes.Count > 0)
                    index = AddNodesToFlatList(node.Nodes, index);
            }
            return index;
        }

        // ================================
        // TreeView 操作
        // ================================
        private void FileTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                fileTree.SelectedNode = e.Node;
                nodeContextMenu.Show(fileTree, e.Location);
            }
        }

        private void CopyPathItem_Click(object sender, EventArgs e)
        {
            if (fileTree.SelectedNode?.Tag != null)
                Clipboard.SetText(fileTree.SelectedNode.Tag.ToString());
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush =
                new System.Drawing.Drawing2D.LinearGradientBrush(
                    this.ClientRectangle,
                    Color.FromArgb(30, 30, 30),
                    Color.FromArgb(45, 45, 60),
                    90f))
            {
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
            }
        }

        private void LoadFolder(string path, TreeNodeCollection parentNodes)
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                var folderNode = new TreeNode($"▶ {Path.GetFileName(dir)}")
                {
                    Tag = dir,
                    ForeColor = Color.LightSkyBlue
                };
                parentNodes.Add(folderNode);
                LoadFolder(dir, folderNode.Nodes);
            }

            foreach (var file in Directory.GetFiles(path))
            {
                var fileNode = new TreeNode($"📄 {Path.GetFileName(file)}")
                {
                    Tag = file,
                    ForeColor = Color.FromArgb(224, 224, 224)
                };
                parentNodes.Add(fileNode);
            }
        }

        private void FileTree_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node.IsSelected)
                e.Graphics.FillRectangle(Brushes.DarkCyan, e.Bounds);

            TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.TreeView.Font,
                e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void FileTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            OpenFileOrFolder(e.Node);
        }

        private void FileTree_KeyDown(object sender, KeyEventArgs e)
        {
            // 0〜9
            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
            {
                int index = e.KeyCode - Keys.D0;
                if (index < flatNodeList.Count)
                    OpenFileOrFolder(flatNodeList[index]);
                e.Handled = true;
                return;
            }

            // A〜Z
            if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
            {
                int index = 10 + (e.KeyCode - Keys.A);
                if (index < flatNodeList.Count)
                    OpenFileOrFolder(flatNodeList[index]);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter && fileTree.SelectedNode != null)
            {
                OpenFileOrFolder(fileTree.SelectedNode);
                e.Handled = true;
            }
        }

        private void OpenFileOrFolder(TreeNode node)
        {
            string path = node.Tag as string;

            if (File.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"実行に失敗しました: {ex.Message}");
                }
            }
            else if (Directory.Exists(path))
            {
                node.Toggle();

                if (node.Text.StartsWith("▶"))
                    node.Text = node.Text.Replace("▶", "▼");
                else if (node.Text.StartsWith("▼"))
                    node.Text = node.Text.Replace("▼", "▶");
            }
        }
    }

    public static class TreeNodeExtensions
    {
        public static void Toggle(this TreeNode node)
        {
            if (node.IsExpanded) node.Collapse();
            else node.Expand();
        }
    }
}
