// Русификатор ZCode Desktop — установщик с выбором папки, прогресс-баром
// и проверкой обновлений через GitHub Releases.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZCodeRuInstaller
{
    public class MainForm : Form
    {
        const string InstallerVersion = "1.0.7";
        const string ReleasesApi = "https://api.github.com/repos/AnikLabs/zcode-ru-localization/releases/latest";
        const string ReleaseTagPrefix = "v";

        TextBox pathBox;
        Button browse, install, rollback;
        TextBox log;
        ProgressBar progress;
        Label stage;

        // --- обновления ---
        Label lblCurVer, lblUpdate;
        Button btnCheck, btnDownload;
        string pendingTag, pendingZipUrl, pendingSumsUrl;

        public MainForm()
        {
            Text = "Русификатор ZCode Desktop";
            Width = 680; Height = 578;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;

            // --- секция обновления ---
            lblCurVer = new Label { Text = "Версия русификатора: " + InstallerVersion, Left = 12, Top = 16, Width = 260 };
            btnCheck = new Button { Text = "Проверить обновления", Left = 500, Top = 12, Width = 150, Height = 26 };
            lblUpdate = new Label { Text = "", Left = 12, Top = 44, Width = 470, ForeColor = System.Drawing.Color.DimGray };
            btnDownload = new Button { Text = "Скачать", Left = 500, Top = 40, Width = 150, Height = 26, Visible = false };

            // --- установка ---
            var lbl = new Label { Text = "Папка с ZCode:", Left = 12, Top = 86, Width = 100 };
            pathBox = new TextBox { Left = 110, Top = 83, Width = 430 };
            browse = new Button { Text = "Обзор...", Left = 550, Top = 81, Width = 100 };
            install = new Button { Text = "Установить русский язык", Left = 12, Top = 117, Width = 300, Height = 32 };
            rollback = new Button { Text = "Откатить (вернуть как было)", Left = 322, Top = 117, Width = 300, Height = 32 };
            progress = new ProgressBar { Left = 12, Top = 156, Width = 638, Height = 20, Minimum = 0, Maximum = 100, Value = 0 };
            stage = new Label { Text = "", Left = 14, Top = 178, Width = 500, ForeColor = System.Drawing.Color.DimGray };
            log = new TextBox { Left = 12, Top = 202, Width = 638, Height = 296, Multiline = true,
                ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new System.Drawing.Font("Consolas", 9) };

            browse.Click += (s, e) => Browse();
            install.Click += (s, e) => Install();
            rollback.Click += (s, e) => Rollback();
            btnCheck.Click += (s, e) => { btnCheck.Enabled = false; CheckUpdates(false); };
            btnDownload.Click += (s, e) => DownloadUpdate();

            Controls.Add(lblCurVer); Controls.Add(btnCheck); Controls.Add(lblUpdate); Controls.Add(btnDownload);
            Controls.Add(lbl); Controls.Add(pathBox); Controls.Add(browse);
            Controls.Add(install); Controls.Add(rollback); Controls.Add(progress);
            Controls.Add(stage); Controls.Add(log);

            string found = FindZCode();
            if (found != null) pathBox.Text = found;

            // тихая проверка обновлений при старте
            Task.Run((Action)(() => CheckUpdates(true)));
        }

        // ================= ОБНОВЛЕНИЯ =================

        class ReleaseInfo { public string Tag; public string ZipUrl; public string SumsUrl; }

        void CheckUpdates(bool silent)
        {
            SafeInvoke(() => { lblUpdate.Text = "Проверка обновлений..."; lblUpdate.ForeColor = System.Drawing.Color.DimGray; });
            try
            {
                ReleaseInfo rel = FetchLatestRelease();
                if (rel == null) throw new Exception("ответ GitHub не распознан");
                string latest = rel.Tag.StartsWith(ReleaseTagPrefix) ? rel.Tag.Substring(1) : rel.Tag;
                int cmp = CompareVersions(latest, InstallerVersion);
                SafeInvoke(() =>
                {
                    btnCheck.Enabled = true;
                    if (cmp > 0)
                    {
                        lblUpdate.Text = "Доступна новая версия русификатора: " + rel.Tag;
                        lblUpdate.ForeColor = System.Drawing.Color.Firebrick;
                        pendingTag = rel.Tag; pendingZipUrl = rel.ZipUrl; pendingSumsUrl = rel.SumsUrl;
                        btnDownload.Text = "Скачать " + rel.Tag;
                        btnDownload.Visible = true;
                    }
                    else if (cmp == 0)
                    {
                        lblUpdate.Text = "У вас последняя версия (" + rel.Tag + ").";
                        lblUpdate.ForeColor = System.Drawing.Color.Green;
                        btnDownload.Visible = false;
                    }
                    else
                    {
                        lblUpdate.Text = "Установленная версия новее, чем последний релиз (" + rel.Tag + ").";
                        lblUpdate.ForeColor = System.Drawing.Color.DimGray;
                        btnDownload.Visible = false;
                    }
                });
            }
            catch (Exception ex)
            {
                SafeInvoke(() =>
                {
                    btnCheck.Enabled = true;
                    lblUpdate.Text = silent ? "" : "Не удалось проверить обновления: " + ex.Message;
                    if (!silent) lblUpdate.ForeColor = System.Drawing.Color.Firebrick;
                });
                if (!silent) LogLine("Проверка обновлений не удалась: " + ex.Message);
            }
        }

        ReleaseInfo FetchLatestRelease()
        {
            string json = HttpGet(ReleasesApi);
            var rel = new ReleaseInfo();
            var mTag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!mTag.Success) return null;
            rel.Tag = mTag.Groups[1].Value;
            var mZip = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*?" + Regex.Escape("zcode-ru-localization-" + rel.Tag.Substring(1) + "-windows-x64.zip") + ")\"");
            var mSums = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*?/checksums\\.txt)\"");
            if (!mZip.Success || !mSums.Success) return null;
            rel.ZipUrl = mZip.Groups[1].Value;
            rel.SumsUrl = mSums.Groups[1].Value;
            return rel;
        }

        string HttpGet(string url)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "zcode-ru-localization-updater";
            req.Accept = "application/vnd.github+json";
            req.Timeout = 15000;
            using (var resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        // 1 → a новее; 0 → равны; −1 → a старее
        int CompareVersions(string a, string b)
        {
            var pa = ParseSegments(a); var pb = ParseSegments(b);
            int len = Math.Max(pa.Count, pb.Count);
            for (int i = 0; i < len; i++)
            {
                int x = i < pa.Count ? pa[i] : 0;
                int y = i < pb.Count ? pb[i] : 0;
                if (x != y) return x > y ? 1 : -1;
            }
            return 0;
        }

        List<int> ParseSegments(string v)
        {
            var list = new List<int>();
            foreach (var part in v.Trim().TrimStart('v', 'V').Split('.'))
            {
                int n;
                if (int.TryParse(part, out n)) list.Add(n);
            }
            return list;
        }

        void DownloadUpdate()
        {
            btnDownload.Enabled = false; btnCheck.Enabled = false;
            install.Enabled = false; rollback.Enabled = false; browse.Enabled = false;
            string tag = pendingTag, zipUrl = pendingZipUrl, sumsUrl = pendingSumsUrl;
            Task.Run((Action)(() =>
            {
                try
                {
                    string workDir = Path.Combine(Path.GetTempPath(), "zcode-ru-update");
                    string tagDir = Path.Combine(workDir, tag);
                    string zipPath = Path.Combine(workDir, tag + ".zip");
                    try { Directory.CreateDirectory(workDir); } catch { }
                    if (Directory.Exists(tagDir)) { try { Directory.Delete(tagDir, true); } catch { } }

                    SafeInvoke(() => { progress.Value = 0; stage.Text = "Скачивание " + tag + "..."; });
                    LogLine("Скачиваю " + zipUrl);
                    HttpDownload(zipUrl, zipPath, (pct, msg) => SafeInvoke(() => { progress.Value = pct; stage.Text = msg; }));
                    LogLine("Скачано: " + new FileInfo(zipPath).Length + " байт");

                    SafeInvoke(() => { stage.Text = "Проверка SHA-256..."; });
                    string sums = HttpGet(sumsUrl);
                    string expected = null;
                    foreach (var line in sums.Split('\n'))
                    {
                        var parts = line.Trim().Split(new[] { ' ' }, 2);
                        if (parts.Length == 2 && parts[1].Trim().IndexOf("windows-x64.zip", StringComparison.OrdinalIgnoreCase) >= 0)
                        { expected = parts[0].Trim().ToLower(); break; }
                    }
                    string actual = Sha256File(zipPath);
                    LogLine("SHA-256: " + actual);
                    if (expected == null) throw new Exception("в checksums.txt не найден хэш zip");
                    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Хэш не совпал!\nОжидался: " + expected + "\nПолучен:   " + actual);
                    LogLine("SHA-256 совпал.");

                    SafeInvoke(() => { stage.Text = "Распаковка..."; progress.Value = 90; });
                    Directory.CreateDirectory(tagDir);
                    ZipFile.ExtractToDirectory(zipPath, tagDir);

                    string newExe = Path.Combine(tagDir, "РусификаторZCode.exe");
                    if (!File.Exists(newExe)) throw new Exception("в архиве нет РусификаторZCode.exe");

                    SafeInvoke(() =>
                    {
                        SetStage(100, "Обновление " + tag + " скачано и проверено.");
                        var ans = MessageBox.Show("Новая версия " + tag + " скачана и проверена (SHA-256 OK).\n\nЗапустить новую версию русификатора?",
                            "Обновление готово", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (ans == DialogResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo(newExe) { WorkingDirectory = tagDir });
                            Close();
                        }
                        btnDownload.Enabled = true; btnCheck.Enabled = true;
                        install.Enabled = true; rollback.Enabled = true; browse.Enabled = true;
                    });
                }
                catch (Exception ex)
                {
                    SafeInvoke(() =>
                    {
                        LogLine("ОШИБКА обновления: " + ex.Message);
                        MessageBox.Show(ex.Message, "Ошибка обновления", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progress.Value = 0; stage.Text = "";
                        btnDownload.Enabled = true; btnCheck.Enabled = true;
                        install.Enabled = true; rollback.Enabled = true; browse.Enabled = true;
                    });
                }
            }));
        }

        void HttpDownload(string url, string dest, Action<int, string> onProgress)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "zcode-ru-localization-updater";
            req.Timeout = 30000;
            using (var resp = req.GetResponse())
            using (var input = resp.GetResponseStream())
            using (var output = File.Create(dest))
            {
                long total = resp.ContentLength; long done = 0; int lastPct = 0;
                var buf = new byte[65536];
                int read;
                while ((read = input.Read(buf, 0, buf.Length)) > 0)
                {
                    output.Write(buf, 0, read);
                    done += read;
                    int pct = total > 0 ? (int)(done * 100 / total) : 0;
                    if (pct != lastPct)
                    {
                        lastPct = pct;
                        double mb = done / 1048576.0;
                        onProgress(pct, string.Format("Скачивание: {0}% ({1:F1} МБ)", pct, mb));
                    }
                }
            }
        }

        string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        void SafeInvoke(Action a)
        {
            try { Invoke((MethodInvoker)delegate { a(); }); }
            catch { a(); }
        }

        // ================= УСТАНОВКА (логика без изменений) =================

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
            if (targetAsar == null) return; // сообщение уже выведено внутри метода

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
            MessageBox.Show("Установка завершена!\n\nРусификация ZCode " + zcodeVersion + " установлена.\n\nЗапустите ZCode — интерфейс будет на русском.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show(
                "Не найден app.asar с версией, совпадающей с ZCode.exe.\n\n" +
                "Возможная причина: в папке resources остались файлы от старой версии ZCode.\n\n" +
                "Решение: переустановите ZCode штатным апдейтером/установщиком, затем запустите русификатор снова.",
                "Ошибка версии", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        async Task<string> ReadVersionFromAsar(string node, string asarPath, string scriptDir)
        {
            try
            {
                string tempPkg = Path.Combine(Path.GetTempPath(), "zcode-pkg-" + Guid.NewGuid().ToString("N") + ".json");
                string script = "const fs=require('fs');fs.writeFileSync(process.argv[2],require(require('path').join(process.argv[3],'node_modules','@electron','asar','lib','asar.js')).extractFile(process.argv[1],'package.json'))";
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
            catch (Exception ex) { LogLine("ОШИБКА: " + ex.Message); MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            install.Enabled = true; rollback.Enabled = true; browse.Enabled = true;
        }

        async Task DoRollback()
        {
            log.Clear(); progress.Value = 0; stage.Text = "";
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
                MessageBox.Show(msg, "Откат невозможен", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MessageBox.Show("Откат выполнен успешно.\n\nВосстановлен app.asar версии " + matchedVersion + " из " + Path.GetFileName(matchedCandidate) + ".\n\nЗапустите ZCode — интерфейс будет на английском.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm();
            // Необязательный аргумент: путь к папке ZCode (пресет для стендовых тестов)
            if (args != null && args.Length > 0 && File.Exists(Path.Combine(args[0], "ZCode.exe")))
                form.pathBox.Text = args[0];
            Application.Run(form);
        }
    }
}
