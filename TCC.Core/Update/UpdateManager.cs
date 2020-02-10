﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nostrum;
using TCC.Data;
using TCC.Utils;
using TCC.ViewModels;
using TCC.Windows;
using Timer = System.Timers.Timer;

namespace TCC.Update
{
    public static class UpdateManager
    {
        private const string AppVersionUrl = "https://raw.githubusercontent.com/Foglio1024/Tera-custom-cooldowns/master/version";
        private const string AppVersionExperimentalUrl = "https://raw.githubusercontent.com/Foglio1024/Tera-custom-cooldowns/experimental/version";
        private static readonly string DatabaseHashFileUrl = $"https://raw.githubusercontent.com/Foglio1024/Tera-custom-cooldowns/{ (App.Beta ? "experimental" : "master")}/database-hashes.json";

        private static Timer _checkTimer;
        private static bool _waitingDownload = true;

        public static Dictionary<string, string> DatabaseHashes { get; private set; }
        public static bool UpdateAvailable { get; private set; }
        public static async Task CheckAppVersion()
        {
            try
            {
                var vp = new VersionParser();
                if (!vp.Valid || !vp.IsNewer) return;
                if (!App.SplashScreen.VM.AskUpdate($"TCC v{vp.NewVersionNumber} available. Download now?")) return;

                await Update(vp.NewVersionUrl);
            }
            catch (Exception e)
            {
                Log.F($"Error while checking update. \nException:\n{e.Message}\n{e.StackTrace}");
                if (!App.SplashScreen.VM.AskUpdate("Error while checking updates. Try again?")) return;
                await CheckAppVersion();
            }
        }

        public static void CheckServersFile()
        {
            var path = Path.Combine(App.DataPath, "servers.txt");
            if (File.Exists(path) && HashUtils.GenerateFileHash(path) == DatabaseHashes["servers.txt"]) return;
            DownloadServersFile();
        }
        public static void CheckDatabaseHash()
        {
            DatabaseHashes = new Dictionary<string, string>();

            try
            {
                DownloadDatabaseHashes();
            }
            catch (Exception ex)
            {
                Log.F($"Failed to download database hashes. nException: {ex.Message}\n{ex.StackTrace}");
                if (App.SplashScreen.VM.AskUpdate("Failed to download database hashes. Try again?")) CheckDatabaseHash();
            }
        }
        public static bool IsExperimentalNewer()
        {
            try
            {
                using (var c = MiscUtils.GetDefaultWebClient())
                {
                    var st = c.OpenRead(AppVersionExperimentalUrl);
                    if (st == null) return false;
                    var newVersionInfo = new StreamReader(st).ReadLine();
                    if (newVersionInfo == null) return false;
                    if (Version.Parse(newVersionInfo) > Assembly.GetExecutingAssembly().GetName().Version) return true;
                }
            }
            catch (Exception e)
            {
                Log.F($"[IsExperimentalNewer] Failed to check experimental version {e}");
            }
            return false;
        }
        public static void UpdateDatabase(string relativePath)
        {
            // example https://raw.githubusercontent.com/neowutran/TeraDpsMeterData/master/acc_benefits/acc_benefits-EU-EN.tsv
            try
            {
                var url = $"https://raw.githubusercontent.com/neowutran/TeraDpsMeterData/master/{ relativePath.Replace("\\", "/") }";
                var destPath = Path.Combine(App.DataPath, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir) && destDir != null) Directory.CreateDirectory(destDir);
                using (var c = MiscUtils.GetDefaultWebClient())
                {
                    c.DownloadFile(url, destPath);
                }
            }
            catch
            {
                var res = TccMessageBox.Show($"Failed to download database file {Path.GetFileNameWithoutExtension(relativePath)}. Try again?", MessageBoxType.ConfirmationWithYesNo);
                if (res == System.Windows.MessageBoxResult.Yes) UpdateDatabase(relativePath);
            }
        }
        public static void StartPeriodicCheck()
        {
            _checkTimer = new Timer((App.ToolboxMode ? 2 : 10) * 60 * 1000);
            _checkTimer.Elapsed += CheckTimer_Elapsed;
            _checkTimer.Start();
        }
        public static async void ForceUpdateToBeta()
        {
            using (var c = MiscUtils.GetDefaultWebClient())
            {
                try
                {
                    var vp = new VersionParser(forceExperimental: true);
                    if (!vp.Valid) return;

                    Log.N("TCC update manager", "Download started", NotificationType.Success, 3000);
                    c.DownloadFile(new Uri(vp.NewVersionUrl), "update.zip");

                    Log.N("TCC update manager", "Extracting zip", NotificationType.Success, 3000);
                    if (Directory.Exists(Path.Combine(App.BasePath, "tmp"))) Directory.Delete(Path.Combine(App.BasePath, "tmp"), true);
                    ZipFile.ExtractToDirectory("update.zip", Path.Combine(App.BasePath, "tmp"));

                    Log.N("TCC update manager", "Moving files", NotificationType.Success, 2000);
                    File.Move(Path.Combine(App.BasePath, "tmp/TCCupdater.exe"), Path.Combine(App.BasePath, "TCCupdater.exe"));

                    Log.N("TCC update manager", "Starting updater", NotificationType.Success, 1000);
                    await Task.Delay(1000).ContinueWith(t => Process.Start(Path.GetDirectoryName(typeof(App).Assembly.Location) + "/TCCupdater.exe", "update"));
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Log.F($"Error while checking updates. \nException: {ex.Message}\n{ex.StackTrace}");
                    if (TccMessageBox.Show("Error while checking updates. Try again?", MessageBoxType.ConfirmationWithYesNo) != System.Windows.MessageBoxResult.Yes) return;
                    ForceUpdateToBeta();
                }
            }
        }
        public static void StopTimer()
        {
            _checkTimer?.Stop();
        }
        public static void TryDeleteUpdater()
        {
            try
            {
                File.Delete(Path.Combine(App.BasePath, "TCCupdater.exe"));
            }
            catch
            {
                /* ignored*/
            }
        }

