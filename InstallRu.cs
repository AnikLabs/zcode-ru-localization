// Русификатор ZCode Desktop — установщик на ReaLTaiizor (тёмная Material-тема)
// + AutoUpdater.NET (сверка с GitHub Releases через update.xml, SHA-256-контроль).
// Библиотеки: vendor/ui/ReaLTaiizor.dll, vendor/ui/lib/net462/AutoUpdater.NET.dll,
// Microsoft.Web.WebView2 (Core/WinForms/WebView2Loader) — рядом с exe.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReaLTaiizor.Colors;
using ReaLTaiizor.Controls;
using ReaLTaiizor.Forms;
using ReaLTaiizor.Manager;
using AutoUpdaterDotNET;

[assembly: AssemblyVersion("1.0.8")]
[assembly: AssemblyFileVersion("1.0.8")]

namespace ZCodeRuInstaller
{
    public class MainForm : MaterialForm
    {
        const string InstallerVersion = "1.0.8";
        // ПРОДАКШН-фид: update.xml — ассет каждого релиза; URL всегда указывает на последний релиз.
        const string UpdateFeed = "https://github.com/AnikLabs/zcode-ru-localization/releases/latest/download/update.xml";

        MaterialSingleTextBox pathBox;
        MaterialButton browse, install, rollback;
        TextBox log;
        MaterialProgressBar progress;
        MaterialLabel stage;
        Label lblVersion;
        static bool autoInstall;

        public MainForm()
        {
            Text = "Русификатор ZCode Desktop";
            Size = new System.Drawing.Size(760, 660);
            Sizable = false;
            StartPosition = FormStartPosition.CenterScreen;

            // --- тёмная Material-тема (ReaLTaiizor) ---
            var skin = MaterialSkinManager.Instance;
            skin.AddFormToManage(this);
            skin.Theme = MaterialSkinManager.Themes.DARK;
            skin.ColorScheme = new MaterialColorScheme(
                System.Drawing.Color.FromArgb(55, 71, 79),     // dark
                System.Drawing.Color.FromArgb(69, 90, 100),    // primary (BlueGrey 800)
                System.Drawing.Color.FromArgb(84, 110, 122),   // light
                System.Drawing.Color.FromArgb(130, 177, 255),  // accent
                System.Drawing.Color.FromArgb(255, 255, 255)); // text

            lblVersion = new MaterialLabel
            {
                Text = "Версия русификатора: " + InstallerVersion,
                Location = new System.Drawing.Point(16, 70),
                AutoSize = true
            };

            // --- установка: «Директория» слева, путь справа ---
            browse = new MaterialButton
            {
                Text = "Директория",
                Location = new System.Drawing.Point(16, 106),
                Size = new System.Drawing.Size(140, 36)
            };
            pathBox = new MaterialSingleTextBox
            {
                Location = new System.Drawing.Point(164, 106),
                Size = new System.Drawing.Size(564, 36),
                Hint = "Папка с ZCode.exe (определена автоматически)"
            };
            install = new MaterialButton
            {
                Text = "Установить русский язык",
                Location = new System.Drawing.Point(16, 152),
                Size = new System.Drawing.Size(348, 38)
            };
            rollback = new MaterialButton
            {
                Text = "Откатить (вернуть как было)",
                Location = new System.Drawing.Point(372, 152),
                Size = new System.Drawing.Size(356, 38)
            };
            progress = new MaterialProgressBar
            {
                Location = new System.Drawing.Point(16, 202),
                Size = new System.Drawing.Size(712, 10)
            };
            stage = new MaterialLabel
            {
                Text = "",
                Location = new System.Drawing.Point(16, 222),
                AutoSize = true
            };
            log = new TextBox
            {
                Location = new System.Drawing.Point(16, 250),
                Size = new System.Drawing.Size(712, 340),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = System.Drawing.Color.FromArgb(48, 48, 48),
                ForeColor = System.Drawing.Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new System.Drawing.Font("Consolas", 9)
            };

            browse.Click += (s, e) => Browse();
            install.Click += (s, e) => Install();
            rollback.Click += (s, e) => Rollback();

            // --- разработчик ---
            var credit = new LinkLabel
            {
                Text = "Разработчик: AnikBeris",
                Location = new System.Drawing.Point(560, 72),
                AutoSize = true,
                LinkColor = System.Drawing.Color.LightSteelBlue,
                ForeColor = System.Drawing.Color.LightSteelBlue,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            credit.LinkClicked += (s, e) =>
                Process.Start(new ProcessStartInfo("https://github.com/AnikBeris") { UseShellExecute = true });

            Controls.Add(credit);
            Controls.Add(lblVersion);
            Controls.Add(browse); Controls.Add(pathBox);
            Controls.Add(install); Controls.Add(rollback);
            Controls.Add(progress); Controls.Add(stage); Controls.Add(log);

            string found = FindZCode();
            if (found != null) pathBox.Text = found;

            // /install — автоматический старт установки (для стендовых тестов)
            if (autoInstall) Shown += (s, e) => Install();

            Shown += (s, e) => CheckUpdates();
        }

        // ================= ОБНОВЛЕНИЯ (AutoUpdater.NET) =================
        // update.xml — ассет каждого релиза; URL постоянный (всегда последний релиз).

        void CheckUpdates()
        {
            AutoUpdaterDotNET.AutoUpdater.ShowRemindLaterButton = false;
            AutoUpdaterDotNET.AutoUpdater.ShowSkipButton = false;
            AutoUpdaterDotNET.AutoUpdater.RunUpdateAsAdmin = false;
            AutoUpdaterDotNET.AutoUpdater.Start(UpdateFeed);
        }

        // ================= УСТАНОВКА (логика v1.0.7, без изменений) =================

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
            if (log.InvokeRequired) { try { Invoke((MethodInvoker)delegate { LogLine(s); }); } catch { } return; }
            log.AppendText(s + Environment.NewLine);
            Application.DoEvents();
        }

