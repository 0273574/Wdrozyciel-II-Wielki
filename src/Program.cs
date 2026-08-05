// Wdrozyciel II Wielki - instalator offline, narzedzia administracyjne i dolaczanie do domeny AD
// Kompilacja: src\build.cmd (.NET Framework 4.x, Windows 8/10/11)
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
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Wdrozyciel II Wielki")]
[assembly: System.Reflection.AssemblyProduct("Wdrozyciel II Wielki")]
[assembly: System.Reflection.AssemblyCompany("Gliwice Cloud")]
[assembly: System.Reflection.AssemblyVersion("21.37.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("21.37.0.0")]

namespace Wdrozyciel
{
    static class App
    {
        public const string Title = "Wdro\u017cyciel II Wielki";
        public const string Version = "21.37";
    }

    static class DataLocation
    {
        public const string PortableFolderName = ".wdrozyciel";

        public static string Resolve(string executableDirectory)
        {
            string path = Path.Combine(executableDirectory, PortableFolderName);
            Directory.CreateDirectory(path);
            try
            {
                FileAttributes attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.Hidden) == 0)
                    File.SetAttributes(path, attrs | FileAttributes.Hidden);
            }
            catch { }
            return path;
        }
    }

    class AppEntry
    {
        public string Id = "";
        public string Name = "";
        public string WingetId = "";
        public string Locale = "";
        public string DirectUrl = "";
        public string DirectVersion = "";
        public string Scope = "machine";
        public string Category = "Inne";
        public string ExeArgs = "/S";
        public string MsiArgs = "/qn ALLUSERS=1";
        public string Version = "";
        public string FileRel = "";
        public string Sha256 = "";
        public string ManifestArgs = "";
        public string ShortcutName = "";
        public string ShortcutTarget = "";
        public string PostInstall = "";
        public bool PublicDesktop = true;
        public double SizeMB;
        public List<string> Deps = new List<string>();

        public string InstallerPath(string repoDir)
        {
            if (string.IsNullOrEmpty(FileRel)) return null;
            return Path.Combine(repoDir, FileRel.Replace('/', '\\'));
        }
    }

    class ScriptItem
    {
        public string Path;
        public ScriptItem(string path) { Path = path; }
        public override string ToString() { return System.IO.Path.GetFileNameWithoutExtension(Path); }
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
        public readonly string BaseDir;
        public readonly string RepoDir;
        public readonly string ManifestPath;
        public readonly string AppsPath;
        public readonly string LogDir;
        public readonly string ScriptsDir;
        public readonly string ToolsDir;
        public List<AppEntry> Apps;
        public Action<string> Log = delegate { };
        public Action<string> Status = delegate { };

        string wingetPath;
        bool wingetResolved;
        bool sourceRefreshed;

        public Engine(string baseDir)
        {
            BaseDir = baseDir;
            RepoDir = Path.Combine(baseDir, "repo");
            ManifestPath = Path.Combine(baseDir, "manifest.json");
            AppsPath = Path.Combine(baseDir, "apps.json");
            LogDir = Path.Combine(baseDir, "logs");
            ScriptsDir = Path.Combine(baseDir, "scripts");
            ToolsDir = Path.Combine(baseDir, "tools");
            Directory.CreateDirectory(RepoDir);
            Directory.CreateDirectory(LogDir);
            Directory.CreateDirectory(ScriptsDir);
            Directory.CreateDirectory(ToolsDir);
            Apps = LoadApps();
        }

        static List<AppEntry> DefaultApps()
        {
            return new List<AppEntry>
            {
                new AppEntry { Id="firefox", Name="Mozilla Firefox", Category="Przegladarki",
                    WingetId="Mozilla.Firefox", Locale="pl-PL", Scope="machine",
                    ExeArgs="/S", MsiArgs="/qn ALLUSERS=1 INSTALL_MAINTENANCE_SERVICE=true",
                    DirectUrl="https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=pl",
                    ShortcutName="Mozilla Firefox", ShortcutTarget="%ProgramFiles%\\Mozilla Firefox\\firefox.exe|%ProgramFiles(x86)%\\Mozilla Firefox\\firefox.exe",
                    PostInstall="firefox-auto-update" },
                new AppEntry { Id="chrome", Name="Google Chrome", Category="Przegladarki",
                    WingetId="Google.Chrome", Scope="machine", ExeArgs="/silent /install", MsiArgs="/qn ALLUSERS=1",
                    DirectUrl="https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi",
                    ShortcutName="Google Chrome", ShortcutTarget="%ProgramFiles%\\Google\\Chrome\\Application\\chrome.exe" },
                new AppEntry { Id="adobe-reader", Name="Adobe Acrobat Reader", Category="Biurowe",
                    WingetId="Adobe.Acrobat.Reader.64-bit", Scope="machine",
                    ExeArgs="-sfx_nu /sAll /rs /rps /msi EULA_ACCEPT=YES ALLUSERS=1", MsiArgs="/qn ALLUSERS=1",
                    ShortcutName="Adobe Acrobat", ShortcutTarget="%ProgramFiles%\\Adobe\\Acrobat DC\\Acrobat\\Acrobat.exe|%ProgramFiles%\\Adobe\\Acrobat Reader DC\\Reader\\AcroRd32.exe" },
                new AppEntry { Id="libreoffice", Name="LibreOffice", Category="Biurowe",
                    WingetId="TheDocumentFoundation.LibreOffice", Scope="machine", ExeArgs="/S", MsiArgs="/qn ALLUSERS=1",
                    ShortcutName="LibreOffice", ShortcutTarget="%ProgramFiles%\\LibreOffice\\program\\soffice.exe" },
                new AppEntry { Id="vlc", Name="VLC media player", Category="Multimedia",
                    WingetId="VideoLAN.VLC", Scope="machine", ExeArgs="/S", MsiArgs="/qn ALLUSERS=1",
                    ShortcutName="VLC media player", ShortcutTarget="%ProgramFiles%\\VideoLAN\\VLC\\vlc.exe" },
                new AppEntry { Id="everything", Name="Everything (voidtools)", Category="Narzedzia",
                    WingetId="voidtools.Everything", Scope="machine", ExeArgs="/S", MsiArgs="/qn ALLUSERS=1",
                    ShortcutName="Everything", ShortcutTarget="%ProgramFiles%\\Everything\\Everything.exe" },
                new AppEntry { Id="vscode", Name="Visual Studio Code", Category="Narzedzia",
                    WingetId="Microsoft.VisualStudioCode", Scope="machine",
                    ExeArgs="/VERYSILENT /NORESTART /MERGETASKS=!runcode /ALLUSERS", MsiArgs="/qn ALLUSERS=1",
                    DirectUrl="https://update.code.visualstudio.com/latest/win32-x64/stable",
                    ShortcutName="Visual Studio Code", ShortcutTarget="%ProgramFiles%\\Microsoft VS Code\\Code.exe" },
                new AppEntry { Id="notepadpp", Name="Notepad++", Category="Narzedzia",
                    WingetId="Notepad++.Notepad++", Scope="machine", ExeArgs="/S", MsiArgs="/qn ALLUSERS=1",
                    ShortcutName="Notepad++", ShortcutTarget="%ProgramFiles%\\Notepad++\\notepad++.exe" },
                new AppEntry { Id="inkscape", Name="Inkscape", Category="Grafika",
                    WingetId="Inkscape.Inkscape", Scope="machine", ExeArgs="/S", MsiArgs="/qn ALLUSERS=1",
                    ShortcutName="Inkscape", ShortcutTarget="%ProgramFiles%\\Inkscape\\bin\\inkscape.exe|%ProgramFiles%\\Inkscape\\inkscape.exe" },
                new AppEntry { Id="gimp", Name="GIMP 2.x", Category="Grafika",
                    WingetId="GIMP.GIMP.2", Scope="machine", ExeArgs="/VERYSILENT /NORESTART /ALLUSERS", MsiArgs="/qn ALLUSERS=1",
                    ShortcutName="GIMP", ShortcutTarget="%ProgramFiles%\\GIMP 2\\bin\\gimp-2.10.exe" },
                new AppEntry { Id="krita", Name="Krita 5.3.2.1", Category="Grafika",
                    WingetId="", Scope="machine", ExeArgs="/S", MsiArgs="/qn ALLUSERS=1",
                    DirectUrl="https://download.kde.org/stable/krita/5.3.2.1/krita-x64-5.3.2.1-setup.exe", DirectVersion="5.3.2.1",
                    ShortcutName="Krita", ShortcutTarget="%ProgramFiles%\\Krita (x64)\\bin\\krita.exe|%ProgramFiles%\\Krita\\bin\\krita.exe" },
                new AppEntry { Id="7zip", Name="7-Zip", Category="Narzedzia",
                    WingetId="7zip.7zip", Scope="machine", ExeArgs="/S", MsiArgs="/qn ALLUSERS=1",
                    ShortcutName="7-Zip File Manager", ShortcutTarget="%ProgramFiles%\\7-Zip\\7zFM.exe" }
            };
        }

        List<AppEntry> LoadDefs()
        {
            if (!File.Exists(AppsPath))
            {
                List<AppEntry> defs = DefaultApps();
                try { SaveDefs(defs); } catch { }
                return defs;
            }

            try
            {
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(AppsPath)) as Dictionary<string, object>;
                List<AppEntry> list = new List<AppEntry>();
                if (root != null && root.ContainsKey("apps"))
                {
                    foreach (object o in (object[])root["apps"])
                    {
                        Dictionary<string, object> d = o as Dictionary<string, object>;
                        if (d == null) continue;
                        AppEntry e = new AppEntry();
                        e.Id = Str(d, "id");
                        e.Name = Str(d, "name");
                        e.WingetId = Str(d, "wingetId");
                        AssignIfNotEmpty(d, "category", delegate(string v) { e.Category = v; });
                        AssignIfNotEmpty(d, "locale", delegate(string v) { e.Locale = v; });
                        AssignIfNotEmpty(d, "scope", delegate(string v) { e.Scope = v; });
                        AssignIfNotEmpty(d, "exeArgs", delegate(string v) { e.ExeArgs = v; });
                        AssignIfNotEmpty(d, "msiArgs", delegate(string v) { e.MsiArgs = v; });
                        AssignIfNotEmpty(d, "directUrl", delegate(string v) { e.DirectUrl = v; });
                        AssignIfNotEmpty(d, "directVersion", delegate(string v) { e.DirectVersion = v; });
                        AssignIfNotEmpty(d, "shortcutName", delegate(string v) { e.ShortcutName = v; });
                        AssignIfNotEmpty(d, "shortcutTarget", delegate(string v) { e.ShortcutTarget = v; });
                        AssignIfNotEmpty(d, "postInstall", delegate(string v) { e.PostInstall = v; });
                        object publicDesktop;
                        if (d.TryGetValue("publicDesktop", out publicDesktop))
                        {
                            try { e.PublicDesktop = Convert.ToBoolean(publicDesktop); } catch { }
                        }
                        if (e.Id.Length > 0 && e.Name.Length > 0 && (e.WingetId.Length > 0 || e.DirectUrl.Length > 0)) list.Add(e);
                    }
                }
                if (list.Count > 0) return list;
                Log("apps.json nie zawiera programow - uzywam listy wbudowanej.");
            }
            catch (Exception ex)
            {
                Log("Blad odczytu apps.json: " + ex.Message + " - uzywam listy wbudowanej.");
            }
            return DefaultApps();
        }

        static void AssignIfNotEmpty(Dictionary<string, object> d, string key, Action<string> setter)
        {
            string v = Str(d, key);
            if (v.Length > 0) setter(v);
        }

        void SaveDefs(List<AppEntry> defs)
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            foreach (AppEntry a in defs)
            {
                list.Add(new Dictionary<string, object> {
                    { "id", a.Id }, { "name", a.Name }, { "category", a.Category },
                    { "wingetId", a.WingetId }, { "locale", a.Locale }, { "scope", a.Scope },
                    { "exeArgs", a.ExeArgs }, { "msiArgs", a.MsiArgs }, { "directUrl", a.DirectUrl },
                    { "directVersion", a.DirectVersion },
                    { "publicDesktop", a.PublicDesktop }, { "shortcutName", a.ShortcutName },
                    { "shortcutTarget", a.ShortcutTarget }, { "postInstall", a.PostInstall }
                });
            }
            Dictionary<string, object> root = new Dictionary<string, object> { { "apps", list } };
            File.WriteAllText(AppsPath, PrettyJson(new JavaScriptSerializer().Serialize(root)), new UTF8Encoding(false));
        }

        List<AppEntry> LoadApps()
        {
            List<AppEntry> result = LoadDefs();
            try
            {
                if (!File.Exists(ManifestPath)) return result;
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(ManifestPath)) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("apps")) return result;
                foreach (object o in (object[])root["apps"])
                {
                    Dictionary<string, object> d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    string id = Str(d, "id");
                    AppEntry entry = result.Find(delegate(AppEntry a) { return a.Id == id; });
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
                    {
                        foreach (object dep in (object[])depsObj)
                            if (dep != null) entry.Deps.Add(dep.ToString());
                    }
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
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            foreach (AppEntry a in Apps)
            {
                if (string.IsNullOrEmpty(a.FileRel)) continue;
                bool isMsi = a.FileRel.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
                list.Add(new Dictionary<string, object> {
                    { "id", a.Id }, { "name", a.Name }, { "wingetId", a.WingetId },
                    { "category", a.Category }, { "version", a.Version }, { "file", a.FileRel },
                    { "sha256", a.Sha256 }, { "sizeMB", a.SizeMB },
                    { "silentArgs", isMsi ? a.MsiArgs : a.ExeArgs }, { "deps", a.Deps }
                });
            }
            Dictionary<string, object> root = new Dictionary<string, object> {
                { "updated", DateTime.Now.ToString("s") }, { "apps", list }
            };
            File.WriteAllText(ManifestPath, PrettyJson(new JavaScriptSerializer().Serialize(root)), new UTF8Encoding(false));
        }

        static string PrettyJson(string json)
        {
            StringBuilder sb = new StringBuilder();
            bool quoted = false;
            bool escaped = false;
            int indent = 0;
            foreach (char ch in json)
            {
                if (escaped) { sb.Append(ch); escaped = false; continue; }
                if (ch == '\\' && quoted) { sb.Append(ch); escaped = true; continue; }
                if (ch == '"') { quoted = !quoted; sb.Append(ch); continue; }
                if (quoted) { sb.Append(ch); continue; }
                if (ch == '{' || ch == '[')
                {
                    sb.Append(ch).AppendLine();
                    indent++;
                    sb.Append(new string(' ', indent * 2));
                }
                else if (ch == '}' || ch == ']')
                {
                    sb.AppendLine();
                    indent--;
                    sb.Append(new string(' ', indent * 2)).Append(ch);
                }
                else if (ch == ',')
                {
                    sb.Append(ch).AppendLine();
                    sb.Append(new string(' ', indent * 2));
                }
                else if (ch == ':') sb.Append(": ");
                else if (!char.IsWhiteSpace(ch)) sb.Append(ch);
            }
            return sb.ToString();
        }

        public void DownloadOne(AppEntry app)
        {
            string latest = string.IsNullOrEmpty(app.WingetId) && !string.IsNullOrEmpty(app.DirectVersion)
                ? app.DirectVersion : null;
            bool wingetAvailable = ResolveWinget() != null;
            if (wingetAvailable && !string.IsNullOrEmpty(app.WingetId))
            {
                latest = WingetLatestVersion(app.WingetId);
                if (latest != null) Log("Najnowsza wersja wedlug winget: " + latest);
                else Log("Nie udalo sie odczytac wersji z winget; mimo to probuje winget download.");
            }
            else if (!string.IsNullOrEmpty(app.WingetId))
            {
                Log("Winget niedostepny. Uzyje adresu bezposredniego, jezeli skonfigurowano.");
            }
            else if (!string.IsNullOrEmpty(app.DirectVersion))
            {
                Log("Skonfigurowana wersja instalatora bezposredniego: " + app.DirectVersion + ".");
            }

            string existing = app.InstallerPath(RepoDir);
            if (latest != null && latest == app.Version && existing != null && File.Exists(existing))
            {
                Log("Repo aktualne (" + latest + ") - pomijam.");
                return;
            }

            string tmp = Path.Combine(Path.GetTempPath(), "wdrozyciel", app.Id + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            bool ok = false;

            try
            {
                if (wingetAvailable && !string.IsNullOrEmpty(app.WingetId))
                    ok = WingetDownloadWithFallbacks(app, tmp);

                if (!ok && !string.IsNullOrEmpty(app.DirectUrl))
                {
                    Log("Pobieram bezposrednio od producenta: " + app.DirectUrl);
                    DownloadDirect(app.DirectUrl, tmp);
                    ok = AllInstallers(tmp).Count > 0;
                }

                if (!ok)
                {
                    Log("BLAD: nie udalo sie pobrac " + app.Name + ". Sprawdz internet, winget i identyfikator pakietu.");
                    return;
                }

                List<string> files = AllInstallers(tmp);
                if (files.Count == 0) { Log("BLAD: brak pliku instalatora po pobraniu."); return; }
                files.Sort(delegate(string a, string b) { return new FileInfo(b).Length.CompareTo(new FileInfo(a).Length); });
                string mainFile = files[0];

                string appRepo = Path.Combine(RepoDir, app.Id);
                string newRepo = appRepo + ".new";
                string oldRepo = appRepo + ".old";
                if (Directory.Exists(newRepo)) Directory.Delete(newRepo, true);
                Directory.CreateDirectory(newRepo);

                app.Deps.Clear();
                string mainDest = null;
                foreach (string f in files)
                {
                    string fileName = SafeFileName(Path.GetFileName(f));
                    string dest = UniqueDestination(newRepo, fileName);
                    File.Copy(f, dest, true);
                    if (f == mainFile) mainDest = dest;
                    else
                    {
                        app.Deps.Add(app.Id + "/" + Path.GetFileName(dest));
                        Log("Zaleznosc: " + Path.GetFileName(dest));
                    }
                }

                if (mainDest == null) { Log("BLAD: nie wybrano glownego instalatora."); return; }
                string detectedVersion = latest != null ? latest : InferVersion(mainDest);
                string detectedFileRel = app.Id + "/" + Path.GetFileName(mainDest);
                string detectedSha256 = Sha256Of(mainDest);
                double detectedSizeMB = Math.Round(new FileInfo(mainDest).Length / 1048576.0, 1);

                if (Directory.Exists(oldRepo)) Directory.Delete(oldRepo, true);
                bool movedOld = false;
                try
                {
                    if (Directory.Exists(appRepo)) { Directory.Move(appRepo, oldRepo); movedOld = true; }
                    Directory.Move(newRepo, appRepo);
                }
                catch
                {
                    try { if (Directory.Exists(appRepo)) Directory.Delete(appRepo, true); } catch { }
                    try { if (movedOld && Directory.Exists(oldRepo)) Directory.Move(oldRepo, appRepo); } catch { }
                    throw;
                }

                app.Version = detectedVersion;
                app.FileRel = detectedFileRel;
                app.Sha256 = detectedSha256;
                app.SizeMB = detectedSizeMB;
                Log(string.Format("Zapisano: {0} ({1} MB), wersja {2}", Path.GetFileName(mainDest), app.SizeMB, app.Version));
                try { if (Directory.Exists(oldRepo)) Directory.Delete(oldRepo, true); } catch { }
            }
            finally
            {
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            }
        }

        static string UniqueDestination(string dir, string fileName)
        {
            string dest = Path.Combine(dir, fileName);
            if (!File.Exists(dest)) return dest;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int i = 2;
            while (File.Exists(dest))
            {
                dest = Path.Combine(dir, stem + "-" + i + ext);
                i++;
            }
            return dest;
        }

        static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        static string InferVersion(string path)
        {
            try
            {
                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(vi.ProductVersion)) return vi.ProductVersion;
                if (!string.IsNullOrEmpty(vi.FileVersion)) return vi.FileVersion;
            }
            catch { }
            Match m = Regex.Match(Path.GetFileName(path), @"(?<!\d)(\d+(?:\.\d+){1,4})(?!\d)");
            return m.Success ? m.Groups[1].Value : "nieznana-" + DateTime.Now.ToString("yyyyMMdd");
        }

        string ResolveWinget()
        {
            if (wingetResolved) return wingetPath;
            wingetResolved = true;
            string output;
            if (RunCapture("where.exe", "winget", out output, 15000) == 0)
            {
                foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string p = line.Trim();
                    if (File.Exists(p)) { wingetPath = p; break; }
                }
            }
            if (wingetPath == null)
            {
                string candidate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
                if (File.Exists(candidate)) wingetPath = candidate;
            }
            if (wingetPath == null)
            {
                string ps = "$p=Get-AppxPackage Microsoft.DesktopAppInstaller -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty InstallLocation; if($p){Join-Path $p 'winget.exe'}";
                if (RunPowerShellCapture(ps, out output, 20000) == 0)
                {
                    string p = output.Trim();
                    if (File.Exists(p)) wingetPath = p;
                }
            }
            return wingetPath;
        }

        public string GetWingetPath()
        {
            return ResolveWinget();
        }

        void RefreshWingetSourceOnce()
        {
            if (sourceRefreshed || ResolveWinget() == null) return;
            sourceRefreshed = true;
            string output;
            int code = RunCapture(wingetPath,
                "source update --name winget --accept-source-agreements --disable-interactivity",
                out output, 120000);
            if (code != 0) Log("UWAGA: winget source update nie powiodl sie; kontynuuje z obecnym cache.");
        }

        string WingetLatestVersion(string wingetId)
        {
            RefreshWingetSourceOnce();
            string output;
            int code = RunCapture(ResolveWinget(),
                "show --id " + QuoteArg(wingetId) + " --exact --source winget --accept-source-agreements --disable-interactivity",
                out output, 120000);
            if (code != 0) return null;
            Match m = Regex.Match(output, @"(?mi)^\s*(?:Version|Wersja|Versi[oó]n|Versione|Vers[aã]o):\s*(\S+)");
            if (m.Success) return m.Groups[1].Value.Trim();
            m = Regex.Match(output, @"(?mi)^\s*(?:Found|Znaleziono).*?\[(?:[^\]]+)\]\s+Version\s+(\S+)");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        bool WingetDownloadWithFallbacks(AppEntry app, string dir)
        {
            RefreshWingetSourceOnce();
            List<string[]> attempts = new List<string[]>();
            attempts.Add(new[] { app.Locale, app.Scope, "x64" });
            if (!string.IsNullOrEmpty(app.Locale)) attempts.Add(new[] { "", app.Scope, "x64" });
            if (!string.IsNullOrEmpty(app.Scope)) attempts.Add(new[] { app.Locale, "", "x64" });
            attempts.Add(new[] { "", "", "x64" });
            attempts.Add(new[] { "", "", "" });

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string[] attempt in attempts)
            {
                string key = (attempt[0] ?? "") + "|" + (attempt[1] ?? "") + "|" + (attempt[2] ?? "");
                if (!seen.Add(key)) continue;
                ClearDirectory(dir);
                if (WingetDownload(app, dir, attempt[0], attempt[1], attempt[2])) return true;
            }
            return false;
        }

        static void ClearDirectory(string dir)
        {
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
                foreach (string d in Directory.GetDirectories(dir)) Directory.Delete(d, true);
                foreach (string f in Directory.GetFiles(dir)) File.Delete(f);
            }
            catch { }
        }

        bool WingetDownload(AppEntry app, string dir, string locale, string scope, string architecture)
        {
            string args = "download --id " + QuoteArg(app.WingetId) + " --exact --source winget" +
                          " --download-directory " + QuoteArg(dir) +
                          " --accept-package-agreements --accept-source-agreements --disable-interactivity";
            if (!string.IsNullOrEmpty(architecture)) args += " --architecture " + architecture;
            if (!string.IsNullOrEmpty(locale)) args += " --locale " + locale;
            if (!string.IsNullOrEmpty(scope)) args += " --scope " + scope;

            Log("winget download" +
                (!string.IsNullOrEmpty(locale) ? " locale=" + locale : "") +
                (!string.IsNullOrEmpty(scope) ? " scope=" + scope : "") +
                (!string.IsNullOrEmpty(architecture) ? " arch=" + architecture : ""));
            string output;
            int code = RunCapture(ResolveWinget(), args, out output, 15 * 60 * 1000);
            if (code != 0)
            {
                string last = LastUsefulLine(output);
                if (last.Length > 0) Log("winget: " + last);
            }
            return code == 0 && AllInstallers(dir).Count > 0;
        }

        static string LastUsefulLine(string output)
        {
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string s = lines[i].Trim();
                if (s.Length > 0 && !Regex.IsMatch(s, @"^[\-\\|/\s]+$")) return s;
            }
            return "";
        }

        void DownloadDirect(string url, string dir)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.AllowAutoRedirect = true;
            req.UserAgent = "Wdrozyciel/" + App.Version;
            req.Timeout = 120000;
            req.ReadWriteTimeout = 120000;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                string name = Uri.UnescapeDataString(Path.GetFileName(resp.ResponseUri.LocalPath));
                if (string.IsNullOrEmpty(name) ||
                    (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                     !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)))
                {
                    name = "installer" + (resp.ContentType.IndexOf("msi", StringComparison.OrdinalIgnoreCase) >= 0 || url.IndexOf(".msi", StringComparison.OrdinalIgnoreCase) >= 0 ? ".msi" : ".exe");
                }
                string dest = Path.Combine(dir, SafeFileName(name));
                using (Stream s = resp.GetResponseStream())
                using (FileStream f = File.Create(dest))
                {
                    byte[] buf = new byte[81920];
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
            List<string> result = new List<string>();
            if (!Directory.Exists(dir)) return result;
            foreach (string pat in new[] { "*.exe", "*.msi" })
                result.AddRange(Directory.GetFiles(dir, pat, SearchOption.AllDirectories));
            return result;
        }

        public bool InstallOne(AppEntry app, bool checkHash)
        {
            return InstallOne(app, checkHash, false);
        }

        public bool InstallOne(AppEntry app, bool checkHash, bool skipCurrent)
        {
            string path = app.InstallerPath(RepoDir);
            if (path == null || !File.Exists(path)) { Log("BLAD: brak instalatora w repo."); return false; }

            string installedVersion;
            if (skipCurrent && IsCurrentOrNewerInstalled(app, out installedVersion))
            {
                Log("POMINIETO: zainstalowana wersja " + installedVersion + " jest taka sama lub nowsza od repo " + app.Version + ".");
                RunPostInstall(app);
                return true;
            }

            if (checkHash && !string.IsNullOrEmpty(app.Sha256))
            {
                Status("Weryfikuje SHA256: " + app.Name + "...");
                if (!string.Equals(Sha256Of(path), app.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Log("BLAD: niezgodny SHA256 (uszkodzony lub podmieniony plik).");
                    return false;
                }
                Log("SHA256 OK.");
            }

            foreach (string dep in app.Deps)
            {
                string dpath = Path.Combine(RepoDir, dep.Replace('/', '\\'));
                if (!File.Exists(dpath)) { Log("UWAGA: brak zaleznosci " + dep + " - pomijam."); continue; }
                Status("Zaleznosc: " + Path.GetFileName(dpath) + "...");
                bool depMsi = dpath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
                int dcode = RunInstaller(dpath, depMsi ? "/qn ALLUSERS=1" : "/install /quiet /norestart", "machine");
                if (SuccessCode(dcode) || dcode == 1638)
                    Log("Zaleznosc " + Path.GetFileName(dpath) + ": OK (kod " + dcode + ").");
                else
                    Log("UWAGA: zaleznosc " + Path.GetFileName(dpath) + " zwrocila kod " + dcode + " - kontynuuje.");
            }

            Status("Instaluje: " + app.Name + "...");
            Stopwatch sw = Stopwatch.StartNew();
            int code;
            try
            {
                bool isMsi = path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
                string silent = isMsi ? app.MsiArgs : app.ExeArgs;
                if (string.IsNullOrEmpty(silent)) silent = app.ManifestArgs;
                code = RunInstaller(path, silent, app.Scope);
            }
            catch (Exception ex) { Log("BLAD uruchomienia: " + ex.Message); return false; }
            sw.Stop();

            if (!SuccessCode(code))
            {
                Log("BLAD: kod wyjscia " + code);
                return false;
            }

            Log(string.Format("OK - zainstalowano w {0}s{1}", (int)sw.Elapsed.TotalSeconds,
                code != 0 ? " (wymagany restart)" : ""));
            RunPostInstall(app);
            return true;
        }

        static bool SuccessCode(int code) { return code == 0 || code == 1638 || code == 3010 || code == 1641; }

        bool IsCurrentOrNewerInstalled(AppEntry app, out string installedVersion)
        {
            installedVersion = "";
            if (string.IsNullOrEmpty(app.Version) || string.IsNullOrEmpty(app.ShortcutTarget)) return false;

            string target = FindExistingTarget(app.ShortcutTarget);
            if (target == null) return false;
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(target);
                installedVersion = !string.IsNullOrEmpty(info.ProductVersion) ? info.ProductVersion : info.FileVersion;
                if (string.IsNullOrEmpty(installedVersion)) return false;
                return CompareNumericVersions(installedVersion, app.Version) >= 0;
            }
            catch { return false; }
        }

        static int CompareNumericVersions(string left, string right)
        {
            MatchCollection lm = Regex.Matches(left ?? "", @"\d+");
            MatchCollection rm = Regex.Matches(right ?? "", @"\d+");
            if (lm.Count == 0 || rm.Count == 0) return -1;
            int count = Math.Max(lm.Count, rm.Count);
            for (int i = 0; i < count; i++)
            {
                long lv = i < lm.Count ? ParseVersionPart(lm[i].Value) : 0;
                long rv = i < rm.Count ? ParseVersionPart(rm[i].Value) : 0;
                if (lv < rv) return -1;
                if (lv > rv) return 1;
            }
            return 0;
        }

        static long ParseVersionPart(string value)
        {
            long parsed;
            return long.TryParse(value, out parsed) ? parsed : 0;
        }

        static int RunInstaller(string path, string silentArgs, string scope)
        {
            bool isMsi = path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
            if (silentArgs == null) silentArgs = "";
            if (isMsi && string.Equals(scope, "machine", StringComparison.OrdinalIgnoreCase) &&
                silentArgs.IndexOf("ALLUSERS", StringComparison.OrdinalIgnoreCase) < 0)
                silentArgs += " ALLUSERS=1";

            ProcessStartInfo psi = isMsi
                ? new ProcessStartInfo("msiexec.exe", "/i " + QuoteArg(path) + " " + silentArgs + " /norestart")
                : new ProcessStartInfo(path, silentArgs);
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetDirectoryName(path);
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        void RunPostInstall(AppEntry app)
        {
            if (string.Equals(app.PostInstall, "firefox-auto-update", StringComparison.OrdinalIgnoreCase))
                ConfigureFirefoxAutoUpdate();
            if (app.PublicDesktop && !string.IsNullOrEmpty(app.ShortcutTarget))
                EnsurePublicShortcut(app);
        }

        void ConfigureFirefoxAutoUpdate()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Mozilla\Firefox"))
                {
                    key.SetValue("DisableAppUpdate", 0, RegistryValueKind.DWord);
                    key.SetValue("AppAutoUpdate", 1, RegistryValueKind.DWord);
                    key.SetValue("BackgroundAppUpdate", 1, RegistryValueKind.DWord);
                }

                string output;
                int query = RunCapture("sc.exe", "query MozillaMaintenance", out output, 30000);
                if (query != 0)
                {
                    string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    string installer = Path.Combine(pf, @"Mozilla Firefox\maintenanceservice_installer.exe");
                    if (!File.Exists(installer) && !string.IsNullOrEmpty(pf86))
                        installer = Path.Combine(pf86, @"Mozilla Firefox\maintenanceservice_installer.exe");
                    if (File.Exists(installer))
                    {
                        int serviceInstall = RunInstaller(installer, "/S", "machine");
                        Log("Firefox: instalator Mozilla Maintenance Service zakonczyl sie kodem " + serviceInstall + ".");
                    }
                }

                int config = RunCapture("sc.exe", "config MozillaMaintenance start= demand", out output, 30000);
                int verify = RunCapture("sc.exe", "query MozillaMaintenance", out output, 30000);
                if (verify == 0)
                    Log("Firefox: automatyczne aktualizacje sa wlaczone, a Mozilla Maintenance Service jest zainstalowana.");
                else
                    Log("UWAGA: polityki aktualizacji Firefox ustawiono, ale nie znaleziono Mozilla Maintenance Service. Kod konfiguracji: " + config + ".");
            }
            catch (Exception ex) { Log("UWAGA: nie udalo sie skonfigurowac aktualizacji Firefox: " + ex.Message); }
        }

        void EnsurePublicShortcut(AppEntry app)
        {
            try
            {
                string target = FindExistingTarget(app.ShortcutTarget);
                if (target == null)
                {
                    Log("UWAGA: nie znaleziono pliku programu do utworzenia publicznego skrotu.");
                    return;
                }
                string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                if (string.IsNullOrEmpty(publicDesktop)) publicDesktop = Path.Combine(Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public", "Desktop");
                Directory.CreateDirectory(publicDesktop);
                string shortcutName = string.IsNullOrEmpty(app.ShortcutName) ? app.Name : app.ShortcutName;
                string lnk = Path.Combine(publicDesktop, SafeFileName(shortcutName) + ".lnk");
                string ps = "$ws=New-Object -ComObject WScript.Shell;" +
                            "$s=$ws.CreateShortcut(" + PsQuote(lnk) + ");" +
                            "$s.TargetPath=" + PsQuote(target) + ";" +
                            "$s.WorkingDirectory=" + PsQuote(Path.GetDirectoryName(target)) + ";" +
                            "$s.IconLocation=" + PsQuote(target + ",0") + ";$s.Save()";
                string output;
                int code = RunPowerShellCapture(ps, out output, 30000);
                if (code == 0) Log("Utworzono skrot na pulpicie publicznym: " + shortcutName + ".");
                else Log("UWAGA: nie udalo sie utworzyc publicznego skrotu: " + LastUsefulLine(output));
            }
            catch (Exception ex) { Log("UWAGA: publiczny skrot: " + ex.Message); }
        }

        static string FindExistingTarget(string candidates)
        {
            foreach (string raw in candidates.Split('|'))
            {
                string path = Environment.ExpandEnvironmentVariables(raw.Trim());
                if (File.Exists(path)) return path;
                string dir = Path.GetDirectoryName(path);
                string file = Path.GetFileName(path);
                if (Directory.Exists(dir) && file.IndexOf('*') >= 0)
                {
                    string[] found = Directory.GetFiles(dir, file, SearchOption.TopDirectoryOnly);
                    if (found.Length > 0) return found[found.Length - 1];
                }
            }
            return null;
        }

        public static string Sha256Of(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
        }

        public static int RunCapture(string file, string args, out string output, int timeoutMs)
        {
            output = "";
            if (string.IsNullOrEmpty(file)) { output = "Nie znaleziono programu wykonawczego."; return -1; }
            ProcessStartInfo psi = new ProcessStartInfo(file, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            StringBuilder sb = new StringBuilder();
            try
            {
                using (Process p = Process.Start(psi))
                {
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        output = sb.ToString() + Environment.NewLine + "Przekroczono limit czasu.";
                        return -2;
                    }
                    p.WaitForExit();
                    output = sb.ToString();
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { output = ex.Message; return -1; }
        }

        public static int RunPowerShellCapture(string script, out string output, int timeoutMs)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return RunCapture("powershell.exe", "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, out output, timeoutMs);
        }

        public static int RunVisible(string file, string args, bool wait)
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args);
            psi.UseShellExecute = true;
            string workingDir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(workingDir)) psi.WorkingDirectory = workingDir;
            using (Process p = Process.Start(psi))
            {
                if (!wait) return 0;
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        public static string QuoteArg(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        public static string PsQuote(string value)
        {
            return "'" + (value ?? "").Replace("'", "''") + "'";
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }

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
            Engine eng = new Engine(DataLocation.Resolve(AppDomain.CurrentDomain.BaseDirectory));
            string logf = Path.Combine(eng.LogDir, string.Format("download-{0:yyyyMMdd-HHmmss}.log", DateTime.Now));
            eng.Log = delegate(string s)
            {
                try { File.AppendAllText(logf, string.Format("[{0:HH:mm:ss}] {1}\r\n", DateTime.Now, s)); } catch { }
            };
            foreach (AppEntry app in eng.Apps)
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
        static readonly string[] CategoryOrder = { "Przegladarki", "Biurowe", "Multimedia", "Grafika", "Narzedzia" };

        readonly Engine eng;
        TreeView tv;
        TextBox txtLog, txtHostname, txtDomUser, txtDomPass, txtToolsOutput, txtAppxNames, txtWingetIds;
        CheckedListBox clbScripts;
        ProgressBar pb;
        Label lblStatus, lblDomStatus, lblJoined, lblScriptFolder;
        Button btnAll, btnNone, btnDownload, btnInstall, btnPrepare, btnJoin, btnRename;
        Button btnScriptsRefresh, btnScriptsOpen, btnScriptsRun, btnWingetList, btnPower100, btnFastStartup;
        Button btnOfficeScrubber, btnMcAfee, btnAppxLoad, btnAppxRemove, btnWingetLoad, btnWingetRemove;
        TabControl toolsSubTabs;
        CheckBox chkHash, chkPrepareFast, chkPrepareOffice, chkPrepareUpdate, chkSkipInstalled;
        string logFilePath;
        bool domainChecking, suppressCheck;
        volatile bool domainUp;
        volatile bool restartPending;
        volatile bool workerRunning;

        public MainForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = App.Title + " - wersja " + App.Version;
            ClientSize = new Size(920, 780);
            MinimumSize = new Size(840, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            eng = new Engine(DataLocation.Resolve(AppDomain.CurrentDomain.BaseDirectory));
            logFilePath = Path.Combine(eng.LogDir, string.Format("gui-{0}-{1:yyyyMMdd-HHmmss}.log", Environment.MachineName, DateTime.Now));
            eng.Log = Log;
            eng.Status = Status;
            FormClosing += MainFormClosing;

            TabControl tabs = new TabControl { Location = new Point(10, 10), Size = new Size(900, 500), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            TabPage tabApps = new TabPage("Programy i domena");
            TabPage tabTools = new TabPage("Skrypty i narzedzia");
            tabs.TabPages.Add(tabApps);
            tabs.TabPages.Add(tabTools);
            BuildAppsTab(tabApps);
            BuildToolsTab(tabTools);

            lblStatus = new Label { Text = "Gotowy.", Location = new Point(12, 520), AutoSize = true };
            pb = new ProgressBar { Location = new Point(12, 541), Size = new Size(896, 18), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            txtLog = new TextBox { Location = new Point(12, 570), Size = new Size(896, 198), Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f), BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            Controls.AddRange(new Control[] { tabs, lblStatus, pb, txtLog });

            RefreshList(true);
            RefreshScripts();
            UpdateJoinedLabel();
            Log(App.Title + " " + App.Version + " | Repo: " + eng.RepoDir);
            Log("Skrypty PowerShell: " + eng.ScriptsDir + " (program dziala jako administrator).");

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += delegate { CheckDomainAsync(); };
            timer.Start();
            CheckDomainAsync();
        }

        void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (workerRunning && MessageBox.Show(
                "Przygotowanie lub inne zadanie nadal trwa. Zamkniecie aplikacji moze przerwac prace.\n\nMimo to zamknac?",
                App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            if (!restartPending) return;
            if (MessageBox.Show(
                "Zmiana nazwy komputera lub dolaczenie do domeny wymaga restartu.\n\nUruchomic komputer ponownie teraz?",
                App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            restartPending = false;
            try { Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true }); }
            catch (Exception ex) { MessageBox.Show("Nie udalo sie uruchomic ponownie komputera: " + ex.Message, App.Title, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void BuildAppsTab(TabPage tab)
        {
            Label lblTop = new Label {
                Text = "Zaznacz programy, potem: Pobierz (online) lub Zainstaluj (offline z repo). Instalacja jest maszynowa.",
                Location = new Point(10, 10), AutoSize = true
            };
            tv = new TreeView { Location = new Point(10, 35), Size = new Size(610, 318), CheckBoxes = true,
                ShowLines = false, FullRowSelect = true, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            tv.AfterCheck += delegate(object s, TreeViewEventArgs e)
            {
                if (suppressCheck) return;
                suppressCheck = true;
                if (e.Node.Tag == null)
                    foreach (TreeNode ch in e.Node.Nodes) ch.Checked = e.Node.Checked;
                else if (e.Node.Parent != null)
                {
                    bool all = true;
                    foreach (TreeNode ch in e.Node.Parent.Nodes) if (!ch.Checked) { all = false; break; }
                    e.Node.Parent.Checked = all;
                }
                suppressCheck = false;
            };

            int bx = 635;
            btnAll = new Button { Text = "Zaznacz wszystko", Location = new Point(bx, 35), Size = new Size(235, 30) };
            btnNone = new Button { Text = "Odznacz wszystko", Location = new Point(bx, 70), Size = new Size(235, 30) };
            btnDownload = new Button { Text = "POBIERZ aktualne wersje\r\n(wymaga internetu)", Location = new Point(bx, 112), Size = new Size(235, 50) };
            btnInstall = new Button { Text = "ZAINSTALUJ zaznaczone\r\n(offline, z repo)", Location = new Point(bx, 170), Size = new Size(235, 50), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            chkHash = new CheckBox { Text = "Weryfikuj sumy SHA256", Location = new Point(bx, 228), Size = new Size(235, 20), Checked = true };

            GroupBox prepare = new GroupBox { Text = "Szybkie przygotowanie offline", Location = new Point(10, 360), Size = new Size(610, 95) };
            chkPrepareFast = new CheckBox { Text = "Wylacz szybkie uruchamianie", Location = new Point(12, 22), Size = new Size(195, 22), Checked = true };
            chkPrepareOffice = new CheckBox { Text = "Wyczysc fabryczny Office", Location = new Point(12, 50), Size = new Size(195, 22), Checked = true };
            chkPrepareUpdate = new CheckBox { Text = "Otworz Windows Update", Location = new Point(215, 22), Size = new Size(180, 22), Checked = true };
            chkSkipInstalled = new CheckBox { Text = "Pomin aktualne programy", Location = new Point(215, 50), Size = new Size(180, 22), Checked = true };
            btnPrepare = new Button { Text = "PRZYGOTUJ KOMPUTER\r\n(offline)", Location = new Point(405, 22), Size = new Size(190, 52), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            prepare.Controls.AddRange(new Control[] { chkPrepareFast, chkPrepareOffice, chkPrepareUpdate, chkSkipInstalled, btnPrepare });

            GroupBox grp = new GroupBox { Text = "Domena AD", Location = new Point(bx, 255), Size = new Size(235, 200) };
            Label lblDomName = new Label { Text = Domain, Location = new Point(10, 20), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            lblDomStatus = new Label { Text = "Sprawdzam domene...", Location = new Point(10, 40), AutoSize = true, ForeColor = Color.Gray };
            txtHostname = new TextBox { Location = new Point(10, 64), Size = new Size(215, 23), Text = Environment.MachineName };
            btnRename = new Button { Text = "Zmien tylko nazwe", Location = new Point(10, 91), Size = new Size(215, 25) };
            txtDomUser = new TextBox { Location = new Point(10, 121), Size = new Size(215, 23) };
            txtDomPass = new TextBox { Location = new Point(10, 148), Size = new Size(215, 23), UseSystemPasswordChar = true };
            btnJoin = new Button { Text = "DODAJ DO DOMENY", Location = new Point(10, 174), Size = new Size(215, 25), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            lblJoined = new Label { Text = "", Location = new Point(10, 202), AutoSize = true, ForeColor = Color.DimGray };
            grp.Controls.AddRange(new Control[] { lblDomName, lblDomStatus, txtHostname, btnRename, txtDomUser, txtDomPass, btnJoin });

            tab.Controls.AddRange(new Control[] { lblTop, tv, prepare, btnAll, btnNone, btnDownload, btnInstall, chkHash, grp, lblJoined });
            lblJoined.Location = new Point(bx + 2, 460);

            btnAll.Click += delegate { CheckAllNodes(true); };
            btnNone.Click += delegate { CheckAllNodes(false); };
            btnDownload.Click += delegate
            {
                List<AppEntry> selected = Selected();
                if (selected.Count == 0) { MessageBox.Show("Nie zaznaczono zadnego programu.", App.Title); return; }
                RunWorker(delegate { DownloadSelected(selected); }, false);
            };
            btnInstall.Click += delegate
            {
                List<AppEntry> selected = Selected();
                bool verifyHash = chkHash.Checked;
                bool skipCurrent = chkSkipInstalled.Checked;
                if (selected.Count == 0) { MessageBox.Show("Nie zaznaczono zadnego programu.", App.Title); return; }
                List<string> missing = MissingOfflineFiles(selected, false);
                if (!ConfirmOfflineFiles(missing)) return;
                RunWorker(delegate { InstallSelected(selected, verifyHash, skipCurrent); }, false);
            };
            btnPrepare.Click += delegate { PrepareClicked(); };
            btnJoin.Click += delegate { JoinDomainClicked(); };
            btnRename.Click += delegate { RenameClicked(); };

            Native.SendMessage(txtDomUser.Handle, 0x1501, (IntPtr)1, "login domenowy");
            Native.SendMessage(txtDomPass.Handle, 0x1501, (IntPtr)1, "haslo");
            Native.SendMessage(txtHostname.Handle, 0x1501, (IntPtr)1, "nazwa komputera");
        }

        void BuildToolsTab(TabPage tab)
        {
            GroupBox scripts = new GroupBox { Text = "Lokalne skrypty PowerShell (.ps1)", Location = new Point(10, 10), Size = new Size(425, 445) };
            lblScriptFolder = new Label { Text = eng.ScriptsDir, Location = new Point(10, 22), Size = new Size(405, 36), AutoEllipsis = true };
            clbScripts = new CheckedListBox { Location = new Point(10, 62), Size = new Size(405, 300), CheckOnClick = true };
            btnScriptsRefresh = new Button { Text = "Odswiez", Location = new Point(10, 372), Size = new Size(90, 30) };
            btnScriptsOpen = new Button { Text = "Otworz folder", Location = new Point(105, 372), Size = new Size(110, 30) };
            btnScriptsRun = new Button { Text = "URUCHOM ZAZNACZONE", Location = new Point(220, 372), Size = new Size(195, 30), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            Label scriptInfo = new Label { Text = "Nazwa na liscie = nazwa pliku bez rozszerzenia. PowerShell dziedziczy uprawnienia administratora.", Location = new Point(10, 407), Size = new Size(405, 34) };
            scripts.Controls.AddRange(new Control[] { lblScriptFolder, clbScripts, btnScriptsRefresh, btnScriptsOpen, btnScriptsRun, scriptInfo });

            GroupBox system = new GroupBox { Text = "System i usuwanie", Location = new Point(445, 10), Size = new Size(425, 445) };
            btnWingetList = new Button { Text = "Lista aplikacji (winget list)", Location = new Point(10, 25), Size = new Size(195, 34) };
            btnPower100 = new Button { Text = "Procesor: maksimum 100% (AC/DC)", Location = new Point(215, 25), Size = new Size(200, 34) };
            btnFastStartup = new Button { Text = "Wylacz szybkie uruchamianie", Location = new Point(10, 66), Size = new Size(195, 34) };
            btnOfficeScrubber = new Button { Text = "Uruchom OfficeScrubber", Location = new Point(215, 66), Size = new Size(200, 34) };
            btnMcAfee = new Button { Text = "Uruchom lokalny McAfee MCPR", Location = new Point(10, 107), Size = new Size(405, 34) };

            toolsSubTabs = new TabControl { Location = new Point(10, 150), Size = new Size(405, 284) };
            TabPage outputTab = new TabPage("Wynik");
            TabPage wingetTab = new TabPage("Usuwanie winget");
            TabPage appxTab = new TabPage("Usuwanie Appx");
            toolsSubTabs.TabPages.Add(outputTab);
            toolsSubTabs.TabPages.Add(wingetTab);
            toolsSubTabs.TabPages.Add(appxTab);

            txtToolsOutput = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 8f) };
            outputTab.Controls.Add(txtToolsOutput);

            Label wingetLabel = new Label { Text = "Dokladne ID winget, po jednym w wierszu:", Location = new Point(8, 10), Size = new Size(370, 20) };
            txtWingetIds = new TextBox { Location = new Point(8, 33), Size = new Size(373, 145), Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f) };
            btnWingetLoad = new Button { Text = "Wczytaj winget-remove-defaults.txt", Location = new Point(8, 188), Size = new Size(182, 32) };
            btnWingetRemove = new Button { Text = "ODINSTALUJ WSKAZANE ID", Location = new Point(196, 188), Size = new Size(185, 32), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            Label wingetInfo = new Label { Text = "ID skopiujesz z zakladki Wynik po wykonaniu winget list.", Location = new Point(8, 226), Size = new Size(373, 22) };
            wingetTab.Controls.AddRange(new Control[] { wingetLabel, txtWingetIds, btnWingetLoad, btnWingetRemove, wingetInfo });

            Label appxLabel = new Label { Text = "Maski Appx do usuniecia (wiersze lub przecinki):", Location = new Point(8, 10), Size = new Size(373, 20) };
            txtAppxNames = new TextBox { Location = new Point(8, 33), Size = new Size(373, 145), Multiline = true, ScrollBars = ScrollBars.Vertical };
            btnAppxLoad = new Button { Text = "Wczytaj appx-remove-defaults.txt", Location = new Point(8, 188), Size = new Size(182, 32) };
            btnAppxRemove = new Button { Text = "USUN DLA WSZYSTKICH", Location = new Point(196, 188), Size = new Size(185, 32), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            Label appxInfo = new Label { Text = "Usuwa tez pakiet provisioned dla nowych uzytkownikow.", Location = new Point(8, 226), Size = new Size(373, 22) };
            appxTab.Controls.AddRange(new Control[] { appxLabel, txtAppxNames, btnAppxLoad, btnAppxRemove, appxInfo });

            system.Controls.AddRange(new Control[] { btnWingetList, btnPower100, btnFastStartup, btnOfficeScrubber, btnMcAfee, toolsSubTabs });
            tab.Controls.AddRange(new Control[] { scripts, system });

            btnScriptsRefresh.Click += delegate { RefreshScripts(); };
            btnScriptsOpen.Click += delegate { OpenFolder(eng.ScriptsDir); };
            btnScriptsRun.Click += delegate { RunSelectedScripts(); };
            btnWingetList.Click += delegate { RunAdminWorker(ListInstalledApps); };
            btnPower100.Click += delegate { RunAdminWorker(SetProcessorMaximum); };
            btnFastStartup.Click += delegate { RunAdminWorker(DisableFastStartup); };
            btnOfficeScrubber.Click += delegate
            {
                bool? fullCleanup = ChooseOfficeCleanupMode();
                if (fullCleanup.HasValue)
                    RunAdminWorker(delegate { LaunchOfficeScrubber(fullCleanup.Value); });
            };
            btnMcAfee.Click += delegate { RunAdminWorker(RunLocalMcAfee); };
            btnWingetLoad.Click += delegate { LoadDefaultWingetIds(); };
            btnWingetRemove.Click += delegate { RemoveWingetApps(); };
            btnAppxLoad.Click += delegate { LoadDefaultAppx(); };
            btnAppxRemove.Click += delegate { RemoveAppxForAllUsers(); };
        }

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
                using (TcpClient tcp = new TcpClient())
                {
                    IAsyncResult ar = tcp.BeginConnect(Domain, 389, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(2000)) return false;
                    tcp.EndConnect(ar);
                    return true;
                }
            }
            catch { return false; }
        }

        static bool ValidHostname(string name) { return Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9-]{0,14}$"); }
        static bool ExpectedHostname(string name) { return Regex.IsMatch(name, @"^[A-Za-z0-9]+-\d{4}$"); }
        bool ConfirmHostnameFormat(string name)
        {
            return ExpectedHostname(name) || MessageBox.Show(
                "Nazwa komputera nie pasuje do oczekiwanego formatu SKROT-1234.\n\nKontynuowac z nazwa " + name + "?",
                App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }
        void SetDomainButtons(bool enabled) { btnJoin.Enabled = btnRename.Enabled = enabled; }

        void JoinDomainClicked()
        {
            string host = txtHostname.Text.Trim();
            string user = txtDomUser.Text.Trim();
            string pass = txtDomPass.Text;
            if (user.Length == 0 || pass.Length == 0) { MessageBox.Show("Podaj login i haslo konta domenowego.", App.Title); return; }
            if (!ValidHostname(host)) { MessageBox.Show("Nieprawidlowa nazwa komputera (1-15 znakow: litery, cyfry, myslnik).", App.Title); return; }
            if (!ConfirmHostnameFormat(host)) return;
            if (!domainUp && MessageBox.Show("Domena wyglada na niedostepna. Kontynuowac mimo to?", App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            string account = (user.Contains("\\") || user.Contains("@")) ? user : user + "@" + Domain;
            bool currentlyInDomain;
            Native.GetJoinInfo(out currentlyInDomain);
            SetDomainButtons(false);
            Thread t = new Thread(new ThreadStart(delegate
            {
                try
                {
                    bool renamed = false;
                    if (!host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    {
                        Log("Zmiana nazwy komputera na " + host + " przed dolaczeniem do domeny...");
                        int rr = WmiRename(host, currentlyInDomain ? account : null, currentlyInDomain ? pass : null);
                        if (rr != 0) { Log("BLAD zmiany nazwy (kod " + rr + ") - przerwano dolaczanie do domeny."); return; }
                        Log("Nazwa zmieniona na " + host + ".");
                        renamed = true;
                        restartPending = true;
                    }

                    Log("Dolaczanie do domeny " + Domain + " jako " + account + "...");
                    uint rc = Native.NetJoinDomain(null, Domain, null, account, pass, 0x23);
                    if (rc != 0) { Log("BLAD dolaczania: " + JoinError(rc)); return; }
                    restartPending = true;
                    Log("Dolaczono do domeny " + Domain + ".");
                    UpdateJoinedLabel();
                    UI(delegate { MessageBox.Show("Dodano do domeny " + Domain + (renamed ? "\nNowa nazwa: " + host : "") + "\n\nUruchom ponownie komputer, aby dokonczyc.", App.Title, MessageBoxButtons.OK, MessageBoxIcon.Information); });
                }
                catch (Exception ex) { Log("BLAD operacji domenowej: " + ex.Message); }
                finally { UI(delegate { txtDomPass.Clear(); SetDomainButtons(true); }); }
            }));
            t.IsBackground = true;
            t.Start();
        }

        void RenameClicked()
        {
            string host = txtHostname.Text.Trim();
            if (!ValidHostname(host)) { MessageBox.Show("Nieprawidlowa nazwa komputera (1-15 znakow: litery, cyfry, myslnik).", App.Title); return; }
            if (!ConfirmHostnameFormat(host)) return;
            if (host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Komputer juz nazywa sie " + host + ".", App.Title); return; }

            bool inDom;
            Native.GetJoinInfo(out inDom);
            string user = txtDomUser.Text.Trim();
            string pass = txtDomPass.Text;
            string account = (inDom && user.Length > 0) ? ((user.Contains("\\") || user.Contains("@")) ? user : user + "@" + Domain) : null;
            string accPass = account != null ? pass : null;

            SetDomainButtons(false);
            Thread t = new Thread(new ThreadStart(delegate
            {
                try
                {
                    Log("Zmiana nazwy komputera na " + host + "...");
                    int rr = WmiRename(host, account, accPass);
                    if (rr == 0)
                    {
                        restartPending = true;
                        Log("Nazwa zmieniona na " + host + ".");
                        UI(delegate { MessageBox.Show("Nazwa zmieniona na " + host + ".\nUruchom ponownie komputer.", App.Title, MessageBoxButtons.OK, MessageBoxIcon.Information); });
                    }
                    else Log("BLAD zmiany nazwy: kod " + rr + (inDom && account == null ? " (komputer w domenie - podaj login i haslo domenowe)" : ""));
                }
                catch (Exception ex) { Log("BLAD zmiany nazwy: " + ex.Message); }
                finally { UI(delegate { txtDomPass.Clear(); SetDomainButtons(true); }); }
            }));
            t.IsBackground = true;
            t.Start();
        }

        static int WmiRename(string newName, string user, string pass)
        {
            using (ManagementObject cs = new ManagementObject(new ManagementPath("Win32_ComputerSystem.Name='" + Environment.MachineName + "'")))
            {
                ManagementBaseObject inp = cs.GetMethodParameters("Rename");
                inp["Name"] = newName;
                inp["UserName"] = user;
                inp["Password"] = pass;
                ManagementBaseObject ret = cs.InvokeMethod("Rename", inp, null);
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

        void UI(Action a) { if (InvokeRequired) BeginInvoke((MethodInvoker)delegate { a(); }); else a(); }

        void Log(string msg)
        {
            string line = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, msg);
            UI(delegate { if (txtLog != null) txtLog.AppendText(line + Environment.NewLine); });
            try { File.AppendAllText(logFilePath, line + Environment.NewLine); } catch { }
        }

        void Status(string s) { UI(delegate { if (lblStatus != null) lblStatus.Text = s; }); }
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
                HashSet<string> wasChecked = new HashSet<string>();
                foreach (TreeNode cat in tv.Nodes)
                    foreach (TreeNode n in cat.Nodes)
                        if (n.Checked && n.Tag is AppEntry) wasChecked.Add(((AppEntry)n.Tag).Id);

                List<string> categories = new List<string>(CategoryOrder);
                foreach (AppEntry a in eng.Apps) if (!categories.Contains(a.Category)) categories.Add(a.Category);
                suppressCheck = true;
                tv.BeginUpdate();
                tv.Nodes.Clear();
                foreach (string cat in categories)
                {
                    TreeNode catNode = new TreeNode(cat) { NodeFont = new Font(Font, FontStyle.Bold) };
                    foreach (AppEntry a in eng.Apps)
                    {
                        if (a.Category != cat) continue;
                        string path = a.InstallerPath(eng.RepoDir);
                        string status = path != null && File.Exists(path)
                            ? string.Format("v{0}, {1} MB - gotowy", a.Version, a.SizeMB)
                            : "BRAK W REPO - kliknij Pobierz";
                        catNode.Nodes.Add(new TreeNode(string.Format("{0}   [{1}]", a.Name, status)) { Tag = a, Checked = checkAll || wasChecked.Contains(a.Id) });
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
            List<AppEntry> result = new List<AppEntry>();
            foreach (TreeNode cat in tv.Nodes)
                foreach (TreeNode n in cat.Nodes)
                    if (n.Checked && n.Tag is AppEntry) result.Add((AppEntry)n.Tag);
            return result;
        }

        void RunWorker(Action work, bool requireSelection)
        {
            if (requireSelection && Selected().Count == 0) { MessageBox.Show("Nie zaznaczono zadnego programu.", App.Title); return; }
            SetMainButtons(false);
            workerRunning = true;
            Thread t = new Thread(new ThreadStart(delegate
            {
                try { work(); }
                catch (Exception ex) { Log("BLAD KRYTYCZNY: " + ex); }
                finally { workerRunning = false; UI(delegate { SetMainButtons(true); }); Status("Gotowy."); }
            }));
            t.IsBackground = true;
            t.Start();
        }

        void RunAdminWorker(Action work) { RunWorker(work, false); }

        void SetMainButtons(bool enabled)
        {
            btnDownload.Enabled = btnInstall.Enabled = btnPrepare.Enabled = btnAll.Enabled = btnNone.Enabled = enabled;
            btnScriptsRefresh.Enabled = btnScriptsOpen.Enabled = btnScriptsRun.Enabled = enabled;
            btnWingetList.Enabled = btnPower100.Enabled = btnFastStartup.Enabled = btnOfficeScrubber.Enabled = btnMcAfee.Enabled = enabled;
            btnWingetLoad.Enabled = btnWingetRemove.Enabled = btnAppxLoad.Enabled = btnAppxRemove.Enabled = enabled;
        }

        List<string> MissingOfflineFiles(List<AppEntry> apps, bool includeOfficeScrubber)
        {
            List<string> missing = new List<string>();
            foreach (AppEntry app in apps)
            {
                string installer = app.InstallerPath(eng.RepoDir);
                if (installer == null || !File.Exists(installer)) missing.Add(app.Name + " - instalator");
                foreach (string dep in app.Deps)
                {
                    string dependency = Path.Combine(eng.RepoDir, dep.Replace('/', '\\'));
                    if (!File.Exists(dependency)) missing.Add(app.Name + " - " + Path.GetFileName(dependency));
                }
            }
            if (includeOfficeScrubber)
            {
                string office = Path.Combine(eng.ToolsDir, "OfficeScrubber", "OfficeScrubberAIO.cmd");
                if (!File.Exists(office)) missing.Add("OfficeScrubberAIO.cmd");
            }
            return missing;
        }

        bool ConfirmOfflineFiles(List<string> missing)
        {
            if (missing.Count == 0) return true;
            MessageBox.Show(
                "Nie mozna rozpoczac pracy offline. Brakuje plikow:\n\n" + string.Join("\n", missing.ToArray()) +
                "\n\nUzupelnij ukryty folder .wdrozyciel i sprobuj ponownie.",
                App.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        void PrepareClicked()
        {
            List<AppEntry> selected = Selected();
            bool verifyHash = chkHash.Checked;
            bool disableFast = chkPrepareFast.Checked;
            bool cleanOffice = chkPrepareOffice.Checked;
            bool openUpdate = chkPrepareUpdate.Checked;
            bool skipCurrent = chkSkipInstalled.Checked;

            if (selected.Count == 0 && !disableFast && !cleanOffice && !openUpdate)
            {
                MessageBox.Show("Nie wybrano zadnego zadania.", App.Title);
                return;
            }
            if (!ConfirmOfflineFiles(MissingOfflineFiles(selected, cleanOffice))) return;

            string summary = "Uruchomic przygotowanie komputera?\n\n" +
                "Programy z lokalnego repo: " + selected.Count + "\n" +
                "Weryfikacja SHA256: " + (verifyHash ? "tak" : "nie") + "\n" +
                "Pomijanie aktualnych wersji: " + (skipCurrent ? "tak" : "nie") + "\n" +
                "Szybkie uruchamianie: " + (disableFast ? "wylacz" : "bez zmian") + "\n" +
                "Fabryczny Office: " + (cleanOffice ? "usun" : "bez zmian") + "\n" +
                "Windows Update: " + (openUpdate ? "otworz po instalacji" : "nie otwieraj");
            if (cleanOffice)
                summary += "\n\nUWAGA: czyszczenie Office usuwa preinstalowany Microsoft 365/Click-to-Run, aplikacje UWP, ich licencje i ustawienia.";

            if (MessageBox.Show(summary, App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            RunWorker(delegate { PrepareComputer(selected, verifyHash, skipCurrent, disableFast, cleanOffice, openUpdate); }, false);
        }

        void PrepareComputer(List<AppEntry> selected, bool verifyHash, bool skipCurrent, bool disableFast, bool cleanOffice, bool openUpdate)
        {
            int total = selected.Count + (disableFast ? 1 : 0) + (cleanOffice ? 1 : 0) + (openUpdate ? 1 : 0);
            int done = 0;
            int okCount;
            List<string> failed;
            bool officeOk = true;
            Progress(0, Math.Max(1, total));
            Log("=== START SZYBKIEGO PRZYGOTOWANIA OFFLINE na " + Environment.MachineName + " ===");

            if (disableFast)
            {
                DisableFastStartup();
                Progress(++done, total);
            }

            InstallApplicationsCore(selected, verifyHash, skipCurrent, ref done, total, out okCount, out failed);

            if (cleanOffice)
            {
                officeOk = LaunchOfficeScrubber(false, false);
                Progress(++done, total);
            }

            if (openUpdate)
            {
                OpenWindowsUpdate();
                Progress(++done, total);
            }

            Log(string.Format("=== KONIEC PRZYGOTOWANIA: programy {0}/{1} OK{2}{3} ===", okCount, selected.Count,
                failed.Count > 0 ? " | niepowodzenia: " + string.Join(", ", failed.ToArray()) : "",
                cleanOffice && !officeOk ? " | OfficeScrubber: blad" : ""));
            UI(delegate
            {
                string message = string.Format("Przygotowanie zakonczone.\nProgramy: {0} z {1} OK.", okCount, selected.Count);
                if (failed.Count > 0) message += "\nNiepowodzenia: " + string.Join(", ", failed.ToArray());
                if (cleanOffice && !officeOk) message += "\nCzyszczenie Office nie powiodlo sie - sprawdz log.";
                MessageBox.Show(message, App.Title, MessageBoxButtons.OK,
                    failed.Count > 0 || (cleanOffice && !officeOk) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            });
        }

        void OpenWindowsUpdate()
        {
            Status("Otwieram Windows Update...");
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("ms-settings:windowsupdate");
                psi.UseShellExecute = true;
                Process.Start(psi);
                Log("Otwarto Windows Update.");
            }
            catch (Exception ex) { Log("UWAGA: nie udalo sie otworzyc Windows Update: " + ex.Message); }
        }

        void DownloadSelected(List<AppEntry> sel)
        {
            int done = 0;
            Progress(0, sel.Count);
            foreach (AppEntry app in sel)
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

        void InstallSelected(List<AppEntry> sel, bool verifyHash, bool skipCurrent)
        {
            int done = 0, okCount;
            List<string> failed;
            Progress(0, sel.Count);
            Log("=== START WDROZENIA na " + Environment.MachineName + " ===");
            InstallApplicationsCore(sel, verifyHash, skipCurrent, ref done, sel.Count, out okCount, out failed);
            Log(string.Format("=== KONIEC: {0}/{1} OK{2} ===", okCount, sel.Count,
                failed.Count > 0 ? " | niepowodzenia: " + string.Join(", ", failed.ToArray()) : ""));
            UI(delegate
            {
                MessageBox.Show(string.Format("Zainstalowano lub pominieto jako aktualne: {0} z {1}.{2}", okCount, sel.Count,
                    failed.Count > 0 ? "\nNiepowodzenia: " + string.Join(", ", failed.ToArray()) : ""),
                    App.Title, MessageBoxButtons.OK, failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            });
        }

        void InstallApplicationsCore(List<AppEntry> sel, bool verifyHash, bool skipCurrent, ref int done, int total, out int okCount, out List<string> failed)
        {
            okCount = 0;
            failed = new List<string>();
            foreach (AppEntry app in sel)
            {
                Log("--- " + app.Name + " " + app.Version + " ---");
                bool ok = false;
                try { ok = eng.InstallOne(app, verifyHash, skipCurrent); }
                catch (Exception ex) { Log("BLAD: " + ex.Message); }
                if (ok) okCount++; else failed.Add(app.Name);
                Progress(++done, Math.Max(1, total));
            }
        }

        void RefreshScripts()
        {
            Directory.CreateDirectory(eng.ScriptsDir);
            UI(delegate
            {
                clbScripts.Items.Clear();
                foreach (string file in Directory.GetFiles(eng.ScriptsDir, "*.ps1", SearchOption.TopDirectoryOnly))
                    clbScripts.Items.Add(new ScriptItem(file), false);
                lblScriptFolder.Text = eng.ScriptsDir + " | znaleziono: " + clbScripts.Items.Count;
            });
        }

        void OpenFolder(string path)
        {
            try { Directory.CreateDirectory(path); Process.Start("explorer.exe", Engine.QuoteArg(path)); }
            catch (Exception ex) { MessageBox.Show(ex.Message, App.Title, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void RunSelectedScripts()
        {
            List<ScriptItem> selected = new List<ScriptItem>();
            foreach (object item in clbScripts.CheckedItems) selected.Add((ScriptItem)item);
            if (selected.Count == 0) { MessageBox.Show("Nie zaznaczono zadnego skryptu PowerShell.", App.Title); return; }
            RunWorker(delegate
            {
                int done = 0;
                Progress(0, selected.Count);
                foreach (ScriptItem item in selected)
                {
                    Status("PowerShell: " + item.ToString() + "...");
                    Log("=== SKRYPT: " + item.ToString() + " ===");
                    int code = Engine.RunVisible("powershell.exe", "-NoLogo -NoProfile -ExecutionPolicy Bypass -File " + Engine.QuoteArg(item.Path), true);
                    Log("Skrypt zakonczony kodem " + code + ".");
                    Progress(++done, selected.Count);
                }
            }, false);
        }

        void ListInstalledApps()
        {
            Status("Odczytuje winget list...");
            string output;
            string winget = eng.GetWingetPath();
            int code = Engine.RunCapture(winget, "list --accept-source-agreements --disable-interactivity", out output, 180000);
            string path = Path.Combine(eng.LogDir, string.Format("winget-list-{0:yyyyMMdd-HHmmss}.txt", DateTime.Now));
            File.WriteAllText(path, output, new UTF8Encoding(false));
            UI(delegate { txtToolsOutput.Text = output; toolsSubTabs.SelectedIndex = 0; });
            Log(code == 0 ? "Lista winget zapisana: " + path : "winget list zwrocil kod " + code + ". Wynik zapisano: " + path);
        }

        void SetProcessorMaximum()
        {
            Status("Ustawiam maksymalny stan procesora na 100%...");
            string script =
                "$active=(powercfg /getactivescheme | Select-String -Pattern '[0-9a-fA-F-]{36}' | ForEach-Object { [regex]::Match($_.Line,'[0-9a-fA-F-]{36}').Value } | Select-Object -First 1);" +
                "$schemes=(powercfg /list) | Select-String -Pattern '[0-9a-fA-F-]{36}' | ForEach-Object { [regex]::Match($_.Line,'[0-9a-fA-F-]{36}').Value } | Select-Object -Unique;" +
                "if(-not $schemes){throw 'Nie znaleziono planow zasilania'};" +
                "foreach($s in $schemes){powercfg /setacvalueindex $s SUB_PROCESSOR PROCTHROTTLEMAX 100; if($LASTEXITCODE){throw \"AC $s\"}; powercfg /setdcvalueindex $s SUB_PROCESSOR PROCTHROTTLEMAX 100; if($LASTEXITCODE){throw \"DC $s\"}};" +
                "if($active){powercfg /setactive $active; if($LASTEXITCODE){throw 'Nie udalo sie ponownie aktywowac planu'}}";
            string output;
            int code = Engine.RunPowerShellCapture(script, out output, 120000);
            if (code == 0) Log("Ustawiono maksymalny stan procesora 100% dla zasilania sieciowego i baterii we wszystkich widocznych planach.");
            else Log("BLAD ustawien procesora: " + output.Trim());
        }

        void DisableFastStartup()
        {
            Status("Wylaczam szybkie uruchamianie...");
            try
            {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power"))
                    key.SetValue("HiberbootEnabled", 0, RegistryValueKind.DWord);
                Log("Wylaczono opcje 'Wlacz szybkie uruchamianie'. Zmiana obowiazuje po pelnym zamknieciu systemu.");
            }
            catch (Exception ex) { Log("BLAD szybkiego uruchamiania: " + ex.Message); }
        }

        bool? ChooseOfficeCleanupMode()
        {
            DialogResult mode = MessageBox.Show(
                "Wybierz zakres czyszczenia Office:\n\n" +
                "TAK - fabryczny/preinstalowany Office: Microsoft 365 Click-to-Run i UWP (zalecane)\n" +
                "NIE - wszystkie wykryte wersje Office, takze MSI i UWP\n" +
                "ANULUJ - bez zmian\n\n" +
                "Oba tryby usuwaja ustawienia i licencje wybranego pakietu. Zamknij wszystkie aplikacje Office.",
                App.Title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (mode == DialogResult.Cancel) return null;
            if (mode == DialogResult.Yes) return false;

            return MessageBox.Show(
                "Pelne czyszczenie usunie WSZYSTKIE wykryte wersje Office (Click-to-Run, MSI 2003-2016 i UWP), " +
                "ich licencje, klucze produktu i ustawienia uzytkownika.\n\nKontynuowac?",
                App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes
                ? (bool?)true : null;
        }

        bool LaunchOfficeScrubber(bool fullCleanup)
        {
            return LaunchOfficeScrubber(fullCleanup, true);
        }

        bool LaunchOfficeScrubber(bool fullCleanup, bool allowDownload)
        {
            const string url = "https://raw.githubusercontent.com/abbodi1406/BatUtil/master/OfficeScrubber/OfficeScrubberAIO.cmd";
            const string expectedSha256 = "E418F8A6B36D9C55D6EFDB4B5AD378EBBB848A6A5E38C44EB94690EAE35FFF44";
            string dir = Path.Combine(eng.ToolsDir, "OfficeScrubber");
            string path = Path.Combine(dir, "OfficeScrubberAIO.cmd");
            try
            {
                Directory.CreateDirectory(dir);
                if (!File.Exists(path))
                {
                    if (!allowDownload)
                        throw new FileNotFoundException("brak lokalnego OfficeScrubberAIO.cmd; tryb przygotowania nie korzysta z internetu.", path);
                    string downloadPath = path + ".download";
                    try { if (File.Exists(downloadPath)) File.Delete(downloadPath); } catch { }
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "Wdrozyciel/" + App.Version;
                        wc.DownloadFile(url, downloadPath);
                    }
                    string downloadedHash = Engine.Sha256Of(downloadPath);
                    if (!string.Equals(downloadedHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(downloadPath); } catch { }
                        throw new InvalidDataException("pobrany OfficeScrubber ma nieoczekiwana sume SHA256; plik nie zostal uruchomiony.");
                    }
                    File.Move(downloadPath, path);
                    Log("Pobrano i zweryfikowano OfficeScrubberAIO.cmd.");
                }
                string actualHash = Engine.Sha256Of(path);
                if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("lokalny OfficeScrubber ma niezgodna sume SHA256; plik nie zostal uruchomiony.");

                string modeArg = fullCleanup ? "/A" : "/C /P";
                string modeName = fullCleanup
                    ? "pelne czyszczenie wszystkich wersji"
                    : "fabryczny Office (Microsoft 365 Click-to-Run i UWP)";
                Log("OfficeScrubber: zweryfikowano SHA256; tryb: " + modeName + ".");
                Status("OfficeScrubber: " + modeName + "...");
                int code = Engine.RunVisible("cmd.exe", "/d /c call " + Engine.QuoteArg(path) + " " + modeArg + " -qedit", true);
                if (code == 0)
                {
                    Log("OfficeScrubber zakonczyl czyszczenie. Zalecany jest restart komputera.");
                    return true;
                }
                else
                    Log("BLAD: OfficeScrubber zakonczyl dzialanie kodem " + code + ".");
                return false;
            }
            catch (Exception ex) { Log("BLAD OfficeScrubber: " + ex.Message); return false; }
        }

        void RunLocalMcAfee()
        {
            const string expectedSha256 = "D4D2266A19876BECCC95A97E1E5821EF42D98D503818C1E3F19BE75E9358B100";
            string path = Path.Combine(eng.ToolsDir, "MCPR.exe");
            Status("Sprawdzam lokalny McAfee MCPR...");
            try
            {
                if (!File.Exists(path))
                {
                    Log("BLAD: brak lokalnego MCPR.exe w " + eng.ToolsDir + ".");
                    return;
                }
                string hash = Engine.Sha256Of(path);
                if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    Log("BLAD: lokalny MCPR.exe ma niezgodna sume SHA256 i nie zostanie uruchomiony. Odczytano: " + hash + ".");
                    return;
                }
                Log("Zweryfikowano lokalny MCPR.exe (SHA256 OK).");
                UI(delegate
                {
                    if (MessageBox.Show("MCPR wymaga obslugi interaktywnej i moze wymagac restartu. Uruchomic teraz?", App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        try { Engine.RunVisible(path, "", false); Log("Uruchomiono MCPR.exe."); }
                        catch (Exception ex) { Log("BLAD MCPR: " + ex.Message); }
                    }
                });
            }
            catch (Exception ex) { Log("BLAD MCPR: " + ex.Message); }
        }

        static List<string> ParseNames(string raw)
        {
            List<string> names = new List<string>();
            foreach (string part in (raw ?? "").Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string value = part.Trim();
                if (value.Length > 0 && !value.StartsWith("#") && !names.Contains(value)) names.Add(value);
            }
            return names;
        }

        void LoadDefaultWingetIds()
        {
            string path = Path.Combine(eng.BaseDir, "winget-remove-defaults.txt");
            if (!File.Exists(path))
                File.WriteAllText(path, "# Dokladne ID z polecenia winget list, po jednym w wierszu.\r\n# Przyklad: Microsoft.Teams\r\n", new UTF8Encoding(false));
            txtWingetIds.Text = string.Join(Environment.NewLine, ParseNames(File.ReadAllText(path)).ToArray());
            Log("Wczytano domyslne ID winget: " + ParseNames(txtWingetIds.Text).Count + ".");
        }

        void RemoveWingetApps()
        {
            List<string> ids = ParseNames(txtWingetIds.Text);
            if (ids.Count == 0) { MessageBox.Show("Podaj przynajmniej jedno dokladne ID pakietu winget.", App.Title); return; }
            if (MessageBox.Show("Odinstalowac wskazane pakiety wedlug dokladnych ID?\n\n" + string.Join("\n", ids.ToArray()), App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            RunWorker(delegate
            {
                string winget = eng.GetWingetPath();
                if (string.IsNullOrEmpty(winget)) { Log("BLAD: nie znaleziono winget.exe."); return; }
                int done = 0;
                Progress(0, ids.Count);
                foreach (string id in ids)
                {
                    Status("Odinstalowuje winget: " + id + "...");
                    string output;
                    string common = "uninstall --id " + Engine.QuoteArg(id) + " --exact --accept-source-agreements --disable-interactivity";
                    int code = Engine.RunCapture(winget, common + " --silent --scope machine", out output, 15 * 60 * 1000);
                    if (code != 0)
                    {
                        string retryOutput;
                        int retry = Engine.RunCapture(winget, common + " --silent", out retryOutput, 15 * 60 * 1000);
                        output += Environment.NewLine + retryOutput;
                        code = retry;
                    }
                    Log("winget uninstall " + id + ": kod " + code + (output.Trim().Length > 0 ? " | " + output.Trim() : ""));
                    Progress(++done, ids.Count);
                }
            }, false);
        }

        void LoadDefaultAppx()
        {
            string path = Path.Combine(eng.BaseDir, "appx-remove-defaults.txt");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "# Wpisz maski pakietow Appx, po jednej w wierszu.\r\n# Przyklad: Microsoft.XboxGamingOverlay\r\n", new UTF8Encoding(false));
            }
            List<string> lines = new List<string>();
            foreach (string line in File.ReadAllLines(path))
            {
                string s = line.Trim();
                if (s.Length > 0 && !s.StartsWith("#")) lines.Add(s);
            }
            txtAppxNames.Text = string.Join(Environment.NewLine, lines.ToArray());
            Log("Wczytano domyslne maski Appx: " + lines.Count + ".");
        }

        void RemoveAppxForAllUsers()
        {
            string raw = txtAppxNames.Text.Trim();
            if (raw.Length == 0) { MessageBox.Show("Podaj przynajmniej jedna maske/nazwe pakietu Appx.", App.Title); return; }
            List<string> names = ParseNames(raw);
            if (names.Count == 0) return;
            if (MessageBox.Show("Usunac wskazane pakiety Appx dla wszystkich obecnych uzytkownikow oraz z obrazu dla nowych uzytkownikow?\n\n" + string.Join("\n", names.ToArray()), App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            RunWorker(delegate
            {
                int done = 0;
                Progress(0, names.Count);
                foreach (string name in names)
                {
                    Status("Usuwam Appx: " + name + "...");
                    string script =
                        "$n=" + Engine.PsQuote(name) + ";" +
                        "Get-AppxPackage -Name ('*'+$n+'*') -AllUsers | Remove-AppxPackage -AllUsers -ErrorAction Continue;" +
                        "Get-AppxProvisionedPackage -Online | Where-Object { $_.PackageName -like ('*'+$n+'*') } | ForEach-Object { Remove-AppxProvisionedPackage -Online -AllUsers -PackageName $_.PackageName -ErrorAction Continue }";
                    string output;
                    int code = Engine.RunPowerShellCapture(script, out output, 180000);
                    Log("Appx " + name + ": kod " + code + (output.Trim().Length > 0 ? " | " + output.Trim() : ""));
                    Progress(++done, names.Count);
                }
            }, false);
        }
    }
}
