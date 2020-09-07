using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityEngine
{

    public enum AssetBundleOptions
    {

        None = 0,

        /// <summary>
        /// 下载StreamingAssets到Local
        /// </summary>
        DownloadStreamingAssetsToLocal = 0x1,

        /// <summary>
        /// LZMA 压缩格式禁止 LoadFromFile，使用Web节省内存
        /// </summary>
        DisableLoadFromFile = 0x2,

        /// <summary>
        /// AssetBundle 名称是否包含Hash, 打包时如果使用 BuildAssetBundleOptions.AppendHashToAssetBundleName 标志位则为 true
        /// </summary>
        AssetBundleNameHasHash = 0x4,
    }


}