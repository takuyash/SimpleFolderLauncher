using System;
using System.IO;
using System.Windows.Forms;

namespace StylishLauncherINI
{
    public class SettingsForm : Form
    {
        private TextBox txtPath;
        private Button btnSave;

        private string iniPath;

        public SettingsForm(string iniFilePath)
        {
            iniPath = iniFilePath;

            this.Text = "設定";
            this.Size = new System.Drawing.Size(420, 150);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var lbl = new Label()
            {
                Text = "LauncherFolder のパス:",
                Left = 10,
                Top = 10,
                Width = 380
            };
            this.Controls.Add(lbl);

            txtPath = new TextBox()
            {
                Left = 10,
                Top = 35,
                Width = 380
            };
            this.Controls.Add(txtPath);

            btnSave = new Button()
            {
                Text = "保存",
                Left = 300,
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

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string folder = txtPath.Text.Trim();

            if (string.IsNullOrEmpty(folder))
            {
                MessageBox.Show("パスを入力してください。");
                return;
            }

            try
            {
                // INIを書き込み（上書き or 作成）
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
