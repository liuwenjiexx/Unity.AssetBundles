using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Text;
using Debug = UnityEngine.Debug;
using System.Linq;
using System;
using UnityEditor.Build;
using UnityEditor.GUIExtensions;
using UnityEngine.SocialPlatforms;
using UnityEngine.Apple.TV;
using System.Text.RegularExpressions;
using UnityEngine.tvOS;
using System.Threading;
using Coroutines;

namespace UnityEditor
{

    public class AssetBundleSyncEditorWindow : EditorWindow
    {
        private string[] devices;
        private bool loaded;
        public string selectedAndroidDevice;
        AssetBundleVersion localVersion;
        AssetBundleVersion deviceVersion;
        private bool hasRemoteManifest;
        private bool hasLocalManifest;
        private List<string> localAssetBundles;
        private List<string> remoteAssetBundles;
        private List<ChangedAssetBundle> changedAssetBundles;


        [MenuItem("Build/AssetBundle/Sync")]
        public static void ShowWindow()
        {
            GetWindow<AssetBundleSyncEditorWindow>().Show();
        }
        void RefreshDevices()
        {
            string result = RunADB("devices");
            string[] array = result.Split('\n')
                .Select(o => o.Trim())
                .Where(o => !string.IsNullOrEmpty(o))
                .Skip(1)
                .ToArray();
            for (int i = 0; i < array.Length; i++)
            {
                string[] parts = array[i].Split('\t');
                array[i] = parts[0];
            }

            devices = array
                .OrderBy(o => o)
                .ToArray();

        }
        private void OnEnable()
        {
            if (changedAssetBundles == null)
                changedAssetBundles = new List<ChangedAssetBundle>();
            if (localAssetBundles == null)
                localAssetBundles = new List<string>();
            if (remoteAssetBundles == null)
                remoteAssetBundles = new List<string>();
            titleContent = new GUIContent("AssetBundle Sync");

            GetLocalVersion();

            if (!loaded)
            {
                loaded = true;
                RefreshDevices();
                if (!string.IsNullOrEmpty(SelectedDevice))
                {
                    GetDeviceVersion();
                }
            }
        }

        public string SelectedDevice
        {
            get
            {
                int selectedIndex;
                selectedIndex = Array.FindIndex(devices, (o) => o == selectedAndroidDevice);
                if (selectedIndex == -1)
                    return null;
                return selectedAndroidDevice;
            }
        }

        void OnDeviceChanged()
        {
            isInstall = false;

            isInstall = !string.IsNullOrEmpty(GetInstallPath(Application.identifier));

            GetDeviceVersion();
        }
        static string AndroidDevicePersistentDataPath
        {
            get
            {
                return $"/sdcard/android/data/{Application.identifier}/files";
            }
        }
        public string LocalAssetBundlesDirectory
        {
            get { return BuildAssetBundles.GetOutputPath(); }
        }

