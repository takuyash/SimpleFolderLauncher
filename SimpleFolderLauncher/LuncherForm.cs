using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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

    // アイコン取得用のWin32 API定義
    public static class NativeMethods
    {
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_SMALLICON = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }

    /// <summary>
    /// ランチャー画面
    /// </summary>
    public class LauncherForm : Form
    {
        private TreeView fileTree;
        private ContextMenuStrip nodeContextMenu;
        private List<TreeNode> flatNodeList = new List<TreeNode>();
        private ImageList iconList; // アイコンリスト

        // タスクトレイ
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        /// <summary>
        /// ランチャーフォーム画面
        /// </summary>
        /// <param name="initialPath"></param>
        public LauncherForm(string initialPath = "")
        {
            this.Text = "SimpleFolderLauncher";
            this.Size = new Size(420, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;


            // ImageListの初期化
            iconList = new ImageList();
            iconList.ColorDepth = ColorDepth.Depth32Bit;
            iconList.ImageSize = new Size(16, 16); // アイコンサイズ

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


            // TreeView
            fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                DrawMode = TreeViewDrawMode.OwnerDrawText,
                HideSelection = false,
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.None,
                ImageList = iconList, // ImageListを紐づけする
                ShowLines = false,   
                ShowPlusMinus = true
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

            ReloadTree(initialPath);
        }

        /// <summary>
        /// Esc で LauncherForm を閉じる（Hide）
        /// </summary>
        /// <param name="e"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {

            if (keyData == Keys.Enter)
            {
                if (fileTree.SelectedNode != null)
                {
                    OpenFileOrFolder(fileTree.SelectedNode);
                }
                return true;
            }

            if (keyData == Keys.Escape)
            {
                this.Hide();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// リロード処理
        /// </summary>
        /// <param name="rootPath"></param>
        private void ReloadTree(string rootPath = "")
        {
            fileTree.Nodes.Clear();
            flatNodeList.Clear();
            iconList.Images.Clear(); // リロード時にアイコンキャッシュもクリア

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                var ini = IniHelper.ReadIni(iniPath);
                rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";
            }

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return;

            fileTree.BeginUpdate(); // 描画停止で高速化
            LoadFolder(rootPath, fileTree.Nodes);
            fileTree.EndUpdate();

            BuildFlatNodeList(fileTree.Nodes);

            if (fileTree.Nodes.Count > 0)
            {
                fileTree.SelectedNode = fileTree.Nodes[0];
                fileTree.Focus();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            // ディレクトリ
            foreach (var dir in Directory.GetDirectories(path))
            {
                // ファイル名を表示
                var folderNode = new TreeNode(Path.GetFileName(dir))
                {
                    Tag = dir,
                    ForeColor = Color.LightSkyBlue
                };

                // アイコンを設定する
                SetNodeIcon(folderNode, dir);

                parentNodes.Add(folderNode);
                LoadFolder(dir, folderNode.Nodes);
            }

            // ファイル
            foreach (var file in Directory.GetFiles(path))
            {

                var fileNode = new TreeNode(Path.GetFileName(file))
                {
                    Tag = file,
                    ForeColor = Color.FromArgb(224, 224, 224)
                };

                // アイコン設定
                SetNodeIcon(fileNode, file);

                parentNodes.Add(fileNode);
            }
        }

        /// <summary>
        /// アイコン設定
        /// </summary>
        /// <param name="node"></param>
        /// <param name="path"></param>
        private void SetNodeIcon(TreeNode node, string path)
        {
            NativeMethods.SHFILEINFO shinfo = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON;

            IntPtr hImg = NativeMethods.SHGetFileInfo(
                path,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                flags);

            if (hImg != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    using (Icon icon = Icon.FromHandle(shinfo.hIcon))
                    {

                        Bitmap bmp = icon.ToBitmap();

                        if (!iconList.Images.ContainsKey(path))
                        {
                            iconList.Images.Add(path, bmp);
                        }

                        node.ImageKey = path;
                        node.SelectedImageKey = path;
                    }
                }
                finally
                {
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                }
            }
        }

        /// <summary>
        /// FileTree_DrawNode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileTree_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {

            if (e.Node.IsSelected)
            {
                // 選択時の背景色描画
                e.Graphics.FillRectangle(Brushes.DarkCyan, e.Bounds);
            }

            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                e.Node.TreeView.Font,
                e.Bounds, // テキスト領域に描画
                Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        /// <summary>
        /// FileTree_NodeMouseDoubleClick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// ファイルかフォルダを開く
        /// </summary>
        /// <param name="node"></param>
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