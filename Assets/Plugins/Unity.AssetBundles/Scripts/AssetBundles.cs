using Coroutines;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Networking;

namespace UnityEngine
{


    /// <summary>
    /// AssetBundle管理器
    /// </summary>
    public sealed class AssetBundles
    {
        public static List<string> Variants = new List<string>();

        /// <summary>
        /// 下载超时时间，毫秒
        /// </summary>
        public static int DOWNLOAD_TIMEOUT = 15 * 1000;
        private readonly static Object[] EmptyObjects = new Object[0];

        public delegate void DownloadCompletedDelegate(string assetbundleName, AssetBundle assetBundle);
        public delegate void DownloadFailDelegate(string assetbundleName, Exception error);

        ///// <summary>
        ///// 下载完成时回调
        ///// </summary>
        public static DownloadCompletedDelegate OnDownloadCompleted;

        ///// <summary>
        ///// 下载失败回调
        ///// </summary>
        public static DownloadFailDelegate OnDownloadFail;

        public static Dictionary<AssetBundleKey, AssetBundleRef> abRefs;

        private static SyncCoroutine syncObj = new SyncCoroutine();
        private static Dictionary<string, PackageManifest> manifests = new Dictionary<string, PackageManifest>();
        public static PackageManifest mainManifest;

        static Dictionary<string, string[]> assetBundleNameAndVariants = new Dictionary<string, string[]>();

        private static List<AssetBundleRef> tmps = new List<AssetBundleRef>();

        internal const string LogTag = "AssetBundles";
        internal static ILogger Logger = new LogExtension.LoggerExtension();

        private const string PlayerPrefsKeyPrefix = "unity.assetbundles.";


        public static LoadAssetHandlerDelegate LoadAssetHandler;
        public static LoadAssetHandlerAsyncDelegate LoadAssetAsyncHandler;

        public delegate Object[] LoadAssetHandlerDelegate(string assetBundleName, string assetName, Type type, object owner);
        public delegate Task<Object[]> LoadAssetHandlerAsyncDelegate(string assetBundleName, string assetName, Type type, object owner);

        public static AssetBundleManifest Manifest { get { return mainManifest != null ? mainManifest.manifest : null; } }

