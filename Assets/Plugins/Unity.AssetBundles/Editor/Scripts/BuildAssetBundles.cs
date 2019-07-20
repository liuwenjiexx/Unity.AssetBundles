using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Template.Xslt;
using UnityEditor.Callbacks;
using UnityEngine;


namespace UnityEditor.Build.AssetBundle
{

    public sealed class BuildAssetBundles
    {


        private static string packageDir;
        private static string ConfigFilePath = "ProjectSettings/AssetBundleConfig.json";
        private const string MenuPrefix = "Build/AssetBundle/";


        private static BuildAssetBundleConfig config;
        private static DateTime lastConfigWriteTime;

        private const string FormatArg_BuildTarget = "BuildTarget";
        private const string FormatArg_Platform = "Platform";
        private const string FormatArg_Directory = "Directory";
        private const string FormatArg_FileName = "FileName";
        private const string FormatArg_FileExtension = "FileExtension";
        private const string FormatArg_AssetName = "AssetName";
        private const string FormatArg_AssetPath = "AssetPath";

        public static string PackageDir
        {
            get
            {
                if (string.IsNullOrEmpty(packageDir))
                {
                    packageDir = GetPackageDirectory("Unity.Assetbundles");
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


        public static BuildAssetBundleConfig Config
        {
            get
            {
                LoadConfig();
                return config;
            }
        }

        private static string GetPackageDirectory(string packageName)
        {
            foreach (var dir in Directory.GetDirectories("Assets", "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(dir), packageName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return dir;
                }
            }

            string path = Path.Combine("Packages", packageName);
            if (Directory.Exists(path))
            {
                return path;
            }

            return null;
        }

        [OnEditorApplicationOpen]
        static void OnEditorApplicationOpen()
        {

            string filePath = Path.GetFullPath(ConfigFilePath);

            if (File.Exists(filePath))
            {
                UpdateAllAssetBundleNames();
            }
        }

        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {

            string filePath = Path.GetFullPath(ConfigFilePath);

            if (File.Exists(filePath))
            {
                FileSystemWatcher fsw = new FileSystemWatcher();
                fsw.Path = Path.GetDirectoryName(filePath);
                fsw.Filter = Path.GetFileName(filePath);
                fsw.NotifyFilter = NotifyFilters.LastWrite;
                fsw.Changed += OnFileSystemWatcher;
                fsw.EnableRaisingEvents = true;
            }
        }

        static void OnFileSystemWatcher(object sender, FileSystemEventArgs e)
        {
            if (File.Exists(e.FullPath))
            {
                EditorApplication.delayCall += () =>
                {
                    config = null;
                    LoadConfig();
                    UpdateAllAssetBundleNames();
                };
            }
        }


        //[MenuItem("Assets/Add AssetBundle")]
        public static void AddBuildAssetBundles()
        {

            string[] dirs = GetSelectedDirections();

            bool changed = false;

            foreach (var dir in dirs)
            {
                var item = Config.Items.Where(o => PathEqual(o.Directory, dir))
                    .FirstOrDefault();
                if (item == null)
                {
                    item = new BuildAssetBundleItem()
                    {
                        Directory = dir,
                    };
                    Config.Items.Add(item);
                    changed = true;
                }
            }
            if (changed)
            {
                SaveConfig();
            }

        }


        //[MenuItem("Assets/Add AssetBundle", validate = true)]
        public static bool AddBuildAssetBundles_Validate()
        {
            var dirs = GetSelectedDirections();
            if (dirs.Length == 0)
                return false;

            bool changed = false;

            foreach (var dir in dirs)
            {
                var item = Config.Items.Where(o => PathEqual(o.Directory, dir))
                    .FirstOrDefault();
                if (item == null)
                {
                    changed = true;
                    break;
                }
            }
            return changed;
        }

        private static Dictionary<string, object> GetFormatValues(string filePath)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            GetFormatValues(filePath, values);
            return values;
        }

        private static void GetFormatValues(string filePath, Dictionary<string, object> values)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;

            values[FormatArg_BuildTarget] = buildTarget.ToString();
            values[FormatArg_Platform] = GetPlatformName(buildTarget);

            if (!string.IsNullOrEmpty(filePath))
            {
                values[FormatArg_Directory] = Path.GetFileName(Path.GetDirectoryName(filePath));
                values[FormatArg_FileName] = Path.GetFileName(filePath);
                values[FormatArg_FileExtension] = Path.GetExtension(filePath);
                values[FormatArg_AssetName] = Path.GetFileNameWithoutExtension(filePath);
                values[FormatArg_AssetPath] = filePath;
            };
        }

        private static string GetFormatString(string input, string filePath = null, Dictionary<string, object> values = null)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            if (values == null)
            {
                values = new Dictionary<string, object>();
                GetFormatValues(filePath, values);
            }
            input = FormatString(input, StringRegexFormatProvider.Instance, values);

            return input;
        }
        private static string GetFormatString(string input, Dictionary<string, object> values)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            input = FormatString(input, StringRegexFormatProvider.Instance, values);

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
        [MenuItem(MenuPrefix + "Remove Unused AssetBundle Names")]
        public static void RemoveUnusedAssetBundleNames()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }
        //[MenuItem("Assets/Remove AssetBundle")]
        public static void RemoveBuildAssetBundles()
        {

            string[] dirs = GetSelectedDirections();
            bool changed = false;

            foreach (var dir in dirs)
            {
                var item = Config.Items.Where(o => PathEqual(o.Directory, dir))
                    .FirstOrDefault();
                if (item != null)
                {
                    Config.Items.Remove(item);
                    changed = true;
                }
            }
            if (changed)
            {
                SaveConfig();
            }
        }


