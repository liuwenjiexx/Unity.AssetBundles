using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityEngine
{
    public enum AssetBundleStatus
    {
        None,
        /// <summary>
        /// 下载中， 可访问下载进度<see cref="AssetBundles.DownloadTotal"/>,<see cref="AssetBundles.DownloadProgress"/>
        /// </summary>
        Downloading,
        /// <summary>
        /// 已下载完成
        /// </summary>
        Downloaded,
        /// <summary>
        /// 预加载中, 可访问加载进度<see cref="AssetBundles.PreloadedTotal"/>,<see cref="AssetBundles.PreloadedProgress"/>
        /// </summary>
        Preloading,
        /// <summary>
        /// 已预加载完成，只有预加载的资源才能使用同步方法, <see cref="LoadAsset"/>
        /// </summary>
        Preloaded,
        /// <summary>
        /// 已初始化完成，可以使用<see cref="AssetBundles"/>.LoadXXX 方法
        /// </summary>
        Initialized,
        /// <summary>
        /// 发生错误
        /// </summary>
        Error,
    }
}