        public static bool IsEditorAssetsMode
        {
            get { return Application.isEditor && PlayerPrefs.GetInt(PlayerPrefsKeyPrefix + "IsEditorAssetsMode", 1) != 0; }
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

        private static Object[] AssetBundleLoadAssetHandler(string assetBundleName, string assetName, Type type, object owner)
        {
            if (assetBundleName == null)
                throw new ArgumentNullException("assetBundleName");

            var assetBundle = LoadAssetBundle(assetBundleName, owner);

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
        /// 下载资源清单文件
        /// </summary>
        /// <param name="manifestUrl"></param>
        /// <returns></returns>
        public static Task<AssetBundleManifest> DownloadManifestAsync(string manifestUrl)
        {
            return Task.Run<AssetBundleManifest>(StartDownloadAssetBundleManifest(manifestUrl)); ;
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
        }

        public static AssetBundle LoadAssetBundle(string assetBundleName, object owner = null)
        {
            if (assetBundleName == null)
                throw new ArgumentNullException("assetbundleName");

            var abInfo = GetAssetBundleInfo(assetBundleName);

            AssetBundle assetBundle;
            if (!TryGetAssetbundle(abInfo, owner, out assetBundle))
            {
                throw new Exception("assetBoundle not preloaded, name:" + assetBundleName);
            }

            return assetBundle;
        }

        public static Object LoadAsset(string assetBundleName, string assetName, Type type = null, object owner = null)
        {

            Object[] result;
            if (LoadAssetHandler != null)
                result = LoadAssetHandler(assetBundleName, assetName, type, owner);
            else
                result = AssetBundleLoadAssetHandler(assetBundleName, assetName, type, owner);

            if (result.Length == 0)
            {
                if (!string.IsNullOrEmpty(assetName))
                    Debug.unityLogger.LogError(LogTag, string.Format("load asset bundle fail. assetBundleName: {0}, assetName: {1}, type: {2}", assetBundleName, assetName, type));
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




        public static Object[] LoadAllAssets(string assetBundleName, Type type = null, object owner = null)
        {
            Object[] result;
            if (LoadAssetHandler != null)
                result = LoadAssetHandler(assetBundleName, null, type, owner);
            else
                result = AssetBundleLoadAssetHandler(assetBundleName, null, type, owner);
            return result;
        }
        public static T[] LoadAllAssets<T>(string assetBundleName, object owner = null)
            where T : Object
        {
            return LoadAllAssets(assetBundleName, typeof(Object), owner).Cast<T>().ToArray();
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
        public static Task<AssetBundle> LoadAssetBundleAsync(string assetBundleName, object owner, DownloadCompletedDelegate onCompleted, DownloadFailDelegate onFaulted = null)
        {
            var task = LoadAssetBundleAsync(assetBundleName, owner);
            task.ContinueWith((t) =>
            {
                if (task.IsRanToCompletion)
                {
                    if (onCompleted != null)
                        onCompleted(assetBundleName, task.Result);
                }
                else
                {
                    if (onFaulted != null)
                        onFaulted(assetBundleName, task.Exception);
                }
            });
            return task;
        }
        public static Task<AssetBundle> LoadAssetBundleAsync(string[] assetBundleAndAssetNames, object owner, DownloadCompletedDelegate onCompleted, DownloadFailDelegate onFaulted = null)
        {
            return LoadAssetBundleAsync(assetBundleAndAssetNames[0], owner, onCompleted, onFaulted);
        }
        public static Task<Object> LoadAssetAsync(string assetBundleName, string assetName, Type type = null, object owner = null)
        {
            Task<Object[]> result;
            if (LoadAssetAsyncHandler != null)
                result = LoadAssetAsyncHandler(assetBundleName, assetName, type, owner);
            else
                result = AssetBundleLoadAssetsAsyncHandler(assetBundleName, assetName, type, owner);
            return result
                .ContinueWith(t =>
                {
                    if (t.Result.Length == 0)
                    {
                        Debug.unityLogger.LogError(LogTag, string.Format("load asset bundle fail. assetBundleName: {0}, assetName: {1}, type:{2}", assetBundleName, assetName, type));
                        return null;
                    }
                    return t.Result[0];
                });
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

        public static Task<Object[]> LoadAllAssetsAsync(string assetBundleName, Type type = null, object owner = null)
        {
            Task<Object[]> result;
            if (LoadAssetAsyncHandler != null)
                result = LoadAssetAsyncHandler(assetBundleName, null, type, owner);
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

        public static AssetBundleInfo GetAssetBundleInfo(string assetBundleName)
        {
            AssetBundleInfo abInfo = null;
            if (mainManifest == null)
            {
                Debug.unityLogger.LogError(LogTag, "manifest not loaded");
                return null;
            }

            assetBundleName = ResolveAssetBundleNameVariant(assetBundleName);

            mainManifest.assetBundleInfos.TryGetValue(assetBundleName, out abInfo);

            if (abInfo == null)
            {
                Debug.unityLogger.LogError(LogTag, "not assetBundleName:" + assetBundleName);
            }
            return abInfo;
        }


        static bool TryGetAssetbundle(AssetBundleInfo abInfo, object owner, out AssetBundle assetbundle)
        {
            AssetBundleRef abRef;

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

        static IEnumerator StartDownloadAssetBundleManifest(string manifestUrl)
        {
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


            PackageManifest data;

            if (manifests.TryGetValue(packageName, out data))
            {
                yield return new YieldReturn(data.manifest);
            }

            AssetBundle assetbundle = null;

            using (var l = syncObj.Lock())
            {
                yield return l;

                if (manifests.TryGetValue(packageName, out data))
                {
                    l.Dispose();
                    yield return new YieldReturn(data.manifest);
                }


                if (Logger.logEnabled)
                    Logger.Log(LogTag, string.Format("download assetbundle manifest: {0}, packageName:{1}", manifestUrl, packageName));

                using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(manifestUrl))
                {
                    yield return request.SendWebRequest();
                    if (request.error != null)
                    {
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
                throw new Exception("manifest null");


            data = new PackageManifest();
            if (AssetBundles.mainManifest == null)
                AssetBundles.mainManifest = data;
            data.name = packageName;
            data.manifest = manifest;

            var allAssetBundlesWithVariantSet = new Dictionary<string, Dictionary<string, string>>();

            var allAssetBundlesWithVariant = manifest.GetAllAssetBundlesWithVariant();
            for (int i = 0; i < allAssetBundlesWithVariant.Length; i++)
            {
                var tmp = ParseAssetBundleNameAndVariant(allAssetBundlesWithVariant[i]);
                if (!string.IsNullOrEmpty(tmp[1]))
                {
                    Dictionary<string, string> varintToName;
                    if (!allAssetBundlesWithVariantSet.TryGetValue(tmp[0], out varintToName))
                    {
                        varintToName = new Dictionary<string, string>();
                        allAssetBundlesWithVariantSet[tmp[0]] = varintToName;
                    }
                    varintToName[tmp[1]] = allAssetBundlesWithVariant[i];
                }
            }
            data.allAssetBundlesWithVariantSet = allAssetBundlesWithVariantSet;

            foreach (var assetbundleName in manifest.GetAllAssetBundles())
            {
                AssetBundleInfo abInfo = new AssetBundleInfo();
                abInfo.name = assetbundleName;

                abInfo.hash = manifest.GetAssetBundleHash(assetbundleName);
                abInfo.key = new AssetBundleKey(abInfo.name, abInfo.hash);
                abInfo.url = string.Format(abUrlTemplate, abInfo.name.Replace("/", "\\"));
                data.assetBundleInfos[assetbundleName] = abInfo;
            }

            OnVariantChanged(data);


            manifests[packageName] = data;

            manifest = null;
            assetbundle.Unload(false);

            yield return new YieldReturn(data.manifest);
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



        static string[] ParseAssetBundleNameAndVariant(string assetBundleName)
        {
            string[] nameAndVariant;
            if (!assetBundleNameAndVariants.TryGetValue(assetBundleName, out nameAndVariant))
            {
                string[] parts = assetBundleName.Split(new char[] { '.' }, 2, StringSplitOptions.None);
                if (parts.Length == 1)
                    nameAndVariant = new string[] { assetBundleName, "" };
                else
                    nameAndVariant = parts;
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
                        Logger.LogWarning(LogTag, string.Format("not found match variant. first variant: {0}, assetBundle:{1}", variantSet.Values.First(), assetBundleName));
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


                if (Logger.logEnabled)
                    Logger.Log(LogTag, string.Format("download assetbundle, name:{0}, url:{1}", abInfo.name, abInfo.url));
                using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(abInfo.url, abInfo.hash, 0))
                //using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(abInfo.url, new CachedAssetBundle(abInfo.name+"_test", abInfo.hash)))
                {
                    yield return request.SendWebRequest();
                    if (request.error != null)
                    {
                        throw new Exception(request.error);
                    }
                    assetBundle = (request.downloadHandler as DownloadHandlerAssetBundle).assetBundle;

                }

                if (assetBundle == null)
                {
                    abRef = new AssetBundleRef(abInfo, null);
                    abRef.exception = new Exception(" assetbundle null");
                    abRefs[abInfo.key] = abRef;
                    if (OnDownloadFail != null)
                        OnDownloadFail(abInfo.url, abRef.exception);

                    throw abRef.exception;
                }

                abRef = new AssetBundleRef(abInfo, assetBundle);
                if (owner != null)
                    abRef.AddDependent(owner);
                abRefs[abInfo.key] = abRef;

                if (OnDownloadCompleted != null)
                    OnDownloadCompleted(abInfo.url, abRef.assetBundle);

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
                            Logger.Log(LogTag, string.Format("Unload unused assetbundle: {0}, allObjects:{1}", abRef.abInfo.name, allObjects));
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
                            Logger.Log(LogTag, string.Format("extra assetbundle {0}\n{1}", assetBundleName, assetBundlePath));
                        else
                            Logger.LogWarning(LogTag, string.Format("extra assetbundle empty files. {0}", assetBundleName));
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