        void SetStage(int percent, string text)
        {
            if (progress.InvokeRequired) { try { Invoke((MethodInvoker)delegate { SetStage(percent, text); }); } catch { } return; }
            progress.Value = Math.Max(0, Math.Min(100, percent));
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
                        MaterialMessageBox.Show("В выбранной папке нет ZCode.exe", "Ошибка");
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
            catch (Exception ex) { LogLine("ОШИБКА: " + ex.Message); MaterialMessageBox.Show(ex.Message, "Ошибка"); }
            install.Enabled = true; rollback.Enabled = true; browse.Enabled = true;
        }

        async Task DoInstall()
        {
            log.Clear(); SetStage(0, "");
            string dir = pathBox.Text.Trim();
            string res = Path.Combine(dir, "resources");
            string appDir = Path.Combine(res, "app");
            string zcodeExe = Path.Combine(dir, "ZCode.exe");

            if (!File.Exists(zcodeExe)) { LogLine("В этой папке нет ZCode.exe."); return; }
            if (ZCodeRunning(dir)) { LogLine("Сначала ЗАКРОЙТЕ ZCode (включая значок в трее) и повторите."); return; }

            string node = FindNode();
            if (node == null) { LogLine("Не найден Node.js. Установите Node или добавьте в PATH."); return; }
            LogLine("Node.js: " + node);

            string scriptDir = Application.StartupPath;
            string patch = Path.Combine(scriptDir, "patch.mjs");
            if (!File.Exists(patch)) { LogLine("Рядом с программой нет patch.mjs (" + scriptDir + ")"); return; }

            // --- A. Определить asar-источник (не удаляя ничего) ---
            string targetAsar = await FindAndRestoreMatchingAsar(dir, res, zcodeExe, node, scriptDir);
            if (targetAsar == null) return;

            // --- Патчинг во временную папку ---
            string temp = Path.Combine(Path.GetTempPath(), "zcode-ru-patch");
            SetStage(5, "Патчинг файлов (1-2 минуты)...");
            progress.Style = ProgressBarStyle.Marquee; progress.MarqueeAnimationSpeed = 30;
            LogLine("Патчу файлы...");
            int code = RunLive(node, "\"" + patch + "\" \"" + targetAsar + "\" \"" + temp + "\"");
            progress.Style = ProgressBarStyle.Continuous;
            if (code != 0) { SetStage(0, ""); LogLine("Патч не удался — программа НЕ изменена."); return; }

            // --- C. Сверка версии пропатченного стейджа ---
            string stagePackage = Path.Combine(temp, "package.json");
            if (!File.Exists(stagePackage))
            {
                LogLine("ОШИБКА: после патча не найден package.json в стейдже.");
                try { Directory.Delete(temp, true); } catch { }
                return;
            }
            string stageVersion = ReadVersionFromPackageJson(stagePackage);
            string zcodeVersion = FileVersionInfo.GetVersionInfo(zcodeExe).ProductVersion;
            if (!VersionEquals(stageVersion, zcodeVersion))
            {
                LogLine("ОШИБКА: версия в пропатченном стейдже (" + stageVersion + ") не совпадает с версией ZCode.exe (" + zcodeVersion + ").");
                LogLine("Установка отменена, файлы не изменены.");
                try { Directory.Delete(temp, true); } catch { }
                return;
            }
            LogLine("Версия стейджа подтверждена: " + stageVersion);

            // --- Бэкап app.asar.bak (после успешной сверки) ---
            string backup = Path.Combine(res, "app.asar.bak");
            if (!File.Exists(backup)) { File.Copy(targetAsar, backup); LogLine("Бэкап: app.asar.bak"); }

            // --- B. Удалить старый resources\app ТОЛЬКО после успешных проверок ---
            if (Directory.Exists(appDir))
            {
                LogLine("Удаление старой папки resources\\app...");
                Directory.Delete(appDir, true);
            }

            // --- Копирование файлов ---
            SetStage(70, "Копирование файлов...");
            await Task.Run(() => CopyDirectory(temp, appDir, 70, 95));
            try { Directory.Delete(temp, true); } catch { }

            // --- D. Сохранение оригинала: ровно один .orig актуальной версии ---
            SetStage(96, "Сохранение оригинала...");
            string orig = Path.Combine(res, "app.asar.orig");
            if (File.Exists(orig))
            {
                string origVersion = await ReadVersionFromAsar(node, orig, scriptDir);
                if (!VersionEquals(origVersion, zcodeVersion))
                {
                    LogLine("Удаление устаревшего app.asar.orig (версия " + origVersion + ")");
                    File.Delete(orig);
                }
                else
                {
                    LogLine("Удаление дубликата app.asar.orig (та же версия)");
                    File.Delete(orig);
                }
            }
            if (File.Exists(targetAsar))
            {
                File.Move(targetAsar, orig);
                LogLine("Оригинал сохранён: app.asar.orig");
            }

            SetStage(98, "Включение русского языка...");
            SetLocaleRu();

            SetStage(100, "Готово!");
            LogLine("");
            LogLine("Русификация ZCode " + zcodeVersion + " установлена.");
            MaterialMessageBox.Show("Установка завершена!\n\nРусификация ZCode " + zcodeVersion + " установлена.\n\nЗапустите ZCode — интерфейс будет на русском.", "Готово");
        }

