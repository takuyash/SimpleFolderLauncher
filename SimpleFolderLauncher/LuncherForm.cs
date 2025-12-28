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

        // タスクトレイ
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

            // ★ 追加：フォームがキー入力を先に受け取る
            this.KeyPreview = true;

            // ================================
            // タスクトレイ
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
                ReloadTree();
            });

            trayMenu.Items.Add("終了", null, (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Launcher",
                ContextMenuStrip = trayMenu
            };

            trayIcon.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    trayMenu.Show(Cursor.Position);
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

            nodeContextMenu = new ContextMenuStrip();
            var copyPathItem = new ToolStripMenuItem("パスをコピー");
            copyPathItem.Click += CopyPathItem_Click;
            nodeContextMenu.Items.Add(copyPathItem);

            ReloadTree();
        }

        // Esc で LauncherForm を閉じる（Hide）
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Hide();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Enter / Esc の「つぅるーん」防止 & 全体ショートカット
            if (keyData == Keys.Enter)
            {
                if (fileTree.SelectedNode != null)
                {
                    OpenFileOrFolder(fileTree.SelectedNode);
                }
                return true; // ★ Windowsに渡さない（音が鳴らない）
            }

            if (keyData == Keys.Escape)
            {
                this.Hide();
                return true; // ★ 音防止
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }


        private void ReloadTree()
        {
            fileTree.Nodes.Clear();
            flatNodeList.Clear();

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            var ini = IniHelper.ReadIni(iniPath);
            string rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return;

            LoadFolder(rootPath, fileTree.Nodes);
            BuildFlatNodeList(fileTree.Nodes);

            if (fileTree.Nodes.Count > 0)
            {
                fileTree.SelectedNode = fileTree.Nodes[0];
                fileTree.Focus();
            }
        }

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
                    if (index < 10) keyLabel = $"{index}: ";
                    else if (index < 36) keyLabel = $"{(char)('A' + index - 10)}: ";
                    else keyLabel = "   ";

                    node.Text = keyLabel + node.Text;
                    flatNodeList.Add(node);
                    index++;
                }

                if (node.Nodes.Count > 0)
                    index = AddNodesToFlatList(node.Nodes, index);
            }
            return index;
        }

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

            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                e.Node.TreeView.Font,
                e.Bounds,
                Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void FileTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            OpenFileOrFolder(e.Node);
        }

        private void FileTree_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
            {
                int index = e.KeyCode - Keys.D0;
                if (index < flatNodeList.Count)
                    OpenFileOrFolder(flatNodeList[index]);
                e.Handled = true;
                return;
            }

            if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
            {
                int index = 10 + (e.KeyCode - Keys.A);
                if (index < flatNodeList.Count)
                    OpenFileOrFolder(flatNodeList[index]);
                e.Handled = true;
                return;
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
