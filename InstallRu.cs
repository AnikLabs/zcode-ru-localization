// Русификатор ZCode Desktop — простой установщик с выбором папки.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ZCodeRuInstaller
{
    public class MainForm : Form
    {
        TextBox pathBox;
        Button browse, install, rollback;
        TextBox log;

        public MainForm()
        {
            Text = "Русификатор ZCode Desktop";
            Width = 680; Height = 460;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;

            var lbl = new Label { Text = "Папка с ZCode:", Left = 12, Top = 14, Width = 100 };
            pathBox = new TextBox { Left = 110, Top = 11, Width = 430 };
            browse = new Button { Text = "Обзор...", Left = 550, Top = 9, Width = 100 };
            install = new Button { Text = "Установить русский язык", Left = 12, Top = 45, Width = 300, Height = 32 };
            rollback = new Button { Text = "Откатить (вернуть как было)", Left = 322, Top = 45, Width = 300, Height = 32 };
            log = new TextBox { Left = 12, Top = 90, Width = 638, Height = 310, Multiline = true,
                ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new System.Drawing.Font("Consolas", 9) };

            browse.Click += (s, e) => Browse();
            install.Click += (s, e) => Install();
            rollback.Click += (s, e) => Rollback();

            Controls.Add(lbl); Controls.Add(pathBox); Controls.Add(browse);
            Controls.Add(install); Controls.Add(rollback); Controls.Add(log);

            // автопоиск
            string found = FindZCode();
            if (found != null) pathBox.Text = found;
        }

        string FindZCode()
        {
            foreach (var p in Process.GetProcessesByName("ZCode"))
            {
                try { if (!string.IsNullOrEmpty(p.MainModule.FileName)) return Path.GetDirectoryName(p.MainModule.FileName); } catch { }
            }
            string[] candidates = { @"H:\__ZCode", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "zcode") };
            foreach (var c in candidates) if (File.Exists(Path.Combine(c, "ZCode.exe"))) return c;
            return null;
        }

        void LogLine(string s)
        {
            log.AppendText(s + Environment.NewLine);
            Application.DoEvents();
        }

        int Run(string file, string args, out string output)
        {
            var sb = new StringBuilder();
            output = "";
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            using (var p = Process.Start(psi))
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) { sb.AppendLine(e.Data); LogLine("  " + e.Data); } };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) { sb.AppendLine(e.Data); LogLine("  " + e.Data); } };
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        void Browse()
        {
            using (var d = new FolderBrowserDialog { Description = "Выберите папку с ZCode (там лежит ZCode.exe)" })
            {
                if (Directory.Exists(pathBox.Text)) d.SelectedPath = pathBox.Text;
                if (d.ShowDialog() == DialogResult.OK)
                {
                    if (!File.Exists(Path.Combine(d.SelectedPath, "ZCode.exe")))
                        MessageBox.Show("В выбранной папке нет ZCode.exe", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else pathBox.Text = d.SelectedPath;
                }
            }
        }

        bool ZCodeRunning(string dir)
        {
            dir = Path.GetFullPath(dir).TrimEnd('\\').ToLower();
            foreach (var p in Process.GetProcessesByName("ZCode"))
            {
                try { if (Path.GetFullPath(p.MainModule.FileName).TrimEnd('\\').ToLower().StartsWith(dir)) return true; } catch { }
            }
            return false;
        }

        void Install()
        {
            install.Enabled = false; rollback.Enabled = false;
            try { DoInstall(); }
            catch (Exception ex) { LogLine("ОШИБКА: " + ex.Message); MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            install.Enabled = true; rollback.Enabled = true;
        }

        void DoInstall()
        {
            log.Clear();
            string dir = pathBox.Text.Trim();
            string res = Path.Combine(dir, "resources");
            string asar = Path.Combine(res, "app.asar");
            string appDir = Path.Combine(res, "app");

            if (!File.Exists(Path.Combine(dir, "ZCode.exe"))) { LogLine("В этой папке нет ZCode.exe."); return; }
            if (ZCodeRunning(dir)) { LogLine("Сначала ЗАКРОЙТЕ ZCode (включая значок в трее) и повторите."); return; }
            if (!File.Exists(asar)) { LogLine(Directory.Exists(appDir) ? "Русская локализация уже установлена." : "app.asar не найден — неожиданная структура папки."); return; }

            string node = FindNode();
            if (node == null) { LogLine("Не найден Node.js. Установите Node или добавьте в PATH."); return; }
            LogLine("Node.js: " + node);

            string scriptDir = Application.StartupPath;
            string patch = Path.Combine(scriptDir, "patch.mjs");
            if (!File.Exists(patch)) { LogLine("Рядом с программой нет patch.mjs (" + scriptDir + ")"); return; }

            string temp = Path.Combine(Path.GetTempPath(), "zcode-ru-patch");
            LogLine("Патчу файлы (1-2 минуты)...");
            string tmpOut; int code = Run(node, "\"" + patch + "\" \"" + asar + "\" \"" + temp + "\"", out tmpOut);
            if (code != 0) { LogLine("Патч не удался — программа НЕ изменена."); return; }

            string backup = Path.Combine(res, "app.asar.bak");
            if (!File.Exists(backup)) { File.Copy(asar, backup); LogLine("Бэкап: app.asar.bak"); }

            LogLine("Копирую файлы...");
            code = Run("robocopy", "\"" + temp + "\" \"" + appDir + "\" /E /NFL /NDL /NJH /NJS", out tmpOut);
            try { Directory.Delete(temp, true); } catch { }
            if (code >= 8) { LogLine("Ошибка копирования (robocopy " + code + ")."); return; }

            string orig = Path.Combine(res, "app.asar.orig");
            if (File.Exists(asar)) File.Move(asar, orig);
            LogLine("Оригинал сохранён: app.asar.orig");

            SetLocaleRu();
            LogLine("");
            LogLine("ГОТОВО! Запустите ZCode — интерфейс будет на русском.");
            MessageBox.Show("Установка завершена!\n\nЗапустите ZCode — интерфейс будет на русском.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        string FindNode()
        {
            foreach (var name in new[] { "node.exe" })
            {
                var psi = new ProcessStartInfo("where", name) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd(); p.WaitForExit();
                    foreach (var line in outp.Split('\n')) { var t = line.Trim(); if (File.Exists(t)) return t; }
                }
            }
            return null;
        }

        void SetLocaleRu()
        {
            try
            {
                string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zcode", "v2", "setting.json");
                if (!File.Exists(settings)) { LogLine("setting.json не найден — выберите язык в настройках ZCode вручную."); return; }
                string s = File.ReadAllText(settings, Encoding.UTF8);
                s = Regex.Replace(s, "(\"locale\"\\s*:\\s*\")[^\"]*(\")", "$1ru-RU$2");
                s = Regex.Replace(s, "(\"localePreference\"\\s*:\\s*\")[^\"]*(\")", "$1ru-RU$2");
                File.WriteAllText(settings, s, new UTF8Encoding(false));
                LogLine("Язык в настройках: Русский");
            }
            catch (Exception ex) { LogLine("Не удалось прописать язык: " + ex.Message); }
        }

        void Rollback()
        {
            log.Clear();
            try
            {
                string dir = pathBox.Text.Trim();
                string res = Path.Combine(dir, "resources");
                string asar = Path.Combine(res, "app.asar");
                string orig = Path.Combine(res, "app.asar.orig");
                string appDir = Path.Combine(res, "app");

                if (ZCodeRunning(dir)) { LogLine("Сначала ЗАКРОЙТЕ ZCode."); return; }
                if (!File.Exists(orig) && !Directory.Exists(appDir)) { LogLine("Откатывать нечего — русификация не установлена."); return; }
                if (Directory.Exists(appDir)) { Directory.Delete(appDir, true); LogLine("Удалена папка resources\\app"); }
                if (File.Exists(orig))
                {
                    if (File.Exists(asar)) File.Delete(asar);
                    File.Move(orig, asar);
                    LogLine("Восстановлен оригинальный app.asar");
                }
                LogLine("Готово — программа в исходном виде (английский язык).");
            }
            catch (Exception ex) { LogLine("ОШИБКА: " + ex.Message); }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
