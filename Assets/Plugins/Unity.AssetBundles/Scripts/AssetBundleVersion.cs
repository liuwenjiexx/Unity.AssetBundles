using Coroutines;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;
using System.IO;

namespace UnityEngine
{
    /// <summary>
    /// 使用该配置文件能更快更省流量判断是否需要更新
    /// </summary>
    [Serializable]
    public class AssetBundleVersion : ISerializationCallbackReceiver, IEquatable<AssetBundleVersion>
    {
        /// <summary>
        /// 生成平台，空匹配任何平台
        /// </summary>
        public string platform;
        
        /// <summary>
        /// 资源版本号
        /// </summary>
        public int bundleCode;
        /// <summary>
        /// 生成时应用版本, 资源包需要的最小应用程序版本号，如果应用程序低于该版本号则需要先升级app提高版本号才能下载，如果为空或(0.0.0)匹配任何版本
        /// </summary>
        public string appVersion;

        /// <summary>
        /// 使用string类型，unity 不支持 long 64位序列化
        /// </summary>        
        [SerializeField]
        private int timestamp;

        /// <summary>
        /// 重定向清单 url
        /// </summary>
        public string redirectUrl;

        /// <summary>
        /// 资源版本号
        /// </summary>
        public string bundleVersion;

        /// <summary>
        /// 渠道
        /// </summary>
        public string channel;

        /// <summary>
        /// 资源包时间戳，服务端时间戳值大于本地才下载
        /// </summary> 
        public DateTime Timestamp
        {
            get
            {
                return timestamp.FromUtcSeconds();
            }
            set
            {
                timestamp = value.ToUtcSeconds();
            }
        }

        /// <summary>
        /// 清单文件Hash，如果清单Hash值相等则不下载，            
        /// 在多节点网络分发时可能同时存在多个Hash值不同的版本，不能确定哪个是最新的，所以需要<see cref="Timestamp"/>来判断
        /// </summary>
        public string hash;

        /// <summary>
        /// git 提交 id
        /// </summary>
        public string commitId;

        [SerializeField]
        public string userData;

        /// <summary>
        /// 资源包组列表
        /// </summary>
        public string[] groups;

        static Encoding Encoding
        {
            get => new UTF8Encoding(false);
        }

        public static AssetBundleVersion Parse(byte[] data)
        {
            string json = Encoding.GetString(data);
            return LoadFromJson(json);
        }

        public static AssetBundleVersion LoadFromJson(string json)
        {
            var result = JsonUtility.FromJson<AssetBundleVersion>(json);
            return result;
        }
        public static AssetBundleVersion LoadFromFile(string path)
        {
            if (!File.Exists(path))
                return null;
            string json = File.ReadAllText(path, Encoding);
            var result = JsonUtility.FromJson<AssetBundleVersion>(json);
            return result;
        }

        public static void Save(string path, AssetBundleVersion version)
        {
            byte[] data;
            string json = JsonUtility.ToJson(version, true);
            data = Encoding.GetBytes(json);
            File.WriteAllBytes(path, data);
        }

        public static byte[] ToBytes(AssetBundleVersion version)
        {
            byte[] data;
            string json = JsonUtility.ToJson(version, true);
            data = Encoding.GetBytes(json);
            return data;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
        }

        /// <summary>
        /// 比较版本是否最新，需要更新返回true
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool IsLatest(AssetBundleVersion other)
        {
            if (other == null)
                return true;
            if (timestamp >= other.timestamp)
                return true;
            return false;
        }

        public bool HasGroup(string group)
        {
            if (string.IsNullOrEmpty(group))
                return false;
            if (groups == null)
                return false;
            return groups.Contains(group);
        }

        /// <summary>
        /// 是否包含在 bundle 组
        /// </summary>
        /// <param name="bundleName"></param>
        /// <returns></returns>
        public bool HasBundle(string bundleName)
        {
            return HasGroup(AssetBundles.GetBundleGroup(bundleName));
        }

        public override string ToString()
        {
            return $"{appVersion}-{bundleVersion}-{bundleCode}";
        }

        /// <summary>
        /// 返回一个最新的清单地址，如果所有版本文件无法下载则返回空
        /// </summary>
        /// <param name="manifestUrls"></param>
        /// <returns></returns>
        public static Task<KeyValuePair<string, AssetBundleVersion>[]> GetLatest(Dictionary<string, AssetBundleVersion> manifestUrls)
        {
            return Task.Run<KeyValuePair<string, AssetBundleVersion>[]>(_GetLatest(manifestUrls));
        }
        static IEnumerator _GetLatest(Dictionary<string, AssetBundleVersion> manifestUrls)
        {

            AssetBundleVersion ver1 = null;
            Task<AssetBundleVersion> task;
            List<KeyValuePair<string, AssetBundleVersion>> list = new List<KeyValuePair<string, AssetBundleVersion>>();

            foreach (var item in manifestUrls)
            {
                string manifestUrl = item.Key;
                if (string.IsNullOrEmpty(manifestUrl))
                    continue;
                ver1 = item.Value;
                if (ver1 == null)
                {
                    task = Load(manifestUrl);
                    yield return task.Try();
                    ver1 = null;
                    if (task.IsRanToCompletion)
                    {
                        ver1 = task.Result;
                    }
                }
                list.Add(new KeyValuePair<string, AssetBundleVersion>(manifestUrl, ver1));
            }
            var result = list.OrderByDescending(o => o.Value == null ? 0 : o.Value.timestamp).ToArray();

            yield return new YieldReturn(result);
        }


