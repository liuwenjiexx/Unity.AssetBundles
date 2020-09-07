using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityEngine
{

    [Serializable]
    public class AssetBundleSettings
    {
        #region Fields

        //编辑器Build模式加载
        [SerializeField]
        public string buildManifestPath = "AssetBundles/{$Platform}/{$Platform}";
        [SerializeField]
        private string streamingAssetsManifestPath = "AssetBundles/{$Platform}";
        [SerializeField]
        private string localManifestPath = "AssetBundles/{$Channel}/{$Platform}";
        [SerializeField]
        private string downloadManifestPath = "{$Channel}/{$Platform}/{$BundleVersion}/{$Platform}";
        [SerializeField]
        private string downloadUrl;
        [SerializeField]
        private bool requireDownload;


        [SerializeField]
        private string versionFile = "{$Platform}.json";
        [SerializeField]
        private string downloadVersionFile = "{$Channel}/{$Platform}/{$Platform}.json";
        [SerializeField]
        private string appVersionFormat;
        [SerializeField]
        private bool preloadEnabled;
        [SerializeField]
        private string preloadInclude;
        [SerializeField]
        private string preloadExclude;

        [SerializeField]
        private bool cryptoEnabled = false;
        //[SerializeField]
        //private string cryptoKey;
        //[SerializeField]
        //private string cryptoIV;
        [SerializeField]
        private string cryptoInclude;
        [SerializeField]
        private string cryptoExclude;
        [SerializeField]
        private bool signatureEnabled = false;
        [SerializeField]
        private string signaturePublicKey;
        [SerializeField]
        private string signatureInclude;
        [SerializeField]
        private string signatureExclude;

        #endregion

        #region Provider


        private static Internal.SettingsProvider provider;

        private static Internal.SettingsProvider Provider
        {
            get
            {
                if (provider == null)
                    provider = new Internal.SettingsProvider(typeof(AssetBundleSettings), AssetBundles.PackageName, true, true);
                return provider;
            }
        }

        public static AssetBundleSettings Settings { get => (AssetBundleSettings)Provider.Settings; }

        #endregion

        /// <summary>
        /// 生成路径, [Project Directory]/<see cref="BuildManifestPath"/>
        /// </summary>
        public static string BuildManifestPath
        {
            get => Settings.buildManifestPath;
            set => Provider.SetProperty(nameof(BuildManifestPath), ref Settings.buildManifestPath, value);
        }

        /// <summary>
        /// <see cref="Application.streamingAssetsPath"/>/<see cref="StreamingAssetsManifestPath"/>
        /// </summary>
        public static string StreamingAssetsManifestPath
        {
            get => Settings.streamingAssetsManifestPath;
            set => Provider.SetProperty(nameof(StreamingAssetsManifestPath), ref Settings.streamingAssetsManifestPath, value);
        }
        /// <summary>
        /// 本地路径，<see cref="Application.persistentDataPath"/>/<see cref="LocalManifestPath"/>
        /// </summary>
        public static string LocalManifestPath
        {
            get => Settings.localManifestPath;
            set => Provider.SetProperty(nameof(LocalManifestPath), ref Settings.localManifestPath, value);
        }

        //[SerializeField]
        //private string manifestPath = "{$BundleCode}/{$Channel}/{$Platform}";
        /// <summary>
        /// 生成清单路径 [Project Directory]/<see cref="BuildManifestPath"/>/<see cref="ManifestPath"/>
        /// </summary>
        //public static string ManifestPath
        //{
        //    get => Settings.manifestPath;
        //    set => Provider.SetProperty(nameof(ManifestPath), ref Settings.manifestPath, value);
        //}

        /// <summary>
        /// AssetBundle 下载地址，支持下载更新
        /// </summary>
        public static string DownloadUrl
        {
            get => Settings.downloadUrl;
            set => Provider.SetProperty(nameof(DownloadUrl), ref Settings.downloadUrl, value);
        }
        public static string DownloadManifestPath
        {
            get => Settings.downloadManifestPath;
            set => Provider.SetProperty(nameof(DownloadManifestPath), ref Settings.downloadManifestPath, value);
        }

        /// <summary>
        /// 需要检查远程版本
        /// </summary>
        public static bool RequireDownload
        {
            get => Settings.requireDownload;
            set => Provider.SetProperty(nameof(RequireDownload), ref Settings.requireDownload, value);
        }

        [SerializeField]
        private string channel = "Release";
        /// <summary>
        /// 渠道，开发版Develop/封闭式Alpha/开放式Bat/正式版Release
        /// </summary>
        public static string Channel
        {
            get => Settings.channel;
            set => Provider.SetProperty(nameof(Channel), ref Settings.channel, value);
        }


        [SerializeField]
        private string bundleVersion = "1.0.0";
        /// <summary>
        /// 显示版本号
        /// </summary>
        public static string BundleVersion
        {
            get => Settings.bundleVersion;
            set => Provider.SetProperty(nameof(BundleVersion), ref Settings.bundleVersion, value);
        }

        /// <summary>
        /// 下载版本文件名，支持下载更新
        /// </summary>
        public static string VersionFile
        {
            get => Settings.versionFile;
            set => Provider.SetProperty(nameof(VersionFile), ref Settings.versionFile, value);
        }
        /// <summary>
        /// 根版本文件名
        /// </summary>
        public static string DownloadVersionFile
        {
            get => Settings.downloadVersionFile;
            set => Provider.SetProperty(nameof(DownloadVersionFile), ref Settings.downloadVersionFile, value);
        }

        [SerializeField]
        private string releasePath = "AssetBundles/Release";
        public static string ReleasePath
        {
            get => Settings.releasePath;
            set => Provider.SetProperty(nameof(ReleasePath), ref Settings.releasePath, value);
        }

        /// <summary>
        /// 应用版本格式 
        /// version: 1.2.3,  null or {0}.{1}.{2} => 1.2.3; {0}.{1} => 1.2
        /// </summary> 
        public static string AppVersionFormat
        {
            get => Settings.appVersionFormat;
            set => Provider.SetProperty(nameof(AppVersionFormat), ref Settings.appVersionFormat, value);
        }

        #region 预加载

        /// <summary>
        /// 启用预加载
        /// </summary>
        public static bool PreloadEnabled
        {
            get => Settings.preloadEnabled;
            set => Provider.SetProperty(nameof(PreloadEnabled), ref Settings.preloadEnabled, value);
        }
        /// <summary>
        /// 预加载包含
        /// </summary>
        public static string PreloadInclude
        {
            get => Settings.preloadInclude;
            set => Provider.SetProperty(nameof(PreloadInclude), ref Settings.preloadInclude, value);
        }
        /// <summary>
        /// 预加载排除
        /// </summary>
        public static string PreloadExclude
        {
            get => Settings.preloadExclude;
            set => Provider.SetProperty(nameof(PreloadExclude), ref Settings.preloadExclude, value);
        }

        #endregion

        #region 加密

        /// <summary>
        /// 是否启用加密
        /// </summary>
        public static bool CryptoEnabled
        {
            get => Settings.cryptoEnabled;
            set => Provider.SetProperty(nameof(CryptoEnabled), ref Settings.cryptoEnabled, value);
        }

        //private string cryptoKey;
        /// <summary>
        /// 加密Key，安全考虑不保存在配置文件，运行时设置
        /// </summary>
        public static string CryptoKey
        {
            get;set;
            //get => Settings.cryptoKey;
            //set => Provider.SetProperty(nameof(CryptoKey), ref Settings.cryptoKey, value);
        }

        /// <summary>
        /// 加密IV，安全考虑不保存在配置文件，运行时设置
        /// </summary>
        public static string CryptoIV
        {
            get; set;
            //get => Settings.cryptoIV;
            //set => Provider.SetProperty(nameof(CryptoIV), ref Settings.cryptoIV, value);
        }

        /// <summary>
        /// 加密包含 Bundle Name
        /// </summary>
        public static string CryptoInclude
        {
            get => Settings.cryptoInclude;
            set => Provider.SetProperty(nameof(CryptoInclude), ref Settings.cryptoInclude, value);
        }
        /// <summary>
        /// 加密排除 Bundle Name
        /// </summary>
        public static string CryptoExclude
        {
            get => Settings.cryptoExclude;
            set => Provider.SetProperty(nameof(CryptoExclude), ref Settings.cryptoExclude, value);
        }

        #endregion


        #region 签名

        /// <summary>
        /// 是否启用加密
        /// </summary>
        public static bool SignatureEnabled
        {
            get => Settings.signatureEnabled;
            set => Provider.SetProperty(nameof(SignatureEnabled), ref Settings.signatureEnabled, value);
        }

        /// <summary>
        /// 签名公匙
        /// </summary>
        public static string SignaturePublicKey
        {
            get => Settings.signaturePublicKey;
            set => Provider.SetProperty(nameof(SignaturePublicKey), ref Settings.signaturePublicKey, value);
        }
        /// <summary>
        /// 签名包含 Bundle Name
        /// </summary>
        public static string SignatureInclude
        {
            get => Settings.signatureInclude;
            set => Provider.SetProperty(nameof(SignatureInclude), ref Settings.signatureInclude, value);
        }
        /// <summary>
        /// 签名排除
        /// </summary>
        public static string SignatureExclude
        {
            get => Settings.signatureExclude;
            set => Provider.SetProperty(nameof(SignatureExclude), ref Settings.signatureExclude, value);
        }




        #endregion


        #region 组

        [SerializeField]
        private string localGroupName = "local";
        public static string LocalGroupName
        {
            get => Settings.localGroupName;
            set => Provider.SetProperty(nameof(LocalGroupName), ref Settings.localGroupName, value);
        }


        #endregion

    }
}
