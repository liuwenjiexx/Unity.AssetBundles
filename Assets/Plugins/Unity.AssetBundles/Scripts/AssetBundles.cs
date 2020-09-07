using Coroutines;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.StringFormats;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine
{


    /// <summary>
    /// AssetBundle管理器
    /// 必要的参数设置：<see cref="Platform"/>
    /// </summary>
    public sealed class AssetBundles
    {
        public const string PackageName = "unity.assetbundles";
        public const string FormatArg_Platform = "Platform";
        public const string FormatArg_AppVersion = "AppVersion";
        public const string FormatArg_BundleCode = "BundleCode";
        public const string FormatArg_Channel = "Channel";
        public const string FormatArg_BundleVersion = "BundleVersion";

        /// <summary>
        /// 运行时平台
        /// </summary>
        public static RuntimePlatform Platform
        {
            get => platform.HasValue ? platform.Value : Application.platform;
            set => platform = value;
        }

        /// <summary>
        /// 运行时平台名称，对应 <see cref="AssetBundleManifest"/> 清单文件名称和文件夹名称
        /// </summary>
        public static string PlatformName
        {
            get => string.IsNullOrEmpty(platformName) ? GetPlatformName(Platform) : platformName;
            set => platformName = value;
        }

        /// <summary>
        /// 变体列表
        /// </summary>
        public static List<string> Variants { get; set; } = new List<string>();

        /// <summary>
        /// 资源清单
        /// </summary>
        public static AssetBundleManifest Manifest { get { return mainManifest != null ? mainManifest.manifest : null; } }

        /// <summary>
        /// 运行模式
        /// </summary>
        public static AssetBundleMode Mode
        {
            get { return !Application.isPlaying ? AssetBundleMode.Editor : Application.isEditor ? (AssetBundleMode)PlayerPrefs.GetInt(PlayerPrefsKeyPrefix + "AssetBundleMode", (int)AssetBundleMode.Editor) : AssetBundleMode.Download; }
        }

        /// <summary>
        /// 设置选项
        /// </summary>
        public static AssetBundleOptions Options { get; set; }

        private static AssetBundleStatus status;

        /// <summary>
        /// 状态
        /// </summary>
        public static AssetBundleStatus Status
        {
            get => status;
            set
            {
                if (status != value)
                {
                    status = value;
                    StatusChanged?.Invoke();
                }
            }
        }

        public static bool IsInitialized
        {
            get { return Status == AssetBundleStatus.Initialized; }
        }

        /// <summary>
        /// 状态改变事件
        /// </summary>
        public static event Action StatusChanged;

        /// <summary>
        /// AssetBundleNames 类型, 支持预加载，默认为:AssetBundleNames
        /// </summary>
        public static Type AssetBundleNamesType { get; set; }


        public static string AppVersion
        {
            get
            {
                string appVersion;

                appVersion = Application.version;
                if (!string.IsNullOrEmpty(AssetBundleSettings.AppVersionFormat))
                    appVersion = string.Format(AssetBundleSettings.AppVersionFormat, appVersion.Split('.'));

                return appVersion;
            }
        }

        public static string RootVersionFile
        {
            get
            {
                return FormatString(AssetBundleSettings.DownloadVersionFile);
            }
        }

        public static AssetBundleVersion Version { get; private set; }


        //public static string LocalAssetBundlesDirectory
        //{
        //    get => $"{Application.persistentDataPath}/{Path.GetDirectoryName( FormatString(AssetBundleSettings.LocalManifestPath))}";
        //}

        public static string LocalManifestPath
        {
            get => $"{Application.persistentDataPath}/{FormatString(AssetBundleSettings.LocalManifestPath)}";
        }

        public static string LocalManifestDirectory
        {
            get => Path.GetDirectoryName(LocalManifestPath);
        }

        public static string LocalManifestUrl
        {
            get => $"file://{Application.persistentDataPath}/{FormatString(AssetBundleSettings.LocalManifestPath)}";
        }

        static string buildManifestUrl;
        public static string BuildManifestUrl
        {
            get
            {
                if (buildManifestUrl == null)
                {
                    string buildPath = AssetBundleSettings.BuildManifestPath;
                    buildPath = FormatString(buildPath);
                    //var versionList = AssetBundleVersion.LoadVersionList(buildPath + "/" + RootVersionFile);
                    //var version = AssetBundleVersion.GetLatestVersionList(versionList, PlatformName, AppVersion);
                    //if (version == null)
                    //    throw new Exception("not found assetbundle version. require build assetbundle, [" + RootVersionFile + "]");
                    string path;
                    //path = GetManifestPathWithVersion(buildPath, version);
                    path = "file://" + Path.GetFullPath(buildPath);
                    buildManifestUrl = path;
                }
                return buildManifestUrl;
            }
        }


        public static string StreamingAssetsManifestUrl
        {
            get => $"{StreamingAssetsUrl}/{FormatString(AssetBundleSettings.StreamingAssetsManifestPath)}";
        }

        /// <summary>
        /// 下载版本
        /// </summary>
        public static AssetBundleVersion RemoteVersion { get; private set; }
        /// <summary>
        /// 下载文件总数
        /// </summary>
        public static int DownloadTotal { get; private set; }
        /// <summary>
        /// 下载已完成数
        /// </summary>
        public static int DownloadProgress { get; private set; }
        /// <summary>
        /// 下载错误信息
        /// </summary>
        public static Exception DownloadError { get; private set; }

        /// <summary>
        /// 下载错误次数
        /// </summary>
        public static int DownloadErrorCount { get; set; }

        /// <summary>
        /// 单个文件下载进度
        /// </summary>
        public static int DownloadItemTotalBytes { get; private set; }

        public static int DownloadItemReceiveBytes { get; private set; }

        public static int DownloadSpeed { get; private set; }

        public static int DownloadTotalBytes { get; private set; }

        public static int DownloadReceiveBytes { get; private set; }

        public static DownloadStartedDelegate OnDownloadStarted;
        public static Action OnDownloadCompleted;
        public static Action<AssetBundleVersion> OnDownloadVersionFile;
        public delegate void DownloadStartedDelegate(string downloadManifestUrl, string[] assetBundleNames);
        /// <summary>
        /// 下载进度
        /// </summary>
        public static DownloadProgressDelegate OnDownloadProgress;

        public delegate void DownloadProgressDelegate(string assetBundleName);

        /// <summary>
        /// 下载包含组，[|]分隔多个
        /// </summary>
        public static string DownloadIncludeGroup;
        /// <summary>
        /// 下载排除组，[|]分隔多个
        /// </summary>
        public static string DownloadExcludeGroup;

        /// <summary>
        /// 预加载总数
        /// </summary>
        public static int PreloadedTotal { get; private set; }
        /// <summary>
        /// 预加载进度
        /// </summary>
        public static int PreloadedProgress { get; private set; }

        public static Action<int> OnPreloadStarted;
        public static Action<int, string> OnPreloadProgress;
        public static Action OnPreloadCompleted;

        private readonly static Object[] EmptyObjects = new Object[0];
        private static string platformName;

        //public delegate void DownloadAssetBundleCompletedDelegate(string assetbundleName, AssetBundle assetBundle);
        //public delegate void DownloadAssetBundleFailDelegate(string assetbundleName, Exception error);

        public static Dictionary<AssetBundleKey, AssetBundleRef> abRefs;

        private static SyncCoroutine syncObj = new SyncCoroutine();
        private static Dictionary<string, PackageManifest> manifests = new Dictionary<string, PackageManifest>();
        public static PackageManifest mainManifest;

        private static Dictionary<string, string[]> assetBundleNameAndVariants = new Dictionary<string, string[]>();

        private static Dictionary<string, string> assetBundleNameMapNameHash = new Dictionary<string, string>();

        private static List<AssetBundleRef> tmps = new List<AssetBundleRef>();

        public const string LogPrefix = "[AssetBundle] ";
        [LogExtension.External.LogExtension]
        internal static ILogger Logger = Debug.unityLogger;

        private const string PlayerPrefsKeyPrefix = "unity.assetbundles.";


        static Regex regexCryptoInclude = null, regexCryptoExclude = null;




        static Regex regexSignatureInclude = null, regexSignatureExclude = null;


        public static bool IsLogEnabled
        {
            get => Mode == AssetBundleMode.Build;
        }

        /// <summary>
        /// 运行时解压缩类型
        /// 支持 <see cref="BuildCompression.LZ4Runtime"/>,<see cref="BuildCompression.Uncompressed"/>, <see cref="BuildCompression.UncompressedRuntime"/>
        /// </summary>
        public static BuildCompression Decompression = BuildCompression.LZ4Runtime;


        public static LoadAssetHandlerDelegate LoadAssetHandler;
        public delegate void LoadAssetHandlerDelegate(LoadAssetRequest request);

        public static LoadAssetBundleHandlerDelegate LoadAssetBundleHandler;
        public delegate void LoadAssetBundleHandlerDelegate(LoadAssetRequest request);
        //public static Action<NewAppVersionEventArgs> OnNewAppVersion;

        public class NewAppVersionEventArgs
        {
            public string CurrentVersion;
            public string NewVersion;

            public bool AssetBundleDownlaodCanceled { get; private set; }

            public void Cancel()
            {
                AssetBundleDownlaodCanceled = true;
            }
        }

        #region Private

        //加密
        private static ICryptoTransform crypto;
        private static SymmetricAlgorithm cryptoSA;
        private static byte[] cryptoKey;
        private static byte[] cryptoIV;

        //签名
        private static RSACryptoServiceProvider rsa;
        private static SHA1CryptoServiceProvider sha;
        #endregion


        public class LoadAssetRequest : global::Coroutines.CustomYield
        {
            public string url;
            public string assetBundleName;
            public string assetName;
            public bool isAsync;
            public Type assetType;
            public bool isLoadScene;
            public LoadSceneMode sceneMode;
            public AssetBundle loadedAssetBundle;
            public Object[] loadedAssets;
            public bool isDone { get; private set; }
            public object owner;
            public float progress;

            public override bool KeepWaiting => !isDone;

            internal static LoadAssetRequest FromAssetBundle(string url, string assetBundleName, bool isAsync, object owner)
            {
                LoadAssetRequest loadAssetInfo = new LoadAssetRequest()
                {
                    url = url,
                    assetBundleName = assetBundleName,
                    isAsync = isAsync,
                    owner = owner,
                };
                return loadAssetInfo;
            }
            internal static LoadAssetRequest FromAsset(string url, string assetBundleName, string assetName, Type assetType, bool isAsync, object owner)
            {
                LoadAssetRequest loadAssetInfo = new LoadAssetRequest()
                {
                    url = url,
                    assetBundleName = assetBundleName,
                    assetName = assetName,
                    assetType = assetType,
                    isAsync = isAsync,
                    owner = owner,
                };
                return loadAssetInfo;
            }
            internal static LoadAssetRequest FromScene(string url, string assetBundleName, string sceneName, LoadSceneMode sceneMode, bool isAsync, object owner)
            {
                LoadAssetRequest loadAssetInfo = new LoadAssetRequest()
                {
                    url = url,
                    assetBundleName = assetBundleName,
                    assetName = sceneName,
                    isLoadScene = true,
                    sceneMode = sceneMode,
                    isAsync = isAsync,
                    owner = owner,
                };

                return loadAssetInfo;
            }


            public void Done()
            {
                progress = 1f;
                isDone = true;
            }


        }


        private static string streamingAssetsUrl;
        private static RuntimePlatform? platform;

        private static string StreamingAssetsUrl
        {
            get
            {
                if (streamingAssetsUrl == null)
                {
                    streamingAssetsUrl = Application.streamingAssetsPath;
                    if (streamingAssetsUrl.IndexOf("://") < 0)
                        streamingAssetsUrl = "file://" + Application.streamingAssetsPath;
                }
                return streamingAssetsUrl;
            }
        }

        private static string manifestUrl;
        public static string ManifestUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(manifestUrl))
                    return manifestUrl;
                switch (Mode)
                {
                    case AssetBundleMode.Editor:
                        return string.Empty;
                    case AssetBundleMode.Build:
                        return BuildManifestUrl;
                }
                return LocalManifestUrl;
            }
            set
            {
                manifestUrl = value;
                //Debug.Log("set manifest url: " + manifestUrl);
            }
        }

        static AssetBundles()
        {
            abRefs = new Dictionary<AssetBundleKey, AssetBundleRef>();

        }


        public static string GetPlatformName(RuntimePlatform platform)
        {
            string name;
            switch (platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    name = "Windows";
                    break;
                case RuntimePlatform.Android:
                    name = "Android";
                    break;
                case RuntimePlatform.IPhonePlayer:
                    name = "iOS";
                    break;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    name = "OSX";
                    break;
                default:
                    name = platform.ToString();
                    break;
            }
            return name;
        }


        /// <summary>
        /// 通过清单地址返回版本文件地址 <see cref="VersionFile"/>
        /// </summary>
        /// <param name="manifestUrl"></param>
        /// <returns></returns>
        public static string GetVersionUrl(string manifestUrl)
        {
            string versionUrl = UrlCombine(GetUrlDirectoryName(manifestUrl), FormatString(AssetBundleSettings.VersionFile));
            return versionUrl;
        }


        private static Object[] AssetBundleLoadAssetHandler(string assetBundleName, string assetName, Type type, object owner)
        {
            if (assetBundleName == null)
                throw new ArgumentNullException("assetBundleName");

            var assetBundle = LoadAssetBundle(assetBundleName, owner);

            if (!assetBundle)
                return EmptyObjects;

            //'LoadAsset' This method cannot be used on a streamed scene AssetBundle.
            if (assetBundle.isStreamedSceneAssetBundle)
                return EmptyObjects;

            if (string.IsNullOrEmpty(assetName))
            {
                if (type == null)
                    return assetBundle.LoadAllAssets();
                return assetBundle.LoadAllAssets(type);
            }
            Object asset;
            if (type == null)
                asset = assetBundle.LoadAsset(assetName);
            else
                asset = assetBundle.LoadAsset(assetName, type);
            if (asset == null)
                return EmptyObjects;
            return new Object[] { asset };
        }


        private static Task<Object[]> AssetBundleLoadAssetsAsyncHandler(string assetBundleName, string assetName, Type type, object owner)
        {
            if (assetBundleName == null)
                throw new ArgumentNullException("assetBundleName");

            return LoadAssetBundleAsync(assetBundleName, owner)
                .ContinueWith(t =>
                {
                    var assetBundle = t.Result;
                    if (!assetBundle)
                        return EmptyObjects;

                    if (string.IsNullOrEmpty(assetName))
                    {
                        if (type == null)
                            return assetBundle.LoadAllAssets();
                        return assetBundle.LoadAllAssets(type);
                    }
                    Object asset;
                    if (type == null)
                        asset = assetBundle.LoadAsset(assetName);
                    else
                        asset = assetBundle.LoadAsset(assetName, type);
                    if (asset == null)
                        return EmptyObjects;
                    return new Object[] { asset };
                });
        }

        /// <summary>
        /// 返回初始化后清单url
        /// </summary>
        /// <returns></returns>
        public static Task InitializeAsync()
        {
            return Task.Run(_InitializeAsync()); ;
        }

        static IEnumerator _InitializeAsync()
        {
            if (Status == AssetBundleStatus.Initialized)
                yield break;

            Debug.Log(LogPrefix + $"Initialize\nMode: {Mode}\nOptions: {Options}\nPlatform: {Platform}\nPlatformName: {PlatformName}\nVariants: {string.Join(",", Variants.ToArray())}\nDownloadUrl: {AssetBundleSettings.DownloadUrl}\nRequireDownload: {AssetBundleSettings.RequireDownload}\nAppVersion: {AppVersion}\nIncludeGroup: {""}\nExcludeGroup: {""}");
            Version = null;
            RemoteVersion = null;

            if (Mode == AssetBundleMode.Editor)
            {
                Version = new AssetBundleVersion() { appVersion = AppVersion, bundleVersion = AssetBundleSettings.BundleVersion };
                yield return LoadAddressable();
                Status = AssetBundleStatus.Initialized;
                Debug.Log(LogPrefix + "Initialized Editor Mode");
                yield break;
            }
            yield return EnsureLocalVersion();
            var localVersion = GetLocalVersion();

            yield return localVersion;
            Version = localVersion.Result;

            if (Mode == AssetBundleMode.Build)
            {
                yield return InitializeAsync(BuildManifestUrl);
            }
            else
            {
                yield return DownloadAsync();

                yield return InitializeAsync(ManifestUrl);
                localVersion = GetLocalVersion();
                yield return localVersion;
                //if (localVersion.Result != null && localVersion.Result.IsLatest(Version)) ;
                Version = localVersion.Result;
            }
        }

        /// <summary>
        /// 初始化 AssetBundle
        /// </summary>
        /// <param name="manifestUrl">资源清单文件 url</param>
        /// <returns></returns>
        public static Task InitializeAsync(string manifestUrl)
        {
            return Task.Run(StartInitializedAssetBundleManifest(manifestUrl));
        }

        /// <summary>
        /// 返回最新版本清单url，可能是 <see cref="LocalManifestUrl"/> 或 <see cref="StreamingAssetsManifestUrl"/>
        /// </summary>
        /// <returns></returns>
        public static Task DownloadAsync()
        {
            return Task.Run(_DownloadAsync());
        }
        static IEnumerator _DownloadAsync()
        {
            //非运行时跳过下载
            if (Mode != AssetBundleMode.Download)
            {
                Status = AssetBundleStatus.Downloaded;
                yield break;
            }

            //Runtime
            string downloadUrl = AssetBundleSettings.DownloadUrl;

            yield return DownloadAsync(downloadUrl);
        }



        public static void UnloadManifest(PackageManifest manifest)
        {
            if (manifest == mainManifest)
                mainManifest = null;


            if (manifests.ContainsKey(manifest.Name))
            {
                if (manifest.Manifest)
                {
                    //Object.DestroyImmediate(manifest.Manifest);
                }
                manifests.Remove(manifest.Name);
            }
            assetBundleNameMapNameHash.Clear();
        }

        public static AssetBundle LoadAssetBundle(string assetBundleName, object owner = null)
        {
            if (assetBundleName == null)
                throw new ArgumentNullException("assetbundleName");

            var abInfo = GetAssetBundleInfo(assetBundleName);
            if (abInfo == null)
                return null;
            AssetBundle assetBundle;
            if (!TryGetAssetbundle(abInfo, owner, out assetBundle))
            {
                bool handle = false;

                if (LoadAssetBundleHandler != null)
                {
                    LoadAssetRequest request = LoadAssetRequest.FromAssetBundle(abInfo.url, assetBundleName, false, owner);
                    LoadAssetBundleHandler(request);
                    assetBundle = request.loadedAssetBundle;
                    handle = true;
                }
                else
                {

                    if (abInfo.isSignature || abInfo.isCrypto)
                    {
                        assetBundle = DecryptorAssetBundle(abInfo);
                        handle = true;
                    }

                    if (!handle)
                    {
                        if ((Options & AssetBundleOptions.DisableLoadFromFile) == 0)
                        {
                            if (abInfo.url.StartsWith("file://"))
                            {
                                assetBundle = AssetBundle.LoadFromFile(abInfo.url.Substring(7));
                                handle = true;
                            }
                            else if (abInfo.url.StartsWith("jar:file://"))
                            {
                                //andriod
                                assetBundle = AssetBundle.LoadFromFile(abInfo.url);
                                handle = true;
                            }
                        }
                    }
                }

                if (handle)
                {
                    OnLoadAssetBundle(abInfo, assetBundle, owner);
                    foreach (var dep in abInfo.allDependencies)
                    {
                        AssetBundle tmp;
                        if (!TryGetAssetbundle(dep, null, out tmp))
                        {
                            LoadAssetBundle(dep.name, null);
                        }
                    }
                }
                else
                {
                    throw new Exception("assetBoundle not preloaded, name:" + assetBundleName);
                }
            }

            return assetBundle;
        }






        public static Object LoadAsset(string assetBundleName, string assetName, Type type = null, object owner = null)
        {

            Object[] result;
            if (LoadAssetHandler != null)
            {
                string url = null;
                if (mainManifest != null)
                {
                    var abInfo = GetAssetBundleInfo(assetBundleName);
                    url = abInfo.url;
                }
                LoadAssetRequest request = LoadAssetRequest.FromAsset(url, assetBundleName, assetName, type, false, owner);
                LoadAssetHandler(request);
                result = request.loadedAssets;
            }
            else
                result = AssetBundleLoadAssetHandler(assetBundleName, assetName, type, owner);

            if (result.Length == 0)
            {
                if (!string.IsNullOrEmpty(assetName))
                    Debug.LogError(LogPrefix + string.Format("load asset bundle fail. assetBundleName: {0}, assetName: {1}, type: {2}", assetBundleName, assetName, type));
                return null;
            }
            return result[0];
        }

        public static T LoadAsset<T>(string assetBundleName, string assetName, object owner = null)
            where T : UnityEngine.Object
        {
            return (T)LoadAsset(assetBundleName, assetName, typeof(T), owner);
        }
        public static Object LoadAsset(string[] assetBundleAndAssetNames, object owner = null)
        {
            return LoadAsset(assetBundleAndAssetNames[0], assetBundleAndAssetNames[1], null, owner);
        }
        public static T LoadAsset<T>(string[] assetBundleAndAssetNames, object owner = null)
            where T : UnityEngine.Object
        {
            return LoadAsset<T>(assetBundleAndAssetNames[0], assetBundleAndAssetNames[1], owner);
        }
        public static T Instantiate<T>(string assetBundleName, string assetName, object owner = null)
        where T : Object
        {
            var obj = LoadAsset<T>(assetBundleName, assetName, owner);
            if (!obj)
                return null;
            return Object.Instantiate(obj);
        }
        public static T Instantiate<T>(string[] assetBundleAndAssetNames, object owner = null)
            where T : Object
        {
            return Instantiate<T>(assetBundleAndAssetNames[0], assetBundleAndAssetNames[1], owner);
        }
        public static GameObject Instantiate(string assetBundleName, string assetName, object owner = null)
        {
            var obj = LoadAsset<GameObject>(assetBundleName, assetName, owner);
            if (!obj)
                return null;
            return Object.Instantiate(obj);
        }
        public static GameObject Instantiate(string assetBundleName, string assetName, Transform parent, object owner = null)
        {
            var obj = LoadAsset<GameObject>(assetBundleName, assetName, owner);
            if (!obj)
                return null;
            return Object.Instantiate(obj, parent);
        }

        public static GameObject Instantiate(string[] assetBundleAndAssetNames, object owner = null)
        {
            return Instantiate(assetBundleAndAssetNames[0], assetBundleAndAssetNames[1], owner);
        }

        public static Object[] LoadAllAssets(string assetBundleName, Type type = null, object owner = null)
        {
            Object[] result;
            if (LoadAssetHandler != null)
            {
                string url = null;
                if (mainManifest != null)
                {
                    var abInfo = GetAssetBundleInfo(assetBundleName);
                    url = abInfo.url;
                }
                LoadAssetRequest request = LoadAssetRequest.FromAsset(url, assetBundleName, null, type, false, owner);
                LoadAssetHandler(request);
                result = request.loadedAssets;
            }
            else
                result = AssetBundleLoadAssetHandler(assetBundleName, null, type, owner);
            return result;
        }
        public static T[] LoadAllAssets<T>(string assetBundleName, object owner = null)
            where T : Object
        {
            return LoadAllAssets(assetBundleName, typeof(Object), owner).Where(o => o is T).Cast<T>().ToArray();
        }

        public static Object[] LoadAllAssets(string[] assetBundleAndAssetNames, Type type = null, object owner = null)
        {
            return LoadAllAssets(assetBundleAndAssetNames[0], type, owner);
        }
        public static T[] LoadAllAssets<T>(string[] assetBundleAndAssetNames, object owner = null)
          where T : UnityEngine.Object
        {
            return LoadAllAssets<T>(assetBundleAndAssetNames[0], owner);
        }


        #region Async


        public static Task<AssetBundle> LoadAssetBundleAsync(string assetBundleName, object owner = null)
        {
            if (assetBundleName == null)
                throw new ArgumentNullException("assetbundleName");
            var abInfo = GetAssetBundleInfo(assetBundleName);
            return Task.Run<AssetBundle>(StartDownloadAssetBundle(abInfo, owner));
        }
        public static Task<AssetBundle> LoadAssetBundleAsync(string[] assetBundleAndAssetNames, object owner = null)
        {
            return LoadAssetBundleAsync(assetBundleAndAssetNames[0], owner);
        }
        //public static Task<AssetBundle> LoadAssetBundleAsync(string assetBundleName, object owner, DownloadAssetBundleCompletedDelegate onCompleted, DownloadAssetBundleFailDelegate onFaulted = null)
        //{
        //    var task = LoadAssetBundleAsync(assetBundleName, owner);
        //    task.ContinueWith((t) =>
        //    {
        //        if (task.IsRanToCompletion)
        //        {
        //            if (onCompleted != null)
        //                onCompleted(assetBundleName, task.Result);
        //        }
        //        else
        //        {
        //            if (onFaulted != null)
        //                onFaulted(assetBundleName, task.Exception);
        //        }
        //    });
        //    return task;
        //}
        //public static Task<AssetBundle> LoadAssetBundleAsync(string[] assetBundleAndAssetNames, object owner, DownloadAssetBundleCompletedDelegate onCompleted, DownloadAssetBundleFailDelegate onFaulted = null)
        //{
        //    return LoadAssetBundleAsync(assetBundleAndAssetNames[0], owner, onCompleted, onFaulted);
        //}
        public static Task<Object> LoadAssetAsync(string assetBundleName, string assetName, Type type = null, object owner = null)
        {
            Task<Object[]> result;
            if (LoadAssetHandler != null)
            {
                string url = null;
                if (mainManifest != null)
                {
                    var abInfo = GetAssetBundleInfo(assetBundleName);
                    url = abInfo.url;
                }
                LoadAssetRequest request = LoadAssetRequest.FromAsset(url, assetBundleName, assetName, type, true, owner);
                LoadAssetHandler(request);
                result = StartAssetRequest(request).StartTask<Object[]>();
            }
            else
            {
                result = AssetBundleLoadAssetsAsyncHandler(assetBundleName, assetName, type, owner);
            }
            return result
                .ContinueWith(t =>
                {
                    if (t.Result.Length == 0)
                    {
                        Debug.LogError(LogPrefix + string.Format("load asset bundle fail, assetBundleName: {0}, assetName: {1}, type:{2}", assetBundleName, assetName, type));
                        return null;
                    }
                    return t.Result[0];
                });
        }

        static IEnumerator StartAssetRequest(LoadAssetRequest request)
        {
            while (!request.isDone)
                yield return null;
            yield return new YieldReturn(request.loadedAssets);
        }

        public static Task<T> LoadAssetAsync<T>(string assetBundleName, string assetName, object owner = null)
            where T : Object
        {
            return LoadAssetAsync(assetBundleName, assetName, typeof(T), owner)
                .ContinueWith(t =>
                {
                    return (T)t.Result;
                });
        }

        public static Task<Object> LoadAssetAsync(string[] assetBundleAndAssetNames, object owner = null)
        {
            return LoadAssetAsync<Object>(assetBundleAndAssetNames[0], assetBundleAndAssetNames[1], owner);
        }
        public static Task<T> LoadAssetAsync<T>(string[] assetBundleAndAssetNames, object owner = null)
            where T : Object
        {
            return LoadAssetAsync<T>(assetBundleAndAssetNames[0], assetBundleAndAssetNames[1], owner);
        }
        public static Task<T> InstantiateAsync<T>(string assetBundleName, string assetName, object owner = null)
            where T : Object
        {
            return LoadAssetAsync<T>(assetBundleName, assetName, owner)
                .ContinueWith(t =>
                {
                    return Object.Instantiate(t.Result);
                });
        }
        public static Task<T> InstantiateAsync<T>(string[] assetBundleAndAssetNames, object owner = null)
            where T : Object
        {
            return InstantiateAsync<T>(assetBundleAndAssetNames[0], assetBundleAndAssetNames[1], owner);
        }
        public static Task<GameObject> InstantiateAsync(string assetBundleName, string assetName, object owner = null)
        {
            return LoadAssetAsync<GameObject>(assetBundleName, assetName, owner)
                .ContinueWith(t =>
                {
                    return Object.Instantiate(t.Result);
                });
        }
        public static Task<GameObject> InstantiateAsync(string[] assetBundleAndAssetNames, object owner = null)
        {
            return InstantiateAsync(assetBundleAndAssetNames[0], assetBundleAndAssetNames[1], owner);
        }

        public static Task<Object[]> LoadAllAssetsAsync(string assetBundleName, Type type = null, object owner = null)
        {
            Task<Object[]> result;
            if (LoadAssetHandler != null)
            {
                string url = null;
                if (mainManifest != null)
                {
                    var abInfo = GetAssetBundleInfo(assetBundleName);
                    url = abInfo.url;
                }
                LoadAssetRequest request = LoadAssetRequest.FromAsset(url, assetBundleName, null, type, true, owner);
                LoadAssetHandler(request);
                result = StartAssetRequest(request).StartTask<Object[]>();
            }
            else
                result = AssetBundleLoadAssetsAsyncHandler(assetBundleName, null, type, owner);
            return result;
        }

        public static Task<T[]> LoadAllAssetsAsync<T>(string assetBundleName, object owner = null)
            where T : Object
        {
            return LoadAllAssetsAsync(assetBundleName, typeof(T), owner)
                .ContinueWith(t =>
                {
                    return t.Result.Cast<T>().ToArray();
                });
        }
        public static Task<Object[]> LoadAllAssetsAsync(string[] assetBundleAndAssetNames, Type type = null, object owner = null)
        {
            return LoadAllAssetsAsync(assetBundleAndAssetNames[0], type, owner);
        }
        public static Task<T[]> LoadAllAssetsAsync<T>(string[] assetBundleAndAssetNames, object owner = null)
            where T : Object
        {
            return LoadAllAssetsAsync<T>(assetBundleAndAssetNames[0], owner);
        }

        public static Task LoadSceneAsync(string[] assetBundleName, LoadSceneMode mode, Action<float> progressCallback = null, object owner = null)
        {
            string sceneName = Path.GetFileNameWithoutExtension(assetBundleName[1]);
            return LoadSceneAsync(assetBundleName[0], sceneName, mode, progressCallback, owner);
        }

        public static Task LoadSceneAsync(string assetBundleName, string sceneName, LoadSceneMode mode = LoadSceneMode.Single, Action<float> progressCallback = null, object owner = null)
        {
            return Task.Run(_LoadSceneAsync(assetBundleName, sceneName, mode, progressCallback, owner));
        }


        static IEnumerator _LoadSceneAsync(string assetBundleName, string sceneName, LoadSceneMode mode, Action<float> progressCallback = null, object owner = null)
        {
            Debug.Log("load scene:" + assetBundleName + ", " + sceneName + ", " + mode);
            if (Mode == AssetBundleMode.Editor)
            {
                SceneManager.LoadSceneAsync(sceneName, mode);
                yield break;
            }

            Task task;
            if (LoadAssetHandler != null)
            {
                string url = null;
                if (mainManifest != null)
                {
                    var abInfo = GetAssetBundleInfo(assetBundleName);
                    url = abInfo.url;
                }
                LoadAssetRequest request = LoadAssetRequest.FromScene(url, assetBundleName, sceneName, mode, true, owner);
                LoadAssetHandler(request);
                task = StartAssetRequest(request).StartTask();
                while (!request.isDone)
                {
                    progressCallback?.Invoke(request.progress);
                    yield return null;
                }
            }
            else
            {
                yield return LoadAssetBundleAsync(assetBundleName);
                var async = SceneManager.LoadSceneAsync(sceneName, mode);
                while (!async.isDone)
                {
                    progressCallback?.Invoke(async.progress);
                    yield return null;
                }
            }

            progressCallback?.Invoke(1f);
        }




        #endregion


        public static IEnumerable<string> AllAssetBundleNames()
        {
            foreach (var manifest in manifests.Values)
            {
                foreach (var abInfo in manifest.assetBundleInfos.Values)
                    yield return abInfo.name;
            }
        }

        public static string GetAssetBundleUrl(string assetBundleName, out Hash128 hash)
        {
            var abInfo = GetAssetBundleInfo(assetBundleName);
            hash = abInfo.hash;
            return abInfo.url;
        }

        public static string GetAssetBundleAndVariantName(string assetBundleName)
        {
            var abInfo = GetAssetBundleInfo(assetBundleName);
            if (abInfo == null)
                return null;
            return abInfo.name;
        }

        static PackageManifest GetManifestByAssetBundleName(string assetBundleName)
        {
            foreach (var data in manifests.Values)
            {
                if (data.assetBundleInfos.ContainsKey(assetBundleName))
                    return data;
            }
            return null;
        }

        public static AssetBundleInfo GetAssetBundleInfo(string assetBundleName, bool isTry = false)
        {
            AssetBundleInfo abInfo = null;
            if (mainManifest == null)
            {
                if (!isTry)
                    Debug.LogError(LogPrefix + "manifest not loaded");
                return null;
            }

            assetBundleName = ResolveAssetBundleNameVariant(assetBundleName);

            mainManifest.assetBundleInfos.TryGetValue(assetBundleName, out abInfo);

            if (abInfo == null)
            {
                if (!isTry)
                    Debug.LogError(LogPrefix + "manifest not contains assetBundle:" + assetBundleName);
            }
            return abInfo;
        }


        static bool TryGetAssetbundle(AssetBundleInfo abInfo, object owner, out AssetBundle assetbundle)
        {
            AssetBundleRef abRef;
            if (abInfo == null)
            {
                assetbundle = null;
                return false;
            }
            if (abRefs.TryGetValue(abInfo.key, out abRef))
            {
                if (abRef.assetBundle != null)
                {
                    if (owner != null)
                        abRef.AddDependent(owner);
                    assetbundle = abRef.assetBundle;
                    return true;
                }
                abRef.Unload(false);
            }
            assetbundle = null;
            return false;
        }
        public static void ResetStatus()
        {
            Status = AssetBundleStatus.None;
            manifests.Clear();
        }

        static IEnumerator StartInitializedAssetBundleManifest(string manifestUrl)
        {
            if (Status == AssetBundleStatus.Initialized)
                yield break;

            string packageName;

            string abUrlTemplate;
            int queryIndex = manifestUrl.IndexOf('?');
            string urlPath = manifestUrl;
            if (queryIndex >= 0)
                urlPath = manifestUrl.Substring(0, queryIndex);

            int index;
            if (urlPath.LastIndexOf('/') > urlPath.LastIndexOf('\\'))
                index = urlPath.LastIndexOf('/');
            else
                index = urlPath.LastIndexOf('\\');
            packageName = urlPath.Substring(index + 1);
            abUrlTemplate = manifestUrl.Substring(0, index + 1) + "{0}" + manifestUrl.Substring(index + 1 + packageName.Length);

            bool nameHasHash = ((Options & AssetBundleOptions.AssetBundleNameHasHash) == AssetBundleOptions.AssetBundleNameHasHash) && (Mode != AssetBundleMode.Editor);

            PackageManifest data;


            var verResult = AssetBundleVersion.Load(manifestUrl);
            yield return verResult;
            Version = verResult.Result;


            if (manifests.TryGetValue(packageName, out data))
            {
                yield break;
            }

            string streamingAsssetsManifestUrl = StreamingAssetsManifestUrl;
            string baseStreamingAssetsManifestUrl = GetUrlDirectoryName(streamingAsssetsManifestUrl);
            Dictionary<string, string> streamingAssetsItems = null;

            AssetBundle assetbundle = null;

            using (var l = syncObj.Lock())
            {
                yield return l;

                if (manifests.TryGetValue(packageName, out data))
                {
                    l.Dispose();
                    yield return new YieldReturn(data.manifest);
                }
                if (!string.Equals(manifestUrl, streamingAsssetsManifestUrl, StringComparison.InvariantCultureIgnoreCase))
                {
                    var coStreamingAssets = LoadManifestItems(streamingAsssetsManifestUrl);
                    yield return coStreamingAssets.Try();
                    if (coStreamingAssets.IsRanToCompletion)
                    {
                        streamingAssetsItems = coStreamingAssets.Result.Key;
                    }

                }
                //if (Logger.logEnabled)
                //    Logger.Log(LogTag, string.Format("download assetbundle manifest: {0}, packageName:{1}", manifestUrl, packageName));

                using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(manifestUrl))
                {
                    yield return request.SendWebRequest();
                    if (request.error != null)
                    {
                        Debug.LogError(LogPrefix + "url: " + manifestUrl);
                        throw new Exception(request.error);
                    }
                    assetbundle = (request.downloadHandler as DownloadHandlerAssetBundle).assetBundle;
                }

            }

            if (assetbundle == null)
                throw new Exception("download assetbundle manifest error");

            AssetBundleManifest manifest;

            manifest = assetbundle.LoadAllAssets(typeof(AssetBundleManifest)).Select(o => o as AssetBundleManifest).FirstOrDefault();

            if (manifest == null)
            {
                throw new Exception("manifest null");
            }

            data = new PackageManifest();

            data.name = packageName;
            data.manifest = manifest;
            if (AssetBundles.mainManifest == null)
                AssetBundles.mainManifest = data;

            var allAssetBundlesWithVariantSet = new Dictionary<string, Dictionary<string, string>>();
            var allAssetBundlesWithVariant = manifest.GetAllAssetBundlesWithVariant();
            var allAssetBundlesWithVariantSet2 = new HashSet<string>(allAssetBundlesWithVariant);


            for (int i = 0; i < allAssetBundlesWithVariant.Length; i++)
            {

                var tmp = ParseAssetBundleNameAndVariant(allAssetBundlesWithVariant[i]);
                string abName = tmp[0];
                if (nameHasHash)
                    abName = abName.Substring(0, abName.Length - 33);
                string variant = tmp[1];
                if (!string.IsNullOrEmpty(variant))
                {
                    Dictionary<string, string> varintToName;

                    if (!allAssetBundlesWithVariantSet.TryGetValue(abName, out varintToName))
                    {
                        varintToName = new Dictionary<string, string>();
                        allAssetBundlesWithVariantSet[abName] = varintToName;
                    }
                    varintToName[variant] = allAssetBundlesWithVariant[i];
                }
            }
            data.allAssetBundlesWithVariantSet = allAssetBundlesWithVariantSet;

            var streamingAssetsVerResult = AssetBundleVersion.Load(StreamingAssetsManifestUrl);
            yield return streamingAssetsVerResult;
            var streamingAssetsVer = streamingAssetsVerResult.Result;

            foreach (var assetbundleName in manifest.GetAllAssetBundles())
            {
                AssetBundleInfo abInfo = new AssetBundleInfo();
                abInfo.name = assetbundleName;

                abInfo.hash = manifest.GetAssetBundleHash(assetbundleName);

                if (streamingAssetsItems != null &&
                    streamingAssetsItems.ContainsKey(assetbundleName) &&
                    streamingAssetsItems[assetbundleName] == abInfo.hash.ToString() &&
                    streamingAssetsVer != null &&
                    streamingAssetsVer.HasBundle(assetbundleName))
                {
                    abInfo.url = UrlCombine(baseStreamingAssetsManifestUrl, assetbundleName);
                }
                else
                {
                    abInfo.url = string.Format(abUrlTemplate, abInfo.name/*.Replace("/", "\\")*/);
                }
                
                abInfo.isVariant = allAssetBundlesWithVariantSet2.Contains(assetbundleName);

                if (nameHasHash)
                {
                    if (abInfo.isVariant)
                    {
                        var tmp = ParseAssetBundleNameAndVariant(assetbundleName);
                        data.assetBundleInfos[tmp[0].Substring(0, tmp[0].Length - 33) + "." + tmp[1]] = abInfo;
                    }
                    else
                    {
                        data.assetBundleInfos[assetbundleName.Substring(0, assetbundleName.Length - 33)] = abInfo;
                    }
                }


                abInfo.key = new AssetBundleKey(abInfo.name, abInfo.hash);

                data.assetBundleInfos[assetbundleName] = abInfo;
            }

            CryptoAssetBundleManifest(data);

            OnVariantChanged(data);


            manifests[packageName] = data;

            manifest = null;
            assetbundle.Unload(false);

            //if (Logger.logEnabled)
            //    Logger.Log(LogTag, "download assetbundle manifest ok.");

            ManifestUrl = manifestUrl;

            if (AssetBundleNamesType == null)
            {
                AssetBundleNamesType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(o => o.GetType("AssetBundleNames")).Where(o => o != null).FirstOrDefault();
            }

            yield return LoadAddressable();

            //预加载
            //yield return PreloadAssetBundleAsync();
            //var coPreload = PreloadAssetBundleAsync().StartCoroutine();
            //yield return coPreload.Try();

            if (Status == AssetBundleStatus.Error)
                yield break;
            //if (Status == AssetBundleStatus.Preloading)
            //{
            //    if (coPreload.Exception != null)
            //    {
            //        Debug.LogException(coPreload.Exception);
            //        Status = AssetBundleStatus.Error;
            //        yield break;
            //    }
            //}

            Status = AssetBundleStatus.Initialized;
            Debug.Log(LogPrefix + $"Initialized\nversion: {Version}\nmanifest: {manifestUrl}");
        }

        static void CryptoAssetBundleManifest(PackageManifest manifest)
        {
            if (!(AssetBundleSettings.CryptoEnabled || AssetBundleSettings.SignatureEnabled))
                return;

            regexCryptoInclude = null;
            regexCryptoExclude = null;

            if (AssetBundleSettings.CryptoEnabled)
            {
                cryptoKey = Convert.FromBase64String(AssetBundleSettings.CryptoKey);
                Debug.Assert(cryptoKey.Length == 8, "Crypto Key bytes length :" + cryptoKey.Length);
                cryptoIV = Convert.FromBase64String(AssetBundleSettings.CryptoIV);
                Debug.Assert(cryptoIV.Length == 8, "Crypto IV bytes length :" + cryptoIV.Length);
                cryptoSA = DES.Create();
                if (!string.IsNullOrEmpty(AssetBundleSettings.CryptoInclude))
                {
                    regexCryptoInclude = new Regex(/*FileNamePatternToRegexPattern*/(AssetBundleSettings.CryptoInclude), RegexOptions.IgnoreCase);
                }
                if (!string.IsNullOrEmpty(AssetBundleSettings.CryptoExclude))
                {
                    regexCryptoExclude = new Regex(/*FileNamePatternToRegexPattern*/(AssetBundleSettings.CryptoExclude), RegexOptions.IgnoreCase);
                }

                foreach (var item in manifest.AssetBundleInfos)
                {
                    if (IsCryptoBundle(item.Name))
                        item.isCrypto = true;
                }
            }


            regexSignatureInclude = null;
            regexSignatureExclude = null;

            if (AssetBundleSettings.SignatureEnabled)
            {
                rsa = new RSACryptoServiceProvider();
                rsa.ImportCspBlob(Convert.FromBase64String(AssetBundleSettings.SignaturePublicKey));
                sha = new SHA1CryptoServiceProvider();

                if (!string.IsNullOrEmpty(AssetBundleSettings.SignatureInclude))
                {
                    regexSignatureInclude = new Regex(/*FileNamePatternToRegexPattern*/(AssetBundleSettings.SignatureInclude), RegexOptions.IgnoreCase);
                }
                if (!string.IsNullOrEmpty(AssetBundleSettings.SignatureExclude))
                {
                    regexSignatureExclude = new Regex(/*FileNamePatternToRegexPattern*/(AssetBundleSettings.SignatureExclude), RegexOptions.IgnoreCase);
                }
                foreach (var item in manifest.AssetBundleInfos)
                {
                    if (IsSignatureBundle(item.Name))
                        item.isSignature = true;
                }
            }

        }



        public static bool IsCryptoBundle(string bundleName)
        {
            if (!AssetBundleSettings.CryptoEnabled)
                return false;
            if (regexCryptoInclude != null && !regexCryptoInclude.IsMatch(bundleName))
                return false;
            if (regexCryptoExclude != null && regexCryptoExclude.IsMatch(bundleName))
                return false;
            return true;
        }



        public static bool IsSignatureBundle(string bundleName)
        {
            if (!AssetBundleSettings.SignatureEnabled)
                return false;
            if (regexSignatureInclude != null && !regexSignatureInclude.IsMatch(bundleName))
                return false;
            if (regexSignatureExclude != null && regexSignatureExclude.IsMatch(bundleName))
                return false;
            return true;
        }


        static string FileNamePatternToRegexPattern(string fileNamePattern)
        {
            return fileNamePattern.Replace(".", "\\.").Replace("*", ".*");
        }

        static string AssetBundleNameRemoveHash(string abName)
        {
            return abName.Substring(0, abName.Length - 33);
        }

        //public static void OnVariantChanged()
        //{
        //    OnVariantChanged(mainManifest);
        //}
        static void OnVariantChanged(PackageManifest package)
        {
            var manifest = package.manifest;
            //dependencies
            List<AssetBundleInfo> deps = new List<AssetBundleInfo>();
            foreach (var abInfo in package.assetBundleInfos.Values)
            {
                if (abInfo.allDependencies == null)
                {
                    var allDependencies = manifest.GetAllDependencies(abInfo.name);
                    deps.Clear();
                    for (int i = 0, len = allDependencies.Length; i < len; i++)
                    {
                        string abName = ResolveAssetBundleNameVariant(allDependencies[i]);
                        //ignore dumplication variant
                        if (deps.Where(o => o.name == abName).Count() > 0)
                            continue;
                        deps.Add(package.assetBundleInfos[abName]);
                    }
                    abInfo.allDependencies = deps.ToArray();
                }
            }
        }



        static string[] ParseAssetBundleNameAndVariant(string assetBundleName)
        {
            string[] nameAndVariant;
            if (!assetBundleNameAndVariants.TryGetValue(assetBundleName, out nameAndVariant))
            {
                int index = assetBundleName.LastIndexOf('.');
                if (index >= 0)
                {
                    nameAndVariant = new string[] { assetBundleName.Substring(0, index), index + 1 < assetBundleName.Length ? assetBundleName.Substring(index + 1) : string.Empty };
                }
                else
                {
                    nameAndVariant = new string[] { assetBundleName, "" };
                }

                assetBundleNameAndVariants[assetBundleName] = nameAndVariant;
            }
            return nameAndVariant;
        }

        public static string ResolveAssetBundleNameVariant(string assetBundleName)
        {
            string[] nameAndVariant = ParseAssetBundleNameAndVariant(assetBundleName);

            Dictionary<string, string> variantSet;
            if (mainManifest.allAssetBundlesWithVariantSet.TryGetValue(nameAndVariant[0], out variantSet))
            {
                string newName = null;
                for (int i = 0, len = Variants.Count; i < len; i++)
                {
                    if (variantSet.TryGetValue(Variants[i], out newName))
                    {
                        break;
                    }
                }

                if (newName == null)
                {
                    if (Logger.logEnabled)
                        Logger.LogWarning(LogPrefix, string.Format("not found match variant. first variant: {0}, assetBundle:{1}", variantSet.Values.First(), assetBundleName));
                    return variantSet.Values.First();
                }
                return newName;
            }
            return assetBundleName;
        }


        private static IEnumerator StartDownloadAssetBundle(AssetBundleInfo abInfo, object owner)
        {
            AssetBundleRef abRef;
            AssetBundle assetBundle;


            if (TryGetAssetbundle(abInfo, owner, out assetBundle))
            {
                yield return new YieldReturn(assetBundle);
            }


            using (var l = syncObj.Lock())
            {
                yield return l;

                if (TryGetAssetbundle(abInfo, owner, out assetBundle))
                {
                    l.Dispose();
                    yield return new YieldReturn(assetBundle);
                }

                //if (Logger.logEnabled)
                //    Logger.Log(LogTag, string.Format("download assetbundle, name:{0}, url:{1}", abInfo.name, abInfo.url));

                bool handle = false;

                if (LoadAssetBundleHandler != null)
                {
                    LoadAssetRequest request = LoadAssetRequest.FromAssetBundle(abInfo.url, abInfo.name, true, owner);
                    LoadAssetBundleHandler(request);

                    yield return request;
                    assetBundle = request.loadedAssetBundle;
                    handle = true;
                }
                else
                {
                    if (abInfo.isSignature || abInfo.isCrypto)
                    {
                        LoadAssetRequest request = LoadAssetRequest.FromAssetBundle(abInfo.url, abInfo.name, true, owner);
                        yield return DecryptorAssetBundleAsync(abInfo, request);
                        assetBundle = request.loadedAssetBundle;
                        handle = true;
                    }
                    else
                    {
                        if ((Options & AssetBundleOptions.DisableLoadFromFile) == 0)
                        {
                            if (abInfo.url.StartsWith("file://"))
                            {
                                var fiileAsync = AssetBundle.LoadFromFileAsync(abInfo.url.Substring(7));
                                yield return fiileAsync;
                                assetBundle = fiileAsync.assetBundle;
                                handle = true;
                            }
                            else if (abInfo.url.StartsWith("jar:file://"))
                            {
                                //andriod
                                var jarAsync = AssetBundle.LoadFromFileAsync(abInfo.url);
                                yield return jarAsync;
                                assetBundle = jarAsync.assetBundle;
                                handle = true;
                            }
                        }

                        if (!handle)
                        {

                            using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(abInfo.url, abInfo.hash, 0))
                            {
                                yield return request.SendWebRequest();
                                if (request.error != null)
                                {
                                    throw new Exception(request.error);
                                }
                                assetBundle = (request.downloadHandler as DownloadHandlerAssetBundle).assetBundle;

                            }
                            handle = true;
                        }
                    }
                }

                abRef = OnLoadAssetBundle(abInfo, assetBundle, owner);

            }


            //if (abInfo.allDependencies == null)
            //{
            //    var allDependencies = mainManifest.manifest.GetAllDependencies(abInfo.name);
            //    List<AssetBundleInfo> list = new List<AssetBundleInfo>(allDependencies.Length);
            //    for (int i = 0; i < allDependencies.Length; i++)
            //    {
            //        string abName = ResolveAssetBundleNameVariant(allDependencies[i]);
            //        //ignore dumplication variant
            //        if (list.Where(o => o.name == abName).Count() > 0)
            //            continue;
            //        list.Add(mainManifest.assetBundleInfos[abName]);
            //    }
            //    abInfo.allDependencies = list.ToArray();
            //}

            foreach (var dep in abInfo.allDependencies)
            {
                AssetBundle tmp;
                if (!TryGetAssetbundle(dep, null, out tmp))
                {
                    yield return StartDownloadAssetBundle(dep, null);
                }
            }

            yield return new YieldReturn(abRef.assetBundle);
        }
        static AssetBundle DecryptorAssetBundle(AssetBundleInfo abInfo)
        {
            byte[] bytes = null;
            bytes = ReadFileAllBytes(abInfo.url);

            if (abInfo.isSignature)
            {
                bytes = ParseSignature(bytes, abInfo.Name);
            }

            if (abInfo.isCrypto)
            {
                bytes = Decryption(bytes, abInfo.Name);
            }
            return AssetBundle.LoadFromMemory(bytes);
        }

        static IEnumerator DecryptorAssetBundleAsync(AssetBundleInfo abInfo, LoadAssetRequest assetRequest)
        {
            byte[] bytes = null;
            //Debug.Log(LogPrefix + "DecryptorAssetBundleAsync: " + abInfo.url);

            using (UnityWebRequest request = UnityWebRequest.Get(abInfo.url))
            {
                yield return request.SendWebRequest();
                if (request.error != null)
                {
                    throw new Exception(request.error);
                }
                bytes = request.downloadHandler.data;

                if (abInfo.isSignature)
                {
                    bytes = ParseSignature(bytes, abInfo.Name);
                }

                if (abInfo.isCrypto)
                {
                    bytes = Decryption(bytes, abInfo.Name);
                }

                assetRequest.loadedAssetBundle = AssetBundle.LoadFromMemory(bytes);

                assetRequest.Done();
            }
        }

        static byte[] ParseSignature(byte[] data, string name)
        {
            if (data == null)
            {
                return null;
            }
            if (data.Length < 128)
            {
                throw new InvalidProgramException(name + " signature length less than 128!");
            }
            if (IsLogEnabled)
                Debug.Log(LogPrefix + "parse signature <" + name + ">");
            byte[] filecontent;
            try
            {
                byte[] sig = new byte[128];
                filecontent = new byte[data.Length - 128];
                Array.Copy(data, sig, 128);
                Array.Copy(data, 128, filecontent, 0, filecontent.Length);

                if (!rsa.VerifyData(filecontent, sha, sig))
                {
                    throw new InvalidProgramException(name + " has invalid signature!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(LogPrefix + "parse signature error, <" + name + ">");
                throw ex;
            }

            return filecontent;
        }

        static byte[] Decryption(byte[] data, string abName)
        {
            if (IsLogEnabled)
                Debug.Log(LogPrefix + "decryption <" + abName + ">");
            try
            {
                var decryptor = cryptoSA.CreateDecryptor(cryptoKey, cryptoIV);
                data = decryptor.TransformFinalBlock(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Debug.LogError("decryption error, <" + abName + ">");
                throw ex;
            }
            return data;
        }

        public static Action<string> OnLoadAssetBundleCallback;

        private static AssetBundleRef OnLoadAssetBundle(AssetBundleInfo abInfo, AssetBundle assetBundle, object owner)
        {
            AssetBundleRef abRef;
            if (!assetBundle)
            {
                abRef = new AssetBundleRef(abInfo, null);
                abRef.exception = new Exception("assetbundle null, " + abInfo.name);
                abRefs[abInfo.key] = abRef;

                throw abRef.exception;
            }

            abRef = new AssetBundleRef(abInfo, assetBundle);
            if (owner != null)
                abRef.AddDependent(owner);
            abRefs[abInfo.key] = abRef;
            OnLoadAssetBundleCallback?.Invoke(abInfo.Name);
            return abRef;
        }


        public static void Unload(string url)
        {
            Unload(url, false);
        }


        /// <summary>
        /// 卸载指定的AssetBundle
        /// </summary>
        /// <param name="url"></param>
        /// <param name="allObjects"></param> 

        public static void Unload(string assetBundleName, bool allObjects)
        {
            var abInfo = GetAssetBundleInfo(assetBundleName);
            AssetBundleRef abRef;
            if (abRefs.TryGetValue(abInfo.key, out abRef))
            {
                abRef.Unload(allObjects);
                abRefs.Remove(abInfo.key);
            }
        }
        /// <summary>
        /// 卸载所有AssetBundle
        /// </summary>
        public static void UnloadAll()
        {
            UnloadAll(false);
        }

        public static void UnloadAll(bool allObjects)
        {
            foreach (var abRef in abRefs.Values)
            {
                abRef.Unload(allObjects);
            }

            abRefs.Clear();
            Resources.UnloadUnusedAssets();

        }


        public static void UnloadUnused()
        {
            UnloadUnused(false);
        }
        public static void UnloadUnused(bool allObjects)
        {

            UnloadUnused(abRefs.Values.Select(o => o.abInfo.name), allObjects);
        }

        /// <summary>
        /// 卸载未引用的AssetBundle
        /// </summary>
        /// <param name="allObjects"></param>
        public static void UnloadUnused(IEnumerable<string> assetBundleNames, bool allObjects = false)
        {

            while (true)
            {
                tmps.Clear();
                foreach (var abName in assetBundleNames)
                {
                    AssetBundleRef abRef;
                    if (!abRefs.TryGetValue(GetAssetBundleInfo(abName).key, out abRef))
                        continue;
                    if (!abRef.IsDependent)
                    {
                        bool hasDep = false;
                        foreach (var item in abRefs.Values)
                        {
                            if (item == abRef)
                                continue;
                            if (!item.IsDependent)
                                continue;
                            if (item.abInfo.allDependencies != null)
                            {
                                foreach (var dep in item.abInfo.allDependencies)
                                {
                                    if (dep == abRef.abInfo)
                                    {
                                        hasDep = true;
                                        break;
                                    }
                                }
                            }
                            if (hasDep)
                                break;
                        }

                        if (hasDep)
                            continue;

                        tmps.Add(abRef);

                        abRef.Unload(allObjects);
                        if (Logger.logEnabled)
                            Logger.Log(LogPrefix + string.Format("Unload unused assetbundle: {0}, allObjects:{1}", abRef.abInfo.name, allObjects));
                    }
                }

                if (tmps.Count <= 0)
                    break;

                foreach (var it in tmps)
                {
                    abRefs.Remove(it.abInfo.key);
                }
                tmps.Clear();
            }


            Resources.UnloadUnusedAssets();
        }

        static string GetExtractAssetBundlePath()
        {
            string extractPath = Path.Combine(Application.persistentDataPath, "AssetBundlesExtract");
            return extractPath;
        }
        public static string ExtractAssetBundle(AssetBundle assetBundle, string assetBundleName)
        {
            string extractPath = GetExtractAssetBundlePath();
            return ExtractAssetBundle(assetBundle, assetBundleName, extractPath);
        }

        public static string ExtractAssetBundle(AssetBundle assetBundle, string assetBundleName, string basePath)
        {
            assetBundleName = ResolveAssetBundleNameVariant(assetBundleName);
            Hash128 hash = Manifest.GetAssetBundleHash(assetBundleName);
            string assetBundlePath = Path.Combine(basePath, hash.ToString());

            if (!Directory.Exists(assetBundlePath))
            {
                try
                {
                    Directory.CreateDirectory(assetBundlePath);

                    bool hasFile = false;
                    foreach (var assetName in assetBundle.GetAllAssetNames())
                    {
                        TextAsset textAsset = assetBundle.LoadAsset<TextAsset>(assetName);
                        if (!textAsset)
                            continue;
                        string assetPath = Path.Combine(assetBundlePath, assetName);
                        if (!Directory.Exists(Path.GetDirectoryName(assetPath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                        if (File.Exists(assetPath))
                        {
                            File.SetAttributes(assetPath, FileAttributes.Normal);
                        }
                        File.WriteAllBytes(assetPath, textAsset.bytes);
                        hasFile = true;
                    }

                    if (Logger.logEnabled)
                    {
                        if (hasFile)
                            Logger.Log(LogPrefix + string.Format("extra assetbundle {0}\n{1}", assetBundleName, assetBundlePath));
                        else
                            Logger.LogWarning(LogPrefix, string.Format("extra assetbundle empty files. {0}", assetBundleName));
                    }
                }
                catch (Exception ex)
                {
                    if (Directory.Exists(assetBundlePath))
                        Directory.Delete(assetBundlePath, true);

                    throw ex;
                }
            }

            return assetBundlePath;
        }

        public static void ClearUnusedExtractAssetBundle()
        {
            string extractPath = GetExtractAssetBundlePath();
            if (!Directory.Exists(extractPath))
                return;

            HashSet<string> hashSet = null;

            foreach (var dir in Directory.GetDirectories(extractPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (hashSet == null)
                {
                    hashSet = new HashSet<string>();
                    foreach (var abName in Manifest.GetAllAssetBundles())
                    {
                        hashSet.Add(Manifest.GetAssetBundleHash(abName).ToString());
                    }
                }

                if (!hashSet.Contains(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }



        public AssetBundleRef[] AllAssetBundleRefs()
        {
            return abRefs.Values.ToArray();
        }


        /// <summary>
        /// 下载需要提供版本配置文件<see cref="VersionFile"/>
        /// </summary>
        /// <param name="downloadAssetBundlesUrl"></param>
        /// <returns></returns>
        static Task DownloadAsync(string downloadAssetBundlesUrl)
        {
            DownloadError = null;
            DownloadTotal = 0;
            DownloadProgress = 0;


            var task = Task.Run(_Download(downloadAssetBundlesUrl));
            task.ContinueWith(t =>
            {
                if (Status == AssetBundleStatus.Downloading)
                {
                    if (t.Exception == null)
                    {
                        Status = AssetBundleStatus.Downloaded;
                    }
                    else
                    {
                        Status = AssetBundleStatus.Error;
                        DownloadErrorCount++;
                        DownloadError = t.Exception.InnerExceptions.First();
                        Debug.LogError(LogPrefix + "AssetBundle Download Error: " + DownloadError);
                    }
                }
            });
            return task;
        }

        #region Utils

        const char UrlSeparatorChar = '/';
        const char UrlCompatibilitySeparatorChar = '\\';


        public static string UrlReplaceSeparator(string url)
        {
            return url.Replace(UrlCompatibilitySeparatorChar, UrlSeparatorChar);
        }

        public static bool IsUrlSeparatorChar(char ch)
        {
            if (ch == UrlSeparatorChar || ch == UrlCompatibilitySeparatorChar)
                return true;
            return false;
        }

        public static int UrlSeparatorIndex(string url)
        {
            int index1 = url.IndexOf(UrlSeparatorChar);
            int index2 = url.IndexOf(UrlCompatibilitySeparatorChar);
            if (index2 < index1)
                return index2;
            return index1;
        }

        public static int LastUrlSeparatorIndex(string url)
        {
            int index1 = url.LastIndexOf(UrlSeparatorChar);
            int index2 = url.LastIndexOf(UrlCompatibilitySeparatorChar);
            if (index2 > index1)
                return index2;
            return index1;
        }

        public static string GetUrlDirectoryName(string url)
        {
            int index = LastUrlSeparatorIndex(url);
            if (index < 0)
                return url;
            return url.Substring(0, index);
        }

        public static string UrlCombine(string url1, string url2)
        {
            if (url1 == null)
                url1 = string.Empty;
            if (url2 == null)
                url2 = string.Empty;
            url1 = url1.Trim();
            url2 = url2.Trim();
            if (url1.Length == 0)
                return url2;
            else if (url2.Length == 0)
                return url1;
            if (IsUrlSeparatorChar(url1[url1.Length - 1]))
            {
                if (IsUrlSeparatorChar(url2[0]))
                    return url1 + url2.Substring(1);
                else
                    return url1 + url2;
            }
            else
            {
                if (IsUrlSeparatorChar(url2[0]))
                    return url1 + url2;
                else
                    return url1 + UrlSeparatorChar + url2;
            }

        }

        #endregion


        static Dictionary<string, string> remoteDownloadItems;
        static Dictionary<string, string> streamingAssetsDownloadItems;


        public static Task<KeyValuePair<Dictionary<string, string>, byte[]>> LoadManifestItems(string manifestUrl)
        {
            return Task.Run<KeyValuePair<Dictionary<string, string>, byte[]>>(_LoadManifestItems(manifestUrl));
        }

        static IEnumerator _LoadManifestItems(string manifestUrl)
        {
            Dictionary<string, string> items = new Dictionary<string, string>();
            byte[] data = null;

            using (UnityWebRequest request = UnityWebRequest.Get(manifestUrl))
            {
                yield return request.SendWebRequest();
                if (!string.IsNullOrEmpty(request.error))
                    throw new Exception(request.error + ", url:" + manifestUrl);

                data = request.downloadHandler.data;
                if (data == null || data.Length == 0)
                    throw new Exception("null data");

                var ab = AssetBundle.LoadFromMemory(data);
                if (ab == null)
                    throw new Exception("AssetBundle LoadFromMemory null.");

                var manifest = ab.LoadAllAssets<AssetBundleManifest>().FirstOrDefault();
                if (manifest != null)
                {
                    foreach (var abName in manifest.GetAllAssetBundles())
                    {
                        items[abName] = manifest.GetAssetBundleHash(abName).ToString();
                    }
                }
                ab.Unload(true);
            }
            yield return new YieldReturn(new KeyValuePair<Dictionary<string, string>, byte[]>(items, data));
        }


        static IEnumerator _Download(string downloadAssetBundlesUrl)
        {
            string localManifestUrl = LocalManifestUrl;
            string localManifestDir = LocalManifestDirectory;
            string localManifestName = PlatformName;
            string localManifestFilePath = LocalManifestPath;
            string streamingAssetsManifestUrl = StreamingAssetsManifestUrl;
            string remoteManifestUrl = null;
            AssetBundleVersion remoteVersion = null;
            RemoteVersion = null;
            //下载远程版本列表
            if (!string.IsNullOrEmpty(downloadAssetBundlesUrl))
            {
                var taskRemoteVersion = AssetBundleVersion.DownloadRemoteVersion(downloadAssetBundlesUrl);
                yield return taskRemoteVersion;
                if (taskRemoteVersion.Result != null)
                {
                    remoteVersion = taskRemoteVersion.Result;

                    if (remoteVersion != null)
                        OnDownloadVersionFile?.Invoke(remoteVersion);

                    //if (OnNewAppVersion != null)
                    //{
                    //    System.Version appVer = null;
                    //    System.Version remoteAppVer = null;
                    //    if (!string.IsNullOrEmpty(AppVersion))
                    //    {
                    //        appVer = new Version(AppVersion);
                    //    }
                    //    if (!string.IsNullOrEmpty(remoteVersion.appVersion))
                    //    {
                    //        remoteAppVer = new Version(remoteVersion.appVersion);
                    //    }

                    //    if (remoteAppVer != null && appVer != null && remoteAppVer > appVer)
                    //    {
                    //        Debug.Log("new app version: " + remoteAppVer);
                    //        var newAppArgs = new NewAppVersionEventArgs()
                    //        {
                    //            CurrentVersion = AppVersion,
                    //            NewVersion = remoteVersion.appVersion
                    //        };
                    //        OnNewAppVersion(newAppArgs);
                    //        if (newAppArgs.AssetBundleDownlaodCanceled)
                    //        {
                    //            throw new Exception("New App Version Canceled");
                    //        }
                    //    }
                    //}


                    if (remoteVersion != null)
                    {
                        //获取远程清单地址
                        if (!string.IsNullOrEmpty(remoteVersion.redirectUrl))
                            remoteManifestUrl = remoteVersion.redirectUrl;
                        else
                            remoteManifestUrl = GetManifestPathWithVersion(downloadAssetBundlesUrl, remoteVersion);
                        if (string.IsNullOrEmpty(remoteManifestUrl))
                            remoteManifestUrl = null;
                    }
                }
            }
            RemoteVersion = remoteVersion;

            if (remoteVersion == null)
            {
                if (AssetBundleSettings.RequireDownload)
                    throw new Exception("require remote version");
                ///发生断网情况下没有完整下载则报错
                if (File.Exists(GetDownloadingLockPath()))
                    throw new Exception("download remote version error, previous download was not completed");
            }


            Dictionary<string, AssetBundleVersion> manifestUrls = new Dictionary<string, AssetBundleVersion>();
            manifestUrls.Add(localManifestUrl, null);
            manifestUrls.Add(streamingAssetsManifestUrl, null);
            if (remoteVersion != null)
                manifestUrls.Add(remoteManifestUrl, remoteVersion);


            var taskLatest = AssetBundleVersion.GetLatest(manifestUrls);
            yield return taskLatest;
            var manifestVers = taskLatest.Result;

            for (int i = 0; i < manifestVers.Length; i++)
            {
                var item = manifestVers[i];
                Debug.Log(LogPrefix + $"latest version {i}, {item.Key}\n{JsonUtility.ToJson(item.Value, true)}");
            }

            AssetBundleVersion latestVersion = manifestVers[0].Value;
            string latestManifestUrl = null;
            AssetBundleVersion remoteVer = null, localVer = null, streamingAssetsVer = null;

            if (remoteManifestUrl != null)
            {
                remoteVer = manifestVers.Where(o => o.Key == remoteManifestUrl).First().Value;
            }

            localVer = manifestVers.Where(o => o.Key == localManifestUrl).First().Value;
            streamingAssetsVer = manifestVers.Where(o => o.Key == streamingAssetsManifestUrl).First().Value;

            if (latestVersion != null)
            {
                latestManifestUrl = manifestVers[0].Key;
            }

            if (string.IsNullOrEmpty(latestManifestUrl))
                throw new Exception("check latest asset bundle manifest fail");


            string[] includeGroup, excludeGroup;

            if (!string.IsNullOrEmpty(DownloadIncludeGroup))
                includeGroup = DownloadIncludeGroup.Split('|');
            else
                includeGroup = new string[0];

            if (!string.IsNullOrEmpty(DownloadExcludeGroup))
                excludeGroup = DownloadExcludeGroup.Split('|');
            else
                excludeGroup = new string[0];

            bool groupChanged = false;

            //if (localVer != null)
            //{
            //    for (int i = 0; i < localVer.groups.Length; i++)
            //    {
            //        if (excludeGroup.Contains(localVer.groups[i]))
            //        {
            //            Debug.Log(LogPrefix + $"local exclude group: <{localVer.groups[i]}>");
            //            groupChanged = true;
            //            localVer.groups[i] = null;
            //        }
            //    }
            //    localVer.groups = localVer.groups.Where(o => o != null).ToArray();
            //}
            //else
            //{
            //    Debug.Log(LogPrefix + "local ver null");
            //}

            if (remoteVer != null && localVer != null &&   remoteVer.IsLatest(localVer) && latestManifestUrl != remoteManifestUrl)
            {
                if (!groupChanged )
                {
                    foreach (var g in remoteVer.groups)
                    {
                        if (excludeGroup.Contains(g))
                            continue;
                        if (localVer == null || !localVer.HasGroup(g))
                        {
                            Debug.Log(LogPrefix + $"local not include group: <{g}>");
                            groupChanged = true;
                            break;
                        }
                    }
                } 

                if (groupChanged)
                {
                    latestVersion = remoteVer;
                    latestManifestUrl = remoteManifestUrl;
                    Debug.Log(LogPrefix + "group changed set latest remote");
                }
            }

            Debug.Log(LogPrefix + "latest manifest: " + latestManifestUrl);

            if (!Directory.Exists(localManifestDir))
                Directory.CreateDirectory(localManifestDir);

            if (latestManifestUrl != remoteManifestUrl)
            {
                //本地是最新的
                if (latestManifestUrl == localManifestUrl)
                {
                    //Debug.Log("skip asset bundle download. local lastest");
                    ManifestUrl = localManifestUrl;
                    ClearDownloadingLock();
                    yield break;
                }
                else if (latestManifestUrl == streamingAssetsManifestUrl)
                {
                    //streamingAssets 最新的 
                    //如果本地已下载部分，远程版本未下载完整则不能使用 streamingAssets
                    if (remoteManifestUrl != null && remoteVer == null && localVer == null && File.Exists(localManifestFilePath))
                    {
                        throw new Exception("must be connected to the network updated.");
                    }

                    //清除所有本地文件
                    if (Directory.Exists(localManifestDir))
                        Directory.Delete(localManifestDir, true);
                    if (!Directory.Exists(localManifestDir))
                        Directory.CreateDirectory(localManifestDir);

                    using (UnityWebRequest request = UnityWebRequest.Get(streamingAssetsManifestUrl))
                    {
                        yield return request.SendWebRequest();
                        if (request.error != null)
                            throw new Exception(request.error);
                        File.WriteAllBytes(localManifestFilePath, request.downloadHandler.data);
                    }

                    if (!string.IsNullOrEmpty(FormatString(AssetBundleSettings.VersionFile)))
                    {
                        AssetBundleVersion.Save($"{localManifestDir}/{ FormatString(AssetBundleSettings.VersionFile)}", streamingAssetsVer);
                    }

                    ManifestUrl = localManifestUrl;
                    ClearDownloadingLock();
                    yield break;
                }
            }


            if (remoteVersion != null)
            {
                CreateDownloadingLock();
            }

            Dictionary<string, string> localManifestItems = null;
            Dictionary<string, string> remoteManifestItems = null;
            Dictionary<string, string> streamingAssetsManifestItems = null;
            byte[] remoteManifestData;
            bool downloadStreamingAssets = (Options & AssetBundleOptions.DownloadStreamingAssetsToLocal) == AssetBundleOptions.DownloadStreamingAssetsToLocal;


            //获取本地的文件和哈希值
            var taskManifestItems = LoadManifestItems(localManifestUrl);
            yield return taskManifestItems.Try();
            if (taskManifestItems.IsRanToCompletion)
                localManifestItems = taskManifestItems.Result.Key;
            if (localManifestItems == null)
                localManifestItems = new Dictionary<string, string>();
            if (remoteDownloadItems == null)
                remoteDownloadItems = new Dictionary<string, string>();
            remoteDownloadItems.Clear();

            //获取streamingAssets的文件和哈希值
            taskManifestItems = LoadManifestItems(streamingAssetsManifestUrl);
            yield return taskManifestItems.Try();
            if (taskManifestItems.IsRanToCompletion)
                streamingAssetsManifestItems = taskManifestItems.Result.Key;
            if (streamingAssetsManifestItems == null)
                streamingAssetsManifestItems = new Dictionary<string, string>();
            if (streamingAssetsDownloadItems == null)
                streamingAssetsDownloadItems = new Dictionary<string, string>();
            streamingAssetsDownloadItems.Clear();

            //获取远程的文件和哈希值
            taskManifestItems = LoadManifestItems(remoteManifestUrl);
            yield return taskManifestItems.Try();
            if (taskManifestItems.Exception != null)
            {
                DownloadError = taskManifestItems.Exception;
                throw taskManifestItems.Exception;
            }

            remoteManifestItems = taskManifestItems.Result.Key;
            remoteManifestData = taskManifestItems.Result.Value;

            if (remoteManifestItems.Count == 0)
                throw new Exception("remote manifest items count 0");

            int total = 0;


            Debug.Log(LogPrefix + $"start download remote manifest:{remoteManifestUrl}");


            foreach (var abName in localManifestItems.Keys.ToArray())
            {
                if (localVer != null && !localVer.HasBundle(abName))
                {
                    localManifestItems.Remove(abName);
                    continue;
                }
                if (!remoteManifestItems.ContainsKey(abName) || localManifestItems[abName] != remoteManifestItems[abName])
                {
                    localManifestItems.Remove(abName);
                    continue;
                }
                string path = Path.Combine(localManifestDir, abName);
                if (!File.Exists(path))
                {
                    localManifestItems.Remove(abName);
                    continue;
                }
            }

            foreach (var abName in streamingAssetsManifestItems.Keys.ToArray())
            {
                if (streamingAssetsVer == null || !streamingAssetsVer.HasBundle(abName))
                {
                    streamingAssetsManifestItems.Remove(abName);
                    continue;
                }
            }

            //清理本地文件
            foreach (var filename in Directory.GetFiles(localManifestDir, "*", SearchOption.AllDirectories))
            {
                string abName = filename.Substring(localManifestDir.Length + 1).Replace('\\', '/');
                if (abName == localManifestName)
                    continue;
                if (!localManifestItems.ContainsKey(abName))
                {
                    File.Delete(filename);
                    continue;
                }

                if (!downloadStreamingAssets)
                {
                    if (remoteManifestItems.ContainsKey(abName) &&
                        streamingAssetsManifestItems.ContainsKey(abName) &&
                        remoteManifestItems[abName] == streamingAssetsManifestItems[abName])
                    {
                        File.Delete(filename);
                        continue;
                    }
                }

            }
            DeleteAllEmptyDirectory(localManifestDir);
            //写入最新的清单文件
            File.WriteAllBytes(localManifestFilePath, remoteManifestData);
            int streamingAssetsCount = 0;
            string baseStreamingAssetsUrl = GetUrlDirectoryName(streamingAssetsManifestUrl);

            //统计远程下载和streaingAssets下载文件清单
            foreach (var item in remoteManifestItems)
            {
                string abName = item.Key;
                string abHash = item.Value;

                bool isExclude = false;
                foreach (var g in excludeGroup)
                {
                    if (IsBundleGroup(abName, g))
                    {
                        isExclude = true;
                        break;
                    }
                }
                if (isExclude)
                    continue;

                if (!localManifestItems.ContainsKey(abName))
                {
                    if (streamingAssetsManifestItems.ContainsKey(abName) && streamingAssetsManifestItems[abName] == abHash)
                    {
                        streamingAssetsCount++;
                        if (downloadStreamingAssets)
                        {
                            streamingAssetsDownloadItems[abName] = abHash;
                        }
                    }
                    else
                    {
                        remoteDownloadItems[abName] = abHash;
                    }
                }
            }

            total = remoteDownloadItems.Count + streamingAssetsDownloadItems.Count;


            Debug.Log(LogPrefix + $"asset bundle manifest total: {remoteManifestItems.Count}, download total:{total},  remote:{remoteDownloadItems.Count}, streamingAssets:{streamingAssetsCount}, local:{localManifestItems.Count}");

            if (remoteDownloadItems.Count > 0)
            {
                Debug.Log(LogPrefix + $"download items: {remoteDownloadItems.Count}\n{string.Join("\n", remoteDownloadItems.Keys.ToArray())}");
            }

            if (total > 0)
            {

                DownloadTotal = total;
                DownloadTotalBytes = 0;
                DownloadReceiveBytes = 0;
                DownloadItemReceiveBytes = 0;
                DownloadItemTotalBytes = 0;

                bool isRemote = false;
                Dictionary<string, string> downloadItems;
                string baseDownloadUrl;
                downloadItems = streamingAssetsDownloadItems;
                baseDownloadUrl = GetUrlDirectoryName(streamingAssetsManifestUrl);

                Status = AssetBundleStatus.Downloading;

                OnDownloadStarted?.Invoke(remoteManifestUrl, remoteDownloadItems.Keys.ToArray());
                int lastSpeedReceiveBytes = 0;
                float receiveTime = Time.unscaledTime;
                while (true)
                {

                    Debug.Log(LogPrefix + "download asset bundle base url: " + baseDownloadUrl);
                    string url;
                    foreach (var abName in downloadItems.Keys)
                    {
                        url = baseDownloadUrl + "/" + abName;
                        Debug.Log(LogPrefix + "download asset bundle " + (!isRemote ? "streamingAssets:" : "remote:") + abName);
                        DownloadItemReceiveBytes = 0;
                        DownloadItemTotalBytes = 0;
                        string localPath = localManifestDir + "/" + abName;
                        string tmpLocalPath = GetTempLocalPath(localPath);

                        if (!Directory.Exists(Path.GetDirectoryName(localPath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                        if (File.Exists(tmpLocalPath))
                            File.Delete(tmpLocalPath);

                        DownloadHandlerFile fileHandler = new DownloadHandlerFile(tmpLocalPath);
                        fileHandler.removeFileOnAbort = true;


                        using (UnityWebRequest request = UnityWebRequest.Get(url))
                        {
                            request.downloadHandler = fileHandler;
                            var req = request.SendWebRequest();
                            isFileDone = false;
                            StartDownload2(req).StartCoroutine();
                            int lastItemReceiveLength = 0;
                            //Debug.Log("req: " + req.isDone + ", " + request.isDone + ", " + request.downloadProgress);
                            while (true)
                            {
                                //newrequest.SetRequestHeader("Range", string.Format("bytes={0}-{1}", begin, end));


                                if (DownloadItemTotalBytes == 0)
                                {
                                    string headerValue = request.GetResponseHeader("Content-Length");
                                    //Debug.Log("header:" + headerValue);
                                    if (!string.IsNullOrEmpty(headerValue))
                                    {
                                        int n;
                                        int.TryParse(headerValue, out n);
                                        DownloadItemTotalBytes = n;
                                    }
                                }
                                DownloadItemReceiveBytes = (int)(DownloadItemTotalBytes * request.downloadProgress);

                                //Debug.Log("req2: " + req.isDone + ", " + request.isDone + ", " + request.downloadProgress+ ", isFileDone:" + isFileDone);
                                //Debug.Log("tmp file:" + File.Exists(localPath + ".tmp"));


                                int receiveBytes = DownloadItemReceiveBytes - lastItemReceiveLength;
                                if (receiveBytes > 0)
                                {
                                    lastSpeedReceiveBytes += receiveBytes;
                                    DownloadReceiveBytes += receiveBytes;
                                    lastItemReceiveLength = DownloadItemReceiveBytes;
                                }
                                if (Time.unscaledTime - receiveTime > 0.5f)
                                {
                                    DownloadSpeed = (int)((DownloadSpeed + (lastSpeedReceiveBytes / (Time.unscaledTime - receiveTime))) * 0.5f);
                                    //Debug.Log(abName + " req progress: " + req.progress + ",handler Progress: " + handler.Progress + ",ContentLength " + handler.ContentLength + ",ReceiveLength " + handler.ReceiveLength);
                                    //Debug.Log("DownloadSpeed: " + DownloadSpeed);
                                    receiveTime = Time.unscaledTime;
                                    lastSpeedReceiveBytes = 0;
                                }
                                if (isFileDone)
                                    break;
                                yield return null;
                            }

                            if (!string.IsNullOrEmpty(request.error))
                            {
                                Debug.LogError(LogPrefix + "download error :" + request.error + ", " + url);
                                throw new Exception(request.error);
                            }
                            if (request.isHttpError || request.isNetworkError)
                                throw new Exception("Network error");

                            //byte[] data = request.downloadHandler.data;
                            //if (data == null)
                            //{
                            //    Debug.LogError(LogPrefix + "download error data null  " + url);
                            //    throw new Exception("data null " + abName);
                            //}
                            //string localPath = localManifestDir + "/" + abName;

                            //if (!Directory.Exists(Path.GetDirectoryName(localPath)))
                            //    Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                            //File.WriteAllBytes(localPath, data);

                            request.Dispose();

                            //Debug.Log("tmp file: " + File.Exists(localPath + ".tmp"));
                            if (File.Exists(tmpLocalPath))
                            {
                                if (File.Exists(localPath))
                                    File.Delete(localPath);
                                File.Move(tmpLocalPath, localPath);
                            }

                            DownloadProgress++;
                            OnDownloadProgress?.Invoke(abName);

                        }
                        //Debug.Log(LogPrefix + "download asset bundle " + (!isRemote ? "streamingAssets:" : "remote:") + abName);
                    }

                    if (isRemote)
                        break;
                    downloadItems = remoteDownloadItems;
                    baseDownloadUrl = GetUrlDirectoryName(remoteManifestUrl);
                    isRemote = true;
                }
                OnDownloadCompleted?.Invoke();
            }

            if (latestVersion != null)
            {
                if (latestVersion.groups != null)
                {
                    latestVersion.groups = latestVersion.groups.Where(o => !excludeGroup.Contains(o)).ToArray();
                }

                AssetBundleVersion.Save($"{localManifestDir}/{FormatString(AssetBundleSettings.VersionFile)}", latestVersion);
            }

            ClearDownloadingLock();
            DeleteAllEmptyDirectory(localManifestDir);
            ManifestUrl = localManifestUrl;
        }
        static bool isFileDone;
        static IEnumerator StartDownload2(AsyncOperation a)
        {
            isFileDone = false;
            yield return a;
            isFileDone = true;
        }

        static string GetTempLocalPath(string localPath)
        {
            return localPath + ".tmp";
        }

        static string GetDownloadingLockPath()
        {
            string downloadingLock = LocalManifestDirectory + ".lock";
            return downloadingLock;
        }

        //创建下载锁
        static void CreateDownloadingLock()
        {
            string downloadingLock = GetDownloadingLockPath();
            File.WriteAllBytes(downloadingLock, new byte[0]);
        }

        static void ClearDownloadingLock()
        {
            string downloadingLock = GetDownloadingLockPath();
            if (File.Exists(downloadingLock))
                File.Delete(downloadingLock);
        }
        static bool ExistsDownloadLock()
        {
            return File.Exists(GetDownloadingLockPath());
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

        public static bool IsPreloadedBundle(string bundleName)
        {
            if (!AssetBundleSettings.PreloadEnabled)
                return false;
            return IsMatchIncludeExclude(bundleName, AssetBundleSettings.PreloadInclude, AssetBundleSettings.PreloadExclude);
        }

        public static bool IsMatchIncludeExclude(string input, string includePattern, string excludePattern)
        {
            if (!string.IsNullOrEmpty(includePattern) && !GetOrCacheRegex(includePattern).IsMatch(input))
                return false;

            if (!string.IsNullOrEmpty(excludePattern) && GetOrCacheRegex(excludePattern).IsMatch(input))
                return false;
            return true;
        }

        static Dictionary<string, Regex> cachedRegex;
        public static Regex GetOrCacheRegex(string pattern)
        {
            if (cachedRegex == null)
                cachedRegex = new Dictionary<string, Regex>();
            Regex regex;
            if (!cachedRegex.TryGetValue(pattern, out regex))
            {
                regex = new Regex(pattern, RegexOptions.IgnoreCase);
                cachedRegex[pattern] = regex;
            }
            return regex;
        }

        /// <summary>
        /// 预加载
        /// </summary>
        /// <returns></returns>
        static IEnumerator PreloadAssetBundleAsync()
        {

            PreloadedTotal = 0;
            PreloadedProgress = 0;

            if (Mode == AssetBundleMode.Editor)
            {
                yield break;
            }

            if (!AssetBundleSettings.PreloadEnabled)
            {
                yield break;
            }

            Dictionary<string, bool> preloadItems = new Dictionary<string, bool>();

            try
            {

                if (!string.IsNullOrEmpty(AssetBundleSettings.PreloadInclude))
                {
                    foreach (var bundleName in Manifest.GetAllAssetBundles())
                    {
                        if (!Version.HasGroup(GetBundleGroup(bundleName)))
                            continue;

                        if (IsMatchIncludeExclude(bundleName, AssetBundleSettings.PreloadInclude, AssetBundleSettings.PreloadExclude))
                        {
                            preloadItems[bundleName] = false;
                            var deps = Manifest.GetAllDependencies(bundleName);
                            if (deps != null)
                            {
                                foreach (var dep in deps)
                                {
                                    preloadItems[dep] = false;
                                }
                            }
                        }
                    }
                }

                PreloadedTotal = preloadItems.Count;
                PreloadedProgress = 0;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            if (PreloadedTotal <= 0)
            {
                yield break;
            }

            Status = AssetBundleStatus.Preloading;
            DateTime startTime = DateTime.Now;
            Debug.Log(LogPrefix + $"Preloaded start, total: {PreloadedTotal}\nAssetBundle[\n{string.Join("\n", preloadItems.Select(o => o.Key).ToArray())}\n]");
            OnPreloadStarted?.Invoke(PreloadedTotal);

            Action<string> OnLoad = (bundleName) =>
            {
                if (preloadItems.ContainsKey(bundleName))
                {
                    if (!preloadItems[bundleName])
                    {
                        preloadItems[bundleName] = true;
                        PreloadedProgress++;
                        OnPreloadProgress?.Invoke(PreloadedProgress, bundleName);
                    }
                }
            };
            OnLoadAssetBundleCallback += OnLoad;
            Task task;
            foreach (var abName in preloadItems.Keys.ToArray())
            {
                if (preloadItems.ContainsKey(abName) && preloadItems[abName])
                    continue;
                yield return (task = LoadAssetBundleAsync(abName, typeof(AssetBundles))).Try();

                if (task.Exception != null)
                {
                    Debug.LogError(LogPrefix + $"Preloaded AssetBundle {PreloadedProgress,2}: {abName}, error: {task.Exception.Message}");
                    OnLoadAssetBundleCallback -= OnLoad;
                    Status = AssetBundleStatus.Error;
                    yield break;
                }
                //else
                //{
                //    Debug.Log($"Preloaded AssetBundle {PreloadedProgress,2}: {abName}, ok");
                //}
                PreloadedProgress++;
                OnPreloadProgress?.Invoke(PreloadedProgress, abName);
            }

            Debug.LogFormat(LogPrefix + "Preloaded AssetBundle done, total:{0}, time:{1:0.#}s", PreloadedProgress, (DateTime.Now - startTime).TotalSeconds);
            OnLoadAssetBundleCallback -= OnLoad;
            Status = AssetBundleStatus.Preloaded;
            OnPreloadCompleted?.Invoke();
        }
        public static string[] GetAllDependencies(string bundleName)
        {
            var abInfo = GetAssetBundleInfo(bundleName);
            return abInfo.Dependencies.Select(o => o.Name).ToArray();
        }


        static AndroidJavaClass assetBundleAndroidUtilityClass;

        public static byte[] ReadFileAllBytes(string url)
        {
            byte[] result = null;
            if (Application.platform == RuntimePlatform.Android)
            {
                if (url.StartsWith("jar:file://"))
                {
                    string relativePath = url.Substring(Application.streamingAssetsPath.Length + 1);
                    if (assetBundleAndroidUtilityClass == null)
                        assetBundleAndroidUtilityClass = new AndroidJavaClass("AssetBundleAndroidUtility");

                    sbyte[] bytes = assetBundleAndroidUtilityClass.CallStatic<sbyte[]>("ReadStreamingAssetsAllBytes", relativePath);
                    byte[] buff = new byte[bytes.Length];
                    Buffer.BlockCopy(bytes, 0, buff, 0, bytes.Length);
                    result = buff; 
                }
            }

            if (result == null)
            {
                if (url.StartsWith("file://"))
                    result = File.ReadAllBytes(url.Substring(7));
                else
                    result = File.ReadAllBytes(url);
            }
            return result;
        }

        public static bool ExistsFile(string url)
        {
            bool? result = null;
            if (Application.platform == RuntimePlatform.Android)
            {
                if (url.StartsWith("jar:file://"))
                {
                    string relativePath = url.Substring(Application.streamingAssetsPath.Length + 1);
                    if (assetBundleAndroidUtilityClass == null)
                        assetBundleAndroidUtilityClass = new AndroidJavaClass("AssetBundleAndroidUtility");

                    result = assetBundleAndroidUtilityClass.CallStatic<bool>("ExistsStreamingAssetsFile", relativePath);

                }
            }

            if (result == null)
            {
                if (url.StartsWith("file://"))
                    result = File.Exists(url.Substring(7));
                else
                    result = File.Exists(url);
            }
            Debug.Log("exists file :" + url + ", " + result.Value);
            return result.Value;
        }

        public static Stream FileOpenStream(string url)
        {
            Stream result = null;
            if (Application.platform == RuntimePlatform.Android)
            {
                if (url.StartsWith("jar:file://", StringComparison.InvariantCultureIgnoreCase))
                {
                    string relativePath = url.Substring(Application.streamingAssetsPath.Length + 1);
                    if (assetBundleAndroidUtilityClass == null)
                        assetBundleAndroidUtilityClass = new AndroidJavaClass("AssetBundleAndroidUtility");

                    byte[] bytes = assetBundleAndroidUtilityClass.CallStatic<byte[]>("ReadStreamingAssetsAllBytes", relativePath);
                    result = new MemoryStream(bytes);
                }
            }

            if (result == null)
                result = new FileStream(url, FileMode.Open);
            return result;
        }


        public static string GetManifestPathWithVersion(string basePath, AssetBundleVersion version)
        {
            string path;
            Dictionary<string, object> values = null;
            GetFormatArgsWithVersion(ref values, version);
            path = AssetBundleSettings.DownloadManifestPath.FormatStringWithKey(values);
            path = UrlCombine(basePath, path);
            return path;
        }

        public static Dictionary<string, object> GetFormatArgs()
        {
            Dictionary<string, object> args = new Dictionary<string, object>();

            args[FormatArg_Platform] = PlatformName;
            args[FormatArg_BundleCode] = 1;
            args[FormatArg_AppVersion] = AppVersion;
            args[FormatArg_Channel] = AssetBundleSettings.Channel;
            args[FormatArg_BundleVersion] = AssetBundleSettings.BundleVersion;
            return args;
        }

        public static void GetFormatArgsWithVersion(ref Dictionary<string, object> args, AssetBundleVersion version)
        {
            if (args == null)
                args = new Dictionary<string, object>();
            if (version == null)
            {
                return;
            }

            args[FormatArg_Platform] = version.platform;
            args[FormatArg_BundleCode] = version.bundleCode;
            args[FormatArg_AppVersion] = version.appVersion;
            args[FormatArg_Channel] = version.channel;
            args[FormatArg_BundleVersion] = version.bundleVersion;
        }


        public static string FormatString(string format, Dictionary<string, object> values = null)
        {
            if (string.IsNullOrEmpty(format))
                return format;
            return format.FormatStringWithKey(GetFormatArgs());
        }

        public static Task<AssetBundleVersion> GetLocalVersion()
        {
            return Task.Run<AssetBundleVersion>(_GetLocalVersion());
        }

        static IEnumerator _GetLocalVersion()
        {
            Dictionary<string, AssetBundleVersion> manifestUrls = new Dictionary<string, AssetBundleVersion>();
            manifestUrls.Add(LocalManifestUrl, null);
            manifestUrls.Add(StreamingAssetsManifestUrl, null);
            var taskLatest = AssetBundleVersion.GetLatest(manifestUrls);
            yield return taskLatest;
            yield return new YieldReturn(taskLatest.Result[0].Value);
        }

        public static IEnumerator EnsureLocalVersion()
        {
            string localManifestDirectory = LocalManifestDirectory;
            string localVersionFile = $"{localManifestDirectory}/{FormatString(AssetBundleSettings.VersionFile)}";
            if (File.Exists(localVersionFile))
                yield break;
            string localManifestFilePath = localManifestDirectory + "/" + PlatformName;
            if (File.Exists(localManifestFilePath))
                yield break;

            if (ExistsDownloadLock())
                yield break;

            if (!Directory.Exists(localManifestDirectory))
                Directory.CreateDirectory(localManifestDirectory);

            using (UnityWebRequest request = UnityWebRequest.Get(UrlCombine(GetUrlDirectoryName(StreamingAssetsManifestUrl), FormatString(AssetBundleSettings.VersionFile))))
            {
                yield return request.SendWebRequest();
                if (request.error != null)
                    throw new Exception(request.error);
                File.WriteAllBytes(localVersionFile, request.downloadHandler.data);
            }

            if (!File.Exists(localManifestFilePath))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(StreamingAssetsManifestUrl))
                {
                    yield return request.SendWebRequest();
                    if (request.error != null)
                        throw new Exception(request.error);
                    File.WriteAllBytes(localManifestFilePath, request.downloadHandler.data);
                }
            }
        }

        public static float GetBytesUnit(int bytes, out string unit)
        {
            float result = 0;

            if (bytes < 512)
            {
                result = bytes;
                unit = "B";
            }
            else
            {
                result = bytes / 1024f;
                if (result < 512)
                {
                    unit = "KB";
                }
                else
                {
                    result /= 1024;
                    if (result < 512)
                    {
                        unit = "MB";
                    }
                    else
                    {
                        result /= 1024;
                        if (result < 512)
                        {
                            unit = "GB";
                        }
                        else
                        {
                            result /= 1024;
                            unit = "TB";
                        }
                    }
                }
            }
            return result;
        }

        public static float BytesToUnit(int bytes, string unit)
        {
            unit = unit.ToLower();
            float result;
            switch (unit)
            {
                case "b":
                    result = bytes;
                    break;
                case "kb":
                    result = bytes / 1024f;
                    break;
                case "mb":
                    result = bytes / Mathf.Pow(1024, 2);
                    break;
                case "gb":
                    result = bytes / Mathf.Pow(1024, 3);
                    break;
                case "tb":
                    result = bytes / Mathf.Pow(1024, 4);
                    break;
                default:
                    result = bytes;
                    break;
            }
            return result;
        }


        public class AssetBundleRef
        {
            internal AssetBundle assetBundle;
            internal AssetBundleInfo abInfo;
            internal Exception exception;
            internal LinkedList<WeakReference> weakRefs = new LinkedList<WeakReference>();

            public AssetBundleInfo AssetBundleInfo
            {
                get { return abInfo; }
            }

            public AssetBundle AssetBundle
            {
                get { return assetBundle; }
            }

            public Exception Error
            {
                get { return exception; }
            }

            public IEnumerable<WeakReference> Owners
            {
                get { return weakRefs; }
            }


            public AssetBundleRef(AssetBundleInfo abInfo, AssetBundle assetBundle)
            {
                this.abInfo = abInfo;
                this.assetBundle = assetBundle;
            }

            public void Unload(bool allObjects)
            {
                weakRefs.Clear();

                if (assetBundle != null)
                {
                    assetBundle.Unload(allObjects);
                }
                assetBundle = null;
            }
            public bool IsDependent
            {
                get
                {
                    bool isDependent = false;
                    var current = weakRefs.First;
                    while (current != null)
                    {
                        if (current.Value.Target == null)
                        {
                            var tmp = current.Next;
                            current.List.Remove(current);
                            current = tmp;
                            continue;
                        }
                        if (current.Value.Target != null)
                        {
                            isDependent = true;
                            break;
                        }
                        current = current.Next;
                    }
                    return isDependent;
                }
            }
            public void AddDependent(object owner)
            {
                var item = FindDependent(owner);
                if (item != null)
                    return;
                weakRefs.AddLast(new WeakReference(owner));
            }
            public void RemoveDependent(object owner)
            {
                var item = FindDependent(owner);
                if (item == null)
                    return;
                item.List.Remove(item);
            }
            private LinkedListNode<WeakReference> FindDependent(object owner)
            {
                var current = weakRefs.First;
                while (current != null)
                {
                    if (current.Value.Target == null)
                    {
                        var tmp = current.Next;
                        current.List.Remove(current);
                        current = tmp;
                        continue;
                    }
                    if (current.Value.Target == owner)
                        break;
                    current = current.Next;
                }
                return current;
            }

        }


        public class PackageManifest
        {
            internal string name;
            internal AssetBundleManifest manifest;
            internal Dictionary<string, AssetBundleInfo> assetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            /// <summary>
            /// BaseName > Variant > AssetBundleName
            /// </summary>
            internal Dictionary<string, Dictionary<string, string>> allAssetBundlesWithVariantSet;

            public string Name
            {
                get { return name; }
            }

            public AssetBundleManifest Manifest
            {
                get { return manifest; }
            }
            public IEnumerable<AssetBundleInfo> AssetBundleInfos
            {
                get { return assetBundleInfos.Values; }
            }


            public AssetBundleInfo GetAssetBundleInfo(string assetBundleName)
            {
                AssetBundleInfo info;
                if (!assetBundleInfos.TryGetValue(assetBundleName, out info))
                    return null;
                return info;
            }

        }

        public class AssetBundleInfo
        {
            internal string name;
            internal Hash128 hash;
            internal string url;
            internal AssetBundleKey key;
            internal AssetBundleInfo[] allDependencies;
            internal bool isVariant;


            /// <summary>
            /// 是否加密
            /// </summary>
            internal bool isCrypto;
            /// <summary>
            /// 是否签名
            /// </summary>
            internal bool isSignature;


            public string Name
            {
                get { return name; }
            }

            public Hash128 Hash
            {
                get { return hash; }
            }

            public AssetBundleKey Key
            {
                get { return key; }
            }

            public IEnumerable<AssetBundleInfo> Dependencies
            {
                get { return allDependencies == null ? new AssetBundleInfo[0] : allDependencies; }
            }

            public override string ToString()
            {
                return string.Format("{0}", name);
            }
        }


        #region Addressable


        public static string AddressableBundleName = "addressable";
        public static string AddressableAssetPath = "Assets/addressable.asset";
        static Dictionary<string, AssetBundleAddressableAsset.AssetInfo> assetName2Bundle;

#if UNITY_EDITOR
        static AssetBundleAddressableAsset addressableAsset;
        static AssetBundleAddressableAsset AddressableAsset
        {
            get
            {
                if (!addressableAsset)
                {
                    string addressableBundleAssetPath = AssetBundles.AddressableAssetPath;
                    addressableAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<AssetBundleAddressableAsset>(addressableBundleAssetPath);
                }
                return addressableAsset;
            }
        }
#endif

        static IEnumerator LoadAddressable()
        {
            assetName2Bundle = new Dictionary<string, AssetBundleAddressableAsset.AssetInfo>(StringComparer.InvariantCultureIgnoreCase);

            if (string.IsNullOrEmpty(AddressableBundleName))
                yield break;

            AssetBundleAddressableAsset asset = null;

            if (Mode != AssetBundleMode.Editor)
            {

                var result = LoadAssetBundleAsync(AddressableBundleName);
                yield return result;
                AssetBundle ab = result.Result;
                if (!ab)
                {
                    Debug.LogError(LogPrefix + "Addressable AssetBundle load fail");
                    yield break;
                }
                asset = ab.LoadAllAssets<AssetBundleAddressableAsset>().FirstOrDefault();
            }
            else
            {
#if UNITY_EDITOR
                asset = AddressableAsset;
#endif
            }

            if (!asset)
            {
                Debug.LogError(LogPrefix + "AssetBundleAddressableAsset load fail");
                yield break;
            }
            char[] spr = new char[] { '/', '\\' };

            foreach (var assetInfo in asset.assets)
            {
                var item = new AssetBundleAddressableAsset.AssetInfo()
                {
                    guid = assetInfo.guid,
                    bundleName = assetInfo.bundleName,
                    assetName = assetInfo.assetName,
                };
                assetName2Bundle[assetInfo.assetName] = item;

                // Directory/GetFileNameWithoutExtension
                int index = assetInfo.assetName.LastIndexOfAny(spr);
                if (index >= 0)
                {
                    string filename = assetInfo.assetName.Substring(index + 1);
                    int index2 = filename.LastIndexOf('.');
                    if (index2 >= 0)
                    {
                        assetName2Bundle[assetInfo.assetName.Substring(0, index) + '/' + filename.Substring(0, index2)] = item;
                    }
                    assetName2Bundle[assetInfo.guid] = item;
                }
            }
        }

        public static Func<string, bool> ResolveBundleName;
        public static string GetBundleName(string assetNameOrGuid)
        {
            string addressableName;
            return GetBundleName(assetNameOrGuid, out addressableName);
        }

        public static string GetBundleName(string assetNameOrGuid, out string assetName)
        {
            AssetBundleAddressableAsset.AssetInfo assetInfo = null;
            assetName = null;
     
            assetName2Bundle.TryGetValue(assetNameOrGuid, out assetInfo);

            if (assetInfo != null)
            {
                assetName = assetInfo.assetName;
                return assetInfo.bundleName;
            }
            else
            {
                Debug.LogError("not exists asset name: " + assetNameOrGuid);
            }
            return null;
        }

        #endregion

        #region 新的资源加载接口


        public static Object LoadAsset(string assetName, Type type = null, object owner = null)
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            if (bundleName == null)
                return null;
            return LoadAsset(bundleName, assetName, type, owner);
        }

        public static T LoadAsset<T>(string assetName, object owner = null)
               where T : UnityEngine.Object
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            if (bundleName == null)
                return default(T);
            return LoadAsset<T>(bundleName, assetName, owner);
        }

        public static Task<Object> LoadAssetAsync(string assetName, Type type = null, object owner = null)
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            return LoadAssetAsync(bundleName, assetName, type, owner);
        }

        public static Task<T> LoadAssetAsync<T>(string assetName, object owner = null)
               where T : UnityEngine.Object
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            return LoadAssetAsync<T>(bundleName, assetName, owner);
        }

        public static GameObject Instantiate(string assetName, object owner = null)
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            if (bundleName == null)
                return null;
            return Instantiate(bundleName, assetName, owner);
        }

        public static GameObject Instantiate(string assetName, Transform parent, object owner = null)
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            if (bundleName == null)
                return null;
            return Instantiate(bundleName, assetName, parent, owner);
        }

        public static T Instantiate<T>(string assetName, object owner = null)
           where T : UnityEngine.Object
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            if (bundleName == null)
                return default(T);
            return Instantiate<T>(bundleName, assetName, owner);
        }

        public static Task<T> InstantiateAsync<T>(string assetName, object owner = null)
               where T : UnityEngine.Object
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            return InstantiateAsync<T>(bundleName, assetName, owner);
        }

        public static Task<GameObject> InstantiateAsync(string assetName, object owner = null)
        {
            string bundleName;
            bundleName = GetBundleName(assetName, out assetName);
            return InstantiateAsync(bundleName, assetName, owner);
        }

        #endregion


        #region 资源分组


        public static bool IsBundleGroup(string bundleName, string groupName)
        {
            return bundleName.StartsWith(groupName) && bundleName.Length > groupName.Length && (bundleName[groupName.Length] == '/' || bundleName[groupName.Length] == '\\');
        }
        /// <summary>
        /// 默认 'local' 组
        /// </summary>
        /// <param name="bundleName"></param>
        /// <returns></returns>
        public static string GetBundleGroup(string bundleName)
        {
            int index = bundleName.IndexOf('/');
            if (index >= 0)
                return bundleName.Substring(0, index);
            return AssetBundleSettings.LocalGroupName;
        }

        #endregion

    }



    public struct AssetBundleKey : IEquatable<AssetBundleKey>
    {
        public AssetBundleKey(string name, Hash128 hash)
        {
            this.Name = name;
            this.Hash = hash;
        }

        public string Name;
        public Hash128 Hash;

        public bool Equals(AssetBundleKey other)
        {
            return this.Name == other.Name && this.Hash == other.Hash;
        }
    }


}