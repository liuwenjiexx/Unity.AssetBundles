using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityEditor.Build
{
    public interface IPostprocessBuildAssetBundle
    {
        void OnPostprocessBuildAssetBundle(string outputPath, BuildAssetBundleOptions options, AssetBundleManifest manifest, AssetBundleBuild[] items);
    }
}
