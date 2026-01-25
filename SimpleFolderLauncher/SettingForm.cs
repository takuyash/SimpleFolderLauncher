using System;
using System.IO;
using System.Windows.Forms;

namespace StylishLauncherINI
{
    /// <summary>
    /// 設定画面
    /// </summary>
    public class SettingsForm : Form
    {
        private TextBox txtPath;
        private Button btnBrowse;
        private Button btnSave;
        private NumericUpDown numFontSize;

        private string iniPath;

        public SettingsForm(string iniFilePath)
        {
            iniPath = iniFilePath;

            this.Text = "設定";
            this.Size = new System.Drawing.Size(450, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var lbl = new Label()
            {
                Text = "フォルダのパス:",
                Left = 10,
                Top = 10,
                Width = 400
            };
            this.Controls.Add(lbl);

            txtPath = new TextBox()
            {
                Left = 10,
                Top = 35,
                Width = 320
            };
            this.Controls.Add(txtPath);

            btnBrowse = new Button()
            {
                Text = "参照...",
                Left = 340,
                Top = 33,
                Width = 80
            };
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);

            var lblFont = new Label()
            {
                Text = "文字サイズ:",
                Left = 10,
                Top = 75,
                Width = 80
            };
            this.Controls.Add(lblFont);

            numFontSize = new NumericUpDown()
            {
                Left = 90,
                Top = 73,
                Width = 60,
                Minimum = 8,
                Maximum = 20,
                Value = 10
            };
            this.Controls.Add(numFontSize);

            btnSave = new Button()
            {
                Text = "保存",
                Left = 330,
                Top = 110,
                Width = 90
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            // 既存設定読み込み
            if (File.Exists(iniPath))
            {
                var ini = IniHelper.ReadIni(iniPath);
                if (ini.ContainsKey("LauncherFolder"))
                {
                    txtPath.Text = ini["LauncherFolder"];
                }
                if (ini.ContainsKey("FontSize") && decimal.TryParse(ini["FontSize"], out decimal fs))
                {
                    numFontSize.Value = Math.Max(numFontSize.Minimum, Math.Min(numFontSize.Maximum, fs));
                }
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "フォルダを選択してください";

                // 既に入力されている場合は初期フォルダに設定
                if (Directory.Exists(txtPath.Text))
                {
                    dialog.SelectedPath = txtPath.Text;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string folder = txtPath.Text.Trim();

            // ① 空チェック
            if (string.IsNullOrEmpty(folder))
            {
                MessageBox.Show("パスを入力してください。");
                return;
            }

            // ② 存在チェック
            if (!Directory.Exists(folder))
            {
                MessageBox.Show("指定されたフォルダは存在しません。");
                return;
            }

            // ③ ドライブ直下・システムフォルダチェック
            if (IsInvalidRootFolder(folder))
            {
                MessageBox.Show(
                    "ドライブ直下やシステムフォルダは登録できません。\n" +
                    "サブフォルダを指定してください。");
                return;
            }

            // ④ アクセス権チェック
            if (!CanAccessFolder(folder))
            {
                MessageBox.Show(
                    "このフォルダにはアクセス権がありません。\n" +
                    "別のフォルダを指定してください。");
                return;
            }

            // ⑤ フォルダ＋ファイル総数チェック
            const int MAX_ITEMS = 1000;
            const int WARNING_ITEMS = 500;

            if (!TryCountItems(folder, MAX_ITEMS, out int count))
            {
                MessageBox.Show(
                    $"フォルダ内の項目数が多すぎます。\n\n" +
                    $"上限 : {MAX_ITEMS}\n" +
                    $"検出 : {count} 以上\n\n" +
                    $"より小さなフォルダを指定してください。");
                return;
            }

            // 警告ライン
            if (count > WARNING_ITEMS)
            {
                var result = MessageBox.Show(
                    $"このフォルダには {count} 個の項目があります。\n" +
                    $"動作が重くなる可能性があります。\n\n" +
                    $"それでも登録しますか？",
                    "確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;
            }

            // ⑥ 保存
            try
            {
                File.WriteAllText(
                    iniPath,
                    $"LauncherFolder={folder}\nFontSize={numFontSize.Value}");

                MessageBox.Show("保存しました。");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存に失敗しました: " + ex.Message);
            }
        }

        private bool IsInvalidRootFolder(string path)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd('\\');
            string root = Path.GetPathRoot(fullPath).TrimEnd('\\');

            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                || IsProtectedFolder(path);
        }

        private bool CanAccessFolder(string path)
        {
            try
            {
                Directory.GetDirectories(path);
                Directory.GetFiles(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsReparsePoint(string path)
        {
            try
            {
                var attr = File.GetAttributes(path);
                return (attr & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return true; // 属性取得できない = 危険
            }
        }

        private bool IsProtectedFolder(string path)
        {
            string name = Path.GetFileName(path.TrimEnd('\\'));


            return name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Windows", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryCountItems(string rootPath, int maxCount, out int totalCount)
        {
            totalCount = 0;
            Stack<string> stack = new();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                // 再解析ポイント（ジャンクション・シンボリックリンク）は無視
                if (IsReparsePoint(current))
                    continue;

                // フォルダ自身
                totalCount++;
                if (totalCount > maxCount)
                    return false;

                string[] dirs;
                try
                {
                    dirs = Directory.GetDirectories(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var dir in dirs)
                {
                    if (IsProtectedFolder(dir))
                        continue;

                    stack.Push(dir);
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                totalCount += files.Length;
                if (totalCount > maxCount)
                    return false;
            }

            return true;
        }
    }
}