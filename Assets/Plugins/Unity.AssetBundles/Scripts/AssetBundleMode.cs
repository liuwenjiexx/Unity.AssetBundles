using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityEngine
{
    /// <summary>
    /// 资源加载模式
    /// </summary>
    public enum AssetBundleMode
    {
        /// <summary>
        /// 运行时环境，从 <see cref="LocalManifestUrl"/>加载资源，下载更新的资源到本地
        /// </summary>
        Download,
        /// <summary>
        /// 只对编辑器有效，编辑器模式，不加载 AssetBundle
        /// </summary>
        Editor = 1,
        /// <summary>
        /// 只对编辑器有效，使用打包路径<see cref="BuildManifestUrl"/> 加载 AssetBundle
        /// </summary>
        Build = 2,
    }
}