        string androidDeviceAssetBundlesDirectory;
        public string AndroidDeviceAssetBundlesDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(androidDeviceAssetBundlesDirectory))
                    return androidDeviceAssetBundlesDirectory;
                return $"{AndroidDevicePersistentDataPath}/{Path.GetDirectoryName(BuildAssetBundles.FormatString(AssetBundleSettings.LocalManifestPath)).Replace('\\','/')}";
            }
            set
            {
                androidDeviceAssetBundlesDirectory = value;
            }
        }

        void GetLocalVersion()
        {
            localVersion = null;
            string versionFile = Path.Combine(LocalAssetBundlesDirectory, $"{BuildAssetBundles.PlatformName}.json");
            if (File.Exists(versionFile))
            {
                localVersion = AssetBundleVersion.LoadFromFile(versionFile);
            }
        }



        void GetDeviceVersion()
        {
            deviceVersion = null;
            changedAssetBundles.Clear();
            localAssetBundles.Clear();
            remoteAssetBundles.Clear();
            hasLocalManifest = false;
            hasRemoteManifest = false;
            GetLocalVersion();

            if (!isInstall)
                return;
            string tmpDir = Path.GetFullPath("Temp/AssetBunlde/Device");
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);

            try
            {
                GetRemotePathReadWritePermission(AndroidDeviceAssetBundlesDirectory);
                RunADB(GetADBCmdText($"pull \"{AndroidDeviceAssetBundlesDirectory}/{BuildAssetBundles.PlatformName}.json\" \"{tmpDir}\""));
            }
            catch { }
            RunADB(GetADBCmdText($"pull \"{AndroidDeviceAssetBundlesDirectory}/{BuildAssetBundles.PlatformName}\" \"{tmpDir}\""));
            string versionFile = Path.Combine(tmpDir, $"{BuildAssetBundles.PlatformName}.json");
            if (File.Exists(versionFile))
            {
                deviceVersion = AssetBundleVersion.LoadFromFile(versionFile);
            }

            if (localVersion != null)
            {
                string remoteManifestFile = Path.Combine(tmpDir, $"{BuildAssetBundles.PlatformName}");
                Dictionary<string, Hash128> deviceHashs = new Dictionary<string, Hash128>();
                AssetBundle assetBundle;
                if (File.Exists(remoteManifestFile))
                {
                    assetBundle = AssetBundle.LoadFromFile(remoteManifestFile);
                    AssetBundleManifest deviceManifest = assetBundle.LoadAllAssets<AssetBundleManifest>().FirstOrDefault();

                    if (deviceManifest != null)
                    {
                        foreach (string bundleName in deviceManifest.GetAllAssetBundles())
                        {
                            deviceHashs[bundleName] = deviceManifest.GetAssetBundleHash(bundleName);
                            remoteAssetBundles.Add(bundleName);
                        }
                        hasRemoteManifest = true;
                    }
                    assetBundle.Unload(true);
                }
                if (File.Exists(Path.Combine(LocalAssetBundlesDirectory, $"{BuildAssetBundles.PlatformName}")))
                {
                    assetBundle = AssetBundle.LoadFromFile(Path.Combine(LocalAssetBundlesDirectory, $"{BuildAssetBundles.PlatformName}"));
                    AssetBundleManifest localManifest = assetBundle.LoadAllAssets<AssetBundleManifest>().FirstOrDefault();
                    if (localManifest != null)
                    {
                        hasLocalManifest = true;
                        foreach (string bundleName in localManifest.GetAllAssetBundles())
                        {
                            localAssetBundles.Add(bundleName);
                            var hash = localManifest.GetAssetBundleHash(bundleName);
                            if (!deviceHashs.ContainsKey(bundleName) || hash != deviceHashs[bundleName])
                            {
                                ChangedAssetBundle bundle = new ChangedAssetBundle();
                                bundle.assetBundleName = bundleName;
                                string path = Path.Combine(LocalAssetBundlesDirectory, bundleName);
                                bundle.size = (int)new FileInfo(path).Length;
                                bundle.hash = hash.ToString();
                                changedAssetBundles.Add(bundle);

                            }
                        }

                    }
                    assetBundle.Unload(true);
                }
            }
        }
        bool isInstall;

        private void OnGUI()
        {
            GUIAndroidDeviceList();


            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Package");
                GUILayout.Label(Application.identifier, GUILayout.ExpandWidth(true));

                using (new EditorGUI.DisabledGroupScope(true))
                {
                    GUILayout.Toggle(isInstall, GUIContent.none, GUILayout.ExpandWidth(false));
                }
                //if (RefreshButton())
                //{
                //    isInstall = !string.IsNullOrEmpty(GetInstallPath(Application.identifier));
                //    GetDeviceVersion();
                //}
            }



            using (var sv = new GUILayout.ScrollViewScope(scrollPos))
            {
                scrollPos = sv.scrollPosition;
                using (new EditorGUI.DisabledGroupScope(string.IsNullOrEmpty(SelectedDevice)))
                {
                }
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Local", GUILayout.ExpandWidth(false));
                    if (RefreshButton())
                    {
                        GetLocalVersion();
                    }
                }
                using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope("box"))
                {
                    if (localVersion != null && !string.IsNullOrEmpty(localVersion.hash))
                    {
                        GUIVersion(localVersion);
                    }
                    else
                    {
                        GUILayout.Label("has manifest " + hasLocalManifest);
                        GUILayout.Label("has version: " + (localVersion != null));
                    }
                }

                using (new EditorGUI.DisabledGroupScope(string.IsNullOrEmpty(SelectedDevice)))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Remote", GUILayout.ExpandWidth(false));

                    }

                    using (new GUILayout.VerticalScope())
                    {
                        using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope("box"))
                        {
                            if (deviceVersion != null && !string.IsNullOrEmpty(deviceVersion.hash))
                            {
                                GUIVersion(deviceVersion);
                            }
                            else
                            {
                                GUILayout.Label("has manifest " + hasRemoteManifest);
                                GUILayout.Label("has version: " + (deviceVersion != null));
                            }
                        }
                    }



                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Changed");
                        GUILayout.FlexibleSpace();
                        int totalSize = 0;
                        totalSize = changedAssetBundles.Sum(o => o.size);
                        GUILayout.Label((totalSize / 1024f).ToString("0.#") + "k", GUILayout.ExpandWidth(false));
                    }
                    using (new EditorGUILayoutx.Scopes.IndentLevelVerticalScope("box"))
                    {
                        foreach (var bundle in changedAssetBundles)
                        {
                            using (new GUILayout.HorizontalScope())
                            {
                                GUILayout.Label(bundle.assetBundleName);
                                GUILayout.Label((bundle.size / 1024f).ToString("0.#") + "k", GUILayout.ExpandWidth(false));
                            }
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledGroupScope(string.IsNullOrEmpty(SelectedDevice)))
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                {

                    isInstall = !string.IsNullOrEmpty(GetInstallPath(Application.identifier));
                    GetDeviceVersion();
                }

                if (GUILayout.Button("Sync"))
                {
                    if (changedAssetBundles.Count > 0)
                    {
                        SyncLocalToDeviceAsync()
                            .StartTask()
                            .ContinueWith((t) =>
                            {
                                if (t.Exception != null)
                                    Debug.LogException(t.Exception);
                                GetDeviceVersion();
                            });
                    }
                }
            }
        }

        string GetInstallPath(string package)
        {
            string result = null;
            try
            {
                result = RunADB(GetADBCmdText() + $"shell pm path {package}");
                if (string.IsNullOrEmpty(result))
                    return null;
                if (result.StartsWith("package:"))
                    return result.Substring(8);
            }
            catch { }
            return result;
        }

        IEnumerator SyncLocalToDeviceAsync()
        {
            if (!hasLocalManifest)
                throw new Exception("not local manifest");
            if (!hasRemoteManifest)
                throw new Exception("not remote manifest");

            string localDir = Path.GetFullPath(LocalAssetBundlesDirectory);
            string remoteDir = AndroidDeviceAssetBundlesDirectory;
            Debug.Log("local dir: " + localDir);
            Debug.Log("remote dir: " + remoteDir);
            //GetRemotePathReadWritePermission(remoteDir);
            DeleteRemoteFile(Path.Combine(remoteDir, BuildAssetBundles.PlatformName) + ".json");

            string[] remoteFiles = GetRemoteAllFiles(AndroidDeviceAssetBundlesDirectory);

            foreach (var bundleName in remoteFiles)
            {
                if (remoteFiles.Where(o => o == bundleName).Count() == 0)
                    continue;
                if (bundleName.Equals(BuildAssetBundles.PlatformName, StringComparison.InvariantCultureIgnoreCase))
                    continue;
                if (localAssetBundles.Where(o => o == bundleName).Count() == 0 || changedAssetBundles.Where(o => o.assetBundleName == bundleName).Count() > 0)
                {
                    if (changedAssetBundles.Where(o => o.assetBundleName == bundleName).Count() == 0)
                        Debug.Log($"delete remote assetbundle <{bundleName}>");
                    DeleteRemoteFile(Path.Combine(remoteDir, bundleName));
                }
            }

            CopyToRemoteFile(Path.Combine(localDir, BuildAssetBundles.PlatformName), Path.Combine(remoteDir, BuildAssetBundles.PlatformName));
            int progress = 0;

            new Thread(() =>
            {
                foreach (var bundle in changedAssetBundles)
                {
                    string localPath = Path.Combine(localDir, bundle.assetBundleName);
                    string remotePath = Path.Combine(remoteDir, bundle.assetBundleName);
                    //Debug.Log($"copy to remote assetbundle <{bundle.assetBundleName}>");
                    //Debug.Log(localPath + " => " + remotePath);
                    CopyToRemoteFile(localPath, remotePath);

                    progress++;
                }
            }).Start();

            while (progress < changedAssetBundles.Count)
            {
                EditorUtility.DisplayProgressBar($"Copy Files To Remote {progress}/{changedAssetBundles.Count} ", $"{changedAssetBundles[progress].assetBundleName}", (progress / (float)changedAssetBundles.Count));
                yield return null;
            }

            CopyToRemoteFile(Path.Combine(localDir, BuildAssetBundles.PlatformName) + ".json", Path.Combine(remoteDir, BuildAssetBundles.PlatformName) + ".json");
            EditorUtility.ClearProgressBar();
            Debug.Log("assetbundle sync done");
        }

        void CopyToRemoteFile(string localPath, string remotePath)
        {
            localPath = localPath.Replace('\\', '/');
            remotePath = remotePath.Replace('\\', '/');
            string dir = Path.GetDirectoryName(remotePath);
            dir = dir.Replace('\\', '/');
            string cmdText;

            DeleteRemoteFile(remotePath);
            CreateRemoteDirecotry(dir);
            //Debug.Log($"push file <{localPath}> => <{dir}>");
            cmdText = GetADBCmdText($"push \"{localPath}\" \"{dir}\"");
            string result = RunADB(cmdText);
            ////if (!string.IsNullOrEmpty(result))
            ////{
            ////    Debug.LogError("push file error "+result+", " + localPath + " => " + remotePath);
            ////    throw new Exception(result);
            ////}
            // 1 file pushed, 0 skipped. 95.9 MB/s (342878 bytes in 0.003s)

            //Debug.Log(result);
        }

        void CreateRemoteDirecotry(string path)
        {
            try
            {
                string cmd = GetADBCmdText($"shell mkdir -p \"{path}\"");
                string result = RunADB(cmd);
                //Debug.Log("CreateRemoteDirecotry " + result + ", " + path);
            }
            catch { }
        }

        public void GetRemotePathReadWritePermission(string path)
        {
            path = path.Replace('\\', '/');
            //ADBShell(new string[]{
            //   "su",
            //   $"chmod 777 \"{path}\"" }
            //);
            //Debug.Log("GetRemotePathReadWritePermission: " + path);
            // ADBShell((w, r) =>
            //{
            //    w.WriteLine("su");
            //    w.WriteLine($"chmod 777 \"{path}\"");
            //    w.WriteLine("exit");
            //    w.WriteLine("exit");
            //    //w.WriteLine("exit");
            //});
            RunADB(GetADBCmdText($"shell su -c 'chmod -R 777 \"{path}\"'"));
        }

        bool DeleteRemoteFile(string remotePath)
        {
            remotePath = remotePath.Replace('\\', '/');
            try
            {
                //Debug.Log($"delete file <{remotePath}>");
                string cmdText;
                cmdText = $"shell rm -rf \"{ remotePath}\"";
                if (string.IsNullOrEmpty(RunADB(cmdText)))
                    return true;
            }
            catch { }
            return false;
        }

        string ADBShell(string cmdText)
        {
            return ADBShell(new string[] { cmdText });
        }
        string ADBShell(IEnumerable<string> cmdTexts)
        {
            return Cmd("adb", new string[] { GetADBCmdText() + " shell" }.Concat(cmdTexts));
        }
        void ADBShell(Action<StreamWriter, StreamReader> cmd)
        {
            Cmd("adb", "shell", (w, r) =>
            {
                cmd?.Invoke(w, r);
                w.WriteLine("exit");
            });
        }
        string[] GetRemoteAllFiles(string remoteDir)
        {
            string result = ADBShell(new string[]{
                $"cd \"{remoteDir}\"",
                $"ls -lR"
                });
            string[] parts = result.Split('\n').Select(o => o.Trim()).ToArray();

            bool startDir = false;
            string dir = null;
            List<string> paths = new List<string>();
            Regex regex = new Regex("\\d+:\\d+ (.*)");

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.IndexOf('#') > 0)
                    continue;

                if (startDir)
                {
                    if (string.IsNullOrEmpty(part))
                    {
                        startDir = false;
                        continue;
                    }

                    if (part.StartsWith("d"))
                        continue;
                    if (part.StartsWith("total"))
                        continue;

                    string str = regex.Match(part).Groups[1].Value;
                    if (dir.Length > 0)
                        paths.Add(dir + "/" + str);
                    else
                        paths.Add(str);
                }
                else
                {
                    int index = part.IndexOf(':');
                    if (index > 0)
                    {
                        dir = part.Substring(0, index);
                        if (dir.StartsWith("./"))
                            dir = dir.Substring(2);
                        else if (dir == ".")
                            dir = "";
                        startDir = true;
                    }
                }
            }
            //Debug.Log(result);
            //Debug.Log(string.Join("\n", paths.ToArray()));

            return paths.ToArray();
        }

        string[] GetRemoteAllFileOrDirectorys(string remoteDir)
        {
            string result = ADBShell(new string[]{
                $"cd \"{remoteDir}\"",
                $"ls -lR"
                });
            string[] parts = result.Split('\n').Select(o => o.Trim()).ToArray();

            bool startDir = false;
            string dir = null;
            List<string> paths = new List<string>();
            Regex regex = new Regex("\\d+:\\d+ (.*)");

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.IndexOf('#') > 0)
                    continue;

                if (startDir)
                {
                    if (string.IsNullOrEmpty(part))
                    {
                        startDir = false;
                        continue;
                    }

                    if (part.StartsWith("total"))
                        continue;

                    string str = regex.Match(part).Groups[1].Value;
                    if (dir.Length > 0)
                        paths.Add(dir + "/" + str);
                    else
                        paths.Add(str);
                }
                else
                {
                    int index = part.IndexOf(':');
                    if (index > 0)
                    {
                        dir = part.Substring(0, index);
                        if (dir.StartsWith("./"))
                            dir = dir.Substring(2);
                        else if (dir == ".")
                            dir = "";
                        startDir = true;
                    }
                }
            }
            //Debug.Log(result);
            //Debug.Log(string.Join("\n", paths.ToArray()));

            return paths.ToArray();
        }



        Vector2 scrollPos;

        void GUIVersion(AssetBundleVersion version)
        {

            EditorGUILayout.LabelField("appVersion", version.appVersion);
            EditorGUILayout.LabelField("bundleVersion", version.bundleVersion);
            EditorGUILayout.LabelField("channel", version.channel);
            EditorGUILayout.LabelField("platform", version.platform);
            EditorGUILayout.LabelField("hash", version.hash);
            EditorGUILayout.LabelField("time", version.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        }



        void GUIAndroidDeviceList()
        {
            using (new GUILayout.HorizontalScope())
            {

                int selectedIndex;
                selectedIndex = Array.FindIndex(devices, (o) => o == selectedAndroidDevice);
                GUILayout.Label("Device", GUILayout.ExpandWidth(false));
                int index = EditorGUILayout.Popup(selectedIndex, devices);
                if (selectedIndex != index)
                {
                    string newDevice = null;
                    if (index != -1)
                    {
                        newDevice = devices[index];
                    }
                    if (selectedAndroidDevice != newDevice)
                    {
                        selectedAndroidDevice = newDevice;
                        OnDeviceChanged();
                    }
                }

                if (RefreshButton())
                {
                    RefreshDevices();
                }

            }
        }

        bool RefreshButton()
        {
            var style = new GUIStyle(EditorStyles.largeLabel);
            style.fontSize += 4;
            style.padding = new RectOffset(5, 0, 1, 0);
            style.margin = new RectOffset();
            return GUILayout.Button("↻", style, GUILayout.ExpandWidth(false));
        }

        string GetADBCmdText()
        {
            return GetADBCmdText(SelectedDevice, null);
        }
        string GetADBCmdText(string cmdText)
        {
            return GetADBCmdText(SelectedDevice, cmdText);
        }
        string GetADBCmdText(string device, string cmdText)
        {
            string _cmdText = "";
            if (!string.IsNullOrEmpty(device))
            {
                _cmdText = "-s " + device + " ";
                if (cmdText != null)
                    _cmdText += cmdText;
            }
            return _cmdText;
        }

        static string RunADB(string arguments)
        {
            return StartProcess("adb", arguments, null);
        }

        static string StartProcess(string filePath, string argments, string workingDirectory = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = filePath;
            startInfo.Arguments = argments;
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = Path.GetFullPath(workingDirectory);
            }
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            //startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            //startInfo.StandardErrorEncoding = Encoding.UTF8;
            //Debug.Log("run " + filePath + " " + argments);
            string result;

            using (var p = Process.Start(startInfo))
            {
                result = p.StandardOutput.ReadToEnd();
                if (p.ExitCode != 0)
                {
                    throw new Exception(filePath + " " + argments + "\n" + result);
                }
                else
                {
                    //Debug.Log(filePath + " " + cmdText + "\n" + result);
                }
            }


            return result;
        }

        static string Cmd(string filePath, IEnumerable<string> cmdTexts, string workingDirectory = null)
        {

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = filePath;
            if (cmdTexts.Count() > 0)
                startInfo.Arguments = cmdTexts.First();
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = Path.GetFullPath(workingDirectory);
            }
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            //startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            //startInfo.StandardErrorEncoding = Encoding.UTF8;

            string result;

            using (var p = Process.Start(startInfo))
            {
                foreach (var cmdText in cmdTexts.Skip(1))
                {
                    Debug.Log(cmdText);
                    p.StandardInput.WriteLine(cmdText);
                    p.StandardInput.Flush();
                }

                Debug.Log("exit");
                p.StandardInput.WriteLine("exit");
                p.StandardInput.Flush();
                p.StandardInput.Close();

                Debug.Log("ReadToEnd");
                result = p.StandardOutput.ReadToEnd();
                Debug.Log("ReadToEnd ok " + result);

                if (p.ExitCode != 0)
                {
                    Debug.LogError(string.Join("\n", cmdTexts));
                    throw new Exception(result);
                }
            }


            return result;
        }

        static void Cmd(string file, string args, Action<StreamWriter, StreamReader> inputOutput)
        {
            Cmd(file, args, null, inputOutput);
        }
        static void Cmd(string file, string args, string workingDirectory, Action<StreamWriter, StreamReader> inputOutput)
        {

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = file;
            startInfo.Arguments = args;
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = Path.GetFullPath(workingDirectory);
            }
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            //startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            //startInfo.StandardErrorEncoding = Encoding.UTF8;


            using (var p = Process.Start(startInfo))
            {

                inputOutput(p.StandardInput, p.StandardOutput);
                //p.StandardInput.WriteLine("exit");
                //p.StandardInput.Flush();
                //p.StandardInput.Close();

                if (!p.WaitForExit(1000 * 3))
                {
                    Debug.LogError("timeout " + file + " " + args);
                }
                if (p.ExitCode != 0)
                {
                    if (p.HasExited)
                        throw new Exception(p.StandardOutput.ReadToEnd());
                    throw new Exception();
                }
            }
        }

        [Serializable]
        class ChangedAssetBundle
        {
            public string assetBundleName;
            public int size;
            public string hash;
        }
    }


}