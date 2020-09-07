using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityEditor.Build
{
    public interface IPreprocessBuildAssetBundle
    {
        void OnPreprocessBuildAssetBundle(string outputPath, List<AssetBundleBuild> items, ref BuildAssetBundleOptions options);
    }

    public interface IBuildAssetBundleStart
    {
        void BuildAssetBundleStart(string outputPath);
    }

}