        public static Task<AssetBundleVersion> Load(string manifestUrl)
        {
            return Task.Run<AssetBundleVersion>(_LoadFromManifestUrl(AssetBundles.GetVersionUrl(manifestUrl)));
        }


        static IEnumerator _LoadFromManifestUrl(string url)
        {
            AssetBundleVersion result = null;
            byte[] data;
            //Debug.Log(AssetBundles.LogPrefix + "load manifest version: " + url);

            if (url.StartsWith("file://", StringComparison.InvariantCultureIgnoreCase))
            {
                string path = url.Substring(7);
                if (!File.Exists(path))
                    yield return new YieldReturn(null);
                data = File.ReadAllBytes(path);
            }
            else
            {

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    yield return request.SendWebRequest();
                    if (request.error != null)
                    {
                        //Debug.LogWarning(AssetBundles.LogPrefix + $"load manifest version fail :{request.error}. url:{url}");
                        yield return new YieldReturn(null);
                    }
                    data = request.downloadHandler.data;

                    //if (data == null || data.Length == 0)
                    //    Debug.LogError(AssetBundles.LogPrefix + "data null");
                }
            }
            try
            {
                result = Parse(data);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            yield return new YieldReturn(result);
        }




        public static AssetBundleVersion[] LoadVersionListFromJson(string json)
        {
            return new AssetBundleVersion[0];
            //var list = ArrayWrap<AssetBundleManifestVersion>.FromJson(json);
            //return list;

        }
        public static AssetBundleVersion[] LoadVersionList(string path)
        {
            string json;
            if (!File.Exists(path))
                return new AssetBundleVersion[0];
            json = Encoding.GetString(File.ReadAllBytes(path));
            if (string.IsNullOrEmpty(json))
                return new AssetBundleVersion[0];
            return LoadVersionListFromJson(json);
        }

        public static void Save(string path, AssetBundleVersion[] list)
        {
            string json = ArrayWrap<AssetBundleVersion>.ToJson(list, true);
            File.WriteAllBytes(path, Encoding.GetBytes(json));
        }



        public static Task<AssetBundleVersion> DownloadRemoteVersion(string downloadAssetBundlesUrl)
        {
            return Task.Run<AssetBundleVersion>(_DownloadVersionList(AssetBundles.UrlCombine(downloadAssetBundlesUrl, AssetBundles.FormatString(AssetBundleSettings.DownloadVersionFile))));
        }


        static IEnumerator _DownloadVersionList(string url)
        {
            AssetBundleVersion result = null;
            byte[] data;
            Debug.Log(AssetBundles.LogPrefix + "load manifest version list: " + url);

            if (url.StartsWith("file://", StringComparison.InvariantCultureIgnoreCase))
            {
                string path = url.Substring(7);
                if (!File.Exists(path))
                    yield return new YieldReturn(null);
                data = File.ReadAllBytes(path);
            }
            else
            {

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    yield return request.SendWebRequest();
                    if (request.error != null)
                    {
                        Debug.LogError(AssetBundles.LogPrefix + $"load manifest version list fail :{request.error}. url:{url}");
                        yield return new YieldReturn(null);
                    }
                    data = request.downloadHandler.data;

                    if (data == null || data.Length == 0)
                        Debug.LogError(AssetBundles.LogPrefix + "data null");
                }
            }
            try
            {
                result = Parse(data);
                Debug.Log(Encoding.UTF8.GetString(data));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            yield return new YieldReturn(result);
        }



        [Serializable]
        class ArrayWrap<T>
        {
            public T[] array;

            public static T[] FromJson(string json)
            {
                if (string.IsNullOrEmpty(json))
                    return null;
                return JsonUtility.FromJson<ArrayWrap<T>>("{\"array\":" + json + "}").array;
            }

            public static string ToJson(T[] array, bool prettyPrint = false)
            {
                string json = JsonUtility.ToJson(new ArrayWrap<T>() { array = array }, prettyPrint);
                int index = -1;
                index = json.IndexOf('[');
                if (index > 0)
                {
                    json = json.Substring(index);
                    index = json.LastIndexOf(']');
                    json = json.Substring(0, index + 1);
                }
                return json;
            }
        }


        public static AssetBundleVersion GetLatestVersionList(IEnumerable<AssetBundleVersion> list, string platform, string appVersion)
        {
            if (list == null)
                return null;

            AssetBundleVersion lastest = null;

            foreach (var item in list)
            {
                //过滤平台
                if (!string.IsNullOrEmpty(platform) && !string.Equals(platform, item.platform, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                //过滤应用版本
                if (!string.IsNullOrEmpty(appVersion) && appVersion != item.appVersion)
                    continue;

                if (lastest == null || item.IsLatest(lastest))
                    lastest = item;
            }
            return lastest;
        }

        public bool Equals(AssetBundleVersion other)
        {
            if (other == null)
                return false;
            return this.hash == other.hash && appVersion == other.appVersion && bundleCode == other.bundleCode;
        }
    }
}