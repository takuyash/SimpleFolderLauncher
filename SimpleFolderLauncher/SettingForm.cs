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

        private string iniPath;

        public SettingsForm(string iniFilePath)
        {
            iniPath = iniFilePath;

            this.Text = "設定";
            this.Size = new System.Drawing.Size(450, 150);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var lbl = new Label()
            {
                Text = "LauncherFolder のパス:",
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

            btnSave = new Button()
            {
                Text = "保存",
                Left = 330,
                Top = 70,
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
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Launcher フォルダを選択してください";

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

            if (string.IsNullOrEmpty(folder))
            {
                MessageBox.Show("パスを入力してください。");
                return;
            }

            if (!Directory.Exists(folder))
            {
                MessageBox.Show("指定されたフォルダは存在しません。");
                return;
            }

            try
            {
                File.WriteAllText(iniPath, $"LauncherFolder={folder}");
                MessageBox.Show("保存しました。");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存に失敗しました: " + ex.Message);
            }
        }
    }
}