        //[MenuItem("Assets/Remove AssetBundle", validate = true)]
        public static bool RemoveBuildAssetBundles_Validate()
        {
            string[] dirs = GetSelectedDirections();
            if (dirs.Length == 0)
                return false;
            bool changed = false;

            foreach (var dir in dirs)
            {
                var item = Config.Items.Where(o => PathEqual(o.Directory, dir))
                    .FirstOrDefault();
                if (item != null)
                {
                    changed = true;
                    break;
                }
            }
            return changed;
        }

        [MenuItem(MenuPrefix + "Build", priority = 0)]
        public static void Build()
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            string outputPath = GetOutputPath();

            Build(outputPath);

            string copyPath = GetCopyToPath();
            if (!string.IsNullOrEmpty(copyPath))
            {
                CopyDirectory(outputPath, copyPath);
                Debug.LogFormat("copy AssetBundles\nsrc:{0}\ndst:{1}", outputPath, copyPath);
            }
        }


        [MenuItem(MenuPrefix + "Open Output Path", priority = 2)]
        public static void OpenOutputPath()
        {
            string outputPath = GetOutputPath();
            EditorUtility.RevealInFinder(outputPath);
        }
        [MenuItem(MenuPrefix + "Edit Config", priority = 1)]
        public static void OpenConfigFile()
        {
            EditorUtility.OpenWithDefaultApp(ConfigFilePath);
        }


        [MenuItem(MenuPrefix + "ListAssetBundle", priority = 3)]
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

            //foreach (var abName in AssetDatabase.GetAllAssetBundleNames())
            //{
            //    foreach (var assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(abName))
            //    {

            //    }
            //}
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

        public static string GetOutputPath()
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            string outputPath = Config.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
                throw new Exception("OutputPath empty");
            outputPath = GetFormatString(outputPath);
            return outputPath;
        }
        public static string GetCopyToPath()
        {
            string path = Config.CopyTo;
            if (!string.IsNullOrEmpty(path))
                path = GetFormatString(path);

            return path;
        }
        public static string GetBuildCopyToPath()
        {
            string path = Config.BuildCopyTo;
            if (!string.IsNullOrEmpty(path))
                path = GetFormatString(path);

            return path;
        }


        public static void Build(string outputPath)
        {

            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("outputPath empty");

            BuildTarget buildTarget;

            buildTarget = EditorUserBuildSettings.activeBuildTarget;

            var allAssets = UpdateAllAssetBundleNames();

            DeleteUnusedAssetBundles(outputPath);
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            List<AssetBundleBuild> list = new List<AssetBundleBuild>();
            Dictionary<string, object> formatValues = new Dictionary<string, object>();

            foreach (var assetBundle in AssetDatabase.GetAllAssetBundleNames())
            {

                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundle);
                if (assetPaths.Length == 0)
                    continue;
                AssetBundleBuild assetBundleBuild = new AssetBundleBuild();

                string[] addressableNames = new string[assetPaths.Length];

                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string assetPath = assetPaths[i];
                    formatValues.Clear();
                    GetFormatValues(assetPaths[i], formatValues);
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
            BuildAssetBundleOptions options = BuildAssetBundleOptions.None;
            if (!string.IsNullOrEmpty(Config.Options))
                options = (BuildAssetBundleOptions)Enum.Parse(typeof(BuildAssetBundleOptions), Config.Options);

            BuildPipeline.BuildAssetBundles(outputPath, list.ToArray(), options, buildTarget);

            LastBuildCopyPath = GetBuildCopyToPath();
            CopyToBuildPath();

            Debug.LogFormat("build AssetBundles completed\noutput:{0}", outputPath);
            if (config.BuildGenerateAssetBundleNamesClass)
            {
                GenerateAssetBundleNamesClass(false);
            }

            AssetDatabase.Refresh();

        }

        //public static void GetAssetBundleName(string assetPath, out string assetBundleName, out string variant)
        //{
        //    var itemConfig = FindMatchConfig(assetPath);

        //    GetAssetBundleName(itemConfig, assetPath, out assetBundleName, out variant);
        //}
        static void GetAssetBundleName(BuildAssetBundleItem itemConfig, string assetPath, ref string assetBundleName, ref string variant, Dictionary<string, object> values = null)
        {
            if (itemConfig == null)
            {
                return;
            }

            if (itemConfig.Ignore)
            {
                assetBundleName = string.Empty;
                variant = string.Empty;
                return;
            }

            string exName = Path.GetExtension(assetPath);

            variant = GetVariant(itemConfig, exName);
            if (!string.IsNullOrEmpty(variant))
            {
                if (values == null)
                    values = GetFormatValues(assetPath);
                variant = GetFormatString(variant, values).ToLower();
            }

            if (!string.IsNullOrEmpty(itemConfig.AssetBundleName))
            {
                if (values == null)
                    values = GetFormatValues(assetPath);
                assetBundleName = GetFormatString(itemConfig.AssetBundleName, values).ToLower();
            }



        }

        //static void GetAssetBundleName(string assetPath, out string assetBundleName, out string variant, Dictionary<string, object> values = null)
        //{
        //    var config = Config;

        //    assetBundleName = string.Empty;
        //    variant = string.Empty;

        //    if (!string.IsNullOrEmpty(config.AssetBundleName))
        //        assetBundleName = config.AssetBundleName;
        //    variant = config.Variants;

        //    foreach (var itemConfig in FindMatchConfigs(assetPath))
        //    {
        //        if (itemConfig.Ignore)
        //        {
        //            assetBundleName = string.Empty;
        //            variant = string.Empty;
        //            break;
        //        }
        //        if (!string.IsNullOrEmpty(config.AssetBundleName))
        //            assetBundleName = config.AssetBundleName;
        //        variant = itemConfig.Variants;
        //    }

        //    if (!string.IsNullOrEmpty(assetBundleName))
        //    {
        //        if (values == null)
        //            values = GetFormatValues(assetPath);
        //        assetBundleName = GetFormatString(assetBundleName, values).ToLower();
        //    }
        //    if (!string.IsNullOrEmpty(assetBundleName))
        //    {
        //        if (values == null)
        //            values = GetFormatValues(assetPath);
        //        variant = GetFormatString(variant, values).ToLower();
        //    }
        //}

        public static string GetAddressableName(string assetPath, Dictionary<string, object> values = null)
        {

            var config = Config;

            string addressableName = null;
            if (values == null)
                values = GetFormatValues(assetPath);

            if (!string.IsNullOrEmpty(config.AssetName))
                addressableName = GetFormatString(config.AssetName, values);

            foreach (var itemConfig in FindMatchConfigs(assetPath))
            {
                if (itemConfig.Ignore)
                {
                    break;
                }
                if (!string.IsNullOrEmpty(itemConfig.AssetName))
                {
                    addressableName = GetAddressableName(itemConfig, assetPath, values);
                    break;
                }
            }

            if (string.IsNullOrEmpty(addressableName))
                addressableName = assetPath;
            return addressableName;
        }

        static string GetAddressableName(BuildAssetBundleItem itemConfig, string assetPath, Dictionary<string, object> values)
        {
            string assetName = null;
            if (itemConfig != null)
                assetName = itemConfig.AssetName;
            if (string.IsNullOrEmpty(assetName))
                assetName = config.AssetName;

            if (!string.IsNullOrEmpty(assetName))
                assetName = GetFormatString(assetName, assetPath, values);
            if (string.IsNullOrEmpty(assetName))
                assetName = assetPath;
            return assetName;
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

        public static bool IsPreloadedAssetBundleByAssetpath(string assetPath)
        {
            bool preloaded = false;

            foreach (var itemConfig in FindMatchConfigs(assetPath))
            {
                if (itemConfig.Ignore)
                {
                    preloaded = false;
                    break;
                }
                preloaded = itemConfig.Preloaded;
            }
            return preloaded;
        }

        static BuildAssetBundleItem FindMatchConfig(string assetPath)
        {
            return FindMatchConfigs(assetPath).FirstOrDefault();
        }

        static IEnumerable<BuildAssetBundleItem> FindMatchConfigs(string assetPath)
        {
            var config = Config;

            for (int i = 0; i < config.Items.Count; i++)
            {
                var itemConfig = config.Items[i];
                if (string.IsNullOrEmpty(itemConfig.Directory))
                    continue;
                if (!assetPath.PathStartsWithDirectory(itemConfig.Directory))
                    continue;

                if (!string.IsNullOrEmpty(itemConfig.Pattern) && !itemConfig.PatternRegex.IsMatch(assetPath))
                    continue;

                yield return itemConfig;
            }
        }


        public static bool UpdateAssetBundleName(string assetPath)
        {
            string assetBundleName, variant;

            assetBundleName = string.Empty;
            variant = string.Empty;


            if (!IsRootConfigIgnoreAssetPath(assetPath))
            {
                Dictionary<string, object> formatValues = null;


                foreach (var itemConfig in FindMatchConfigs(assetPath))
                {
                    if (itemConfig.Ignore)
                    {
                        assetBundleName = string.Empty;
                        variant = string.Empty;
                        break;
                    }

                    if (formatValues == null)
                    {
                        formatValues = GetFormatValues(assetPath);
                        if (!string.IsNullOrEmpty(config.AssetBundleName))
                            assetBundleName = GetFormatString(config.AssetBundleName, formatValues);
                    }
                    GetAssetBundleName(itemConfig, assetPath, ref assetBundleName, ref variant, formatValues);
                }
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (!importer)
                return false;
            bool changed = false;
            if (importer.assetBundleName != assetBundleName || importer.assetBundleVariant != variant)
            {
                importer.SetAssetBundleNameAndVariant(assetBundleName, variant);
                changed = true;
            }

            return changed;
        }


        [MenuItem(MenuPrefix + "Update All AssetBundle Names", priority = 2)]
        public static IEnumerable<string> UpdateAllAssetBundleNames()
        {
            var config = Config;

            bool changed = false;
            Action onChange = () =>
            {
                if (!changed)
                {
                    changed = true;
                    AssetDatabase.StartAssetEditing();
                }
            };

            foreach (var abName in AssetDatabase.GetAllAssetBundleNames())
            {
                foreach (var assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(abName))
                {
                    var item = config.FindItem(assetPath);
                    if (item == null || item.Ignore)
                    {
                        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                        if (importer.assetBundleName != string.Empty || importer.assetBundleVariant != string.Empty)
                        {
                            importer.SetAssetBundleNameAndVariant(string.Empty, string.Empty);
                            onChange();
                        }
                    }
                }
            }

            HashSet<string> assetPaths = new HashSet<string>();

            //include all assets
            for (int i = 0; i < config.Items.Count; i++)
            {
                var itemConfig = config.Items[i];
                if (string.IsNullOrEmpty(itemConfig.Directory))
                    continue;

                if (!Directory.Exists(itemConfig.Directory))
                {
                    Debug.LogWarning("directory not exists, " + itemConfig.Directory);
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets("", new string[] { itemConfig.Directory }))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                    if (IsIgnoreAssetPath(assetPath))
                    {
                        assetPaths.Remove(assetPath);
                    }
                    else
                    {
                        assetPaths.Add(assetPath);
                    }
                }
            }

            //exclude  asset
            foreach (var abName in AssetDatabase.GetAllAssetBundleNames())
            {
                foreach (var assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(abName))
                {
                    if (!assetPaths.Contains(assetPath))
                    {
                        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                        if (importer.assetBundleName != string.Empty || importer.assetBundleVariant != string.Empty)
                        {
                            importer.SetAssetBundleNameAndVariant(string.Empty, string.Empty);
                            onChange();
                        }
                    }
                }
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

            return assetPaths;
        }

        static string GetVariant(BuildAssetBundleItem item, string extension)
        {
            string variant = string.Empty;
            if (!item.variantsMap.TryGetValue(extension, out variant))
            {
                if (!item.variantsMap.TryGetValue(string.Empty, out variant))
                {
                    if (!Config.VariantsMap.TryGetValue(extension, out variant))
                    {
                        if (!Config.VariantsMap.TryGetValue(string.Empty, out variant))
                        {
                            variant = string.Empty;
                        }
                    }
                }
            }
            return variant;
        }

        public static void DeleteUnusedAssetBundles(string path)
        {
            if (Directory.Exists(path))
            {
                HashSet<string> names = new HashSet<string>(AssetDatabase.GetAllAssetBundleNames());
                foreach (var name in AssetDatabase.GetUnusedAssetBundleNames())
                {
                    names.Remove(name);
                }
                string[] extensions = new string[] { ".meta", ".manifest", ".manifest.meta" };
                foreach (var filePath in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    string filename = Path.GetFileName(filePath);
                    string exName = Path.GetExtension(filename);

                    if (string.Equals(exName, ".manifest", StringComparison.InvariantCultureIgnoreCase))
                        continue;
                    if (string.Equals(exName, ".meta", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    if (names.Contains(filename))
                        continue;

                    DeleteFile(filePath);
                    foreach (var ex in extensions)
                    {
                        if (File.Exists(filePath + ex))
                            DeleteFile(filePath + ex);
                    }
                }
            }
        }

        private static void DeleteFile(string filePath)
        {
            filePath.ClearFileAttributes();
            File.Delete(filePath);
        }

        private static void CopyDirectory(string src, string dst)
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
                srcFiles.Add(relativePath, srcFile);
            }

            foreach (var dstFile in Directory.GetFiles(dst, "*", SearchOption.AllDirectories))
            {
                string relativePath = dstFile.Substring(dst.Length);
                if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                    relativePath = relativePath.Substring(1);
                dstFiles.Add(relativePath, dstFile);
            }

            foreach (var dstFile in dstFiles)
            {
                if (!srcFiles.ContainsKey(dstFile.Key))
                {
                    DeleteFile(dstFile.Value);
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

        }



        private static bool IsRootConfigIgnoreAssetPath(string assetPath)
        {
            bool ignore = false;
            var ignores = Config.IgnorePaths;
            if (ignores != null)
            {
                foreach (var path in ignores)
                {
                    if (Path.IsPathRooted(path))
                    {
                        if (PathStartsWith(assetPath, path.Substring(1)))
                        {
                            ignore = true;
                            break;
                        }
                    }
                    else
                    {
                        if (PathContains(assetPath, path))
                        {
                            ignore = true;
                            break;
                        }
                    }
                }
            }
            return ignore;
        }

        public static bool IsIgnoreAssetPath(string assetPath)
        {
            bool ignore = false;

            if (Directory.Exists(assetPath))
                return true;

            ignore = IsRootConfigIgnoreAssetPath(assetPath);
            if (ignore)
                return true;
            foreach (var itemConfig in FindMatchConfigs(assetPath))
            {
                if (itemConfig.Ignore)
                {
                    ignore = true;
                    break;
                }
            }
            return ignore;
        }

        public static void LoadConfig()
        {
            string filePath = ConfigFilePath;

            if (config != null)
            {
                if (File.Exists(filePath) && lastConfigWriteTime != File.GetLastWriteTimeUtc(filePath))
                {
                    config = null;
                }
            }
            if (config == null)
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        string json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                        config = JsonUtility.FromJson<BuildAssetBundleConfig>(json);
                        lastConfigWriteTime = File.GetLastWriteTimeUtc(filePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        Debug.LogError("config file format error " + filePath);
                    }
                }
                else
                {
                    config = new BuildAssetBundleConfig();
                    config.OnAfterDeserialize();
                    SaveConfig();
                }

            }
        }

        public static void SaveConfig()
        {
            string str = JsonUtility.ToJson(Config, true);
            ConfigFilePath.ClearFileAttributes();
            File.WriteAllText(ConfigFilePath, str, Encoding.UTF8);
        }


        #region PreProcessBuild

        [PreProcessBuild(1)]
        static void PreProcessBuild_BuildAssetBundles()
        {
            string outputPath = GetOutputPath();

            Build(outputPath);
        }

        //[MenuItem(MenuPrefix + "CopyToBuildPath")]
        static void CopyToBuildPath()
        {
            string outputPath = GetOutputPath();
            string buildCopyPath = GetBuildCopyToPath();
            if (!string.IsNullOrEmpty(buildCopyPath))
            {
                CopyDirectory(outputPath, buildCopyPath);
                Debug.LogFormat("copy AssetBundles\nsrc:{0}\ndst:{1}", outputPath, buildCopyPath);
            }

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
            //打包结束后删除资源
            string copyPath = GetBuildCopyToPath();
            LastBuildCopyPath = copyPath;
            if (!string.IsNullOrEmpty(copyPath))
            {
                if (Directory.Exists(copyPath))
                {
                    Directory.Delete(copyPath, true);
                    LastBuildCopyPath = null;
                    AssetDatabase.Refresh();
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
            var config = Config;

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
                    if (!string.IsNullOrEmpty(config.AssetClass))
                        className = FileNameToSafeChar(GetFormatString(config.AssetClass, assetPath));
                    string assetName = assetPath.ToLower();
                    string assetField;
                    string addressableName;

                    addressableName = assetPath.ToLower();
                    if (!string.IsNullOrEmpty(config.AssetName))
                        addressableName = GetFormatString(config.AssetName, formatValues);

                    assetField = FileNameToSafeChar((string)formatValues[FormatArg_AssetName]);

                    for (int j = 0; j < config.Items.Count; j++)
                    {
                        var itemConfig = config.Items[j];

                        if (!PathDirectoryStartsWith(Path.GetDirectoryName(assetPath), itemConfig.Directory))
                            continue;
                        if (!itemConfig.PatternRegex.IsMatch(assetPath))
                            continue;
                        if (itemConfig.Ignore)
                            break;

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

                        if (!string.IsNullOrEmpty(itemConfig.AssetName))
                        {
                            addressableName = GetAddressableName(itemConfig, assetPath, formatValues);
                            assetField = FileNameToSafeChar(Path.GetFileNameWithoutExtension(addressableName));
                            addressableName = addressableName.ToLower();
                        }

                        if (itemConfig.TagArray != null && itemConfig.TagArray.Length > 0)
                        {
                            foreach (var tag in itemConfig.TagArray)
                            {
                                if (!abInfo.Tags.Contains(tag))
                                    abInfo.Tags.Add(tag);
                            }
                        }

                    }


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
                        if (config.Components != null)
                        {
                            foreach (var cptType in config.Components)
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
        [MenuItem(MenuPrefix + "gen AssetBundleNames")]
        public static void GenerateAssetBundleNamesClass()
        {
            if (GenerateAssetBundleNamesClass(true))
            {
                AssetDatabase.Refresh();
            }
        }


        public static bool GenerateAssetBundleNamesClass(bool force)
        {
            var config = Config;
            string tplPath = config.AssetBundleNamesClassTemplate;
            if (string.IsNullOrEmpty(tplPath))
                tplPath = AssetBundleNamesClassTemplatePath;

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

            XmlDocument doc = GetAssetBundleNamesData();
            string assetBundleNamesClassName = "AssetBundleNames";

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

            if (!string.IsNullOrEmpty(config.AssetBundleNamesClassFilePath))
            {
                if (!Directory.Exists(Path.GetDirectoryName(config.AssetBundleNamesClassFilePath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(config.AssetBundleNamesClassFilePath));
                config.AssetBundleNamesClassFilePath.ClearFileAttributes();
                if (!File.Exists(config.AssetBundleNamesClassFilePath))
                    changed = true;

                if (changed)
                {
                    if (Path.GetExtension(config.AssetBundleNamesClassFilePath).ToLower() == ".dll")
                    {
                        string tmpPath = "Temp/" + Path.GetFileName(config.AssetBundleNamesClassFilePath);
                        if (Directory.Exists(Path.GetDirectoryName(tmpPath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(tmpPath));
                        if (CompilerCode(tmpPath, codeFiles, new string[] { typeof(UnityEngine.Object).Assembly.Location }))
                        {
                            changed = tmpPath.CopyFileIfChanged(config.AssetBundleNamesClassFilePath);
                            if (changed)
                            {
                                Debug.Log("generate file :" + config.AssetBundleNamesClassFilePath);
                            }
                            else
                            {
                                Debug.Log("generate file not changed:" + config.AssetBundleNamesClassFilePath);
                            }
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
                        changed = codeFiles[0].CopyFileIfChanged(config.AssetBundleNamesClassFilePath);
                        if (changed)
                        {
                            Debug.Log("generate file :" + config.AssetBundleNamesClassFilePath);
                        }
                        else
                        {
                            Debug.Log("generate file not changed:" + config.AssetBundleNamesClassFilePath);
                        }
                    }
                }
            }

            for (int i = 0; i < codeFiles.Length; i++)
            {
                codeFiles[i].ClearFileAttributes();
                Debug.Log("generate file:" + codeFiles[i]);
            }


            if (changed)
            {
                if (!hasError)
                    File.WriteAllText(hashPath, newHash);
                else
                    return false;
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


        static string FileNameToSafeChar(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
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
                if (File.Exists(destFileName))
                    File.SetAttributes(destFileName, FileAttributes.Normal);
                File.Copy(sourceFileName, destFileName, true);
            }
            catch (Exception ex)
            {
                Debug.LogError("Copy File error:" + sourceFileName + " > " + destFileName);
                throw ex;
            }
        }

        #endregion




        private static Regex formatStringRegex = new Regex("(?<!\\{)\\{\\$([^}:]*)(:([^}]*))?\\}(?!\\})");


        /// <summary>
        /// format:{$name:format} 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        private static string FormatString(string input, IFormatProvider formatProvider, Dictionary<string, object> values)
        {
            string result;

            result = formatStringRegex.Replace(input, (m) =>
            {
                string paramName = m.Groups[1].Value;
                string format = m.Groups[3].Value;
                object value;
                string ret = null;

                if (string.IsNullOrEmpty(paramName))
                    throw new FormatException("format error:" + m.Value);

                if (!values.TryGetValue(paramName, out value))
                    throw new ArgumentException("not found param name:" + paramName);

                if (value != null)
                {
                    ret = string.Format(formatProvider, "{0:" + format + "}", value);
                }
                else
                {
                    ret = string.Empty;
                }

                return ret;
            });
            return result;
        }
        /// <summary>
        /// string.format format:/(regex expression)/
        /// 正则表达式提取字符串中的第一个匹配组
        /// </summary>
        /// <example>
        ///<see cref="string.Format"/>( <see cref="Instance"/>, "{0:/(he.*ld)/}", "say hello world .")
        /// output: hello world
        /// </example>
        class StringRegexFormatProvider : IFormatProvider, ICustomFormatter
        {
            private static StringRegexFormatProvider instance;
            private static Regex regex = new Regex("^/([^/]*)/([igm]*)$");

            public static StringRegexFormatProvider Instance
            {
                get
                {
                    if (instance == null)
                        instance = new StringRegexFormatProvider();
                    return instance;
                }
            }


            public string Format(string format, object arg, IFormatProvider formatProvider)
            {
                string result = arg.ToString();
                if (format != null && format.Length > 1)
                {
                    var regexMatch = regex.Match(format);
                    if (regexMatch.Success)
                    {
                        string pattern = regexMatch.Groups[1].Value;
                        string vars = regexMatch.Groups[2].Value;
                        RegexOptions optons = RegexOptions.None;
                        if (vars.IndexOf('i') >= 0 || vars.IndexOf('I') >= 0)
                            optons |= RegexOptions.IgnoreCase;
                        if (vars.IndexOf('m') >= 0 || vars.IndexOf('M') >= 0)
                            optons |= RegexOptions.Multiline;
                        bool global = false;
                        if (vars.IndexOf('g') >= 0 || vars.IndexOf('G') >= 0)
                            global = true;
                        Regex regex = new Regex(pattern, optons);
                        string matchResult = string.Empty;
                        if (global)
                        {
                            foreach (Match m in regex.Matches(result))
                            {
                                if (m.Groups.Count > 0)
                                {
                                    matchResult = matchResult + m.Groups[1].Value;
                                }
                            }
                        }
                        else
                        {
                            var m = regex.Match(result);
                            if (m != null && m.Groups.Count > 0)
                            {
                                matchResult = m.Groups[1].Value;
                            }
                        }

                        return matchResult;
                    }
                }

                if (arg is IFormattable)
                    return ((IFormattable)arg).ToString(format, CultureInfo.CurrentCulture);
                else if (arg != null)
                    return arg.ToString();
                else
                    return string.Empty;
            }

            public object GetFormat(Type formatType)
            {
                if (formatType == typeof(ICustomFormatter))
                    return this;
                return null;
            }
        }
    }

}