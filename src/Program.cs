// Wdrozyciel II Wielki - firmowy instalator offline + dolaczanie do domeny AD
// Kompilacja: build.cmd (csc.exe z .NET Framework, obecny na kazdym Windows)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Wdrozyciel II Wielki")]
[assembly: System.Reflection.AssemblyProduct("Wdrozyciel II Wielki")]
[assembly: System.Reflection.AssemblyVersion("21.37.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("21.37.0.0")]

namespace Wdrozyciel
{
    static class App
    {
        public const string Title = "Wdro\u017cyciel II Wielki";
        public const string Version = "21.37";
    }

    class AppEntry
    {
        public string Id, Name, WingetId, Locale, DirectUrl, Scope;
        public string Category = "Inne";
        public string ExeArgs = "/S", MsiArgs = "/qn";
        public string Version = "", FileRel = "", Sha256 = "", ManifestArgs = "";
        public double SizeMB;
        public List<string> Deps = new List<string>();

        public string InstallerPath(string repoDir)
        {
            if (string.IsNullOrEmpty(FileRel)) return null;
            return Path.Combine(repoDir, FileRel.Replace('/', '\\'));
        }
    }

    static class Native
    {
        [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint NetJoinDomain(string server, string domain, string ou, string account, string password, uint options);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int NetGetJoinInformation(string server, out IntPtr nameBuffer, out int joinStatus);

        [DllImport("netapi32.dll")]
        public static extern int NetApiBufferFree(IntPtr buffer);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public static string GetJoinInfo(out bool inDomain)
        {
            IntPtr buf;
            int status;
            inDomain = false;
            if (NetGetJoinInformation(null, out buf, out status) != 0) return "?";
            string name = Marshal.PtrToStringUni(buf);
            NetApiBufferFree(buf);
            inDomain = status == 3;
            return name;
        }
    }

    class Engine
    {
        public readonly string BaseDir, RepoDir, ManifestPath, AppsPath, LogDir;
        public List<AppEntry> Apps;
        public Action<string> Log = delegate { };
        public Action<string> Status = delegate { };

        public Engine(string baseDir)
        {
            BaseDir = baseDir;
            RepoDir = Path.Combine(baseDir, "repo");
            ManifestPath = Path.Combine(baseDir, "manifest.json");
            AppsPath = Path.Combine(baseDir, "apps.json");
            LogDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(RepoDir);
            Directory.CreateDirectory(LogDir);
            Apps = LoadApps();
        }

        static List<AppEntry> DefaultApps()
        {
            return new List<AppEntry>
            {
                new AppEntry { Id="firefox", Name="Mozilla Firefox", Category="Przegladarki",
                    WingetId="Mozilla.Firefox", Locale="pl-PL", ExeArgs="/S", MsiArgs="/qn",
                    DirectUrl="https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=pl" },
                new AppEntry { Id="chrome", Name="Google Chrome", Category="Przegladarki",
                    WingetId="Google.Chrome", ExeArgs="/silent /install", MsiArgs="/qn",
                    DirectUrl="https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi" },
                new AppEntry { Id="adobe-reader", Name="Adobe Acrobat Reader", Category="Biurowe",
                    WingetId="Adobe.Acrobat.Reader.64-bit", ExeArgs="/sAll /rs /msi EULA_ACCEPT=YES", MsiArgs="/qn" },
                new AppEntry { Id="libreoffice", Name="LibreOffice", Category="Biurowe",
                    WingetId="TheDocumentFoundation.LibreOffice", ExeArgs="/S", MsiArgs="/qn ALLUSERS=1" },
                new AppEntry { Id="vlc", Name="VLC media player", Category="Multimedia",
                    WingetId="VideoLAN.VLC", ExeArgs="/S", MsiArgs="/qn" },
                new AppEntry { Id="everything", Name="Everything (voidtools)", Category="Narzedzia",
                    WingetId="voidtools.Everything", ExeArgs="/S", MsiArgs="/qn" },
                new AppEntry { Id="vscode", Name="Visual Studio Code", Category="Narzedzia",
                    WingetId="Microsoft.VisualStudioCode", Scope="machine",
                    ExeArgs="/VERYSILENT /NORESTART /MERGETASKS=!runcode", MsiArgs="/qn",
                    DirectUrl="https://update.code.visualstudio.com/latest/win32-x64/stable" }
            };
        }

        // lista programow jest edytowalna w apps.json obok exe - bez rekompilacji
        List<AppEntry> LoadDefs()
        {
            if (!File.Exists(AppsPath))
            {
                var defs = DefaultApps();
                try { SaveDefs(defs); } catch { }
                return defs;
            }
            try
            {
                var root = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(AppsPath)) as Dictionary<string, object>;
                var list = new List<AppEntry>();
                if (root != null && root.ContainsKey("apps"))
                {
                    foreach (object o in (object[])root["apps"])
                    {
                        var d = o as Dictionary<string, object>;
                        if (d == null) continue;
                        var e = new AppEntry { Id = Str(d, "id"), Name = Str(d, "name"), WingetId = Str(d, "wingetId") };
                        string v;
                        v = Str(d, "category"); if (v.Length > 0) e.Category = v;
                        v = Str(d, "locale"); if (v.Length > 0) e.Locale = v;
                        v = Str(d, "scope"); if (v.Length > 0) e.Scope = v;
                        v = Str(d, "exeArgs"); if (v.Length > 0) e.ExeArgs = v;
                        v = Str(d, "msiArgs"); if (v.Length > 0) e.MsiArgs = v;
                        v = Str(d, "directUrl"); if (v.Length > 0) e.DirectUrl = v;
                        if (e.Id.Length > 0 && e.Name.Length > 0 && e.WingetId.Length > 0) list.Add(e);
                    }
                }
                if (list.Count > 0) return list;
                Log("apps.json nie zawiera programow - uzywam listy wbudowanej.");
            }
            catch (Exception ex) { Log("Blad odczytu apps.json: " + ex.Message + " - uzywam listy wbudowanej."); }
            return DefaultApps();
        }

        void SaveDefs(List<AppEntry> defs)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (var a in defs)
                list.Add(new Dictionary<string, object> {
                    { "id", a.Id }, { "name", a.Name }, { "category", a.Category },
                    { "wingetId", a.WingetId }, { "locale", a.Locale }, { "scope", a.Scope },
                    { "exeArgs", a.ExeArgs }, { "msiArgs", a.MsiArgs }, { "directUrl", a.DirectUrl }
                });
            var root = new Dictionary<string, object> { { "apps", list } };
            File.WriteAllText(AppsPath, new JavaScriptSerializer().Serialize(root));
        }

        List<AppEntry> LoadApps()
        {
            var result = LoadDefs();
            try
            {
                if (!File.Exists(ManifestPath)) return result;
                var root = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(ManifestPath)) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("apps")) return result;
                foreach (object o in (object[])root["apps"])
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    string id = Str(d, "id");
                    var entry = result.Find(a => a.Id == id);
                    if (entry == null)
                    {
                        entry = new AppEntry { Id = id, Name = Str(d, "name"), WingetId = Str(d, "wingetId") };
                        string cat = Str(d, "category");
                        if (cat.Length > 0) entry.Category = cat;
                        result.Add(entry);
                    }
                    entry.Version = Str(d, "version");
                    entry.FileRel = Str(d, "file");
                    entry.Sha256 = Str(d, "sha256");
                    entry.ManifestArgs = Str(d, "silentArgs");
                    if (d.ContainsKey("sizeMB")) { try { entry.SizeMB = Convert.ToDouble(d["sizeMB"]); } catch { } }
                    entry.Deps.Clear();
                    object depsObj;
                    if (d.TryGetValue("deps", out depsObj) && depsObj is object[])
                        foreach (object dep in (object[])depsObj)
                            if (dep != null) entry.Deps.Add(dep.ToString());
                }
            }
            catch (Exception ex) { Log("Blad odczytu manifest.json: " + ex.Message); }
            return result;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        public void SaveManifest()
        {
            var list = new List<Dictionary<string, object>>();
            foreach (var a in Apps)
            {
                if (string.IsNullOrEmpty(a.FileRel)) continue;
                bool isMsi = a.FileRel.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
                list.Add(new Dictionary<string, object> {
                    { "id", a.Id }, { "name", a.Name }, { "wingetId", a.WingetId },
                    { "category", a.Category },
                    { "version", a.Version }, { "file", a.FileRel }, { "sha256", a.Sha256 },
                    { "sizeMB", a.SizeMB }, { "silentArgs", isMsi ? a.MsiArgs : a.ExeArgs },
                    { "deps", a.Deps }
                });
            }
            var root = new Dictionary<string, object> { { "updated", DateTime.Now.ToString("s") }, { "apps", list } };
            File.WriteAllText(ManifestPath, new JavaScriptSerializer().Serialize(root));
        }

        public void DownloadOne(AppEntry app)
        {
            string latest = WingetLatestVersion(app.WingetId);
            if (latest != null) Log("Najnowsza wersja: " + latest);
            else Log("Nie mozna odczytac wersji z winget (brak internetu/winget?).");

            string existing = app.InstallerPath(RepoDir);
            if (latest != null && latest == app.Version && existing != null && File.Exists(existing))
            {
                Log("Repo aktualne (" + latest + ") - pomijam.");
                return;
            }

            string tmp = Path.Combine(Path.GetTempPath(), "wdrozyciel", app.Id);
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            Directory.CreateDirectory(tmp);

            bool ok = false;
            if (latest != null)
            {
                ok = WingetDownload(app, tmp, app.Locale);
                if (!ok && app.Locale != null)
                {
                    Log("Pobieranie z locale " + app.Locale + " nieudane - probuje bez locale.");
                    ok = WingetDownload(app, tmp, null);
                }
            }
            if (!ok && !string.IsNullOrEmpty(app.DirectUrl))
            {
                Log("Winget nieudany - pobieram bezposrednio: " + app.DirectUrl);
                DownloadDirect(app.DirectUrl, tmp);
                ok = true;
            }
            if (!ok) { Log("BLAD: nie udalo sie pobrac " + app.Name + "."); return; }

            var files = AllInstallers(tmp);
            if (files.Count == 0) { Log("BLAD: brak pliku instalatora po pobraniu."); return; }
            files.Sort((a, b) => new FileInfo(b).Length.CompareTo(new FileInfo(a).Length));
            string mainFile = files[0];

            string appRepo = Path.Combine(RepoDir, app.Id);
            try { if (Directory.Exists(appRepo)) Directory.Delete(appRepo, true); } catch { }
            Directory.CreateDirectory(appRepo);

            app.Deps.Clear();
            string mainDest = null;
            foreach (string f in files)
            {
                string dest = Path.Combine(appRepo, Path.GetFileName(f));
                File.Move(f, dest);
                if (f == mainFile) mainDest = dest;
                else
                {
                    app.Deps.Add(app.Id + "/" + Path.GetFileName(f));
                    Log("Zaleznosc: " + Path.GetFileName(f));
                }
            }
            try { Directory.Delete(tmp, true); } catch { }

            app.Version = latest != null ? latest : "?";
            app.FileRel = app.Id + "/" + Path.GetFileName(mainDest);
            app.Sha256 = Sha256Of(mainDest);
            app.SizeMB = Math.Round(new FileInfo(mainDest).Length / 1048576.0, 1);
            Log(string.Format("Zapisano: {0} ({1} MB)", Path.GetFileName(mainDest), app.SizeMB));
        }

        string WingetLatestVersion(string wingetId)
        {
            string outp;
            int code = RunCapture("winget",
                "show --id " + wingetId + " --exact --source winget --accept-source-agreements --disable-interactivity",
                out outp);
            if (code != 0) return null;
            var m = Regex.Match(outp, @"(?m)^\s*(?:Version|Wersja):\s*(\S+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        bool WingetDownload(AppEntry app, string dir, string locale)
        {
            string args = "download --id " + app.WingetId + " --exact --architecture x64 --source winget" +
                          " --download-directory \"" + dir + "\"" +
                          " --accept-package-agreements --accept-source-agreements --disable-interactivity";
            if (locale != null) args += " --locale " + locale;
            if (!string.IsNullOrEmpty(app.Scope)) args += " --scope " + app.Scope;
            string outp;
            int code = RunCapture("winget", args, out outp);
            if (code != 0)
            {
                var lines = outp.TrimEnd().Split('\n');
                if (lines.Length > 0) Log("winget: " + lines[lines.Length - 1].Trim());
            }
            return code == 0 && AllInstallers(dir).Count > 0;
        }

        void DownloadDirect(string url, string dir)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.AllowAutoRedirect = true;
            req.UserAgent = "Wdrozyciel/21.37";
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                string name = Uri.UnescapeDataString(Path.GetFileName(resp.ResponseUri.LocalPath));
                if (string.IsNullOrEmpty(name) ||
                    (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                     !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)))
                    name = "installer" + (url.Contains(".msi") ? ".msi" : ".exe");
                string dest = Path.Combine(dir, name);
                using (var s = resp.GetResponseStream())
                using (var f = File.Create(dest))
                {
                    var buf = new byte[81920];
                    long total = 0;
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0)
                    {
                        f.Write(buf, 0, n);
                        total += n;
                        if (total % (5 * 1048576) < 81920)
                            Status(string.Format("Pobieram bezposrednio... {0} MB", total / 1048576));
                    }
                }
            }
        }

        static List<string> AllInstallers(string dir)
        {
            var result = new List<string>();
            foreach (string pat in new[] { "*.exe", "*.msi" })
                result.AddRange(Directory.GetFiles(dir, pat, SearchOption.AllDirectories));
            return result;
        }

        public bool InstallOne(AppEntry app, bool checkHash)
        {
            string path = app.InstallerPath(RepoDir);
            if (path == null || !File.Exists(path)) { Log("BLAD: brak instalatora w repo."); return false; }

            if (checkHash && !string.IsNullOrEmpty(app.Sha256))
            {
                Status("Weryfikuje SHA256: " + app.Name + "...");
                if (!string.Equals(Sha256Of(path), app.Sha256, StringComparison.OrdinalIgnoreCase))
                { Log("BLAD: niezgodny SHA256 (uszkodzony plik?)."); return false; }
                Log("SHA256 OK.");
            }

            foreach (string dep in app.Deps)
            {
                string dpath = Path.Combine(RepoDir, dep.Replace('/', '\\'));
                if (!File.Exists(dpath)) { Log("UWAGA: brak zaleznosci " + dep + " - pomijam."); continue; }
                Status("Zaleznosc: " + Path.GetFileName(dpath) + "...");
                bool depMsi = dpath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
                int dcode = RunInstaller(dpath, depMsi ? "/qn" : "/install /quiet /norestart");
                if (dcode == 0 || dcode == 1638 || dcode == 3010 || dcode == 1641)
                    Log("Zaleznosc " + Path.GetFileName(dpath) + ": OK (kod " + dcode + ").");
                else
                    Log("UWAGA: zaleznosc " + Path.GetFileName(dpath) + " zwrocila kod " + dcode + " - kontynuuje.");
            }

            Status("Instaluje: " + app.Name + "...");
            var sw = Stopwatch.StartNew();
            int code;
            try
            {
                bool isMsi = path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
                string silent = isMsi ? app.MsiArgs : app.ExeArgs;
                if (string.IsNullOrEmpty(silent)) silent = app.ManifestArgs;
                code = RunInstaller(path, silent);
            }
            catch (Exception ex) { Log("BLAD uruchomienia: " + ex.Message); return false; }
            sw.Stop();

            if (code == 0 || code == 3010 || code == 1641)
            {
                Log(string.Format("OK - zainstalowano w {0}s{1}", (int)sw.Elapsed.TotalSeconds,
                    code != 0 ? " (wymagany restart)" : ""));
                return true;
            }
            Log("BLAD: kod wyjscia " + code);
            return false;
        }

        static int RunInstaller(string path, string silentArgs)
        {
            bool isMsi = path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
            var psi = isMsi
                ? new ProcessStartInfo("msiexec.exe", "/i \"" + path + "\" " + silentArgs + " /norestart")
                : new ProcessStartInfo(path, silentArgs);
            psi.UseShellExecute = false;
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        public static string Sha256Of(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
        }

        static int RunCapture(string file, string args, out string output)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            var sb = new StringBuilder();
            try
            {
                using (var p = Process.Start(psi))
                {
                    p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) sb.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    output = sb.ToString();
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { output = ex.Message; return -1; }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }

            // Wdrozyciel.exe /download [id1,id2] - tryb bez GUI do harmonogramu zadan
            if (args.Length > 0 && args[0].Equals("/download", StringComparison.OrdinalIgnoreCase))
            {
                HeadlessDownload(args.Length > 1 ? args[1].Split(',') : null);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        static void HeadlessDownload(string[] ids)
        {
            var eng = new Engine(AppDomain.CurrentDomain.BaseDirectory);
            string logf = Path.Combine(eng.LogDir, string.Format("download-{0:yyyyMMdd-HHmmss}.log", DateTime.Now));
            eng.Log = delegate (string s)
            {
                try { File.AppendAllText(logf, string.Format("[{0:HH:mm:ss}] {1}\r\n", DateTime.Now, s)); } catch { }
            };
            foreach (var app in eng.Apps)
            {
                if (ids != null && Array.IndexOf(ids, app.Id) < 0) continue;
                eng.Log("=== " + app.Name + " ===");
                try { eng.DownloadOne(app); }
                catch (Exception ex) { eng.Log("BLAD: " + ex.Message); }
            }
            eng.SaveManifest();
            eng.Log("Zakonczono. Manifest zapisany.");
        }
    }

    class MainForm : Form
    {
        const string Domain = "ad.gliwice.cloud";
        static readonly string[] CategoryOrder = { "Przegladarki", "Biurowe", "Multimedia", "Narzedzia" };

        readonly Engine eng;
        TreeView tv;
        TextBox txtLog, txtHostname, txtDomUser, txtDomPass;
        ProgressBar pb;
        Label lblStatus, lblDomStatus, lblJoined;
        Button btnAll, btnNone, btnDownload, btnInstall, btnJoin, btnRename;
        CheckBox chkHash;
        string logFilePath;
        bool domainChecking, suppressCheck;
        volatile bool domainUp;

        public MainForm()
        {
            Text = App.Title + " - wersja " + App.Version;
            ClientSize = new Size(780, 700);
            MinimumSize = new Size(720, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            var lblTop = new Label { Text = "Zaznacz programy, potem: Pobierz (online, aktualizuje repo) lub Zainstaluj (offline, z repo).",
                Location = new Point(12, 10), AutoSize = true };

            tv = new TreeView { Location = new Point(12, 35), Size = new Size(520, 300),
                CheckBoxes = true, ShowLines = false, FullRowSelect = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            tv.AfterCheck += delegate (object s, TreeViewEventArgs e)
            {
                if (suppressCheck) return;
                suppressCheck = true;
                if (e.Node.Tag == null)
                    foreach (TreeNode ch in e.Node.Nodes) ch.Checked = e.Node.Checked;
                else
                {
                    bool all = true;
                    foreach (TreeNode ch in e.Node.Parent.Nodes) if (!ch.Checked) { all = false; break; }
                    e.Node.Parent.Checked = all;
                }
                suppressCheck = false;
            };

            int bx = 545;
            btnAll = new Button { Text = "Zaznacz wszystko", Location = new Point(bx, 35), Size = new Size(215, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnNone = new Button { Text = "Odznacz wszystko", Location = new Point(bx, 70), Size = new Size(215, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnDownload = new Button { Text = "POBIERZ aktualne wersje\r\n(wymaga internetu)", Location = new Point(bx, 120), Size = new Size(215, 50), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnInstall = new Button { Text = "ZAINSTALUJ zaznaczone\r\n(offline, z repo)", Location = new Point(bx, 180), Size = new Size(215, 50), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnInstall.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            chkHash = new CheckBox { Text = "Weryfikuj sumy SHA256", Location = new Point(bx, 240), Size = new Size(215, 20), Checked = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };

            var grp = new GroupBox { Text = "Domena AD", Location = new Point(bx, 266), Size = new Size(215, 246), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var lblDomName = new Label { Text = Domain, Location = new Point(10, 20), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            lblDomStatus = new Label { Text = "Sprawdzam domene...", Location = new Point(10, 40), AutoSize = true, ForeColor = Color.Gray };
            txtHostname = new TextBox { Location = new Point(10, 64), Size = new Size(195, 23), Text = Environment.MachineName };
            btnRename = new Button { Text = "Zmien tylko nazwe", Location = new Point(10, 92), Size = new Size(195, 26) };
            txtDomUser = new TextBox { Location = new Point(10, 128), Size = new Size(195, 23) };
            txtDomPass = new TextBox { Location = new Point(10, 156), Size = new Size(195, 23), UseSystemPasswordChar = true };
            btnJoin = new Button { Text = "DODAJ DO DOMENY", Location = new Point(10, 186), Size = new Size(195, 34), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            lblJoined = new Label { Text = "", Location = new Point(10, 224), AutoSize = true, ForeColor = Color.DimGray };
            grp.Controls.AddRange(new Control[] { lblDomName, lblDomStatus, txtHostname, btnRename, txtDomUser, txtDomPass, btnJoin, lblJoined });

            lblStatus = new Label { Text = "Gotowy.", Location = new Point(12, 345), AutoSize = true };
            pb = new ProgressBar { Location = new Point(12, 365), Size = new Size(520, 18) };

            txtLog = new TextBox { Location = new Point(12, 522), Size = new Size(748, 166),
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5f), BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };

            Controls.AddRange(new Control[] { lblTop, tv, btnAll, btnNone, btnDownload, btnInstall, chkHash, grp, lblStatus, pb, txtLog });

            btnAll.Click += delegate { CheckAllNodes(true); };
            btnNone.Click += delegate { CheckAllNodes(false); };
            btnDownload.Click += delegate { RunWorker(DownloadSelected); };
            btnInstall.Click += delegate { RunWorker(InstallSelected); };
            btnJoin.Click += delegate { JoinDomainClicked(); };
            btnRename.Click += delegate { RenameClicked(); };

            Native.SendMessage(txtDomUser.Handle, 0x1501, (IntPtr)1, "login domenowy");
            Native.SendMessage(txtDomPass.Handle, 0x1501, (IntPtr)1, "haslo");
            Native.SendMessage(txtHostname.Handle, 0x1501, (IntPtr)1, "nazwa komputera");

            eng = new Engine(AppDomain.CurrentDomain.BaseDirectory);
            logFilePath = Path.Combine(eng.LogDir, string.Format("gui-{0}-{1:yyyyMMdd-HHmmss}.log", Environment.MachineName, DateTime.Now));
            eng.Log = Log;
            eng.Status = Status;

            RefreshList(true);
            UpdateJoinedLabel();
            Log(App.Title + " " + App.Version + " | Repo: " + eng.RepoDir);

            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += delegate { CheckDomainAsync(); };
            timer.Start();
            CheckDomainAsync();
        }

        // ---------- domena ----------

        void CheckDomainAsync()
        {
            if (domainChecking) return;
            domainChecking = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool up = DomainReachable();
                domainUp = up;
                UI(delegate
                {
                    lblDomStatus.Text = up ? "Domena dostepna" : "Domena niedostepna";
                    lblDomStatus.ForeColor = up ? Color.Green : Color.Red;
                });
                domainChecking = false;
            });
        }

        static bool DomainReachable()
        {
            try
            {
                using (var tcp = new TcpClient())
                {
                    var ar = tcp.BeginConnect(Domain, 389, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(2000)) return false;
                    tcp.EndConnect(ar);
                    return true;
                }
            }
            catch { return false; }
        }

        static bool ValidHostname(string name)
        {
            return Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9-]{0,14}$");
        }

        void SetDomainButtons(bool enabled) { btnJoin.Enabled = btnRename.Enabled = enabled; }

        void JoinDomainClicked()
        {
            string host = txtHostname.Text.Trim();
            string user = txtDomUser.Text.Trim();
            string pass = txtDomPass.Text;
            if (user.Length == 0 || pass.Length == 0) { MessageBox.Show("Podaj login i haslo konta domenowego.", App.Title); return; }
            if (!ValidHostname(host)) { MessageBox.Show("Nieprawidlowa nazwa komputera (1-15 znakow: litery, cyfry, myslnik).", App.Title); return; }
            if (!domainUp && MessageBox.Show("Domena wyglada na niedostepna. Kontynuowac mimo to?", App.Title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            string account = (user.Contains("\\") || user.Contains("@")) ? user : user + "@" + Domain;
            SetDomainButtons(false);
            var t = new Thread(delegate ()
            {
                try
                {
                    Log("Dolaczanie do domeny " + Domain + " jako " + account + "...");
                    uint rc = Native.NetJoinDomain(null, Domain, null, account, pass, 0x23);
                    if (rc != 0) { Log("BLAD dolaczania: " + JoinError(rc)); return; }
                    Log("Dolaczono do domeny " + Domain + ".");

                    bool renamed = false;
                    if (!host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    {
                        Log("Zmiana nazwy komputera na " + host + "...");
                        int rr = WmiRename(host, account, pass);
                        if (rr == 0) { Log("Nazwa zmieniona na " + host + "."); renamed = true; }
                        else Log("BLAD zmiany nazwy (kod " + rr + ") - zmien nazwe recznie po restarcie.");
                    }
                    UpdateJoinedLabel();
                    UI(delegate
                    {
                        MessageBox.Show("Dodano do domeny " + Domain + (renamed ? "\nNowa nazwa: " + host : "") +
                            "\n\nUruchom ponownie komputer, aby dokonczyc.", App.Title,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                }
                finally { UI(delegate { SetDomainButtons(true); }); }
            });
            t.IsBackground = true;
            t.Start();
        }

        void RenameClicked()
        {
            string host = txtHostname.Text.Trim();
            if (!ValidHostname(host)) { MessageBox.Show("Nieprawidlowa nazwa komputera (1-15 znakow: litery, cyfry, myslnik).", App.Title); return; }
            if (host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            { MessageBox.Show("Komputer juz nazywa sie " + host + ".", App.Title); return; }

            bool inDom;
            Native.GetJoinInfo(out inDom);
            string user = txtDomUser.Text.Trim();
            string pass = txtDomPass.Text;
            string account = (inDom && user.Length > 0) ? ((user.Contains("\\") || user.Contains("@")) ? user : user + "@" + Domain) : null;
            string accPass = account != null ? pass : null;

            SetDomainButtons(false);
            var t = new Thread(delegate ()
            {
                try
                {
                    Log("Zmiana nazwy komputera na " + host + "...");
                    int rr = WmiRename(host, account, accPass);
                    if (rr == 0)
                    {
                        Log("Nazwa zmieniona na " + host + ".");
                        UI(delegate { MessageBox.Show("Nazwa zmieniona na " + host + ".\nUruchom ponownie komputer.", App.Title, MessageBoxButtons.OK, MessageBoxIcon.Information); });
                    }
                    else Log("BLAD zmiany nazwy: kod " + rr + (inDom && account == null ? " (komputer w domenie - podaj login i haslo domenowe)" : ""));
                }
                finally { UI(delegate { SetDomainButtons(true); }); }
            });
            t.IsBackground = true;
            t.Start();
        }

        static int WmiRename(string newName, string user, string pass)
        {
            using (var cs = new ManagementObject(new ManagementPath("Win32_ComputerSystem.Name='" + Environment.MachineName + "'")))
            {
                var inp = cs.GetMethodParameters("Rename");
                inp["Name"] = newName;
                inp["UserName"] = user;
                inp["Password"] = pass;
                var ret = cs.InvokeMethod("Rename", inp, null);
                return Convert.ToInt32(ret["ReturnValue"]);
            }
        }

        static string JoinError(uint rc)
        {
            switch (rc)
            {
                case 5: return "brak uprawnien (kod 5)";
                case 1326: return "zly login lub haslo (kod 1326)";
                case 1355: return "nie znaleziono domeny (kod 1355)";
                case 2224: return "konto komputera juz istnieje (kod 2224)";
                case 1219: return "konflikt polaczen - wyloguj sie i sprobuj ponownie (kod 1219)";
                default: return new System.ComponentModel.Win32Exception((int)rc).Message + " (kod " + rc + ")";
            }
        }

        void UpdateJoinedLabel()
        {
            bool inDom;
            string name = Native.GetJoinInfo(out inDom);
            UI(delegate { lblJoined.Text = inDom ? "Obecnie w domenie: " + name : "Grupa robocza: " + name; });
        }

        // ---------- programy ----------

        void UI(Action a) { if (InvokeRequired) BeginInvoke((MethodInvoker)delegate { a(); }); else a(); }

        void Log(string msg)
        {
            string line = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, msg);
            UI(delegate { txtLog.AppendText(line + Environment.NewLine); });
            try { File.AppendAllText(logFilePath, line + Environment.NewLine); } catch { }
        }

        void Status(string s) { UI(delegate { lblStatus.Text = s; }); }
        void Progress(int val, int max) { UI(delegate { pb.Maximum = Math.Max(1, max); pb.Value = Math.Min(val, pb.Maximum); }); }

        void CheckAllNodes(bool value)
        {
            suppressCheck = true;
            foreach (TreeNode cat in tv.Nodes)
            {
                cat.Checked = value;
                foreach (TreeNode n in cat.Nodes) n.Checked = value;
            }
            suppressCheck = false;
        }

        void RefreshList(bool checkAll)
        {
            UI(delegate
            {
                var wasChecked = new HashSet<string>();
                foreach (TreeNode cat in tv.Nodes)
                    foreach (TreeNode n in cat.Nodes)
                        if (n.Checked && n.Tag is AppEntry) wasChecked.Add(((AppEntry)n.Tag).Id);

                var categories = new List<string>(CategoryOrder);
                foreach (var a in eng.Apps)
                    if (!categories.Contains(a.Category)) categories.Add(a.Category);

                suppressCheck = true;
                tv.BeginUpdate();
                tv.Nodes.Clear();
                foreach (string cat in categories)
                {
                    var catNode = new TreeNode(cat) { NodeFont = new Font(Font, FontStyle.Bold) };
                    foreach (var a in eng.Apps)
                    {
                        if (a.Category != cat) continue;
                        string path = a.InstallerPath(eng.RepoDir);
                        string status = (path != null && File.Exists(path))
                            ? string.Format("v{0}, {1} MB - gotowy", a.Version, a.SizeMB)
                            : "BRAK W REPO - kliknij Pobierz";
                        catNode.Nodes.Add(new TreeNode(string.Format("{0}   [{1}]", a.Name, status))
                        { Tag = a, Checked = checkAll || wasChecked.Contains(a.Id) });
                    }
                    if (catNode.Nodes.Count == 0) continue;
                    bool all = true;
                    foreach (TreeNode n in catNode.Nodes) if (!n.Checked) { all = false; break; }
                    catNode.Checked = all;
                    tv.Nodes.Add(catNode);
                }
                tv.ExpandAll();
                tv.EndUpdate();
                suppressCheck = false;
            });
        }

        List<AppEntry> Selected()
        {
            var result = new List<AppEntry>();
            foreach (TreeNode cat in tv.Nodes)
                foreach (TreeNode n in cat.Nodes)
                    if (n.Checked && n.Tag is AppEntry) result.Add((AppEntry)n.Tag);
            return result;
        }

        void RunWorker(Action work)
        {
            if (Selected().Count == 0) { MessageBox.Show("Nie zaznaczono zadnego programu.", App.Title); return; }
            btnDownload.Enabled = btnInstall.Enabled = btnAll.Enabled = btnNone.Enabled = false;
            var t = new Thread(delegate ()
            {
                try { work(); }
                catch (Exception ex) { Log("BLAD KRYTYCZNY: " + ex.Message); }
                finally
                {
                    UI(delegate { btnDownload.Enabled = btnInstall.Enabled = btnAll.Enabled = btnNone.Enabled = true; });
                    Status("Gotowy.");
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        void DownloadSelected()
        {
            var sel = Selected();
            int done = 0;
            Progress(0, sel.Count);
            foreach (var app in sel)
            {
                Status("Pobieram: " + app.Name + "...");
                Log("=== " + app.Name + " ===");
                try { eng.DownloadOne(app); }
                catch (Exception ex) { Log("BLAD pobierania " + app.Name + ": " + ex.Message); }
                Progress(++done, sel.Count);
            }
            eng.SaveManifest();
            RefreshList(false);
            Log("Pobieranie zakonczone. Manifest zapisany.");
        }

        void InstallSelected()
        {
            var sel = Selected();
            int done = 0, okCount = 0;
            var failed = new List<string>();
            Progress(0, sel.Count);
            Log("=== START WDROZENIA na " + Environment.MachineName + " ===");

            foreach (var app in sel)
            {
                Log("--- " + app.Name + " " + app.Version + " ---");
                bool ok = false;
                try { ok = eng.InstallOne(app, chkHash.Checked); }
                catch (Exception ex) { Log("BLAD: " + ex.Message); }
                if (ok) okCount++; else failed.Add(app.Name);
                Progress(++done, sel.Count);
            }

            Log(string.Format("=== KONIEC: {0}/{1} OK{2} ===", okCount, sel.Count,
                failed.Count > 0 ? " | niepowodzenia: " + string.Join(", ", failed.ToArray()) : ""));
            UI(delegate
            {
                MessageBox.Show(string.Format("Zainstalowano {0} z {1}.{2}", okCount, sel.Count,
                    failed.Count > 0 ? "\nNiepowodzenia: " + string.Join(", ", failed.ToArray()) : ""),
                    App.Title, MessageBoxButtons.OK,
                    failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            });
        }
    }
}