        // ===== Вспомогательные методы =====

        async Task<string> FindAndRestoreMatchingAsar(string dir, string res, string zcodeExe, string node, string scriptDir)
        {
            string zcodeVersion = FileVersionInfo.GetVersionInfo(zcodeExe).ProductVersion;
            LogLine("Версия ZCode.exe: " + zcodeVersion);

            string[] candidates = {
                Path.Combine(res, "app.asar"),
                Path.Combine(res, "app.asar.orig"),
                Path.Combine(res, "app.asar.bak"),
                Path.Combine(res, "app.asar.original")
            };

            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;

                string ver = await ReadVersionFromAsar(node, candidate, scriptDir);
                LogLine("Проверка " + Path.GetFileName(candidate) + ": версия " + ver);
                if (VersionEquals(ver, zcodeVersion))
                {
                    string targetAsar = Path.Combine(res, "app.asar");
                    if (!string.Equals(candidate, targetAsar, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(targetAsar))
                        {
                            string existingVer = await ReadVersionFromAsar(node, targetAsar, scriptDir);
                            LogLine("Удалён несовпадающий app.asar (версия " + existingVer + ")");
                            File.Delete(targetAsar);
                        }
                        File.Move(candidate, targetAsar);
                        LogLine("Восстановлен " + Path.GetFileName(candidate) + " -> app.asar");
                    }
                    return targetAsar;
                }
            }

            LogLine("");
            LogLine("ОШИБКА: не найден app.asar с версией, совпадающей с ZCode.exe.");
            LogLine("Возможная причина: в папке resources остались файлы от старой версии ZCode.");
            LogLine("Решение: переустановите ZCode штатным апдейтером/установщиком, затем запустите русификатор снова.");
            MaterialMessageBox.Show(
                "Не найден app.asar с версией, совпадающей с ZCode.exe.\n\n" +
                "Возможная причина: в папке resources остались файлы от старой версии ZCode.\n\n" +
                "Решение: переустановите ZCode штатным апдейтером/установщиком, затем запустите русификатор снова.",
                "Ошибка версии");
            return null;
        }

        async Task<string> ReadVersionFromAsar(string node, string asarPath, string scriptDir)
        {
            try
            {
                string tempPkg = Path.Combine(Path.GetTempPath(), "zcode-pkg-" + Guid.NewGuid().ToString("N") + ".json");
                string script = "const fs=require('fs');fs.writeFileSync(process.argv[2],require(require('path').join(process.argv[3],'node_modules','@electron/asar','lib','asar.js')).extractFile(process.argv[1],'package.json'))";
                int code = RunLive(node, "-e \"" + script + "\" \"" + asarPath + "\" \"" + tempPkg + "\" \"" + scriptDir + "\"");
                if (code != 0 || !File.Exists(tempPkg))
                    return "unknown";
                string ver = ReadVersionFromPackageJson(tempPkg);
                try { File.Delete(tempPkg); } catch { }
                return ver;
            }
            catch { return "unknown"; }
        }

