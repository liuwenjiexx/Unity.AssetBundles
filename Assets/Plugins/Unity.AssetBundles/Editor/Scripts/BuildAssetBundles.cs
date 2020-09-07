using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.StringFormats;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Template.Xslt;
using UnityEditor.Build.Internal;
using UnityEditor.Callbacks;
using UnityEngine;

namespace UnityEditor.Build
{

    public sealed class BuildAssetBundles
    {
        private static string packageDir;
        internal static string ConfigFilePath = "ProjectSettings/AssetBundle.json";
        public static string LocalGroupName
        {
            get
            {
                if (!EditorAssetBundleSettings.LocalGroup)
                    return AssetBundleSettings.LocalGroupName;
                return EditorAssetBundleSettings.LocalGroup.Asset.name.ToLower();
            }
        }
        public static string AutoDependencyAssetBundleName { get => LocalGroupName + "/auto/auto"; }

        private static EditorAssetBundleSettings config;
        private static DateTime lastConfigWriteTime;

        private const string FormatArg_BuildTarget = "BuildTarget";
        private const string FormatArg_AssetPath = "AssetPath";
        internal const string PackageName = "unity.assetbundles";

        public static string PackageDir
        {
            get
            {
                if (string.IsNullOrEmpty(packageDir))
                {
                    packageDir = GetPackageDirectory(PackageName);
                }
                return packageDir;
            }
        }


        private static string LastBuildCopyPath
        {
            get { return EditorPrefs.GetString("BuildAssetBundles.LastBuildCopyPath", null); }
            set { EditorPrefs.SetString("BuildAssetBundles.LastBuildCopyPath", value); }
        }
        private static bool AssetDirtied
        {
            get { return EditorPrefs.GetBool("BuildAssetBundles.AssetDirtied", false); }
            set { EditorPrefs.SetBool("BuildAssetBundles.AssetDirtied", value); }
        }
        private static string AssetBundleNamesClassTemplatePath
        {
            get
            {
                return Path.Combine(PackageDir, "Editor/AssetBundleNames.xslt");
            }
        }


        /// PlayerPrefs Key: [UnityEditor.BuildPlayer.AssetBundleManifestPath]
        /// <summary>
        /// 带 .manifest
        /// </summary>
        public static string BuildAssetBundleManifestPath
        {
            get { return PlayerPrefs.GetString("UnityEditor.BuildPlayer.AssetBundleManifestPath", null); }
            set
            {
                if (BuildAssetBundleManifestPath != value)
                {
                    PlayerPrefs.SetString("UnityEditor.BuildPlayer.AssetBundleManifestPath", value);
                    PlayerPrefs.Save();
                }
            }
        }

        public const string BuildLogPrefix = "[Build AssetBundle] ";


        public static string PlatformName
        {
            get => GetPlatformName();
        }

        private static string GetPackageDirectory(string packageName)
        {
            foreach (var dir in Directory.GetDirectories("Assets", packageName, SearchOption.AllDirectories))
            {
                if (File.Exists(Path.Combine(dir, "package.json")))
                {
                    return dir;
                }
            }

            string path = Path.Combine("Packages", packageName);
            if (File.Exists(Path.Combine(path, "package.json")))
            {
                return path;
            }

            return null;
        }