        //---------------------------------------------------------------------
        private static async Task Update(string url)
        {
            using (var c = MiscUtils.GetDefaultWebClient())
            {
                try
                {
                    App.SplashScreen.VM.BottomText = "Downloading update...";
                    c.DownloadFileCompleted += (s, ev) => _waitingDownload = false;
                    c.DownloadProgressChanged += (s, ev) => App.SplashScreen.VM.Progress = ev.ProgressPercentage;
                    // ReSharper disable once PossibleNullReferenceException
                    await App.SplashScreen.Dispatcher.InvokeAsync(() =>
                    {
                        c.DownloadFileAsync(new Uri(url), "update.zip");
                    });

                    while (_waitingDownload) Thread.Sleep(1000); //only way to wait for downlaod

                    App.SplashScreen.VM.BottomText = "Extracting zip...";
                    if (Directory.Exists(Path.Combine(App.BasePath, "tmp"))) Directory.Delete(Path.Combine(App.BasePath, "tmp"), true);
                    ZipFile.ExtractToDirectory("update.zip", Path.Combine(App.BasePath, "tmp"));

                    App.SplashScreen.VM.BottomText = "Moving files...";
                    File.Move(Path.Combine(App.BasePath, "tmp/TCCupdater.exe"), Path.Combine(App.BasePath, "TCCupdater.exe"));

                    App.SplashScreen.VM.BottomText = "Starting updater...";
                    Process.Start(Path.GetDirectoryName(typeof(App).Assembly.Location) + "/TCCupdater.exe", "update");
                    Environment.Exit(0);
                }
                catch (Exception e)
                {
                    Log.F($"Error while downloading update. \nException:\n{e.Message}\n{e.StackTrace}");
                    var res = TccMessageBox.Show("Error while downloading update. Try again? If the error perists download TCC manually.", MessageBoxType.ConfirmationWithYesNo);
                    if (res != System.Windows.MessageBoxResult.Yes) return;
                    await Update(url);
                }
            }
        }
        private static void DownloadServersFile()
        {
            if (!Directory.Exists(App.DataPath)) Directory.CreateDirectory(App.DataPath);
            using (var c = MiscUtils.GetDefaultWebClient())
            {
                try
                {
                    c.DownloadFile("https://raw.githubusercontent.com/neowutran/TeraDpsMeterData/master/servers.txt", Path.Combine(App.DataPath, "servers.txt"));
                }
                catch
                {
                    var res = TccMessageBox.Show("Failed to download servers file. Try again?", MessageBoxType.ConfirmationWithYesNo);
                    if (res == System.Windows.MessageBoxResult.Yes) DownloadServersFile();
                }
            }
        }
        private static void CheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _checkTimer.Stop();
            CheckAppVersionPeriodic();
            if(!UpdateAvailable) _checkTimer.Start();
        }
        private static void CheckAppVersionPeriodic()
        {
            try
            {
                var vp = new VersionParser();
                if (!vp.Valid || !vp.IsNewer) return;

                UpdateAvailable = true;

                ChatManager.Instance.AddTccMessage($"TCC v{vp.NewVersionNumber} is now available!");
                Log.N("Update manager", $"TCC v{vp.NewVersionNumber} available!", NotificationType.Success, 10000);
            }
            catch (Exception ex)
            {
                Log.F($"{ex.Message}\n{ex.StackTrace}\n{ex.Source}\n{ex}\n{ex.Data}\n{ex.InnerException}\n{ex.TargetSite}");
            }
        }
        private static void DownloadDatabaseHashes()
        {
            DatabaseHashes.Clear();
            using (var c = MiscUtils.GetDefaultWebClient())
            {
                var f = c.OpenRead(DatabaseHashFileUrl);
                if (f == null) return;
                using (var sr = new StreamReader(f))
                {
                    var sHashes = sr.ReadToEnd();
                    var jHashes = JObject.Parse(sHashes);
                    jHashes.Descendants().ToList().ForEach(jDesc =>
                    {
                        if (!(jDesc is JProperty jProp)) return;
                        DatabaseHashes[jProp.Name] = jProp.Value.ToString();
                    });
                }
            }
        }


        private class VersionParser
        {
            private Version Version => Version.Parse(NewVersionNumber);

            public string NewVersionNumber { get; }
            public string NewVersionUrl { get; }
            public bool IsNewer => Version > Assembly.GetExecutingAssembly().GetName().Version;
            public bool Valid { get; }

            public VersionParser(bool forceExperimental = false)
            {
                using var c = MiscUtils.GetDefaultWebClient();
                var st = c.OpenRead(App.Beta || forceExperimental ? AppVersionExperimentalUrl : AppVersionUrl);
                if (st == null) return;

                using var sr = new StreamReader(st);
                NewVersionNumber = sr.ReadLine();
                NewVersionUrl = sr.ReadLine();
                Valid = true;
            }
        }
    }
}