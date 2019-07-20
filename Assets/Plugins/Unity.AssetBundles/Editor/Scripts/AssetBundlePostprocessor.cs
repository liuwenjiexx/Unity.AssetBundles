using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle
{
    internal class AssetBundlePostprocessor : AssetPostprocessor
    {
        private static string[] ignoreDirection = new string[]
        {
        "assets/resources/",
        "assets/streamingassets/"
        };
        private static HashSet<string> ignoreFileExtension = new HashSet<string>()
    {
         ".cs",
         ".meta",
    };

        public static void OnPostprocessAllAssets(string[] importedAsset, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            EditorApplication.delayCall += () =>
            {
                var config = BuildAssetBundles.Config;

                bool changed = false;
                Action onChange = () =>
                {
                    if (!changed)
                    {
                        changed = true;
                        AssetDatabase.StartAssetEditing();
                    }
                };


                foreach (string assetPath in importedAsset.Concat(movedAssets))
                {
                    string assetPathLower = assetPath.ToLower();
                    if (ignoreFileExtension.Contains(Path.GetExtension(assetPathLower)))
                        continue;

                    if (ignoreDirection.Where(o => assetPathLower.StartsWith(o)).FirstOrDefault() != null)
                        continue;

                    if (Directory.Exists(assetPath))
                        continue;
                    if (EditorAssetBundles.Logger.logEnabled)
                        EditorAssetBundles.Logger.Log(EditorAssetBundles.LogTag, "AssetBundlePostprocessor: " + assetPath);
                    if (BuildAssetBundles.UpdateAssetBundleName(assetPath))
                    {
                        onChange();
                    }
                }
                if (changed)
                {
                    AssetDatabase.StopAssetEditing();
                }
            };
        }


    }
}