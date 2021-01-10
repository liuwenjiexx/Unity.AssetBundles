using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Build
{

    [Serializable]
    public class EditorAssetBundleSettings : ISerializationCallbackReceiver
    {

        #region Provider


        private static UnityEditor.Internal.SettingsProvider provider;

        internal static UnityEditor.Internal.SettingsProvider Provider
        {
            get
            {
                if (provider == null)
                    provider = new UnityEditor.Internal.SettingsProvider(typeof(EditorAssetBundleSettings), AssetBundles.PackageName, false, true);
                return provider;
            }
        }

        public static EditorAssetBundleSettings Settings { get => (EditorAssetBundleSettings)Provider.Settings; }

        #endregion


        [SerializeField]
        private bool enabled = true;

        public static bool Enabled
        {
            get => Settings.enabled;
            set => Provider.Set(nameof(Enabled), ref Settings.enabled, value);
        }

        [SerializeField]
        private string assetBundleName = "{$AssetPath:#DirectoryPath,-1}/{$AssetPath:#DirectoryName}_bundle";
        public static string AssetBundleName
        {
            get => Settings.assetBundleName;
            set => Provider.Set(nameof(AssetBundleName), ref Settings.assetBundleName, value);
        }

        [SerializeField]
        private string assetName = "{$AssetPath:$DirectoryPath}/{$AssetPath:$FileNameWithoutExtension}";
        public static string AssetName
        {
            get => Settings.assetName;
            set => Provider.Set(nameof(AssetName), ref Settings.assetName, value);
        }

        [SerializeField]
        private bool assetNameToLower = false;
        public static bool AssetNameToLower
        {
            get => Settings.assetNameToLower;
            set => Provider.Set(nameof(AssetNameToLower), ref Settings.assetNameToLower, value);
        }


        [SerializeField]
        private string autoDependencyBundleName = "local/auto/bundle_auto";
        public static string AutoDependencyBundleName
        {
            get => Settings.autoDependencyBundleName;
            set => Provider.Set(nameof(AutoDependencyBundleName), ref Settings.autoDependencyBundleName, value);
        }

        [SerializeField]
        private string[] components;
        public static string[] Components
        {
            get => Settings.components;
            set => Provider.Set(nameof(Components), ref Settings.components, value);
        }

        [SerializeField]
        private string assetValue;
        public static string AssetValue
        {
            get => Settings.assetValue;
            set => Provider.Set(nameof(AssetValue), ref Settings.assetValue, value);
        }

        [SerializeField]
        private BuildAssetBundleOptions options;
        public static BuildAssetBundleOptions Options
        {
            get => Settings.options;
            set => Provider.Set(nameof(Options), ref Settings.options, value);
        }

        [SerializeField]
        private string streamingAssetsExcludeGroup;
        /// <summary>
        /// StreamingAssets 排除资源组
        /// </summary>
        public static string StreamingAssetsExcludeGroup
        {
            get => Settings.streamingAssetsExcludeGroup;
            set => Provider.Set(nameof(StreamingAssetsExcludeGroup), ref Settings.streamingAssetsExcludeGroup, value);
        }


        [SerializeField]
        private bool bundleCodeResetOfAppVersion = false;
        /// <summary>
        /// 0.0.1 =>  {0}.{1}.{2}
        /// </summary>
        //public string AppVersionFormat;
        public static bool BundleCodeResetOfAppVersion
        {
            get => Settings.bundleCodeResetOfAppVersion;
            set => Provider.Set(nameof(BundleCodeResetOfAppVersion), ref Settings.bundleCodeResetOfAppVersion, value);
        }
        [SerializeField]
        private string[] ignorePaths = new string[] { "^Assets/StreamingAssets/", "/Resources/" };
        public static string[] IgnorePaths
        {
            get => Settings.ignorePaths;
            set => Provider.Set(nameof(IgnorePaths), ref Settings.ignorePaths, value);
        }

        [SerializeField]
        private string releasePath;
        public static string ReleasePath
        {
            get => Settings.releasePath;
            set => Provider.Set(nameof(ReleasePath), ref Settings.releasePath, value);
        }

        [SerializeField]
        private AssetBundleNamesClassSettings assetBundleNamesClass = new AssetBundleNamesClassSettings();
        public static AssetBundleNamesClassSettings AssetBundleNamesClass
        {
            get => Settings.assetBundleNamesClass;
            set => Provider.Set(nameof(AssetBundleNamesClass), ref Settings.assetBundleNamesClass, value);
        }

        [SerializeField]
        private PreBuildPlayerSettings preBuildPlayer = new PreBuildPlayerSettings();
        public static PreBuildPlayerSettings PreBuildPlayer
        {
            get => Settings.preBuildPlayer;
            set => Provider.Set(nameof(PreBuildPlayer), ref Settings.preBuildPlayer, value);
        }
        [SerializeField]
        private PostBuildPlayerSettings postBuildPlayer = new PostBuildPlayerSettings();
        public static PostBuildPlayerSettings PostBuildPlayer
        {
            get => Settings.postBuildPlayer;
            set => Provider.Set(nameof(PostBuildPlayer), ref Settings.postBuildPlayer, value);
        }
        [SerializeField]
        private PostBuildSettings postBuild = new PostBuildSettings();
        public static PostBuildSettings PostBuild
        {
            get => Settings.postBuild;
            set => Provider.Set(nameof(PostBuild), ref Settings.postBuild, value);
        }

        [SerializeField]
        private int autoDependencySplit = 1;

        public static int AutoDependencySplit
        {
            get => Settings.autoDependencySplit;
            set => Provider.Set(nameof(AutoDependencySplit), ref Settings.autoDependencySplit, value);
        }

        /// <summary>
        /// 签名私匙文件位置
        /// </summary>
        [SerializeField]
        private string signatureKeyPath;
        public static string SignatureKeyPath
        {
            get => Settings.signatureKeyPath;
            set => Provider.Set(nameof(SignatureKeyPath), ref Settings.signatureKeyPath, value);
        }
        [SerializeField]
        private string cryptoKey;
        public static string CryptoKey
        {
            get => Settings.cryptoKey;
            set => Provider.Set(nameof(CryptoKey), ref Settings.cryptoKey, value);
        }
        [SerializeField]
        private string cryptoIV;
        public static string CryptoIV
        {
            get => Settings.cryptoIV;
            set => Provider.Set(nameof(CryptoIV), ref Settings.cryptoIV, value);
        }

        [SerializeField]
        private string[] excludeExtensions = new string[] { ".cs" };
        public static string[] ExcludeExtensions
        {
            get => Settings.excludeExtensions;
            set => Provider.Set(nameof(ExcludeExtensions), ref Settings.excludeExtensions, value);
        }
        [SerializeField]
        private string[] excludeTypeNames = new string[] { "UnityEditor.MonoScript" };
        public static string[] ExcludeTypeNames
        {
            get => Settings.excludeTypeNames;
            set => Provider.Set(nameof(ExcludeTypeNames), ref Settings.excludeTypeNames, value);
        }
        [SerializeField]
        private string[] excludeDependencyExtensions;//= new string[] { ".spriteatlas" };
        public static string[] ExcludeDependencyExtensions
        {
            get => Settings.excludeDependencyExtensions;
            set => Provider.Set(nameof(ExcludeDependencyExtensions), ref Settings.excludeDependencyExtensions, value);
        }

        [SerializeField]
        private AssetObjectReferenced localGroup = new AssetObjectReferenced();
        public static AssetObjectReferenced LocalGroup
        {
            get => Settings.localGroup;
            set => Settings.localGroup = value;
        }

        [SerializeField]
        private AssetObjectReferenced[] groups = new AssetObjectReferenced[0];
        public static AssetObjectReferenced[] Groups
        {
            get => Settings.groups;
            set => Provider.Set(nameof(Groups), ref Settings.groups, value);
        }


        static Dictionary<string, string> ToVariantsMap(string variants)
        {
            var variantsMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            if (!string.IsNullOrEmpty(variants))
            {
                foreach (var part in variants.Split(';'))
                {
                    if (string.IsNullOrEmpty(part))
                        continue;
                    string[] s = part.Split('|');
                    if (s.Length == 2)
                    {
                        variantsMap[s[0]] = s[1];
                    }
                    else if (s.Length == 1)
                    {
                        variantsMap[""] = s[0];
                    }
                }
            }
            return variantsMap;
        }



        public void OnBeforeSerialize()
        {

        }
        public void OnAfterDeserialize()
        {

            if (assetBundleNamesClass == null)
                assetBundleNamesClass = new AssetBundleNamesClassSettings();

            if (preBuildPlayer == null)
                preBuildPlayer = new PreBuildPlayerSettings();
            if (postBuildPlayer == null)
                postBuildPlayer = new PostBuildPlayerSettings();

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



        [Serializable]
        public class PostBuildSettings
        {

            [SerializeField]
            private bool showFolder;
            public static bool ShowFolder
            {
                get => PostBuild.showFolder;
                set => Provider.Set(nameof(ShowFolder), ref PostBuild.showFolder, value);
            }
        }



        //[Serializable]
        //public class PreBuildConfig
        //{
        //    public string delete

        //}


        [Serializable]
        public class PreBuildPlayerSettings
        {
            [SerializeField]
            private bool autoBuildAssetBundle = true;
            public static bool AutoBuildAssetBundle
            {
                get => PreBuildPlayer.autoBuildAssetBundle;
                set => Provider.Set(nameof(AutoBuildAssetBundle), ref PreBuildPlayer.autoBuildAssetBundle, value);
            }
        }

        [Serializable]
        public class PostBuildPlayerSettings
        {
            [SerializeField]
            private bool clearStreamingAssets = false;
            public static bool ClearStreamingAssets
            {
                get => PostBuildPlayer.clearStreamingAssets;
                set => Provider.Set(nameof(ClearStreamingAssets), ref PostBuildPlayer.clearStreamingAssets, value);
            }
        }

        [Serializable]
        public class AssetBundleNamesClassSettings
        {
            [SerializeField]
            private bool enabled = false;
            public static bool Enabled
            {
                get => AssetBundleNamesClass.enabled;
                set => Provider.Set(nameof(Enabled), ref AssetBundleNamesClass.enabled, value);
            }
            [SerializeField]
            private string filePath = "Assets/Plugins/gen/AssetBundleNames.dll";
            public static string FilePath
            {
                get => AssetBundleNamesClass.filePath;
                set => Provider.Set(nameof(FilePath), ref AssetBundleNamesClass.filePath, value);
            }
            [SerializeField]
            private string assetNameClass = "{$AssetPath:#DirectoryName}";
            public static string AssetNameClass
            {
                get => AssetBundleNamesClass.assetNameClass;
                set => Provider.Set(nameof(AssetNameClass), ref AssetBundleNamesClass.assetNameClass, value);
            }
            [SerializeField]
            private string assetBundleNamesClassTemplate;
            public static string AssetBundleNamesClassTemplate
            {
                get => AssetBundleNamesClass.assetBundleNamesClassTemplate;
                set => Provider.Set(nameof(AssetBundleNamesClassTemplate), ref AssetBundleNamesClass.assetBundleNamesClassTemplate, value);
            }
        }

    }

}