        [OnEditorApplicationStartup(1)]
        static void OnEditorApplicationStartup()
        {
            string filePath = Path.GetFullPath(ConfigFilePath);

            if (File.Exists(filePath))
            {
                UpdateAllAssetBundleNames();
            }

            addressableDiry = true;
        }
        static bool addressableDiry
        {
            get
            {
                return PlayerPrefs.HasKey("AddressableDiry");
            }
            set
            {
                if (value != addressableDiry)
                {
                    if (value)
                        PlayerPrefs.SetInt("AddressableDiry", 1);
                    else
                        PlayerPrefs.DeleteKey("AddressableDiry");
                    PlayerPrefs.Save();
                }
            }
        }
        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {
            //修改配置时很卡
            //string filePath = Path.GetFullPath(ConfigFilePath);

            //if (File.Exists(filePath))
            //{
            //    FileSystemWatcher fsw = new FileSystemWatcher();
            //    fsw.Path = Path.GetDirectoryName(filePath);
            //    fsw.Filter = Path.GetFileName(filePath);
            //    fsw.NotifyFilter = NotifyFilters.LastWrite;
            //    fsw.Changed += OnFileSystemWatcher;
            //    fsw.IncludeSubdirectories = false;
            //    fsw.EnableRaisingEvents = true;
            //}
            if (true)
            {
                FileSystemWatcher fsw = new FileSystemWatcher(Path.GetFullPath("Assets"), "*.meta");
                fsw.NotifyFilter = NotifyFilters.LastWrite;
                fsw.Changed += (o, e) =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        addressableDiry = true;
                    };
                };
                fsw.IncludeSubdirectories = true;
                fsw.EnableRaisingEvents = true;
            }
        }
        [RuntimeInitializeOnLoadMethod]
        static void RuntimeInitializeOnLoadMethod()
        {
            if (addressableDiry)
            {
                addressableDiry = false;
                DateTime startTime = DateTime.Now;
                var list = LoadAllAssetBundleList();
                CreateAddressableAsset(list);
                addressableDiry = false;
                Debug.Log(BuildLogPrefix + $"Build addressable time: {(DateTime.Now - startTime).TotalSeconds.ToString("0.#")}s");
            }
        }
        //static void OnFileSystemWatcher(object sender, FileSystemEventArgs e)
        //{
        //    if (File.Exists(e.FullPath))
        //    {
        //        EditorApplication.delayCall += () =>
        //        {
        //            if (ignoreLoad)
        //            {
        //                ignoreLoad = false;
        //                return;
        //            }
        //            config = null;
        //            LoadConfig();
        //            UpdateAllAssetBundleNames();
        //        };
        //    }
        //}

        private static Dictionary<string, object> GetFormatArgsWithFilePath(string filePath)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            GetFormatValues(filePath, values);
            return values;
        }


        public static InitializeFormatValuesDelegate InitializeFormatValues;
        public delegate void InitializeFormatValuesDelegate(string filePath, Dictionary<string, object> values);
        public static void GetFormatValues(string assetPath, Dictionary<string, object> values)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;

            values[FormatArg_BuildTarget] = buildTarget.ToString();
            values[AssetBundles.FormatArg_Platform] = GetPlatformName(buildTarget);
            values[AssetBundles.FormatArg_AppVersion] = GetAppVersion();
            values[AssetBundles.FormatArg_Channel] = AssetBundleSettings.Channel;
            values[AssetBundles.FormatArg_BundleVersion] = AssetBundleSettings.BundleVersion;
            GetFormatValuesWithAssetPath(values, assetPath);
        }
        public static void GetFormatValues(Dictionary<string, object> values)
        {
            GetFormatValues(null, values);
        }

        public static void GetFormatValuesWithAssetPath(Dictionary<string, object> values, string assetPath)
        {
            values[FormatArg_AssetPath] = assetPath;
            if (InitializeFormatValues != null)
                InitializeFormatValues(assetPath, values);
        }
        public static void GetFormatValues(Dictionary<string, object> values, AssetBundleVersion version)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;

            values[FormatArg_BuildTarget] = buildTarget.ToString();
            values[AssetBundles.FormatArg_Platform] = version.platform;
            values[AssetBundles.FormatArg_AppVersion] = version.appVersion;
            values[AssetBundles.FormatArg_Channel] = version.channel;
            values[AssetBundles.FormatArg_BundleVersion] = version.bundleVersion;
        }

        interface IFormatValueProvider
        {
            string GetValue(string key);
        }


        public static string FormatString(string input, Dictionary<string, object> values = null)
        {
            return FormatString(input, null, values);
        }
        public static string FormatString(string input, string filePath, Dictionary<string, object> values = null)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            if (values == null)
            {
                values = new Dictionary<string, object>();
                GetFormatValues(filePath, values);
            }
            try
            {
                input = input.FormatStringWithKey(values);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return input;
        }
        private static string GetFormatString(string input, Dictionary<string, object> values)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            input = input.FormatStringWithKey(values);

            return input;
        }


        public static string GetPlatformName(BuildTarget buildTarget)
        {
            string name;
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    name = "Windows";
                    break;
                case BuildTarget.Android:
                    name = "Android";
                    break;
                case BuildTarget.iOS:
                    name = "iOS";
                    break;
                case BuildTarget.StandaloneOSX:
                    name = "OSX";
                    break;
                default:
                    name = buildTarget.ToString();
                    break;
            }
            return name;
        }

        public static string GetPlatformName()
        {
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            return GetPlatformName(buildTarget);
        }


        [MenuItem(EditorAssetBundles.MenuPrefix + "Build", priority = EditorAssetBundles.BuildMenuPriority)]
        public static void Build()
        {
            //var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            //string outputPath = GetNextOutputPath();

            Build(null);

            //string copyPath = GetCopyToPath();
            //if (!string.IsNullOrEmpty(copyPath))
            //{
            //    CopyDirectory(outputPath, copyPath);
            //    Debug.LogFormat("copy AssetBundles\nsrc:{0}\ndst:{1}", outputPath, copyPath);
            //}
        }

        [MenuItem("Build/Build AssetBundle", priority = 1)]
        public static void Build2()
        {
            //string outputPath = GetNextOutputPath();
            Build();
        }


        //   [MenuItem(MenuPrefix + "ListAssetBundle", priority = 3)]
        public static void ListAssetBundle()
        {

            Selection.objects = AssetDatabase.GetAllAssetBundleNames()
               .SelectMany(o => AssetDatabase.GetAssetPathsFromAssetBundle(o))
               .OrderBy(o => o)
               .Select(o => AssetDatabase.LoadAssetAtPath(o, typeof(UnityEngine.Object)))
               .ToArray();
            foreach (var p in Selection.objects)
            {
                Debug.Log(AssetDatabase.GetAssetPath(p));
            }
            Debug.Log(Selection.objects.Length);

        }

        //[MenuItem(MenuPrefix + "Clear All Directory AssetBundleName", priority = 3)]
        public static void ClearAllDirectoryAssetBundleName()
        {
            foreach (var assetPath in Directory.GetDirectories("assets", "*", SearchOption.AllDirectories))
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (!importer)
                    continue;
                if (!string.IsNullOrEmpty(importer.assetBundleName) || !string.IsNullOrEmpty(importer.assetBundleVariant))
                {
                    Debug.Log(assetPath + "," + importer.assetBundleName + "," + importer.assetBundleVariant);
                    importer.SetAssetBundleNameAndVariant(string.Empty, string.Empty);
                }
            }
        }
        //public static AssetBundleVersion[] FindAllVersionList()
        //{
        //    List<AssetBundleVersion> list = new List<AssetBundleVersion>();
        //    foreach (var file in Directory.GetFiles(GetBaseOutputPath(), AssetBundleSettings.VersionFile, SearchOption.AllDirectories))
        //    {
        //        try
        //        {
        //            var tmp = JsonUtility.FromJson<AssetBundleVersion>(File.ReadAllText(file, Encoding.UTF8));
        //            if (tmp != null)
        //            {
        //                list.Add(tmp);
        //            }
        //        }
        //        catch { }
        //    }
        //    return list.ToArray();
        //}

        //public static AssetBundleVersion FindLastestVersion()
        //{
        //    var versions = FindAllVersionList();
        //    AssetBundleVersion lastestVersion;
        //    lastestVersion = AssetBundleVersion.GetLatestVersionList(versions, GetPlatformName(), GetAppVersion());
        //    if (lastestVersion == null)
        //        lastestVersion = AssetBundleVersion.GetLatestVersionList(versions, GetPlatformName(), null);
        //    return lastestVersion;
        //}
        //public static AssetBundleVersion FindLastestPlatformVersion()
        //{
        //    var versions = FindAllVersionList();
        //    AssetBundleVersion lastestVersion;
        //    lastestVersion = AssetBundleVersion.GetLatestVersionList(versions, GetPlatformName(), null);
        //    return lastestVersion;
        //}

        public static string GetOutputPath()
        {
            //string path;
            //var lastest = FindLastestVersion();
            //if (lastest != null)
            //    path = GetOutputPath(lastest);
            //else
            //{
            //    path = GetOutputPath(null);
            //}
            //return path;
            string outputPath = AssetBundleSettings.BuildManifestPath;
            outputPath = FormatString(outputPath);
            outputPath = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputPath))
                throw new Exception("OutputPath empty");

            return outputPath;
        }
        //public static string GetNextOutputPath()
        //{
        //    var list = FindAllVersionList();
        //    AssetBundleVersion lastestVersion;

        //    //lastestVersion = AssetBundleVersion.GetLatestVersionList(list, GetPlatformName(), GetAppVersion());

        //    //if (lastestVersion == null)
        //    lastestVersion = AssetBundleVersion.GetLatestVersionList(list, GetPlatformName(), null);
        //    if (lastestVersion != null)
        //    {
        //        lastestVersion.appVersion = GetAppVersion();
        //        lastestVersion.bundleCode++;
        //    }
        //    else
        //    {
        //        lastestVersion = new AssetBundleVersion();
        //        lastestVersion.appVersion = GetAppVersion();
        //        lastestVersion.bundleCode = 1;
        //    }
        //    string outputPath = GetOutputPath(lastestVersion);
        //    return outputPath;
        //}

        public static string GetAppVersion()
        {
            //string appVersion = Application.version;
            //if (!string.IsNullOrEmpty(AssetBundleSettings.AppVersionFormat))
            //    appVersion = string.Format(AssetBundleSettings.AppVersionFormat, appVersion.Split('.'));
            //return appVersion;
            return AssetBundles.AppVersion;
        }

        //public static string GetBaseOutputPath()
        //{
        //    var buildTarget = EditorUserBuildSettings.activeBuildTarget;
        //    string outputPath = AssetBundleSettings.BuildPath;
        //    outputPath = FormatString(outputPath);
        //    if (string.IsNullOrEmpty(outputPath))
        //        throw new Exception("OutputPath empty");
        //    return outputPath;
        //}

        //public static string GetOutputPath(AssetBundleVersion version)
        //{
        //    string outputPath = GetBaseOutputPath();
        //    Dictionary<string, object> values = new Dictionary<string, object>();
        //    GetFormatValues(null, values);
        //    if (version != null)
        //    {
        //        AssetBundles.GetFormatArgsWithVersion(ref values, version);
        //    }
        //    outputPath = FormatString(outputPath, values);
        //    outputPath = Path.Combine(outputPath, FormatString(AssetBundleSettings.ManifestPath, values));
        //    return outputPath;
        //}



        public static string GetStreamingAssetsPath()
        {
            string path;
            path = FormatString(AssetBundleSettings.StreamingAssetsManifestPath);
            if (string.IsNullOrEmpty(path))
                return null;
            path = Path.GetDirectoryName(path);
            path = Path.Combine("Assets/StreamingAssets", path);
            return path;
        }


        //public static string GetVersionListPath()
        //{
        //    string outputPath = GetBaseOutputPath();
        //    outputPath = FormatString(outputPath);
        //    return Path.Combine(outputPath, FormatString(AssetBundleSettings.RootVersionFile));
        //}

        //public static string GetRootVersionPath(AssetBundleVersion version)
        //{
        //    string outputPath = GetBaseOutputPath();
        //    Dictionary<string, object> values = new Dictionary<string, object>();
        //    GetFormatValues(null, values);
        //    AssetBundles.GetFormatArgsWithVersion(ref values, version);
        //    outputPath = outputPath.FormatStringWithKey(values);
        //    return Path.Combine(outputPath, AssetBundleSettings.RootVersionFile.FormatStringWithKey(values));
        //}


        public static void Build(string outputPath)
        {

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = GetOutputPath();
            }
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("outputPath empty");
            outputPath = outputPath.Replace('\\', '/');
            if (outputPath[outputPath.Length - 1] == '/')
                outputPath = outputPath.Substring(0, outputPath.Length - 1);


            BuildTarget buildTarget;
            System.Diagnostics.Stopwatch sw1 = new System.Diagnostics.Stopwatch();
            sw1.Start();
            buildTarget = EditorUserBuildSettings.activeBuildTarget;
            using (var progressBar = new EditorProgressBar("Build AssetBundle"))
            {
                Debug.Log(BuildLogPrefix + " BuildTarget: " + buildTarget);

                progressBar.OnProgress("build start", 0f);
                foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                  .Referenced(typeof(IBuildAssetBundleStart).Assembly)
                  .SelectMany(o => o.GetTypes())
                  .Where(o => o.IsClass && !o.IsAbstract && typeof(IBuildAssetBundleStart).IsAssignableFrom(o)))
                {
                    var obj = Activator.CreateInstance(type) as IBuildAssetBundleStart;
                    obj.BuildAssetBundleStart(outputPath);
                }


                progressBar.OnProgress("update all assetbundle name", 0f);
                var allAssets = UpdateAllAssetBundleNames();
                //Debug.Log("UpdateAllAssetBundleNames:" + sw1.ElapsedMilliseconds);



                if (!Directory.Exists(outputPath))
                {
                    //var versions = FindAllVersionList();
                    //AssetBundleVersion lastest = FindLastestPlatformVersion();
                    //if (lastest != null)
                    //{
                    //    string srcPath = GetOutputPath(lastest);
                    //    if (Directory.Exists(srcPath))
                    //    {
                    //        if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
                    //            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    //        FileUtil.CopyFileOrDirectory(srcPath, outputPath);
                    //        Debug.Log(BuildLogPrefix + $"origin: {srcPath}");
                    //    }
                    //}

                    if (!Directory.Exists(outputPath))
                        Directory.CreateDirectory(outputPath);
                }

                AssetBundleVersion version = null;

                if (File.Exists(Path.Combine(outputPath, FormatString(AssetBundleSettings.VersionFile))))
                {
                    version = AssetBundleVersion.LoadFromFile(Path.Combine(outputPath, FormatString(AssetBundleSettings.VersionFile)));
                    //    File.Delete(Path.Combine(outputPath, AssetBundleSettings.VersionFile));
                }


                progressBar.OnProgress("load AssetBundleBuild list", 0.2f);
                List<AssetBundleBuild> list = LoadAllAssetBundleList();
                Dictionary<string, object> formatValues = new Dictionary<string, object>();

                CreateAddressableAsset(list);

                AssetBundleBuild assetBundle = new AssetBundleBuild()
                {
                    assetBundleName = AssetBundles.AddressableBundleName,
                    assetNames = new string[] { AssetBundles.AddressableAssetPath }
                };
                list.Add(assetBundle);

                //Debug.Log(" List<AssetBundleBuild> list:" + sw1.ElapsedMilliseconds);
                BuildAssetBundleOptions options;

                options = EditorAssetBundleSettings.Options;

                foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                    .Referenced(typeof(IPreprocessBuildAssetBundle).Assembly)
                    .SelectMany(o => o.GetTypes())
                    .Where(o => o.IsClass && !o.IsAbstract && typeof(IPreprocessBuildAssetBundle).IsAssignableFrom(o)))
                {
                    var obj = Activator.CreateInstance(type) as IPreprocessBuildAssetBundle;

                    progressBar.OnProgress("preprocess " + type.Name, 0.3f);
                    obj.OnPreprocessBuildAssetBundle(outputPath, list, ref options);
                }

                progressBar.OnProgress("collect all dependencies", 0.4f);
                var deps = GetAllDependenciesMultiRef(list);
                //Debug.Log("GetAllDependencies:" + sw1.ElapsedMilliseconds);
                if (deps.Count > 0)
                {
                    list.AddRange(SplitWithHashCode(new AssetBundleBuild()
                    {
                        assetBundleName = EditorAssetBundleSettings.AutoDependencyBundleName,
                        assetNames = deps.ToArray()
                    }, EditorAssetBundleSettings.AutoDependencySplit));
                }

                //清理空的
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    if (item.assetNames == null || item.assetNames.Length == 0)
                    {
                        list.RemoveAt(i);
                        i--;
                        continue;
                    }
                }

                ValidateBundleNameAndDirectoryDuplication(list);

                ClearExcludeDependency(list);

                var items = list.ToArray();
                outputPath = outputPath.Trim();
                string manifestName = Path.GetFileName(outputPath);
                string manifestPath = outputPath + "/" + manifestName;

                progressBar.OnProgress($"clear output directory", 0.5f);
                ClearOutputDirectory(outputPath, items);

                Debug.Log(BuildLogPrefix + $"BuildPipeline.BuildAssetBundles: options:{options}, assetbundle total:{items.Length}");
                progressBar.OnProgress($"BuildPipeline.BuildAssetBundles total:{items.Length}", 0.6f);

                //记录build最后写入时间
                DateTime[] lastWriteTimes = new DateTime[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    string path = Path.Combine(outputPath, items[i].assetBundleName);
                    if (File.Exists(path))
                    {
                        lastWriteTimes[i] = File.GetLastWriteTimeUtc(path);
                    }
                }

                AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputPath, items, options, buildTarget);
                //Debug.Log("BuildPipeline.BuildAssetBundles:" + sw1.ElapsedMilliseconds);
                progressBar.Show();
                if (manifest == null)
                {
                    throw new Exception("build asset bundle error. manifest null");
                }

                //记录改变的文件
                string changedAbNames = "";
                List<AssetBundleBuild> changed = new List<AssetBundleBuild>();
                for (int i = 0; i < items.Length; i++)
                {
                    string path = Path.Combine(outputPath, items[i].assetBundleName);

                    if (lastWriteTimes[i] != File.GetLastWriteTimeUtc(path))
                    {
                        changed.Add(items[i]);
                        if (!string.IsNullOrEmpty(changedAbNames))
                            changedAbNames += "\n";
                        changedAbNames += items[i].assetBundleName;
                    }
                }

                Debug.Log(BuildLogPrefix + $"changed {changed.Count} \n[\n{changedAbNames}\n]");

                //加密
                CryptoAssetBundles(outputPath, manifest, changed);

                //签名
                SignatureAssetBundles(outputPath, manifest, changed);

                progressBar.OnProgress($"create version file", 0.7f);
                bool hashChanged = true;

                hashChanged = CreateVersionFile(outputPath, ref version);


                DeleteOutputEmptyDirectory(outputPath);
                //string dstPath = GetOutputPath(version);
                //if (dstPath.Replace("\\", "/") != outputPath.Replace("\\", "/"))
                //{
                //    if (Directory.Exists(dstPath))
                //        Directory.Delete(dstPath, true);
                //    progressBar.OnProgress($"Move", 1f);
                //    if (!Directory.Exists(Path.GetDirectoryName(dstPath)))
                //        Directory.CreateDirectory(Path.GetDirectoryName(dstPath));
                //    FileUtil.MoveFileOrDirectory(Path.GetFullPath(outputPath), dstPath);
                //    DeleteOutputEmptyDirectory(outputPath);
                //    outputPath = dstPath;
                //}
                EditorAssetBundleSettingsWindow.versionList = null;
                BuildAssetBundleManifestPath = outputPath + "/" + PlatformName + ".manifest";


                if (EditorAssetBundleSettings.AssetBundleNamesClassSettings.Enabled)
                {
                    progressBar.OnProgress($"generate assetBundleNames class", 0.9f);
                    //Debug.Log("GenerateAssetBundleNamesClass before:" + sw1.ElapsedMilliseconds);
                    GenerateAssetBundleNamesClass(false);
                    //Debug.Log("GenerateAssetBundleNamesClass after:" + sw1.ElapsedMilliseconds);
                }

                foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                    .Referenced(typeof(IPostprocessBuildAssetBundle).Assembly)
                    .SelectMany(o => o.GetTypes())
                    .Where(o => o.IsClass && !o.IsAbstract && typeof(IPostprocessBuildAssetBundle).IsAssignableFrom(o)))
                {

                    progressBar.OnProgress($"postprocess " + type.Name, 0.8f);
                    var obj = Activator.CreateInstance(type) as IPostprocessBuildAssetBundle;
                    obj.OnPostprocessBuildAssetBundle(outputPath, options, manifest, items);
                }

                LastBuildCopyPath = GetStreamingAssetsPath();

                if (!string.IsNullOrEmpty(LastBuildCopyPath))
                {
                    progressBar.OnProgress($"copy to streamingAssets", 0.9f);
                    CopyToStreamingAssetsPath();
                }

                if (!EditorUserBuildSettings.development && !string.IsNullOrEmpty(EditorAssetBundleSettings.ReleasePath))
                {
                    string releaseDir = FormatString(EditorAssetBundleSettings.ReleasePath);
                    releaseDir = Path.GetFullPath(releaseDir);
                    if (Directory.Exists(Path.GetDirectoryName(releaseDir)))
                        Directory.CreateDirectory(Path.GetDirectoryName(releaseDir));
                    if (Directory.Exists(releaseDir))
                        Directory.Delete(releaseDir, true);
                    CopyToReleasePath(releaseDir);
                }

                progressBar.OnProgress($"Refresh", 1f);
                Debug.Log(BuildLogPrefix + "output path: " + outputPath);
                Debug.LogFormat(BuildLogPrefix + $"completed, bundleCode: {version.bundleCode} {(hashChanged ? "changed" : "unchanged")} ({sw1.Elapsed.TotalSeconds.ToString("#.#")}s)");
                AssetDatabase.Refresh();

                if (EditorAssetBundleSettings.PostBuildSettings.ShowFolder && !Application.isBatchMode)
                {
                    EditorUtility.RevealInFinder(Path.Combine(outputPath, manifestName));
                }
            }
        }

        public static List<AssetBundleBuild> LoadAllAssetBundleList()
        {
            List<AssetBundleBuild> list = new List<AssetBundleBuild>();
            Dictionary<string, object> formatValues = new Dictionary<string, object>();
            GetFormatValues(formatValues);

            foreach (var assetBundle in AssetDatabase.GetAllAssetBundleNames())
            {
                if (IsExcludeGroup(AssetBundles.GetBundleGroup(assetBundle)))
                {
                    Debug.Log(BuildLogPrefix + $"exclude bundle [{assetBundle}]");
                    continue;
                }
                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundle);
                if (assetPaths.Length == 0)
                    continue;
                AssetBundleBuild assetBundleBuild = new AssetBundleBuild();

                string[] addressableNames = new string[assetPaths.Length];

                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string assetPath = assetPaths[i];
                    GetFormatValuesWithAssetPath(formatValues, assetPaths[i]);
                    if (i == 0)
                    {
                        assetBundleBuild.assetBundleName = AssetDatabase.GetImplicitAssetBundleName(assetPath);
                        assetBundleBuild.assetBundleVariant = AssetDatabase.GetImplicitAssetBundleVariantName(assetPath);
                    }

                    addressableNames[i] = GetAddressableName(assetPath, formatValues);
                }
                assetBundleBuild.assetNames = assetPaths;
                assetBundleBuild.addressableNames = addressableNames;
                list.Add(assetBundleBuild);
            }
            return list;
        }

        /// <summary>
        /// 检查Bundle名称与文件夹冲突
        /// </summary>
        /// <param name="list"></param>
        static void ValidateBundleNameAndDirectoryDuplication(List<AssetBundleBuild> list)
        {
            HashSet<string> directoies = new HashSet<string>(list.Select(o => Path.GetDirectoryName(o.assetBundleName).Replace('\\', '/')));
            foreach (var item in list)
            {
                if (directoies.Contains(item.assetBundleName))
                    throw new Exception($"assetBundle [{item.assetBundleName }]  and directory duplication, asset [{item.assetNames[0]}]");
            }
        }

        static AssetBundleBuild[] SplitWithHashCode(AssetBundleBuild bundle, int split)
        {
            if (split <= 1)
                return new AssetBundleBuild[] { bundle };

            List<List<string>> assetNames = new List<List<string>>();
            List<List<string>> addressableNames = new List<List<string>>();

            for (int i = 0; i < split; i++)
            {
                assetNames.Add(new List<string>());
                addressableNames.Add(new List<string>());
            }

            for (int i = 0; i < bundle.assetNames.Length; i++)
            {
                int index = Mathf.Abs(bundle.assetNames[i].GetHashCode()) % split;
                assetNames[index].Add(bundle.assetNames[i]);
                if (bundle.addressableNames != null && bundle.addressableNames.Length > 0)
                    addressableNames[index].Add(bundle.addressableNames[i]);
            }

            List<AssetBundleBuild> list = new List<AssetBundleBuild>();
            for (int i = 0; i < split; i++)
            {
                if (assetNames[i].Count > 0)
                {
                    list.Add(new AssetBundleBuild()
                    {
                        assetNames = assetNames[i].ToArray(),
                        addressableNames = addressableNames.Count > 0 ? addressableNames[i].ToArray() : null,
                        assetBundleName = bundle.assetBundleName + i,
                        assetBundleVariant = bundle.assetBundleVariant,
                    });
                }
            }
            return list.ToArray();
        }

        public static void DeleteOutputEmptyDirectory(string outputPath)
        {
            DeleteAllEmptyDirectory(outputPath);
            if (Directory.Exists(Path.GetDirectoryName(outputPath)) && Directory.GetFiles(Path.GetDirectoryName(outputPath), "*", SearchOption.AllDirectories).Length == 0)
                Directory.Delete(Path.GetDirectoryName(outputPath));
        }

        /// <summary>
        /// 加密 AssetBundle
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="manifest"></param>
        static void CryptoAssetBundles(string outputPath, AssetBundleManifest manifest, List<AssetBundleBuild> changed)
        {

            if (!AssetBundleSettings.CryptoEnabled)
                return;

            if (string.IsNullOrEmpty(EditorAssetBundleSettings.CryptoKey))
                throw new Exception("crypto key null");

            string[] abNames = GetCryptoAssetBundleNames(manifest.GetAllAssetBundles());

            int total = abNames.Length;

            abNames = abNames.Where(o => changed.Where(o2 => o2.assetBundleName == o).Count() > 0).ToArray();

            Debug.Log(BuildLogPrefix + $"crypto total: { total}, crypto: {abNames.Length}");

            if (abNames.Length <= 0)
                return;

            //加密
            CryptoFiles(outputPath, abNames);
        }

        static void SignatureAssetBundles(string outputPath, AssetBundleManifest manifest, List<AssetBundleBuild> changed)
        {

            if (!AssetBundleSettings.SignatureEnabled)
                return;

            if (string.IsNullOrEmpty(EditorAssetBundleSettings.SignatureKeyPath))
                throw new Exception("signature KeyPath null");

            string[] abNames;
            abNames = GetSignatureAssetBundleNames(manifest.GetAllAssetBundles());

            int total = abNames.Length;

            abNames = abNames.Where(o => changed.Where(o2 => o2.assetBundleName == o).Count() > 0).ToArray();

            Debug.Log(BuildLogPrefix + $"signature total: { total}, signature: {abNames.Length}");

            if (abNames.Length <= 0)
                return;

            //签名 
            if (!string.IsNullOrEmpty(EditorAssetBundleSettings.SignatureKeyPath))
            {
                SignatureFiles(outputPath, abNames);
            }
        }

        public static string[] GetCryptoAssetBundleNames(string[] abNames)
        {
            if (!AssetBundleSettings.CryptoEnabled)
                return new string[0];

            if (string.IsNullOrEmpty(EditorAssetBundleSettings.CryptoKey))
                throw new Exception("crypto key null");
            return IncludeAssetBundleNames(abNames, AssetBundleSettings.CryptoInclude, AssetBundleSettings.CryptoExclude);
        }

        public static string[] GetSignatureAssetBundleNames(string[] abNames)
        {
            if (!AssetBundleSettings.SignatureEnabled)
                return new string[0];

            if (string.IsNullOrEmpty(EditorAssetBundleSettings.SignatureKeyPath))
                throw new Exception("signature KeyPath null");
            return IncludeAssetBundleNames(abNames, AssetBundleSettings.SignatureInclude, AssetBundleSettings.SignatureExclude);
        }

        static string[] IncludeAssetBundleNames(string[] abNames, string include, string exclude)
        {
            List<string> result = new List<string>(abNames);

            if (!string.IsNullOrEmpty(include))
            {
                //包含  
                for (int i = 0; i < abNames.Length; i++)
                {
                    if (abNames[i] != null)
                    {
                        if (!EditorAssetBundles.IsMatch(include, abNames[i], ignoreCase: true))
                        {
                            result.Remove(abNames[i]);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(exclude))
            {
                //排除
                for (int i = 0; i < abNames.Length; i++)
                {
                    if (abNames[i] != null)
                    {
                        if (EditorAssetBundles.IsMatch(exclude, abNames[i], ignoreCase: true))
                        {
                            result.Remove(abNames[i]);
                        }
                    }
                }
            }
            return result.ToArray();
        }

        public static void CryptoFiles(string outputPath, string[] abNames)
        {
            if (!AssetBundleSettings.CryptoEnabled)
                return;

            if (string.IsNullOrEmpty(EditorAssetBundleSettings.CryptoKey))
                throw new Exception("crypto Key null");
            if (string.IsNullOrEmpty(EditorAssetBundleSettings.CryptoIV))
                throw new Exception("crypto IV null");

            byte[] key, iv;
            key = Convert.FromBase64String(EditorAssetBundleSettings.CryptoKey);
            iv = Convert.FromBase64String(EditorAssetBundleSettings.CryptoIV);
            SymmetricAlgorithm sa = DES.Create();
            sa.Key = key;
            sa.IV = iv;

            string cryptoAbNames = "";
            for (int i = 0; i < abNames.Length; i++)
            {
                string abName = abNames[i];
                string file = Path.Combine(outputPath, abName);
                if (string.IsNullOrEmpty(file))
                    continue;
                byte[] bytes = File.ReadAllBytes(file);

                using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write))
                {
                    using (CryptoStream cs = new CryptoStream(fs, sa.CreateEncryptor(key, iv), CryptoStreamMode.Write))
                    {
                        cs.Write(bytes, 0, bytes.Length);
                        cs.FlushFinalBlock();
                    }
                }
                if (!string.IsNullOrEmpty(cryptoAbNames))
                    cryptoAbNames += ", ";
                cryptoAbNames += abName;
            }

            Debug.Log(BuildLogPrefix + "crypto :" + cryptoAbNames);
        }

        /// <summary>
        /// 签名
        /// </summary>
        public static void SignatureFiles(string outputPath, string[] abNames)
        {
            if (string.IsNullOrEmpty(EditorAssetBundleSettings.SignatureKeyPath))
                throw new Exception("SignatureKeyPath null");

            SHA1 sha = new SHA1CryptoServiceProvider();
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(File.ReadAllText(EditorAssetBundleSettings.SignatureKeyPath));

            byte[] bytes;
            byte[] sigData;
            string signatureAbNames = "";

            for (int i = 0; i < abNames.Length; i++)
            {
                string abName = abNames[i];
                string file = Path.Combine(outputPath, abName);
                if (string.IsNullOrEmpty(file))
                    continue;

                bytes = File.ReadAllBytes(file);

                sigData = rsa.SignData(bytes, sha);
                File.Delete(file);
                using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(sigData, 0, sigData.Length);
                    fs.Write(bytes, 0, bytes.Length);
                }

                if (!string.IsNullOrEmpty(signatureAbNames))
                    signatureAbNames += ", ";
                signatureAbNames += abName;
            }

            Debug.Log(BuildLogPrefix + "signature :" + signatureAbNames);
        }

        public static void DeleteAllCryptoOrSignatureBuildAssetBundle()
        {
            string[] abNames = AssetDatabase.GetAllAssetBundleNames();
            string outputPath = GetOutputPath();
            if (!Directory.Exists(outputPath))
                return;
            foreach (var abName in GetCryptoAssetBundleNames(abNames))
            {
                string path = Path.Combine(outputPath, abName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log("delete " + abName);
                }
                if (File.Exists(path + ".manifest"))
                    File.Delete(path + ".manifest");
            }

            foreach (var abName in GetSignatureAssetBundleNames(abNames))
            {
                string path = Path.Combine(outputPath, abName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log("delete " + abName);
                }
                if (File.Exists(path + ".manifest"))
                    File.Delete(path + ".manifest");
            }
        }

        static string FileNamePatternToRegexPattern(string fileNamePattern)
        {
            return fileNamePattern.Replace(".", "\\.").Replace("*", ".*");
        }
        static HashSet<string> deleteFileExclude;

        public static void ClearOutputDirectory(string outputPath, AssetBundleBuild[] items)
        {
            string manifestName = Path.GetFileName(outputPath);
            if (deleteFileExclude == null)
            {
                deleteFileExclude = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                deleteFileExclude.Add(FormatString(AssetBundleSettings.VersionFile));
            }
            HashSet<string> abs = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            abs.Add(manifestName);
            abs.Add(manifestName + ".manifest");
            foreach (var abName in items.Select(o => o.assetBundleName.ToLower()))
            {
                abs.Add(abName);
                abs.Add(abName + ".manifest");
            }

            int startIndex = outputPath.Length;
            if (!(outputPath.EndsWith("/") || outputPath.EndsWith("\\")))
                startIndex++;

            foreach (var file in Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories))
            {
                string ab = file.Substring(startIndex).Replace('\\', '/');
                if (deleteFileExclude.Contains(ab))
                    continue;
                if (!abs.Contains(ab))
                    FileUtil.DeleteFileOrDirectory(file);
            }
            DeleteAllEmptyDirectory(outputPath);
        }


        public static bool CreateVersionFile(string outputPath, ref AssetBundleVersion version)
        {
            bool changed = true;
            string manifestName = Path.GetFileName(outputPath);
            string hash = GetFileHashMD5(Path.Combine(outputPath, manifestName));
            if (version != null)
            {
                if (version.hash != hash)
                {
                    version.bundleCode = version.bundleCode + 1;
                    changed = true;
                }
                else
                {
                    changed = false;
                }

                if (EditorAssetBundleSettings.BundleCodeResetOfAppVersion && version.appVersion != GetAppVersion())
                {
                    version.bundleCode = 1;
                }
            }
            else
            {
                version = new AssetBundleVersion();
                version.bundleCode = 1;
                changed = true;
            }
            version.appVersion = GetAppVersion();
            version.platform = GetPlatformName();
            version.channel = AssetBundleSettings.Channel;
            version.bundleVersion = AssetBundleSettings.BundleVersion;
            version.Timestamp = DateTime.UtcNow;
            version.hash = hash;
            try
            {
                version.commitId = System.VersionControl.Git.GetShortCommitId();
            }
            catch { }
            version.groups = new string[] { LocalGroupName }.Concat(EditorAssetBundleSettings.Groups.Where(o => o.Asset).Select(o => (AssetBundleGroup)o.Asset).Select(o => o.name.ToLower())).ToArray();

            AssetBundleVersion.Save(Path.Combine(outputPath, FormatString(AssetBundleSettings.VersionFile)), version);
            Debug.Log(BuildLogPrefix + "create version file, appVersion [" + version.appVersion + "] bundleVersion [" + version.bundleVersion + "] bundleCode [" + version.bundleCode + "]");
            //latestVersion = AssetBundleVersion.GetLatestVersionList(versionList, version.platform, null);
            //RemoveRootVersion(latestVersion);
            //AddRootVersion(version);
            return changed;
        }

        public static int FindVersion(AssetBundleVersion[] versionList, AssetBundleVersion version)
        {
            for (int i = 0; i < versionList.Length; i++)
            {
                var item = versionList[i];
                if (item.hash == version.hash)
                    return i;
            }
            return -1;
        }

        //public static void ClearVersion()
        //{
        //    AssetBundleVersion[] versionList = AssetBundleVersion.LoadVersionList(GetVersionListPath());
        //    bool changed = false;
        //    for (int i = 0; i < versionList.Length; i++)
        //    {
        //        var item = versionList[i];
        //        string path = GetOutputPath(item);
        //        DeleteAllEmptyDirectory(path);
        //        if (!Directory.Exists(path))
        //        {
        //            versionList[i] = null;
        //            changed = true;
        //        }
        //    }

        //    if (changed)
        //    {
        //        AssetBundleVersion.Save(GetVersionListPath(), versionList.Where(o => o != null).ToArray());
        //    }
        //}

        //public static void RemoveRootVersion(AssetBundleVersion version)
        //{
        //    if (version == null)
        //        return;

        //    string rootVersionPath = GetRootVersionPath(version);
        //    if (File.Exists(rootVersionPath))
        //        File.Delete(rootVersionPath);

        //    //AssetBundleVersion[] versionList = AssetBundleVersion.LoadVersionList(GetVersionListPath());
        //    //ArrayUtility.Remove(ref versionList, version);
        //    //AssetBundleVersion.Save(GetVersionListPath(), versionList);
        //    //BuildAssetBundleConfigEditorWindow.versionList = null;
        //}

        //public static void AddRootVersion(AssetBundleVersion version)
        //{
        //    if (version == null)
        //        return;
        //    AssetBundleVersion[] versionList;
        //    string rootVersionPath = GetRootVersionPath(version);
        //    versionList = new AssetBundleVersion[0];
        //    //= AssetBundleVersion.LoadVersionList(GetVersionListPath());
        //    ArrayUtility.Add(ref versionList, version);
        //    AssetBundleVersion.Save(GetVersionListPath(), versionList);
        //    BuildAssetBundleConfigEditorWindow.versionList = null;
        //}

        public static string GetFileHashMD5(string filePath)
        {
            return GetHashMD5(File.ReadAllBytes(filePath));
        }

        public static string GetHashMD5(byte[] data)
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] hashBytes = md5.ComputeHash(data, 0, data.Length);
            string hash;
            hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLower();
            return hash;
        }

        static void DeleteAllEmptyDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                return;

            foreach (var subDir in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
            {
                if (Directory.Exists(subDir) && Directory.GetFiles(subDir, "*", SearchOption.AllDirectories).Length == 0)
                    Directory.Delete(subDir, true);
            }
        }

        public static string GetAddressableName(string assetPath, Dictionary<string, object> values = null)
        {

            string addressableName = null;
            bool assetNameToLower = EditorAssetBundleSettings.AssetNameToLower;

            if (EditorAssetBundleSettings.Groups != null && EditorAssetBundleSettings.Groups.Length > 0)
            {
                for (int i = EditorAssetBundleSettings.Groups.Length - 1; i >= 0; i--)
                {
                    var g = EditorAssetBundleSettings.Groups[i].Asset as AssetBundleGroup;
                    if (!g)
                        continue;
                    addressableName = g.GetAssetName(assetPath);
                    if (!string.IsNullOrEmpty(addressableName))
                        break;
                }
            }

            if (EditorAssetBundleSettings.LocalGroup)
            {
                addressableName = ((AssetBundleGroup)EditorAssetBundleSettings.LocalGroup.Asset).GetAssetName(assetPath);
            }

            return addressableName;
        }
        public static string GetDefaultAddressableName(string assetPath)
        {
            string addressableName = null;
            if (string.IsNullOrEmpty(addressableName))
            {
                if (!string.IsNullOrEmpty(EditorAssetBundleSettings.AssetName))
                {
                    addressableName = FormatString(EditorAssetBundleSettings.AssetName, assetPath);
                }
                else
                {
                    addressableName = assetPath;
                }
                if (EditorAssetBundleSettings.AssetNameToLower)
                    addressableName = addressableName.ToLower();
            }
            return addressableName;
        }


        static XmlDocument assetBundleData;

        static XmlDocument AssetBundleData
        {
            get
            {
                if (assetBundleData == null)
                {
                    assetBundleData = GetAssetBundleNamesData();
                }
                return assetBundleData;
            }
        }

        public static bool UpdateAssetBundleName(string assetPath)
        {
            string assetBundleName, variant;

            assetBundleName = string.Empty;
            variant = string.Empty;

            if (!IsExcludePath(assetPath))
            {
                Dictionary<string, object> formatValues = new Dictionary<string, object>();
                GetFormatValues(assetPath, formatValues);


                if (string.IsNullOrEmpty(assetBundleName))
                {
                    if (EditorAssetBundleSettings.Groups != null && EditorAssetBundleSettings.Groups.Length > 0)
                    {
                        for (int i = EditorAssetBundleSettings.Groups.Length - 1; i >= 0; i--)
                        {
                            var g = EditorAssetBundleSettings.Groups[i].Asset as AssetBundleGroup;
                            if (!g)
                                continue;
                            assetBundleName = g.GetBundleName(assetPath, out variant);
                            if (!string.IsNullOrEmpty(assetBundleName))
                                break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(assetBundleName))
            {
                if (EditorAssetBundleSettings.LocalGroup)
                {
                    assetBundleName = ((AssetBundleGroup)EditorAssetBundleSettings.LocalGroup.Asset).GetBundleName(assetPath, out variant);
                }
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (!importer)
                return false;
            bool changed = false;
            if (assetBundleName == null)
                assetBundleName = string.Empty;
            if (variant == null)
                variant = string.Empty;
            if (importer.assetBundleName != assetBundleName || importer.assetBundleVariant != variant)
            {
                importer.SetAssetBundleNameAndVariant(assetBundleName, variant);
                changed = true;
            }
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            if (string.IsNullOrEmpty(assetBundleName))
            {
                if (AddressableAsset.Remove(guid))
                {
                    EditorUtility.SetDirty(AddressableAsset);
                    DelaySaveAssets();
                }
            }
            else
            {
                if (AddressableAsset.Add(guid, GetAddressableName(assetPath), assetBundleName))
                {
                    EditorUtility.SetDirty(AddressableAsset);
                    DelaySaveAssets();
                }
            }
            //EditorApplication.delayCall -= DirtyAddressableAsset;
            //EditorApplication.delayCall += DirtyAddressableAsset;
            return changed;
        }

        public static string GetDefaultBundleName(string assetPath)
        {
            string assetBundleName = null;
            if (!string.IsNullOrEmpty(EditorAssetBundleSettings.AssetBundleName))
            {
                assetBundleName = FormatString(EditorAssetBundleSettings.AssetBundleName, assetPath);
                assetBundleName = assetBundleName.ToLower();
            }
            return assetBundleName;
        }
        //static void DirtyAddressableAsset()
        //{
        //    EditorUtility.SetDirty(AddressableAsset);
        //}
        static bool isSaveAssets;
        public static void DelaySaveAssets()
        {
            isSaveAssets = true;
            EditorApplication.delayCall += () =>
            {
                if (!isSaveAssets)
                    return;
                isSaveAssets = false;
                AssetDatabase.SaveAssets();
            };
        }

        // [MenuItem(EditorAssetBundles.MenuPrefix + "Update All AssetBundle Names", priority = EditorAssetBundles.OtherMenuPriority)]
        public static IEnumerable<string> UpdateAllAssetBundleNames()
        {
            DateTime startTime = DateTime.Now;
            RequireLocalGroup();
            bool changed = false;
            Action onChange = () =>
            {
                if (!changed)
                {
                    changed = true;
                    AssetDatabase.StartAssetEditing();
                }
            };

            HashSet<string> allAssetBundles = new HashSet<string>();


            HashSet<string> assetPaths = new HashSet<string>();

            foreach (var assetPath in AssetDatabase.GetAllAssetPaths())
            {
                assetPaths.Add(assetPath);
            }


            foreach (var assetPath in assetPaths)
            {
                if (UpdateAssetBundleName(assetPath))
                {
                    onChange();
                }
            }

            AssetDatabase.RemoveUnusedAssetBundleNames();

            if (changed)
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDirtied = false;
            }
            Debug.Log(BuildLogPrefix + "update all assetbundle name time: " + (DateTime.Now - startTime).TotalSeconds.ToString("0.#") + "s");
            ValidateGroup();
            return assetPaths;
        }



        //public static void DeleteUnusedAssetBundles(string path)
        //{
        //    if (Directory.Exists(path))
        //    {
        //        HashSet<string> names = new HashSet<string>(AssetDatabase.GetAllAssetBundleNames());
        //        foreach (var name in AssetDatabase.GetUnusedAssetBundleNames())
        //        {
        //            names.Remove(name);
        //        }
        //        string[] extensions = new string[] { ".meta", ".manifest", ".manifest.meta" };
        //        foreach (var filePath in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
        //        {
        //            string filename = Path.GetFileName(filePath);
        //            string exName = Path.GetExtension(filename);

        //            if (string.Equals(exName, ".manifest", StringComparison.InvariantCultureIgnoreCase))
        //                continue;
        //            if (string.Equals(exName, ".meta", StringComparison.InvariantCultureIgnoreCase))
        //                continue;

        //            if (names.Contains(filename))
        //                continue;

        //            DeleteFileOrDirectory(filePath);
        //            foreach (var ex in extensions)
        //            {
        //                if (File.Exists(filePath + ex))
        //                    DeleteFileOrDirectory(filePath + ex);
        //            }
        //        }
        //    }
        //}

        private static void DeleteFileOrDirectory(string path)
        {
            if (File.Exists(path))
            {
                //清除只读权限
                path.FileClearAttributes();
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    DeleteFileOrDirectory(file);
                Directory.Delete(path, true);
            }
        }

        private static void SyncDirectory(string src, Func<string, bool> filterSrc, string dst, Func<string, bool> filterDst = null)
        {
            if (!Directory.Exists(src))
                return;
            if (!Directory.Exists(dst))
                Directory.CreateDirectory(dst);

            Dictionary<string, string> srcFiles = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, string> dstFiles = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);


            foreach (var srcFile in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string relativePath = srcFile.Substring(src.Length);
                if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                    relativePath = relativePath.Substring(1);
                if (filterSrc != null && !filterSrc(relativePath))
                {
                    continue;
                }
                srcFiles.Add(relativePath, srcFile);
            }

            foreach (var dstFile in Directory.GetFiles(dst, "*", SearchOption.AllDirectories))
            {
                string relativePath = dstFile.Substring(dst.Length);
                if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                    relativePath = relativePath.Substring(1);
                if (filterDst != null && !filterDst(relativePath))
                    continue;
                dstFiles.Add(relativePath, dstFile);

                if (!srcFiles.ContainsKey(relativePath))
                {
                    DeleteFileOrDirectory(dstFile);
                }
            }

            foreach (var srcFile in srcFiles)
            {
                string dstFile = Path.Combine(dst, srcFile.Key);
                bool copy = false;
                if (!dstFiles.ContainsKey(srcFile.Key))
                {
                    copy = true;
                }
                else
                {
                    if (File.GetLastWriteTimeUtc(srcFile.Value) != File.GetLastWriteTimeUtc(dstFile))
                    {
                        copy = true;
                    }
                }
                if (copy)
                {
                    CopyFile(srcFile.Value, dstFile);
                }
            }
            DeleteAllEmptyDirectory(dst);

        }



        private static bool IsExcludePath(string assetPath)
        {
            bool ignore = false;

            if (EditorAssetBundleSettings.ExcludeExtensions != null && EditorAssetBundleSettings.ExcludeExtensions.Length > 0)
            {
                var excludes = EditorAssetBundleSettings.ExcludeExtensions;
                for (int i = 0, len = excludes.Length; i < len; i++)
                {
                    var exclude = excludes[i];
                    if (!string.IsNullOrEmpty(exclude) && assetPath.EndsWith(exclude, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
            }


            if (EditorAssetBundleSettings.IgnorePaths != null)
            {
                for (int i = 0; i < EditorAssetBundleSettings.IgnorePaths.Length; i++)
                {
                    if (AssetBundles.GetOrCacheRegex(EditorAssetBundleSettings.IgnorePaths[i]).IsMatch(assetPath))
                    {
                        ignore = true;
                        break;
                    }
                }
            }

            return ignore;
        }
        public static bool IsExcludeGroup(string groupName)
        {
            if (!EditorUserBuildSettings.development )
            {
                foreach(var g in EditorAssetBundleSettings.Groups.Select(o => (AssetBundleGroup)o.Asset))
                {
                    if (g.IsDebug && g.GroupName.Equals(groupName, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
            }
            return false;
        }


        public static bool IsIgnoreAssetPath(string assetPath)
        {
            bool ignore = false;

            if (Directory.Exists(assetPath))
                return true;
            ignore = IsExcludePath(assetPath);
            return ignore;
        }


        public static bool ignoreLoad;

        public static int FindIndex(List<AssetBundleBuild> list, string assetBundleName)
        {
            int index = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].assetBundleName == assetBundleName)
                {
                    index = i;
                    break;
                }
            }
            return index;
        }

        public static AssetBundleBuild GetOrCreate(List<AssetBundleBuild> list, string bundleName, string variant = null)
        {
            int index;
            return GetOrCreate(list, bundleName, variant, out index);
        }
        public static AssetBundleBuild GetOrCreate(List<AssetBundleBuild> list, string bundleName, out int index)
        {
            return GetOrCreate(list, bundleName, null, out index);
        }
        public static AssetBundleBuild GetOrCreate(List<AssetBundleBuild> list, string bundleName, string variant, out int index)
        {
            index = FindIndex(list, bundleName);
            if (index == -1)
            {
                AssetBundleBuild ab = new AssetBundleBuild()
                {
                    assetBundleName = bundleName,
                    assetBundleVariant = variant,
                    assetNames = new string[0],
                    addressableNames = new string[0]
                };
                list.Add(ab);
                index = list.Count - 1;
            }
            return list[index];
        }
        public static void Remove(List<AssetBundleBuild> list, string bundleName)
        {
            int index = FindIndex(list, bundleName);
            if (index >= 0)
            {
                list.RemoveAt(index);
            }
        }
        public static List<string> GetAutoDependencies(List<string> assetPaths)
        {
            HashSet<string> deps = new HashSet<string>();
            HashSet<string> origin = new HashSet<string>(assetPaths);

            foreach (var dep in AssetDatabase.GetDependencies(assetPaths.ToArray(), true))
            {
                if (origin.Contains(dep))
                    continue;
                deps.Add(dep);
            }
            return deps.ToList();
        }

        public static IEnumerable<string> GetAutoDependencies(List<AssetBundleBuild> list)
        {
            HashSet<string> all = new HashSet<string>(list.SelectMany(o => o.assetNames), StringComparer.InvariantCultureIgnoreCase);

            foreach (var assetPath in AssetDatabase.GetDependencies(all.ToArray()))
            {
                if (Path.GetFileName(assetPath) == "LightingData.asset")
                    continue;
                if (assetPath.EndsWith(".cs"))
                    continue;
                if (assetPath.EndsWith(".shader", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (all.Contains(assetPath))
                    {
                        continue;
                    }
                    yield return assetPath;
                }
            }
        }

        public static List<string> GetAllDependenciesMultiRef(List<AssetBundleBuild> list, Func<string, bool> filter = null)
        {

            Dictionary<string, List<string>> allDeps = new Dictionary<string, List<string>>();
            HashSet<string> abAssetPaths = new HashSet<string>();

            foreach (var item in list)
            {
                foreach (var assetPath in item.assetNames)
                {
                    abAssetPaths.Add(assetPath);
                }
            }

            foreach (var item in list)
            {
                foreach (var dep in AssetDatabase.GetDependencies(item.assetNames, true))
                {
                    if (IsExclude(dep))
                        continue;
                    if (filter != null && !filter(dep))
                        continue;
                    if (abAssetPaths.Contains(dep))
                        continue;

                    if (allDeps.ContainsKey(dep))
                    {
                        allDeps[dep].Add(item.assetBundleName);
                    }
                    else
                    {
                        var tmp = new List<string>();
                        tmp.Add(item.assetBundleName);
                        allDeps.Add(dep, tmp);
                    }
                }
            }

            List<string> result = new List<string>();
            foreach (var item in allDeps)
            {
                string assetPath = item.Key;
                if (item.Value.Count > 1)
                {
                    result.Add(assetPath);
                }
            }

            return result;
        }

        public static bool IsExclude(string assetPath)
        {
            //场景光照信息只能和场景包一起
            if (Path.GetFileName(assetPath) == "LightingData.asset")
                return true;

            if (EditorAssetBundleSettings.ExcludeExtensions != null && EditorAssetBundleSettings.ExcludeExtensions.Length > 0)
            {
                var excludes = EditorAssetBundleSettings.ExcludeExtensions;
                for (int i = 0, len = excludes.Length; i < len; i++)
                {
                    var exclude = excludes[i];
                    if (!string.IsNullOrEmpty(exclude) && Path.GetExtension(assetPath).Equals(exclude, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
            }
            if (EditorAssetBundleSettings.ExcludeTypeNames != null && EditorAssetBundleSettings.ExcludeTypeNames.Length > 0)
            {
                var excludes = EditorAssetBundleSettings.ExcludeTypeNames;
                for (int i = 0, len = excludes.Length; i < len; i++)
                {
                    var exclude = excludes[i];
                    if (!string.IsNullOrEmpty(exclude))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
                        if (obj && obj.GetType().FullName == exclude)
                            return true;
                    }
                }
            }
            return false;
        }
        public static bool ClearExcludeDependency(List<AssetBundleBuild> items)
        {
            HashSet<string> excludePaths = new HashSet<string>();

            if (EditorAssetBundleSettings.ExcludeDependencyExtensions != null && EditorAssetBundleSettings.ExcludeDependencyExtensions.Length > 0)
            {
                var excludes = EditorAssetBundleSettings.ExcludeDependencyExtensions;
                for (int i = 0, len = excludes.Length; i < len; i++)
                {
                    var exclude = excludes[i];
                    for (int j = 0; j < items.Count; j++)
                    {
                        AssetBundleBuild bundleBuild = items[j];
                        string[] assetPaths = bundleBuild.assetNames;
                        string assetPath;
                        for (int k = 0; k < assetPaths.Length; k++)
                        {
                            assetPath = assetPaths[k];
                            if (!string.IsNullOrEmpty(exclude) && Path.GetExtension(assetPath).Equals(exclude, StringComparison.InvariantCultureIgnoreCase))
                            {
                                excludePaths.Add(assetPath);
                            }
                        }
                    }
                }
            }

            HashSet<string> excludeDepPaths = new HashSet<string>(AssetDatabase.GetDependencies(excludePaths.ToArray(), true));
            foreach (var assetPath in excludePaths)
                excludeDepPaths.Remove(assetPath);

            for (int i = 0; i < items.Count; i++)
            {
                AssetBundleBuild bundleBuild = items[i];
                string[] assetPaths = bundleBuild.assetNames;
                string[] addressableNames = bundleBuild.addressableNames;
                string assetPath;
                int changed = 0;
                for (int j = 0; j < assetPaths.Length; j++)
                {
                    assetPath = assetPaths[j];
                    if (excludeDepPaths.Contains(assetPath))
                    {
                        assetPaths[j] = null;
                        addressableNames[j] = null;
                        changed++;
                    }
                }
                if (changed > 0)
                {
                    if (assetPaths.Length > changed)
                    {
                        bundleBuild.assetNames = assetPaths.Where(o => o != null).ToArray();
                        bundleBuild.addressableNames = addressableNames.Where(o => o != null).ToArray();
                    }
                    else
                    {
                        items.RemoveAt(i);
                        i--;
                    }
                }
            }

            return false;
        }

        #region PreProcessBuild

        [PreProcessBuild(1)]
        static void PreProcessBuild_BuildAssetBundles()
        {
            if (!EditorAssetBundleSettings.Enabled && EditorAssetBundleSettings.PreBuildPlayerSettings.AutoBuildAssetBundle)
                return;
            //string outputPath = GetNextOutputPath();

            Build();
        }

        //[MenuItem(MenuPrefix + "CopyToBuildPath")]

        static void CopyToStreamingAssetsPath()
        {
            string path = GetStreamingAssetsPath();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("StreamingAssetsPath null");
                return;
            }
            string[] excludeGroups = null;
            if (!string.IsNullOrEmpty(EditorAssetBundleSettings.StreamingAssetsExcludeGroup))
                excludeGroups = EditorAssetBundleSettings.StreamingAssetsExcludeGroup.Split('|');
            CopyToReleasePath(path, excludeGroups);
            AssetDatabase.Refresh();
        }

        public static void CopyToReleasePath(string copyToPath, string[] excludeGroup = null)
        {
            if (string.IsNullOrEmpty(copyToPath))
                return;

            //string platformName = GetPlatformName();
            //string parentDir = Path.GetDirectoryName(copyToPath);

            //if (Directory.Exists(parentDir))
            //{
            //    foreach (var dir in Directory.GetDirectories(parentDir, "*", SearchOption.TopDirectoryOnly))
            //    {
            //        DirectoryInfo dirInfo = new DirectoryInfo(dir);
            //        if (string.Equals(dirInfo.Name, platformName, StringComparison.InvariantCultureIgnoreCase))
            //            continue;
            //        dirInfo.Attributes = FileAttributes.Normal;
            //        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            //        {
            //            FileInfo fileInfo = new FileInfo(file);
            //            fileInfo.Attributes = FileAttributes.Normal;
            //        }
            //        Directory.Delete(dir, true);
            //        Debug.Log("delete dir:" + dir);
            //    }
            //}
            SyncDirectory(GetOutputPath(), o =>
            {
                if (o.EndsWith(".manifest", StringComparison.InvariantCultureIgnoreCase))
                    return false;
                if (excludeGroup != null)
                {
                    foreach (var group in excludeGroup)
                    {
                        if (AssetBundles.IsBundleGroup(o, group))
                            return false;
                    }
                }

                return true;
            }, copyToPath, o =>
            !o.EndsWith(".meta"));
            if (excludeGroup != null && excludeGroup.Length > 0)
            {
                string path = Path.Combine(copyToPath, FormatString(AssetBundleSettings.VersionFile));
                var version = AssetBundleVersion.LoadFromFile(path);
                version.groups = version.groups.Where(o => !excludeGroup.Contains(o)).ToArray();
                AssetBundleVersion.Save(path, version);
            }
            Debug.LogFormat(BuildLogPrefix + "release :{0}", copyToPath);
        }

        /// <summary>
        /// 运行在编辑器时通过<see cref="AssetDatabase.GetAllAssetBundleNames"/>获取资源
        /// </summary>
        //[PostProcessScene]
        //static void PostProcessScene()
        //{
        //    UpdateConfig();
        //}

        [PostProcessBuild]
        static void PostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (!EditorAssetBundleSettings.Enabled)
                return;
            if (!EditorAssetBundleSettings.PostBuildPlayerSettings.ClearStreamingAssets)
                return;
            //打包结束后删除资源
            string copyPath = GetStreamingAssetsPath();
            if (string.IsNullOrEmpty(copyPath))
                return;
            LastBuildCopyPath = copyPath;
            if (!string.IsNullOrEmpty(copyPath))
            {
                if (Directory.Exists(copyPath))
                {
                    Directory.Delete(copyPath, true);
                    LastBuildCopyPath = null;
                    AssetDatabase.Refresh();
                    Debug.Log("delete copy path");
                }
            }
        }



        #endregion
        static bool PathEqual(string path1, string path2)
        {
            path1 = ReplaceDirectorySeparatorChar(path1);
            path2 = ReplaceDirectorySeparatorChar(path2);
            return string.Equals(path1, path2, StringComparison.InvariantCultureIgnoreCase);
        }

        static bool PathStartsWith(string path1, string startPath)
        {
            path1 = ReplaceDirectorySeparatorChar(path1);
            startPath = ReplaceDirectorySeparatorChar(startPath);
            return path1.StartsWith(startPath, StringComparison.InvariantCultureIgnoreCase);
        }

        static bool PathContains(string path1, string subPath)
        {
            path1 = ReplaceDirectorySeparatorChar(path1);
            subPath = ReplaceDirectorySeparatorChar(subPath);
            return path1.IndexOf(subPath, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        public static bool PathDirectoryStartsWith(string path1, string startPath)
        {
            path1 = ReplaceDirectorySeparatorChar(path1);
            startPath = ReplaceDirectorySeparatorChar(startPath);
            if (!path1.EndsWith("" + Path.DirectorySeparatorChar))
                path1 += Path.DirectorySeparatorChar;
            if (!startPath.EndsWith("" + Path.DirectorySeparatorChar))
                startPath += Path.DirectorySeparatorChar;
            return path1.StartsWith(startPath, StringComparison.InvariantCultureIgnoreCase);
        }

        internal static string ReplaceDirectorySeparatorChar(string path)
        {
            if (path == null)
                return null;
            if (Path.DirectorySeparatorChar == '/')
            {
                path = path.Replace('\\', '/');
            }
            else
            {
                path = path.Replace('/', '\\');
            }
            return path;
        }
        class AssetBundleInfo
        {
            public string field;
            public string AssetBundleName;
            public List<string> Tags = new List<string>();

        }

        class AssetInfo
        {
            public AssetBundleInfo abInfo;
            public string field;
            public string assetPath;
            public string addressableName;
        }

        static XmlDocument GetAssetBundleNamesData()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();

            Dictionary<string, Dictionary<string, AssetInfo>> classes = new Dictionary<string, Dictionary<string, AssetInfo>>();
            string assetBundleNamesClassName = "AssetBundleNames";


            string[] abNameAndVariants = AssetDatabase.GetAllAssetBundleNames().OrderBy(o => o).ToArray();
            HashSet<string> abNames = new HashSet<string>();
            Dictionary<string, AssetBundleInfo> abFields = new Dictionary<string, AssetBundleInfo>();
            Dictionary<string, HashSet<string>> abVariants = new Dictionary<string, HashSet<string>>();
            HashSet<string> abPreloadeds = new HashSet<string>();
            Dictionary<string, object> formatValues = new Dictionary<string, object>();

            for (int i = 0; i < abNameAndVariants.Length; i++)
            {
                string abNameAndVariant = abNameAndVariants[i];
                string abField = null;
                string abName = null;
                AssetBundleInfo abInfo = null;
                foreach (var assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(abNameAndVariant))
                {
                    formatValues.Clear();
                    GetFormatValues(assetPath, formatValues);
                    string variantName = AssetDatabase.GetImplicitAssetBundleVariantName(assetPath);

                    if (abField == null)
                    {
                        if (string.IsNullOrEmpty(variantName))
                        {
                            abName = abNameAndVariant;
                        }
                        else
                        {
                            abName = abNameAndVariant.Substring(0, abNameAndVariant.Length - variantName.Length - 1);
                        }
                        abField = abName + "_AssetBundle";

                        abField = FileNameToSafeChar(abField);
                        if (!abNames.Contains(abName))
                        {
                            abNames.Add(abName);
                            abInfo = new AssetBundleInfo() { field = abField, AssetBundleName = abName };
                            abFields[abName] = abInfo;
                            abVariants[abName] = new HashSet<string>();
                        }
                        else
                        {
                            abInfo = abFields[abName];
                        }
                    }

                    if (!string.IsNullOrEmpty(variantName))
                    {
                        abVariants[abName].Add(variantName);
                    }


                    string className = assetBundleNamesClassName;
                    if (!string.IsNullOrEmpty(EditorAssetBundleSettings.AssetBundleNamesClassSettings.AssetNameClass))
                        className = FileNameToSafeChar(FormatString(EditorAssetBundleSettings.AssetBundleNamesClassSettings.AssetNameClass, assetPath));

                    string assetField;
                    string addressableName = null;
                    addressableName = GetAddressableName(assetPath, formatValues);

                    //assetField = FileNameToSafeChar(GetFormatValue(formatValues, FormatArg_AssetName));
                    assetField = FileNameToSafeChar(Path.GetFileNameWithoutExtension(assetPath));

                    /*
                    for (int j = 0; j < config.Items.Count; j++)
                    {
                        var itemConfig = config.Items[j];

                        if (!string.IsNullOrEmpty(itemConfig.Directory))
                        {
                            if (!PathDirectoryStartsWith(Path.GetDirectoryName(assetPath), itemConfig.Directory))
                                continue;
                        }
                        else if (!string.IsNullOrEmpty(itemConfig.File))
                        {
                            if (!string.Equals(assetPath, itemConfig.File))
                                continue;
                        }
                        else
                        {
                            continue;
                        }
                        if (!string.IsNullOrEmpty(itemConfig.Filter) && !Utils.IsMatchPath(assetPath, itemConfig.Filter))
                            continue;

                        if (!string.IsNullOrEmpty(itemConfig.AssetClass))
                            className = FileNameToSafeChar(GetFormatString(itemConfig.AssetClass, formatValues));
                        if (itemConfig.Preloaded)
                        {
                            if (!abPreloadeds.Contains(abNameAndVariant))
                            {
                                abPreloadeds.Add(abNameAndVariant);
                            }
                        }
                        else
                        {
                            if (abPreloadeds.Contains(abNameAndVariant))
                            {
                                abPreloadeds.Remove(abNameAndVariant);
                            }
                        }

                        if (itemConfig.Tags != null && itemConfig.Tags.Length > 0)
                        {
                            foreach (var tag in itemConfig.Tags)
                            {
                                if (!abInfo.Tags.Contains(tag))
                                    abInfo.Tags.Add(tag);
                            }
                        }

                    }*/


                    Dictionary<string, AssetInfo> fields;
                    if (!classes.TryGetValue(className, out fields))
                    {
                        fields = new Dictionary<string, AssetInfo>();
                        classes[className] = fields;
                    }

                    fields[assetField] = new AssetInfo() { field = assetField, abInfo = abInfo, assetPath = assetPath, addressableName = addressableName };
                    //new string[] { abField, assetPath, addressableName };
                }


            }

            XmlDocument doc = new XmlDocument();
            XmlNode absNode = doc.CreateElement("AssetBundles");
            Dictionary<string, XmlNode> absNodes = new Dictionary<string, XmlNode>();

            foreach (var abName in abNames)
            {

                AssetBundleInfo abField = abFields[abName];

                if (string.IsNullOrEmpty(abField.field))
                {
                    Debug.Log("abfield null, assetbundle name: " + abName);
                    continue;
                }

                //int tmp = -1;
                //for (int j = i - 1; j >= 0; j--)
                //{
                //    if (abFields[i] == abFields[j])
                //    {
                //        tmp = j;
                //        break;
                //    }
                //}
                //if (tmp != -1)
                //    continue;

                XmlNode abNode = doc.CreateElement("AssetBundle");
                var attr = doc.CreateAttribute("Name");
                attr.Value = abName;
                abNode.Attributes.Append(attr);
                attr = doc.CreateAttribute("Field");
                attr.Value = abField.field;
                abNode.Attributes.Append(attr);
                attr = doc.CreateAttribute("FieldValue");
                attr.Value = abName;
                abNode.Attributes.Append(attr);

                attr = doc.CreateAttribute("Preloaded");
                if (abPreloadeds.Contains(abName))
                {
                    attr.Value = "true";
                }
                else
                {
                    attr.Value = "false";
                }
                abNode.Attributes.Append(attr);

                if (abVariants[abName] != null && abVariants[abName].Count > 0)
                {
                    XmlNode variantsNode = doc.CreateElement("Variants");

                    foreach (var variantName in abVariants[abName])
                    {
                        XmlNode variantNode = doc.CreateElement("Variant");
                        variantNode.InnerText = variantName;
                        variantsNode.AppendChild(variantNode);
                    }

                    abNode.AppendChild(variantsNode);
                }


                XmlElement elmTags = doc.CreateElement("Tags");
                if (abField.Tags.Count > 0)
                {
                    foreach (var tag in abField.Tags)
                    {
                        XmlElement elmTag = doc.CreateElement("Tag");
                        elmTag.InnerText = tag;
                        elmTags.AppendChild(elmTag);
                    }
                }
                abNode.AppendChild(elmTags);

                absNode.AppendChild(abNode);

                absNodes[abField.field] = abNode;
            }

            List<string> components = new List<string>();

            foreach (var item in classes.OrderBy(o => o.Key).OrderBy(o => o.Key == assetBundleNamesClassName ? 0 : 1))
            {
                string className = item.Key;

                foreach (var fields in item.Value.OrderBy(o => o.Value.assetPath))
                {
                    string field = fields.Key;
                    var values = fields.Value;
                    string assetPath = values.assetPath;
                    AssetBundleInfo abInfo = values.abInfo;
                    XmlNode assetNode = doc.CreateElement("Asset");
                    XmlAttribute attr = doc.CreateAttribute("Field");
                    attr.Value = field;
                    assetNode.Attributes.Append(attr);

                    attr = doc.CreateAttribute("FieldValue");
                    attr.Value = values.addressableName;
                    assetNode.Attributes.Append(attr);
                    attr = doc.CreateAttribute("Class");
                    attr.Value = className;
                    assetNode.Attributes.Append(attr);

                    attr = doc.CreateAttribute("Path");
                    attr.Value = assetPath;
                    assetNode.Attributes.Append(attr);

                    attr = doc.CreateAttribute("Name");
                    attr.Value = Path.GetFileNameWithoutExtension(assetPath);
                    assetNode.Attributes.Append(attr);

                    string assetId = AssetDatabase.AssetPathToGUID(assetPath);
                    attr = doc.CreateAttribute("Id");
                    attr.Value = assetId;
                    assetNode.Attributes.Append(attr);

                    attr = doc.CreateAttribute("FileName");
                    attr.Value = Path.GetFileName(assetPath);
                    assetNode.Attributes.Append(attr);
                    var abNode = absNodes[values.abInfo.field];
                    string assetType = "";

                    string extension = Path.GetExtension(assetPath);

                    var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
                    if (!asset)
                    {
                        Debug.LogError("LoadAsset null, assetPath:" + assetPath);
                        continue;
                    }
                    assetType = asset.GetType().Name;

                    attr = doc.CreateAttribute("Type");
                    attr.Value = assetType;
                    assetNode.Attributes.Append(attr);


                    components.Clear();
                    if (asset is GameObject)
                    {
                        var go = asset as GameObject;
                        if (EditorAssetBundleSettings.Components != null)
                        {
                            foreach (var cptType in EditorAssetBundleSettings.Components)
                            {
                                var cpt = go.GetComponent(cptType);
                                if (cpt)
                                {
                                    components.Add(cptType);
                                }
                            }
                        }
                    }

                    if (components.Count > 0)
                    {
                        var cptsElem = doc.CreateElement("Components");
                        foreach (var cpt in components)
                        {
                            var cptElem = doc.CreateElement("Component");
                            cptElem.InnerText = cpt;
                            cptsElem.AppendChild(cptElem);
                        }

                        assetNode.AppendChild(cptsElem);
                    }


                    abNode.AppendChild(assetNode);
                }


            }

            doc.AppendChild(absNode);

            //if (EditorAssetBundles.Logger.logEnabled)
            {
                if (!Directory.Exists("Temp/gen"))
                    Directory.CreateDirectory("Temp/gen");
                doc.Save("Temp/gen/AssetBundleNames.xml");
            }
            assetBundleData = doc;
            return doc;
        }


        /// <summary>
        /// 不自动生成新的资源名称类，避免少资源或者资源重命名导致编译失败无法正常打包和开发
        /// </summary>
        // [MenuItem(EditorAssetBundles.MenuPrefix + "gen AssetBundleNames", priority = EditorAssetBundles.BuildMenuPriority + 2)]
        public static void GenerateAssetBundleNamesClass()
        {
            if (!EditorAssetBundleSettings.AssetBundleNamesClassSettings.Enabled)
            {
                Debug.LogWarning("Generate AssetBundleNames Class canceled. AssetBundleNamesClass.Enabled = false");
                return;
            }
            if (GenerateAssetBundleNamesClass(true))
            {
                AssetDatabase.Refresh();
            }
        }


        public static bool GenerateAssetBundleNamesClass(bool force)
        {
            string tplPath = AssetBundleNamesClassTemplatePath;

            if (string.IsNullOrEmpty(tplPath))
            {
                Debug.LogError("AssetBundleNamesTemplate null");
                return false;
            }

            if (!File.Exists(tplPath))
            {
                Debug.LogError("AssetBundleNames Template file not exisits. " + tplPath);
                return false;
            }

            using (var progressBar = new EditorProgressBar($"Generate {EditorAssetBundleSettings.AssetBundleNamesClassSettings.FilePath}"))
            {
                string assetBundleNamesClassName = "AssetBundleNames";
                progressBar.OnProgress($"load  {assetBundleNamesClassName} class data", 0f);

                XmlDocument doc = GetAssetBundleNamesData();

                progressBar.OnProgress($"generate {assetBundleNamesClassName}.cs code file", 0.5f);

                XsltTemplate tpl = new XsltTemplate();
                tpl.Load(tplPath);
                tpl.Variables["AssetBundleNamesClass"] = assetBundleNamesClassName;
                string[] codeFiles = tpl.Transform(doc);

                string hashPath = codeFiles[0] + ".hash";
                string newHash = codeFiles[0].GetFileHashSHA256();
                string oldHash = null;
                if (File.Exists(hashPath))
                {
                    oldHash = File.ReadAllLines(hashPath)[0];
                }
                bool changed = false;
                if (force || oldHash != newHash)
                    changed = true;

                bool hasError = false;

                string filePath = EditorAssetBundleSettings.AssetBundleNamesClassSettings.FilePath;
                if (!string.IsNullOrEmpty(filePath))
                {

                    if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    filePath.FileClearAttributes();
                    if (!File.Exists(filePath))
                        changed = true;

                    if (changed)
                    {
                        if (Path.GetExtension(EditorAssetBundleSettings.AssetBundleNamesClassSettings.FilePath).ToLower() == ".dll")
                        {
                            string tmpPath = "Temp/" + Path.GetFileName(filePath);
                            if (Directory.Exists(Path.GetDirectoryName(tmpPath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(tmpPath));

                            progressBar.OnProgress("compiler code", 0.8f);
                            if (CompilerCode(tmpPath, codeFiles, new string[] { typeof(UnityEngine.Object).Assembly.Location }))
                            {
                                progressBar.OnProgress("copy to " + filePath, 0.9f);
                                changed = tmpPath.FileCopyIfChanged(filePath);
                            }
                            else
                            {
                                changed = false;
                                hasError = true;
                            }
                            File.Delete(tmpPath);
                        }
                        else
                        {
                            progressBar.OnProgress("copy to " + filePath, 0.9f);
                            changed = codeFiles[0].FileCopyIfChanged(filePath);
                        }
                    }
                }

                for (int i = 0; i < codeFiles.Length; i++)
                {
                    codeFiles[i].FileClearAttributes();
                }


                if (changed)
                {
                    if (!hasError)
                        File.WriteAllText(hashPath, newHash);
                    else
                        return false;
                }
                progressBar.OnProgress("success. generate AssetBundleNames", 1f);
                Debug.Log(BuildLogPrefix + "generate AssetBundleNames ");
            }

            return true;
        }

        /// <summary>
        /// https://docs.microsoft.com/zh-cn/dotnet/fsharp/language-reference/compiler-options
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="codeFiles"></param>
        /// <returns></returns>
        static bool CompilerCode(string outputPath, string[] codeFiles, string[] assemblies = null)
        {
            CodeDomProvider cSharpCodePrivoder = new CSharpCodeProvider();

            CompilerParameters cp = new CompilerParameters();
            cp.ReferencedAssemblies.Add("System.dll");
            cp.ReferencedAssemblies.Add("System.Core.dll");
            cp.GenerateExecutable = false;
            cp.GenerateInMemory = false;
            cp.OutputAssembly = outputPath;
            cp.CompilerOptions = "/optimize";
            cp.TempFiles = new TempFileCollection(Path.GetFullPath("Temp/gen"), true);
            if (assemblies != null)
            {
                foreach (var ass in assemblies)
                    cp.ReferencedAssemblies.Add(ass);
            }
            CompilerResults cr = cSharpCodePrivoder.CompileAssemblyFromFile(cp, codeFiles[0]);

            if (cr.Errors.HasErrors)
            {
                foreach (CompilerError err in cr.Errors)
                {
                    Debug.LogError(err.ErrorText);
                }
                return false;
            }

            return true;
        }

        public static Dictionary<string, string> OvverideSafeChar = new Dictionary<string, string>()
        {
            { "continue","_continue" }
        };

        static string FileNameToSafeChar(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            string tmp;
            if (OvverideSafeChar.TryGetValue(str, out tmp))
                return tmp;

            foreach (var ch in new char[] { '#', '-', ' ', '.' })
                str = str.Replace(ch, '_');

            if (str.Length > 0)
            {
                char[] chs;
                int start = 0;
                if (str[0] >= '0' && str[0] <= '9')
                {
                    chs = new char[str.Length + 1];
                    start = 1;
                    chs[0] = '_';
                }
                else
                {
                    chs = new char[str.Length];
                }
                for (int i = 0; i < str.Length; i++)
                {
                    var ch = str[i];
                    if (!(('a' <= ch && ch <= 'z') || ('A' <= ch && ch <= 'Z') || ('0' <= ch && ch <= '9')))
                    {
                        ch = '_';
                    }
                    chs[start++] = ch;
                }
                str = new string(chs);
            }

            return str;
        }

        static string[] GetSelectedDirections()
        {
            string[] dirs = ToDirections(Selection.assetGUIDs);
            if (dirs.Length > 0)
            {
                if (dirs.Where(o => string.IsNullOrEmpty(o)).Count() > 0)
                    dirs = new string[0];
            }


            //dirs = dirs.Where(o => !ignoreDirs.Contains(o, StringComparer.InvariantCultureIgnoreCase)).ToArray();

            return dirs;
        }

        static string[] ToDirections(string[] assetGuids)
        {
            string[] dirs = new string[assetGuids.Length];
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                if (Directory.Exists(assetPath))
                {
                    dirs[i] = assetPath;
                }
                else
                {
                    dirs[i] = null;
                }
            }
            return dirs;
        }

        #region Utils



        static void CopyFile(string sourceFileName, string destFileName)
        {

            try
            {
                string dir = Path.GetDirectoryName(destFileName);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                FileUtil.DeleteFileOrDirectory(destFileName);
                File.Copy(sourceFileName, destFileName, true);
            }
            catch (Exception ex)
            {
                Debug.LogError("Copy File error:" + sourceFileName + " > " + destFileName);
                throw ex;
            }
        }

        #endregion




        static string GetFormatValue(Dictionary<string, object> values, string key)
        {
            object value;
            if (!values.TryGetValue(key, out value))
                return null;
            string str = null;
            if (value != null)
            {
                if (value is IFormatValueProvider)
                    str = ((IFormatValueProvider)value).GetValue(key);
                else
                    str = value.ToString();
            }
            return str;
        }

        public static void Release()
        {
            string outputManifestDir = GetOutputPath();

            if (!File.Exists(FormatString(AssetBundleSettings.BuildManifestPath)))
            {
                Build();
            }

            AssetBundleVersion version = AssetBundleVersion.LoadFromFile(Path.Combine(outputManifestDir, FormatString(AssetBundleSettings.VersionFile)));
            Dictionary<string, object> formatValues = new Dictionary<string, object>();
            GetFormatValues(formatValues, version);

            string releaseDirectory = FormatString(AssetBundleSettings.ReleasePath, formatValues);
            string releaseManifestDir = Path.GetDirectoryName(FormatString(AssetBundleSettings.DownloadManifestPath, formatValues));
            releaseManifestDir = Path.Combine(releaseDirectory, releaseManifestDir);
            CopyToReleasePath(releaseManifestDir);
            string dstVersionFile = Path.Combine(releaseDirectory, FormatString(AssetBundleSettings.DownloadVersionFile, formatValues));
            string srcVerstionFile = Path.Combine(outputManifestDir, FormatString(AssetBundleSettings.VersionFile, formatValues));
            File.Copy(srcVerstionFile, dstVersionFile, true);
        }




        #region Addressable

        static AssetBundleAddressableAsset addressableAsset;
        public static AssetBundleAddressableAsset AddressableAsset
        {
            get
            {
                if (!addressableAsset)
                {
                    string addressableBundleAssetPath = AssetBundles.AddressableAssetPath;

                    if (File.Exists(addressableBundleAssetPath))
                    {
                        addressableAsset = AssetDatabase.LoadAssetAtPath<AssetBundleAddressableAsset>(addressableBundleAssetPath);
                    }

                    if (!addressableAsset)
                    {
                        addressableAsset = ScriptableObject.CreateInstance<AssetBundleAddressableAsset>();
                        AssetDatabase.CreateAsset(addressableAsset, addressableBundleAssetPath);
                        AssetDatabase.SaveAssets();
                    }
                }
                return addressableAsset;
            }
        }


        public static void CreateAddressableAsset(List<AssetBundleBuild> list)
        {

            var asset = AddressableAsset;
            asset.assets.Clear();

            foreach (var item in list)
            {
                for (int i = 0; i < item.assetNames.Length; i++)
                {
                    AssetBundleAddressableAsset.AssetInfo assetInfo = new AssetBundleAddressableAsset.AssetInfo();
                    assetInfo.assetName = item.assetNames[i];
                    assetInfo.guid = AssetDatabase.AssetPathToGUID(item.assetNames[i]);
                    assetInfo.bundleName = item.assetBundleName;
                    if (item.addressableNames != null && i < item.addressableNames.Length)
                        assetInfo.assetName = item.addressableNames[i];
                    asset.assets.InsertSorted(assetInfo);
                }
                //asset.bundles.Add(bundleInfo);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        #endregion

        #region AssetBundle Group



        public static string GetAssetName(AssetBundleGroup g, string assetPath)
        {
            var item = FindGroupItem(g, assetPath);
            return GetAssetName(item, assetPath);
        }

        public static string GetAssetName(AssetBundleGroup.BundleItem item, string assetPath)
        {

            string assetName;
            if (item != null)
            {
                if (!string.IsNullOrEmpty(item.assetName))
                {
                    assetName = BuildAssetBundles.FormatString(item.assetName, assetPath);
                    if (item.assetNameToLower)
                        assetName = assetName.ToLower();
                    return assetName;
                }
            }
            return BuildAssetBundles.GetAddressableName(assetPath);
        }

        public static AssetBundleGroup FindGroup(string assetPath, out AssetBundleGroup.BundleItem item)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            for (int i = EditorAssetBundleSettings.Groups.Length - 1; i >= 0; i--)
            {
                var g = EditorAssetBundleSettings.Groups[i].Asset as AssetBundleGroup;
                if (!g)
                    continue;

                bool matching = false;

                for (int j = g.items.Count - 1; j >= 0; j--)
                {
                    var it = g.items[j];

                    if (IsMatch(it, assetPath))
                    {
                        matching = true;
                        if (it.excludeGuids.Contains(guid))
                            break;
                        item = it;
                        return g;
                    }
                }
                if (matching)
                    break;
            }
            item = null;
            return null;
        }

        public static AssetBundleGroup.BundleItem FindGroupItem(AssetBundleGroup g, string assetPath)
        {
            for (int i = g.items.Count - 1; i >= 0; i--)
            {
                var item = g.items[i];
                if (IsMatch(item, assetPath))
                    return item;
            }
            return null;
        }
        public static bool IsMatch(AssetBundleGroup.BundleItem item, string assetPath)
        {
            if (string.IsNullOrEmpty(item.include))
            {
                return false;
            }

            return AssetBundles.IsMatchIncludeExclude(assetPath, item.include, item.exclude);
        }


        public static void ValidateGroup()
        {
            string[] localBundles = AssetDatabase.GetAllAssetBundleNames().Where(o => AssetBundles.IsBundleGroup(o, LocalGroupName)).ToArray();
            bool hasError = false;

            foreach (var localBundle in localBundles)
            {
                foreach (var dep in AssetDatabase.GetAssetBundleDependencies(localBundle, true))
                {
                    if (!AssetBundles.IsBundleGroup(dep, LocalGroupName))
                    {
                        Debug.LogError(BuildLogPrefix + $"<{localBundle}> bundle dependency <{dep}>");
                        hasError = true;
                    }
                }
            }

            if (hasError)
                throw new Exception($"{LocalGroupName} bundle can't dependency group bundle");
        }

        static void RequireLocalGroup()
        {
            if (!EditorAssetBundleSettings.LocalGroup)
                throw new Exception("local group null");
        }
        #endregion


    }
}