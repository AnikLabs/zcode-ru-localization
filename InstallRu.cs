// Русификатор ZCode Desktop — установщик с выбором папки и прогресс-баром.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZCodeRuInstaller
{
    public class MainForm : Form
    {
        TextBox pathBox;
        Button browse, install, rollback;
        TextBox log;
        ProgressBar progress;
        Label stage;

        public MainForm()
        {
            Text = "Русификатор ZCode Desktop";
            Width = 680; Height = 500;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;

            var lbl = new Label { Text = "Папка с ZCode:", Left = 12, Top = 14, Width = 100 };
            pathBox = new TextBox { Left = 110, Top = 11, Width = 430 };
            browse = new Button { Text = "Обзор...", Left = 550, Top = 9, Width = 100 };
            install = new Button { Text = "Установить русский язык", Left = 12, Top = 45, Width = 300, Height = 32 };
            rollback = new Button { Text = "Откатить (вернуть как было)", Left = 322, Top = 45, Width = 300, Height = 32 };
            progress = new ProgressBar { Left = 12, Top = 84, Width = 638, Height = 20, Minimum = 0, Maximum = 100, Value = 0 };
            stage = new Label { Text = "", Left = 14, Top = 106, Width = 500, ForeColor = System.Drawing.Color.DimGray };
            log = new TextBox { Left = 12, Top = 130, Width = 638, Height = 300, Multiline = true,
                ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new System.Drawing.Font("Consolas", 9) };

            browse.Click += (s, e) => Browse();
            install.Click += (s, e) => Install();
            rollback.Click += (s, e) => Rollback();

            Controls.Add(lbl); Controls.Add(pathBox); Controls.Add(browse);
            Controls.Add(install); Controls.Add(rollback); Controls.Add(progress);
            Controls.Add(stage); Controls.Add(log);

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

        void SetStage(int percent, string text)
        {
            progress.Value = Math.Max(progress.Minimum, Math.Min(progress.Maximum, percent));
            stage.Text = text;
            Application.DoEvents();
        }

        int RunLive(string file, string args)
        {
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
                p.OutputDataReceived += (s, e) => { if (e.Data != null) LogLine("  " + e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) LogLine("  " + e.Data); };
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                while (!p.HasExited)
                {
                    p.WaitForExit(200);
                    Application.DoEvents();
                }
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

        async void Install()
        {
            install.Enabled = false; rollback.Enabled = false; browse.Enabled = false;
            try { await DoInstall(); }
            catch (Exception ex) { LogLine("ОШИБКА: " + ex.Message); MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            install.Enabled = true; rollback.Enabled = true; browse.Enabled = true;
        }

        async Task DoInstall()
        {
            log.Clear(); progress.Value = 0; stage.Text = "";
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
            SetStage(5, "Патчинг файлов (1-2 минуты)...");
            progress.Style = ProgressBarStyle.Marquee; progress.MarqueeAnimationSpeed = 30;
            LogLine("Патчу файлы...");
            int code = RunLive(node, "\"" + patch + "\" \"" + asar + "\" \"" + temp + "\"");
            progress.Style = ProgressBarStyle.Continuous;
            if (code != 0) { SetStage(0, ""); LogLine("Патч не удался — программа НЕ изменена."); return; }

            string backup = Path.Combine(res, "app.asar.bak");
            if (!File.Exists(backup)) { File.Copy(asar, backup); LogLine("Бэкап: app.asar.bak"); }
            SetStage(70, "Копирование файлов...");

            await Task.Run(() => CopyDirectory(temp, appDir, 70, 95));
            try { Directory.Delete(temp, true); } catch { }

            SetStage(96, "Сохранение оригинала...");
            string orig = Path.Combine(res, "app.asar.orig");
            if (File.Exists(asar)) File.Move(asar, orig);
            LogLine("Оригинал сохранён: app.asar.orig");

            SetStage(98, "Включение русского языка...");
            SetLocaleRu();

            SetStage(100, "Готово!");
            LogLine("");
            LogLine("ГОТОВО! Запустите ZCode — интерфейс будет на русском.");
            MessageBox.Show("Установка завершена!\n\nЗапустите ZCode — интерфейс будет на русском.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void CopyDirectory(string src, string dst, int fromPct, int toPct)
        {
            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
            long total = 0, done = 0;
            foreach (var f in files) total += new FileInfo(f).Length;
            int lastPct = fromPct;
            foreach (var f in files)
            {
                string rel = f.Substring(src.Length + 1);
                string target = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(f, target, true);
                done += new FileInfo(f).Length;
                int pct = fromPct + (int)((double)done / total * (toPct - fromPct));
                if (pct != lastPct) { lastPct = pct; SetStage(pct, "Копирование файлов: " + pct + "%"); }
            }
        }

        string FindNode()
        {
            var psi = new ProcessStartInfo("where", "node.exe") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (var p = Process.Start(psi))
            {
                string outp = p.StandardOutput.ReadToEnd(); p.WaitForExit();
                foreach (var line in outp.Split('\n')) { var t = line.Trim(); if (File.Exists(t)) return t; }
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
            log.Clear(); progress.Value = 0; stage.Text = "";
            try
            {
                string dir = pathBox.Text.Trim();
                string res = Path.Combine(dir, "resources");
                string asar = Path.Combine(res, "app.asar");
                string orig = Path.Combine(res, "app.asar.orig");
                string appDir = Path.Combine(res, "app");

                if (ZCodeRunning(dir)) { LogLine("Сначала ЗАКРОЙТЕ ZCode."); return; }
                if (!File.Exists(orig) && !Directory.Exists(appDir)) { LogLine("Откатывать нечего — русификация не установлена."); return; }
                SetStage(30, "Удаление русской папки...");
                if (Directory.Exists(appDir)) { Directory.Delete(appDir, true); LogLine("Удалена папка resources\\app"); }
                SetStage(70, "Восстановление оригинала...");
                if (File.Exists(orig))
                {
                    if (File.Exists(asar)) File.Delete(asar);
                    File.Move(orig, asar);
                    LogLine("Восстановлен оригинальный app.asar");
                }
                SetStage(100, "Готово!");
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