        string ReadVersionFromPackageJson(string pkgPath)
        {
            try
            {
                string json = File.ReadAllText(pkgPath, Encoding.UTF8);
                var match = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                return match.Success ? match.Groups[1].Value : "unknown";
            }
            catch { return "unknown"; }
        }

        bool VersionEquals(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == "unknown" || b == "unknown") return false;
            var aParts = a.Split('.');
            var bParts = b.Split('.');
            int len = Math.Min(3, Math.Min(aParts.Length, bParts.Length));
            for (int i = 0; i < len; i++)
            {
                if (aParts[i] != bParts[i]) return false;
            }
            return true;
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

        async void Rollback()
        {
            install.Enabled = false; rollback.Enabled = false; browse.Enabled = false;
            try { await DoRollback(); }
            catch (Exception ex) { LogLine("ОШИБКА: " + ex.Message); MaterialMessageBox.Show(ex.Message, "Ошибка"); }
            install.Enabled = true; rollback.Enabled = true; browse.Enabled = true;
        }

        async Task DoRollback()
        {
            log.Clear(); SetStage(0, "");
            string dir = pathBox.Text.Trim();
            string res = Path.Combine(dir, "resources");
            string asar = Path.Combine(res, "app.asar");
            string appDir = Path.Combine(res, "app");
            string zcodeExe = Path.Combine(dir, "ZCode.exe");

            if (!File.Exists(zcodeExe)) { LogLine("В этой папке нет ZCode.exe."); return; }
            if (ZCodeRunning(dir)) { LogLine("Сначала ЗАКРОЙТЕ ZCode (включая значок в трее) и повторите."); return; }

            string node = FindNode();
            if (node == null) { LogLine("Не найден Node.js. Установите Node или добавьте в PATH."); return; }
            string scriptDir = Application.StartupPath;

            string zcodeVersion = FileVersionInfo.GetVersionInfo(zcodeExe).ProductVersion;
            LogLine("Версия ZCode.exe: " + zcodeVersion);

            string[] candidates = {
                Path.Combine(res, "app.asar.orig"),
                Path.Combine(res, "app.asar.bak"),
                Path.Combine(res, "app.asar.original")
            };

            List<string> foundVersions = new List<string>();
            string matchedCandidate = null;
            string matchedVersion = null;

            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate))
                {
                    foundVersions.Add(Path.GetFileName(candidate) + ": (файл не найден)");
                    continue;
                }

                string ver = await ReadVersionFromAsar(node, candidate, scriptDir);
                foundVersions.Add(Path.GetFileName(candidate) + ": " + ver);
                LogLine("Проверка " + Path.GetFileName(candidate) + ": версия " + ver);

                if (matchedCandidate == null && VersionEquals(ver, zcodeVersion))
                {
                    matchedCandidate = candidate;
                    matchedVersion = ver;
                }
            }

            LogLine("");
            LogLine("Найденные бэкапы: " + string.Join(", ", foundVersions));

            if (matchedCandidate == null)
            {
                string msg = "Найденные бэкапы (" + string.Join(", ", foundVersions) + ") не совпадают с версией ZCode (" + zcodeVersion + ").\n\n" +
                             "Откат отменён — иначе приложение станет старой версии.\n" +
                             "Переустановите ZCode штатным апдейтером, затем поставьте русификатор заново.";
                LogLine(msg);
                MaterialMessageBox.Show(msg, "Откат невозможен");
                return;
            }

            LogLine("Найден подходящий бэкап: " + Path.GetFileName(matchedCandidate) + " (версия " + matchedVersion + ")");
            SetStage(30, "Восстановление app.asar...");

            if (File.Exists(asar)) File.Delete(asar);
            File.Move(matchedCandidate, asar);
            LogLine("Восстановлен app.asar версии " + matchedVersion + " из " + Path.GetFileName(matchedCandidate));

            SetStage(70, "Удаление русской папки...");
            if (Directory.Exists(appDir))
            {
                Directory.Delete(appDir, true);
                LogLine("Удалена папка resources\\app");
            }

            SetStage(100, "Готово!");
            LogLine("Готово — программа в исходном виде (английский язык).");
            MaterialMessageBox.Show("Откат выполнен успешно.\n\nВосстановлен app.asar версии " + matchedVersion + " из " + Path.GetFileName(matchedCandidate) + ".\n\nЗапустите ZCode — интерфейс будет на английском.", "Готово");
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm();
            if (args != null && args.Length > 0)
            {
                if (File.Exists(Path.Combine(args[0], "ZCode.exe")))
                    form.pathBox.Text = args[0];
                if (args.Length > 1 && args[1] == "/install" && File.Exists(Path.Combine(args[0], "ZCode.exe")))
                    autoInstall = true;
            }
            Application.Run(form);
        }
    }
}
